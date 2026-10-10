namespace TipShield.Core.Models;

public sealed class AnalysisRequest
{
    public string? Text { get; init; }
    public string? ImageBase64 { get; init; }
    public string? ImageMimeType { get; init; }
    public string OutputLanguage { get; init; } = "en";
    public AnalysisMode Mode { get; init; } = AnalysisMode.Hybrid;
}

public enum AnalysisMode
{
    Hybrid,
    Rules,
    Gemma
}

