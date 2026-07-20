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

    /// <summary>Entries larger than the configured cache budget are not retained.</summary>
    [Fact]
    public void Set_EntryLargerThanSizeLimit_DoesNotRetainCards()
    {
        var cache = new CutLabResolvedCardCache(sizeLimitBytes: 10);
        string key = CutLabResolvedCardCache.ComputePoolKey([("Ancient Tomb", 1)]);
        IReadOnlyList<ScryfallCardData> cards =
        [
            new ScryfallCardData
            {
                Name = "Ancient Tomb",
                TypeLine = "Land",
                OracleText = "{T}: Add {C}{C}. Ancient Tomb deals 2 damage to you.",
            },
        ];

        cache.Set(key, cards);

        bool found = cache.TryGet(key, out var cachedCards);

        Assert.False(found);
        Assert.Null(cachedCards);
    }

    /// <summary>Storing beyond the configured budget causes the overflow write to miss.</summary>
    [Fact]
    public void Set_PastSizeLimit_DoesNotRetainOverflowEntry()
    {
        var cache = new CutLabResolvedCardCache(sizeLimitBytes: 70);
        string firstKey = CutLabResolvedCardCache.ComputePoolKey([("Island", 1)]);
        string secondKey = CutLabResolvedCardCache.ComputePoolKey([("Mountain", 1)]);
        IReadOnlyList<ScryfallCardData> firstCards =
        [
            new ScryfallCardData
            {
                Name = "A",
                TypeLine = "B",
                OracleText = new string('x', 20),
            },
        ];
        IReadOnlyList<ScryfallCardData> secondCards =
        [
            new ScryfallCardData
            {
                Name = "C",
                TypeLine = "D",
                OracleText = new string('y', 60),
            },
        ];

        cache.Set(firstKey, firstCards);
        cache.Set(secondKey, secondCards);

        bool secondFound = cache.TryGet(secondKey, out var cachedSecondCards);
        bool firstFound = cache.TryGet(firstKey, out var cachedFirstCards);

        Assert.False(secondFound);
        Assert.Null(cachedSecondCards);
        Assert.True(firstFound);
        Assert.Same(firstCards, cachedFirstCards);
    }

    /// <summary>Derived working-list keys can be seeded from a fully resolved superset without live resolver calls.</summary>
    [Fact]
    public void TrySeedFromSuperset_WhenSupersetContainsAllCards_CachesDerivedPool()
    {
        var cache = new CutLabResolvedCardCache();
        IReadOnlyList<(string Name, int Quantity)> fullPool =
        [
            ("Commander", 1),
            ("Arcane Signet", 1),
            ("Counterspell", 99),
        ];
        IReadOnlyList<(string Name, int Quantity)> afterPool =
        [
            ("Commander", 1),
            ("Counterspell", 99),
        ];
        IReadOnlyList<ScryfallCardData> resolvedCards =
        [
            new ScryfallCardData
            {
                Name = "Commander",
                TypeLine = "Legendary Creature",
            },
            new ScryfallCardData
            {
                Name = "Arcane Signet",
                TypeLine = "Artifact",
            },
            new ScryfallCardData
            {
                Name = "Counterspell",
                TypeLine = "Instant",
            },
        ];

        cache.Set(CutLabResolvedCardCache.ComputePoolKey(fullPool), resolvedCards);

        bool seeded = cache.TrySeedFromSuperset(afterPool, resolvedCards, out IReadOnlyList<ScryfallCardData>? seededCards);
        bool found = cache.TryGet(CutLabResolvedCardCache.ComputePoolKey(afterPool), out IReadOnlyList<ScryfallCardData>? cachedCards);

        Assert.True(seeded);
        Assert.NotNull(seededCards);
        Assert.True(found);
        Assert.NotNull(cachedCards);
        Assert.Equal(["Commander", "Counterspell"], cachedCards.Select(card => card.Name));
    }

    /// <summary>Derived working-list keys keep the resolvable subset when the superset is missing a card.</summary>
    [Fact]
    public void TrySeedFromSuperset_WhenSupersetIsMissingCard_ReturnsFalseAndDoesNotWrite()
    {
        var cache = new CutLabResolvedCardCache();
        IReadOnlyList<(string Name, int Quantity)> afterPool =
        [
            ("Commander", 1),
            ("Typo Card", 1),
            ("Counterspell", 99),
        ];
        IReadOnlyList<ScryfallCardData> resolvedCards =
        [
            new ScryfallCardData
            {
                Name = "Commander",
                TypeLine = "Legendary Creature",
            },
            new ScryfallCardData
            {
                Name = "Counterspell",
                TypeLine = "Instant",
            },
        ];

        bool seeded = cache.TrySeedFromSuperset(afterPool, resolvedCards, out IReadOnlyList<ScryfallCardData>? seededCards);
        bool found = cache.TryGet(CutLabResolvedCardCache.ComputePoolKey(afterPool), out IReadOnlyList<ScryfallCardData>? cachedCards);

        Assert.False(seeded);
        Assert.NotNull(seededCards);
        Assert.False(found);
        Assert.Null(cachedCards);
        Assert.Equal(["Commander", "Counterspell"], seededCards.Select(card => card.Name));
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
