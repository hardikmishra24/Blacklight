using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TipShield.Core.Models;

/// <summary>
/// A single pattern-based detection rule loaded from rules.json.
/// </summary>
public sealed class Rule
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("weight")]
    public required int Weight { get; init; }

    [JsonPropertyName("pattern")]
    public required string Pattern { get; init; }

    [JsonPropertyName("category")]
    public required string Category { get; init; }
}

public sealed class RuleSet
{
    [JsonPropertyName("rules")]
    public List<Rule> Rules { get; init; } = new();
}

