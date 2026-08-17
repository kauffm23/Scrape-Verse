using System.Net;
using System.Text;
using LabWatch.Infrastructure;
using Microsoft.Extensions.Options;

namespace LabWatch.Tests;

public sealed class BrightDataClientTests
{
    [Fact]
    public async Task Trigger_returns_collection_id_as_snapshot_id()
    {
        var handler = new QueueHandler(Response(HttpStatusCode.OK, "{\"collection_id\":\"j_123\"}"));
        var client = Client(handler);
        var id = await client.TriggerAsync("c_123", ["https://example.test"], CancellationToken.None);
        Assert.Equal("j_123", id);
        Assert.Equal("Bearer token", handler.LastRequest!.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Poll_waits_through_building_status()
    {
        var handler = new QueueHandler(Response(HttpStatusCode.OK, "{\"status\":\"building\"}"), Response(HttpStatusCode.OK, "[]"));
        var result = await Client(handler).PollDatasetAsync("j_123", CancellationToken.None);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Poll_retries_transient_server_failure()
    {
        var handler = new QueueHandler(Response(HttpStatusCode.ServiceUnavailable, "temporary"), Response(HttpStatusCode.OK, "[]"));
        var result = await Client(handler).PollDatasetAsync("j_123", CancellationToken.None);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, result.ValueKind);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Invalid_credentials_raise_typed_exception()
    {
        var handler = new QueueHandler(Response(HttpStatusCode.Unauthorized, "invalid token"));
        var error = await Assert.ThrowsAsync<BrightDataApiException>(() =>
            Client(handler).TriggerAsync("c_123", ["https://example.test"], CancellationToken.None));
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    private static BrightDataClient Client(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.brightdata.com/") };
        return new BrightDataClient(http, Options.Create(new BrightDataOptions
        { ApiToken = "token", PollIntervalSeconds = 0, TimeoutSeconds = 5 }));
    }

    private static HttpResponseMessage Response(HttpStatusCode code, string json) => new(code)
    { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; LastRequest = request;
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
