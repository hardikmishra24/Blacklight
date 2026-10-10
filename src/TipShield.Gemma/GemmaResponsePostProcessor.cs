using System.Text.Json;
using System.Text.RegularExpressions;
using TipShield.Core.Models;

namespace TipShield.Gemma;

public static partial class GemmaResponsePostProcessor
{
    private static readonly HashSet<string> ValidTactics = new(StringComparer.OrdinalIgnoreCase)
    {
        "guaranteed_returns", "unrealistic_gain", "urgency", "fake_authority",
        "insider_claim", "paid_group_funnel", "fake_proof", "pump_language",
        "impersonation", "other"
    };

    private const int MaxQuoteLength = 300;
    private const int MaxWhyLength = 300;
    private const int MaxExplanationLength = 800;
    private const int MaxNextStepLength = 200;
    private const int MaxTickers = 3;

    [GeneratedRegex(@"^[A-Z0-9&\-]{1,20}$")]
    private static partial Regex TickerRegex();

    /// <summary>
    /// Attempts to parse JSON from Gemma output, stripping markdown fences and trailing text.
    /// </summary>
    public static GemmaResponse? TryParseResponse(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
            return null;

        // Strip markdown code fences
        var cleaned = rawOutput.Trim();
        if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[7..];
        else if (cleaned.StartsWith("```"))
            cleaned = cleaned[3..];

        if (cleaned.EndsWith("```"))
            cleaned = cleaned[..^3];

        cleaned = cleaned.Trim();

        // Find the JSON object boundaries
        var startIdx = cleaned.IndexOf('{');
        var endIdx = cleaned.LastIndexOf('}');
        if (startIdx < 0 || endIdx < 0 || endIdx <= startIdx)
            return null;

        var jsonStr = cleaned[startIdx..(endIdx + 1)];

        try
        {
            return JsonSerializer.Deserialize<GemmaResponse>(jsonStr, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Validates and sanitizes a parsed GemmaResponse against the original text.
    /// </summary>
    public static GemmaResponse Sanitize(GemmaResponse response, string? originalText, string? imageText)
    {
        // Clamp risk score
        response.RiskScore = Math.Clamp(response.RiskScore, 0, 100);

        // Whitelist tactics
        response.Tactics = response.Tactics
            .Where(t => ValidTactics.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Validate and filter red flags
        var combinedText = CombineTexts(originalText, imageText);
        response.RedFlags = response.RedFlags
            .Select(rf => ValidateRedFlag(rf, combinedText, imageText != null))
            .Where(rf => rf != null)
            .Select(rf => rf!)
            .ToList();

        // Validate tickers
        response.Tickers = response.Tickers
            .Where(t => TickerRegex().IsMatch(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTickers)
            .ToList();

        // Truncate strings
        response.Explanation = Truncate(response.Explanation, MaxExplanationLength);
        response.NextSteps = response.NextSteps
            .Select(s => Truncate(s, MaxNextStepLength))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        // Validate language
        if (response.Language is not ("en" or "hi" or "hinglish"))
            response.Language = "en";

        return response;
    }

    private static GemmaRedFlag? ValidateRedFlag(GemmaRedFlag flag, string? combinedText, bool hasImageText)
    {
        if (string.IsNullOrWhiteSpace(flag.Quote) || string.IsNullOrWhiteSpace(flag.Why))
            return null;

        flag.Quote = Truncate(flag.Quote, MaxQuoteLength);
        flag.Why = Truncate(flag.Why, MaxWhyLength);

        if (combinedText != null)
        {
            // Whitespace/case-normalized substring check
            var normalizedQuote = NormalizeWhitespace(flag.Quote).ToLowerInvariant();
            var normalizedText = NormalizeWhitespace(combinedText).ToLowerInvariant();

            if (!normalizedText.Contains(normalizedQuote, StringComparison.Ordinal))
            {
                // If we have image text available, mark as unverified rather than dropping
                if (hasImageText)
                {
                    // Keep but mark as unverified (caller will handle)
                    return flag; // The Verified property is on RedFlag, not GemmaRedFlag
                }
                return null; // Drop if quote not found
            }
        }

        return flag;
    }

    private static string? CombineTexts(string? text, string? imageText)
    {
        if (text == null && imageText == null) return null;
        var parts = new List<string>();
        if (text != null) parts.Add(text);
        if (imageText != null) parts.Add(imageText);
        return string.Join(" ", parts);
    }

    private static string NormalizeWhitespace(string input)
    {
        return Regex.Replace(input, @"\s+", " ").Trim();
    }

    private static string Truncate(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Length <= maxLength ? input : input[..maxLength];
    }
}
