using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TipShield.Core.Models;

public sealed class DomainCheck
{
    [JsonPropertyName("hostname")]
    public required string Hostname { get; init; }

    [JsonPropertyName("creation_date")]
    public string? CreationDate { get; init; }

    [JsonPropertyName("age_days")]
    public int? AgeDays { get; init; }

    [JsonPropertyName("registrar")]
    public string? Registrar { get; init; }

    [JsonPropertyName("lookalike_of")]
    public string? LookalikeOf { get; init; }

    [JsonPropertyName("suspicious")]
    public bool Suspicious { get; init; }

    [JsonPropertyName("reasons")]
    public List<string> Reasons { get; init; } = new();
}

