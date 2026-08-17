using LabWatch.Core;
using Microsoft.EntityFrameworkCore;

namespace LabWatch.Infrastructure;

public sealed class DatabaseInitializer(IDbContextFactory<LabWatchDbContext> factory, ImpactMatcher matcher)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        if (!await db.Assets.AnyAsync(cancellationToken))
            db.Assets.AddRange(Assets());
        await db.SaveChangesAsync(cancellationToken);

        if (await db.Advisories.AnyAsync(cancellationToken)) return;
        var run = new CollectorRun
        {
            Source = "Demo seed",
            CollectorId = "c_demo_seed",
            SnapshotId = "j_demo_seed",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-4),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            ItemCount = 3,
            ContentHash = "demo",
            Status = CollectorRunStatus.Accepted
        };
        db.CollectorRuns.Add(run);
        var advisories = DemoAdvisories();
        db.Advisories.AddRange(advisories);
        var assets = await db.Assets.ToListAsync(cancellationToken);
        foreach (var advisory in advisories)
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
                    EvidenceUrl = advisory.SourceUrl,
                    AssessedAt = run.CompletedAt!.Value
                });
            }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<LabAsset> Assets() =>
    [
        new() { AssetTag = "LAB-001", Manufacturer = "INFINITT Healthcare", Product = "INFINITT PACS System Manager", Model = "System Manager", Version = "3.0.11.4", Department = "Pathology", Criticality = "Critical" },
        new() { AssetTag = "LAB-002", Manufacturer = "Siemens Healthineers", Product = "Atellica Data Manager", Model = "ADM", Version = "1.2", Department = "Core Laboratory", Criticality = "Critical" },
        new() { AssetTag = "LAB-003", Manufacturer = "Becton Dickinson", Product = "BD Synapsys", Model = "Synapsys", Version = "4.20", Department = "Microbiology", Criticality = "High" },
        new() { AssetTag = "LAB-004", Manufacturer = "Roche Diagnostics", Product = "cobas infinity", Model = "Core Lab", Version = "3.03", Department = "Chemistry", Criticality = "Critical" },
        new() { AssetTag = "LAB-005", Manufacturer = "Abbott", Product = "AlinIQ AMS", Model = "AMS", Version = "2.5", Department = "Chemistry", Criticality = "High" },
        new() { AssetTag = "LAB-006", Manufacturer = "INFINITT Healthcare", Product = "INFINITT PACS System Manager", Model = "System Manager", Version = "3.0.12.1", Department = "Radiology", Criticality = "High" },
        new() { AssetTag = "LAB-007", Manufacturer = "Siemens", Product = "Atellica Data Manager", Model = "ADM", Version = "", Department = "Satellite Lab", Criticality = "Medium" },
        new() { AssetTag = "LAB-008", Manufacturer = "Becton Dickinson", Product = "BD Kiestra", Model = "TLA", Version = "5.1", Department = "Microbiology", Criticality = "Critical" },
        new() { AssetTag = "LAB-009", Manufacturer = "Beckman Coulter", Product = "DxA 5000", Model = "DxA", Version = "1.4", Department = "Automation", Criticality = "High" },
        new() { AssetTag = "LAB-010", Manufacturer = "Fictional Lab Systems", Product = "Northstar LIMS", Model = "LIMS", Version = "21.2", Department = "Informatics", Criticality = "Critical" }
    ];

    private static List<Advisory> DemoAdvisories() =>
    [
        new()
        {
            SourceId = "ICSMA-25-100-01", SourceUrl = "https://www.cisa.gov/news-events/ics-medical-advisories/icsma-25-100-01-infinitt-healthcare-infinitt-pacs",
            Title = "INFINITT Healthcare INFINITT PACS", PublishedAt = new DateTimeOffset(2025, 4, 10, 0, 0, 0, TimeSpan.Zero),
            Manufacturer = "INFINITT Healthcare", Products = "INFINITT PACS System Manager", Models = "System Manager", AffectedVersions = "<= 3.0.11.5 BN9",
            Cves = "CVE-2025-24489 | CVE-2025-27714 | CVE-2025-27721", Severity = "High", Summary = "Multiple flaws may allow dangerous file upload or unauthorized access to system resources.",
            Mitigations = "Update to 3.0.11.5 BN10 or later and restrict network access to trusted clinical segments.", SnapshotId = "j_demo_seed", ContentHash = "demo-cisa"
        },
        new()
        {
            SourceId = "LAB-FIX-2026-001", SourceUrl = "http://localhost:5181/advisories#LAB-FIX-2026-001",
            Title = "Atellica Data Manager Update", PublishedAt = new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero),
            Manufacturer = "Siemens", Products = "Atellica Data Manager", Models = "ADM", AffectedVersions = "1.0 - 1.4",
            Cves = "CVE-2026-41001", Severity = "Critical", Summary = "Fictional chaos-fixture advisory for demonstration only.",
            Mitigations = "Install version 1.5 and review audit logs.", SnapshotId = "j_demo_seed", ContentHash = "demo-fixture-1"
        },
        new()
        {
            SourceId = "LAB-FIX-2026-002", SourceUrl = "http://localhost:5181/advisories#LAB-FIX-2026-002",
            Title = "BD Synapsys Input Validation", PublishedAt = new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero),
            Manufacturer = "BD", Products = "BD Synapsys", Models = "Synapsys", AffectedVersions = "< 4.25",
            Cves = "CVE-2026-41002", Severity = "High", Summary = "Fictional chaos-fixture advisory for demonstration only.",
            Mitigations = "Upgrade to 4.25 or later.", SnapshotId = "j_demo_seed", ContentHash = "demo-fixture-2"
        }
    ];
}
