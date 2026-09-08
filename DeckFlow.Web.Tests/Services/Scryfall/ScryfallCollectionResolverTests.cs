using System.Net;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Scryfall;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests.Services.Scryfall;

/// <summary>
/// Covers the collection resolver's identifier construction, batching, and failure contract directly.
/// </summary>
public sealed class ScryfallCollectionResolverTests
{
    [Fact]
    public async Task ResolveCardsAsync_EmptyDeck_ReturnsEmptyWithoutCallingDelegate()
    {
        int calls = 0;

        var result = await ScryfallCollectionResolver.ResolveCardsAsync([], (_, _) =>
        {
            calls++;
            return Task.FromResult(MakeCollectionResponse(HttpStatusCode.OK, []));
        }, "empty deck", CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ResolveCardsAsync_PrintingEntries_ReturnsPayloadCards()
    {
        ScryfallCard card = MakeCard("Sol Ring", "cmr", "331");
        var result = await ResolveAsync([MakeEntry("Sol Ring", "cmr", "331")], [MakeCollectionResponse(HttpStatusCode.OK, [card])]);

        Assert.Equal([card], result);
    }

    [Fact]
    public async Task ResolveCardsAsync_NameOnlyEntries_ReturnsPayloadCards()
    {
        ScryfallCard card = MakeCard("Sol Ring", "cmr", "331");
        var result = await ResolveAsync([MakeEntry("Sol Ring", null, null)], [MakeCollectionResponse(HttpStatusCode.OK, [card])]);

        Assert.Equal([card], result);
    }

    /// <summary>
    /// A full batch of distinct printings plus one case-variant duplicate of an existing printing
    /// (76 raw identifiers) collapses to exactly one batch of 75 only if the resolver's printing-key
    /// HashSet dedup is intact; deleting that dedup would push the raw count into a second batch.
    /// </summary>
    [Fact]
    public async Task ResolveCardsAsync_CaseVariantPrintingStraddlesBatchBoundary_CollapsesToOneCall()
    {
        int calls = 0;
        List<DeckEntry> entries = [.. MakeEntries(ScryfallLimits.CollectionBatchSize), MakeEntry("Card 1", "DFC", "1")];

        await ScryfallCollectionResolver.ResolveCardsAsync(entries, (_, _) =>
        {
            calls++;
            return Task.FromResult(MakeCollectionResponse(HttpStatusCode.OK, []));
        }, "case dedup", CancellationToken.None);

        Assert.Equal(1, calls);
    }

    /// <summary>
    /// A full batch of distinct name-only entries plus one exact-duplicate name (76 raw identifiers)
    /// collapses to exactly one batch of 75 only if the resolver's name-key HashSet dedup is intact;
    /// deleting that dedup would push the raw count into a second batch.
    /// </summary>
    [Fact]
    public async Task ResolveCardsAsync_DuplicateNameOnlyEntryStraddlesBatchBoundary_CollapsesToOneCall()
    {
        int calls = 0;
        List<DeckEntry> entries =
        [
            .. Enumerable.Range(1, ScryfallLimits.CollectionBatchSize).Select(index => MakeEntry($"Card {index}", null, null)),
            MakeEntry("Card 1", null, null),
        ];

        await ScryfallCollectionResolver.ResolveCardsAsync(entries, (_, _) =>
        {
            calls++;
            return Task.FromResult(MakeCollectionResponse(HttpStatusCode.OK, []));
        }, "name dedup", CancellationToken.None);

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ResolveCardsAsync_ExactlyBatchSize_CallsDelegateOnce()
    {
        int calls = 0;
        await ScryfallCollectionResolver.ResolveCardsAsync(
            MakeEntries(ScryfallLimits.CollectionBatchSize),
            (_, _) =>
            {
                calls++;
                return Task.FromResult(MakeCollectionResponse(HttpStatusCode.OK, []));
            },
            "batch boundary",
            CancellationToken.None);

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ResolveCardsAsync_OneMoreThanBatchSize_CallsDelegateTwice()
    {
        int calls = 0;
        await ScryfallCollectionResolver.ResolveCardsAsync(
            MakeEntries(ScryfallLimits.CollectionBatchSize + 1),
            (_, _) =>
            {
                calls++;
                return Task.FromResult(MakeCollectionResponse(HttpStatusCode.OK, []));
            },
            "batch boundary",
            CancellationToken.None);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ResolveCardsAsync_MultipleBatches_ConcatenatesCardsInBatchOrder()
    {
        ScryfallCard first = MakeCard("First", "dfc", "1");
        ScryfallCard second = MakeCard("Second", "dfc", "2");
        var result = await ResolveAsync(
            MakeEntries(ScryfallLimits.CollectionBatchSize + 1),
            [MakeCollectionResponse(HttpStatusCode.OK, [first]), MakeCollectionResponse(HttpStatusCode.OK, [second])]);

        Assert.Equal([first, second], result);
    }

    [Fact]
    public async Task ResolveCardsAsync_ServiceUnavailable_ThrowsWithSuffixAndStatusCode()
    {
        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() => ResolveAsync(
            [MakeEntry("Sol Ring", "cmr", "331")],
            [MakeCollectionResponse(HttpStatusCode.ServiceUnavailable, [])],
            "deck conversion"));

        Assert.Contains("deck conversion", exception.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Fact]
    public async Task ResolveCardsAsync_NullPayload_ThrowsHttpRequestException()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => ResolveAsync(
            [MakeEntry("Sol Ring", "cmr", "331")],
            [MakeCollectionResponse(HttpStatusCode.OK, null)]));
    }

    [Fact]
    public async Task ResolveCardsAsync_SecondBatchFails_ThrowsWithoutReturningFirstBatch()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => ResolveAsync(
            MakeEntries(ScryfallLimits.CollectionBatchSize + 1),
            [MakeCollectionResponse(HttpStatusCode.OK, [MakeCard("First", "dfc", "1")]),
                MakeCollectionResponse(HttpStatusCode.ServiceUnavailable, [])]));
    }

    [Fact]
    public async Task ResolveCardsAsync_NullDeckCards_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => ScryfallCollectionResolver.ResolveCardsAsync(
            null!, (_, _) => Task.FromResult(MakeCollectionResponse(HttpStatusCode.OK, [])), "guards", CancellationToken.None));
    }

    [Fact]
    public async Task ResolveCardsAsync_NullDelegate_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => ScryfallCollectionResolver.ResolveCardsAsync(
            [], null!, "guards", CancellationToken.None));
    }

    [Fact]
    public async Task ResolveCardsAsync_WhitespaceSuffix_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ScryfallCollectionResolver.ResolveCardsAsync(
            [], (_, _) => Task.FromResult(MakeCollectionResponse(HttpStatusCode.OK, [])), " ", CancellationToken.None));
    }

    private static async Task<IReadOnlyList<ScryfallCard>> ResolveAsync(
        IReadOnlyList<DeckEntry> entries,
        IReadOnlyList<RestResponse<ScryfallCollectionResponse>> responses,
        string suffix = "resolver test")
    {
        var stubResponses = new Queue<RestResponse<ScryfallCollectionResponse>>(responses);
        return await ScryfallCollectionResolver.ResolveCardsAsync(
            entries,
            (_, _) => Task.FromResult(stubResponses.Dequeue()),
            suffix,
            CancellationToken.None);
    }

    private static List<DeckEntry> MakeEntries(int count) =>
        Enumerable.Range(1, count).Select(index => MakeEntry($"Card {index}", "dfc", index.ToString())).ToList();

    private static DeckEntry MakeEntry(string name, string? setCode, string? collectorNumber) =>
        new()
        {
            Name = name,
            NormalizedName = CardNormalizer.Normalize(name),
            Quantity = 1,
            Board = "mainboard",
            SetCode = setCode,
            CollectorNumber = collectorNumber
        };

    private static ScryfallCard MakeCard(string name, string setCode, string collectorNumber) =>
        new(name, null, string.Empty, null, null, null, null, null, setCode, null, collectorNumber);

    private static RestResponse<ScryfallCollectionResponse> MakeCollectionResponse(
        HttpStatusCode statusCode,
        List<ScryfallCard>? cards) =>
        new(new RestRequest("cards/collection"))
        {
            StatusCode = statusCode,
            Data = cards is null ? null : new ScryfallCollectionResponse(cards, null)
        };
}
