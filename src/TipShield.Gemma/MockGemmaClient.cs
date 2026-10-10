using TipShield.Core.Models;

namespace TipShield.Gemma;

public sealed class MockGemmaClient : IGemmaClient
{
    public Task<GemmaClassifierResult> ClassifyAsync(
        string? text,
        byte[]? imageBytes,
        string? imageMimeType,
        string outputLanguage,
        CancellationToken cancellationToken = default)
    {
        var input = (text ?? "").ToLowerInvariant();

        // Deterministic mock logic based on content
        int score;
        List<string> tactics;
        List<GemmaRedFlag> redFlags;
        List<string> tickers = new();
        List<string> registrationNumbers = new();
        string explanation;
        List<string> nextSteps;

        if (ContainsScamIndicators(input))
        {
            score = 78;
            tactics = new List<string> { "guaranteed_returns", "urgency", "paid_group_funnel" };
            redFlags = ExtractMockRedFlags(text ?? "", input);
            explanation = outputLanguage == "hi"
                ? "इस संदेश में कई धोखाधड़ी के संकेत हैं जैसे गारंटीड रिटर्न और तत्काल कार्रवाई का दबाव।"
                : "This message contains multiple scam indicators including guaranteed returns and urgency pressure.";
            nextSteps = outputLanguage == "hi"
                ? new List<string> { "कोई भुगतान न करें", "SEBI की वेबसाइट पर सलाहकार की जांच करें", "1930 पर कॉल करें" }
                : new List<string> { "Do not make any payment", "Verify the advisor on SEBI's website", "Call 1930 for cyber fraud helpline" };
        }
        else
        {
            score = 12;
            tactics = new List<string>();
            redFlags = new List<GemmaRedFlag>();
            explanation = outputLanguage == "hi"
                ? "इस संदेश में कोई स्पष्ट धोखाधड़ी के संकेत नहीं मिले।"
                : "No obvious scam indicators found in this message.";
            nextSteps = new List<string> { "Always verify investment advice from multiple sources" };
        }

        // Extract tickers for any input (scam or normal)
        tickers = ExtractTickers(text ?? "");

        var response = new GemmaResponse
        {
            RiskScore = score,
            Tactics = tactics,
            RedFlags = redFlags,
            Tickers = tickers,
            ClaimedRegistrationNumbers = registrationNumbers,
            Language = DetectLanguage(input),
            Explanation = explanation,
            NextSteps = nextSteps
        };

        return Task.FromResult(new GemmaClassifierResult
        {
            Success = true,
            Available = true,
            Response = response
        });
    }

    private static bool ContainsScamIndicators(string input)
    {
        var indicators = new[]
        {
            "guaranteed", "guarantee", "100%", "200%", "double your money",
            "join vip", "premium group", "operator", "insider",
            "last chance", "limited seats", "hurry", "jaldi",
            "paisa double", "guaranteed returns", "pay ₹", "pay rs"
        };
        return indicators.Any(i => input.Contains(i, StringComparison.OrdinalIgnoreCase));
    }

    private static List<GemmaRedFlag> ExtractMockRedFlags(string originalText, string lowerText)
    {
        var flags = new List<GemmaRedFlag>();

        if (lowerText.Contains("guaranteed"))
        {
            var idx = lowerText.IndexOf("guaranteed", StringComparison.Ordinal);
            var end = Math.Min(idx + 40, originalText.Length);
            flags.Add(new GemmaRedFlag
            {
                Quote = originalText[idx..end].Trim(),
                Why = "Claims of guaranteed returns are a hallmark of financial scams"
            });
        }

        if (lowerText.Contains("last chance") || lowerText.Contains("hurry") || lowerText.Contains("jaldi"))
        {
            flags.Add(new GemmaRedFlag
            {
                Quote = FindMatchingSubstring(originalText, lowerText, new[] { "last chance", "hurry", "jaldi" }),
                Why = "Urgency tactics pressure victims into acting without due diligence"
            });
        }

        if (lowerText.Contains("premium group") || lowerText.Contains("vip"))
        {
            flags.Add(new GemmaRedFlag
            {
                Quote = FindMatchingSubstring(originalText, lowerText, new[] { "premium group", "vip group", "vip channel" }),
                Why = "Paid group funnels are a common scam monetization tactic"
            });
        }

        return flags;
    }

    private static string FindMatchingSubstring(string original, string lower, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var idx = lower.IndexOf(pattern, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var end = Math.Min(idx + pattern.Length + 10, original.Length);
                return original[idx..end].Trim();
            }
        }
        return patterns[0];
    }

    private static string DetectLanguage(string input)
    {
        // Simple heuristic
        var hindiChars = input.Count(c => c >= 0x0900 && c <= 0x097F);
        if (hindiChars > input.Length * 0.3) return "hi";

        var hinglishWords = new[] { "bhai", "karo", "hoga", "jaldi", "paisa", "aaj", "abhi", "mein", "hai" };
        if (hinglishWords.Count(w => input.Contains(w, StringComparison.OrdinalIgnoreCase)) >= 2)
            return "hinglish";

        return "en";
    }

    private static readonly HashSet<string> ExcludedWords = new(StringComparer.OrdinalIgnoreCase)
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

    private static List<string> ExtractTickers(string rawText)
    {
        var tickers = new List<string>();
        if (string.IsNullOrWhiteSpace(rawText)) return tickers;

        // 1. Check for known companies / tickers regardless of case
        var words = System.Text.RegularExpressions.Regex.Split(rawText, @"[\s,;.!?:()\/]+");
        foreach (var word in words)
        {
            var cleaned = word.Trim();
            if (string.IsNullOrEmpty(cleaned)) continue;

            if (KnownCompanyTickers.TryGetValue(cleaned, out var ticker))
            {
                if (!tickers.Contains(ticker, StringComparer.OrdinalIgnoreCase))
                {
                    tickers.Add(ticker);
                }
            }
        }

        // 2. Also check for uppercase tickers in the text
        var tickerMatches = System.Text.RegularExpressions.Regex.Matches(rawText, @"\b([A-Z]{2,20})\b");
        foreach (System.Text.RegularExpressions.Match match in tickerMatches)
        {
            var val = match.Value;
            if (!ExcludedWords.Contains(val) && !tickers.Contains(val, StringComparer.OrdinalIgnoreCase))
            {
                tickers.Add(val);
            }
        }

        return tickers.Take(3).ToList();
    }
}
