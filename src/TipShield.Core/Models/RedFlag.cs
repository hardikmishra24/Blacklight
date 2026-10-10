namespace TipShield.Core.Models;

public sealed class RedFlag
{
    public required string Quote { get; init; }
    public required string Why { get; init; }
    public bool Verified { get; init; } = true;
}

