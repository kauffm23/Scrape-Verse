using LabWatch.Core;
using Microsoft.EntityFrameworkCore;

namespace LabWatch.Infrastructure;

public sealed record DashboardAssessment(Assessment Assessment, Advisory Advisory, LabAsset Asset);
public sealed record DashboardSnapshot(
    IReadOnlyList<Advisory> Advisories,
    IReadOnlyList<LabAsset> Assets,
    IReadOnlyList<DashboardAssessment> Assessments,
    IReadOnlyList<CollectorRun> Runs,
    IReadOnlyList<HealingEvent> HealingEvents)
{
    public CollectorRun? LastAccepted => Runs.FirstOrDefault(x => x.Status == CollectorRunStatus.Accepted);
    public CollectorRun? Latest => Runs.FirstOrDefault();
    public bool IsDegraded => Latest is { Status: CollectorRunStatus.Quarantined or CollectorRunStatus.Failed or CollectorRunStatus.TimedOut };
}

public sealed class DashboardService(IDbContextFactory<LabWatchDbContext> factory)
{
    public async Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var advisories = (await db.Advisories.ToListAsync(cancellationToken)).OrderByDescending(x => x.PublishedAt).ToList();
        var assets = await db.Assets.OrderBy(x => x.AssetTag).ToListAsync(cancellationToken);
        var assessments = (await db.Assessments.ToListAsync(cancellationToken)).OrderByDescending(x => x.AssessedAt).ToList();
        var runs = (await db.CollectorRuns.ToListAsync(cancellationToken)).OrderByDescending(x => x.StartedAt).Take(20).ToList();
        var healing = (await db.HealingEvents.ToListAsync(cancellationToken)).OrderByDescending(x => x.ApprovedAt).Take(10).ToList();
        var advisoryMap = advisories.ToDictionary(x => x.Id);
        var assetMap = assets.ToDictionary(x => x.Id);
        var joined = assessments
            .Where(x => advisoryMap.ContainsKey(x.AdvisoryId) && assetMap.ContainsKey(x.AssetId))
            .Select(x => new DashboardAssessment(x, advisoryMap[x.AdvisoryId], assetMap[x.AssetId])).ToList();
        return new DashboardSnapshot(advisories, assets, joined, runs, healing);
    }

    public async Task<(Advisory Advisory, IReadOnlyList<DashboardAssessment> Assessments)?> GetAdvisoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetAsync(cancellationToken);
        var advisory = snapshot.Advisories.SingleOrDefault(x => x.Id == id);
        return advisory is null ? null : (advisory, snapshot.Assessments.Where(x => x.Advisory.Id == id).ToList());
    }
}
