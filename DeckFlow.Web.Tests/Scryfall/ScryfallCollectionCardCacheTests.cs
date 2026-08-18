using DeckFlow.Web.Services.Scryfall;
using DeckFlow.Web.Services;
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
}
