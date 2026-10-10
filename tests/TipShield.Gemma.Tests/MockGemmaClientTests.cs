using TipShield.Gemma;
using Xunit;

namespace TipShield.Gemma.Tests;

public class MockGemmaClientTests
{
    [Fact]
    public async Task ScamMessageReturnsHighScore()
    {
        var client = new MockGemmaClient();
        var result = await client.ClassifyAsync("Hurry up and join VIP group for guaranteed 100% returns on YESBANK!", null, null, "en");
        
        Assert.True(result.Success);
        Assert.True(result.Available);
        Assert.NotNull(result.Response);
        Assert.Equal(78, result.Response.RiskScore);
        Assert.Contains("guaranteed_returns", result.Response.Tactics);
        Assert.Contains("YESBANK", result.Response.Tickers);
        Assert.Equal("en", result.Response.Language);
    }

    [Fact]
    public async Task LegitimateMessageReturnsLowScore()
    {
        var client = new MockGemmaClient();
        var result = await client.ClassifyAsync("HDFC Bank reported a profit of 10%.", null, null, "en");
        
        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Equal(12, result.Response.RiskScore);
        Assert.Empty(result.Response.Tactics);
    }

    [Fact]
    public async Task HinglishDetectionWorks()
    {
        var client = new MockGemmaClient();
        var result = await client.ClassifyAsync("Bhai jaldi paisa lagao", null, null, "en");
        
        Assert.NotNull(result.Response);
        Assert.Equal("hinglish", result.Response.Language);
    }

    [Fact]
    public async Task HindiDetectionWorks()
    {
        var client = new MockGemmaClient();
        var result = await client.ClassifyAsync("जल्दी पैसा लगाओ", null, null, "en");
        
        Assert.NotNull(result.Response);
        Assert.Equal("hi", result.Response.Language);
    }
}
