using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for the Cut Lab proposal-delta cache.</summary>
public sealed class CutLabDeltaCacheTests
{
    /// <summary>Stored proposal deltas round-trip by pool key and card name.</summary>
    [Fact]
    public void TryGet_AfterSet_ReturnsStoredDeltas()
    {
        var cache = new CutLabDeltaCache();
        string poolKey = CutLabResolvedCardCache.ComputePoolKey([("Mana Crypt", 1), ("Island", 3)]);
        var deltas = new CutLabProposalDeltas
        {
            CardName = "Mana Crypt",
            ChangedFamilyCount = 2,
            Deltas =
            [
                new CutLabMetricDelta
                {
                    Kind = CutLabMetricKind.CommanderOnTime,
                    Family = CutLabMetricFamily.CommanderOnTime,
                    Label = "Commander on time",
                    Before = 81.0,
                    After = 77.0,
                    Delta = -4.0,
                    Direction = CutLabMetricDirection.Down,
                    IsMeaningful = true,
                },
            ],
        };

        cache.Set(poolKey, "Mana Crypt", deltas);

        bool found = cache.TryGet(poolKey, "Mana Crypt", out var cachedDeltas);

        Assert.True(found);
        Assert.Same(deltas, cachedDeltas);
    }

    /// <summary>Different card names do not collide inside the same pool key.</summary>
    [Fact]
    public void TryGet_DifferentCardName_Misses()
    {
        var cache = new CutLabDeltaCache();
        string poolKey = CutLabResolvedCardCache.ComputePoolKey([("Mana Crypt", 1), ("Island", 3)]);

        cache.Set(
            poolKey,
            "Mana Crypt",
            new CutLabProposalDeltas
            {
                CardName = "Mana Crypt",
            });

        bool found = cache.TryGet(poolKey, "Sol Ring", out var deltas);

        Assert.False(found);
        Assert.Null(deltas);
    }
}
