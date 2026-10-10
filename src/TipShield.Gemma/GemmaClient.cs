using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TipShield.Core.Models;

namespace TipShield.Gemma;

public sealed class GemmaClient : IGemmaClient
{
    private readonly HttpClient _httpClient;
    private readonly GemmaOptions _options;
    private readonly ILogger<GemmaClient> _logger;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly SemaphoreSlim _semaphore;

    public GemmaClient(
        HttpClient httpClient,
        IOptions<GemmaOptions> options,
        ILogger<GemmaClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _circuitBreaker = new CircuitBreaker(
            _options.CircuitBreakerFailureThreshold,
            TimeSpan.FromSeconds(_options.CircuitBreakerResetSeconds));
        _semaphore = new SemaphoreSlim(_options.MaxConcurrent, _options.MaxConcurrent);
    }

    public async Task<GemmaClassifierResult> ClassifyAsync(
        string? text,
        byte[]? imageBytes,
        string? imageMimeType,
        string outputLanguage,
        CancellationToken cancellationToken = default)
    {
        if (!_circuitBreaker.IsAvailable)
        {
            _logger.LogWarning("Gemma circuit breaker is open, skipping AI classification");
            return new GemmaClassifierResult { Success = false, Available = false, Error = "Service temporarily unavailable" };
        }

        if (!await _semaphore.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            _logger.LogWarning("Gemma concurrency limit reached");
            return new GemmaClassifierResult { Success = false, Available = false, Error = "Too many concurrent requests" };
        }

        try
        {
            return await ClassifyWithRetriesAsync(text, imageBytes, imageMimeType, outputLanguage, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<GemmaClassifierResult> ClassifyWithRetriesAsync(
        string? text, byte[]? imageBytes, string? imageMimeType,
        string outputLanguage, CancellationToken cancellationToken)
    {
        var systemInstruction = Prompts.ClassifierPrompt.GetSystemInstruction(outputLanguage);
        var userMessage = Prompts.ClassifierPrompt.BuildUserMessage(text, null); // image text extraction handled separately

        for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                // Exponential backoff with jitter
                var baseDelay = Math.Pow(2, attempt) * 1000;
                var jitter = Random.Shared.Next(0, (int)(baseDelay * 0.5));
                await Task.Delay((int)baseDelay + jitter, cancellationToken);
            }

            try
            {
                var url = $"{_options.BaseUrl}/models/{_options.ModelId}:generateContent";

                var requestBody = BuildRequestBody(systemInstruction, userMessage, imageBytes, imageMimeType);
                var jsonContent = JsonSerializer.Serialize(requestBody);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                request.Headers.Add("x-goog-api-key", _options.ApiKey);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

                using var response = await _httpClient.SendAsync(request, cts.Token);

                if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500)
                {
                    _logger.LogWarning("Gemma API returned {StatusCode}, attempt {Attempt}/{MaxRetries}",
                        (int)response.StatusCode, attempt + 1, _options.MaxRetries + 1);

                    if (attempt == _options.MaxRetries)
                    {
                        _circuitBreaker.RecordFailure();
                        return new GemmaClassifierResult
                        {
                            Success = false,
                            Available = false,
                            Error = $"API returned {(int)response.StatusCode} after {_options.MaxRetries + 1} attempts"
                        };
                    }
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
                var gemmaResponse = ParseApiResponse(responseJson);

                if (gemmaResponse == null)
                {
                    if (attempt == _options.MaxRetries)
                    {
                        _circuitBreaker.RecordFailure();
                        return new GemmaClassifierResult { Success = false, Available = false, Error = "Failed to parse Gemma response" };
                    }
                    continue;
                }

                var sanitized = GemmaResponsePostProcessor.Sanitize(gemmaResponse, text, null);
                _circuitBreaker.RecordSuccess();

                return new GemmaClassifierResult
                {
                    Success = true,
                    Available = true,
                    Response = sanitized
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Gemma API call failed, attempt {Attempt}/{MaxRetries}",
                    attempt + 1, _options.MaxRetries + 1);

                if (attempt == _options.MaxRetries)
                {
                    _circuitBreaker.RecordFailure();
                    return new GemmaClassifierResult { Success = false, Available = false, Error = "API communication error" };
                }
            }
        }

        return new GemmaClassifierResult { Success = false, Available = false, Error = "Unexpected retry exhaustion" };
    }

    private object BuildRequestBody(string systemInstruction, string userMessage, byte[]? imageBytes, string? imageMimeType)
    {
        var parts = new List<object>();

        // Add image part if present
        if (imageBytes != null && imageMimeType != null)
        {
            parts.Add(new
            {
                inline_data = new
                {
                    mime_type = imageMimeType,
                    data = Convert.ToBase64String(imageBytes)
                }
            });
        }

        // Add text part
        parts.Add(new { text = userMessage });

        var body = new Dictionary<string, object>
        {
            ["system_instruction"] = new { parts = new[] { new { text = systemInstruction } } },
            ["contents"] = new[] { new { parts } },
            ["generationConfig"] = new Dictionary<string, object>
            {
                ["temperature"] = _options.Temperature,
                ["responseMimeType"] = "application/json"
            }
        };

        if (!_options.Thinking)
        {
            // Thinking mode off - add thinkingConfig
            body["generationConfig"] = new Dictionary<string, object>
            {
                ["temperature"] = _options.Temperature,
                ["responseMimeType"] = "application/json",
                ["thinkingConfig"] = new { thinkingBudget = 0 }
            };
        }

        return body;
    }

    private GemmaResponse? ParseApiResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var contentParts) &&
                    contentParts.GetArrayLength() > 0)
                {
                    var textPart = contentParts[0];
                    if (textPart.TryGetProperty("text", out var textElement))
                    {
                        var rawText = textElement.GetString();
                        if (rawText != null)
                        {
                            return GemmaResponsePostProcessor.TryParseResponse(rawText);
                        }
                    }
                }
            }

            _logger.LogWarning("Unexpected Gemini API response structure");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gemini API response");
            return null;
        }
    }
}
