using FluentAssertions;
using Microsoft.Extensions.Options;
using TipShield.Core.Models;
using TipShield.Core.Scoring;
using Xunit;

namespace TipShield.Core.Tests.Scoring;

public class ScoreAggregatorTests
{
    private readonly ScoreAggregator _aggregator;

    public ScoreAggregatorTests()
    {
        _aggregator = new ScoreAggregator(Options.Create(new ScoreAggregatorOptions()));
    }

    [Fact]
    public void Aggregate_HybridMode_ShouldCalculateWeightedScore()
    {
        var result = _aggregator.Aggregate(
            rulesScore: 50,
            gemmaScore: 60,
            hasSuspiciousTicker: false,
            hasRegistrationClaim: false,
            aiAvailable: true);

        result.FinalScore.Should().Be(48);
        result.Verdict.Should().Be(Verdict.Suspicious);
    }

    [Fact]
    public void Aggregate_AiUnavailable_ShouldCapAt80()
    {
        var result = _aggregator.Aggregate(
            rulesScore: 100,
            gemmaScore: null,
            hasSuspiciousTicker: false,
            hasRegistrationClaim: false,
            aiAvailable: false);

        result.FinalScore.Should().Be(80);
        result.Verdict.Should().Be(Verdict.LikelyScam);
        result.AiUnavailableNote.Should().NotBeNull();
    }

    [Fact]
    public void Aggregate_ShouldApplyRulesFloor()
    {
        var result = _aggregator.Aggregate(
            rulesScore: 75,
            gemmaScore: 0,
            hasSuspiciousTicker: false,
            hasRegistrationClaim: false,
            aiAvailable: true);

        result.FinalScore.Should().Be(35);
        result.Verdict.Should().Be(Verdict.Suspicious);
    }

    [Fact]
    public void Aggregate_ShouldApplyBonuses()
    {
        var result = _aggregator.Aggregate(
            rulesScore: 0,
            gemmaScore: 0,
            hasSuspiciousTicker: true,
            hasRegistrationClaim: true,
            aiAvailable: true);

        result.FinalScore.Should().Be(25);
    }

    [Fact]
    public void Aggregate_ShouldClampTo100()
    {
        var result = _aggregator.Aggregate(
            rulesScore: 100,
            gemmaScore: 100,
            hasSuspiciousTicker: true,
            hasRegistrationClaim: true,
            aiAvailable: true);

        result.FinalScore.Should().Be(100);
    }
}

