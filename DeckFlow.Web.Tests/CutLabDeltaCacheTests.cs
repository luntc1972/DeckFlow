using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for the Cut Lab proposal-delta cache.</summary>
public sealed class CutLabDeltaCacheTests
{
    private const string AnyPlayExperience = "cEDH";

    [Fact]
    public void Snapshot_RoleOverride_AbsentThenPresent_Misses()
    {
        var cache = new CutLabDeltaCache();
        CutLabRoleOverride[] overrides =
        [
            new() { Name = "Card", Added = ["draw"], Removed = ["ramp"] },
            new() { Name = "Other", Added = ["payoffs"] },
        ];
        var value = new CutLabMetricSnapshot();
        cache.SetSnapshot("pool", "Focused", 10, value);

        Assert.False(cache.TryGetSnapshot("pool", "Focused", 10, out var actual, roleOverrides: overrides));
    }

    [Fact]
    public void Snapshot_RoleOverride_PermutedOrder_Hits()
    {
        var cache = new CutLabDeltaCache();
        CutLabRoleOverride[] overrides =
        [
            new() { Name = "Card", Added = ["draw"], Removed = ["ramp"] },
            new() { Name = "Other", Added = ["payoffs"] },
        ];
        var value = new CutLabMetricSnapshot();
        cache.SetSnapshot("pool", "Focused", 10, value, roleOverrides: overrides);

        Assert.True(cache.TryGetSnapshot("pool", "Focused", 10, out var actual, roleOverrides: overrides.Reverse().ToArray()));
        Assert.Same(value, actual);
    }

    [Fact]
    public void Result_RoleOverride_AbsentThenPresent_Misses()
    {
        var cache = new CutLabDeltaCache();
        CutLabRoleOverride[] overrides =
        [
            new() { Name = "Card", Added = ["draw"], Removed = ["ramp"] },
            new() { Name = "Other", Added = ["payoffs"] },
        ];
        var value = new CutLabSimulationResult();
        cache.SetResult("pool", "Focused", 10, value);

        Assert.False(cache.TryGetResult("pool", "Focused", 10, out var actual, roleOverrides: overrides));
    }

    [Fact]
    public void Result_RoleOverride_PermutedOrder_Hits()
    {
        var cache = new CutLabDeltaCache();
        CutLabRoleOverride[] overrides =
        [
            new() { Name = "Card", Added = ["draw"], Removed = ["ramp"] },
            new() { Name = "Other", Added = ["payoffs"] },
        ];
        var value = new CutLabSimulationResult();
        cache.SetResult("pool", "Focused", 10, value, roleOverrides: overrides);

        Assert.True(cache.TryGetResult("pool", "Focused", 10, out var actual, roleOverrides: overrides.Reverse().ToArray()));
        Assert.Same(value, actual);
    }

    [Fact]
    public void Delta_RoleOverride_AbsentThenPresent_Misses()
    {
        var cache = new CutLabDeltaCache();
        CutLabRoleOverride[] overrides =
        [
            new() { Name = "Card", Added = ["draw"], Removed = ["ramp"] },
            new() { Name = "Other", Added = ["payoffs"] },
        ];
        var value = new CutLabProposalDeltas { CardName = "Card" };
        cache.Set("pool", "Card", AnyPlayExperience, value);

        Assert.False(cache.TryGet("pool", "Card", AnyPlayExperience, out var actual, roleOverrides: overrides));
    }

    [Fact]
    public void Delta_RoleOverride_PermutedOrder_Hits()
    {
        var cache = new CutLabDeltaCache();
        CutLabRoleOverride[] overrides =
        [
            new() { Name = "Card", Added = ["draw"], Removed = ["ramp"] },
            new() { Name = "Other", Added = ["payoffs"] },
        ];
        var value = new CutLabProposalDeltas { CardName = "Card" };
        cache.Set("pool", "Card", AnyPlayExperience, value, roleOverrides: overrides);

        Assert.True(cache.TryGet("pool", "Card", AnyPlayExperience, out var actual, roleOverrides: overrides.Reverse().ToArray()));
        Assert.Same(value, actual);
    }

