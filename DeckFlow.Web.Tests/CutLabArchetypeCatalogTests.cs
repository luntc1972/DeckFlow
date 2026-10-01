using DeckFlow.Core.Analysis;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Coverage for the fixed Cut Lab deck archetype catalog.</summary>
public sealed class CutLabArchetypeCatalogTests
{
    [Fact]
    public void ArchetypeCatalog_HasSevenEntriesInSpecOrder()
    {
        Assert.Equal(
            ["turbo-combo", "midrange-combo", "stax", "control", "aggro-voltron", "value-engine", "spellslinger-storm"],
            CutLabArchetypeCatalog.Entries.Select(entry => entry.Slug));
    }

    [Fact]
    public void ArchetypeCatalog_EveryPresetStrategyIsAKnownStrategySlug()
    {
        Assert.All(
            CutLabArchetypeCatalog.Entries.SelectMany(entry => entry.PresetStrategies),
            strategy => Assert.True(DeckPlanStrategyCatalog.TryGetBySlug(strategy, out _)));
    }

    [Fact]
    public void ArchetypeCatalog_EveryDefaultGoalIsWithinGoalBounds()
    {
        Assert.All(CutLabArchetypeCatalog.Entries, entry =>
        {
            Assert.InRange(entry.DefaultGoals.CommanderByTurn, CutLabGoalDefaults.MinGoalTurn, CutLabGoalDefaults.MaxGoalTurn);
            Assert.InRange(entry.DefaultGoals.EngineByTurn, CutLabGoalDefaults.MinGoalTurn, CutLabGoalDefaults.MaxGoalTurn);
            Assert.InRange(entry.DefaultGoals.RepresentativeLineByTurn, CutLabGoalDefaults.MinGoalTurn, CutLabGoalDefaults.MaxGoalTurn);
        });
    }

    [Theory]
    [InlineData("Stax", null)]
    [InlineData("stax ", null)]
    [InlineData("unknown", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("stax", "stax")]
    public void ArchetypeCatalog_NormalizeSlug_RejectsWrongCaseWhitespaceAndUnknown(string? slug, string? expected)
    {
        Assert.Equal(expected, CutLabArchetypeCatalog.NormalizeSlug(slug));
    }

    [Fact]
    public void EffectiveStrategies_ArchetypeOnly_ReturnsPresets()
    {
        var profile = new CutLabPlanProfile { Archetype = "aggro-voltron" };

        Assert.Equal(["voltron", "combat"], CutLabArchetypeCatalog.EffectiveStrategies(profile));
    }

    [Fact]
    public void EffectiveStrategies_ManualAndPresetOverlap_NoDuplicatesManualOrderFirst()
    {
        var profile = new CutLabPlanProfile { Archetype = "turbo-combo", GenericStrategies = ["tokens", "combo"] };

        Assert.Equal(["tokens", "combo"], CutLabArchetypeCatalog.EffectiveStrategies(profile));
    }

    [Fact]
    public void EffectiveStrategies_NullOrUnknownArchetype_ReturnsManualOnly()
    {
        var manual = new CutLabPlanProfile { GenericStrategies = ["tokens"] };
        var unknown = manual with { Archetype = "unknown" };

        Assert.Empty(CutLabArchetypeCatalog.EffectiveStrategies(null));
        Assert.Equal(["tokens"], CutLabArchetypeCatalog.EffectiveStrategies(manual));
        Assert.Equal(["tokens"], CutLabArchetypeCatalog.EffectiveStrategies(unknown));
    }

    [Fact]
    public void EffectiveStrategies_DoesNotChangeGenericStrategies()
    {
        var profile = new CutLabPlanProfile { Archetype = "turbo-combo", GenericStrategies = ["tokens"] };

        _ = CutLabArchetypeCatalog.EffectiveStrategies(profile);

        Assert.Equal(["tokens"], profile.GenericStrategies);
    }
}
