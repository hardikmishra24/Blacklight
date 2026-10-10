using System.Text.Json;
using TipShield.Core.Models;

namespace TipShield.Api.Services;

public interface IMarketServiceClient
{
    Task<MarketCheck?> GetTickerAsync(string symbol, CancellationToken cancellationToken = default);
    Task<DomainCheck?> GetDomainAsync(string hostname, CancellationToken cancellationToken = default);
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

public sealed class MarketServiceClient : IMarketServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MarketServiceClient> _logger;

    public MarketServiceClient(HttpClient httpClient, ILogger<MarketServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<MarketCheck?> GetTickerAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/ticker/{Uri.EscapeDataString(symbol)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Market service ticker lookup failed: {StatusCode}", (int)response.StatusCode);
                return null;
            }
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<MarketCheck>(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Market service ticker call failed for symbol length {Length}", symbol.Length);
            return null;
        }
    }

    public async Task<DomainCheck?> GetDomainAsync(string hostname, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/domain?host={Uri.EscapeDataString(hostname)}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<DomainCheck>(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Market service domain call failed");
            return null;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
