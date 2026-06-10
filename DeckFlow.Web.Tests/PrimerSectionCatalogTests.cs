using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Unit tests for <see cref="PrimerSectionCatalog"/> catalog shape, bracket gating, presets, and normalization.
/// </summary>
public sealed class PrimerSectionCatalogTests
{
    [Fact]
    public void Catalog_Has31Sections()
    {
        Assert.Equal(31, PrimerSectionCatalog.AllSections.Count);
    }

    [Fact]
    public void Catalog_Has5Groups()
    {
        Assert.Equal(5, PrimerSectionCatalog.Groups.Count);
        Assert.Equal(
            ["Identity", "Combos", "Gameplay", "Matchups", "Maintenance"],
            PrimerSectionCatalog.Groups.Select(group => group.Label).ToArray());
    }

    [Fact]
    public void Catalog_EverySection_HasHelpText()
    {
        Assert.All(PrimerSectionCatalog.AllSections, section => Assert.False(string.IsNullOrWhiteSpace(section.HelpText)));
    }

    [Fact]
    public void Gates_ExactlyTwoCedhOnly_OneCasualOnly()
    {
        Assert.Equal(2, PrimerSectionCatalog.CedhOnlySectionIds.Count);
        Assert.Single(PrimerSectionCatalog.CasualOnlySectionIds);
    }

    [Fact]
    public void GetPreset_Cedh_IncludesCedhOnly_ExcludesCasualOnly()
    {
        var preset = PrimerSectionCatalog.GetPresetForBracket("cEDH");

        Assert.All(PrimerSectionCatalog.CedhOnlySectionIds, id => Assert.Contains(id, preset));
        Assert.All(PrimerSectionCatalog.CasualOnlySectionIds, id => Assert.DoesNotContain(id, preset));
    }

    [Fact]
    public void GetPreset_Casual_ExcludesCedhOnly()
    {
        var preset = PrimerSectionCatalog.GetPresetForBracket("Core");

        Assert.All(PrimerSectionCatalog.CedhOnlySectionIds, id => Assert.DoesNotContain(id, preset));
    }

    [Fact]
    public void Normalize_StripsCedhOnly_ForCasualBracket()
    {
        var cedhOnlyId = PrimerSectionCatalog.CedhOnlySectionIds.First();
        var commonId = PrimerSectionCatalog.AllSections.First(section => !PrimerSectionCatalog.CedhOnlySectionIds.Contains(section.Id) && !PrimerSectionCatalog.CasualOnlySectionIds.Contains(section.Id)).Id;

        var normalized = PrimerSectionCatalog.NormalizeSelections([cedhOnlyId, commonId], "Core");

        Assert.Equal([commonId], normalized);
    }

    [Fact]
    public void Normalize_StripsCasualOnly_ForCedhBracket()
    {
        var casualOnlyId = PrimerSectionCatalog.CasualOnlySectionIds.First();
        var commonId = PrimerSectionCatalog.AllSections.First(section => !PrimerSectionCatalog.CedhOnlySectionIds.Contains(section.Id) && !PrimerSectionCatalog.CasualOnlySectionIds.Contains(section.Id)).Id;

        var normalized = PrimerSectionCatalog.NormalizeSelections([casualOnlyId, commonId], "cEDH");

        Assert.Equal([commonId], normalized);
    }

    [Fact]
    public void Normalize_DropsUnknownIds_AndDedupes()
    {
        var firstId = PrimerSectionCatalog.AllSections[0].Id;
        var secondId = PrimerSectionCatalog.AllSections[1].Id;

        var normalized = PrimerSectionCatalog.NormalizeSelections([secondId, "unknown-id", firstId, secondId], "Core");

        Assert.Equal([firstId, secondId], normalized);
    }
}
