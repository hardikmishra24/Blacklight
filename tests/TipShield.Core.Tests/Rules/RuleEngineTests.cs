using System.IO;
using FluentAssertions;
using TipShield.Core.Rules;
using Xunit;

namespace TipShield.Core.Tests.Rules;

public class RuleEngineTests
{
    private readonly RuleEngine _engine;

    public RuleEngineTests()
    {
        var json = File.ReadAllText(GetRulesFilePath());
        _engine = new RuleEngine(json);
    }

    private static string GetRulesFilePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "TipShield.Core", "Rules", "rules.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate src/TipShield.Core/Rules/rules.json from test base directory.");
    }

    [Theory]
    [InlineData("We provide guaranteed 50% returns on investment.", true)]
    [InlineData("We guarantee quality service for all.", false)]
    [InlineData("Get 10x returns in 5 days with this trick", true)]
    [InlineData("The company grew 10x over 10 years", false)]
    [InlineData("Hurry, last chance to join!", true)]
    [InlineData("The last quarter results are out", false)]
    [InlineData("Our operator is buying this stock now", true)]
    [InlineData("Contact your network operator", false)]
    [InlineData("Join our premium VIP group for tips", true)]
    [InlineData("We offer premium quality products", false)]
    [InlineData("Please pay \u20b95000 to join", true)]
    [InlineData("My monthly salary is \u20b950000", false)]
    [InlineData("Share your OTP for verification", true)]
    [InlineData("OTP verification successful", false)]
    [InlineData("Join my channel on t.me/stocktips", true)]
    [InlineData("Visit our website for info", false)]
    [InlineData("paisa double scheme", true)]
    public void Evaluate_Should_MatchCorrectly(string text, bool shouldMatch)
    {
        var result = _engine.Evaluate(text);
        if (shouldMatch)
        {
            result.Hits.Should().NotBeEmpty();
            result.Score.Should().BeGreaterThan(0);
        }
        else
        {
            result.Hits.Should().BeEmpty();
            result.Score.Should().Be(0);
        }
    }

    [Fact]
    public void Evaluate_Should_CalculateScoreCorrectly()
    {
        var result = _engine.Evaluate("guaranteed returns and 10x returns in 5 days");
        result.Score.Should().BeGreaterThan(0);
        result.Hits.Should().HaveCount(2);
    }

    [Fact]
    public void Evaluate_Should_CapScoreAt100()
    {
        var result = _engine.Evaluate("guaranteed returns 10x returns in 5 days share your otp operator is buying pay \u20b95000 to join");
        result.Score.Should().Be(100);
    }

    [Fact]
    public void Evaluate_Should_NotReDos()
    {
        var longText = new string('a', 100000);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = _engine.Evaluate(longText);
        stopwatch.Stop();
        
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000);
        result.Score.Should().Be(0);
    }
}
