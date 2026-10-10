using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TipShield.Api.Data;

namespace TipShield.Api.Services;

public interface IReportService
{
    Task<int> SubmitReportAsync(string type, string value, CancellationToken cancellationToken = default);
    Task<ReportLookupResult> LookupAsync(string type, string value, CancellationToken cancellationToken = default);
}

public sealed class ReportLookupResult
{
    public int ReportedCount { get; init; }
    public DateTime? FirstSeen { get; init; }
}

public sealed partial class ReportService : IReportService
{
    private readonly ReportDbContext _db;
    private readonly byte[] _pepper;
    private readonly ILogger<ReportService> _logger;

    public ReportService(ReportDbContext db, IConfiguration configuration, ILogger<ReportService> logger)
    {
        _db = db;
        _logger = logger;
        var pepperStr = configuration["REPORT_HASH_PEPPER"] ?? configuration["ReportHashPepper"]
            ?? throw new InvalidOperationException("REPORT_HASH_PEPPER is required");
        _pepper = Encoding.UTF8.GetBytes(pepperStr);
    }

    public async Task<int> SubmitReportAsync(string type, string value, CancellationToken cancellationToken = default)
    {
        var normalizedValue = NormalizeValue(type, value);
        var hash = ComputeHash(normalizedValue);

        var report = new ReportEntity
        {
            IdentifierHash = hash,
            Type = type,
            ReportedAt = DateTime.UtcNow
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Report submitted, Type={Type}, RequestId={RequestId}", type, Guid.NewGuid().ToString("N")[..8]);

        return await _db.Reports.CountAsync(r => r.IdentifierHash == hash, cancellationToken);
    }

    public async Task<ReportLookupResult> LookupAsync(string type, string value, CancellationToken cancellationToken = default)
    {
        var normalizedValue = NormalizeValue(type, value);
        var hash = ComputeHash(normalizedValue);

        var reports = await _db.Reports
            .Where(r => r.IdentifierHash == hash)
            .OrderBy(r => r.ReportedAt)
            .ToListAsync(cancellationToken);

        return new ReportLookupResult
        {
            ReportedCount = reports.Count,
            FirstSeen = reports.FirstOrDefault()?.ReportedAt
        };
    }

    private string ComputeHash(string normalizedValue)
    {
        var valueBytes = Encoding.UTF8.GetBytes(normalizedValue);
        var hashBytes = HMACSHA256.HashData(_pepper, valueBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    private static string NormalizeValue(string type, string value)
    {
        value = value.Trim().ToLowerInvariant();

        return type.ToLowerInvariant() switch
        {
            "phone" => NormalizePhone(value),
            "handle" => value.TrimStart('@'),
            "domain" => value.TrimEnd('.'),
            _ => value
        };
    }

    [GeneratedRegex(@"[^0-9+]")]
    private static partial Regex NonPhoneChars();

    private static string NormalizePhone(string phone)
    {
        // Strip non-digit chars except +
        phone = NonPhoneChars().Replace(phone, "");
        // Normalize Indian numbers to +91 format
        if (phone.StartsWith("0") && phone.Length == 11)
            phone = "+91" + phone[1..];
        else if (phone.Length == 10 && !phone.StartsWith("+"))
            phone = "+91" + phone;
        else if (phone.StartsWith("91") && phone.Length == 12)
            phone = "+" + phone;
        return phone;
    }
}
