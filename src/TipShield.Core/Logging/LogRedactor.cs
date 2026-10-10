using System.Text.RegularExpressions;
using TipShield.Core.TextProcessing;

namespace TipShield.Core.Logging;

public static class LogRedactor
{
    private static readonly Regex ApiKeyRegex = new Regex(@"[A-Za-z0-9-_]{21,}", RegexOptions.Compiled);

    public static string Redact(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var stripped = PiiStripper.StripPii(input);
        return ApiKeyRegex.Replace(stripped, "[REDACTED_TOKEN]");
    }
}

