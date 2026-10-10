using System.Security.Cryptography;
using System.Text;

namespace TipShield.Api.Auth;

public sealed class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly byte[][] _validKeys;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        var keyString = configuration["API_KEY"] ?? configuration["ApiKey"] ?? "";
        _validKeys = keyString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => Encoding.UTF8.GetBytes(k))
            .ToArray();

        if (_validKeys.Length == 0 || _validKeys.Any(k => k.Length == 0))
        {
            throw new InvalidOperationException("API_KEY environment variable is required and must not be empty or a placeholder.");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.EndsWith("/health"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var providedKey) ||
            string.IsNullOrWhiteSpace(providedKey))
        {
            _logger.LogWarning("API request without X-Api-Key header, RequestId={RequestId}", context.TraceIdentifier);
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Unauthorized",
                status = 401,
                detail = "Missing or invalid X-Api-Key header."
            });
            return;
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedKey.ToString());
        var isValid = _validKeys.Any(validKey =>
            validKey.Length == providedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(validKey, providedBytes));

        if (!isValid)
        {
            _logger.LogWarning("Invalid API key provided, RequestId={RequestId}", context.TraceIdentifier);
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Unauthorized",
                status = 401,
                detail = "Missing or invalid X-Api-Key header."
            });
            return;
        }

        await _next(context);
    }
}
