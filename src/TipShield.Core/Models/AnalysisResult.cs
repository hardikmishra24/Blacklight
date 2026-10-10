using System.Collections.Generic;

namespace TipShield.Core.Models;

public sealed class AnalysisResult
{
    public required int Score { get; init; }
    public required string Verdict { get; init; }
    public required bool AiAvailable { get; init; }
    public List<RuleHit> RuleHits { get; init; } = new();
    public List<RedFlag> RedFlags { get; init; } = new();
    public List<string> Tactics { get; init; } = new();
    public List<MarketCheck> Market { get; init; } = new();
    public List<string> ClaimedRegistrationNumbers { get; init; } = new();
    public string SebiVerifyUrl { get; init; } = "https://www.sebi.gov.in/sebiweb/other/OtherAction.do?doRecognisedFpi=yes&intmId=13";
    public List<string> NextSteps { get; init; } = new();
    public required string Explanation { get; init; }
    public required string Language { get; init; }
    public string Disclaimer { get; init; } = "Educational risk signals only. Not financial advice. A clean result does not mean a tip is safe.";
    public required string RequestId { get; init; }
}

