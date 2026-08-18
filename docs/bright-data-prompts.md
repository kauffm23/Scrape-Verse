# Bright Data collector prompts

## CISA collector

Create a Discovery + detail-page Scraper Studio collector starting at:

`https://raw.githubusercontent.com/cisagov/CSAF/refs/heads/develop/csaf_files/OT/white/index.txt`

Prompt:

> Each line in index.txt contains the relative path to advisory detail information. Find ICS Medical Advisories and visit each advisory detail page. Produce one row per advisory with exactly these fields: source_id, source_url, title, published_at (ISO 8601), manufacturer, products (array), models (array), affected_versions (array), cves (array), severity, summary, and mitigations. source_url must be the canonical CISA detail URL. Do not infer missing manufacturer, date, product, version, CVE, severity, or mitigation values. Return empty arrays for missing array fields and empty strings for missing optional text.

Begin with a small recent-page scope for the demo. Verify the first dataset manually before copying the collector ID.

## Chaos fixture collector

Create a collector against the public tunnel URL ending in `/advisories`.

Prompt:

> Extract one row per fictional laboratory security advisory. Return exactly: source_id, source_url, title, published_at, manufacturer, products, models, affected_versions, cves, severity, summary, and mitigations. source_url must link to the advisory anchor on this same page. products, models, affected_versions, and cves must be arrays. Preserve all three records.

Approve and publish while the fixture is in V1.

## Healing prompt

After switching the same URL to V2:

> The source page was redesigned. Restore manufacturer, product, model, affected versions, severity, CVE identifiers, publication date, mitigation, source ID, and source URL from their semantic equivalents in the new layout. Preserve the existing output schema and exactly one row per advisory. Do not infer values that are absent. All three fictional advisories must be present in the preview.

Commands:

```text
brightdata scraper heal c_YOUR_FIXTURE_COLLECTOR "The source page was redesigned..."
brightdata scraper approve c_YOUR_FIXTURE_COLLECTOR
brightdata scraper run c_YOUR_FIXTURE_COLLECTOR https://YOUR-TUNNEL/advisories
```

Keep screenshots of the broken result, healing preview, approval, and recovered run as backup demo footage.
