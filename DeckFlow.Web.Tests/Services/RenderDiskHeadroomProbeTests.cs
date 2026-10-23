using System.Net;
using System.Text;
using DeckFlow.Web.Extensions;
using DeckFlow.Web.Services.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.Registry;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class RenderDiskHeadroomProbeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetFreeBytesAsync_FreshSample_ReturnsCapacityMinusUsed()
    {
        var fixture = CreateFixture(capacity: 15, usage: Usage((T0.AddSeconds(-50), 99, "GB"), (T0.AddSeconds(-20), 13.5, "GB")));
        var result = await fixture.Probe.GetFreeBytesAsync();
        Assert.Equal(1_500_000_000, result);
        Assert.Collection(fixture.Handler.Requests,
            request => Assert.Equal("/v1/postgres/test-db-1", request.Uri.AbsolutePath),
            request =>
            {
                Assert.Equal("/v1/metrics/disk-usage", request.Uri.AbsolutePath);
                Assert.Contains("resource=test-db-1", request.Uri.Query, StringComparison.Ordinal);
                Assert.Contains("resolutionSeconds=30", request.Uri.Query, StringComparison.Ordinal);
                Assert.DoesNotContain("startTime", request.Uri.Query, StringComparison.Ordinal);
                Assert.DoesNotContain("endTime", request.Uri.Query, StringComparison.Ordinal);
            });
        Assert.All(fixture.Handler.Requests, request => Assert.Equal("Bearer test-key-not-real", request.Authorization));
    }

    [Fact]
    public async Task GetFreeBytesAsync_StaleSample_ReturnsNull()
    {
        var fixture = CreateFixture(15, Usage((T0.AddSeconds(-181), 13.5, "GB")));
        Assert.Null(await fixture.Probe.GetFreeBytesAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GetFreeBytesAsync_EmptyOrAmbiguousSeries_ReturnsNull(int shape)
    {
        var usage = shape switch
        {
            0 => "[]",
            1 => "[{\"labels\":{},\"values\":[],\"unit\":\"GB\"}]",
            _ => "[{\"labels\":{},\"values\":[],\"unit\":\"GB\"},{\"labels\":{},\"values\":[],\"unit\":\"GB\"}]",
        };
        Assert.Null(await CreateFixture(15, usage).Probe.GetFreeBytesAsync());
    }

    [Theory]
    [InlineData("capacity-401")]
    [InlineData("usage-403")]
    [InlineData("throw")]
    public async Task GetFreeBytesAsync_HttpFailure_ReturnsNull(string failure)
    {
        var fixture = CreateFixture(15, Usage((T0.AddSeconds(-20), 13.5, "GB")), failure: failure);
        Assert.Null(await fixture.Probe.GetFreeBytesAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"diskSizeGB\":0}")]
    public async Task GetFreeBytesAsync_InvalidCapacity_ReturnsNull(string capacity)
    {
        Assert.Null(await CreateFixture(capacity, Usage((T0.AddSeconds(-20), 13.5, "GB"))).Probe.GetFreeBytesAsync());
    }

    [Theory]
    [InlineData(null, "test-db-1")]
    [InlineData("test-key-not-real", null)]
    [InlineData("test-key-not-real", "bad/id")]
    public async Task GetFreeBytesAsync_Unconfigured_ReturnsNullWithoutHttp(string? key, string? id)
    {
        var fixture = CreateFixture(15, Usage((T0.AddSeconds(-20), 13.5, "GB")), key: key, id: id);
        Assert.Null(await fixture.Probe.GetFreeBytesAsync());
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GetFreeBytesAsync_UnknownUnit_ReturnsNull()
    {
        Assert.Null(await CreateFixture(15, Usage((T0.AddSeconds(-20), 13.5, "percent"))).Probe.GetFreeBytesAsync());
    }

    [Fact]
    public async Task GetFreeBytesAsync_NegativeUsage_ReturnsNull()
    {
        var fixture = CreateFixture(15, Usage((T0.AddSeconds(-20), -1, "GB")));
        Assert.Null(await fixture.Probe.GetFreeBytesAsync());
        Assert.Contains(fixture.Logger.Entries, entry => entry.Level == LogLevel.Warning && entry.State.ContainsKey("Reason") && (string?)entry.State["Reason"] == "render-metric-invalid");
    }

    [Fact]
    public async Task GetFreeBytesAsync_FutureSample_ReturnsNull()
    {
        var fixture = CreateFixture(15, Usage((T0.AddSeconds(31), 13.5, "GB")));
        Assert.Null(await fixture.Probe.GetFreeBytesAsync());
        Assert.Contains(fixture.Logger.Entries, entry => entry.State.TryGetValue("Reason", out var reason) && (string?)reason == "render-metric-invalid");
    }

    [Theory]
    [InlineData("{\"diskSizeGB\":10000000000}", "")]
    [InlineData("{\"diskSizeGB\":15}", "[{\"labels\":{},\"values\":[{\"timestamp\":\"2026-10-21T11:59:40+00:00\",\"value\":1000000000000,\"unit\":\"TB\"}],\"unit\":\"TB\"}]")]
    public async Task GetFreeBytesAsync_UnrepresentableValue_ReturnsNull(string capacity, string usage)
    {
        var fixture = CreateFixture(capacity, string.IsNullOrEmpty(usage) ? Usage((T0.AddSeconds(-20), 13.5, "GB")) : usage);
        Assert.Null(await fixture.Probe.GetFreeBytesAsync());
        Assert.Contains(fixture.Logger.Entries, entry => entry.State.TryGetValue("Reason", out var reason) && (string?)reason == "render-metric-invalid");
    }

    [Fact]
    public async Task GetFreeBytesAsync_AfterFailure_SameInstanceRecovers()
    {
        var calls = 0;
        var fixture = CreateFixture(15, Usage((T0.AddSeconds(-20), 13.5, "GB")), responder: request =>
        {
            calls++;
            return calls == 1 ? Response(HttpStatusCode.Unauthorized, "{}") : DefaultResponse(request, 15, Usage((T0.AddSeconds(-20), 13.5, "GB")));
        });
        Assert.Null(await fixture.Probe.GetFreeBytesAsync());
        Assert.Equal(1_500_000_000, await fixture.Probe.GetFreeBytesAsync());
    }

    [Theory]
    [InlineData("B", 13_500_000_000d, 15_000_000_000L, 1_500_000_000L)]
    [InlineData("KB", 13_500_000d, 15_000_000_000L, 1_500_000_000L)]
    [InlineData("MB", 13_500d, 15_000_000_000L, 1_500_000_000L)]
    [InlineData("GB", 13.5d, 15_000_000_000L, 1_500_000_000L)]
    [InlineData("TB", 1d, 2_000_000_000_000L, 1_000_000_000_000L)]
    [InlineData("KiB", 1d, 2_000_000_000_000L, 1_999_999_998_976L)]
    [InlineData("MiB", 1d, 2_000_000_000_000L, 1_999_998_951_424L)]
    [InlineData("GiB", 13.5d, 15_000_000_000L, 504_485_376L)]
    [InlineData("TiB", 1d, 2_000_000_000_000L, 900_488_372_224L)]
    public async Task GetFreeBytesAsync_UnitLabel_ConvertsToBytes(string unit, double used, long capacity, long expected)
    {
        var fixture = CreateFixture($"{{\"diskSizeGB\":{capacity / 1_000_000_000d}}}", Usage((T0.AddSeconds(-20), used, unit), seriesUnit: unit));
        Assert.Equal(expected, await fixture.Probe.GetFreeBytesAsync());
    }

    [Fact]
    public async Task GetFreeBytesAsync_BudgetExhausted_WaitsForNewerSample()
    {
        Fixture? fixture = null;
        fixture = CreateFixture(15, Usage((T0.AddSeconds(-20), 13.5, "GB")), responder: request =>
            request.RequestUri!.AbsolutePath.Contains("metrics", StringComparison.Ordinal) && fixtureClock(request) >= T0.AddSeconds(45)
                ? DefaultResponse(request, 15, Usage((T0.AddSeconds(40), 13.6, "GB")))
                : DefaultResponse(request, 15, Usage((T0.AddSeconds(-20), 13.5, "GB"))));
        for (var i = 0; i < 10; i++) Assert.Equal(1_500_000_000, await fixture.Probe.GetFreeBytesAsync());
        Assert.Equal(1_400_000_000, await fixture.Probe.GetFreeBytesAsync());
        Assert.Equal(new[] { TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15) }, fixture.Delays);
        Assert.Equal(5, fixture.Handler.Requests.Count);
        DateTimeOffset fixtureClock(HttpRequestMessage _) => fixture!.Clock.GetUtcNow();
    }

    [Fact]
    public async Task GetFreeBytesAsync_BudgetExhaustedUntilStale_ReturnsNull()
    {
        var fixture = CreateFixture(15, Usage((T0.AddSeconds(-20), 13.5, "GB")));
        for (var i = 0; i < 10; i++) Assert.Equal(1_500_000_000, await fixture.Probe.GetFreeBytesAsync());
        Assert.Null(await fixture.Probe.GetFreeBytesAsync());
        Assert.Equal(11, fixture.Delays.Count);
        Assert.All(fixture.Delays, delay => Assert.Equal(TimeSpan.FromSeconds(15), delay));
    }

    [Fact]
    public async Task GetFreeBytesAsync_CapacityCachedForTenMinutes()
    {
        Fixture? fixture = null;
        fixture = CreateFixture(15, Usage((T0.AddSeconds(-20), 13.5, "GB")), responder: request => DefaultResponse(request, 15, Usage((fixtureClock(request).AddSeconds(-20), 13.5, "GB"))));
        Assert.NotNull(await fixture.Probe.GetFreeBytesAsync());
        fixture.Clock.Advance(TimeSpan.FromMinutes(9) + TimeSpan.FromSeconds(59));
        Assert.NotNull(await fixture.Probe.GetFreeBytesAsync());
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.NotNull(await fixture.Probe.GetFreeBytesAsync());
        Assert.Equal(2, fixture.Handler.Requests.Count(request => request.Uri.AbsolutePath.Contains("postgres", StringComparison.Ordinal)));
        DateTimeOffset fixtureClock(HttpRequestMessage _) => fixture!.Clock.GetUtcNow();
    }

    [Fact]
    public async Task GetFreeBytesAsync_CallerCancelled_Propagates()
    {
        using var cts = new CancellationTokenSource();
        var fixture = CreateFixture(15, Usage((T0.AddSeconds(-20), 13.5, "GB")), responder: _ =>
        {
            cts.Cancel();
            return Response(HttpStatusCode.OK, "{\"diskSizeGB\":15}");
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Probe.GetFreeBytesAsync(cts.Token));
    }

    [Fact]
    public async Task GetFreeBytesAsync_FailurePaths_NeverLogKeyOrId()
    {
        var fixtures = new[]
        {
            CreateFixture(15, Usage((T0.AddSeconds(-181), 13.5, "GB")), key: "test-key-not-real", id: null),
            CreateFixture(15, Usage((T0.AddSeconds(-181), 13.5, "GB")), id: "bad/id"),
            CreateFixture(15, Usage((T0.AddSeconds(-181), 13.5, "GB")), key: null),
            CreateFixture(15, Usage((T0.AddSeconds(-181), 13.5, "GB")), failure: "capacity-401"),
            CreateFixture(15, "not-json"),
            CreateFixture(15, Usage((T0.AddSeconds(-20), 13.5, "GB")), failure: "throw"),
        };
        foreach (var fixture in fixtures) Assert.Null(await fixture.Probe.GetFreeBytesAsync());
        var entries = fixtures.SelectMany(fixture => fixture.Logger.Entries).ToArray();
        Assert.All(entries, entry =>
        {
            Assert.Null(entry.Exception);
            Assert.DoesNotContain("test-key-not-real", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("test-db-1", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("api.render.com", entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(entry.State.Values.OfType<string>(), value => value.Contains("test-key-not-real", StringComparison.Ordinal) || value.Contains("test-db-1", StringComparison.Ordinal) || value.Contains("api.render.com", StringComparison.Ordinal));
        });
        Assert.Contains(entries, entry => entry.Level == LogLevel.Warning && entry.State.TryGetValue("Reason", out var reason) && (string?)reason == "render-http-failure" && entry.State.TryGetValue("StatusCode", out var code) && (int?)code == 401);
    }

    [Fact]
    public void AddTo_ResolvesSingletonFromRootKeys()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDeckFlowHttpClients();
        services.AddDeckFlowResiliencePipelines();
        RenderDiskHeadroomProbe.AddTo(services);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["RENDER_API_KEY"] = "test-key-not-real", ["RENDER_POSTGRES_ID"] = "test-db-1" }).Build());
        var provider = services.BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<RenderDiskHeadroomProbe>(), provider.GetRequiredService<RenderDiskHeadroomProbe>());
    }

    [Fact]
    public void RenderApiClient_RegistrationRemovesHttpLogging()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDeckFlowHttpClients();
        var factory = services.BuildServiceProvider().GetRequiredService<IHttpMessageHandlerFactory>();
        Assert.Contains(Handlers(factory.CreateHandler("commander-banlist")), handler => handler.GetType().Name.StartsWith("Logging", StringComparison.Ordinal));
        Assert.DoesNotContain(Handlers(factory.CreateHandler("render-api")), handler => handler.GetType().Name.StartsWith("Logging", StringComparison.Ordinal));
    }

    private static Fixture CreateFixture(double capacity, string usage, string? key = "test-key-not-real", string? id = "test-db-1", string? failure = null, Func<HttpRequestMessage, HttpResponseMessage>? responder = null) => CreateFixture($"{{\"diskSizeGB\":{capacity}}}", usage, key, id, failure, responder);

    private static Fixture CreateFixture(string capacity, string usage, string? key = "test-key-not-real", string? id = "test-db-1", string? failure = null, Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
    {
        var clock = new FakeClock(T0);
        var handler = new StubRenderHandler(request => responder?.Invoke(request) ?? DefaultResponse(request, capacity, usage, failure));
        var logger = new RecordingLogger();
        var delays = new List<TimeSpan>();
        var services = new ServiceCollection();
        services.AddDeckFlowResiliencePipelines();
        var pipeline = services.BuildServiceProvider().GetRequiredService<ResiliencePipelineProvider<string>>();
        var probe = new RenderDiskHeadroomProbe(new FakeHttpClientFactory(new() { ["render-api"] = handler }), pipeline, logger, key, id, clock, (delay, _) => { delays.Add(delay); clock.Advance(delay); return Task.CompletedTask; });
        return new Fixture(probe, handler, logger, clock, delays);
    }

    private static HttpResponseMessage DefaultResponse(HttpRequestMessage request, double capacity, string usage, string? failure = null) => DefaultResponse(request, $"{{\"diskSizeGB\":{capacity}}}", usage, failure);
    private static HttpResponseMessage DefaultResponse(HttpRequestMessage request, string capacity, string usage, string? failure = null)
    {
        if (failure == "throw") throw new HttpRequestException("test-key-not-real test-db-1");
        if (request.RequestUri!.AbsolutePath.Contains("postgres", StringComparison.Ordinal)) return Response(failure == "capacity-401" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, capacity);
        return Response(failure == "usage-403" ? HttpStatusCode.Forbidden : HttpStatusCode.OK, usage);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static string Usage((DateTimeOffset Timestamp, double Value, string Unit) point, (DateTimeOffset Timestamp, double Value, string Unit)? second = null, string? seriesUnit = null) => "[{\"labels\":{},\"values\":[" + string.Join(',', new[] { point }.Concat(second is null ? Array.Empty<(DateTimeOffset Timestamp, double Value, string Unit)>() : new[] { second.Value }).Select(value => $"{{\"timestamp\":\"{value.Timestamp:O}\",\"value\":{value.Value},\"unit\":\"{value.Unit}\"}}")) + $"],\"unit\":\"{seriesUnit ?? point.Unit}\"}}]";
    private static IEnumerable<DelegatingHandler> Handlers(HttpMessageHandler handler) { for (var current = handler; current is DelegatingHandler delegating; current = delegating.InnerHandler!) yield return delegating; }

    private sealed record Fixture(RenderDiskHeadroomProbe Probe, StubRenderHandler Handler, RecordingLogger Logger, FakeClock Clock, List<TimeSpan> Delays);
    private sealed class FakeClock(DateTimeOffset value) : TimeProvider { private DateTimeOffset _value = value; public override DateTimeOffset GetUtcNow() => _value; public void Advance(TimeSpan duration) => _value += duration; }
    private sealed class StubRenderHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler { public List<(Uri Uri, string? Authorization)> Requests { get; } = []; protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Requests.Add((request.RequestUri!, request.Headers.Authorization?.ToString())); return Task.FromResult(respond(request)); } }
    private sealed class RecordingLogger : ILogger<RenderDiskHeadroomProbe> { public List<LogEntry> Entries { get; } = []; public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null; public bool IsEnabled(LogLevel logLevel) => true; public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { Entries.Add(new(level, formatter(state, exception), (state as IEnumerable<KeyValuePair<string, object?>>)?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? [], exception)); } }
    private sealed record LogEntry(LogLevel Level, string Message, Dictionary<string, object?> State, Exception? Exception);
}
