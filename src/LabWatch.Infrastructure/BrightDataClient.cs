using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LabWatch.Infrastructure;

public sealed class BrightDataOptions
{
    public const string SectionName = "BrightData";
    public string ApiToken { get; set; } = "";
    public string CisaCollectorId { get; set; } = "";
    public string FixtureCollectorId { get; set; } = "";
    public string CisaUrl { get; set; } = "https://www.cisa.gov/news-events/ics-advisories";
    public string FixtureUrl { get; set; } = "http://localhost:5181/advisories";
    public int PollIntervalSeconds { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 120;
}

public sealed class FixtureOptions
{
    public const string SectionName = "Fixture";
    public string BaseUrl { get; set; } = "http://localhost:5181";
    public string AdminToken { get; set; } = "local-demo-only";
}

public sealed class BrightDataApiException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

public sealed class FixtureControlService(HttpClient client, IOptions<FixtureOptions> options)
{
    public async Task<string> SetLayoutAsync(string layout, CancellationToken cancellationToken = default)
    {
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/admin/layout");
        request.Headers.Add("X-Fixture-Token", options.Value.AdminToken);
        request.Content = JsonContent.Create(new { layout });
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Fixture layout switch returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        return layout.ToLowerInvariant();
    }
}

public interface IBrightDataClient
{
    Task<string> TriggerAsync(string collectorId, IReadOnlyList<string> urls, CancellationToken cancellationToken);
    Task<JsonElement> PollDatasetAsync(string snapshotId, CancellationToken cancellationToken);
}

public sealed class BrightDataClient(HttpClient httpClient, IOptions<BrightDataOptions> options) : IBrightDataClient
{
    private readonly BrightDataOptions _options = options.Value;

    public async Task<string> TriggerAsync(string collectorId, IReadOnlyList<string> urls, CancellationToken cancellationToken)
    {
        EnsureConfigured(collectorId);
        using var request = CreateRequest(HttpMethod.Post, $"dca/trigger?collector={Uri.EscapeDataString(collectorId)}&queue_next=1");
        request.Content = JsonContent.Create(urls.Select(url => new { url }));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        if (!body.RootElement.TryGetProperty("collection_id", out var id) || string.IsNullOrWhiteSpace(id.GetString()))
            throw new BrightDataApiException("Bright Data trigger response did not contain collection_id.");
        // Bright Data returns collection_id here; all later APIs call the same value snapshot_id.
        return id.GetString()!;
    }

    public async Task<JsonElement> PollDatasetAsync(string snapshotId, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var transientAttempt = 0;
        try
        {
            while (true)
            {
                using var request = CreateRequest(HttpMethod.Get, $"dca/dataset?id={Uri.EscapeDataString(snapshotId)}");
                using var response = await httpClient.SendAsync(request, timeout.Token);
                if ((int)response.StatusCode >= 500)
                {
                    transientAttempt++;
                    var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, transientAttempt - 1), 8));
                    await Task.Delay(delay, timeout.Token);
                    continue;
                }

                await EnsureSuccess(response, timeout.Token);
                using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(timeout.Token));
                if (body.RootElement.ValueKind == JsonValueKind.Array)
                    return body.RootElement.Clone();

                transientAttempt = 0;
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Bright Data snapshot {snapshotId} did not complete within {_options.TimeoutSeconds} seconds.");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        return request;
    }

    private void EnsureConfigured(string collectorId)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiToken))
            throw new BrightDataApiException("BrightData:ApiToken is not configured.");
        if (string.IsNullOrWhiteSpace(collectorId))
            throw new BrightDataApiException("A Bright Data collector ID is not configured.");
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new BrightDataApiException($"Bright Data returned {(int)response.StatusCode} {response.ReasonPhrase}: {detail}", response.StatusCode);
    }
}
