using System.Net;
using System.Net.Http;
using DeckFlow.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="CommanderBanListService"/> covering HTML parsing, caching, and ban-list lookup.
/// </summary>
public sealed class CommanderBanListServiceTests
{
    [Fact]
    public void ParseBannedCards_ReturnsSummaryNames()
    {
        const string html = """
<html>
<body>
<details><summary>Ancestral Recall</summary><p>text</p></details>
<details><summary>Dockside Extortionist</summary><p>text</p></details>
<details><summary>Mana Crypt</summary><p>text</p></details>
</body>
</html>
""";

        var cards = CommanderBanListService.ParseBannedCards(html);

        Assert.Collection(
            cards,
            card => Assert.Equal("Ancestral Recall", card),
            card => Assert.Equal("Dockside Extortionist", card),
            card => Assert.Equal("Mana Crypt", card));
    }

    [Fact]
    public async Task GetBannedCardsAsync_CachesResults()
    {
        var fetchCount = 0;
        var service = TestServiceFactory.CreateCommanderBanListService(
            new MemoryCache(new MemoryCacheOptions()),
            _ =>
            {
                fetchCount++;
                return Task.FromResult("<details><summary>Mana Crypt</summary></details>");
            });

        var first = await service.GetBannedCardsAsync();
        var second = await service.GetBannedCardsAsync();

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public async Task GetBannedCardsAsync_FailedFetch_IsNegativeCached()
    {
        var fetchCount = 0;
        var service = TestServiceFactory.CreateCommanderBanListService(
            new MemoryCache(new MemoryCacheOptions()),
            _ =>
            {
                fetchCount++;
                throw new HttpRequestException("boom", null, HttpStatusCode.ServiceUnavailable);
            });

        var first = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetBannedCardsAsync());
        var second = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetBannedCardsAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(first.Message, second.Message);
        Assert.Equal(1, fetchCount);
    }

    [Fact]
    public async Task GetBannedCardsAsync_Cancellation_IsNotNegativeCached()
    {
        var fetchCount = 0;
        var service = TestServiceFactory.CreateCommanderBanListService(
            new MemoryCache(new MemoryCacheOptions()),
            _ =>
            {
                fetchCount++;
                if (fetchCount == 1)
                {
                    throw new OperationCanceledException();
                }

                return Task.FromResult("<details><summary>Mana Crypt</summary></details>");
            });

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetBannedCardsAsync());
        var cards = await service.GetBannedCardsAsync();

        Assert.Equal("Mana Crypt", Assert.Single(cards));
        Assert.Equal(2, fetchCount);
    }

    [Fact]
    public async Task GetBannedCardsAsync_EmptyParse_IsNotCachedAsSuccess()
    {
        var fetchCount = 0;
        var service = TestServiceFactory.CreateCommanderBanListService(
            new MemoryCache(new MemoryCacheOptions()),
            _ =>
            {
                fetchCount++;
                return Task.FromResult("<html></html>");
            });

        var first = await service.GetBannedCardsAsync();
        var second = await service.GetBannedCardsAsync();

        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal(2, fetchCount);
    }

    [Fact]
    public async Task GetBannedCardsAsync_CallerCancelledDuringFetch_IsNotNegativeCached()
    {
        using var cts = new CancellationTokenSource();
        var fetchCount = 0;
        var service = TestServiceFactory.CreateCommanderBanListService(
            new MemoryCache(new MemoryCacheOptions()),
            _ =>
            {
                fetchCount++;
                if (fetchCount == 1)
                {
                    cts.Cancel();
                    throw new HttpRequestException("aborted", null, null);
                }

                return Task.FromResult("<details><summary>Mana Crypt</summary></details>");
            });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetBannedCardsAsync(cts.Token));
        Assert.Equal("aborted", exception.Message);
        var cards = await service.GetBannedCardsAsync(CancellationToken.None);

        Assert.Equal(2, fetchCount);
        Assert.Equal("Mana Crypt", Assert.Single(cards));
    }
}
