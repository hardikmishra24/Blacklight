using FluentAssertions;
using TipShield.Core.TextProcessing;
using Xunit;

namespace TipShield.Core.Tests.TextProcessing;

public class TextNormalizerTests
{
    [Fact]
    public void Normalize_Should_HandleNullOrEmpty()
    {
        TextNormalizer.Normalize(null!).Should().BeNull();
        TextNormalizer.Normalize("").Should().Be("");
    }

    [Fact]
    public void Normalize_Should_RemoveZeroWidthCharacters()
    {
        var input = "hello\u200Bworld\uFEFF!";
        TextNormalizer.Normalize(input).Should().Be("helloworld!");
    }

    [Fact]
    public void Normalize_Should_CollapseWhitespace()
    {
        var input = "  too   many    spaces\nand\ttabs  ";
        TextNormalizer.Normalize(input).Should().Be("too many spaces and tabs");
    }

    [Fact]
    public void NormalizeForPatternMatching_Should_FoldLeetspeak()
    {
        var input = "j0in @nd m@k3 m0ney!";
        TextNormalizer.NormalizeForPatternMatching(input).Should().Be("join and make money!");
    }

    [Fact]
    public void StripControlCharacters_Should_RemoveControlsButKeepNewlines()
    {
        var input = "line1\x0Bline2\nline3\tline4\x07";
        TextNormalizer.StripControlCharacters(input).Should().Be("line1line2\nline3\tline4");
    }
}

