using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Harvest;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for Cut Lab original-entry state capture, serialization, and reload survival.</summary>
public sealed class CutLabOriginalEntriesTests
{
    [Fact]
    public void SerializeDeserialize_RoundTripsOriginalEntriesWithFullFidelity()
    {
        var state = new CutLabState
        {
            Commander = "Atraxa, Praetors' Voice",
            OriginalEntries =
            [
                new CutLabOriginalEntry
                {
                    Name = "Atraxa, Praetors' Voice",
                    Quantity = 1,
                    Board = "commander",
                    SetCode = "2xm",
                    CollectorNumber = "190",
                    Category = "Commanders",
                },
                new CutLabOriginalEntry
                {
                    Name = "Arcane Signet",
                    Quantity = 1,
                    Board = "sideboard",
                    SetCode = "cmm",
                    CollectorNumber = "948",
                    Category = "Ramp",
                },
            ],
        };

        var json = CutLabStateSerializer.Serialize(state);
        var roundTripped = CutLabStateSerializer.Deserialize(json);

        Assert.Equal(state.OriginalEntries, roundTripped.OriginalEntries);
    }

    [Fact]
    public void Deserialize_Pre105JsonWithoutOriginalEntries_ReturnsEmptyList()
    {
        const string json =
            """
            {
              "commander": "Atraxa, Praetors' Voice",
              "pool": [],
              "packages": [],
              "decisions": [],
              "roleFloors": [],
              "goals": {
                "commanderByTurn": 3,
                "engineByTurn": 2,
                "representativeLineByTurn": 4
              },
              "intent": {
                "primaryPlan": "Counters",
                "secondaryPlan": null,
                "bracket": 3,
                "playExperience": "Focused"
              }
            }
            """;

        var state = CutLabStateSerializer.Deserialize(json);

        Assert.Empty(state.OriginalEntries);
    }

    [Fact]
    public void Deserialize_OriginalEntriesOverMax_TruncatesToTwoHundredEntries()
    {
        string entriesJson = string.Join(
            ",",
            Enumerable.Range(1, 205).Select(index =>
                $$"""{"name":"Card {{index:000}}","quantity":1,"board":"mainboard","setCode":"set","collectorNumber":"{{index}}","category":"Value"}"""));
        string json =
            $$"""
            {
              "commander": "Atraxa, Praetors' Voice",
              "pool": [],
              "packages": [],
              "decisions": [],
              "originalEntries": [
                {{entriesJson}}
              ],
              "roleFloors": [],
              "intent": {
                "primaryPlan": "Counters",
                "secondaryPlan": null,
                "bracket": 3,
                "playExperience": "Focused"
              }
            }
            """;

        var state = CutLabStateSerializer.Deserialize(json);

        Assert.Equal(200, state.OriginalEntries.Count);
        Assert.Equal("Card 001", state.OriginalEntries[0].Name);
        Assert.Equal("Card 200", state.OriginalEntries[^1].Name);
    }

    [Fact]
    public async Task ProcessAsync_FirstIntake_CapturesOriginalEntriesFromAnalyzedEntries()
    {
        List<DeckEntry> entries =
        [
            Entry("Atraxa, Praetors' Voice", "commander", set: "2xm", collectorNumber: "190", category: "Commanders"),
            Entry("Mainboard Card", "mainboard", set: "bro", collectorNumber: "12", category: "Ramp"),
            Entry("Sideboard Card", "sideboard", set: "cmm", collectorNumber: "33", category: "Maybeboard"),
            Entry("Maybe Card", "maybeboard", set: "mh3", collectorNumber: "77", category: "Maybe"),
        ];
        entries.AddRange(BuildMainboard(start: 1, count: 100));
        var cards = BuildResolvedCards(entries);
        var service = CreateService(entries, cards);

        var result = await service.ProcessAsync(new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            IncludeSideboard = true,
            IncludeMaybeboard = true,
        });

