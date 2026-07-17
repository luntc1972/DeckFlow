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
/// Verifies the flag-gated community-baseline result block wiring on <see cref="ManabaseAnalysisService"/>.
/// </summary>
public sealed class ManabaseCommunityBaselineWiringTests
{
    [Fact]
    public async Task AnalyzeAsync_BaselineFlagOff_LeavesCommunityBaselineNull()
    {
        var (entries, cards) = Fixture();
        var service = new ManabaseAnalysisService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [ManabaseAnalysisService.BaselineFlagKey] = false,
            }),
            manabaseBaseline: new FakeManabaseBaselineProvider(
                new Dictionary<int, ManabaseBracketBaseline>
                {
                    [2] = new() { Bracket = 2, AvgLands = 35.9, DeckCount = 124221, Source = "edhrec-pilot-aggregate" },
                }));

        var result = await service.AnalyzeAsync("paste", "Baseline Deck");

        Assert.NotNull(result.Report);
        Assert.Null(result.CommunityBaseline);
    }

    [Fact]
    public async Task AnalyzeAsync_BaselineFlagOn_CedhMode_UsesBracketFiveFallback()
    {
        var result = await AnalyzeAsync(
            ManabaseMode.Cedh,
            new Dictionary<int, ManabaseBracketBaseline>
            {
                [5] = new() { Bracket = 5, AvgLands = 30.5, DeckCount = 4761, Source = "edhrec-pilot-aggregate" },
            });

        Assert.NotNull(result.Report);
        Assert.NotNull(result.CommunityBaseline);
        Assert.Equal(5, result.CommunityBaseline!.Bracket);
        Assert.Equal(30.5, result.CommunityBaseline.AvgLands, 3);
        Assert.Equal(ManabaseBracketSource.Fallback, result.CommunityBaseline.BracketSource);
    }

    [Fact]
    public async Task AnalyzeAsync_BaselineFlagOn_CasualMode_UsesBracketTwoFallback()
    {
        var result = await AnalyzeAsync(
            ManabaseMode.Casual,
            new Dictionary<int, ManabaseBracketBaseline>
            {
                [2] = new() { Bracket = 2, AvgLands = 35.9, DeckCount = 124221, Source = "edhrec-pilot-aggregate" },
            });

        Assert.NotNull(result.Report);
        Assert.NotNull(result.CommunityBaseline);
        Assert.Equal(2, result.CommunityBaseline!.Bracket);
        Assert.Equal(35.9, result.CommunityBaseline.AvgLands, 3);
    }

    [Fact]
    public async Task AnalyzeAsync_BaselineFlagOn_ProviderMiss_ReturnsNullCommunityBaseline()
    {
        var result = await AnalyzeAsync(ManabaseMode.Cedh, new Dictionary<int, ManabaseBracketBaseline>());

        Assert.NotNull(result.Report);
        Assert.Null(result.CommunityBaseline);
    }

    [Fact]
    public async Task AnalyzeAsync_BaselineFlagOn_NullProvider_ReturnsNullCommunityBaseline()
    {
        var (entries, cards) = Fixture();
        var service = new ManabaseAnalysisService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [ManabaseAnalysisService.BaselineFlagKey] = true,
            }));

        var result = await service.AnalyzeAsync("paste", "Baseline Deck", new ManabaseAnalysisOptions { Mode = ManabaseMode.Cedh });

        Assert.NotNull(result.Report);
        Assert.Null(result.CommunityBaseline);
    }

    private static async Task<ManabaseAnalysisResult> AnalyzeAsync(
        ManabaseMode mode,
        IReadOnlyDictionary<int, ManabaseBracketBaseline> rows)
    {
        var (entries, cards) = Fixture();
        var service = new ManabaseAnalysisService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [ManabaseAnalysisService.BaselineFlagKey] = true,
            }),
            manabaseBaseline: new FakeManabaseBaselineProvider(rows));

        return await service.AnalyzeAsync("paste", "Baseline Deck", new ManabaseAnalysisOptions { Mode = mode });
    }

    private static (List<DeckEntry> Entries, List<ScryfallCard> Cards) Fixture()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Kinnan, Bonder Prodigy", 1, "commander"),
            Land("Forest", 18),
            Land("Island", 18),
            Entry("Arcane Signet", 1, "mainboard"),
            Entry("Cultivate", 1, "mainboard"),
        };
        for (int i = 0; i < 20; i++)
        {
            entries.Add(Entry($"Filler Spell {i}", 1, "mainboard"));
        }

        var cards = new List<ScryfallCard>
        {
            BasicLand("Forest", "G"),
            BasicLand("Island", "U"),
            Spell("Kinnan, Bonder Prodigy", "{G}{U}", 2, "Legendary Creature — Human Druid"),
            Spell("Arcane Signet", "{2}", 2, "Artifact"),
            Spell("Cultivate", "{2}{G}", 3, "Sorcery"),
        };
        for (int i = 0; i < 20; i++)
        {
            cards.Add(Spell($"Filler Spell {i}", "{2}", 3, "Sorcery"));
        }

        return (entries, cards);
    }

    private static DeckEntry Entry(string name, int qty, string board) => new()
    {
        Name = name,
        NormalizedName = name.ToLowerInvariant(),
        Quantity = qty,
        Board = board,
    };

    private static DeckEntry Land(string name, int qty) => Entry(name, qty, "mainboard");

    private static ScryfallCard BasicLand(string name, string color) => new(
        Name: name, ManaCost: null, TypeLine: $"Basic Land — {name}", OracleText: null,
        Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
        SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
        Layout: "normal", Cmc: 0, ProducedMana: new[] { color }, Rarity: "common");

    private static ScryfallCard Spell(string name, string manaCost, double cmc, string typeLine) => new(
        Name: name, ManaCost: manaCost, TypeLine: typeLine, OracleText: "...",
        Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
        SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
        Layout: "normal", Cmc: cmc, ProducedMana: null, Rarity: "rare");

    private sealed class FakeLoader : IDeckEntryLoader
    {
        private readonly List<DeckEntry> _entries;

        public FakeLoader(List<DeckEntry> entries)
        {
            _entries = entries;
        }

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeckSourceLoadResult(_entries, null, null));

        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default)
            => throw new System.NotSupportedException();

        public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100)
        {
        }
    }

    private sealed class FakeResolver : IScryfallCardResolver
    {
        private readonly List<ScryfallCard> _cards;

        public FakeResolver(List<ScryfallCard> cards)
        {
            _cards = cards;
        }

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(_cards, null),
            });

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult(_cards.FirstOrDefault(card => string.Equals(card.Name, cardName, System.StringComparison.OrdinalIgnoreCase)));

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult<ScryfallCard?>(null);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult(_cards.FirstOrDefault(card => string.Equals(card.Name, cardName, System.StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class FakeManabaseBaselineProvider : IManabaseBaselineProvider
    {
        private readonly IReadOnlyDictionary<int, ManabaseBracketBaseline> _rows;

        public FakeManabaseBaselineProvider(IReadOnlyDictionary<int, ManabaseBracketBaseline> rows)
        {
            _rows = rows;
        }

        public void EnsureLoaded()
        {
        }

        public ManabaseBracketBaseline? TryGetBracketBaseline(int bracket)
            => _rows.TryGetValue(bracket, out ManabaseBracketBaseline? row) ? row : null;
    }
}
