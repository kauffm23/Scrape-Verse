using System.Text.RegularExpressions;

namespace LabWatch.Core;

public sealed record MatchDecision(AssessmentOutcome Outcome, IReadOnlyList<string> Reasons, string RecommendedAction);

public sealed partial class ImpactMatcher
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["siemens healthineers"] = "siemens",
        ["siemens healthcare"] = "siemens",
        ["bd"] = "becton dickinson",
        ["becton dickinson and company"] = "becton dickinson",
        ["infinitt healthcare co"] = "infinitt healthcare",
        ["infinitt pacs healthcare platform"] = "infinitt pacs"
    };

    public MatchDecision Match(Advisory advisory, LabAsset asset)
    {
        var reasons = new List<string>();
        if (Canonical(advisory.Manufacturer) != Canonical(asset.Manufacturer))
            return NotAffected("Manufacturer does not match.");
        reasons.Add($"Manufacturer matched: {asset.Manufacturer}.");

        if (!advisory.ProductList.Any(x => Canonical(x) == Canonical(asset.Product)))
            return NotAffected("Product does not match.");
        reasons.Add($"Product matched: {asset.Product}.");

        if (advisory.ModelList.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(asset.Model))
                return Review(reasons, "Asset model is missing; model scope cannot be confirmed.");
            if (!advisory.ModelList.Any(x => Canonical(x) == Canonical(asset.Model)))
                return NotAffected("Manufacturer and product match, but model is outside the advisory scope.");
            reasons.Add($"Model matched: {asset.Model}.");
        }

        if (advisory.VersionList.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(asset.Version))
                return Review(reasons, "Asset version is missing; affected-version scope cannot be confirmed.");
            var evaluated = advisory.VersionList.Select(rule => VersionRule.Evaluate(asset.Version, rule)).ToArray();
            if (evaluated.All(x => x is false))
                return NotAffected("Installed version is outside the advisory's affected range.");
            if (evaluated.All(x => x is null))
                return Review(reasons, "Version expression could not be evaluated deterministically.");
            reasons.Add($"Installed version {asset.Version} is within the affected range.");
        }
        else
        {
            return Review(reasons, "Advisory does not provide deterministic model or version scope.");
        }

        return new MatchDecision(AssessmentOutcome.Affected, reasons,
            "Open the evidence, confirm the installed build, apply the vendor mitigation, and document change control.");
    }

    public static string Canonical(string value)
    {
        var normalized = NonWord().Replace(value.ToLowerInvariant(), " ");
        normalized = CorporateSuffix().Replace(normalized, "").Trim();
        normalized = MultiSpace().Replace(normalized, " ");
        return Aliases.TryGetValue(normalized, out var alias) ? alias : normalized;
    }

    private static MatchDecision NotAffected(string reason) =>
        new(AssessmentOutcome.NotAffected, [reason], "No action required; retain the assessment as evidence.");

    private static MatchDecision Review(List<string> reasons, string reason)
    {
        reasons.Add(reason);
        return new MatchDecision(AssessmentOutcome.ReviewRequired, reasons,
            "Verify model and version details with the asset owner before disposition.");
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonWord();
    [GeneratedRegex(@"\b(incorporated|corporation|company|limited|inc|corp|co|ltd|llc)\b")]
    private static partial Regex CorporateSuffix();
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpace();
}

internal static partial class VersionRule
{
    public static bool? Evaluate(string installed, string rule)
    {
        if (!Version.TryParse(Clean(installed), out var installedVersion)) return null;
        var normalized = BuildSuffix().Replace(rule.Trim().ToLowerInvariant().Replace("version", "").Trim(), "").Trim();

        var range = Range().Match(normalized);
        if (range.Success && Version.TryParse(Clean(range.Groups[1].Value), out var min) &&
            Version.TryParse(Clean(range.Groups[2].Value), out var max))
            return installedVersion >= min && installedVersion <= max;

        var comparison = Comparison().Match(normalized);
        if (comparison.Success && Version.TryParse(Clean(comparison.Groups[2].Value), out var target))
            return comparison.Groups[1].Value switch
            {
                "<" => installedVersion < target,
                "<=" => installedVersion <= target,
                ">" => installedVersion > target,
                ">=" => installedVersion >= target,
                _ => installedVersion == target
            };

        return Version.TryParse(Clean(normalized), out var exact) ? installedVersion == exact : null;
    }

    private static string Clean(string value) => BuildSuffix().Replace(value.Trim().TrimStart('v', 'V').Replace('*', '0'), "").Trim();
    [GeneratedRegex(@"^([0-9]+(?:\.[0-9]+){0,3})\s*(?:-|to|through)\s*([0-9]+(?:\.[0-9]+){0,3})$")]
    private static partial Regex Range();
    [GeneratedRegex(@"^(<=|>=|<|>|=)?\s*([0-9]+(?:\.[0-9]+){0,3})$")]
    private static partial Regex Comparison();
    [GeneratedRegex(@"\s+BN\d+$", RegexOptions.IgnoreCase)]
    private static partial Regex BuildSuffix();
}
