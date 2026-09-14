using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Scryfall;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Covers <see cref="ScryfallCardResolver.ResolveSingleAsync"/>: the single-name collection lookup
/// with a normalized-name match, plus the exact-name search fallback when the collection misses.
/// </summary>
public sealed class ScryfallCardResolverTests
{
    [Fact]
    public async Task ResolveSingleAsync_CollectionHit_ReturnsMatch_WithoutFallback()
    {
        var searchCalled = false;
        var resolver = BuildResolver(
            collection: _ => Collection(Card("Jegantha, the Wellspring")),
            search: _ =>
            {
                searchCalled = true;
                return Search();
            });

        var card = await resolver.ResolveSingleAsync("Jegantha, the Wellspring", CancellationToken.None);

        Assert.NotNull(card);
        Assert.Equal("Jegantha, the Wellspring", card!.Name);
        Assert.False(searchCalled);
    }

    [Fact]
    public async Task ResolveSingleAsync_CollectionMiss_FallsBackToSearch()
    {
        var resolver = BuildResolver(
            collection: _ => Collection(),
            search: _ => Search(Card("Kaheera, the Orphanguard")));

        var card = await resolver.ResolveSingleAsync("Kaheera, the Orphanguard", CancellationToken.None);

        Assert.NotNull(card);
        Assert.Equal("Kaheera, the Orphanguard", card!.Name);
    }

    [Fact]
    public async Task ResolveSingleAsync_BothMiss_ReturnsNull()
    {
        var resolver = BuildResolver(
            collection: _ => Collection(),
            search: _ => Search());

        var card = await resolver.ResolveSingleAsync("Nonexistent Card", CancellationToken.None);

        Assert.Null(card);
    }

    [Fact]
    public async Task ResolveSingleAsync_BlankName_ReturnsNull_WithoutCallingScryfall()
    {
        var anyCall = false;
        var resolver = BuildResolver(
            collection: _ => { anyCall = true; return Collection(); },
            search: _ => { anyCall = true; return Search(); });

        var card = await resolver.ResolveSingleAsync("   ", CancellationToken.None);

        Assert.Null(card);
        Assert.False(anyCall);
    }

    [Fact]
    public async Task ResolveSingleAsync_CollectionHit_MatchesViaNormalizedName_NotExactString()
    {
        var searchCalled = false;
        var resolver = BuildResolver(
            // Upcased name: only matches the query after CardNormalizer.Normalize lowercases both.
            collection: _ => Collection(Card("JEGANTHA, THE WELLSPRING")),
            search: _ =>
            {
                searchCalled = true;
                return Search();
            });

        var card = await resolver.ResolveSingleAsync("Jegantha, the Wellspring", CancellationToken.None);

        Assert.NotNull(card);
        Assert.Equal("JEGANTHA, THE WELLSPRING", card!.Name);
        Assert.False(searchCalled);
    }

    [Fact]
    public async Task ResolveSingleAsync_CollectionNon2xx_FallsBackToSearch()
    {
        var resolver = BuildResolver(
            collection: _ => new RestResponse<ScryfallCollectionResponse>(new RestRequest("cards/collection", Method.Post))
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Data = null,
            },
            search: _ => Search(Card("Lutri, the Spellchaser")));

        var card = await resolver.ResolveSingleAsync("Lutri, the Spellchaser", CancellationToken.None);

