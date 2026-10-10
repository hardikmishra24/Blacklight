using System;
using Microsoft.Extensions.Options;
using TipShield.Core.Models;

namespace TipShield.Core.Scoring;

public interface IScoreAggregator
{
    AggregatedScore Aggregate(
        int rulesScore,
        int? gemmaScore,
        bool hasSuspiciousTicker,
        bool hasRegistrationClaim,
        bool aiAvailable);
}

public sealed class AggregatedScore
{
    public required int FinalScore { get; init; }
    public required Verdict Verdict { get; init; }
    public required bool AiAvailable { get; init; }
    public string? AiUnavailableNote { get; init; }
}

public sealed class ScoreAggregatorOptions
{
    public double RulesWeight { get; set; } = 0.35;
    public double GemmaWeight { get; set; } = 0.50;
    public int SuspiciousTickerBonus { get; set; } = 15;
    public int RegistrationClaimBonus { get; set; } = 10;
    public int RulesOnlyMaxScore { get; set; } = 80;
    public int RulesFloorThreshold { get; set; } = 70;
}

public sealed class ScoreAggregator : IScoreAggregator
{
    private readonly ScoreAggregatorOptions _options;

    public ScoreAggregator(IOptions<ScoreAggregatorOptions> options)
    {
        _options = options.Value;
    }

    public AggregatedScore Aggregate(
        int rulesScore,
        int? gemmaScore,
        bool hasSuspiciousTicker,
        bool hasRegistrationClaim,
        bool aiAvailable)
    {
        int finalScore;
        string? note = null;

        if (aiAvailable && gemmaScore.HasValue)
        {
            double baseScore = (_options.RulesWeight * rulesScore) + (_options.GemmaWeight * gemmaScore.Value);
            
            if (hasSuspiciousTicker) baseScore += _options.SuspiciousTickerBonus;
            if (hasRegistrationClaim) baseScore += _options.RegistrationClaimBonus;

            finalScore = (int)Math.Round(baseScore);
        }
        else
        {
            int baseScore = rulesScore;
            if (hasSuspiciousTicker) baseScore += _options.SuspiciousTickerBonus;
            if (hasRegistrationClaim) baseScore += _options.RegistrationClaimBonus;
            
            finalScore = Math.Min(baseScore, _options.RulesOnlyMaxScore);
            note = "AI score unavailable. Score calculated using rules and bonuses only.";
        }

        if (rulesScore >= _options.RulesFloorThreshold && finalScore < 35)
        {
            finalScore = 35;
        }

        finalScore = Math.Clamp(finalScore, 0, 100);

        return new AggregatedScore
        {
            FinalScore = finalScore,
            Verdict = VerdictExtensions.FromScore(finalScore),
            AiAvailable = aiAvailable,
            AiUnavailableNote = note
        };
    }
}

