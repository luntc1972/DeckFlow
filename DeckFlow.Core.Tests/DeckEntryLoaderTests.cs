using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Covers source-autodetect deck loading via <see cref="DeckEntryLoader"/>.
/// </summary>
public sealed class DeckEntryLoaderTests
{
    [Theory]
    [InlineData("99999999999 Sol Ring")]
    [InlineData("1000 Sol Ring")]
    public async Task LoadFromSourceAsync_OutOfRangePastedQuantityInDefaultMode_ThrowsInvalidOperationException(string line)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateLoader().LoadFromSourceAsync(line));

        Assert.Contains(line, exception.Message);
        Assert.Contains("0..999", exception.Message);
    }

    [Theory]
    [InlineData("99999999999 Sol Ring")]
    [InlineData("1000 Sol Ring")]
    public async Task LoadFromSourceAsync_OutOfRangePastedQuantityWithPropagateParseException_ThrowsDeckParseException(string line)
    {
        var exception = await Assert.ThrowsAsync<DeckParseException>(() => CreateLoader().LoadFromSourceAsync(
            line,
            UnrecognizedPasteBehavior.PropagateParseException));

        Assert.Contains(line, exception.Message);
        Assert.Contains("0..999", exception.Message);
    }

    [Fact]
    public async Task LoadFromSourceAsync_NonAsciiPastedQuantity_UsesUnquantifiedName()
    {
        var result = await CreateLoader().LoadFromSourceAsync("１ Sol Ring");
        var entry = Assert.Single(result.Entries);

        Assert.Equal("１ Sol Ring", entry.Name);
        Assert.Equal(1, entry.Quantity);
    }

    [Fact]
    public async Task LoadFromSourceAsync_MoxfieldUrl_ReturnsEntriesAndFallbackNotice()
    {
        var expectedEntries = new List<DeckEntry>
        {
            new() { Name = "Atraxa, Praetors' Voice", NormalizedName = "atraxa, praetors' voice", Quantity = 1, Board = "commander" },
            new() { Name = "Sol Ring", NormalizedName = "sol ring", Quantity = 1, Board = "mainboard" }
        };
        var importer = new FakeMoxfieldDeckImporter(
            _ => expectedEntries,
            _ => new MoxfieldImportResult(expectedEntries, MoxfieldImportSource.CommanderSpellbookFallback, "Used fallback import."));
        var loader = CreateLoader(importer: importer);

        var result = await loader.LoadFromSourceAsync(" https://www.moxfield.com/decks/example ", cancellationToken: CancellationToken.None);

        Assert.Collection(
            result.Entries,
            entry =>
            {
                Assert.Equal("Atraxa, Praetors' Voice", entry.Name);
                Assert.Equal("commander", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Sol Ring", entry.Name);
                Assert.Equal("mainboard", entry.Board);
            });
        Assert.Equal("Used fallback import.", result.FallbackNotice);
        Assert.True(result.IncludedBoardsUnavailable);
        Assert.Equal(" https://www.moxfield.com/decks/example ", importer.LastImportWithSourceArgument);
    }

    [Fact]
    public async Task LoadFromSourceAsync_MoxfieldUrl_PropagatesDetectedCompanionName()
    {
        var expectedEntries = new List<DeckEntry>
        {
            new() { Name = "Kraum, Ludevic's Opus", NormalizedName = "kraum, ludevic's opus", Quantity = 1, Board = "commander" }
        };
        var importer = new FakeMoxfieldDeckImporter(
            _ => expectedEntries,
            _ => new MoxfieldImportResult(expectedEntries, MoxfieldImportSource.Direct, null, "Jegantha, the Wellspring"));
        var loader = CreateLoader(importer: importer);

        var result = await loader.LoadFromSourceAsync("https://www.moxfield.com/decks/example");

        Assert.Equal("Jegantha, the Wellspring", result.DetectedCompanionName);
        Assert.False(result.IncludedBoardsUnavailable);
    }

    [Fact]
    public async Task LoadFromSourceAsync_ArchidektUrl_ReturnsEntriesAndNullNotice()
    {
        var expectedEntries = new List<DeckEntry>
        {
            new() { Name = "Kinnan, Bonder Prodigy", NormalizedName = "kinnan, bonder prodigy", Quantity = 1, Board = "commander" },
            new() { Name = "Arcane Signet", NormalizedName = "arcane signet", Quantity = 1, Board = "mainboard" }
        };
        var importer = new FakeArchidektDeckImporter(_ => expectedEntries);
        var loader = CreateLoader(archidektImporter: importer);

        var result = await loader.LoadFromSourceAsync(" https://archidekt.com/decks/123 ");

        Assert.Same(expectedEntries, result.Entries);
        Assert.Null(result.FallbackNotice);
        Assert.Null(result.DetectedCompanionName);
        Assert.Equal(" https://archidekt.com/decks/123 ", importer.LastImportArgument);
    }

    [Fact]
    public async Task LoadFromSourceAsync_PastedMoxfieldText_ParsesEntries()
    {
        var loader = CreateLoader();

        var result = await loader.LoadFromSourceAsync("""
Commander
1 Atraxa, Praetors' Voice

Deck
1 Sol Ring
""");

        Assert.Collection(
            result.Entries,
            entry =>
            {
                Assert.Equal("Atraxa, Praetors' Voice", entry.Name);
                Assert.Equal("commander", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Sol Ring", entry.Name);
                Assert.Equal("mainboard", entry.Board);
            });
        Assert.Null(result.FallbackNotice);
        Assert.Null(result.DetectedCompanionName);
    }

    [Fact]
    public async Task LoadFromSourceAsync_PastedArchidektStyleSectionText_ParsesEntries()
    {
        var loader = CreateLoader();

        // Real production cascade tries MoxfieldParser first, and this Archidekt-style
        // section-header paste is accepted there without falling through.
        var result = await loader.LoadFromSourceAsync("""
Commander
Atraxa, Praetors' Voice

Deck
1 Sol Ring
""");

        Assert.Collection(
            result.Entries,
            entry =>
            {
                Assert.Equal("Atraxa, Praetors' Voice", entry.Name);
                Assert.Equal("commander", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Sol Ring", entry.Name);
                Assert.Equal("mainboard", entry.Board);
            });
        Assert.Null(result.FallbackNotice);
        Assert.Null(result.DetectedCompanionName);
    }

    [Fact]
    public async Task LoadFromSourceAsync_PastedArchidektStyleXQuantities_ParsesEntries()
    {
        var result = await LoadPasteAsync("""
Commander
1x Atraxa, Praetors' Voice
Main
1x Sol Ring
1x Arcane Signet
""");

        Assert.Equal(["Atraxa, Praetors' Voice", "Sol Ring", "Arcane Signet"],
            result.Entries.Select(entry => entry.Name));
        Assert.Equal("commander", result.Entries[0].Board);
        Assert.All(result.Entries.Skip(1), entry => Assert.Equal("mainboard", entry.Board));
    }

    [Theory]
    [InlineData("1x")]
    [InlineData("1")]
    public async Task LoadFromSourceAsync_PastedArchidektCategories_PreservesNamesBoardsAndPrintings(string quantity)
    {
        var result = await LoadPasteAsync($$"""
            {{quantity}} Atraxa, Praetors' Voice (cm2) 10 [Commander{top}]
            {{quantity}} Sol Ring (cmr) 123 [Ramp]
            {{quantity}} Anger (soc) 235 [Anthem]
            """);

        Assert.Equal(["Atraxa, Praetors' Voice", "Sol Ring", "Anger"], result.Entries.Select(entry => entry.Name));
        Assert.Equal("commander", result.Entries[0].Board);
        Assert.All(result.Entries.Skip(1), entry => Assert.Equal("mainboard", entry.Board));
        Assert.Equal("cmr", result.Entries[1].SetCode);
        Assert.Equal("soc", result.Entries[2].SetCode);
    }

    [Fact]
    public async Task LoadFromSourceAsync_PastedArchidektCategories_PreservesFoilAndPrinting()
    {
        var result = await LoadPasteAsync("""
            1x Sol Ring (cmr) 123 *F* [Ramp]
            1x Arcane Signet (cmr) 297 *E* [Ramp]
            """);

        var solRing = Assert.Single(result.Entries, entry => entry.Name == "Sol Ring");
        var arcaneSignet = Assert.Single(result.Entries, entry => entry.Name == "Arcane Signet");
        Assert.Equal("cmr", solRing.SetCode);
        Assert.Equal("123", solRing.CollectorNumber);
        Assert.True(solRing.IsFoil);
        Assert.True(arcaneSignet.IsFoil);
    }

    [Theory]
    [InlineData("1 deck idea [wip]\n1 Sol Ring\nSB: 1 Pyroblast", "Pyroblast")]
    // Why: Moxfield keeps quantified notes lines as cards, so only board placement is asserted.
    [InlineData("1 Sol Ring\nSB: 1 Swords to Plowshares\n\nNotes:\n2 combos [see primer]", "Swords to Plowshares")]
    public async Task LoadFromSourceAsync_BracketedPaste_PreservesInlineSideboard(string deckSource, string sideboardCard)
    {
        var result = await LoadPasteAsync(deckSource);

        Assert.Single(result.Entries, entry => entry.Name == "Sol Ring" && entry.Board == "mainboard");
        Assert.Single(result.Entries, entry => entry.Name == sideboardCard && entry.Board == "sideboard");
    }

    [Fact]
    public async Task LoadFromSourceAsync_PasteWithBracketedIdea_PreservesSideboardSectionAndCommander()
    {
        var result = await LoadPasteAsync(
            "1 deck idea [wip]\n1 Sol Ring\n\nSIDEBOARD:\n1 Pyroblast\n\n1 Atraxa, Praetors' Voice");

        Assert.Single(result.Entries, entry => entry.Name == "Pyroblast" && entry.Board == "sideboard");
        Assert.Single(result.Entries, entry => entry.Name == "Atraxa, Praetors' Voice" && entry.Board == "commander");
    }

    [Fact]
    public async Task LoadFromSourceAsync_PartiallyTaggedPaste_CleansNameAndPromotesTrailingCommander()
    {
        var result = await LoadPasteAsync(
            "1 Sol Ring\n1 Arcane Signet [Ramp]\n\nSIDEBOARD:\n1 Path to Exile\n\n1 Atraxa, Praetors' Voice");

        Assert.Single(result.Entries, entry => entry.Name == "Arcane Signet");
        Assert.Single(result.Entries, entry => entry.Name == "Path to Exile" && entry.Board == "sideboard");
        Assert.Single(result.Entries, entry => entry.Name == "Atraxa, Praetors' Voice" && entry.Board == "commander");
    }

    [Fact]
    public async Task LoadFromSourceAsync_TaggedPasteWithSideboardPrefix_PreservesPrintingAndSideboard()
    {
        var result = await LoadPasteAsync("1x Sol Ring (cmr) 123 [Ramp]\nSB: 1 Pyroblast");

        var solRing = Assert.Single(result.Entries, entry => entry.Name == "Sol Ring");
        Assert.Equal("cmr", solRing.SetCode);
        Assert.Equal("123", solRing.CollectorNumber);
        Assert.Single(result.Entries, entry => entry.Name == "Pyroblast" && entry.Board == "sideboard");
    }

    [Fact]
    public async Task LoadFromSourceAsync_TaggedPasteWithImplicitQuantity_PreservesBothCards()
    {
        var result = await LoadPasteAsync("1x Sol Ring (cmr) 123 [Ramp]\nArcane Signet");

        Assert.Single(result.Entries, entry => entry.Name == "Sol Ring");
        Assert.Single(result.Entries, entry => entry.Name == "Arcane Signet");
    }

    [Theory]
    [InlineData("^Have,#37d67a^")]
    [InlineData("^Have,#37d67a^ ^Foil,#ffaa00^")]
    public async Task LoadFromSourceAsync_PastedArchidektTags_PreservesCardName(string tags)
    {
        var result = await LoadPasteAsync($"1x Sol Ring (cmr) 123 [Ramp] {tags}");

        Assert.Equal("Sol Ring", Assert.Single(result.Entries).Name);
    }

    [Fact]
    public async Task LoadFromSourceAsync_PastedMoxfieldHashtags_PreservesNamesBoardsAndPrinting()
    {
        var result = await LoadPasteAsync("""
            1 Atraxa, Praetors' Voice #Commander
            1 Sol Ring (CMR) 123 #Ramp
            """);

        Assert.Equal(["Atraxa, Praetors' Voice", "Sol Ring"], result.Entries.Select(entry => entry.Name));
        Assert.Equal("commander", result.Entries[0].Board);
        Assert.Equal("mainboard", result.Entries[1].Board);
        Assert.Equal("CMR", result.Entries[1].SetCode);
        Assert.Equal("123", result.Entries[1].CollectorNumber);
    }

    [Theory]
    [InlineData("1 Sol Ring (CMR) 123\n// Removal [3]\nSB: 1 Swords to Plowshares")]
    [InlineData("1 Sol Ring\nSB: 1 Swords to Plowshares\n\nNotes:\nCombo line [see primer]")]
    public async Task LoadFromSourceAsync_BracketedNonCardLine_PreservesMoxfieldBoardAssignments(string deckSource)
    {
        var result = await LoadPasteAsync(deckSource);

        Assert.Single(result.Entries, entry => entry.Name == "Sol Ring" && entry.Board == "mainboard");
        Assert.Single(result.Entries, entry => entry.Name == "Swords to Plowshares" && entry.Board == "sideboard");
        Assert.Equal(2, result.Entries.Count);
    }

    [Fact]
    public async Task LoadFromSourceAsync_InvalidArchidektPasteWithPropagateParseException_PropagatesArchidektException()
    {
        var loader = CreateLoader();

        var exception = await Assert.ThrowsAsync<DeckParseException>(() => loader.LoadFromSourceAsync(
            "1x Sol Ring (cmr) 123 [Ramp]\n1x *F*",
            UnrecognizedPasteBehavior.PropagateParseException));

        Assert.Equal("Unable to parse Archidekt line 2: \"1x *F*\"", exception.Message);
    }

    [Fact]
    public async Task LoadFromSourceAsync_PastedGoldfishArenaText_ParsesEntries()
    {
        var loader = CreateLoader();

        var result = await loader.LoadFromSourceAsync("""
About
Name 8 Rack

Deck
4 Thoughtseize (2XM) 109
Sideboard
2 Duress
""");

        Assert.Collection(
            result.Entries,
            entry =>
            {
                Assert.Equal("Thoughtseize", entry.Name);
                Assert.Equal(4, entry.Quantity);
                Assert.Equal("mainboard", entry.Board);
            },
            entry =>
            {
                Assert.Equal("Duress", entry.Name);
                Assert.Equal(2, entry.Quantity);
                Assert.Equal("sideboard", entry.Board);
            });
        Assert.Null(result.FallbackNotice);
        Assert.Null(result.DetectedCompanionName);
    }

    [Theory]
    [InlineData("https://moxfield.com.evil.tld/decks/abc")]
    [InlineData("https://evilmoxfield.com/decks/abc")]
    [InlineData("https://moxfield.com@evil.tld/decks/abc")]
    public async Task LoadFromSourceAsync_SpoofedMoxfieldUrl_DoesNotRouteToMoxfieldImporter(string spoofUrl)
    {
        // Spoof URLs must never reach the Moxfield importer, regardless of whether the parser
        // cascade accepts or rejects the input. The null-capture assertion is the security proof:
        // if the importer were called (a future regression), LastImportWithSourceArgument would be set.
        var importer = new FakeMoxfieldDeckImporter(_ => []);
        var loader = CreateLoader(importer: importer);

        // The spoof URL falls through to the parser cascade (treated as pasted text, not a
        // recognised deck URL). The importer must never be invoked.
        await loader.LoadFromSourceAsync(spoofUrl);

        Assert.Null(importer.LastImportWithSourceArgument);
    }

    [Theory]
    [InlineData("https://archidekt.com.evil.tld/decks/abc")]
    [InlineData("https://evilarchidekt.com/decks/abc")]
    [InlineData("https://archidekt.com@evil.tld/decks/abc")]
    public async Task LoadFromSourceAsync_SpoofedArchidektUrl_DoesNotRouteToArchidektImporter(string spoofUrl)
    {
        // Spoof URLs must never reach the Archidekt importer. See spoof-Moxfield variant above.
        var archidektImporter = new FakeArchidektDeckImporter(_ => []);
        var loader = CreateLoader(archidektImporter: archidektImporter);

        await loader.LoadFromSourceAsync(spoofUrl);

        Assert.Null(archidektImporter.LastImportArgument);
    }

    [Fact]
    public async Task LoadFromSourceAsync_UnrecognizedTextWithThrowNotRecognized_ThrowsExactMessage()
    {
        var loader = CreateLoader();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadFromSourceAsync("*F*"));

        Assert.Equal("The submitted deck was not recognized as a Moxfield URL, Archidekt URL, or a Moxfield, Archidekt, or MTG Arena deck export.", exception.Message);
    }

    [Fact]
    public async Task LoadFromSourceAsync_UnrecognizedTextWithPropagateParseException_PropagatesDeckParseException()
    {
        var loader = CreateLoader();

        await Assert.ThrowsAsync<DeckParseException>(() => loader.LoadFromSourceAsync(
            "*F*",
            UnrecognizedPasteBehavior.PropagateParseException));
    }

    [Fact]
    public void ValidateCommanderDeckSize_SideboardEntriesAreExcluded()
    {
        var entries = new List<DeckEntry>
        {
            new() { Name = "Commander", NormalizedName = "commander", Quantity = 1, Board = "commander" },
            new() { Name = "Mainboard", NormalizedName = "mainboard", Quantity = 99, Board = "mainboard" },
            new() { Name = "Sideboard", NormalizedName = "sideboard", Quantity = 10, Board = "sideboard" }
        };
        var loader = CreateLoader();

        loader.ValidateCommanderDeckSize("Test", entries);
    }

    private static Task<DeckSourceLoadResult> LoadPasteAsync(string deckSource)
        => CreateLoader().LoadFromSourceAsync(deckSource);

    private static DeckEntryLoader CreateLoader(
        IMoxfieldDeckImporter? importer = null,
        IArchidektDeckImporter? archidektImporter = null)
        => new(
            importer ?? new FakeMoxfieldDeckImporter(_ => []),
            archidektImporter ?? new FakeArchidektDeckImporter(_ => []),
            new MoxfieldParser(),
            new ArchidektParser());

    private sealed class FakeMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        private readonly Func<string, List<DeckEntry>> _importAsync;
        private readonly Func<string, MoxfieldImportResult> _importWithSourceAsync;

        public FakeMoxfieldDeckImporter(
            Func<string, List<DeckEntry>> importAsync,
            Func<string, MoxfieldImportResult>? importWithSourceAsync = null)
        {
            _importAsync = importAsync;
            _importWithSourceAsync = importWithSourceAsync ?? (url => new MoxfieldImportResult(_importAsync(url), MoxfieldImportSource.Direct));
        }

        public string? LastImportArgument { get; private set; }

        public string? LastImportWithSourceArgument { get; private set; }

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            LastImportArgument = urlOrDeckId;
            return Task.FromResult(_importAsync(urlOrDeckId));
        }

        public Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            LastImportWithSourceArgument = urlOrDeckId;
            return Task.FromResult(_importWithSourceAsync(urlOrDeckId));
        }
    }

    private sealed class FakeArchidektDeckImporter : IArchidektDeckImporter
    {
        private readonly Func<string, List<DeckEntry>> _importAsync;

        public FakeArchidektDeckImporter(Func<string, List<DeckEntry>> importAsync)
        {
            _importAsync = importAsync;
        }

        public string? LastImportArgument { get; private set; }

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            LastImportArgument = urlOrDeckId;
            return Task.FromResult(_importAsync(urlOrDeckId));
        }
    }
}
