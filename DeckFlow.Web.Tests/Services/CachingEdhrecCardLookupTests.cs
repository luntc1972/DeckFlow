using DeckFlow.Core.Integration;
using DeckFlow.Web.Services.Edhrec;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CachingEdhrecCardLookupTests
{
    [Fact]
    public async Task LookupCategoriesAsync_WarmCache_UsesOneUpstreamRequest()
    {
        var inner = new FakeEdhrecCardLookup(["ramp"]);
        var sut = new CachingEdhrecCardLookup(inner);

        var first = await sut.LookupCategoriesAsync("Birds of Paradise");
        var second = await sut.LookupCategoriesAsync("Birds of Paradise");

        Assert.Equal(["ramp"], first);
        Assert.Equal(first, second);
        Assert.Equal(1, inner.LookupCalls);
    }

    [Fact]
    public async Task LookupCategoriesAsync_EmptyResult_IsRetried()
    {
        var inner = new FakeEdhrecCardLookup([], []);
        var sut = new CachingEdhrecCardLookup(inner);

        await sut.LookupCategoriesAsync("Birds of Paradise");
        await sut.LookupCategoriesAsync("Birds of Paradise");

        Assert.Equal(2, inner.LookupCalls);
    }

    [Fact]
    public async Task LookupCategoriesAsync_SlugEquivalentNames_ReuseEntry()
    {
        var inner = new FakeEdhrecCardLookup(["ramp"]);
        var sut = new CachingEdhrecCardLookup(inner);

        await sut.LookupCategoriesAsync("Urza's Saga");
        await sut.LookupCategoriesAsync("URZA’S SAGA");

        Assert.Equal(1, inner.LookupCalls);
    }

    [Fact]
    public async Task LookupCategoriesAsync_EntryLargerThanCacheLimit_IsNotRetained()
    {
        var oversizedCategory = new string('x', checked((int)CachingEdhrecCardLookup.CacheCapacityChars + 1));
        var inner = new FakeEdhrecCardLookup([oversizedCategory], [oversizedCategory]);
        var sut = new CachingEdhrecCardLookup(inner);
        const string cardName = "Birds of Paradise";

        await sut.LookupCategoriesAsync(cardName);
        await sut.LookupCategoriesAsync(cardName);

        Assert.Equal(2, inner.LookupCalls);
    }

    private sealed class FakeEdhrecCardLookup : IEdhrecCardLookup
    {
        private readonly Queue<IReadOnlyList<string>> _responses;

        public FakeEdhrecCardLookup(params IReadOnlyList<string>[] responses)
        {
            _responses = new Queue<IReadOnlyList<string>>(responses);
        }

        public int LookupCalls { get; private set; }

        public Task<IReadOnlyList<string>> LookupCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
        {
            LookupCalls++;
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : Array.Empty<string>() as IReadOnlyList<string>);
        }
    }
}
