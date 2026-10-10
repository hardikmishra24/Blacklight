using System.IO;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using TipShield.Core.Validation;
using Xunit;

namespace TipShield.Core.Tests.Validation;

public class ImageValidatorTests
{
    [Fact]
    public void Validate_Should_Pass_ValidPng()
    {
        using var image = new Image<Rgba32>(100, 100);
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        var bytes = ms.ToArray();

        var result = ImageValidator.Validate(bytes, "image/png");

        result.IsValid.Should().BeTrue();
        result.ActualMimeType.Should().Be("image/png");
        result.Width.Should().Be(100);
        result.Height.Should().Be(100);
        result.CleanImageBytes.Should().NotBeNull();
    }

    [Fact]
    public void Validate_Should_Fail_InvalidMagicBytes()
    {
        var bytes = new byte[] { 0x4D, 0x5A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var result = ImageValidator.Validate(bytes, "image/png");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("magic bytes");
    }

    [Fact]
    public void Validate_Should_Fail_OversizedImage()
    {
        using var image = new Image<Rgba32>(5000, 10);
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        var bytes = ms.ToArray();

        var result = ImageValidator.Validate(bytes, "image/png");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("dimensions exceed");
    }
}

