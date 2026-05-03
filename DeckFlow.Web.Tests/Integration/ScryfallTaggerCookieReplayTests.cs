using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using DeckFlow.Web.Services;
using DeckFlow.Web.Tests;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Phase 5 BUG-01 regression guard. Exercises the FULL Tagger flow
/// (cards/named -> page-GET -> graphql-POST) against a localhost HttpListener
/// stub using a REAL SocketsHttpHandler — unlike MockHttp-based unit tests,
/// this verifies that the SocketsHttpHandler.CookieContainer auto-replays the
/// session cookie on the POST. Closes the verification gap that let commit
/// 4db8b8a ship without testing the GraphQL POST leg (Phase 4 lesson #4).
/// </summary>
public sealed class ScryfallTaggerCookieReplayTests : IDisposable
{
    private const string ScryfallNamedJson =
        """{"object":"card","id":"abc123","name":"Thrasios, Triton Hero","set":"lea","collector_number":"161"}""";

    private const string TaggerCsrfHtml =
        """<html><head><meta name="csrf-token" content="test-csrf-token"/></head><body></body></html>""";

    private const string TaggerGraphQlJson =
        """{"data":{"card":{"taggings":[{"tag":{"name":"ramp","type":"ORACLE_CARD_TAG","slug":"ramp","weight":1,"status":"APPROVED"}}]}}}""";

    private const string TaggerGraphQlEmptyJson =
        """{"data":{"card":{"taggings":[]}}}""";

    private const string SessionCookieValue = "test-session-cookie";

    private readonly HttpListener _listener;
    private readonly string _baseUrl;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Task _serverTask;
    private string? _lastPostCookieHeader;

    public ScryfallTaggerCookieReplayTests()
    {
        var port = GrabFreePort();
        _baseUrl = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(_baseUrl);
        _listener.Start();
        _serverTask = Task.Run(() => RunServerAsync(_serverCts.Token));
    }

    public void Dispose()
    {
        _serverCts.Cancel();
        try { _listener.Stop(); } catch { /* ignore */ }
        try { _serverTask.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _serverCts.Dispose();
        _listener.Close();
    }

    [Fact]
    public async Task LookupOracleTagsAsync_AgainstLocalhostStub_RepliesWithCookieAutomatically()
    {
        using var taggerHandler = new SocketsHttpHandler
        {
            UseCookies = true,
            AllowAutoRedirect = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        using var taggerHttpClient = new HttpClient(taggerHandler) { BaseAddress = new Uri(_baseUrl) };
        taggerHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow-IntegrationTest/1.0");

        using var scryfallHandler = new SocketsHttpHandler();
        using var scryfallHttpClient = new HttpClient(scryfallHandler) { BaseAddress = new Uri(_baseUrl) };
        scryfallHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow-IntegrationTest/1.0");

        var typedTaggerClient = new ScryfallTaggerHttpClient(taggerHttpClient);
        var restFactory = new FakeScryfallRestClientFactory(scryfallHttpClient);
        var sessionCache = new TaggerSessionCache(new MemoryCache(new MemoryCacheOptions()));

        var sut = new ScryfallTaggerService(
            restFactory,
            typedTaggerClient,
            sessionCache,
            new FakeResiliencePipelineProvider(),
            new FakeFeatureFlagCache());

        var tags = await sut.LookupOracleTagsAsync("Thrasios, Triton Hero", CancellationToken.None);

        Assert.NotEmpty(tags);
        Assert.NotNull(_lastPostCookieHeader);
        Assert.Contains("_scryfall_tagger_session=test-session-cookie", _lastPostCookieHeader);
    }

    [Fact]
    public async Task LookupOracleTagsAsync_AgainstLocalhostStub_PostMissingCookieWhenUseCookiesFalse()
    {
        // Meta-test: confirm the cookie-presence assertion in the happy-path test is
        // meaningful (not a tautology). With UseCookies=false on the handler AND the
        // post-Phase-5 service that does NOT add a manual Cookie header, the POST
        // arrives WITHOUT the session cookie.
        using var taggerHandler = new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = true };
        using var taggerHttpClient = new HttpClient(taggerHandler) { BaseAddress = new Uri(_baseUrl) };
        taggerHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow-IntegrationTest/1.0");

        using var scryfallHandler = new SocketsHttpHandler();
        using var scryfallHttpClient = new HttpClient(scryfallHandler) { BaseAddress = new Uri(_baseUrl) };
        scryfallHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow-IntegrationTest/1.0");

        var typedTaggerClient = new ScryfallTaggerHttpClient(taggerHttpClient);
        var restFactory = new FakeScryfallRestClientFactory(scryfallHttpClient);
        var sessionCache = new TaggerSessionCache(new MemoryCache(new MemoryCacheOptions()));

        var sut = new ScryfallTaggerService(
            restFactory,
            typedTaggerClient,
            sessionCache,
            new FakeResiliencePipelineProvider(),
            new FakeFeatureFlagCache());

        _lastPostCookieHeader = null;
        await sut.LookupOracleTagsAsync("Thrasios, Triton Hero", CancellationToken.None);

        Assert.True(string.IsNullOrEmpty(_lastPostCookieHeader),
            $"Expected no Cookie header on POST when handler UseCookies=false and service does not write manual Cookie. Saw: '{_lastPostCookieHeader}'");
    }

    private async Task RunServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) { return; }
            catch (HttpListenerException) { return; }
            catch (ObjectDisposedException) { return; }

            _ = Task.Run(() => HandleAsync(context), cancellationToken);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? string.Empty;
            if (path.StartsWith("/cards/named", StringComparison.OrdinalIgnoreCase))
            {
                await WriteAsync(context.Response, HttpStatusCode.OK, "application/json", ScryfallNamedJson);
            }
            else if (path.Equals("/card/lea/161", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers.Add("Set-Cookie", $"_scryfall_tagger_session={SessionCookieValue}; Path=/; HttpOnly");
                await WriteAsync(context.Response, HttpStatusCode.OK, "text/html", TaggerCsrfHtml);
            }
            else if (path.Equals("/graphql", StringComparison.OrdinalIgnoreCase) && context.Request.HttpMethod == "POST")
            {
                var cookieHeader = context.Request.Headers["Cookie"];
                Volatile.Write(ref _lastPostCookieHeader, cookieHeader);
                if (string.IsNullOrEmpty(cookieHeader))
                {
                    await WriteAsync(context.Response, HttpStatusCode.OK, "application/json", TaggerGraphQlEmptyJson);
                }
                else
                {
                    await WriteAsync(context.Response, HttpStatusCode.OK, "application/json", TaggerGraphQlJson);
                }
            }
            else
            {
                await WriteAsync(context.Response, HttpStatusCode.NotFound, "text/plain", $"unmapped {path}");
            }
        }
        catch
        {
            try { context.Response.Close(); } catch { }
        }
    }

    private static async Task WriteAsync(HttpListenerResponse response, HttpStatusCode status, string contentType, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = (int)status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }

    private static int GrabFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
