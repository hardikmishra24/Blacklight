using System.Collections.Generic;
using System.Text.RegularExpressions;
using TipShield.Core.Models;

namespace TipShield.Core.Validation;

public static class InputValidator
{
    private static readonly Regex TickerRegex = new Regex(@"^[A-Z0-9&\-]{1,20}$", RegexOptions.Compiled);

    public static List<string> ValidateAnalysisRequest(AnalysisRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Text) && string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            errors.Add("At least one of Text or ImageBase64 must be provided.");
        }

        if (!string.IsNullOrWhiteSpace(request.Text) && request.Text.Length > 5000)
        {
            errors.Add("Text exceeds maximum length of 5000 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            if (request.ImageBase64.Length > 5600000)
            {
                errors.Add("Image exceeds maximum size.");
            }

            var mime = ValidateImageMimeType(request.ImageMimeType ?? "");
            if (mime == null)
            {
                errors.Add("Invalid or missing image mime type. Must be png, jpeg, or webp.");
            }
        }

        if (request.OutputLanguage != "en" && request.OutputLanguage != "hi")
        {
            errors.Add("OutputLanguage must be 'en' or 'hi'.");
        }

        if (!System.Enum.IsDefined(typeof(AnalysisMode), request.Mode))
        {
            errors.Add("Invalid Mode specified.");
        }

        return errors;
    }

    public static bool IsValidTicker(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker)) return false;
        return TickerRegex.IsMatch(ticker);
    }

    public static bool IsValidHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return false;
        
        if (hostname.Equals("localhost", System.StringComparison.OrdinalIgnoreCase)) return false;

        if (System.Net.IPAddress.TryParse(hostname, out _)) return false;
        
        if (!hostname.Contains('.')) return false;
        
        if (hostname.Contains('@') || hostname.Contains(':') || hostname.Contains('/') || hostname.Contains('\\')) return false;

        return System.Text.RegularExpressions.Regex.IsMatch(hostname, @"^[a-zA-Z0-9.-]+$");
    }

    public static string? ValidateImageMimeType(string mimeType)
    {
        var lower = mimeType?.ToLowerInvariant();
        return lower switch
        {
            "image/png" or "png" => "image/png",
            "image/jpeg" or "image/jpg" or "jpg" or "jpeg" => "image/jpeg",
            "image/webp" or "webp" => "image/webp",
            _ => null
        };
    }
}

