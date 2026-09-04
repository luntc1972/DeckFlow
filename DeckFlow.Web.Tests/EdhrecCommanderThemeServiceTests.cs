using System.Net;
using System.Text.Json;
using System.Net.Http.Headers;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests the fail-open EDHREC commander theme source.</summary>
public sealed class EdhrecCommanderThemeServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "deckflow-edhrec-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetCommanderThemesAsync_SuccessfulFetch_ParsesTaglinksOrderedByDeckCount()
    {
        var handler = new RecordingHandler(Response(HttpStatusCode.OK, Taglinks(("zeta", "Zeta", 4), ("alpha", "Alpha", 9))));
        var result = await CreateService(handler).GetCommanderThemesAsync("Atraxa, Praetors' Voice");
        Assert.False(result.IsUnavailable);
        Assert.Equal(["alpha", "zeta"], result.Themes.Select(theme => theme.Slug));
        Assert.Equal(9, result.Themes[0].DeckCount);
    }

    [Fact]
    public async Task GetCommanderThemesAsync_TiedDeckCounts_OrdersBySlugOrdinal()
    {
        var handler = new RecordingHandler(Response(HttpStatusCode.OK, Taglinks(("zeta", "Zeta", 9), ("alpha", "Alpha", 9))));
        var result = await CreateService(handler).GetCommanderThemesAsync("Atraxa");
        Assert.Equal(["alpha", "zeta"], result.Themes.Select(theme => theme.Slug));
    }

    [Fact]
    public async Task GetCommanderThemesAsync_403AccessDenied_ReturnsEmptyUnavailable_AndDoesNotRetry()
    {
        var handler = new RecordingHandler(Response(HttpStatusCode.Forbidden, "<Error><Code>AccessDenied</Code></Error>"));
        var result = await CreateService(handler).GetCommanderThemesAsync("Atraxa");
        Assert.True(result.IsUnavailable);
        Assert.Empty(result.Themes);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetCommanderThemesAsync_500_ReturnsEmptyUnavailable()
    {
        var result = await CreateService(new RecordingHandler(Response(HttpStatusCode.InternalServerError, "nope"))).GetCommanderThemesAsync("Atraxa");
        Assert.True(result.IsUnavailable);
        Assert.Empty(result.Themes);
    }

    [Fact]
    public async Task GetCommanderThemesAsync_MalformedJson_ReturnsEmptyUnavailable_DoesNotThrow()
    {
        var result = await CreateService(new RecordingHandler(Response(HttpStatusCode.OK, "{"))).GetCommanderThemesAsync("Atraxa");
        Assert.True(result.IsUnavailable);
        Assert.Empty(result.Themes);
    }

    [Fact]
    public async Task GetCommanderThemesAsync_OversizeBody_RejectedWithoutParsing()
    {
        var body = new string('x', EdhrecCommanderThemeService.MaxResponseBytes + 1);
        var result = await CreateService(new RecordingHandler(Response(HttpStatusCode.OK, body))).GetCommanderThemesAsync("Atraxa");
        Assert.True(result.IsUnavailable);
        Assert.Empty(result.Themes);
    }

    [Fact]
    public async Task GetCommanderThemesAsync_SlugFailingPattern_NeverIssuesRequest()
    {
        var handler = new RecordingHandler();
        var result = await CreateService(handler).GetCommanderThemesAsync("☠");
        Assert.True(result.IsUnavailable);
        Assert.Empty(result.Themes);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetThemeCardNamesAsync_ThemeSlugFailingPattern_NeverIssuesRequest()
    {
        var handler = new RecordingHandler();
        var result = await CreateService(handler).GetThemeCardNamesAsync("Atraxa", "../counters");
        Assert.Empty(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetThemeCardNamesAsync_UnexpectedShape_ReturnsEmpty_DoesNotThrow()
    {
        var body = "{\"container\":{\"json_dict\":{\"cardlists\":{}}}}";
        var result = await CreateService(new RecordingHandler(Response(HttpStatusCode.OK, body))).GetThemeCardNamesAsync("Atraxa", "counters");
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetThemeCardNamesAsync_Duplicates_DeduplicatedPreservingFirstSeenOrder()
    {
        const string body = """{"container":{"json_dict":{"cardlists":[[{"name":"Sol Ring"},{"name":"Arcane Signet"}],[{"name":"sol ring"},{"name":"Fellwar Stone"}]]}}}""";
        var result = await CreateService(new RecordingHandler(Response(HttpStatusCode.OK, body))).GetThemeCardNamesAsync("Atraxa", "counters");
        Assert.Equal(["Sol Ring", "Arcane Signet", "Fellwar Stone"], result);
    }

    [Fact]
    public async Task GetThemeCardNamesAsync_SameCommanderAndTheme_UsesMemoryCache()
    {
        const string body = """{"container":{"json_dict":{"cardlists":[[{"name":"Sol Ring"},{"name":"Arcane Signet"}]]}}}""";
        var handler = new RecordingHandler(Response(HttpStatusCode.OK, body));
        var service = CreateService(handler);

        var first = await service.GetThemeCardNamesAsync("Atraxa", "counters");
        var second = await service.GetThemeCardNamesAsync("Atraxa", "counters");

        Assert.Equal(["Sol Ring", "Arcane Signet"], first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DiskCache_SecondCall_SendsIfNoneMatch_And304ServesCachedBody()
    {
        var handler = new RecordingHandler(Response(HttpStatusCode.OK, Taglinks(("counters", "Counters", 12)), "etag-1"), new HttpResponseMessage(HttpStatusCode.NotModified));
        var first = await CreateService(handler).GetCommanderThemesAsync("Atraxa");
        var second = await CreateService(handler).GetCommanderThemesAsync("Atraxa");
        Assert.Equal(first.Themes, second.Themes);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal("etag-1", handler.Requests[1].IfNoneMatch);
    }

    [Fact]
    public async Task DiskCache_FetchFailure_ServesFreshCachedBody()
    {
        var handler = new RecordingHandler(Response(HttpStatusCode.OK, Taglinks(("counters", "Counters", 12))), Response(HttpStatusCode.InternalServerError, "nope"));
        var first = await CreateService(handler).GetCommanderThemesAsync("Atraxa");
        var second = await CreateService(handler).GetCommanderThemesAsync("Atraxa");
        Assert.False(second.IsUnavailable);
        Assert.Equal(first.Themes, second.Themes);
    }

    [Fact]
    public async Task DiskCache_FetchFailure_DoesNotServeExpiredCachedBody()
    {
        var directory = Path.Combine(_root, "artifacts", "edhrec-themes");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "atraxa.json"), "{\"Body\":\"" + Taglinks(("counters", "Counters", 12)).Replace("\"", "\\\"") + "\",\"ETag\":null,\"WrittenAtUtc\":\"2000-01-01T00:00:00+00:00\"}");
        var result = await CreateService(new RecordingHandler(Response(HttpStatusCode.InternalServerError, "nope"))).GetCommanderThemesAsync("Atraxa");
        Assert.True(result.IsUnavailable);
    }

    [Fact]
    public async Task DiskCache_ConcurrentWrites_PublishesValidCompleteEntry()
    {
        var handler = new RecordingHandler(Enumerable.Range(0, 8).Select(_ => Response(HttpStatusCode.OK, Taglinks(("counters", "Counters", 12)))).ToArray());
        var service = CreateService(handler);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.GetCommanderThemesAsync("Atraxa")));
        var contents = await File.ReadAllTextAsync(Path.Combine(_root, "artifacts", "edhrec-themes", "atraxa.json"));
        using JsonDocument document = JsonDocument.Parse(contents);
        Assert.Equal(Taglinks(("counters", "Counters", 12)), document.RootElement.GetProperty("Body").GetString());
    }

    [Fact]
    public async Task DiskCache_Write_EvictsExpiredEntries()
    {
        var cacheDirectory = Path.Combine(_root, "artifacts", "edhrec-themes");
        Directory.CreateDirectory(cacheDirectory);
        var expiredPath = Path.Combine(cacheDirectory, "expired.json");
        await File.WriteAllTextAsync(expiredPath, "{}");
        File.SetLastWriteTimeUtc(expiredPath, DateTime.UtcNow - EdhrecCommanderThemeService.DiskCacheFallbackMaxAge - TimeSpan.FromMinutes(1));

        var result = await CreateService(new RecordingHandler(Response(HttpStatusCode.OK, Taglinks(("counters", "Counters", 12))))).GetCommanderThemesAsync("Atraxa");

        Assert.False(result.IsUnavailable);
        Assert.False(File.Exists(expiredPath));
        Assert.True(File.Exists(Path.Combine(cacheDirectory, "atraxa.json")));
    }

    [Fact]
    public async Task DiskCache_WriteFailure_StillReturnsResult()
    {
        var service = CreateService(new RecordingHandler(Response(HttpStatusCode.OK, Taglinks(("counters", "Counters", 12)))), "/proc/deckflow-tests");
        var result = await service.GetCommanderThemesAsync("Atraxa");
        Assert.False(result.IsUnavailable);
        Assert.Single(result.Themes);
    }

    [Fact]
    public async Task DiskCache_ReadFailure_FallsThroughToFetch()
    {
        var cacheDirectory = Path.Combine(_root, "artifacts", "edhrec-themes");
        Directory.CreateDirectory(cacheDirectory);
        await File.WriteAllTextAsync(Path.Combine(cacheDirectory, "atraxa.json"), "{");
        var handler = new RecordingHandler(Response(HttpStatusCode.OK, Taglinks(("counters", "Counters", 12))));
        var result = await CreateService(handler).GetCommanderThemesAsync("Atraxa");
        Assert.False(result.IsUnavailable);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public void SelectDefaultThemes_ReturnsAtMostThree()
    {
        IReadOnlyList<CutLabCommanderTheme> themes = [Theme("a", 50), Theme("b", 20), Theme("c", 15), Theme("d", 10), Theme("e", 5)];
        var selected = EdhrecCommanderThemeService.SelectDefaultThemes(themes);
        Assert.Equal(EdhrecCommanderThemeService.PreselectMaximumThemes, selected.Count);
        Assert.True(themes.Count(theme => (double)theme.DeckCount / 100 >= EdhrecCommanderThemeService.PreselectMinimumShare) > selected.Count);
    }

    [Fact]
    public void SelectDefaultThemes_ZeroTotal_ReturnsEmpty() => Assert.Empty(EdhrecCommanderThemeService.SelectDefaultThemes([Theme("a", 0)]));

    [Fact]
    public void SelectDefaultThemes_ExcludesThemesBelowMinimumShare()
    {
        IReadOnlyList<CutLabCommanderTheme> themes = [Theme("a", 95), Theme("b", 4), Theme("c", 1)];
        var selected = EdhrecCommanderThemeService.SelectDefaultThemes(themes);
        Assert.All(selected, theme => Assert.True((double)theme.DeckCount / 100 >= EdhrecCommanderThemeService.PreselectMinimumShare));
        Assert.Equal(["a"], selected.Select(theme => theme.Slug));
    }

    [Fact]
    public async Task Cancellation_PropagatesOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateService(new RecordingHandler()).GetCommanderThemesAsync("Atraxa", cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private EdhrecCommanderThemeService CreateService(RecordingHandler handler, string? contentRoot = null)
        => new(new TestHttpClientFactory(handler), new FakeResiliencePipelineProvider(), new MemoryCache(new MemoryCacheOptions()), new TestWebHostEnvironment(contentRoot ?? Path.Combine(_root, "content")));

    private static CutLabCommanderTheme Theme(string slug, int deckCount) => new() { Slug = slug, DisplayName = slug, DeckCount = deckCount };
    private static HttpResponseMessage Response(HttpStatusCode statusCode, string body, string? etag = null)
    {
        var response = new HttpResponseMessage(statusCode) { Content = new StringContent(body) };
        if (etag is not null) response.Headers.ETag = new EntityTagHeaderValue($"\"{etag}\"");
        return response;
    }

    private static string Taglinks(params (string Slug, string Value, int Count)[] tags)
        => "{\"panels\":{\"taglinks\":[" + string.Join(',', tags.Select(tag => $"{{\"slug\":\"{tag.Slug}\",\"value\":\"{tag.Value}\",\"count\":{tag.Count}}}")) + "]}}";

    private sealed class TestHttpClientFactory(RecordingHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("https://json.edhrec.com/pages/") };
    }

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public int CallCount { get; private set; }
        public List<(Uri? Uri, string? IfNoneMatch)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Requests.Add((request.RequestUri, request.Headers.IfNoneMatch.FirstOrDefault()?.Tag?.Trim('"')));
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : Response(HttpStatusCode.NotFound, string.Empty));
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DeckFlow.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
