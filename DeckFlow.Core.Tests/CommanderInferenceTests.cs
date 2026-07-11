using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Covers <see cref="CommanderInference.InferLeadingCommanderNames"/>: the Moxfield
/// header-less commander convention (leading card is the commander) plus the partner-pair
/// and alphabetical-mainboard guards.
/// </summary>
public sealed class CommanderInferenceTests
{
    [Fact]
    public void InferLeadingCommanderNames_HeaderlessAlphabeticalMainboard_ReturnsSingleLeader()
    {
        // Real report shape: commander first, then an alphabetically-sorted mainboard.
        var entries = new List<DeckEntry>
        {
            Entry("Bello, Bard of the Brambles"),
            Entry("Aggravated Assault"),
            Entry("Ancient Tomb"),
            Entry("Arid Mesa"),
        };

        IReadOnlyList<string> commanders = CommanderInference.InferLeadingCommanderNames(entries);

        Assert.Equal(new[] { "Bello, Bard of the Brambles" }, commanders);
    }

    [Fact]
    public void InferLeadingCommanderNames_ExplicitCommanderBoard_ReturnsEmpty()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Tymna the Weaver", board: "commander"),
            Entry("Aggravated Assault"),
        };

        Assert.Empty(CommanderInference.InferLeadingCommanderNames(entries));
    }

    [Fact]
    public void InferLeadingCommanderNames_PartnerPair_ReturnsBoth()
    {
        // Two leading one-ofs whose second sorts AFTER the third entry — a genuine partner
        // pair, not an alphabetical run — so both are kept.
        var entries = new List<DeckEntry>
        {
            Entry("Tana, the Bloodsower"),
            Entry("Kraum, Ludevic's Opus"),
            Entry("Aggravated Assault"),
            Entry("Ancient Tomb"),
        };

        IReadOnlyList<string> commanders = CommanderInference.InferLeadingCommanderNames(entries);

        Assert.Equal(new[] { "Tana, the Bloodsower", "Kraum, Ludevic's Opus" }, commanders);
    }

    [Fact]
    public void InferLeadingCommanderNames_AlphabetizedRun_PinsCurrentSingleCommanderGuard()
    {
        // Eligibility-based partner recovery happens later in ManabaseAnalysisService; Core
        // intentionally keeps the alphabetical single-commander guard unchanged here.
        var entries = new List<DeckEntry>
        {
            Entry("Bello, Bard of the Brambles"),
            Entry("Aggravated Assault"),
            Entry("Ancient Tomb"),
        };

        IReadOnlyList<string> commanders = CommanderInference.InferLeadingCommanderNames(entries);

        Assert.Equal(new[] { "Bello, Bard of the Brambles" }, commanders);
    }

    [Fact]
    public void InferLeadingCommanderNames_NonAlphabeticalLeadingPair_PinsCurrentPartnerHeuristic()
    {
        // Eligibility-based partner recovery happens later in ManabaseAnalysisService; this
        // test pins the current structure-only partner heuristic until that pass runs.
        var entries = new List<DeckEntry>
        {
            Entry("Tana, the Bloodsower"),
            Entry("Kraum, Ludevic's Opus"),
            Entry("Aggravated Assault"),
        };

        IReadOnlyList<string> commanders = CommanderInference.InferLeadingCommanderNames(entries);

        Assert.Equal(new[] { "Tana, the Bloodsower", "Kraum, Ludevic's Opus" }, commanders);
    }

    [Fact]
    public void InferLeadingCommanderNames_LeadingMultiCopyCard_ReturnsEmpty()
    {
        // A list pasted lands-first (e.g. a playset of basics) has no leading one-of, so no
        // commander can be inferred — the guard must not grab a basic land.
        var entries = new List<DeckEntry>
        {
            Entry("Snow-Covered Forest", quantity: 3),
            Entry("Aggravated Assault"),
        };

        Assert.Empty(CommanderInference.InferLeadingCommanderNames(entries));
    }

    [Fact]
    public void InferLeadingCommanderNames_SingleEntry_ReturnsThatEntry()
    {
        var entries = new List<DeckEntry> { Entry("Bello, Bard of the Brambles") };

        Assert.Equal(new[] { "Bello, Bard of the Brambles" }, CommanderInference.InferLeadingCommanderNames(entries));
    }

    [Fact]
    public void InferLeadingCommanderNames_EmptyList_ReturnsEmpty()
    {
        Assert.Empty(CommanderInference.InferLeadingCommanderNames(new List<DeckEntry>()));
    }

    private static DeckEntry Entry(string name, int quantity = 1, string board = "mainboard") => new()
    {
        Name = name,
        NormalizedName = name.ToLowerInvariant(),
        Quantity = quantity,
        Board = board,
    };
}
