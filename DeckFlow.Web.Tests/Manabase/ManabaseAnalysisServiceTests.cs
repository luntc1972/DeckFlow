using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Validates <see cref="ManabaseAnalysisService"/>: board filtering, printing-preferred
/// resolution (alternate names), unresolved handling, and report production — all with
/// faked deck loading and Scryfall HTTP.
/// </summary>
public sealed class ManabaseAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_ProducesReport_FiltersSideboard_ResolvesByPrinting()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Tymna the Weaver", 1, "commander", set: "cmr", cn: "1"),
            Land("Plains", 12),
            Land("Island", 10),
            Entry("Swords to Plowshares", 1, "mainboard"),
            // Alternate (flavor) name; resolves only via its printing.
            Entry("Godzilla, King of the Monsters", 1, "mainboard", set: "iko", cn: "275"),
            // Sideboard card must be excluded from the analysis.
            Entry("Black Lotus", 1, "sideboard"),
        };

        var cards = new List<ScryfallCard>
        {
            BasicLand("Plains", "W"),
            BasicLand("Island", "U"),
            Spell("Tymna the Weaver", "{1}{W}", 2, "Legendary Creature — Human Cleric"),
            Spell("Swords to Plowshares", "{W}", 1, "Instant"),
            // Canonical name differs from the deck entry; matched by set+collector.
            Spell("Zilortha, Strength Incarnate", "{2}{R}{R}", 4, "Legendary Creature — Dinosaur",
                set: "iko", cn: "275"),
        };

        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var result = await service.AnalyzeAsync(
            "https://archidekt.com/decks/1", "Test Deck", options: null, CancellationToken.None);

        Assert.NotNull(result.Report);
        Assert.Equal(22, result.Report.ActualLands); // 12 Plains + 10 Island; sideboard excluded.
        Assert.Empty(result.Unresolved); // Godzilla resolved via printing.
        Assert.Contains("Test Deck", result.ChatGptSwapPrompt);
        Assert.NotEmpty(result.Report.ColorFindings);
        // Default profile is Casual so existing output is unchanged.
        Assert.Equal(ManabaseMode.Casual, result.Report.Mode);
    }

    [Fact]
    public async Task AnalyzeAsync_DefaultMode_IsCasual()
    {
        var (entries, cards) = CurveFixture();
        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var result = await service.AnalyzeAsync("paste", null);

        Assert.Equal(ManabaseMode.Casual, result.Report.Mode);
    }

    [Fact]
    public async Task AnalyzeAsync_CedhMode_LowersTargetLands_AndEchoesMode()
    {
        var (entries, cards) = CurveFixture();

        var casual = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));
        var cedh = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var casualResult = await casual.AnalyzeAsync(
            "paste", null, new ManabaseAnalysisOptions { Mode = ManabaseMode.Casual });
        var cedhResult = await cedh.AnalyzeAsync(
            "paste", null, new ManabaseAnalysisOptions { Mode = ManabaseMode.Cedh });

        Assert.Equal(ManabaseMode.Cedh, cedhResult.Report.Mode);
        Assert.True(
            cedhResult.Report.TargetLands < casualResult.Report.TargetLands,
            $"cEDH target {cedhResult.Report.TargetLands} should be below casual {casualResult.Report.TargetLands}");
    }

    // A full ~99-card singleton fixture (so the Karsten regression target sits well above the
    // cEDH floor of 28 and the two modes genuinely differ). 36 lands + 63 distinct spells across
    // a normal curve gives a casual target around the mid-30s; cEDH cuts ~3.5 off it.
    private static (List<DeckEntry> Entries, List<ScryfallCard> Cards) CurveFixture()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Tymna the Weaver", 1, "commander", set: "cmr", cn: "1"),
            Land("Plains", 18),
            Land("Island", 18),
        };
        var cards = new List<ScryfallCard>
        {
            BasicLand("Plains", "W"),
            BasicLand("Island", "U"),
            Spell("Tymna the Weaver", "{1}{W}", 2, "Legendary Creature — Human Cleric"),
        };

        // 63 single-copy spells on a mid curve (avg MV ~3) so the regression is realistic.
        for (int i = 0; i < 63; i++)
        {
            int mv = 2 + (i % 4); // 2,3,4,5 repeating
            string name = $"Filler Spell {i}";
            entries.Add(Entry(name, 1, "mainboard"));
            cards.Add(Spell(name, $"{{{mv - 1}}}{{U}}", mv, "Sorcery"));
        }

        return (entries, cards);
    }

    [Fact]
    public async Task AnalyzeAsync_CommanderImportance_ThreadsThroughToTheReport()
    {
        // The service must forward options.CommanderImportance to the analyzer. A WU commander on a
        // blue-thin base diverges: Central tightens the commander's blue bar (more under-supported)
        // versus Low. Same deck, only the importance knob differs.
        var (entries, cards) = StrainedCommanderFixture();

        var central = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));
        var low = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var centralResult = await central.AnalyzeAsync(
            "paste", null, new ManabaseAnalysisOptions { CommanderImportance = CommanderImportance.Central });
        var lowResult = await low.AnalyzeAsync(
            "paste", null, new ManabaseAnalysisOptions { CommanderImportance = CommanderImportance.Low });

        // Land target is importance-orthogonal — identical regardless of the knob.
        Assert.Equal(centralResult.Report.TargetLands, lowResult.Report.TargetLands);

        var centralBlue = centralResult.Report.ColorFindings.FirstOrDefault(f => f.Color == ManaColor.Blue);
        var lowBlue = lowResult.Report.ColorFindings.FirstOrDefault(f => f.Color == ManaColor.Blue);
        Assert.NotNull(centralBlue);
        Assert.NotNull(lowBlue);
        Assert.True(centralBlue!.UnderSupportedCount >= lowBlue!.UnderSupportedCount,
            "Central must hold the commander's blue to at least as strict a bar as Low");
    }

    // A WU commander with thin blue support so Central vs Low diverges on the blue finding.
    private static (List<DeckEntry> Entries, List<ScryfallCard> Cards) StrainedCommanderFixture()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Brago, King Eternal", 1, "commander"),
            Land("Plains", 24),
            Land("Island", 9),
            Spell("Blue Spell", "{2}{U}", 3, "Sorcery").ToEntry(),
            Spell("White Spell", "{1}{W}", 2, "Sorcery").ToEntry(),
        };
        var cards = new List<ScryfallCard>
        {
            BasicLand("Plains", "W"),
            BasicLand("Island", "U"),
            Spell("Brago, King Eternal", "{2}{W}{U}", 4, "Legendary Creature — Spirit Noble"),
            Spell("Blue Spell", "{2}{U}", 3, "Sorcery"),
            Spell("White Spell", "{1}{W}", 2, "Sorcery"),
        };

        return (entries, cards);
    }

    [Fact]
    public async Task AnalyzeAsync_UnresolvedCard_ListedNotThrown()
    {
        var entries = new List<DeckEntry>
        {
            Land("Plains", 1),
            Spell("Swords to Plowshares", "{W}", 1, "Instant").ToEntry(),
            Entry("Totally Made Up Card", 1, "mainboard"),
        };
        var cards = new List<ScryfallCard>
        {
            BasicLand("Plains", "W"),
            Spell("Swords to Plowshares", "{W}", 1, "Instant"),
        };

        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var result = await service.AnalyzeAsync("paste", null);

        Assert.Contains("Totally Made Up Card", result.Unresolved);
        Assert.NotNull(result.Report);
    }

    [Fact]
    public async Task AnalyzeAsync_BlankSource_Throws()
    {
        var service = new ManabaseAnalysisService(new FakeLoader(new List<DeckEntry>()), new FakeResolver(new List<ScryfallCard>()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeAsync("   ", null));
    }

    [Fact]
    public async Task AnalyzeAsync_OnlySideboard_Throws()
    {
        var entries = new List<DeckEntry> { Entry("Black Lotus", 1, "sideboard") };
        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(new List<ScryfallCard>()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeAsync("paste", null));
    }

    [Fact]
    public async Task AnalyzeAsync_OversizeDeckSource_Throws()
    {
        var service = new ManabaseAnalysisService(new FakeLoader(new List<DeckEntry>()), new FakeResolver(new List<ScryfallCard>()));
        string huge = new string('x', 100_001);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeAsync(huge, null));
    }

    [Fact]
    public async Task AnalyzeAsync_TooManyCards_Throws()
    {
        var entries = Enumerable.Range(0, 501)
            .Select(i => Entry($"Card {i}", 1, "mainboard"))
            .ToList();
        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(new List<ScryfallCard>()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeAsync("paste", null));
    }

    // --- helpers -------------------------------------------------------------

    private static DeckEntry Entry(string name, int qty, string board, string? set = null, string? cn = null) => new()
    {
        Name = name,
        NormalizedName = name.ToLowerInvariant(),
        Quantity = qty,
        Board = board,
        SetCode = set,
        CollectorNumber = cn,
    };

    private static DeckEntry Land(string name, int qty) => Entry(name, qty, "mainboard");

    private static ScryfallCard BasicLand(string name, string color) => new(
        Name: name, ManaCost: null, TypeLine: $"Basic Land — {name}", OracleText: null,
        Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
        SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
        Layout: "normal", Cmc: 0, ProducedMana: new[] { color }, Rarity: "common");

    private static ScryfallCard Spell(string name, string manaCost, double cmc, string typeLine, string? set = null, string? cn = null) => new(
        Name: name, ManaCost: manaCost, TypeLine: typeLine, OracleText: "...",
        Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
        SetCode: set, SetName: null, CollectorNumber: cn, CardFaces: null, Id: null,
        Layout: "normal", Cmc: cmc, ProducedMana: null, Rarity: "rare");

    private sealed class FakeLoader : IDeckEntryLoader
    {
        private readonly List<DeckEntry> _entries;

        public FakeLoader(List<DeckEntry> entries) => _entries = entries;

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeckSourceLoadResult(_entries, null));

        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100)
        {
        }
    }

    private sealed class FakeResolver : IScryfallCardResolver
    {
        private readonly List<ScryfallCard> _cards;

        public FakeResolver(List<ScryfallCard> cards) => _cards = cards;

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(_cards, null),
            });

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult<ScryfallCard?>(null);

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult<ScryfallCard?>(null);
    }
}

internal static class ManabaseTestExtensions
{
    // Build a mainboard entry whose name matches a spell card, for terse arrange blocks.
    public static DeckEntry ToEntry(this ScryfallCard card) => new()
    {
        Name = card.Name,
        NormalizedName = card.Name.ToLowerInvariant(),
        Quantity = 1,
        Board = "mainboard",
    };
}
