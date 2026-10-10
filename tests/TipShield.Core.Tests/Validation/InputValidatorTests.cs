using System.Linq;
using FluentAssertions;
using TipShield.Core.Models;
using TipShield.Core.Validation;
using Xunit;

namespace TipShield.Core.Tests.Validation;

public class InputValidatorTests
{
    [Fact]
    public void ValidateAnalysisRequest_Should_Pass_ValidRequest()
    {
        var request = new AnalysisRequest { Text = "hello", Mode = AnalysisMode.Rules };
        var errors = InputValidator.ValidateAnalysisRequest(request);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAnalysisRequest_Should_Fail_WhenTextAndImageMissing()
    {
        var request = new AnalysisRequest();
        var errors = InputValidator.ValidateAnalysisRequest(request);
        errors.Should().Contain(e => e.Contains("At least one"));
    }

    [Fact]
    public void ValidateAnalysisRequest_Should_Fail_WhenTextTooLong()
    {
        var request = new AnalysisRequest { Text = new string('a', 5001) };
        var errors = InputValidator.ValidateAnalysisRequest(request);
        errors.Should().Contain(e => e.Contains("exceeds maximum length"));
    }

    [Fact]
    public void ValidateAnalysisRequest_Should_Fail_WhenInvalidMimeType()
    {
        var request = new AnalysisRequest { ImageBase64 = "base64", ImageMimeType = "image/gif" };
        var errors = InputValidator.ValidateAnalysisRequest(request);
        errors.Should().Contain(e => e.Contains("Invalid or missing image mime type"));
    }

    [Fact]
    public void ValidateAnalysisRequest_Should_Fail_WhenInvalidLanguage()
    {
        var request = new AnalysisRequest { Text = "text", OutputLanguage = "fr" };
        var errors = InputValidator.ValidateAnalysisRequest(request);
        errors.Should().Contain(e => e.Contains("OutputLanguage must be 'en' or 'hi'"));
    }

    [Theory]
    [InlineData("AAPL", true)]
    [InlineData("A", true)]
    [InlineData("INFY-EQ", true)]
    [InlineData("M&M", true)]
    [InlineData("", false)]
    [InlineData("AAPL AAPL", false)]
    [InlineData("TOOLONGTICKERNAME1234567890", false)]
    public void IsValidTicker_Tests(string ticker, bool expected)
    {
        InputValidator.IsValidTicker(ticker).Should().Be(expected);
    }

    [Theory]
    [InlineData("google.com", true)]
    [InlineData("my-site.co.in", true)]
    [InlineData("localhost", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("example", false)]
    [InlineData("user:pass@google.com", false)]
    [InlineData("google.com:8080", false)]
    [InlineData("google.com/path", false)]
    public void IsValidHostname_Tests(string hostname, bool expected)
    {
        InputValidator.IsValidHostname(hostname).Should().Be(expected);
    }
}

