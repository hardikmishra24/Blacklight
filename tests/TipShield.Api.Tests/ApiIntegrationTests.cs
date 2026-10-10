using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TipShield.Api.Data;
using TipShield.Core.Models;
using TipShield.Gemma;

namespace TipShield.Api.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string ApiKey = "test-api-key";

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["API_KEY"] = ApiKey,
                    ["REPORT_HASH_PEPPER"] = "test-pepper",
                    ["DATABASE_PATH"] = ":memory:" // Use in-memory SQLite
                });
            });

            builder.ConfigureServices(services =>
            {
                // Replace IGemmaClient with MockGemmaClient
                services.RemoveAll<IGemmaClient>();
                services.AddSingleton<IGemmaClient, MockGemmaClient>();
            });
        });
    }

    [Fact]
    public async Task Health_ReturnsOk_WithoutAuth()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Analyze_ReturnsUnauthorized_WithoutApiKey()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/analyze", new AnalysisRequest { Text = "hello" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_ReturnsUnauthorized_WithInvalidApiKey()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");
        var response = await client.PostAsJsonAsync("/api/analyze", new AnalysisRequest { Text = "hello" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_ReturnsOk_WithValidApiKey()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        var response = await client.PostAsJsonAsync("/api/analyze", new AnalysisRequest { Text = "100% guaranteed returns buy now" });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AnalysisResult>();
        Assert.NotNull(result);
        Assert.True(result.Score > 0);
    }

    [Fact]
    public async Task Analyze_RejectsTextOver5000Chars()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        var text = new string('a', 5001);
        var response = await client.PostAsJsonAsync("/api/analyze", new AnalysisRequest { Text = text });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_RequiresTextOrImage()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        var response = await client.PostAsJsonAsync("/api/analyze", new AnalysisRequest());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reports_SubmitAndLookup()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        
        // Submit
        var submitRes = await client.PostAsJsonAsync("/api/reports", new { Type = "phone", Value = "9876543210" });
        submitRes.EnsureSuccessStatusCode();
        
        // Lookup
        var lookupRes = await client.GetAsync("/api/reports/lookup?type=phone&value=9876543210");
        lookupRes.EnsureSuccessStatusCode();
        var doc = await JsonDocument.ParseAsync(await lookupRes.Content.ReadAsStreamAsync());
        var count = doc.RootElement.GetProperty("reportedCount").GetInt32();
        Assert.True(count >= 1);
    }

    [Fact]
    public async Task FamilyCard_GeneratesCard()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        
        var response = await client.PostAsJsonAsync("/api/family-card", new { Score = 90, Verdict = "likely_scam", TopRedFlags = new[] { "flag1" }, Language = "en" });
        response.EnsureSuccessStatusCode();
        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var card = doc.RootElement.GetProperty("card").GetString();
        Assert.Contains("TipShield Warning", card);
    }
}
