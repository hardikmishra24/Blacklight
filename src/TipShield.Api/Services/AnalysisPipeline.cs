using TipShield.Core.Models;
using TipShield.Core.Rules;
using TipShield.Core.Scoring;
using TipShield.Core.TextProcessing;
using TipShield.Core.Validation;
using TipShield.Gemma;

namespace TipShield.Api.Services;

public interface IAnalysisPipeline
{
    Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken = default);
}

public sealed class AnalysisPipeline : IAnalysisPipeline
{
    private readonly IRuleEngine _ruleEngine;
    private readonly IGemmaClient _gemmaClient;
    private readonly IScoreAggregator _scoreAggregator;
    private readonly IMarketServiceClient _marketService;
    private readonly ILogger<AnalysisPipeline> _logger;

    private const string Disclaimer = "Educational risk signals only. Not financial advice. A clean result does not mean a tip is safe.";

    public AnalysisPipeline(
        IRuleEngine ruleEngine,
        IGemmaClient gemmaClient,
        IScoreAggregator scoreAggregator,
        IMarketServiceClient marketService,
        ILogger<AnalysisPipeline> logger)
    {
        _ruleEngine = ruleEngine;
        _gemmaClient = gemmaClient;
        _scoreAggregator = scoreAggregator;
        _marketService = marketService;
        _logger = logger;
    }

    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N")[..12];
        _logger.LogInformation("Starting analysis, RequestId={RequestId}, TextLength={TextLength}, HasImage={HasImage}, Mode={Mode}",
            requestId, request.Text?.Length ?? 0, request.ImageBase64 != null, request.Mode);

        // 1. Normalize text
        var normalizedText = request.Text != null ? TextNormalizer.Normalize(request.Text) : null;

        // 2. Process image if present
        byte[]? cleanImageBytes = null;
        string? actualMimeType = null;
        if (request.ImageBase64 != null)
        {
            var imageBytes = Convert.FromBase64String(request.ImageBase64);
            var imageResult = ImageValidator.Validate(imageBytes, request.ImageMimeType ?? "image/png");
            if (!imageResult.IsValid)
            {
                return CreateErrorResult(requestId, request.OutputLanguage, $"Image validation failed: {imageResult.Error}");
            }
            cleanImageBytes = imageResult.CleanImageBytes;
            actualMimeType = imageResult.ActualMimeType;
        }

        // 3. Rule engine evaluation
        RuleEngineResult? ruleResult = null;
        if (request.Mode != AnalysisMode.Gemma && normalizedText != null)
        {
            ruleResult = _ruleEngine.Evaluate(normalizedText);
            _logger.LogInformation("Rules evaluated, RequestId={RequestId}, Score={Score}, Hits={HitCount}",
                requestId, ruleResult.Score, ruleResult.Hits.Count);
        }

        // 4. Gemma classification
        GemmaClassifierResult? gemmaResult = null;
        if (request.Mode != AnalysisMode.Rules)
        {
            gemmaResult = await _gemmaClient.ClassifyAsync(
                normalizedText, cleanImageBytes, actualMimeType,
                request.OutputLanguage, cancellationToken);

            _logger.LogInformation("Gemma classification complete, RequestId={RequestId}, Available={Available}, Success={Success}",
                requestId, gemmaResult.Available, gemmaResult.Success);
        }

        // 5. Collect tickers for market check
        var tickers = new List<string>();
        if (gemmaResult?.Response?.Tickers != null)
            tickers.AddRange(gemmaResult.Response.Tickers);

        if (normalizedText != null)
        {
            foreach (var dt in ExtractDirectTickers(normalizedText))
            {
                if (!tickers.Contains(dt, StringComparer.OrdinalIgnoreCase))
                {
                    tickers.Add(dt);
                }
            }
        }

        // Validate and deduplicate tickers
        tickers = tickers
            .Where(InputValidator.IsValidTicker)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        // 6. Market checks (parallel, with timeouts)
        var marketChecks = new List<MarketCheck>();
        if (tickers.Count > 0)
        {
            using var marketCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            marketCts.CancelAfter(TimeSpan.FromSeconds(15));

            var marketTasks = tickers.Select(t => _marketService.GetTickerAsync(t, marketCts.Token));
            var results = await Task.WhenAll(marketTasks);
            // Only include tickers that were actually found on the exchange
            marketChecks = results.Where(r => r != null && r.Found).Select(r => r!).ToList();
        }

        // 7. Score aggregation
        var hasSuspiciousTicker = marketChecks.Any(m => m.Suspicious);
        var hasRegistrationClaim = gemmaResult?.Response?.ClaimedRegistrationNumbers?.Count > 0;
        var aiAvailable = gemmaResult?.Available ?? false;

