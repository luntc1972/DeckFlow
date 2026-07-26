using DeckFlow.Core.Integration;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Parsing;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for <see cref="MoxfieldParser"/> and <see cref="ArchidektParser"/> covering name normalization,
/// MDFC handling, board assignment, set code parsing, and category tag parsing.
/// </summary>
public sealed class ParserTests
{
    [Fact]
    public void Normalize_HandlesCaseSpacesAndMdfc()
    {
        var normalized = CardNormalizer.Normalize(" Bridgeworks Battle // Tanglespan Bridgeworks ");
        Assert.Equal("bridgeworks battle", normalized);
    }

    [Fact]
    public void Normalize_IgnoresCommaDifferences()
    {
        Assert.Equal(
            CardNormalizer.Normalize("Bello, Bard of the Brambles"),
            CardNormalizer.Normalize("Bello Bard of the Brambles"));
    }

    [Fact]
    public void MoxfieldApiUrl_ExtractsDeckIdFromPublicUrl()
    {
        var success = MoxfieldApiUrl.TryGetDeckId("https://moxfield.com/decks/fNC0NaQftkO8uWFMD8O49g", out var deckId);

        Assert.True(success);
        Assert.Equal("fNC0NaQftkO8uWFMD8O49g", deckId);
        Assert.Equal("https://api2.moxfield.com/v3/decks/all/fNC0NaQftkO8uWFMD8O49g", MoxfieldApiUrl.BuildDeckApiUri(deckId).ToString());
    }

    [Fact]
    public void ArchidektApiUrl_ExtractsDeckIdFromPublicUrl()
    {
        var success = ArchidektApiUrl.TryGetDeckId("https://archidekt.com/decks/15918942/trashpanda", out var deckId);

        Assert.True(success);
        Assert.Equal("15918942", deckId);
        Assert.Equal("https://archidekt.com/api/decks/15918942/", ArchidektApiUrl.BuildDeckApiUri(deckId).ToString());
    }

    [Fact]
    public void MoxfieldParser_TracksBoardsAndFoilMarkers()
    {
        var entries = new MoxfieldParser().ParseText("""
            Commander
            1 Atraxa, Praetors' Voice (MH2) 17 *F*

            1 Arcane Signet
            Sideboard
            2 Snow-Covered Mountain
            """);

        Assert.Equal(3, entries.Count);
        Assert.Equal("commander", entries[0].Board);
        Assert.True(entries[0].IsFoil);
        Assert.Equal("mainboard", entries[1].Board);
        Assert.Equal("sideboard", entries[2].Board);
    }

    [Fact]
    public void MoxfieldParser_StripsEtchedMarkerAndKeepsPrinting()
    {
        // Regression: a trailing *E* (etched foil) used to defeat the end-anchored printing
        // regex, leaving "(P30M) 2 *E*" stuck in the name so both lookups missed.
        var entries = new MoxfieldParser().ParseText("1 Lotus Petal (P30M) 2 *E*");

        Assert.Single(entries);
        Assert.Equal("Lotus Petal", entries[0].Name);
        Assert.Equal("P30M", entries[0].SetCode);
        Assert.Equal("2", entries[0].CollectorNumber);
        Assert.True(entries[0].IsFoil);
    }

    [Fact]
    public void ArchidektParser_StripsEtchedMarkerAndKeepsPrinting()
    {
        var entries = new ArchidektParser().ParseText("1 Mox Opal (SLD) 1072 *E*");

        Assert.Single(entries);
        Assert.Equal("Mox Opal", entries[0].Name);
        Assert.Equal("SLD", entries[0].SetCode);
        Assert.Equal("1072", entries[0].CollectorNumber);
        Assert.True(entries[0].IsFoil);
    }

