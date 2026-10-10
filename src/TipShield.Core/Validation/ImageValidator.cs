using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace TipShield.Core.Validation;

public sealed class ImageValidationResult
{
    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public byte[]? CleanImageBytes { get; init; }
    public string? ActualMimeType { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
}

public static class ImageValidator
{
    public static ImageValidationResult Validate(byte[] imageBytes, string declaredMimeType, int maxDimension = 4096)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return new ImageValidationResult { IsValid = false, Error = "Image bytes cannot be empty." };
        }

        string? actualMime = DetectMimeType(imageBytes);
        if (actualMime == null)
        {
            return new ImageValidationResult { IsValid = false, Error = "Invalid magic bytes." };
        }

        try
        {
            using var image = Image.Load<Rgba32>(imageBytes);

            if (image.Frames.Count > 1)
            {
                return new ImageValidationResult { IsValid = false, Error = "Animated images are not supported." };
            }

            if (image.Width > maxDimension || image.Height > maxDimension)
            {
                return new ImageValidationResult { IsValid = false, Error = $"Image dimensions exceed {maxDimension}x{maxDimension}." };
            }

            // Strip metadata
            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IccProfile = null;

            using var ms = new MemoryStream();
            if (actualMime == "image/png")
            {
                image.SaveAsPng(ms);
            }
            else if (actualMime == "image/webp")
            {
                image.SaveAsWebp(ms);
            }
            else
            {
                image.SaveAsJpeg(ms);
                actualMime = "image/jpeg";
            }

            return new ImageValidationResult
            {
                IsValid = true,
                CleanImageBytes = ms.ToArray(),
                ActualMimeType = actualMime,
                Width = image.Width,
                Height = image.Height
            };
        }
        catch (Exception ex)
        {
            return new ImageValidationResult { IsValid = false, Error = $"Failed to parse image: {ex.Message}" };
        }
    }

    private static string? DetectMimeType(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return "image/png";
        
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
            
        if (bytes.Length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return "image/webp";

        return null;
    }
}