        Assert.NotNull(card);
        Assert.Equal("Lutri, the Spellchaser", card!.Name);
    }

    [Fact]
    public async Task ResolveSingleAsync_SameIdentifierTwice_IssuesOneCollectionRequest()
    {
        var collectionCalls = 0;
        var resolver = BuildResolver(
            collection: _ => { collectionCalls++; return Collection(Card("Sol Ring")); },
            search: _ => Search());

        await resolver.ResolveSingleAsync("Sol Ring", CancellationToken.None);
        await resolver.ResolveSingleAsync("Sol Ring", CancellationToken.None);

        Assert.Equal(1, collectionCalls);
    }

    [Fact]
    public async Task ResolveSingleAsync_MultiEntryCollectionMatchNotFirst_CachesMatchedCard()
    {
        var collectionCalls = 0;
        var resolver = BuildResolver(
            collection: _ =>
            {
                collectionCalls++;
                return Collection(Card("Not Sol Ring"), Card("Sol Ring"));
            },
            search: _ => Search());

        var coldCard = await resolver.ResolveSingleAsync("Sol Ring", CancellationToken.None);
        var warmCard = await resolver.ResolveSingleAsync("Sol Ring", CancellationToken.None);

        Assert.Equal("Sol Ring", coldCard!.Name);
        Assert.Equal(coldCard, warmCard);
        Assert.Equal(1, collectionCalls);
    }

    [Fact]
    public async Task ResolveSingleAsync_CachedPositiveForDifferentName_ReissuesCollectionPost()
    {
        var collectionCalls = 0;
        var resolver = BuildResolver(
            collection: _ =>
            {
                collectionCalls++;
                return Collection(Card(collectionCalls == 1 ? "A//B" : "A // C"));
            },
            search: _ => Search());

        await resolver.ResolveSingleAsync("A//B", CancellationToken.None);
        var secondCard = await resolver.ResolveSingleAsync("A // C", CancellationToken.None);

        Assert.Equal("A // C", secondCard!.Name);
        Assert.Equal(2, collectionCalls);
    }

    [Fact]
    public async Task ResolveSingleAsync_NotFoundAlongsideUnrelatedData_CachesCollectionMiss()
    {
        var collectionCalls = 0;
        var fallbackCalls = 0;
        var resolver = BuildResolver(
            collection: _ =>
            {
                collectionCalls++;
                return new RestResponse<ScryfallCollectionResponse>(new RestRequest("cards/collection", Method.Post))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallCollectionResponse([Card("Unrelated")], [new ScryfallCollectionNameIdentifier("A")]),
                };
            },
            search: _ =>
            {
                fallbackCalls++;
                return Search(Card("A // B"));
            });

        await resolver.ResolveSingleAsync("A // B", CancellationToken.None);
        await resolver.ResolveSingleAsync("A // B", CancellationToken.None);

        Assert.Equal(1, collectionCalls);
        Assert.Equal(2, fallbackCalls);
    }

    [Fact]
    public async Task ResolveSingleAsync_DifferentSubmittedIdentifiers_DoNotShareCacheEntry()
    {
        var collectionCalls = 0;
        var resolver = BuildResolver(
            collection: request =>
            {
                collectionCalls++;
                return Collection(Card("Smuggler's Copter"));
            },
            search: _ => Search());

        await resolver.ResolveSingleAsync("Smuggler's Copter", CancellationToken.None);
        await resolver.ResolveSingleAsync("Smuggler’s Copter", CancellationToken.None);

        Assert.Equal(2, collectionCalls);
    }

    [Fact]
    public async Task ResolveSingleAsync_CombinedName_MissesCollectionAndFallsBackWithCacheActive()
    {
        var collectionCalls = 0;
        var fallbackCalls = 0;
        var resolver = BuildResolver(
            collection: request =>
            {
                collectionCalls++;
                string requestJson = System.Text.Json.JsonSerializer.Serialize(request.Parameters.First().Value);
                Assert.Contains("\"name\":\"A\"", requestJson, StringComparison.Ordinal);
                return new RestResponse<ScryfallCollectionResponse>(new RestRequest("cards/collection", Method.Post))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallCollectionResponse([], [new ScryfallCollectionNameIdentifier("A")]),
                };
            },
            search: _ =>
            {
                fallbackCalls++;
                return Search(Card("A // B"));
            });

        var coldCard = await resolver.ResolveSingleAsync("A // B", CancellationToken.None);
        var warmCard = await resolver.ResolveSingleAsync("A // C", CancellationToken.None);

        Assert.Equal(1, collectionCalls);
        Assert.Equal(2, fallbackCalls);
        Assert.Equal(coldCard, warmCard);
        Assert.Equal("A // B", warmCard!.Name);
    }

    [Fact]
    public async Task ResolveSingleAsync_CachedCollectionMiss_StillFallsBackWithoutCollectionPost()
    {
        var collectionCalls = 0;
        var fallbackCalls = 0;
        var resolver = BuildResolver(
            collection: _ =>
            {
                collectionCalls++;
                return new RestResponse<ScryfallCollectionResponse>(new RestRequest("cards/collection", Method.Post))
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallCollectionResponse([], [new ScryfallCollectionNameIdentifier("A")]),
                };
            },
            search: _ =>
            {
                fallbackCalls++;
                return Search(Card("A // B"));
            });

        await resolver.ResolveSingleAsync("A / B", CancellationToken.None);
        await resolver.ResolveSingleAsync("A / B", CancellationToken.None);

        Assert.Equal(1, collectionCalls);
        Assert.Equal(2, fallbackCalls);
    }

    [Fact]
    public async Task ResolveSingleAsync_TooManyRequests_DoesNotCache()
    {
        var collectionCalls = 0;
        var resolver = BuildResolver(
            collection: _ =>
            {
                collectionCalls++;
                return new RestResponse<ScryfallCollectionResponse>(new RestRequest("cards/collection", Method.Post))
                {
                    StatusCode = HttpStatusCode.TooManyRequests,
                    Data = new ScryfallCollectionResponse([Card("Sol Ring")], null),
                };
            },
            search: _ => Search());

        await resolver.ResolveSingleAsync("Sol Ring", CancellationToken.None);
        await resolver.ResolveSingleAsync("Sol Ring", CancellationToken.None);

        Assert.Equal(2, collectionCalls);
    }

    private static ScryfallCardResolver BuildResolver(
        Func<RestRequest, RestResponse<ScryfallCollectionResponse>> collection,
        Func<RestRequest, RestResponse<ScryfallSearchResponse>> search)
        => new(
            new FakeScryfallRestClientFactory(new HttpClient { BaseAddress = new Uri("https://api.scryfall.com/") }),
            new FakeResiliencePipelineProvider(),
            executeCollectionAsyncOverride: (request, _) => Task.FromResult(collection(request)),
            executeSearchAsyncOverride: (request, _) => Task.FromResult(search(request)));

    private static RestResponse<ScryfallCollectionResponse> Collection(params ScryfallCard[] cards)
        => new(new RestRequest("cards/collection", Method.Post))
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallCollectionResponse(new List<ScryfallCard>(cards), null),
        };

    private static RestResponse<ScryfallSearchResponse> Search(params ScryfallCard[] cards)
        => new(new RestRequest("cards/search", Method.Get))
        {
            StatusCode = cards.Length > 0 ? HttpStatusCode.OK : HttpStatusCode.NotFound,
            Data = new ScryfallSearchResponse(new List<ScryfallCard>(cards)),
        };

    private static ScryfallCard Card(string name)
        => new(
            Name: name, ManaCost: "{1}", TypeLine: "Creature", OracleText: null,
            Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
            SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
            Layout: "normal", Cmc: 1, ProducedMana: null, Rarity: "common");
}
