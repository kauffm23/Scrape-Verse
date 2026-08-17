using LabWatch.Core;
using Microsoft.EntityFrameworkCore;

namespace LabWatch.Infrastructure;

public sealed record IngestionResult(CollectorRun Run, int AcceptedRecords, int AssessmentsCreated);

public sealed class IngestionService(
    IDbContextFactory<LabWatchDbContext> dbFactory,
    IBrightDataClient brightData,
    AdvisoryJsonParser parser,
    SnapshotValidator validator,
    ImpactMatcher matcher)
{
    public async Task<IngestionResult> RunAsync(string source, string collectorId, IReadOnlyList<string> urls, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = new CollectorRun { Source = source, CollectorId = collectorId, Status = CollectorRunStatus.Pending };
        db.CollectorRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            run.SnapshotId = await brightData.TriggerAsync(collectorId, urls, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            var dataset = await brightData.PollDatasetAsync(run.SnapshotId, cancellationToken);
            var candidates = parser.Parse(dataset);
            var priorAccepted = await db.CollectorRuns
                .Where(x => x.Source == source && x.Status == CollectorRunStatus.Accepted && x.Id != run.Id)
                .ToListAsync(cancellationToken);
            var previousCount = priorAccepted.OrderByDescending(x => x.CompletedAt).Select(x => (int?)x.ItemCount).FirstOrDefault();
            var validation = validator.Validate(candidates, previousCount);
            run.ItemCount = candidates.Count;
            run.ContentHash = validation.ContentHash;
            run.CompletedAt = DateTimeOffset.UtcNow;

            if (!validation.IsAccepted)
            {
                run.Status = CollectorRunStatus.Quarantined;
                run.ValidationErrors = string.Join(Environment.NewLine, validation.Errors);
                await db.SaveChangesAsync(cancellationToken);
                return new IngestionResult(run, 0, 0);
            }

            var created = await AcceptAsync(db, validation.ValidRecords, run.SnapshotId, validation.ContentHash, cancellationToken);
            run.Status = CollectorRunStatus.Accepted;
            await db.SaveChangesAsync(cancellationToken);
            return new IngestionResult(run, validation.ValidRecords.Count, created);
        }
        catch (TimeoutException ex)
        {
            await MarkFailed(db, run, CollectorRunStatus.TimedOut, ex.Message, cancellationToken);
            return new IngestionResult(run, 0, 0);
        }
        catch (Exception ex) when (ex is BrightDataApiException or FormatException or System.Text.Json.JsonException)
        {
            await MarkFailed(db, run, CollectorRunStatus.Failed, ex.Message, cancellationToken);
            return new IngestionResult(run, 0, 0);
        }
    }

    public async Task<HealingEvent> RecordHealingAsync(Guid failedRunId, Guid? recoveredRunId, string prompt, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var healing = new HealingEvent { FailedRunId = failedRunId, RecoveredRunId = recoveredRunId, RepairPrompt = prompt, ApprovedAt = DateTimeOffset.UtcNow };
        db.HealingEvents.Add(healing);
        await db.SaveChangesAsync(cancellationToken);
        return healing;
    }

    private async Task<int> AcceptAsync(LabWatchDbContext db, IReadOnlyList<AdvisoryCandidate> candidates, string snapshotId, string hash, CancellationToken token)
    {
        var assets = await db.Assets.ToListAsync(token);
        var assessmentsCreated = 0;
        foreach (var candidate in candidates)
        {
            var advisory = await db.Advisories.SingleOrDefaultAsync(x => x.SourceId == candidate.SourceId, token);
            if (advisory is null)
            {
                advisory = Map(candidate, snapshotId, hash);
                db.Advisories.Add(advisory);
            }
            else
            {
                Update(advisory, candidate, snapshotId, hash);
                var old = await db.Assessments.Where(x => x.AdvisoryId == advisory.Id).ToListAsync(token);
                db.Assessments.RemoveRange(old);
            }

            foreach (var asset in assets)
            {
                var decision = matcher.Match(advisory, asset);
                db.Assessments.Add(new Assessment
                {
                    AdvisoryId = advisory.Id,
                    AssetId = asset.Id,
                    Outcome = decision.Outcome,
                    Reasons = ValueList.Join(decision.Reasons),
                    RecommendedAction = decision.RecommendedAction,
                    EvidenceUrl = advisory.SourceUrl
                });
                assessmentsCreated++;
            }
        }
        return assessmentsCreated;
    }

    private static Advisory Map(AdvisoryCandidate x, string snapshotId, string hash) => new()
    {
        SourceId = x.SourceId,
        SourceUrl = x.SourceUrl,
        Title = x.Title,
        PublishedAt = x.PublishedAt!.Value,
        Manufacturer = x.Manufacturer,
        Products = ValueList.Join(x.Products),
        Models = ValueList.Join(x.Models),
        AffectedVersions = ValueList.Join(x.AffectedVersions),
        Cves = ValueList.Join(x.Cves),
        Severity = x.Severity,
        Summary = x.Summary,
        Mitigations = x.Mitigations,
        SnapshotId = snapshotId,
        ContentHash = hash
    };

    private static void Update(Advisory target, AdvisoryCandidate x, string snapshotId, string hash)
    {
        target.SourceUrl = x.SourceUrl; target.Title = x.Title; target.PublishedAt = x.PublishedAt!.Value;
        target.Manufacturer = x.Manufacturer; target.Products = ValueList.Join(x.Products); target.Models = ValueList.Join(x.Models);
        target.AffectedVersions = ValueList.Join(x.AffectedVersions); target.Cves = ValueList.Join(x.Cves);
        target.Severity = x.Severity; target.Summary = x.Summary; target.Mitigations = x.Mitigations;
        target.SnapshotId = snapshotId; target.ContentHash = hash;
    }

    private static async Task MarkFailed(LabWatchDbContext db, CollectorRun run, CollectorRunStatus status, string error, CancellationToken token)
    {
        run.Status = status; run.ValidationErrors = error; run.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(token);
    }
}
