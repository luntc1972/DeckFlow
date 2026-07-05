using System.Net;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Packets;
using DeckFlow.Web.Services.Scryfall;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Fixture-driven tests for <see cref="ScryfallReferenceResolver"/> -- no live HTTP. Each test
/// constructs a REAL <see cref="ScryfallCardResolver"/> with deterministic
/// <c>executeCollectionAsyncOverride</c>/<c>executeSearchAsyncOverride</c> fixtures (mirroring
/// <c>DeckAnalysisPacketServiceTests.CreateService</c>'s test seam), so the resolver under test
/// wraps the real production collaborator, not a hand-rolled substitute.
/// </summary>
public sealed class ScryfallReferenceResolverTests
{
    /// <summary>
    /// H2 lock: a single-slash Archidekt-style name ("A / B") normalized on submission to the
    /// double-slash Scryfall form ("A // B") must NOT match its own original request in the
    /// collection match-back step (original "A / B" != returned "A // B"), so it falls through to
    /// the supplied fallback strategy (Analysis's SearchPrintingFallback-style delegate) and is
    /// keyed by the ORIGINAL name, not the normalized submission or the returned card name.
    /// </summary>
    [Fact]
    public async Task ResolveBatchAsync_SingleSlashNameWithNormalizeOn_FallsThroughToFallbackKeyedByOriginalName()
    {
        var doubleSlashCard = CreateCard("A // B");
        var resolver = CreateResolver(executeCollectionAsync: (request, _) =>
            Task.FromResult(CreateCollectionResponse(new List<ScryfallCard> { doubleSlashCard })));

        var fallbackInvocations = new List<string>();
        Task<ScryfallCard?> Fallback(string name, CancellationToken _)
        {
            fallbackInvocations.Add(name);
            return Task.FromResult<ScryfallCard?>(doubleSlashCard);
        }

        var result = await resolver.ResolveBatchAsync(
            new[] { "A / B" },
            Fallback,
            normalizeForScryfall: true,
            CancellationToken.None);

        Assert.Equal(new[] { "A / B" }, fallbackInvocations);
        var resolution = Assert.Single(result.Resolutions);
        Assert.Equal("A / B", resolution.RequestName);
        Assert.Equal("A // B", resolution.Card.Name);
        Assert.True(resolution.FromFallback);
        Assert.Equal("A // B", result.OracleNameMap["A / B"]);
    }

    /// <summary>
    /// A printed-name request that misses the collection lookup recovers via the supplied fallback
    /// delegate (Comparison/MetaGap's SearchFallback-style strategy) with normalize OFF (default);
    /// the resolution is keyed by the original request name and flagged FromFallback.
    /// </summary>
    [Fact]
    public async Task ResolveBatchAsync_CollectionMissWithNormalizeOff_RecoversViaFallback()
    {
        var resolver = CreateResolver(executeCollectionAsync: (request, _) =>
            Task.FromResult(CreateCollectionResponse(new List<ScryfallCard>())));

        var oracleCard = CreateCard("Oracle Name");
        Task<ScryfallCard?> Fallback(string name, CancellationToken _)
            => Task.FromResult<ScryfallCard?>(oracleCard);

        var result = await resolver.ResolveBatchAsync(
            new[] { "Printed Name" },
            Fallback,
            normalizeForScryfall: false,
            CancellationToken.None);

        var resolution = Assert.Single(result.Resolutions);
        Assert.Equal("Printed Name", resolution.RequestName);
        Assert.Equal("Oracle Name", resolution.Card.Name);
        Assert.True(resolution.FromFallback);
        Assert.Equal("Oracle Name", result.OracleNameMap["Printed Name"]);
    }