        var rulesScore = ruleResult?.Score ?? 0;
        var gemmaScore = gemmaResult?.Response?.RiskScore;

        if (request.Mode == AnalysisMode.Rules)
        {
            gemmaScore = null;
            aiAvailable = false;
        }
        else if (request.Mode == AnalysisMode.Gemma)
        {
            rulesScore = 0;
        }

        var aggregated = _scoreAggregator.Aggregate(
            rulesScore, gemmaScore, hasSuspiciousTicker, hasRegistrationClaim, aiAvailable);

        // 8. Build red flags list
        var redFlags = new List<RedFlag>();
        if (gemmaResult?.Response?.RedFlags != null)
        {
            redFlags = gemmaResult.Response.RedFlags.Select(rf => new RedFlag
            {
                Quote = rf.Quote,
                Why = rf.Why,
                Verified = true // Already verified by post-processor
            }).ToList();
        }

        // 9. Build response
        var result = new AnalysisResult
        {
            Score = aggregated.FinalScore,
            Verdict = aggregated.Verdict.ToDisplayString(),
            AiAvailable = aggregated.AiAvailable,
            RuleHits = ruleResult?.Hits ?? new List<RuleHit>(),
            RedFlags = redFlags,
            Tactics = gemmaResult?.Response?.Tactics ?? new List<string>(),
            Market = marketChecks,
            ClaimedRegistrationNumbers = gemmaResult?.Response?.ClaimedRegistrationNumbers ?? new List<string>(),
            NextSteps = BuildNextSteps(gemmaResult?.Response, request.OutputLanguage),
            Explanation = gemmaResult?.Response?.Explanation ?? BuildFallbackExplanation(aggregated, request.OutputLanguage),
            Language = gemmaResult?.Response?.Language ?? (request.OutputLanguage == "hi" ? "hi" : "en"),
            RequestId = requestId
        };

        _logger.LogInformation("Analysis complete, RequestId={RequestId}, Score={Score}, Verdict={Verdict}",
            requestId, result.Score, result.Verdict);

