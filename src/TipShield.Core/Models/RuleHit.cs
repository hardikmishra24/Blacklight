namespace TipShield.Core.Models;

public sealed class RuleHit
{
    public required string RuleId { get; init; }
    public required string Description { get; init; }
    public required int Weight { get; init; }
    public required string MatchedText { get; init; }
    public required int StartIndex { get; init; }
    public required int Length { get; init; }
}

