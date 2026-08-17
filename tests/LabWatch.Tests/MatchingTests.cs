using LabWatch.Core;

namespace LabWatch.Tests;

public sealed class MatchingTests
{
    private readonly ImpactMatcher _matcher = new();

    [Fact]
    public void Alias_and_affected_version_produce_affected()
    {
        var result = _matcher.Match(Advisory(), Asset("Siemens Healthineers", "1.2"));
        Assert.Equal(AssessmentOutcome.Affected, result.Outcome);
        Assert.Contains(result.Reasons, x => x.Contains("version 1.2", StringComparison.Ordinal));
    }

    [Fact]
    public void Version_outside_range_produces_not_affected()
    {
        var result = _matcher.Match(Advisory(), Asset("Siemens", "1.6"));
        Assert.Equal(AssessmentOutcome.NotAffected, result.Outcome);
    }

    [Fact]
    public void Missing_version_produces_human_review()
    {
        var result = _matcher.Match(Advisory(), Asset("Siemens", ""));
        Assert.Equal(AssessmentOutcome.ReviewRequired, result.Outcome);
    }

    [Fact]
    public void Custom_vendor_build_suffix_is_evaluated_conservatively()
    {
        var advisory = Advisory();
        advisory.AffectedVersions = "<= 3.0.11.5 BN9";
        var asset = Asset("Siemens", "3.0.11.4");
        var result = _matcher.Match(advisory, asset);
        Assert.Equal(AssessmentOutcome.Affected, result.Outcome);
    }

    private static Advisory Advisory() => new()
    {
        SourceId = "A",
        SourceUrl = "https://example.test/A",
        Title = "Test",
        PublishedAt = DateTimeOffset.UtcNow,
        Manufacturer = "Siemens",
        Products = "Atellica Data Manager",
        Models = "ADM",
        AffectedVersions = "1.0 - 1.4",
        SnapshotId = "j_test",
        ContentHash = "hash"
    };

    private static LabAsset Asset(string manufacturer, string version) => new()
    {
        AssetTag = "LAB-1",
        Manufacturer = manufacturer,
        Product = "Atellica Data Manager",
        Model = "ADM",
        Version = version,
        Department = "Core Lab",
        Criticality = "Critical"
    };
}
