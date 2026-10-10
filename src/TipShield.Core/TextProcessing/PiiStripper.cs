using System;
using System.Text.RegularExpressions;

namespace TipShield.Core.TextProcessing;

public static class PiiStripper
{
    private static readonly Regex PhoneRegex = new Regex(@"(?:\+91[\-\s]?)?[6-9]\d{9}", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new Regex(@"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+", RegexOptions.Compiled);
    private static readonly Regex UpiRegex = new Regex(@"[a-zA-Z0-9.\-_]{3,}@[a-zA-Z]{3,}", RegexOptions.Compiled);
    private static readonly Regex LongDigitsRegex = new Regex(@"\d{7,}", RegexOptions.Compiled);

    public static string StripPii(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var result = input;
        result = PhoneRegex.Replace(result, "[REDACTED]");
        result = EmailRegex.Replace(result, "[REDACTED]");
        result = UpiRegex.Replace(result, "[REDACTED]");
        result = LongDigitsRegex.Replace(result, "[REDACTED]");
        
        return result;
    }
}

