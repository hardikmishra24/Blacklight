using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TipShield.Core.Models;
using System.Net.Http.Json;

namespace TipShield.Bot;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    // In-memory rate limiting per chat: chatId -> timestamps of recent requests
    private readonly Dictionary<long, List<DateTime>> _rateLimits = new();
    // Concurrency limit per chat: lock per chat ID
    private readonly HashSet<long> _activeChats = new();
    private readonly object _lock = new();

    public Worker(ILogger<Worker> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("TipShieldApi");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = _configuration["TELEGRAM_BOT_TOKEN"] ?? Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("TELEGRAM_BOT_TOKEN is not set or empty. Telegram Bot worker will idle without crashing.");
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
            return;
        }

        var botClient = new TelegramBotClient(token);
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        _logger.LogInformation("Starting Telegram bot polling...");

        botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error: [{apiRequestException.ErrorCode}] {apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError("Telegram Polling Error: {ErrorMessage}", errorMessage);
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message) return;
        
        // Ignore messages from channels and bots
        if (message.ForwardFromChat?.Type == ChatType.Channel || message.From?.IsBot == true) return;

        var chatId = message.Chat.Id;
        var chatHash = HashChatId(chatId);
        var requestId = Guid.NewGuid().ToString("N")[..8];

        _logger.LogInformation("Received Telegram update. ChatHash={ChatHash}, RequestId={RequestId}", chatHash, requestId);

        // Per-chat rate limit: 5 requests/minute & concurrency 1
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (!_rateLimits.TryGetValue(chatId, out var timestamps))
            {
                timestamps = new List<DateTime>();
                _rateLimits[chatId] = timestamps;
            }

            timestamps.RemoveAll(t => now - t > TimeSpan.FromMinutes(1));
            if (timestamps.Count >= 5)
            {
                _logger.LogWarning("Rate limit exceeded for chat {ChatHash}", chatHash);
                _ = botClient.SendTextMessageAsync(chatId, "⚠️ Rate limit exceeded. Please wait a minute before submitting again.", cancellationToken: cancellationToken);
                return;
            }

            if (_activeChats.Contains(chatId))
            {
                _logger.LogWarning("Concurrency limit hit for chat {ChatHash}", chatHash);
                _ = botClient.SendTextMessageAsync(chatId, "⏳ Please wait for your previous analysis to complete.", cancellationToken: cancellationToken);
                return;
            }

            timestamps.Add(now);
            _activeChats.Add(chatId);
        }

        try
        {
            if (message.Text is { } text)
            {
                if (text.StartsWith("/start"))
                {
                    await botClient.SendTextMessageAsync(chatId, 
                        "🛡️ *Welcome to TipShield!*\n\nForward or paste any suspicious stock tip, investment message, or screenshot here to check for scam indicators.\n\nType /help for instructions.\n\n_Disclaimer: Educational risk signals only. Not financial advice. A clean result does not mean a tip is safe._",
                        parseMode: ParseMode.MarkdownV2, cancellationToken: cancellationToken);
                    return;
                }

                if (text.StartsWith("/help"))
                {
                    await botClient.SendTextMessageAsync(chatId,
                        "ℹ️ *TipShield Help*\n\n1\\. Forward or type a stock message/tip directly into this chat\\.\n2\\. Send a screenshot of the tip\\.\n3\\. Get an instant risk evaluation, top red flags, and market check\\.\n\nNational Cybercrime helpline: *1930*",
                        parseMode: ParseMode.MarkdownV2, cancellationToken: cancellationToken);
                    return;
                }

                // Normal message analysis
                await AnalyzeAndReplyAsync(botClient, chatId, text, null, null, cancellationToken);
            }
            else if (message.Photo is { Length: > 0 } photos)
            {
                // Download largest photo
                var largestPhoto = photos.MaxBy(p => p.FileSize ?? 0);
                if (largestPhoto != null && (largestPhoto.FileSize ?? 0) <= 4 * 1024 * 1024)
                {
                    var file = await botClient.GetFileAsync(largestPhoto.FileId, cancellationToken);
                    using var stream = new MemoryStream();
                    await botClient.DownloadFileAsync(file.FilePath!, stream, cancellationToken);
                    var bytes = stream.ToArray();
                    var base64 = Convert.ToBase64String(bytes);

                    await AnalyzeAndReplyAsync(botClient, chatId, message.Caption, base64, "image/jpeg", cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "⚠️ Photo exceeds the 4 MB size limit.", cancellationToken: cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Telegram message RequestId={RequestId}", requestId);
            await botClient.SendTextMessageAsync(chatId, "❌ An error occurred during analysis. Please try again later.", cancellationToken: cancellationToken);
        }
        finally
        {
            lock (_lock)
            {
                _activeChats.Remove(chatId);
            }
        }
    }

    private async Task AnalyzeAndReplyAsync(
        ITelegramBotClient botClient,
        long chatId,
        string? text,
        string? imageBase64,
        string? imageMimeType,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            text,
            imageBase64,
            imageMimeType,
            outputLanguage = "en"
        };

        var response = await _httpClient.PostAsJsonAsync("/api/analyze", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Failed to complete analysis from API.", cancellationToken: cancellationToken);
            return;
        }

        var result = await response.Content.ReadFromJsonAsync<AnalysisResult>(cancellationToken: cancellationToken);
        if (result == null) return;

        var verdictEmoji = result.Score switch
        {
            >= 65 => "🚨",
            >= 35 => "⚠️",
            _ => "✅"
        };

        var sb = new StringBuilder();
        sb.AppendLine($"{verdictEmoji} *TipShield Assessment: {EscapeMarkdown(result.Verdict.ToUpper())}*");
        sb.AppendLine($"*Risk Score:* `{result.Score}/100`\n");

        if (result.RedFlags.Count > 0)
        {
            sb.AppendLine("*Top Red Flags:*");
            foreach (var flag in result.RedFlags.Take(3))
            {
                sb.AppendLine($"• \"_{EscapeMarkdown(flag.Quote)}_\" — {EscapeMarkdown(flag.Why)}");
            }
            sb.AppendLine();
        }

        if (result.Market.Count > 0)
        {
            sb.AppendLine("*Market Reality Check:*");
            foreach (var market in result.Market)
            {
                var icon = market.Suspicious ? "⚠️" : "ℹ️";
                sb.AppendLine($"{icon} *{EscapeMarkdown(market.Symbol)}*: Close ₹{market.LastClose}, Vol Spike: {market.VolumeSpikeX:F1}x");
            }
            sb.AppendLine();
        }

        if (result.NextSteps.Count > 0)
        {
            sb.AppendLine("*Next Steps:*");
            foreach (var step in result.NextSteps.Take(3))
            {
                sb.AppendLine($"• {EscapeMarkdown(step)}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"_{EscapeMarkdown(result.Disclaimer)}_");

        await botClient.SendTextMessageAsync(chatId, sb.ToString(), parseMode: ParseMode.MarkdownV2, cancellationToken: cancellationToken);
    }

    private static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var specialChars = new[] { "_", "*", "[", "]", "(", ")", "~", "`", ">", "#", "+", "-", "=", "|", "{", "}", ".", "!" };
        foreach (var c in specialChars)
        {
            text = text.Replace(c, "\\" + c);
        }
        return text;
    }

    private static string HashChatId(long chatId)
    {
        var bytes = Encoding.UTF8.GetBytes(chatId.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash)[..12];
    }
}
