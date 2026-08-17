using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LabWatch.Core;

public sealed record SnapshotValidationResult(
    bool IsAccepted,
    IReadOnlyList<string> Errors,
    string ContentHash,
    IReadOnlyList<AdvisoryCandidate> ValidRecords);

public sealed class SnapshotValidator
{
    public SnapshotValidationResult Validate(IReadOnlyList<AdvisoryCandidate> records, int? previousAcceptedCount = null)
    {
        var errors = new List<string>();
        if (records.Count == 0)
            errors.Add("Snapshot contained no records.");

        var valid = new List<AdvisoryCandidate>();
        for (var index = 0; index < records.Count; index++)
        {
            var recordErrors = RequiredFieldErrors(records[index]);
            if (recordErrors.Count == 0)
                valid.Add(records[index]);
            else
                errors.AddRange(recordErrors.Select(x => $"Record {index + 1}: {x}"));
        }

        if (records.Count > 0 && (records.Count - valid.Count) / (double)records.Count > 0.20)
            errors.Add("More than 20% of records failed schema validation.");

        if (previousAcceptedCount is > 0 && records.Count < previousAcceptedCount.Value * 0.60)
            errors.Add($"Record count dropped more than 40% ({previousAcceptedCount} to {records.Count}).");

        AddDuplicates(records.Select(x => x.SourceId), "source ID", errors);
        AddDuplicates(records.Select(x => x.SourceUrl), "source URL", errors);

        return new SnapshotValidationResult(errors.Count == 0, errors, CanonicalHash(records), valid);
    }

    public static string CanonicalHash(IReadOnlyList<AdvisoryCandidate> records)
    {
        var canonical = records
            .OrderBy(x => x.SourceId, StringComparer.Ordinal)
            .Select(x => new
            {
                x.SourceId,
                x.SourceUrl,
                x.Title,
                PublishedAt = x.PublishedAt?.ToUniversalTime().ToString("O"),
                x.Manufacturer,
                Products = x.Products.OrderBy(y => y, StringComparer.Ordinal),
                Models = x.Models.OrderBy(y => y, StringComparer.Ordinal),
                Versions = x.AffectedVersions.OrderBy(y => y, StringComparer.Ordinal),
                Cves = x.Cves.OrderBy(y => y, StringComparer.Ordinal),
                x.Severity,
                x.Summary,
                x.Mitigations
            });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static List<string> RequiredFieldErrors(AdvisoryCandidate record)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(record.SourceUrl)) errors.Add("source URL is required.");
        else if (!Uri.TryCreate(record.SourceUrl, UriKind.Absolute, out _)) errors.Add("source URL must be absolute.");
        if (string.IsNullOrWhiteSpace(record.Title)) errors.Add("title is required.");
        if (string.IsNullOrWhiteSpace(record.Manufacturer)) errors.Add("manufacturer is required.");
        if (record.PublishedAt is null) errors.Add("publication date is required.");
        return errors;
    }

    private static void AddDuplicates(IEnumerable<string> values, string label, ICollection<string> errors)
    {
        var duplicates = values.Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key);
        foreach (var duplicate in duplicates)
            errors.Add($"Duplicate {label}: {duplicate}.");
    }
}