    /// <summary>
    /// A clean collection hit for two names is keyed by original name, FromFallback=false, and the
    /// resolutions preserve ORIGINAL REQUEST ORDER regardless of the order Scryfall returned them in.
    /// The fallback delegate must never be invoked for a full collection hit.
    /// </summary>
    [Fact]
    public async Task ResolveBatchAsync_CleanCollectionHit_PreservesOriginalOrderAndNeverCallsFallback()
    {
        var solRing = CreateCard("Sol Ring");
        var arcaneSignet = CreateCard("Arcane Signet");
        var resolver = CreateResolver(executeCollectionAsync: (request, _) =>
            // Response order deliberately reversed relative to the request order below.
            Task.FromResult(CreateCollectionResponse(new List<ScryfallCard> { arcaneSignet, solRing })));

        Task<ScryfallCard?> Fallback(string name, CancellationToken _)
            => throw new InvalidOperationException($"Fallback must not be invoked for a full collection hit (got: {name}).");

        var result = await resolver.ResolveBatchAsync(
            new[] { "Sol Ring", "Arcane Signet" },
            Fallback,
            normalizeForScryfall: false,
            CancellationToken.None);

        Assert.Equal(2, result.Resolutions.Count);
        Assert.Equal("Sol Ring", result.Resolutions[0].RequestName);
        Assert.False(result.Resolutions[0].FromFallback);
        Assert.Equal("Arcane Signet", result.Resolutions[1].RequestName);
        Assert.False(result.Resolutions[1].FromFallback);
    }

    /// <summary>
    /// OracleNameMap[originalName] == the RETURNED card's Name for both a collection hit and a
    /// fallback-recovered miss within the same batch call.
    /// </summary>
    [Fact]
    public async Task ResolveBatchAsync_MixedHitAndFallback_OracleNameMapKeyedByOriginalNameForBoth()
    {
        var solRing = CreateCard("Sol Ring");
        var resolver = CreateResolver(executeCollectionAsync: (request, _) =>
            Task.FromResult(CreateCollectionResponse(new List<ScryfallCard> { solRing })));

        var resolvedMiss = CreateCard("Resolved Miss");
        Task<ScryfallCard?> Fallback(string name, CancellationToken _)
            => Task.FromResult<ScryfallCard?>(resolvedMiss);

        var result = await resolver.ResolveBatchAsync(
            new[] { "Sol Ring", "Miss Card" },
            Fallback,
            normalizeForScryfall: false,
            CancellationToken.None);

        Assert.Equal("Sol Ring", result.OracleNameMap["Sol Ring"]);
        Assert.Equal("Resolved Miss", result.OracleNameMap["Miss Card"]);
    }

    /// <summary>Empty input yields an empty resolution with no HTTP calls (collection endpoint never invoked).</summary>
    [Fact]
    public async Task ResolveBatchAsync_EmptyInput_ReturnsEmptyResolutionWithNoHttpCalls()
    {
        var collectionCallCount = 0;
        var resolver = CreateResolver(executeCollectionAsync: (request, _) =>
        {
            collectionCallCount++;
            return Task.FromResult(CreateCollectionResponse(new List<ScryfallCard>()));
        });

        Task<ScryfallCard?> Fallback(string name, CancellationToken _)
            => throw new InvalidOperationException("Fallback must not be invoked for empty input.");

        var result = await resolver.ResolveBatchAsync(
            Array.Empty<string>(),
            Fallback,
            normalizeForScryfall: false,
            CancellationToken.None);

        Assert.Empty(result.Resolutions);
        Assert.Empty(result.OracleNameMap);
        Assert.Equal(0, collectionCallCount);
    }

