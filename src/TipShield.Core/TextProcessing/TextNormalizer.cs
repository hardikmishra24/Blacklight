using System;
using System.Text;
using System.Text.RegularExpressions;

namespace TipShield.Core.TextProcessing;

public static class TextNormalizer
{
    private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
    
    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        // 1. Unicode NFKC normalization
        var normalized = input.Normalize(NormalizationForm.FormKC);
        
        // 2. Strip zero-width characters
        normalized = normalized.Replace("\u200B", "")
                               .Replace("\u200C", "")
                               .Replace("\u200D", "")
                               .Replace("\uFEFF", "")
                               .Replace("\u00AD", "");
                               
        // 6. Strip control characters
        normalized = StripControlCharacters(normalized);
        
        // 3. Collapses multiple whitespace to single space
        normalized = WhitespaceRegex.Replace(normalized, " ");
        
        // 4. Trims
        return normalized.Trim();
    }
    
    public static string NormalizeForPatternMatching(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var normalized = Normalize(input);
        
        // 5. Basic leetspeak folding: fold @, $, 3, and only 0/1 within words
        // Fold @ -> a, $ -> s, 3 -> e
        var folded = normalized.Replace("@", "a")
                               .Replace("$", "s")
                               .Replace("3", "e");
        
        // Replace 0 with o in words like j0in, m0ney (not in 10x, 20x, etc.)
        folded = Regex.Replace(folded, @"(?<=[a-zA-Z])0(?=[a-wy-zA-WY-Z])|\b([a-zA-Z]+)0([a-zA-Z]*)", m => m.Value.Replace('0', 'o'));
        folded = Regex.Replace(folded, @"\b([a-zA-Z]*)0([a-zA-Z]+)\b", m => m.Value.Replace('0', 'o'));
        return folded;
    }
    
    public static string StripControlCharacters(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (!char.IsControl(c) || c == '\n' || c == '\t' || c == '\r')
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}

