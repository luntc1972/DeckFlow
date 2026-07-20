using System.Net;
using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabSimulationService"/> covering SIM-01 projection, deltas, and caching.</summary>
public sealed class CutLabSimulationServiceTests
{
    [Fact]
    public async Task BuildSnapshot_ProjectsKeepableHandFromUnderlyingReport()
    {
        TestPool pool = BuildCedhPool();
        var resolver = new FakeResolver(pool.Cards);
        var service = CreateService(resolver);

        CutLabMetricSnapshot snapshot = await service.BuildSnapshot(pool.WorkingList, "cEDH");
        ManabaseReport report = BuildDirectReport(pool.WorkingList, "cEDH", trialsOverride: 4000, pool.Cards);

        CutLabMetricValue keepable = Assert.Single(snapshot.Metrics, metric => metric.Kind == CutLabMetricKind.KeepableHand);
        Assert.Equal(report.MulliganEvaluation!.KeepableHandPercent, keepable.Value);
    }

    [Fact]
    public async Task BuildSnapshot_ProducesAllSevenFamiliesAndSeparateFloodScrewCurveMetrics()
    {
        TestPool pool = BuildCedhPool();
        var service = CreateService(new FakeResolver(pool.Cards));

        CutLabMetricSnapshot snapshot = await service.BuildSnapshot(pool.WorkingList, "cEDH");

        Assert.Equal(7, snapshot.Metrics.Select(metric => metric.Family).Distinct().Count());
        Assert.Contains(snapshot.Metrics, metric => metric.Kind == CutLabMetricKind.Flood);
        Assert.Contains(snapshot.Metrics, metric => metric.Kind == CutLabMetricKind.Screw);
        Assert.Contains(snapshot.Metrics, metric => metric.Kind == CutLabMetricKind.Curve);
    }

    [Fact]
    public async Task BuildSnapshot_CasualModeMarksEarlyInteractionNotApplicable()
    {
        TestPool pool = BuildCasualPool();
        var service = CreateService(new FakeResolver(pool.Cards));

        CutLabMetricSnapshot snapshot = await service.BuildSnapshot(pool.WorkingList, "Casual");

        CutLabMetricValue earlyInteraction = Assert.Single(snapshot.Metrics, metric => metric.Kind == CutLabMetricKind.EarlyInteraction);
        Assert.True(double.IsNaN(earlyInteraction.Value));
    }

    [Fact]
    public async Task ComputeProposalDeltas_UsesNoiseFloorAgainstCurrentWorkingList()
    {
        TestPool pool = BuildCedhPool();
        var service = CreateService(new FakeResolver(pool.Cards));

        CutLabProposalDeltas quiet = await service.ComputeProposalDeltas(pool.WorkingList, "Filler 01", "cEDH");
        CutLabProposalDeltas loud = await service.ComputeProposalDeltas(pool.WorkingList, "Island", "cEDH");
        CutLabMetricSnapshot current = await service.BuildSnapshot(pool.WorkingList, "cEDH");

        CutLabMetricDelta quietKeepable = Assert.Single(quiet.Deltas, delta => delta.Kind == CutLabMetricKind.KeepableHand);
        Assert.Equal(CutLabMetricDirection.None, quietKeepable.Direction);
        Assert.False(quietKeepable.IsMeaningful);

        Assert.Contains(loud.Deltas, delta => delta.Direction is CutLabMetricDirection.Up or CutLabMetricDirection.Down && delta.IsMeaningful);
        CutLabMetricDelta loudKeepable = Assert.Single(loud.Deltas, delta => delta.Kind == CutLabMetricKind.KeepableHand);
        Assert.Equal(
            Assert.Single(current.Metrics, metric => metric.Kind == CutLabMetricKind.KeepableHand).Value,
            loudKeepable.Before);
    }

