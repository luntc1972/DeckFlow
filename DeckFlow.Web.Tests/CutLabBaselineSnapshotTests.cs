using System.Net;
using System.Text;
using System.Collections.Generic;
using System.Linq;

using DeckFlow.Web.Extensions;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabBaselineSnapshot"/> covering D-12 baseline reuse, persistence, determinism, and DI registration.</summary>
public sealed class CutLabBaselineSnapshotTests
{
    [Fact]
    public async Task Build_UsesSimulationPipelineAtFullDefaultTrialsAndReturnsSevenFamilies()
    {
        TestPool pool = BuildPool();
        var resolver = new FakeResolver(pool.Cards);
        var simulationService = new CutLabSimulationService(
            new CutLabResolvedCardCache(),
            new CutLabDeltaCache(),
            resolver,
            NullLogger<CutLabSimulationService>.Instance);
        var baselineBuilder = new CutLabBaselineSnapshot(simulationService);

        CutLabMetricSnapshot baseline = await baselineBuilder.Build(pool.WorkingList, "cEDH");
        CutLabMetricSnapshot direct = await simulationService.BuildSnapshot(pool.WorkingList, "cEDH", trialsOverride: null);

        Assert.Equal(7, baseline.Metrics.Select(metric => metric.Family).Distinct().Count());
        Assert.Equal(direct.Metrics, baseline.Metrics);
    }

    [Fact]
    public async Task Build_RoundTripsThroughCutLabStateSerializerUnderByteCap()
    {
        TestPool pool = BuildPool();
        var baseline = await CreateBuilder(pool).Build(pool.WorkingList, "cEDH");
        var state = new CutLabState
        {
            Commander = "Cut Lab Commander",
            Pool = pool.WorkingList,
            BaselineSnapshot = baseline,
        };

        string json = CutLabStateSerializer.Serialize(state);
        CutLabState roundTripped = CutLabStateSerializer.Deserialize(json);

        Assert.True(Encoding.UTF8.GetByteCount(json) < CutLabStateSerializer.MaxUploadBytes);
        Assert.NotNull(roundTripped.BaselineSnapshot);
        Assert.Equal(baseline.Metrics, roundTripped.BaselineSnapshot!.Metrics);
    }

    [Fact]
    public async Task Build_CasualBaselineRoundTripsThroughCutLabStateSerializer()
    {
        TestPool pool = BuildCasualPool();
        var baseline = await CreateBuilder(pool).Build(pool.WorkingList, "Casual");
        var state = new CutLabState
        {
            Commander = "Cut Lab Commander",
            Pool = pool.WorkingList,
            BaselineSnapshot = baseline,
        };

        string json = CutLabStateSerializer.Serialize(state);
        CutLabState roundTripped = CutLabStateSerializer.Deserialize(json);

        Assert.NotNull(roundTripped.BaselineSnapshot);
        Assert.DoesNotContain(roundTripped.BaselineSnapshot!.Metrics, metric => metric.Kind == CutLabMetricKind.EarlyInteraction);
    }

    [Fact]
    public async Task Build_IsDeterministicForSameOriginalPool()
    {
        TestPool pool = BuildPool();
        CutLabBaselineSnapshot builder = CreateBuilder(pool);

        CutLabMetricSnapshot first = await builder.Build(pool.WorkingList, "cEDH");
        CutLabMetricSnapshot second = await builder.Build(pool.WorkingList, "cEDH");

        Assert.Equal(first.Metrics, second.Metrics);
    }

    [Fact]
    public void AddDeckFlowCutLabServices_RegistersSimulationServiceAndBaselineBuilder()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IScryfallCardResolver>(new FakeResolver([]));
        services.AddLogging();

        services.AddDeckFlowCutLabServices();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<CutLabResolvedCardCache>());
        Assert.NotNull(provider.GetRequiredService<CutLabDeltaCache>());
        Assert.NotNull(provider.GetRequiredService<ICutLabSimulationService>());
        Assert.NotNull(provider.GetRequiredService<CutLabBaselineSnapshot>());
    }

    private static CutLabBaselineSnapshot CreateBuilder(TestPool pool)
    {
        var simulationService = new CutLabSimulationService(
            new CutLabResolvedCardCache(),
            new CutLabDeltaCache(),
            new FakeResolver(pool.Cards),
            NullLogger<CutLabSimulationService>.Instance);
        return new CutLabBaselineSnapshot(simulationService);
    }

    private static TestPool BuildPool()
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
        TestPool cedh = BuildPool();
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
            => Task.FromResult(cards.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase)));
    }
}
