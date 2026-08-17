using LabWatch.Core;
using Microsoft.EntityFrameworkCore;

namespace LabWatch.Infrastructure;

public sealed class DemoWorkflowService(IDbContextFactory<LabWatchDbContext> factory)
{
    public async Task<CollectorRun> SimulateDriftAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var run = new CollectorRun
        {
            Source = "Chaos fixture",
            CollectorId = "c_fixture_demo",
            SnapshotId = $"j_drift_{DateTimeOffset.UtcNow:HHmmss}",
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-3),
            CompletedAt = DateTimeOffset.UtcNow,
            ItemCount = 1,
            ContentHash = "quarantined",
            Status = CollectorRunStatus.Quarantined,
            ValidationErrors = "Record count dropped more than 40% (3 to 1).\nRecord 1: manufacturer is required."
        };
        db.CollectorRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task<(CollectorRun Run, HealingEvent Healing)> SimulateRecoveryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var failed = (await db.CollectorRuns.Where(x => x.Status == CollectorRunStatus.Quarantined).ToListAsync(cancellationToken))
            .OrderByDescending(x => x.StartedAt).FirstOrDefault()
            ?? throw new InvalidOperationException("Create a quarantined fixture run before recording recovery.");
        var recovered = new CollectorRun
        {
            Source = "Chaos fixture",
            CollectorId = failed.CollectorId,
            SnapshotId = $"j_healed_{DateTimeOffset.UtcNow:HHmmss}",
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-4),
            CompletedAt = DateTimeOffset.UtcNow,
            ItemCount = 3,
            ContentHash = "healed",
            Status = CollectorRunStatus.Accepted
        };
        var healing = new HealingEvent
        {
            FailedRunId = failed.Id,
            RecoveredRunId = recovered.Id,
            RepairPrompt = "Restore manufacturer, product, affected versions, severity, CVEs, dates, mitigations, and source URL while preserving the existing schema and one row per advisory.",
            ApprovedAt = DateTimeOffset.UtcNow
        };
        db.CollectorRuns.Add(recovered);
        db.HealingEvents.Add(healing);
        await db.SaveChangesAsync(cancellationToken);
        return (recovered, healing);
    }
}