    [Fact]
    public async Task ComputeProposalDeltas_ReusesDeltaCacheBeforeAnyRecomputation()
    {
        TestPool pool = BuildCedhPool();
        var sharedDeltaCache = new CutLabDeltaCache();
        var warmResolver = new FakeResolver(pool.Cards);
        var warmService = new CutLabSimulationService(
            new CutLabResolvedCardCache(),
            sharedDeltaCache,
            warmResolver,
            NullLogger<CutLabSimulationService>.Instance);

        CutLabProposalDeltas first = await warmService.ComputeProposalDeltas(pool.WorkingList, "Utility Land", "cEDH");

        var coldResolver = new FakeResolver(pool.Cards);
        var coldService = new CutLabSimulationService(
            new CutLabResolvedCardCache(),
            sharedDeltaCache,
            coldResolver,
            NullLogger<CutLabSimulationService>.Instance);

        CutLabProposalDeltas second = await coldService.ComputeProposalDeltas(pool.WorkingList, "Utility Land", "cEDH");

        Assert.Equal(0, coldResolver.ResolveSingleCalls);
        Assert.Equal(first.Deltas, second.Deltas);
    }

    [Fact]
    public async Task BuildSnapshot_IsDeterministicForSameWorkingList()
    {
        TestPool pool = BuildCedhPool();
        var service = CreateService(new FakeResolver(pool.Cards));

        CutLabMetricSnapshot first = await service.BuildSnapshot(pool.WorkingList, "cEDH");
        CutLabMetricSnapshot second = await service.BuildSnapshot(pool.WorkingList, "cEDH");

        Assert.Equal(first.Metrics, second.Metrics);
    }

    [Fact]
    public async Task BuildSnapshot_UsesNeutralLabelsOnly()
    {
        TestPool pool = BuildCedhPool();
        var service = CreateService(new FakeResolver(pool.Cards));

        CutLabMetricSnapshot snapshot = await service.BuildSnapshot(pool.WorkingList, "cEDH");

        Assert.DoesNotContain(
            snapshot.Metrics,
            metric => metric.Label.Contains("better", StringComparison.OrdinalIgnoreCase)
                || metric.Label.Contains("worse", StringComparison.OrdinalIgnoreCase)
                || metric.Label.Contains("bad", StringComparison.OrdinalIgnoreCase));
    }

    private static CutLabSimulationService CreateService(FakeResolver resolver)
        => new(
            new CutLabResolvedCardCache(),
            new CutLabDeltaCache(),
            resolver,
            NullLogger<CutLabSimulationService>.Instance);

    private static ManabaseReport BuildDirectReport(
        IReadOnlyList<CutLabPoolCard> workingList,
        string playExperience,
        int? trialsOverride,
        IReadOnlyList<ScryfallCard> cards)
    {
        var cardLookup = cards.ToDictionary(card => card.Name, StringComparer.OrdinalIgnoreCase);
        DeckCardEntry[] deckEntries = workingList
            .Select(card => new DeckCardEntry
            {
                Card = ScryfallCardDataMapper.ToCardData(cardLookup[card.Name]),
                Quantity = card.Quantity,
                IsCommander = card.IsCommander,
            })
            .ToArray();

        IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(deckEntries);
        ManabaseMode mode = CutLabRoleAssigner.ResolveMode(playExperience);
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        deck = deck with
        {
            Spells = deck.Spells
                .Select(spell =>
                {
                    CardFact fact = facts.Single(entry => string.Equals(entry.Name, spell.Name, StringComparison.OrdinalIgnoreCase));
                    PlanRole roles = PlanRoleClassifier.Classify(fact, [], false, mode, out bool interactionMeritPreGate);
                    return spell with { PlanRoles = roles, IsInteractionSpell = interactionMeritPreGate };
                })
                .ToArray(),
        };

        return ManabaseAnalyzer.Analyze(
            deck,
            mode,
            gateRampOnCastable: true,
            keepShapes: true,
            interactionLens: mode == ManabaseMode.Cedh,
            trialsOverride: trialsOverride);
    }

