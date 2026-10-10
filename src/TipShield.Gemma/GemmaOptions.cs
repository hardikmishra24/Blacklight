using System.ComponentModel.DataAnnotations;

namespace TipShield.Gemma;

public sealed class GemmaOptions
{
    public const string SectionName = "Gemma";

    public string ApiKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = "gemma-4-31b-it";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public bool Mock { get; set; }

    public bool Thinking { get; set; }

    public double Temperature { get; set; } = 0.2;

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRetries { get; set; } = 2;

    public int MaxConcurrent { get; set; } = 4;

    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    public int CircuitBreakerResetSeconds { get; set; } = 60;
}
