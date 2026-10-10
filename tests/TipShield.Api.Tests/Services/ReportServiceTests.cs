using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TipShield.Api.Data;
using TipShield.Api.Services;

namespace TipShield.Api.Tests.Services;

public class ReportServiceTests
{
    private readonly ReportDbContext _db;
    private readonly IConfiguration _config;
    
    public ReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ReportDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new ReportDbContext(options);
        
        var inMemorySettings = new Dictionary<string, string?> {
            {"REPORT_HASH_PEPPER", "test-pepper"}
        };
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task SubmitReport_HashesConsistently()
    {
        var service = new ReportService(_db, _config, NullLogger<ReportService>.Instance);
        
        await service.SubmitReportAsync("phone", "9876543210");
        await service.SubmitReportAsync("phone", "+919876543210");
        
        var count = await _db.Reports.CountAsync();
        Assert.Equal(2, count);
        
        var reports = await _db.Reports.ToListAsync();
        Assert.Equal(reports[0].IdentifierHash, reports[1].IdentifierHash);
    }

    [Fact]
    public async Task SubmitReport_NeverStoresRawValue()
    {
        var service = new ReportService(_db, _config, NullLogger<ReportService>.Instance);
        var rawPhone = "9998887776";
        
        await service.SubmitReportAsync("phone", rawPhone);
        
        var report = await _db.Reports.FirstAsync();
        Assert.NotEqual(rawPhone, report.IdentifierHash);
        Assert.DoesNotContain(rawPhone, report.IdentifierHash);
    }
}