    private static TestPool BuildCedhPool()
    {
        List<CutLabPoolCard> workingList =
        [
            PoolCard("Cut Lab Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Utility Land", "Land"),
            PoolCard("Island", "Basic Land — Island", quantity: 28),
            PoolCard("Swamp", "Basic Land — Swamp", quantity: 6),
            PoolCard("Fast Interaction", "Instant"),
            PoolCard("Value Engine", "Enchantment"),
            PoolCard("Combo Tutor", "Sorcery"),
            PoolCard("Closing Threat", "Creature — Leviathan"),
        ];

        workingList.AddRange(Enumerable.Range(1, 62).Select(index => PoolCard($"Filler {index:00}", "Artifact")));

        List<ScryfallCard> cards =
        [
            Spell("Cut Lab Commander", "Legendary Creature — Human Wizard", manaCost: "{1}{U}{B}", oracleText: "Whenever you cast your second spell each turn, draw a card.", power: "3", cmc: 3),
            Spell("Utility Land", "Land", oracleText: "{T}: Add {U} or {B}.", producedMana: ["U", "B"]),
            Spell("Island", "Basic Land — Island", oracleText: "{T}: Add {U}.", producedMana: ["U"]),
            Spell("Swamp", "Basic Land — Swamp", oracleText: "{T}: Add {B}.", producedMana: ["B"]),
            Spell("Fast Interaction", "Instant", manaCost: "{U}", oracleText: "Counter target spell.", cmc: 1),
            Spell("Value Engine", "Enchantment", manaCost: "{1}{U}", oracleText: "At the beginning of your upkeep, draw a card.", cmc: 2),
            Spell("Combo Tutor", "Sorcery", manaCost: "{1}{B}", oracleText: "Search your library for a card, put that card into your hand, then shuffle.", cmc: 2),
            Spell("Closing Threat", "Creature — Leviathan", manaCost: "{5}{U}", oracleText: "Whenever this creature attacks, creatures you control get +6/+6 until end of turn.", power: "6", cmc: 6),
        ];

        cards.AddRange(Enumerable.Range(1, 62).Select(index => Spell($"Filler {index:00}", "Artifact", manaCost: "{2}", oracleText: "A test artifact.", cmc: 2)));

        return new TestPool(workingList, cards);
    }

    private static TestPool BuildCasualPool()
    {
        TestPool cedh = BuildCedhPool();
        return cedh with
        {
            WorkingList = cedh.WorkingList
                .Where(card => !string.Equals(card.Name, "Fast Interaction", StringComparison.OrdinalIgnoreCase))
                .ToArray(),
            Cards = cedh.Cards
                .Where(card => !string.Equals(card.Name, "Fast Interaction", StringComparison.OrdinalIgnoreCase))
                .ToArray(),
        };
    }

    private static CutLabPoolCard PoolCard(string name, string typeLine, int quantity = 1, bool isCommander = false)
        => new()
        {
            Name = name,
            Quantity = quantity,
            TypeLine = typeLine,
            IsCommander = isCommander,
            IsLocked = isCommander,
        };

    private static ScryfallCard Spell(
        string name,
        string typeLine,
        string? manaCost = null,
        string? oracleText = null,
        string? power = null,
        double cmc = 0,
        IReadOnlyList<string>? producedMana = null)
        => new(
            name,
            manaCost,
            typeLine,
            oracleText,
            power,
            null,
            null,
            null,
            null,
            null,
            null,
            Cmc: cmc,
            ProducedMana: producedMana);

    private sealed record TestPool(IReadOnlyList<CutLabPoolCard> WorkingList, IReadOnlyList<ScryfallCard> Cards);

    private sealed class FakeResolver(IReadOnlyList<ScryfallCard> cards) : IScryfallCardResolver
    {
        public int ResolveSingleCalls { get; private set; }

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(cards.ToList(), []),
            });

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => ResolveSingleAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => ResolveSingleAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
        {
            ResolveSingleCalls++;
            return Task.FromResult(cards.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
