using System.Reflection;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Services.CutLab;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for the Cut Lab resolved-card cache.</summary>
public sealed class CutLabResolvedCardCacheTests
{
    /// <summary>Pool keys stay stable regardless of input ordering.</summary>
    [Fact]
    public void ComputePoolKey_SameCardsDifferentOrder_ReturnsSameKey()
    {
        string first = CutLabResolvedCardCache.ComputePoolKey(
        [
            ("Mana Crypt", 1),
            ("Island", 3),
            ("Forest", 2),
        ]);

        string second = CutLabResolvedCardCache.ComputePoolKey(
        [
            ("Forest", 2),
            ("Mana Crypt", 1),
            ("Island", 3),
        ]);

        Assert.Equal(first, second);
    }

    /// <summary>Stored resolved-card payloads round-trip by pool key.</summary>
    [Fact]
    public void TryGet_AfterSet_ReturnsStoredCards()
    {
        var cache = new CutLabResolvedCardCache();
        string key = CutLabResolvedCardCache.ComputePoolKey([("Mana Crypt", 1), ("Island", 3)]);
        IReadOnlyList<ScryfallCardData> cards =
        [
            new ScryfallCardData
            {
                Name = "Mana Crypt",
                Cmc = 0,
                TypeLine = "Artifact",
                OracleText = "{T}: Add {C}{C}.",
            },
            new ScryfallCardData
            {
                Name = "Island",
                Cmc = 0,
                TypeLine = "Basic Land — Island",
                OracleText = "({T}: Add {U}.)",
                ProducedMana = ["U"],
            },
        ];

        cache.Set(key, cards);

        bool found = cache.TryGet(key, out var cachedCards);

        Assert.True(found);
        Assert.Same(cards, cachedCards);
    }

    /// <summary>Missing pool keys do not produce a payload.</summary>
    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var cache = new CutLabResolvedCardCache();

        bool found = cache.TryGet("missing", out var cards);

        Assert.False(found);
        Assert.Null(cards);
    }

    /// <summary>Resolved-card and delta caches use separate backing MemoryCache instances.</summary>
    [Fact]
    public void Constructor_UsesDedicatedMemoryCacheInstance()
    {
        var resolvedCache = new CutLabResolvedCardCache();
        var deltaCache = new CutLabDeltaCache();

        var resolvedMemoryCache = Assert.IsType<MemoryCache>(GetPrivateCache(resolvedCache));
        var deltaMemoryCache = Assert.IsType<MemoryCache>(GetPrivateCache(deltaCache));

        Assert.NotSame(resolvedMemoryCache, deltaMemoryCache);
    }

    private static object GetPrivateCache(object instance)
    {
        FieldInfo field = instance.GetType().GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected private _cache field.");
        return field.GetValue(instance) ?? throw new InvalidOperationException("Expected non-null cache.");
    }
}
