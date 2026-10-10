using Microsoft.EntityFrameworkCore;

namespace TipShield.Api.Data;

public class ReportDbContext : DbContext
{
    public ReportDbContext(DbContextOptions<ReportDbContext> options) : base(options) { }

    public DbSet<ReportEntity> Reports => Set<ReportEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReportEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IdentifierHash);
            entity.Property(e => e.IdentifierHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(20).IsRequired();
        });
    }
}

public class ReportEntity
{
    public int Id { get; set; }
    public required string IdentifierHash { get; set; }
    public required string Type { get; set; }
    public DateTime ReportedAt { get; set; }
}
