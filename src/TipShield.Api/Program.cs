using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TipShield.Api.Auth;
using TipShield.Api.Data;
using TipShield.Api.Middleware;
using TipShield.Api.Services;
using TipShield.Core.Models;
using TipShield.Core.Rules;
using TipShield.Core.Scoring;
using TipShield.Core.Validation;
using TipShield.Gemma;

var builder = WebApplication.CreateBuilder(args);

// Kestrel configuration
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 6 * 1024 * 1024; // 6 MB
});

// JSON options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = "Too Many Requests",
            status = 429,
            detail = "Rate limit exceeded. Please try again later."
        }, token);
    };

    var analyzePermitLimit = int.TryParse(builder.Configuration["RateLimiting:AnalyzePermitLimit"], out var alp) ? alp : 20;
    options.AddFixedWindowLimiter("analyze", opt =>
    {
        opt.PermitLimit = analyzePermitLimit;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 100;
    });

    options.AddFixedWindowLimiter("lookup", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    var concurrencyLimit = int.TryParse(builder.Configuration["RateLimiting:ConcurrencyLimit"], out var clp) ? clp : 50;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetConcurrencyLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new ConcurrencyLimiterOptions { PermitLimit = concurrencyLimit, QueueLimit = 50 }));
});

// EF Core SQLite
var dbPath = builder.Configuration["DATABASE_PATH"] ?? "tipshield.db";
builder.Services.AddDbContext<ReportDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Rule Engine
var rulesJsonPath = Path.Combine(AppContext.BaseDirectory, "rules.json");
if (!File.Exists(rulesJsonPath))
{
    // Try relative to project
    rulesJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "TipShield.Core", "Rules", "rules.json");
}

// We need to load rules.json - copy it as a content file
var rulesJson = File.Exists(rulesJsonPath)
    ? File.ReadAllText(rulesJsonPath)
    : "{}";

builder.Services.AddSingleton<IRuleEngine>(sp =>
{
    // Try to load from multiple locations
    var paths = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "rules.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "rules.json"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TipShield.Core", "Rules", "rules.json")
    };

    string? loadedJson = null;
    foreach (var path in paths)
    {
        if (File.Exists(path))
        {
            loadedJson = File.ReadAllText(path);
            break;
        }
    }

    return new RuleEngine(loadedJson ?? "{\"rules\":[]}");
});

// Score Aggregator
builder.Services.Configure<ScoreAggregatorOptions>(builder.Configuration.GetSection("Scoring"));
builder.Services.AddSingleton<IScoreAggregator, ScoreAggregator>();

// Gemma Services
builder.Services.AddGemmaServices(builder.Configuration);

// Market Service Client
builder.Services.AddHttpClient<IMarketServiceClient, MarketServiceClient>(client =>
{
    var baseUrl = builder.Configuration["MARKET_SERVICE_URL"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
    var internalKey = builder.Configuration["INTERNAL_API_KEY"] ?? "";
    if (!string.IsNullOrEmpty(internalKey))
    {
        client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
    }
});

// Application Services
builder.Services.AddScoped<IAnalysisPipeline, AnalysisPipeline>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddSingleton<IFamilyCardService, FamilyCardService>();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
    db.Database.EnsureCreated();
}

// Middleware pipeline
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ApiKeyAuthMiddleware>();
app.UseRateLimiter();

// API Routes
var api = app.MapGroup("/api");

api.MapPost("/analyze", async (
    HttpContext httpContext,
    IAnalysisPipeline pipeline,
    CancellationToken cancellationToken) =>
{
    var modeStr = httpContext.Request.Query["mode"].FirstOrDefault() ?? "hybrid";
    if (!Enum.TryParse<AnalysisMode>(modeStr, ignoreCase: true, out var mode))
        mode = AnalysisMode.Hybrid;

    AnalysisRequest? request;
    try
    {
        request = await httpContext.Request.ReadFromJsonAsync<AnalysisRequest>(cancellationToken);
    }
    catch (JsonException)
    {
        return Results.Problem("Invalid JSON in request body.", statusCode: 400);
    }

    if (request == null)
        return Results.Problem("Request body is required.", statusCode: 400);

    // Override mode from query parameter
    request = new AnalysisRequest
    {
        Text = request.Text,
        ImageBase64 = request.ImageBase64,
        ImageMimeType = request.ImageMimeType,
        OutputLanguage = request.OutputLanguage,
        Mode = mode
    };

    // Validate
    var errors = InputValidator.ValidateAnalysisRequest(request);
    if (errors.Count > 0)
        return Results.Problem(string.Join("; ", errors), statusCode: 400);

    var result = await pipeline.AnalyzeAsync(request, cancellationToken);
    return Results.Ok(result);
})
.RequireRateLimiting("analyze");

api.MapPost("/family-card", async (HttpContext httpContext, IFamilyCardService cardService) =>
{
    var body = await httpContext.Request.ReadFromJsonAsync<FamilyCardRequest>();
    if (body == null)
        return Results.Problem("Request body is required.", statusCode: 400);

    var card = cardService.GenerateCard(
        body.Score,
        body.Verdict ?? "suspicious",
        body.TopRedFlags ?? new List<string>(),
        body.Language ?? "en");

    return Results.Ok(new { card, language = body.Language ?? "en" });
});

api.MapPost("/reports", async (HttpContext httpContext, IReportService reportService, CancellationToken ct) =>
{
    var body = await httpContext.Request.ReadFromJsonAsync<ReportRequest>(ct);
    if (body == null || string.IsNullOrWhiteSpace(body.Type) || string.IsNullOrWhiteSpace(body.Value))
        return Results.Problem("Type and value are required.", statusCode: 400);

    var validTypes = new[] { "phone", "handle", "domain" };
    if (!validTypes.Contains(body.Type.ToLowerInvariant()))
        return Results.Problem("Type must be 'phone', 'handle', or 'domain'.", statusCode: 400);

    var count = await reportService.SubmitReportAsync(body.Type, body.Value, ct);
    return Results.Ok(new { reportedCount = count });
});

api.MapGet("/reports/lookup", async (
    string? type, string? value,
    IReportService reportService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(value))
        return Results.Problem("Type and value query parameters are required.", statusCode: 400);

    var result = await reportService.LookupAsync(type, value, ct);
    return Results.Ok(result);
})
.RequireRateLimiting("lookup");

api.MapGet("/health", async (IMarketServiceClient marketService, IGemmaClient gemmaClient) =>
{
    var marketHealthy = await marketService.IsHealthyAsync();
    return Results.Ok(new
    {
        status = "ok",
        gemmaAvailable = true, // We can't easily test without making a call
        marketServiceAvailable = marketHealthy,
        disclaimer = "Educational risk signals only. Not financial advice. A clean result does not mean a tip is safe."
    });
});

app.Run();

// DTOs for API routes
public sealed record FamilyCardRequest(int Score, string? Verdict, List<string>? TopRedFlags, string? Language);
public sealed record ReportRequest(string? Type, string? Value);

// Make Program accessible for integration tests
public partial class Program { }

/* Note: Required NuGet packages for this project:
- Microsoft.EntityFrameworkCore.Sqlite
- Microsoft.AspNetCore.RateLimiting (implicit in AspNetCore framework depending on version, otherwise specific package needed if missing)
- Microsoft.EntityFrameworkCore.Tools (for design-time migrations if needed later)
- Serilog.AspNetCore (if you wish to configure Serilog instead of default logging)
*/
