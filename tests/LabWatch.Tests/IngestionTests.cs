using System.Text.Json;
using LabWatch.Core;
using LabWatch.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LabWatch.Tests;

public sealed class IngestionTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"labwatch-tests-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Repeated_snapshot_is_idempotent_but_audits_each_run()
    {
        var factory = Factory();
        await SeedAsset(factory);
        var service = Service(factory, Dataset(ValidationTests.Candidate("A")), Dataset(ValidationTests.Candidate("A")));
        await service.RunAsync("fixture", "c_test", ["https://example.test"], CancellationToken.None);
        await service.RunAsync("fixture", "c_test", ["https://example.test"], CancellationToken.None);

        await using var db = await factory.CreateDbContextAsync(CancellationToken.None);
        Assert.Equal(2, await db.CollectorRuns.CountAsync(CancellationToken.None));
        Assert.Equal(1, await db.Advisories.CountAsync(CancellationToken.None));
        Assert.Equal(1, await db.Assessments.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Quarantined_snapshot_does_not_replace_last_good_data()
    {
        var factory = Factory();
        await SeedAsset(factory);
        var service = Service(factory, Dataset(ValidationTests.Candidate("A")), EmptyDataset());
        var accepted = await service.RunAsync("fixture", "c_test", ["https://example.test"], CancellationToken.None);
        var rejected = await service.RunAsync("fixture", "c_test", ["https://example.test"], CancellationToken.None);

        await using var db = await factory.CreateDbContextAsync(CancellationToken.None);
        Assert.Equal(CollectorRunStatus.Accepted, accepted.Run.Status);
        Assert.Equal(CollectorRunStatus.Quarantined, rejected.Run.Status);
        Assert.Equal(1, await db.Advisories.CountAsync(CancellationToken.None));
        Assert.Equal("j_1", (await db.Advisories.SingleAsync(CancellationToken.None)).SnapshotId);
    }

    private TestDbFactory Factory() => new(_databasePath);

    private static IngestionService Service(TestDbFactory factory, params JsonElement[] datasets) => new(
        factory, new FakeBrightDataClient(datasets), new AdvisoryJsonParser(), new SnapshotValidator(), new ImpactMatcher());

    private static async Task SeedAsset(TestDbFactory factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        if (!await db.Assets.AnyAsync())
        {
            db.Assets.Add(new LabAsset { AssetTag = "LAB-1", Manufacturer = "Siemens", Product = "Atellica Data Manager", Model = "ADM", Version = "1.2", Department = "Core", Criticality = "High" });
            await db.SaveChangesAsync();
        }
    }

    private static JsonElement Dataset(AdvisoryCandidate candidate) => JsonSerializer.SerializeToElement(new[]
    {
        new { source_id = candidate.SourceId, source_url = candidate.SourceUrl, title = candidate.Title,
            published_at = candidate.PublishedAt, manufacturer = candidate.Manufacturer, products = candidate.Products,
            models = candidate.Models, affected_versions = candidate.AffectedVersions, cves = candidate.Cves,
            severity = candidate.Severity, summary = candidate.Summary, mitigations = candidate.Mitigations }
    });
    private static JsonElement EmptyDataset() => JsonSerializer.SerializeToElement(Array.Empty<object>());

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }

    private sealed class TestDbFactory(string path) : IDbContextFactory<LabWatchDbContext>
    {
        private readonly DbContextOptions<LabWatchDbContext> _options = new DbContextOptionsBuilder<LabWatchDbContext>().UseSqlite($"Data Source={path}").Options;
        public LabWatchDbContext CreateDbContext() => new(_options);
        public Task<LabWatchDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class FakeBrightDataClient(IEnumerable<JsonElement> datasets) : IBrightDataClient
    {
        private readonly Queue<JsonElement> _datasets = new(datasets);
        private int _run;
        public Task<string> TriggerAsync(string collectorId, IReadOnlyList<string> urls, CancellationToken cancellationToken) => Task.FromResult($"j_{++_run}");
        public Task<JsonElement> PollDatasetAsync(string snapshotId, CancellationToken cancellationToken) => Task.FromResult(_datasets.Dequeue());
    }
}
