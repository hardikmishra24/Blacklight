using TipShield.Core.Models;

namespace TipShield.Gemma;

public interface IGemmaClient
{
    Task<GemmaClassifierResult> ClassifyAsync(
        string? text,
        byte[]? imageBytes,
        string? imageMimeType,
        string outputLanguage,
        CancellationToken cancellationToken = default);
}

public sealed class GemmaClassifierResult
{
    public bool Success { get; init; }
    public bool Available { get; init; } = true;
    public GemmaResponse? Response { get; init; }
    public string? Error { get; init; }
}