    [Fact]
    public void MoxfieldParser_ParsesSideboardEntries()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Bello, Bard of the Brambles
            Sideboard
            1 Counterspell
            1 Negate
            """);

        Assert.Equal(3, entries.Count);
        Assert.Equal("mainboard", entries[0].Board);
        Assert.Equal("Bello, Bard of the Brambles", entries[0].Name);
        Assert.Equal("sideboard", entries[1].Board);
        Assert.Equal("Counterspell", entries[1].Name);
        Assert.Equal("Sideboard", entries[1].Category);
        Assert.Equal("sideboard", entries[2].Board);
        Assert.Equal("Negate", entries[2].Name);
    }

    [Fact]
    public void MoxfieldParser_ParsesSideboardHeaderWithColon()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Bello, Bard of the Brambles
            SIDEBOARD:
            1 Counterspell
            """);

        Assert.Equal(2, entries.Count);
        Assert.Equal("mainboard", entries[0].Board);
        Assert.Equal("Bello, Bard of the Brambles", entries[0].Name);
        Assert.Equal("sideboard", entries[1].Board);
        Assert.Equal("Counterspell", entries[1].Name);
    }

    [Fact]
    public void MoxfieldParser_ParsesGoldfishArenaDownloadPreamble()
    {
        var entries = new MoxfieldParser().ParseText("""
            About
            Name 8 Rack

            Deck
            4 Thoughtseize (2XM) 109
            1 The Rack (4ED) 343
            Sideboard
            2 Duress
            """);

        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal("Thoughtseize", entry.Name);
                Assert.Equal(4, entry.Quantity);
                Assert.Equal("mainboard", entry.Board);
            },
            entry =>
            {
                Assert.Equal("The Rack", entry.Name);
                Assert.Equal(1, entry.Quantity);
                Assert.Equal("mainboard", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Duress", entry.Name);
                Assert.Equal(2, entry.Quantity);
                Assert.Equal("sideboard", entry.Board);
            });
        Assert.DoesNotContain(entries, entry => string.Equals(entry.Name, "About", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => string.Equals(entry.Name, "Name 8 Rack", StringComparison.Ordinal));
    }

    [Fact]
    public void MoxfieldParser_ParsesArenaExportCommanderSection()
    {
        var entries = new MoxfieldParser().ParseText("""
            Deck
            1 Sol Ring (C21) 263

            Commander
            1 Atraxa, Praetors' Voice (2X2) 190
            """);

        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal("Sol Ring", entry.Name);
                Assert.Equal("mainboard", entry.Board);
                Assert.Equal("C21", entry.SetCode);
                Assert.Equal("263", entry.CollectorNumber);
            },
            entry =>
            {
                Assert.Equal("Atraxa, Praetors' Voice", entry.Name);
                Assert.Equal("commander", entry.Board);
                Assert.Equal("2X2", entry.SetCode);
                Assert.Equal("190", entry.CollectorNumber);
            });
    }

    [Fact]
    public void MoxfieldParser_ParsesLegacyDecSideboardPrefixWithoutChangingRunningBoard()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Sol Ring
            SB: 2 Duress
            1 Arcane Signet
            """);

        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal("Sol Ring", entry.Name);
                Assert.Equal("mainboard", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Duress", entry.Name);
                Assert.Equal(2, entry.Quantity);
                Assert.Equal("sideboard", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Arcane Signet", entry.Name);
                Assert.Equal("mainboard", entry.Board);
            });
    }

    [Fact]
    public void MoxfieldParser_TrailingSbPrefixedBlockIsNeverPromotedToCommander()
    {
        // Why: an SB: prefix is an explicit sideboard marker, so a trailing blank-separated block it
        // opens must be exempt from the trailing-commander promotion heuristic.
        var entries = new MoxfieldParser().ParseText("""
            1 Sol Ring

            Sideboard
            1 Duress

            SB: 1 Vexing Shusher
            """);

        var shusher = Assert.Single(entries, entry => entry.Name == "Vexing Shusher");
        Assert.Equal("sideboard", shusher.Board);
        Assert.DoesNotContain(entries, entry => entry.Board == "commander");
    }

    [Fact]
    public void MoxfieldParser_BareDeckAndSideboardHeadersStillParse()
    {
        var entries = new MoxfieldParser().ParseText("""
            Deck
            1 Sol Ring
            Sideboard
            1 Duress
            """);

        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal("Sol Ring", entry.Name);
                Assert.Equal("mainboard", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Duress", entry.Name);
                Assert.Equal("sideboard", entry.Board);
            });
    }

    [Fact]
    public void MoxfieldParser_NameLineAfterEntriesIsNotIgnoredAsPreamble()
    {
        // Why: the deck-name preamble rule is gated to the pre-entry region only. After entries
        // exist, a "Name ..." line falls through to the LEGACY path — implicit-quantity parsing
        // treats it as a card line (pre-existing behavior this change must not alter). This pins
        // that the new rule does not swallow post-entry lines; it does NOT bless the legacy quirk.
        var entries = new MoxfieldParser().ParseText("""
            1 Sol Ring
            Name Sideboard Notes
            """);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Sol Ring", entries[0].Name);
        Assert.Equal("Name Sideboard Notes", entries[1].Name);
    }

    [Fact]
    public void MoxfieldParser_PromotesTrailingSingleCommanderBlockAfterSideboard()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Sol Ring
            1 Arcane Signet
            SIDEBOARD:
            1 Abrade
            1 Return to Dust

            1 Winota, Joiner of Forces
            """);

        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal("Sol Ring", entry.Name);
                Assert.Equal("mainboard", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Arcane Signet", entry.Name);
                Assert.Equal("mainboard", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Abrade", entry.Name);
                Assert.Equal("sideboard", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Return to Dust", entry.Name);
                Assert.Equal("sideboard", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Winota, Joiner of Forces", entry.Name);
                Assert.Equal("commander", entry.Board);
            });
    }

    [Fact]
    public void MoxfieldParser_PromotesTrailingPartnerBlockAfterSideboard()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Sol Ring
            Sideboard
            1 Swords to Plowshares

            1 Tana, the Bloodsower
            1 Kraum, Ludevic's Opus
            """);

        Assert.Equal("mainboard", entries[0].Board);
        Assert.Equal("sideboard", entries[1].Board);
        Assert.Equal("commander", entries[2].Board);
        Assert.Equal("Tana, the Bloodsower", entries[2].Name);
        Assert.Equal("commander", entries[3].Board);
        Assert.Equal("Kraum, Ludevic's Opus", entries[3].Name);
    }

    [Fact]
    public void MoxfieldParser_ExplicitCommanderHeaderStillWins()
    {
        var entries = new MoxfieldParser().ParseText("""
            Commander
            1 Winota, Joiner of Forces

            1 Sol Ring
            Sideboard
            1 Abrade
            """);

        Assert.Equal("commander", entries[0].Board);
        Assert.Equal("Winota, Joiner of Forces", entries[0].Name);
        Assert.Equal("mainboard", entries[1].Board);
        Assert.Equal("sideboard", entries[2].Board);
    }

    [Fact]
    public void MoxfieldParser_SingleSideboardCardWithoutTrailingBlock_StaysSideboard()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Sol Ring
            Sideboard
            1 Winota, Joiner of Forces
            """);

        Assert.Equal("mainboard", entries[0].Board);
        Assert.Equal("sideboard", entries[1].Board);
        Assert.Equal("Winota, Joiner of Forces", entries[1].Name);
    }

    [Fact]
    public void MoxfieldParser_FinalMaybeboardBlock_IsNotPromotedToCommander()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Sol Ring
            Sideboard
            1 Abrade

            Maybeboard
            1 Tana, the Bloodsower
            1 Kraum, Ludevic's Opus
            """);

        Assert.Equal("mainboard", entries[0].Board);
        Assert.Equal("sideboard", entries[1].Board);
        Assert.Equal("maybeboard", entries[2].Board);
        Assert.Equal("maybeboard", entries[3].Board);
    }

    [Fact]
    public void MoxfieldParser_TrailingBlockFollowedByMoreCards_IsNotPromoted()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Sol Ring
            Sideboard
            1 Abrade

            1 Winota, Joiner of Forces

            2 Mountain
            """);

        Assert.Equal("mainboard", entries[0].Board);
        Assert.Equal("sideboard", entries[1].Board);
        Assert.Equal("sideboard", entries[2].Board);
        Assert.Equal("sideboard", entries[3].Board);
    }

    [Fact]
    public void MoxfieldParser_AllowsImplicitQuantityOfOne()
    {
        var entries = new MoxfieldParser().ParseText("""
            Bello, Bard of the Brambles (BLC) 1
            1 Arcane Signet
            """);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Bello, Bard of the Brambles", entries[0].Name);
        Assert.Equal(1, entries[0].Quantity);
        Assert.Equal("BLC", entries[0].SetCode);
        Assert.Equal("1", entries[0].CollectorNumber);
    }

    [Fact]
    public void MoxfieldParser_IgnoresPossibleNamesAndTrailingNotes()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Bello, Bard of the Brambles
            1 Arcane Signet
            Possible names:
            The Fire You Saved
            """);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Bello, Bard of the Brambles", entries[0].Name);
        Assert.Equal("Arcane Signet", entries[1].Name);
    }

    [Fact]
    public void MoxfieldParser_SkipsBareKeywordLinesAndContinuesParsingRemainingCards()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Sol Ring
            1 Arcane Signet
            Notes
            1 Swords to Plowshares
            Description:
            1 Path to Exile
            1 Generous Gift
            """);

        Assert.Equal(5, entries.Count);
        Assert.Equal("Sol Ring", entries[0].Name);
        Assert.Equal("Arcane Signet", entries[1].Name);
        Assert.Equal("Swords to Plowshares", entries[2].Name);
        Assert.Equal("Path to Exile", entries[3].Name);
        Assert.Equal("Generous Gift", entries[4].Name);
    }

    [Fact]
    public void MoxfieldParser_IgnoresTrailingProseWithoutMisparsingItAsACard()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Sol Ring
            1 Arcane Signet
            Notes:
            Bring in artifact hate if the table is heavy on fast mana.
            Description
            Keep the curve low and pressure planeswalkers.
            """);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Sol Ring", entries[0].Name);
        Assert.Equal("Arcane Signet", entries[1].Name);
    }

    [Fact]
    public void MoxfieldParser_UsesInlineHashtagsToAssignBoardsInFlatLists()
    {
        var entries = new MoxfieldParser().ParseText("""
            1 Bohn, Beguiling Balladeer (sld) 1242 #Commander #engine #exile
            1 Arcane Signet (lcc) 299 #ramp
            1 Counterspell (2xm) 51 #Sideboard #interaction
            1 Smothering Tithe (rna) 22 #Maybeboard #ramp
            """);

        Assert.Equal("commander", entries[0].Board);
        Assert.Equal("engine,exile", entries[0].Category);
        Assert.Equal("mainboard", entries[1].Board);
        Assert.Equal("ramp", entries[1].Category);
        Assert.Equal("sideboard", entries[2].Board);
        Assert.Equal("interaction", entries[2].Category);
        Assert.Equal("maybeboard", entries[3].Board);
        Assert.Equal("ramp", entries[3].Category);
    }

    [Fact]
    public void ArchidektParser_ParsesCategoriesAndMaybeboard()
    {
        var entries = new ArchidektParser().ParseText("""
            1 Wandering Archaic (stx) 6 [Maybeboard{noDeck}{noPrice},Ramp]
            1 Aggravated Assault (wot) 39 [Finisher]
            """);

        Assert.Equal("maybeboard", entries[0].Board);
        Assert.Equal("Ramp", entries[0].Category);
        Assert.Equal("mainboard", entries[1].Board);
        Assert.Equal("Finisher", entries[1].Category);
    }

    [Fact]
    public void ArchidektParser_AllowsFoilMarkerBeforeCategories()
    {
        var entries = new ArchidektParser().ParseText("1 Guardian Project (pip) 727 *F* [Draw]");

        var entry = Assert.Single(entries);
        Assert.Equal("Guardian Project", entry.Name);
        Assert.Equal("pip", entry.SetCode);
        Assert.Equal("727", entry.CollectorNumber);
        Assert.Equal("Draw", entry.Category);
        Assert.True(entry.IsFoil);
    }

    [Fact]
    public void ArchidektParser_AllowsCommanderLineWithoutLeadingQuantity()
    {
        var entries = new ArchidektParser().ParseText("""
            Edgin, Larcenous Lutenist (SLD) 1242 #Commander #ExileEngine
            Deck
            1 Arcane Signet (LCC) 299 #Ramp
            """);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Edgin, Larcenous Lutenist", entries[0].Name);
        Assert.Equal(1, entries[0].Quantity);
        Assert.Equal("commander", entries[0].Board);
        Assert.Equal("ExileEngine", entries[0].Category);
        Assert.Equal("SLD", entries[0].SetCode);
        Assert.Equal("1242", entries[0].CollectorNumber);
    }

    [Fact]
    public void ArchidektParser_IgnoresPossibleNamesAndTrailingNotes()
    {
        var entries = new ArchidektParser().ParseText("""
            Edgin, Larcenous Lutenist (SLD) 1242 #Commander #ExileEngine
            Deck
            1 Arcane Signet (LCC) 299 #Ramp
            1 Goblin Bombardment (WOT) 43 #TokenConversion

            Possible names
            Anytime, Anywhere, All at Once
            The Fire You Saved
            """);

        Assert.Equal(3, entries.Count);
        Assert.Equal("Edgin, Larcenous Lutenist", entries[0].Name);
        Assert.Equal("Arcane Signet", entries[1].Name);
        Assert.Equal("Goblin Bombardment", entries[2].Name);
    }

    [Fact]
    public void ArchidektParser_SkipsBareKeywordLinesAndContinuesParsingRemainingCards()
    {
        var entries = new ArchidektParser().ParseText("""
            1 Sol Ring
            1 Arcane Signet
            Notes
            1 Swords to Plowshares
            Description:
            1 Path to Exile
            1 Generous Gift
            """);

        Assert.Equal(5, entries.Count);
        Assert.Equal("Sol Ring", entries[0].Name);
        Assert.Equal("Arcane Signet", entries[1].Name);
        Assert.Equal("Swords to Plowshares", entries[2].Name);
        Assert.Equal("Path to Exile", entries[3].Name);
        Assert.Equal("Generous Gift", entries[4].Name);
    }

    [Fact]
    public void ArchidektParser_IgnoresTrailingProseWithoutMisparsingItAsACard()
    {
        var entries = new ArchidektParser().ParseText("""
            1 Sol Ring
            1 Arcane Signet
            Notes:
            Bring in artifact hate if the table is heavy on fast mana.
            Description
            Keep the curve low and pressure planeswalkers.
            """);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Sol Ring", entries[0].Name);
        Assert.Equal("Arcane Signet", entries[1].Name);
    }

    [Fact]
    public void ArchidektParser_UsesInlineSideboardAndMaybeboardTagsInFlatLists()
    {
        var entries = new ArchidektParser().ParseText("""
            Edgin, Larcenous Lutenist (SLD) 1242 #Commander #ExileEngine
            1 Arcane Signet (LCC) 299 #Ramp
            1 Counterspell (2XM) 51 #Sideboard #Interaction
            1 Smothering Tithe (RNA) 22 #Maybeboard #Ramp
            """);

        Assert.Equal("commander", entries[0].Board);
        Assert.Equal("ExileEngine", entries[0].Category);
        Assert.Equal("mainboard", entries[1].Board);
        Assert.Equal("Ramp", entries[1].Category);
        Assert.Equal("sideboard", entries[2].Board);
        Assert.Equal("Interaction", entries[2].Category);
        Assert.Equal("maybeboard", entries[3].Board);
        Assert.Equal("Ramp", entries[3].Category);
    }

    [Fact]
    public void ArchidektParser_SwitchesBoard_OnCommanderAndMainboardSectionHeaders()
    {
        var entries = new ArchidektParser().ParseText("""
            Commander
            1 Atraxa, Praetors' Voice
            Mainboard
            1 Sol Ring
            1 Arcane Signet
            """);

        Assert.Equal("commander", entries[0].Board);
        Assert.Equal("Atraxa, Praetors' Voice", entries[0].Name);
        Assert.Equal("mainboard", entries[1].Board);
        Assert.Equal("mainboard", entries[2].Board);
    }

    [Fact]
    public void ArchidektParser_TreatsDeckHeaderAsMainboardAndSwitchesBack_AfterCommanderSection()
    {
        var entries = new ArchidektParser().ParseText("""
            Commander
            1 Kinnan, Bonder Prodigy
            Deck
            1 Birds of Paradise
            1 Mana Crypt
            """);

        Assert.Equal("commander", entries[0].Board);
        Assert.Equal("mainboard", entries[1].Board);
        Assert.Equal("mainboard", entries[2].Board);
    }

    [Fact]
    public void ArchidektParser_MapsSectionHeadersToSideboardAndMaybeboard()
    {
        var entries = new ArchidektParser().ParseText("""
            Commander
            1 Atraxa, Praetors' Voice
            Mainboard
            1 Sol Ring
            Sideboard
            1 Counterspell
            Maybeboard
            1 Cyclonic Rift
            Possible Includes
            1 Wrath of God
            """);

        Assert.Equal("commander", entries[0].Board);
        Assert.Equal("mainboard", entries[1].Board);
        Assert.Equal("sideboard", entries[2].Board);
        Assert.Equal("maybeboard", entries[3].Board);
        Assert.Equal("maybeboard", entries[4].Board);
    }

    [Fact]
    public void ArchidektParser_InlineTagOverridesSectionHeaderBoardState()
    {
        // A card explicitly tagged [Sideboard] under a Mainboard section header
        // should still go to the sideboard — inline categories win.
        var entries = new ArchidektParser().ParseText("""
            Mainboard
            1 Sol Ring [Ramp]
            1 Counterspell [Sideboard,Interaction]
            """);

        Assert.Equal("mainboard", entries[0].Board);
        Assert.Equal("sideboard", entries[1].Board);
    }

    [Fact]
    public void ArchidektParser_RoundTripsCanonicalDeckFlowFormat()
    {
        // The hybrid storage commit emits decklist text starting with
        // "Commander\n1 <commander>\n\nMainboard\n<cards>\n\nPossible Includes\n<cards>".
        // The Archidekt parser must accept this format cleanly so the
        // canonical-fallback path works for users whose decks parse via
        // Archidekt (set codes / collector numbers).
        var entries = new ArchidektParser().ParseText("""
            Commander
            1 Atraxa, Praetors' Voice

            Mainboard
            1 Sol Ring
            1 Arcane Signet

            Possible Includes
            1 Smothering Tithe
            """);

        Assert.Equal(4, entries.Count);
        Assert.Equal("commander", entries[0].Board);
        Assert.Equal("mainboard", entries[1].Board);
        Assert.Equal("mainboard", entries[2].Board);
        Assert.Equal("maybeboard", entries[3].Board);
    }
}
