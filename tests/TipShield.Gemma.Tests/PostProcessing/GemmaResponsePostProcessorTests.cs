using TipShield.Core.Models;
using TipShield.Gemma;
using Xunit;

namespace TipShield.Gemma.Tests.PostProcessing;

public class GemmaResponsePostProcessorTests
{
    [Fact]
    public void TryParseResponse_ValidJson()
    {
        var json = "{\"risk_score\": 50, \"language\": \"en\"}";
        var result = GemmaResponsePostProcessor.TryParseResponse(json);
        Assert.NotNull(result);
        Assert.Equal(50, result.RiskScore);
        Assert.Equal("en", result.Language);
    }

    [Fact]
    public void TryParseResponse_MarkdownFencedJson()
    {
        var json = "```json\n{\"risk_score\": 75}\n```";
        var result = GemmaResponsePostProcessor.TryParseResponse(json);
        Assert.NotNull(result);
        Assert.Equal(75, result.RiskScore);
    }

    [Fact]
    public void TryParseResponse_TrailingText()
    {
        var json = "{\"risk_score\": 25} \n Some trailing thought process text";
        var result = GemmaResponsePostProcessor.TryParseResponse(json);
        Assert.NotNull(result);
        Assert.Equal(25, result.RiskScore);
    }

    [Fact]
    public void TryParseResponse_InvalidJson_ReturnsNull()
    {
        var json = "{\"risk_score\": }";
        var result = GemmaResponsePostProcessor.TryParseResponse(json);
        Assert.Null(result);
    }

    [Fact]
    public void TryParseResponse_EmptyString_ReturnsNull()
    {
        var result = GemmaResponsePostProcessor.TryParseResponse("");
        Assert.Null(result);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(150, 100)]
    [InlineData(50, 50)]
    public void Sanitize_ClampsScore(int inputScore, int expectedScore)
    {
        var response = new GemmaResponse { RiskScore = inputScore };
        var sanitized = GemmaResponsePostProcessor.Sanitize(response, "text", null);
        Assert.Equal(expectedScore, sanitized.RiskScore);
    }

    [Fact]
    public void Sanitize_WhitelistsTactics()
    {
        var response = new GemmaResponse 
        { 
            Tactics = new List<string> { "guaranteed_returns", "invalid_tactic", "urgency" } 
        };
        var sanitized = GemmaResponsePostProcessor.Sanitize(response, "text", null);
        Assert.Contains("guaranteed_returns", sanitized.Tactics);
        Assert.Contains("urgency", sanitized.Tactics);
        Assert.DoesNotContain("invalid_tactic", sanitized.Tactics);
    }

    [Fact]
    public void Sanitize_DropsRedFlagsNotInText()
    {
        var response = new GemmaResponse
        {
            RedFlags = new List<GemmaRedFlag>
            {
                new GemmaRedFlag { Quote = "guaranteed returns", Why = "scam" },
                new GemmaRedFlag { Quote = "not in text", Why = "fake" }
            }
        };
        var sanitized = GemmaResponsePostProcessor.Sanitize(response, "this message promises guaranteed returns today", null);
        
        Assert.Single(sanitized.RedFlags);
        Assert.Equal("guaranteed returns", sanitized.RedFlags[0].Quote);
    }

    [Fact]
    public void Sanitize_ValidatesTickers()
    {
        var response = new GemmaResponse
        {
            Tickers = new List<string> { "RELIANCE", "INVALID!", "TCS", "INFY", "WIPRO" } // 4 valid
        };
        var sanitized = GemmaResponsePostProcessor.Sanitize(response, "text", null);
        
        Assert.Equal(3, sanitized.Tickers.Count); // Max 3
        Assert.Contains("RELIANCE", sanitized.Tickers);
        Assert.DoesNotContain("INVALID!", sanitized.Tickers);
    }

    [Fact]
    public void Sanitize_ValidatesLanguage()
    {
        var response = new GemmaResponse { Language = "fr" };
        var sanitized = GemmaResponsePostProcessor.Sanitize(response, "text", null);
        Assert.Equal("en", sanitized.Language);
    }
}
