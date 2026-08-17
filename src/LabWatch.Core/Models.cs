namespace LabWatch.Core;

public enum CollectorRunStatus { Pending, Accepted, Quarantined, TimedOut, Failed }
public enum AssessmentOutcome { Affected, NotAffected, ReviewRequired }

public sealed class Advisory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string SourceId { get; set; }
    public required string SourceUrl { get; set; }
    public required string Title { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public required string Manufacturer { get; set; }
    public string Products { get; set; } = "";
    public string Models { get; set; } = "";
    public string AffectedVersions { get; set; } = "";
    public string Cves { get; set; } = "";
    public string Severity { get; set; } = "Unknown";
    public string Summary { get; set; } = "";
    public string Mitigations { get; set; } = "";
    public required string SnapshotId { get; set; }
    public required string ContentHash { get; set; }

    public IReadOnlyList<string> ProductList => ValueList.Split(Products);
    public IReadOnlyList<string> ModelList => ValueList.Split(Models);
    public IReadOnlyList<string> VersionList => ValueList.Split(AffectedVersions);
    public IReadOnlyList<string> CveList => ValueList.Split(Cves);
}

public sealed class LabAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AssetTag { get; set; }
    public required string Manufacturer { get; set; }
    public required string Product { get; set; }
    public string Model { get; set; } = "";
    public string Version { get; set; } = "";
    public required string Department { get; set; }
    public required string Criticality { get; set; }
}

public sealed class Assessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdvisoryId { get; set; }
    public Guid AssetId { get; set; }
    public AssessmentOutcome Outcome { get; set; }
    public required string Reasons { get; set; }
    public required string RecommendedAction { get; set; }
    public required string EvidenceUrl { get; set; }
    public DateTimeOffset AssessedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CollectorRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Source { get; set; }
    public required string CollectorId { get; set; }
    public string SnapshotId { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int ItemCount { get; set; }
    public string ContentHash { get; set; } = "";
    public CollectorRunStatus Status { get; set; } = CollectorRunStatus.Pending;
    public string ValidationErrors { get; set; } = "";
}

public sealed class HealingEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FailedRunId { get; set; }
    public Guid? RecoveredRunId { get; set; }
    public required string RepairPrompt { get; set; }
    public DateTimeOffset ApprovedAt { get; set; }
    public string ApprovedBy { get; set; } = "Human operator";
}

public sealed record AdvisoryCandidate(
    string SourceId,
    string SourceUrl,
    string Title,
    DateTimeOffset? PublishedAt,
    string Manufacturer,
    IReadOnlyList<string> Products,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> AffectedVersions,
    IReadOnlyList<string> Cves,
    string Severity,
    string Summary,
    string Mitigations);

public static class ValueList
{
    public static string Join(IEnumerable<string> values) =>
        string.Join(" | ", values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
