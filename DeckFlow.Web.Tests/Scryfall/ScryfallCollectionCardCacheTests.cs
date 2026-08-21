using DeckFlow.Web.Services.Scryfall;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using System;
using System.Collections.Generic;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class ScryfallCollectionCardCacheTests
{
    [Fact]
    public void NameAndPrintingNamespaces_CannotCollide()
    {
        var cache = new ScryfallCollectionCardCache();
        var card = Card("Sol Ring");

        cache.SetNamePositive("m21:m21", card);
        cache.SetPrintingPositive("m21", "m21", Card("Different Card"));

        Assert.True(cache.TryGetName("m21:m21", out var nameCard));
        Assert.True(cache.TryGetPrinting("m21", "m21", out var printingCard));
        Assert.Equal("Sol Ring", nameCard!.Name);
        Assert.Equal("Different Card", printingCard!.Name);
    }

    [Fact]
    public void OversizedEntryIsNotRetained_WhileNormalEntryIsRetained()
    {
        var cache = new ScryfallCollectionCardCache(20);

        cache.SetNamePositive("oversized", Card("Oversized", new string('x', 100)));
        cache.SetNamePositive("produced-mana", Card("Produced", producedMana: [new string('m', 100)]));
        cache.SetNamePositive("faces", Card("Faces") with
        {
            CardFaces = [new ScryfallCardFace(new string('f', 100), null, null, null, null, null)],
        });
        cache.SetNamePositive("normal", Card("Normal"));

        Assert.False(cache.TryGetName("oversized", out _));
        Assert.False(cache.TryGetName("produced-mana", out _));
        Assert.False(cache.TryGetName("faces", out _));
        Assert.True(cache.TryGetName("normal", out var normal));
        Assert.Equal("Normal", normal!.Name);
    }

    [Fact]
    public void PositiveEntryOutlivesCollectionMiss()
    {
        var timeProvider = new TestTimeProvider();
        var cache = new ScryfallCollectionCardCache(100, timeProvider);

        cache.SetNamePositive("positive", Card("Positive"));
        cache.SetNameCollectionMiss("miss");
        timeProvider.Advance(TimeSpan.FromMinutes(90));

        Assert.True(cache.TryGetName("positive", out var positive));
        Assert.Equal("Positive", positive!.Name);
        Assert.False(cache.TryGetName("miss", out _));
    }

    [Fact]
    public void CollectionMissesAreChargedByTheirRetainedKeyLength()
    {
        var cache = new ScryfallCollectionCardCache(20);

        cache.SetNameCollectionMiss("one");

        Assert.True(cache.TryGetName("one", out var retainedMiss));
        Assert.Null(retainedMiss);

        cache.SetNameCollectionMiss("first-long-key");
        cache.SetNameCollectionMiss("second-long-key");

        Assert.False(cache.TryGetName("first-long-key", out _));
        Assert.False(cache.TryGetName("second-long-key", out _));
    }

    [Fact]
    public void FeatureFlagOff_NamePositiveIsNotCached()
    {
        var flags = new FakeFeatureFlagCache(false);
        var cache = new ScryfallCollectionCardCache(flags);

        cache.SetNamePositive("sol-ring", Card("Sol Ring"));
        // Why: see FeatureFlagOff_NameCollectionMissIsNotCached - the read gate masks the write gate.
        flags.Enabled = true;

        Assert.False(cache.TryGetName("sol-ring", out var card));
        Assert.Null(card);
    }

    [Fact]
    public void FeatureFlagOff_PrintingPositiveIsNotCached()
    {
        var flags = new FakeFeatureFlagCache(false);
        var cache = new ScryfallCollectionCardCache(flags);

        cache.SetPrintingPositive("CMM", "396", Card("Sol Ring"));
        // Why: see FeatureFlagOff_NameCollectionMissIsNotCached - the read gate masks the write gate.
        flags.Enabled = true;

        Assert.False(cache.TryGetPrinting("CMM", "396", out var card));
        Assert.Null(card);
    }

    [Fact]
    public void FeatureFlagOn_NamePositiveIsCached()
    {
        var cache = new ScryfallCollectionCardCache(new FakeFeatureFlagCache(true));

        cache.SetNamePositive("sol-ring", Card("Sol Ring"));

        Assert.True(cache.TryGetName("sol-ring", out var card));
        Assert.Equal("Sol Ring", card!.Name);
    }

    [Fact]
    public void FeatureFlagFlipsOffAfterSet_NameResultIsUnavailable()
    {
        var flags = new FakeFeatureFlagCache(true);
        var cache = new ScryfallCollectionCardCache(flags);
        cache.SetNamePositive("sol-ring", Card("Sol Ring"));
        flags.Enabled = false;

        Assert.False(cache.TryGetName("sol-ring", out var card));
        Assert.Null(card);
    }

    [Fact]
    public void FeatureFlagFlipsOffAfterSet_PrintingResultIsUnavailable()
    {
        var flags = new FakeFeatureFlagCache(true);
        var cache = new ScryfallCollectionCardCache(flags);
        cache.SetPrintingPositive("CMM", "396", Card("Sol Ring"));
        flags.Enabled = false;

        Assert.False(cache.TryGetPrinting("CMM", "396", out var card));
        Assert.Null(card);
    }

    [Fact]
    public void FeatureFlagFlipsOnAfterOffWrite_OnlyFreshValueIsCached()
    {
        var flags = new FakeFeatureFlagCache(false);
        var cache = new ScryfallCollectionCardCache(flags);
        cache.SetNamePositive("sol-ring", Card("Sol Ring"));
        flags.Enabled = true;

        Assert.False(cache.TryGetName("sol-ring", out _));
        cache.SetNamePositive("sol-ring", Card("Sol Ring"));

        Assert.True(cache.TryGetName("sol-ring", out var card));
        Assert.Equal("Sol Ring", card!.Name);
    }

    [Fact]
    public void FeatureFlagOff_NameCollectionMissIsNotCached()
    {
        var flags = new FakeFeatureFlagCache(false);
        var cache = new ScryfallCollectionCardCache(flags);

        cache.SetNameCollectionMiss("not-a-card");
        // Why: reading while the flag is off would pass whether or not the write was gated, because
        // the read gate returns false either way. Flip the flag on so the write path is observed.
        flags.Enabled = true;

        Assert.False(cache.TryGetName("not-a-card", out var card));
        Assert.Null(card);
    }

    [Fact]
    public void FeatureFlagOn_NameCollectionMissIsCached()
    {
        var cache = new ScryfallCollectionCardCache(new FakeFeatureFlagCache(true));

        cache.SetNameCollectionMiss("not-a-card");

        Assert.True(cache.TryGetName("not-a-card", out var card));
        Assert.Null(card);
    }

    [Fact]
    public void CacheReadsTheSeededFlagKey()
    {
        var flags = new FakeFeatureFlagCache(true);
        var cache = new ScryfallCollectionCardCache(flags);

        cache.TryGetName("sol-ring", out _);

        Assert.Contains(FakeFeatureFlagCache.CacheFlagKey, flags.RequestedKeys);
    }

    [Fact]
    public void NullFeatureFlags_DefaultConstructorStillCaches()
    {
        var cache = new ScryfallCollectionCardCache();
        cache.SetNamePositive("sol-ring", Card("Sol Ring"));

        Assert.True(cache.TryGetName("sol-ring", out var card));
        Assert.Equal("Sol Ring", card!.Name);
    }

    private static ScryfallCard Card(
        string name,
        string? oracleText = null,
        IReadOnlyList<string>? producedMana = null) => new(
        name,
        null,
        "Artifact",
        oracleText,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        ProducedMana: producedMana);

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan elapsed) => _utcNow += elapsed;
    }

    private sealed class FakeFeatureFlagCache(bool enabled) : IFeatureFlagCache
    {
        // Why: the cache's flag key is a private constant, so a fake that answered every key would
        // let a typo in it pass every test while shipping an un-gated cache to production.
        internal const string CacheFlagKey = "service.scryfall-collection-cache.enabled";

        public bool Enabled { get; set; } = enabled;

        public List<string> RequestedKeys { get; } = [];

        // Why: mirrors FeatureFlagCache's D-13 default-on contract - an unknown key reads as enabled.
        public bool IsEnabled(string key)
        {
            RequestedKeys.Add(key);
            return string.Equals(key, CacheFlagKey, StringComparison.Ordinal) ? Enabled : true;
        }

        public IReadOnlyDictionary<string, bool> Snapshot() => new Dictionary<string, bool>
        {
            [CacheFlagKey] = Enabled,
        };

        public Task ReloadAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
