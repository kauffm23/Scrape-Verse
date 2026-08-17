using LabWatch.Core;
using Microsoft.EntityFrameworkCore;

namespace LabWatch.Infrastructure;

public sealed class LabWatchDbContext(DbContextOptions<LabWatchDbContext> options) : DbContext(options)
{
    public DbSet<Advisory> Advisories => Set<Advisory>();
    public DbSet<LabAsset> Assets => Set<LabAsset>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<CollectorRun> CollectorRuns => Set<CollectorRun>();
    public DbSet<HealingEvent> HealingEvents => Set<HealingEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Advisory>().HasIndex(x => x.SourceId).IsUnique();
        modelBuilder.Entity<LabAsset>().HasIndex(x => x.AssetTag).IsUnique();
        modelBuilder.Entity<Assessment>().HasIndex(x => new { x.AdvisoryId, x.AssetId }).IsUnique();
        modelBuilder.Entity<CollectorRun>().HasIndex(x => x.SnapshotId);
        modelBuilder.Entity<Advisory>().Ignore(x => x.ProductList).Ignore(x => x.ModelList).Ignore(x => x.VersionList).Ignore(x => x.CveList);
    }
}