        return result;
    }

    private static List<string> BuildNextSteps(GemmaResponse? gemmaResponse, string language)
    {
        if (gemmaResponse?.NextSteps?.Count > 0)
            return gemmaResponse.NextSteps;

        return language == "hi"
            ? new List<string>
            {
                "कोई भुगतान न करें",
                "SEBI की आधिकारिक वेबसाइट पर सलाहकार की पुष्टि करें",
                "cybercrime.gov.in पर रिपोर्ट करें",
                "वित्तीय साइबर-धोखाधड़ी हेल्पलाइन 1930 पर कॉल करें",
                "वर्तमान पोर्टल विवरण स्वयं सत्यापित करें"
            }
            : new List<string>
            {
                "Do not make any payment",
                "Verify the advisor on SEBI's official intermediary search page",
                "Report at cybercrime.gov.in",
                "Call 1930 for financial cyber-fraud helpline",
                "Verify current portal details independently"
            };
    }

    private static string BuildFallbackExplanation(AggregatedScore aggregated, string language)
    {
        var note = aggregated.AiUnavailableNote != null ? $" {aggregated.AiUnavailableNote}" : "";

        if (language == "hi")
        {
            return aggregated.Verdict switch
            {
                Verdict.LikelyScam => $"नियम-आधारित विश्लेषण में कई धोखाधड़ी के संकेत मिले।{note}",
                Verdict.Suspicious => $"कुछ संदिग्ध संकेत पाए गए। सावधानी बरतें।{note}",
                _ => $"कोई स्पष्ट खतरे के संकेत नहीं मिले।{note}"
            };
        }

        return aggregated.Verdict switch
        {
            Verdict.LikelyScam => $"Rule-based analysis found multiple scam indicators.{note}",
            Verdict.Suspicious => $"Some suspicious indicators were found. Exercise caution.{note}",
            _ => $"No obvious red flags detected.{note}"
        };
    }

    private static AnalysisResult CreateErrorResult(string requestId, string language, string error)
    {
        return new AnalysisResult
        {
            Score = 0,
            Verdict = Verdict.NoObviousRedFlags.ToDisplayString(),
            AiAvailable = false,
            Explanation = error,
            Language = language == "hi" ? "hi" : "en",
            RequestId = requestId
        };
    }

    private static readonly HashSet<string> ExcludedTickerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "VIP", "GUARANTEED", "JOIN", "ALERT", "CALL", "BUY", "SELL", "NOW", "FREE", "URGENT",
        "JACKPOT", "BLAST", "SUPER", "PROFIT", "TARGET", "STOPLOSS", "NSE", "BSE", "SEBI", "NIFTY",
        "SENSEX", "TODAY", "TOMORROW", "MOON", "THE", "AND", "FOR", "TELEGRAM", "WHATSAPP", "GROUP",
        "SURE", "SHOT", "TIP", "TIPS", "MONEY", "CASH", "GAIN", "GAINS", "RETURN", "RETURNS",
        "SHARE", "STOCK", "PRICE", "TRADE", "TRADING", "TRADER", "POINTS", "HIGH", "LOW", "LEVEL",
        "INVEST", "INVESTMENT", "EARN", "EARNING", "WEEK", "DAY", "DAYS", "FAST", "QUICK", "SAFE",
        "RISK", "OFFER", "BEST", "TOP", "NEWS", "INFO", "DAILY", "REAL", "GOOD", "VERY", "WILL",
        "WITH", "FROM", "THAT", "THIS", "THEN", "WHEN", "WHAT", "WHERE", "WHO", "HOW", "WHY", "HOLD",
        "YES", "JUST", "LOOK", "SEEN", "VIEW", "POST", "OPEN", "CLOSE", "EXIT", "ENTRY"
    };

    private static readonly Dictionary<string, string> KnownCompanyTickers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RELIANCE"] = "RELIANCE",
        ["TCS"] = "TCS",
        ["INFOSYS"] = "INFY",
        ["INFY"] = "INFY",
        ["HDFC"] = "HDFCBANK",
        ["HDFCBANK"] = "HDFCBANK",
        ["ICICI"] = "ICICIBANK",
        ["ICICIBANK"] = "ICICIBANK",
        ["SBI"] = "SBIN",
        ["SBIN"] = "SBIN",
        ["STATEBANK"] = "SBIN",
        ["ITC"] = "ITC",
        ["WIPRO"] = "WIPRO",
        ["TATAMOTORS"] = "TATAMOTORS",
        ["TATASTEEL"] = "TATASTEEL",
        ["TATAPOWER"] = "TATAPOWER",
        ["TATACHEM"] = "TATACHEM",
        ["ZOMATO"] = "ZOMATO",
        ["PAYTM"] = "PAYTM",
        ["YESBANK"] = "YESBANK",
        ["ADANI"] = "ADANIENT",
        ["ADANIENT"] = "ADANIENT",
        ["ADANIPORTS"] = "ADANIPORTS",
        ["ADANIPOWER"] = "ADANIPOWER",
        ["MARUTI"] = "MARUTI",
        ["BAJFINANCE"] = "BAJFINANCE",
        ["BAJAJFINANCE"] = "BAJFINANCE",
        ["BHARTIARTL"] = "BHARTIARTL",
        ["AIRTEL"] = "BHARTIARTL",
        ["KOTAK"] = "KOTAKBANK",
        ["KOTAKBANK"] = "KOTAKBANK",
        ["AXIS"] = "AXISBANK",
        ["AXISBANK"] = "AXISBANK",
        ["LT"] = "LT",
        ["LARSEN"] = "LT",
        ["HUL"] = "HINDUNILVR",
        ["HINDUNILVR"] = "HINDUNILVR",
        ["IRFC"] = "IRFC",
        ["BHEL"] = "BHEL",
        ["HAL"] = "HAL",
        ["BEL"] = "BEL",
        ["NTPC"] = "NTPC",
        ["ONGC"] = "ONGC",
        ["COALINDIA"] = "COALINDIA",
        ["SUZLON"] = "SUZLON",
        ["IOC"] = "IOC",
        ["POWERGRID"] = "POWERGRID",
        ["TITAN"] = "TITAN",
        ["ULTRACEMCO"] = "ULTRACEMCO",
        ["ASIANPAINT"] = "ASIANPAINT",
        ["NESTLEIND"] = "NESTLEIND",
        ["JSWSTEEL"] = "JSWSTEEL",
        ["SUNPHARMA"] = "SUNPHARMA",
        ["CIPLA"] = "CIPLA",
        ["DIVISLAB"] = "DIVISLAB",
        ["DRREDDY"] = "DRREDDY",
        ["VEDL"] = "VEDL",
        ["TRENT"] = "TRENT",
        ["POLYCAB"] = "POLYCAB",
        ["IDEA"] = "IDEA",
        ["PNB"] = "PNB"
    };

    private static List<string> ExtractDirectTickers(string text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return list;

        var words = System.Text.RegularExpressions.Regex.Split(text, @"[\s,;.!?:()\/]+");
        foreach (var word in words)
        {
            var cleaned = word.Trim();
            if (string.IsNullOrEmpty(cleaned)) continue;

            if (KnownCompanyTickers.TryGetValue(cleaned, out var ticker))
            {
                if (!list.Contains(ticker, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(ticker);
                }
            }
        }

        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\b([A-Z]{2,20})\b");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var val = match.Value;
            if (!ExcludedTickerWords.Contains(val) && !list.Contains(val, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(val);
            }
        }

        return list;
    }
}
