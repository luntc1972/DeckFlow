using DeckFlow.Core.Analysis;

namespace DeckFlow.Core.Tests;

public sealed class DeckPlanStrategyCatalogTests
{
    [Fact]
    public void Entries_HasExactlyTwelveInDeclarationOrder()
    {
        DeckPlanStrategy[] expectedOrder =
        [
            DeckPlanStrategy.Combo,
            DeckPlanStrategy.Aristocrats,
            DeckPlanStrategy.Voltron,
            DeckPlanStrategy.Tokens,
            DeckPlanStrategy.Spellslinger,
            DeckPlanStrategy.Stax,
            DeckPlanStrategy.Reanimator,
            DeckPlanStrategy.Landfall,
            DeckPlanStrategy.Lifegain,
            DeckPlanStrategy.PlusOneCounters,
            DeckPlanStrategy.Combat,
            DeckPlanStrategy.Control,
        ];

        Assert.Equal(12, DeckPlanStrategyCatalog.Entries.Count);
        Assert.Equal(expectedOrder, DeckPlanStrategyCatalog.Entries.Select(entry => entry.Strategy));
    }

    [Fact]
    public void Entries_EveryEnumMemberAppearsExactlyOnce()
    {
        var strategies = DeckPlanStrategyCatalog.Entries.Select(entry => entry.Strategy).ToList();

        foreach (DeckPlanStrategy strategy in Enum.GetValues<DeckPlanStrategy>())
        {
            Assert.Single(strategies, s => s == strategy);
        }
    }

    [Fact]
    public void Entries_EveryEntryHasNonEmptyCopyAndAtLeastOneNeedle()
    {
        foreach (var entry in DeckPlanStrategyCatalog.Entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Slug));
            Assert.False(string.IsNullOrWhiteSpace(entry.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(entry.Definition));
            Assert.False(string.IsNullOrWhiteSpace(entry.Consequence));
            Assert.NotEmpty(entry.CategoryNeedles);
        }
    }

    [Fact]
    public void Entries_SlugsAreUniqueUnderOrdinalIgnoreCase()
    {
        var distinctSlugs = DeckPlanStrategyCatalog.Entries
            .Select(entry => entry.Slug)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(DeckPlanStrategyCatalog.Entries.Count, distinctSlugs.Count());
    }

    [Theory]
    [InlineData("combo")]
    [InlineData("COMBO")]
    [InlineData("Combo")]
    public void TryGetBySlug_KnownSlug_ResolvesCaseInsensitively(string slug)
    {
        bool resolved = DeckPlanStrategyCatalog.TryGetBySlug(slug, out var entry);

        Assert.True(resolved);
        Assert.Equal(DeckPlanStrategy.Combo, entry.Strategy);
    }

    [Fact]
    public void TryGetBySlug_UnknownSlug_ReturnsFalseWithoutThrowing()
    {
        bool resolved = DeckPlanStrategyCatalog.TryGetBySlug("not-a-real-strategy", out var entry);

        Assert.False(resolved);
        Assert.Null(entry);
    }

    [Fact]
    public void MatchesCategories_SacrificeOutletTag_MatchesAristocrats()
    {
        DeckPlanStrategyCatalog.TryGetBySlug("aristocrats", out var aristocrats);

        bool matches = DeckPlanStrategyCatalog.MatchesCategories(aristocrats, ["Sacrifice Outlet"]);

        Assert.True(matches);
    }

    [Fact]
    public void MatchesCategories_ManaRampTag_DoesNotMatchAristocrats()
    {
        DeckPlanStrategyCatalog.TryGetBySlug("aristocrats", out var aristocrats);

        bool matches = DeckPlanStrategyCatalog.MatchesCategories(aristocrats, ["Mana Ramp"]);

        Assert.False(matches);
    }

    [Fact]
    public void MatchesCategories_CounterspellTag_DoesNotMatchPlusOneCounters()
    {
        DeckPlanStrategyCatalog.TryGetBySlug("counters", out var counters);

        bool matches = DeckPlanStrategyCatalog.MatchesCategories(counters, ["Counterspell"]);

        Assert.False(matches);
    }

    [Theory]
    [InlineData("Counters")]
    [InlineData("+1/+1 Counters")]
    public void MatchesCategories_CountersTag_MatchesPlusOneCounters(string category)
    {
        DeckPlanStrategyCatalog.TryGetBySlug("counters", out var counters);

        bool matches = DeckPlanStrategyCatalog.MatchesCategories(counters, [category]);

        Assert.True(matches);
    }

    [Fact]
    public void MatchesCategories_CountermagicTag_DoesNotMatchPlusOneCounters()
    {
        DeckPlanStrategyCatalog.TryGetBySlug("counters", out var counters);

        bool matches = DeckPlanStrategyCatalog.MatchesCategories(counters, ["Countermagic"]);

        Assert.False(matches);
    }

    [Fact]
    public void MatchesCategories_EmptyCategoryList_MatchesNoEntry()
    {
        foreach (var entry in DeckPlanStrategyCatalog.Entries)
        {
            Assert.False(DeckPlanStrategyCatalog.MatchesCategories(entry, []));
        }
    }
}
