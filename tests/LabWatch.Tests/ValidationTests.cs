using LabWatch.Core;

namespace LabWatch.Tests;

public sealed class ValidationTests
{
    private readonly SnapshotValidator _validator = new();

    [Fact]
    public void Accepts_complete_unique_snapshot()
    {
        var result = _validator.Validate([Candidate("A"), Candidate("B")], 2);
        Assert.True(result.IsAccepted);
        Assert.Empty(result.Errors);
        Assert.Equal(64, result.ContentHash.Length);
    }

    [Fact]
    public void Rejects_empty_snapshot()
    {
        var result = _validator.Validate([]);
        Assert.False(result.IsAccepted);
        Assert.Contains(result.Errors, x => x.Contains("no records", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_missing_required_field()
    {
        var broken = Candidate("A") with { Manufacturer = "" };
        var result = _validator.Validate([broken]);
        Assert.False(result.IsAccepted);
        Assert.Contains(result.Errors, x => x.Contains("manufacturer is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_duplicate_source_id()
    {
        var result = _validator.Validate([Candidate("A"), Candidate("A")]);
        Assert.False(result.IsAccepted);
        Assert.Contains(result.Errors, x => x.Contains("Duplicate source ID", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_count_drop_greater_than_forty_percent()
    {
        var result = _validator.Validate([Candidate("A"), Candidate("B")], previousAcceptedCount: 4);
        Assert.False(result.IsAccepted);
        Assert.Contains(result.Errors, x => x.Contains("more than 40%", StringComparison.Ordinal));
    }

    internal static AdvisoryCandidate Candidate(string id) => new(
        id, $"https://example.test/{id}", $"Advisory {id}", DateTimeOffset.Parse("2026-08-17T00:00:00Z"),
        "Siemens", ["Atellica Data Manager"], ["ADM"], ["<= 1.4"], ["CVE-2026-0001"],
        "High", "Summary", "Upgrade.");
}