    /// <summary>Different play experiences must not share proposal deltas.</summary>
    [Fact]
    public void TryGet_DifferentPlayExperience_Misses()
    {
        var cache = new CutLabDeltaCache();
        var deltas = new CutLabProposalDeltas { CardName = "Mana Crypt" };
        cache.Set("pool", "Mana Crypt", "Casual", deltas);

        bool found = cache.TryGet("pool", "Mana Crypt", "cEDH", out var cachedDeltas);

        Assert.False(found);
        Assert.Null(cachedDeltas);
    }

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
                    Unit = CutLabMetricUnit.Percent,
                    Direction = CutLabMetricDirection.Down,
                    IsMeaningful = true,
                },
            ],
        };

        cache.Set(poolKey, "Mana Crypt", AnyPlayExperience, deltas);

        bool found = cache.TryGet(poolKey, "Mana Crypt", AnyPlayExperience, out var cachedDeltas);

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
            AnyPlayExperience,
            new CutLabProposalDeltas
            {
                CardName = "Mana Crypt",
            });

        bool found = cache.TryGet(poolKey, "Sol Ring", AnyPlayExperience, out var deltas);

        Assert.False(found);
        Assert.Null(deltas);
    }

    /// <summary>Card-name cache keys are case-insensitive to avoid duplicate proposal sims.</summary>
    [Fact]
    public void TryGet_SameCardNameDifferentCase_HitsStoredDeltas()
    {
        var cache = new CutLabDeltaCache();
        string poolKey = CutLabResolvedCardCache.ComputePoolKey([("Mana Crypt", 1), ("Island", 3)]);
        var deltas = CreateProposalDeltas("Mana Crypt", "short");

        cache.Set(poolKey, "Mana Crypt", AnyPlayExperience, deltas);

        bool found = cache.TryGet(poolKey, "mana crypt", AnyPlayExperience, out var cachedDeltas);

        Assert.True(found);
        Assert.Same(deltas, cachedDeltas);
    }

    /// <summary>Entries larger than the configured cache budget are not retained.</summary>
    [Fact]
    public void Set_EntryLargerThanSizeLimit_DoesNotRetainDeltas()
    {
        var cache = new CutLabDeltaCache(sizeLimitBytes: 20);
        string poolKey = CutLabResolvedCardCache.ComputePoolKey([("Mana Crypt", 1)]);
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
                    Unit = CutLabMetricUnit.Percent,
                    Direction = CutLabMetricDirection.Down,
                    IsMeaningful = true,
                },
            ],
        };

        cache.Set(poolKey, "Mana Crypt", AnyPlayExperience, deltas);

        bool found = cache.TryGet(poolKey, "Mana Crypt", AnyPlayExperience, out var cachedDeltas);

        Assert.False(found);
        Assert.Null(cachedDeltas);
    }

    /// <summary>Storing beyond the configured budget causes the overflow write to miss.</summary>
    [Fact]
    public void Set_PastSizeLimit_DoesNotRetainOverflowEntry()
    {
        var cache = new CutLabDeltaCache(sizeLimitBytes: 80);
        string poolKey = CutLabResolvedCardCache.ComputePoolKey([("Mana Crypt", 1), ("Island", 3)]);
        var firstDeltas = CreateProposalDeltas("A", "short");
        var secondDeltas = CreateProposalDeltas("B", new string('x', 25));

        cache.Set(poolKey, "Mana Crypt", AnyPlayExperience, firstDeltas);
        cache.Set(poolKey, "Sol Ring", AnyPlayExperience, secondDeltas);

        bool secondFound = cache.TryGet(poolKey, "Sol Ring", AnyPlayExperience, out var cachedSecondDeltas);
        bool firstFound = cache.TryGet(poolKey, "Mana Crypt", AnyPlayExperience, out var cachedFirstDeltas);

        Assert.False(secondFound);
        Assert.Null(cachedSecondDeltas);
        Assert.True(firstFound);
        Assert.Same(firstDeltas, cachedFirstDeltas);
    }

    /// <summary>Snapshot cache entries do not collide across distinct trial overrides.</summary>
    [Fact]
    public void TryGetSnapshot_DifferentTrialsOverride_Misses()
    {
        var cache = new CutLabDeltaCache();
        string poolKey = CutLabResolvedCardCache.ComputePoolKey([("Mana Crypt", 1), ("Island", 3)]);
        var snapshot = new CutLabMetricSnapshot
        {
            Metrics =
            [
                new CutLabMetricValue
                {
                    Kind = CutLabMetricKind.KeepableHand,
                    Family = CutLabMetricFamily.KeepableHand,
                    Label = "Keepable hand",
                    Value = 77,
                    Unit = CutLabMetricUnit.Percent,
                },
            ],
        };

        cache.SetSnapshot(poolKey, AnyPlayExperience, 4000, snapshot);

        bool found = cache.TryGetSnapshot(poolKey, AnyPlayExperience, null, out var cachedSnapshot);

        Assert.False(found);
        Assert.Null(cachedSnapshot);
    }

    private static CutLabProposalDeltas CreateProposalDeltas(string cardName, string label) =>
        new()
        {
            CardName = cardName,
            ChangedFamilyCount = 2,
            Deltas =
            [
                new CutLabMetricDelta
                {
                    Kind = CutLabMetricKind.CommanderOnTime,
                    Family = CutLabMetricFamily.CommanderOnTime,
                    Label = label,
                    Before = 81.0,
                    After = 77.0,
                    Delta = -4.0,
                    Unit = CutLabMetricUnit.Percent,
                    Direction = CutLabMetricDirection.Down,
                    IsMeaningful = true,
                },
            ],
        };
}
