using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<LayoutState>();
var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/advisories"));
app.MapGet("/health", (LayoutState state) => Results.Ok(new { status = "ok", layout = state.Current }));
app.MapGet("/advisories", (LayoutState state) => Results.Content(
    state.Current == "v1" ? FixtureHtml.V1 : FixtureHtml.V2, "text/html", System.Text.Encoding.UTF8));

app.MapPost("/admin/layout", (LayoutRequest request, HttpRequest http, LayoutState state, IConfiguration configuration) =>
{
    var expected = Environment.GetEnvironmentVariable("FIXTURE_ADMIN_TOKEN") ?? configuration["Fixture:AdminToken"] ?? "local-demo-only";
    if (!http.Headers.TryGetValue("X-Fixture-Token", out var supplied) || supplied != expected)
        return Results.Unauthorized();
    var layout = request.Layout.Trim().ToLowerInvariant();
    if (layout is not ("v1" or "v2"))
        return Results.BadRequest(new { error = "Layout must be v1 or v2." });
    state.Current = layout;
    return Results.Ok(new { layout = state.Current, advisoryUrl = "/advisories" });
});

app.Run();

internal sealed class LayoutState { public string Current { get; set; } = "v1"; }
internal sealed record LayoutRequest(string Layout);

internal static class FixtureHtml
{
    private const string Styles = "<style>body{font-family:Arial;margin:0;background:#f3f6f8;color:#17212b}header{background:#081f2c;color:white;padding:28px 7vw}main{max-width:1050px;margin:30px auto;padding:0 24px}.badge{color:#9ae6b4;font-weight:700}.card{background:white;border-left:5px solid #1bb98c;padding:22px;margin:18px 0;border-radius:8px;box-shadow:0 6px 20px #102a4315}dt{font-weight:700;margin-top:10px}dd{margin-left:0}.pill{display:inline-block;padding:4px 9px;background:#e8f8f2;border-radius:12px;margin:3px}.release{background:white;margin:16px 0;padding:18px;border-radius:8px}summary{font-size:1.15rem;font-weight:700}</style>";
    private static readonly AdvisoryFixture[] Items =
    [
        new("LAB-FIX-2026-001", "Atellica Data Manager Update", "Siemens", "Atellica Data Manager", "ADM", "1.0 - 1.4", "Critical", "CVE-2026-41001", "2026-08-17", "Install version 1.5 and review audit logs."),
        new("LAB-FIX-2026-002", "BD Synapsys Input Validation", "BD", "BD Synapsys", "Synapsys", "< 4.25", "High", "CVE-2026-41002", "2026-08-17", "Upgrade to 4.25 or later."),
        new("LAB-FIX-2026-003", "Roche cobas infinity Session Handling", "Roche Diagnostics", "cobas infinity", "Core Lab", "<= 3.03", "High", "CVE-2026-41003", "2026-08-17", "Apply the vendor hotfix and rotate active sessions.")
    ];

    public static string V1 => Page("Fixture V1 · Conventional cards", string.Join("", Items.Select(x => $"""
      <article class="card advisory" id="{x.Id}" data-advisory-id="{x.Id}"><h2 class="title">{x.Title}</h2><dl>
      <dt>Manufacturer</dt><dd class="manufacturer">{x.Manufacturer}</dd><dt>Product</dt><dd class="product">{x.Product}</dd>
      <dt>Model</dt><dd class="model">{x.Model}</dd><dt>Affected versions</dt><dd class="versions">{x.Versions}</dd>
      <dt>Severity</dt><dd class="severity">{x.Severity}</dd><dt>CVE</dt><dd class="cves">{x.Cve}</dd>
      <dt>Published</dt><dd class="published">{x.Date}</dd><dt>Mitigation</dt><dd class="mitigation">{x.Mitigation}</dd></dl>
      <a class="source-url" href="/advisories#{x.Id}">Source evidence</a></article>
      """)));

    public static string V2 => Page("Fixture V2 · Redesigned semantic stream", $"""
      <section class="release-stream"><p>The advisory experience has been completely redesigned.</p>
      {string.Join("", Items.Select(x => $"""
        <details class="release" id="{x.Id}" open><summary><span>{x.Severity} signal</span> — {x.Title}</summary>
        <div class="release-body"><p>Issued <time>{x.Date}</time> by <strong>{x.Manufacturer}</strong>.</p>
        <p>Systems in scope: <span class="pill">{x.Product}</span><span class="pill">{x.Model}</span><span class="pill">builds {WebUtility.HtmlEncode(x.Versions)}</span></p>
        <p>Evidence marker <code>{x.Cve}</code>. Recommended response: {x.Mitigation}</p>
        <footer>Reference <a href="/advisories#{x.Id}">{x.Id}</a></footer></div></details>
      """))}</section>
      """);

    private static string Page(string label, string content) => $"<!doctype html><html><head><meta charset=\"utf-8\"><title>LabWatch Chaos Fixture</title>{Styles}</head><body><header><span class=\"badge\">PUBLIC SYNTHETIC TEST DATA</span><h1>Laboratory Security Advisories</h1><p>{label}</p></header><main>{content}</main></body></html>";
    private sealed record AdvisoryFixture(string Id, string Title, string Manufacturer, string Product, string Model, string Versions, string Severity, string Cve, string Date, string Mitigation);
}

public partial class Program;
