using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Registry;
using RestSharp;

namespace DeckFlow.Web.Services;

/// <summary>
/// Fetches community-curated oracle tags from Scryfall Tagger for a given card.
/// </summary>
public interface IScryfallTaggerService
{
    /// <summary>
    /// Looks up oracle/functional tags for the supplied card name via the Scryfall Tagger GraphQL endpoint.
    /// </summary>
    Task<IReadOnlyList<string>> LookupOracleTagsAsync(string cardName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IScryfallTaggerService"/>.
/// Resolves the card via the Scryfall REST API, then queries the Tagger GraphQL endpoint for oracle tags.
///
/// Phase 5 BUG-01: cookies are managed automatically by the typed Tagger HttpClient's
/// SocketsHttpHandler.CookieContainer (Program.cs). This service only manages the CSRF token
/// (via TaggerSessionCache) and emits structured logs at every step (Tagger.Resolve,
/// Tagger.SessionFetch, Tagger.GraphQlPost, Tagger.Parse, Tagger.Lookup, Tagger.RefreshAndRetry).
/// </summary>
public sealed class ScryfallTaggerService : IScryfallTaggerService
{
    private static readonly Uri TaggerCookieScopeUri = new("https://tagger.scryfall.com/");

    private static readonly string TaggerQuery =
        "query($set:String!,$number:String!){card:cardBySet(set:$set,number:$number){taggings{tag{name type slug}weight status}}}";

    private readonly IScryfallRestClientFactory _scryfallRestClientFactory;
    private readonly IScryfallTaggerHttpClient _taggerHttpClient;
    private readonly ITaggerSessionCache _taggerSessionCache;
    private readonly ResiliencePipeline<RestResponse> _scryfallPipeline;
    private readonly ResiliencePipeline<RestResponse> _taggerPipeline;
    private readonly ResiliencePipeline<RestResponse> _taggerPostPipeline;
    private readonly IFeatureFlagCache _flagCache;
    private readonly ILogger<ScryfallTaggerService> _logger;

    /// <summary>
    /// HIGH-1 loop guard — flows correctly across async/await boundaries.
    /// Prevents the 403-retry path from recursing if the refreshed session also returns 403.
    /// </summary>
    private static readonly AsyncLocal<bool> _attemptedRefresh = new();

    /// <summary>
    /// Creates a Tagger service backed by the typed Tagger HttpClient (auto-cookies via
    /// SocketsHttpHandler.CookieContainer per Phase 5 BUG-01), the IScryfallRestClientFactory
    /// for Scryfall card lookups, the named Polly v8 pipelines (scryfall, tagger, tagger-post),
    /// the 270s session cache (HIGH-2), and the in-process feature-flag cache used by the
    /// FLAG-04 / D-11 kill-switch gate at the top of <see cref="LookupOracleTagsAsync"/>.
    /// </summary>
    public ScryfallTaggerService(
        IScryfallRestClientFactory scryfallRestClientFactory,
        IScryfallTaggerHttpClient taggerHttpClient,
        ITaggerSessionCache taggerSessionCache,
        ResiliencePipelineProvider<string> pipelineProvider,
        IFeatureFlagCache flagCache,
        ILogger<ScryfallTaggerService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scryfallRestClientFactory);
        ArgumentNullException.ThrowIfNull(taggerHttpClient);
        ArgumentNullException.ThrowIfNull(taggerSessionCache);
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(flagCache);
        _scryfallRestClientFactory = scryfallRestClientFactory;
        _taggerHttpClient = taggerHttpClient;
        _taggerSessionCache = taggerSessionCache;
        _scryfallPipeline = pipelineProvider.GetPipeline<RestResponse>("scryfall");
        _taggerPipeline = pipelineProvider.GetPipeline<RestResponse>("tagger");
        _taggerPostPipeline = pipelineProvider.GetPipeline<RestResponse>("tagger-post");
        _flagCache = flagCache;
        _logger = logger ?? NullLogger<ScryfallTaggerService>.Instance;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> LookupOracleTagsAsync(string cardName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);

        // FLAG-04, D-11: kill-switch gate. Off → return empty without any HTTP work.
        if (!_flagCache.IsEnabled("scryfall.tagger.enabled"))
        {
            return Array.Empty<string>();
        }

        var stopwatch = Stopwatch.StartNew();
        var trimmedName = cardName.Trim();

        var (set, collectorNumber) = await ResolveCardPrintingAsync(trimmedName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(set) || string.IsNullOrEmpty(collectorNumber))
        {
            return [];
        }

        // Cache-first: serve a fresh cached session, fetch on miss.
        var session = _taggerSessionCache.TryGet();
        if (session is null)
        {
            session = await FetchTaggerSessionAsync(trimmedName, set, collectorNumber, cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                return [];
            }
            _taggerSessionCache.Set(session);
        }
        else if (_taggerSessionCache.IsApproachingExpiry())
        {
            // HIGH-2: cached session age >= 240s but TTL not yet hit. Trigger background refresh
            // so the next request gets a fresh CSRF token while the current request still uses
            // the cached value. Decouples session expiry from the 5-min HandlerLifetime rotation.
            var bgCardName = trimmedName;
            var bgSet = set;
            var bgNumber = collectorNumber;
            _ = Task.Run(async () =>
            {
                try
                {
                    var refreshed = await FetchTaggerSessionAsync(bgCardName, bgSet, bgNumber, CancellationToken.None).ConfigureAwait(false);
                    if (refreshed is not null) _taggerSessionCache.Set(refreshed);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Background Tagger session refresh failed; cached value remains.");
                }
            });
        }

        return await QueryTaggerGraphQlAsync(trimmedName, set, collectorNumber, session, stopwatch, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Calls the Scryfall REST API to resolve a card name into its default set code and collector number.
    /// </summary>
    private async Task<(string Set, string CollectorNumber)> ResolveCardPrintingAsync(string cardName, CancellationToken cancellationToken)
    {
        var resolveStopwatch = Stopwatch.StartNew();
        var scryfallClient = _scryfallRestClientFactory.Create();
        var request = new RestRequest("cards/named", Method.Get);
        request.AddQueryParameter("exact", cardName);

        var response = await ScryfallThrottle.ExecuteAsync(
            ct => _scryfallPipeline.ExecuteAsync(
                async pollyCt => await scryfallClient.ExecuteAsync(request, pollyCt).ConfigureAwait(false),
                ct).AsTask(),
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
        {
            _logger.LogWarning(
                "Tagger.Resolve failed for {CardName}: HTTP {StatusCode} in {ElapsedMs}ms",
                cardName, (int)response.StatusCode, resolveStopwatch.ElapsedMilliseconds);
            return (string.Empty, string.Empty);
        }

        using var document = JsonDocument.Parse(response.Content);
        var root = document.RootElement;

        var set = root.TryGetProperty("set", out var setProp) ? setProp.GetString() ?? string.Empty : string.Empty;
        var number = root.TryGetProperty("collector_number", out var numProp) ? numProp.GetString() ?? string.Empty : string.Empty;

        return (set, number);
    }

    /// <summary>
    /// Fetches a Tagger card page via the typed auto-cookie HttpClient (Phase 5 BUG-01) and
    /// extracts the CSRF token. The session cookie is captured automatically by the
    /// SocketsHttpHandler.CookieContainer for replay on the subsequent /graphql POST.
    /// </summary>
    private async Task<TaggerSession?> FetchTaggerSessionAsync(string cardName, string set, string collectorNumber, CancellationToken cancellationToken)
    {
        var sessionStopwatch = Stopwatch.StartNew();
        var taggerRestClient = new RestClient(_taggerHttpClient.Inner);
        var pageRequest = new RestRequest($"card/{set}/{collectorNumber}", Method.Get);
        // Phase 5 BUG-01 follow-up: explicit Accept for the HTML page GET so Cloudflare's
        // BIC sees the request as browser-shaped (the GraphQL POST sets its own JSON Content-Type
        // and doesn't need this).
        pageRequest.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");

        var pageResponse = await _taggerPipeline.ExecuteAsync(
            async ct => await taggerRestClient.ExecuteAsync(pageRequest, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (!pageResponse.IsSuccessful || string.IsNullOrEmpty(pageResponse.Content))
        {
            _logger.LogWarning(
                "Tagger.SessionFetch failed for {CardName} ({Set}/{Number}): HTTP {StatusCode} in {ElapsedMs}ms; csrf={CsrfPresent} cookies={CookieCount}",
                cardName, set, collectorNumber, (int)pageResponse.StatusCode, sessionStopwatch.ElapsedMilliseconds, false, CountTaggerCookies());
            return null;
        }

        var token = ScryfallTaggerParsers.TryExtractCsrfToken(pageResponse.Content);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning(
                "Tagger.SessionFetch failed for {CardName} ({Set}/{Number}): HTTP {StatusCode} in {ElapsedMs}ms; csrf={CsrfPresent} cookies={CookieCount}",
                cardName, set, collectorNumber, (int)pageResponse.StatusCode, sessionStopwatch.ElapsedMilliseconds, false, CountTaggerCookies());
            return null;
        }

        // Cookies are now managed automatically by SocketsHttpHandler.CookieContainer (Program.cs Tagger
        // handler config — UseCookies=true, Phase 5 BUG-01). The session cookie set by this GET response
        // is auto-replayed on the subsequent /graphql POST through the same typed client.
        return new TaggerSession(token, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Reads the live cookie count for the Tagger BaseAddress from the shared CookieContainer
    /// owned by the SocketsHttpHandler primary handler (Program.cs). Used by the
    /// Tagger.SessionFetch log line {CookieCount} slot. Defensive try/catch returns 0 if
    /// the container is somehow unavailable — should never trigger in production.
    /// </summary>
    private int CountTaggerCookies()
    {
        try
        {
            return _taggerHttpClient.Cookies.GetCookies(TaggerCookieScopeUri).Count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Posts the GraphQL query to the Tagger endpoint via the tagger-post pipeline (retry=0
    /// because GraphQL POST is non-idempotent). On 403 invokes
    /// <see cref="RefreshSessionAndRetryAsync"/> to satisfy SC-2.
    /// </summary>
    private async Task<IReadOnlyList<string>> QueryTaggerGraphQlAsync(
        string cardName,
        string set,
        string collectorNumber,
        TaggerSession session,
        Stopwatch outerStopwatch,
        CancellationToken cancellationToken)
    {
        var postStopwatch = Stopwatch.StartNew();
        var response = await ExecuteTaggerPostAsync(set, collectorNumber, session, cancellationToken).ConfigureAwait(false);
        postStopwatch.Stop();

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            // HIGH-1: 403 received - invalidate stale session, force-refresh, retry POST once.
            return await RefreshSessionAndRetryAsync(cardName, set, collectorNumber, outerStopwatch, cancellationToken).ConfigureAwait(false);
        }

        if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
        {
            _logger.LogWarning(
                "Tagger.GraphQlPost failed for {CardName} ({Set}/{Number}): HTTP {StatusCode} in {ElapsedMs}ms",
                cardName, set, collectorNumber, (int)response.StatusCode, postStopwatch.ElapsedMilliseconds);
            return Array.Empty<string>();
        }

        var tags = ScryfallTaggerParsers.ParseOracleTagsFromJson(response.Content);
        if (tags.Count == 0)
        {
            _logger.LogWarning(
                "Tagger.Parse failed for {CardName}: {Reason}",
                cardName, "ParseOracleTagsFromJson returned empty list for 200-OK response");
        }

        _logger.LogInformation(
            "Tagger.Lookup succeeded for {CardName} in {ElapsedMs}ms returning {TagCount} tags",
            cardName, outerStopwatch.ElapsedMilliseconds, tags.Count);
        return tags;
    }

    /// <summary>
    /// Executes a single Tagger GraphQL POST with the supplied session credentials. The
    /// session cookie is replayed automatically by the SocketsHttpHandler.CookieContainer
    /// (Phase 5 BUG-01); only the CSRF header is set explicitly here.
    /// </summary>
    private async Task<RestResponse> ExecuteTaggerPostAsync(
        string set,
        string collectorNumber,
        TaggerSession session,
        CancellationToken cancellationToken)
    {
        var taggerRestClient = new RestClient(_taggerHttpClient.Inner);
        var graphqlRequest = new RestRequest("graphql", Method.Post);
        graphqlRequest.AddHeader("Accept", "application/json");
        graphqlRequest.AddHeader("X-CSRF-Token", session.CsrfToken);

        var payload = JsonSerializer.Serialize(new
        {
            query = TaggerQuery,
            variables = new { set, number = collectorNumber }
        });
        graphqlRequest.AddStringBody(payload, ContentType.Json);

        return await _taggerPostPipeline.ExecuteAsync(
            async ct => await taggerRestClient.ExecuteAsync(graphqlRequest, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// HIGH-1 fix: On 403 from the tagger-post pipeline, invalidates the cached session,
    /// fetches a fresh session (new CSRF token), and retries the POST exactly once.
    /// A max-1-retry guard (_attemptedRefresh AsyncLocal) prevents infinite loops.
    /// Degrades to empty result if the retry also fails or returns 403.
    /// </summary>
    private async Task<IReadOnlyList<string>> RefreshSessionAndRetryAsync(
        string cardName,
        string set,
        string collectorNumber,
        Stopwatch outerStopwatch,
        CancellationToken cancellationToken)
    {
        if (_attemptedRefresh.Value)
        {
            // Already retried once - prevent infinite loop, degrade gracefully.
            _logger.LogWarning("Tagger GraphQL 403 persisted after session refresh for {Set}/{Number}; degrading to empty", set, collectorNumber);
            _taggerSessionCache.Invalidate();
            return Array.Empty<string>();
        }

        _logger.LogWarning(
            "Tagger.RefreshAndRetry triggered for {CardName} ({Set}/{Number}) after 403",
            cardName, set, collectorNumber);

        _attemptedRefresh.Value = true;
        try
        {
            _taggerSessionCache.Invalidate();
            var freshSession = await FetchTaggerSessionAsync(cardName, set, collectorNumber, cancellationToken).ConfigureAwait(false);
            if (freshSession is null)
            {
                _logger.LogWarning("Tagger session refresh failed for {Set}/{Number}; degrading to empty", set, collectorNumber);
                return Array.Empty<string>();
            }

            _taggerSessionCache.Set(freshSession);

            var retryResponse = await ExecuteTaggerPostAsync(set, collectorNumber, freshSession, cancellationToken).ConfigureAwait(false);
            if (!retryResponse.IsSuccessful || string.IsNullOrEmpty(retryResponse.Content))
            {
                _logger.LogWarning("Tagger GraphQL retry failed: {Status}", retryResponse.StatusCode);
                _taggerSessionCache.Invalidate();
                return Array.Empty<string>();
            }

            var tags = ScryfallTaggerParsers.ParseOracleTagsFromJson(retryResponse.Content);
            _logger.LogInformation(
                "Tagger.Lookup succeeded for {CardName} in {ElapsedMs}ms returning {TagCount} tags",
                cardName, outerStopwatch.ElapsedMilliseconds, tags.Count);
            return tags;
        }
        finally
        {
            _attemptedRefresh.Value = false;
        }
    }
}
