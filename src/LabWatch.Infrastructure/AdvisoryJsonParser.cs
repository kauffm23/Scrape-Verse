using System.Globalization;
using System.Text.Json;
using LabWatch.Core;

namespace LabWatch.Infrastructure;

public sealed class AdvisoryJsonParser
{
    public IReadOnlyList<AdvisoryCandidate> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
            throw new FormatException("Bright Data dataset must be a JSON array.");
        return root.EnumerateArray().Select(ParseRecord).ToArray();
    }

    private static AdvisoryCandidate ParseRecord(JsonElement row)
    {
        var url = Text(row, "source_url", "sourceUrl", "url");
        return new AdvisoryCandidate(
            Text(row, "source_id", "sourceId", "id", "advisory_id", "advisoryId", fallback: url),
            url,
            Text(row, "title", "name"),
            Date(row, "published_at", "publishedAt", "publication_date", "publicationDate", "date"),
            Text(row, "manufacturer", "vendor"),
            List(row, "products", "product", "affected_products"),
            List(row, "models", "model", "affected_models"),
            List(row, "affected_versions", "affectedVersions", "versions", "version"),
            List(row, "cves", "cve_ids", "cveIds"),
            Text(row, "severity", "risk", fallback: "Unknown"),
            Text(row, "summary", "risk_summary", "riskSummary", "description"),
            Text(row, "mitigations", "mitigation", "recommendations"));
    }

    private static string Text(JsonElement row, string key1, string key2 = "", string key3 = "", string key4 = "", string key5 = "", string? fallback = "")
    {
        foreach (var key in new[] { key1, key2, key3, key4, key5 }.Where(x => x.Length > 0))
        {
            if (!row.TryGetProperty(key, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) continue;
            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
        }
        return fallback ?? "";
    }

    private static IReadOnlyList<string> List(JsonElement row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!row.TryGetProperty(key, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : x.ToString()).Where(x => x.Length > 0).ToArray();
            var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
            return string.IsNullOrWhiteSpace(text) ? [] : text.Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }
        return [];
    }

    private static DateTimeOffset? Date(JsonElement row, params string[] keys)
    {
        var value = Text(row, keys.ElementAtOrDefault(0) ?? "", keys.ElementAtOrDefault(1) ?? "", keys.ElementAtOrDefault(2) ?? "", keys.ElementAtOrDefault(3) ?? "", keys.ElementAtOrDefault(4) ?? "");
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;
    }
}
