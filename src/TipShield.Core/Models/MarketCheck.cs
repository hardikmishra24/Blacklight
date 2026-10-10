using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TipShield.Core.Models;

public sealed class MarketCheck
{
    [JsonPropertyName("symbol")]
    public required string Symbol { get; init; }

    [JsonPropertyName("found")]
    public bool Found { get; init; }

    [JsonPropertyName("last_close")]
    public decimal? LastClose { get; init; }

    [JsonPropertyName("volume_spike_x")]
    public double? VolumeSpikeX { get; init; }

    [JsonPropertyName("market_cap")]
    public decimal? MarketCap { get; init; }

    [JsonPropertyName("week_change_pct")]
    public double? WeekChangePct { get; init; }

    [JsonPropertyName("month_change_pct")]
    public double? MonthChangePct { get; init; }

    [JsonPropertyName("liquidity_flag")]
    public string? LiquidityFlag { get; init; }

    [JsonPropertyName("suspicious")]
    public bool Suspicious { get; init; }

    [JsonPropertyName("reasons")]
    public List<string> Reasons { get; init; } = new();

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

