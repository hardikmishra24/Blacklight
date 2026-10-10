using System.Text.Json;

namespace TipShield.Api.Middleware;

public sealed class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogWarning("Bad request: {ErrorClass}", ex.GetType().Name);
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Bad Request",
                status = 400,
                detail = "The request was malformed."
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled");
            context.Response.StatusCode = 499; // Client closed request
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception, ErrorClass={ErrorClass}, RequestId={RequestId}",
                ex.GetType().Name, context.TraceIdentifier);
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Internal Server Error",
                status = 500,
                detail = "An unexpected error occurred. Please try again later."
            });
        }
    }
}