    /// <summary>A non-2xx / null-Data collection response throws an HttpRequestException (the
    /// ScryfallReferenceCollectionException subclass) with the upstream status preserved — the broad
    /// catch the controllers rely on still matches.</summary>
    [Fact]
    public async Task ResolveBatchAsync_NonSuccessCollectionResponse_ThrowsWithUpstreamStatus()
    {
        var resolver = CreateResolver(executeCollectionAsync: (request, _) =>
            Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Data = null,
            }));

        Task<ScryfallCard?> Fallback(string name, CancellationToken _)
            => throw new InvalidOperationException("Fallback must not be invoked when the collection call itself fails.");

        var exception = await Assert.ThrowsAnyAsync<HttpRequestException>(() =>
            resolver.ResolveBatchAsync(new[] { "Sol Ring" }, Fallback, normalizeForScryfall: false, CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    /// <summary>
    /// The collection-call failure surfaces as the DISTINCT <see cref="ScryfallReferenceCollectionException"/>
    /// (not a plain <see cref="HttpRequestException"/>) so consuming services can re-label ONLY it (WR-01).
    /// </summary>
    [Fact]
    public async Task ResolveBatchAsync_NonSuccessCollectionResponse_ThrowsCollectionExceptionType()
    {
        var resolver = CreateResolver(executeCollectionAsync: (request, _) =>
            Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Data = null,
            }));

        Task<ScryfallCard?> Fallback(string name, CancellationToken _)
            => throw new InvalidOperationException("Fallback must not be invoked when the collection call itself fails.");

        var exception = await Assert.ThrowsAsync<ScryfallReferenceCollectionException>(() =>
            resolver.ResolveBatchAsync(new[] { "Sol Ring" }, Fallback, normalizeForScryfall: false, CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    /// <summary>
    /// A failure raised INSIDE the caller's fallback delegate propagates unwrapped — it is NOT converted
    /// into a <see cref="ScryfallReferenceCollectionException"/> — so the caller's original error message
    /// (and its downstream routing) is preserved (WR-01).
    /// </summary>
    [Fact]
    public async Task ResolveBatchAsync_FallbackDelegateThrows_PropagatesOriginalExceptionUnwrapped()
    {
        // Collection succeeds (200) but returns no card for the request -> the miss dispatches the
        // fallback delegate, which here fails upstream.
        var resolver = CreateResolver(executeCollectionAsync: (request, _) =>
            Task.FromResult(CreateCollectionResponse(new List<ScryfallCard>())));

        var fallbackFailure = new HttpRequestException(
            "Scryfall fallback lookup failed while resolving Sol Ring with HTTP 503.",
            null,
            HttpStatusCode.ServiceUnavailable);

        Task<ScryfallCard?> Fallback(string name, CancellationToken _) => throw fallbackFailure;

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() =>
            resolver.ResolveBatchAsync(new[] { "Sol Ring" }, Fallback, normalizeForScryfall: false, CancellationToken.None));

        Assert.Same(fallbackFailure, thrown);
        Assert.IsNotType<ScryfallReferenceCollectionException>(thrown);
    }

    private static ScryfallReferenceResolver CreateResolver(
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>> executeCollectionAsync)
    {
        var cardResolver = new ScryfallCardResolver(
            new FakeScryfallRestClientFactory(new HttpClient { BaseAddress = new Uri("https://api.scryfall.com/") }),
            new FakeResiliencePipelineProvider(),
            executeCollectionAsyncOverride: executeCollectionAsync,
            executeSearchAsyncOverride: (request, _) =>
                Task.FromResult(new RestResponse<ScryfallSearchResponse>(request)
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallSearchResponse(new List<ScryfallCard>()),
                }),
            executeNamedAsyncOverride: (request, _) =>
                Task.FromResult(new RestResponse<ScryfallCard>(request)
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Data = null,
                }));

        return new ScryfallReferenceResolver(cardResolver);
    }

    private static RestResponse<ScryfallCollectionResponse> CreateCollectionResponse(List<ScryfallCard> cards)
        => new(new RestRequest("cards/collection", Method.Post))
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallCollectionResponse(cards, []),
        };

    private static ScryfallCard CreateCard(string name)
        => new(
            Name: name,
            ManaCost: null,
            TypeLine: "Artifact",
            OracleText: null,
            Power: null,
            Toughness: null,
            Keywords: null,
            ColorIdentity: null,
            SetCode: null,
            SetName: null,
            CollectorNumber: null);
}
