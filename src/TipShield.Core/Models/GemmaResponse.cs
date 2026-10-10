using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TipShield.Core.Models;

/// <summary>
/// Represents the parsed and validated response from the Gemma classifier.
/// </summary>
public sealed class GemmaResponse
{
    [JsonPropertyName("risk_score")]
    public int RiskScore { get; set; }

    [JsonPropertyName("tactics")]
    public List<string> Tactics { get; set; } = new();

    [JsonPropertyName("red_flags")]
    public List<GemmaRedFlag> RedFlags { get; set; } = new();

    [JsonPropertyName("tickers")]
    public List<string> Tickers { get; set; } = new();

    [JsonPropertyName("claimed_registration_numbers")]
    public List<string> ClaimedRegistrationNumbers { get; set; } = new();

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;

    [JsonPropertyName("next_steps")]
    public List<string> NextSteps { get; set; } = new();
}

public sealed class GemmaRedFlag
{
    [JsonPropertyName("quote")]
    public string Quote { get; set; } = string.Empty;

    [JsonPropertyName("why")]
    public string Why { get; set; } = string.Empty;
}

