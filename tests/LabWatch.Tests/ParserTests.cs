using System.Text.Json;
using LabWatch.Infrastructure;

namespace LabWatch.Tests;

public sealed class ParserTests
{
    [Fact]
    public void Parses_common_scraper_schema_variants()
    {
        using var json = JsonDocument.Parse("""
        [{"id":"ICSMA-1","url":"https://cisa.gov/a","title":"Advisory","date":"2026-08-17",
          "vendor":"BD","product":"BD Synapsys","model":"Synapsys","versions":["< 4.25"],
          "cve_ids":"CVE-1, CVE-2","severity":"High"}]
        """);
        var records = new AdvisoryJsonParser().Parse(json.RootElement);
        var record = Assert.Single(records);
        Assert.Equal("ICSMA-1", record.SourceId);
        Assert.Equal(["CVE-1", "CVE-2"], record.Cves);
        Assert.Equal("BD Synapsys", Assert.Single(record.Products));
    }

    [Fact]
    public void Rejects_non_array_dataset()
    {
        using var json = JsonDocument.Parse("{\"status\":\"building\"}");
        Assert.Throws<FormatException>(() => new AdvisoryJsonParser().Parse(json.RootElement));
    }
}