        Assert.True(result.HasResult);
        Assert.Contains(result.State!.OriginalEntries, entry =>
            entry.Name == "Mainboard Card" &&
            entry.Board == "mainboard" &&
            entry.SetCode == "bro" &&
            entry.CollectorNumber == "12" &&
            entry.Category == "Ramp");
        Assert.Contains(result.State.OriginalEntries, entry =>
            entry.Name == "Sideboard Card" &&
            entry.Board == "sideboard" &&
            entry.SetCode == "cmm" &&
            entry.CollectorNumber == "33" &&
            entry.Category == "Maybeboard");
        Assert.Contains(result.State.OriginalEntries, entry =>
            entry.Name == "Maybe Card" &&
            entry.Board == "maybeboard" &&
            entry.SetCode == "mh3" &&
            entry.CollectorNumber == "77" &&
            entry.Category == "Maybe");
    }

    [Fact]
    public async Task ProcessAsync_DecisionRoundTrip_DoesNotOverwriteCapturedOriginalEntries()
    {
        List<DeckEntry> entries =
        [
            Entry("Atraxa, Praetors' Voice", "commander", set: "2xm", collectorNumber: "190", category: "Commanders"),
            Entry("Arcane Signet", "mainboard", set: "bro", collectorNumber: "12", category: "Ramp"),
        ];
        entries.AddRange(BuildMainboard(start: 1, count: 100));
        var cards = BuildResolvedCards(entries);
        var service = CreateService(entries, cards);
        var request = new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            PlayExperience = "Focused",
        };

        CutLabProcessResult intake = await service.ProcessAsync(request);
        CutLabOriginalEntry capturedEntry = Assert.Single(intake.State!.OriginalEntries, entry => entry.Name == "Arcane Signet");
        CutLabState carriedState = CutLabStateSerializer.Deserialize(intake.SerializedStateJson);
        carriedState = CutLabDecisionApplier.Apply(carriedState, "Arcane Signet", CutLabDecideAction.Accept, CutLabCutRoundEngine.Round1Key);
        request.CutLabStateJson = CutLabStateSerializer.Serialize(carriedState);

        CutLabProcessResult afterDecision = await service.ProcessAsync(request);
        CutLabOriginalEntry roundTrippedEntry = Assert.Single(afterDecision.State!.OriginalEntries, entry => entry.Name == "Arcane Signet");

        Assert.Equal(capturedEntry, roundTrippedEntry);
    }

    [Fact]
    public async Task ProcessAsync_SavedScenarioReload_PreservesExistingOriginalEntries()
    {
        List<DeckEntry> entries =
        [
            Entry("Atraxa, Praetors' Voice", "commander", set: "2xm", collectorNumber: "190", category: "Commanders"),
        ];
        entries.AddRange(BuildMainboard(start: 1, count: 101));
        var cards = BuildResolvedCards(entries);
        var priorState = new CutLabState
        {
            Commander = "Atraxa, Praetors' Voice",
            OriginalEntries =
            [
                new CutLabOriginalEntry
                {
                    Name = "Captured Baseline Card",
                    Quantity = 2,
                    Board = "sideboard",
                    SetCode = "who",
                    CollectorNumber = "42",
                    Category = "Snapshot",
                },
            ],
        };
        var service = CreateService(entries, cards);

        var result = await service.ProcessAsync(new CutLabRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "pool",
            CutLabStateJson = CutLabStateSerializer.Serialize(priorState),
        });

        Assert.True(result.HasResult);
        Assert.Equal(priorState.OriginalEntries, result.State!.OriginalEntries);
        Assert.Equal(priorState.OriginalEntries, CutLabStateSerializer.Deserialize(result.SerializedStateJson).OriginalEntries);
    }

    private static CutLabPageService CreateService(
        List<DeckEntry> entries,
        IReadOnlyList<ScryfallCard> cards,
        IScryfallCardResolver? resolver = null,
        CutLabResolvedCardCache? cache = null)
    {
        resolver ??= new FakeResolver(cards);
        cache ??= new CutLabResolvedCardCache();
        var simulationService = new CutLabSimulationService(
            cache,
            new CutLabDeltaCache(),
            resolver,
            NullLogger<CutLabSimulationService>.Instance);
        var analysisBuilder = new CutLabAnalysisContextBuilder(
            resolver,
            cache,
            new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()),
            new FakeSpellbookService(),
            new FakeCategoryKnowledgeStore());
        return new CutLabPageService(
            new FakeLoader(entries),
            resolver,
            new FakeBanListService([]),
            new FakeManabaseBaselineProvider(),
            new FakeCedhLandBaselineProvider(),
            analysisContextBuilder: analysisBuilder,
            simulationService: simulationService);
    }

    private static List<DeckEntry> BuildMainboard(int start, int count) =>
        Enumerable.Range(start, count)
            .Select(index => Entry($"Card {index:000}", "mainboard"))
            .ToList();

    private static List<ScryfallCard> BuildResolvedCards(IEnumerable<DeckEntry> entries) =>
        entries
            .Select(entry => entry.Board.Equals("commander", StringComparison.OrdinalIgnoreCase)
                ? Spell(entry.Name, "Legendary Creature — Phyrexian Angel Horror")
                : Spell(entry.Name, entry.Name.Contains("Land", StringComparison.OrdinalIgnoreCase) ? "Land" : "Artifact", set: entry.SetCode, collectorNumber: entry.CollectorNumber))
            .ToList();

    private static DeckEntry Entry(
        string name,
        string board,
        string? set = null,
        string? collectorNumber = null,
        string? category = null) =>
        new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = 1,
            Board = board,
            SetCode = set,
            CollectorNumber = collectorNumber,
            Category = category,
        };

    private static ScryfallCard Spell(
        string name,
        string typeLine,
        string? manaCost = "{2}",
        string? oracleText = "",
        string? power = null,
        double cmc = 2,
        string? set = null,
        string? collectorNumber = null) =>
        new(
            name,
            manaCost,
            typeLine,
            oracleText,
            power,
            null,
            null,
            null,
            set,
            null,
            collectorNumber,
            Cmc: cmc);

    private sealed class FakeLoader(List<DeckEntry> entries) : IDeckEntryLoader
    {
        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DeckSourceLoadResult(entries, null));

        public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100)
            => throw new NotSupportedException();
    }

    private sealed class FakeResolver(IReadOnlyList<ScryfallCard> cards) : IScryfallCardResolver
    {
        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
        {
            var matches = cards.ToList();
            return Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(matches, []),
            });
        }

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult(cards.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase)));

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);
    }

    private sealed class FakeBanListService(IReadOnlyList<string> bannedCards) : ICommanderBanListService
    {
        public Task<IReadOnlyList<string>> GetBannedCardsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(bannedCards);
    }

    private sealed class FakeSpellbookService : ICommanderSpellbookService
    {
        public Task<CommanderSpellbookResult?> FindCombosAsync(
            IReadOnlyList<DeckEntry> entries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CommanderSpellbookResult?>(new CommanderSpellbookResult([], []));
    }

    private sealed class FakeManabaseBaselineProvider : IManabaseBaselineProvider
    {
        public void EnsureLoaded()
        {
        }

        public ManabaseBracketBaseline? TryGetBracketBaseline(int bracket)
            => null;

        public ManabaseCommanderBaseline? TryGetCommanderBaseline(IReadOnlyList<string> commanderNames)
            => null;
    }

    private sealed class FakeCedhLandBaselineProvider : ICedhLandBaselineProvider
    {
        public void EnsureLoaded()
        {
        }

        public bool TryGetBaseline(IReadOnlyList<string> commanderNames, out double mean, out int n, out double sd, out string? generated)
        {
            mean = default;
            n = default;
            sd = default;
            generated = default;
            return false;
        }
    }
}
