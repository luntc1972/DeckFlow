using System.Net;
using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabSimulationService"/> covering SIM-01 projection, deltas, and caching.</summary>
public sealed class CutLabSimulationServiceTests
{
    [Fact]
    public async Task ComputeProposalDeltas_AfterAccept_ReusesResultAndIdenticalDeltas()
    {
        TestPool pool = BuildCedhPool();
        int calls = 0;
        var service = new CutLabSimulationService(
            new CutLabResolvedCardCache(), new CutLabDeltaCache(), new FakeResolver(pool.Cards),
            NullLogger<CutLabSimulationService>.Instance,
            (_, _, _, _, _) => new(),
            (_, _, _, _, _) => { calls++; return new(); });
        string candidate = pool.WorkingList.First(card => !card.IsCommander).Name;
        CutLabProposalDeltas deltas = await service.ComputeProposalDeltas(pool.WorkingList, candidate, "cEDH");
        var after = pool.WorkingList.Where(card => card.Name != candidate).ToArray();
        await service.BuildSnapshotResult(after, "cEDH");
        Assert.Equal(2, calls);
        Assert.Same(deltas, await service.ComputeProposalDeltas(pool.WorkingList, candidate, "cEDH"));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task BuildSnapshot_MultipleMisses_UsesCollectionOnce()
    {
        TestPool pool = BuildCedhPool();
        var resolver = new FakeResolver(pool.Cards);
        await CreateService(resolver).BuildSnapshot(pool.WorkingList.Take(2).ToArray(), "cEDH");
        Assert.Equal(1, resolver.CollectionCalls);
        Assert.Equal(0, resolver.ResolveSingleCalls);
    }

    [Fact]
    public async Task BuildSnapshot_DuplicateAndCaseVariantNames_PreservesOrderAndCount()
    {
        TestPool pool = BuildCedhPool();
        var resolver = new FakeResolver(pool.Cards);
        CutLabPoolCard[] workingList =
        [
            pool.WorkingList[0],
            pool.WorkingList[1],
            pool.WorkingList[0] with { Name = pool.WorkingList[0].Name.ToUpperInvariant() },
            pool.WorkingList[0],
        ];
        IReadOnlyList<DeckCardEntry>? capturedEntries = null;
        var service = new CutLabSimulationService(
            new CutLabResolvedCardCache(), new CutLabDeltaCache(), resolver,
            NullLogger<CutLabSimulationService>.Instance,
            (entries, experience, trials, goals, overrides) =>
            {
                capturedEntries = entries;
                return new CutLabMetricSnapshot();
            });

        await service.BuildSnapshot(workingList, "cEDH");

        Assert.NotNull(capturedEntries);
        Assert.Equal(workingList.Length, capturedEntries.Count);
        Assert.Equal(new[] { pool.WorkingList[0].Name, pool.WorkingList[1].Name, pool.WorkingList[0].Name, pool.WorkingList[0].Name },
            capturedEntries.Select(entry => entry.Card.Name));
        Assert.Equal(workingList.Select(card => card.Quantity), capturedEntries.Select(entry => entry.Quantity));
        Assert.Equal(1, resolver.CollectionCalls);
        Assert.Equal(0, resolver.ResolveSingleCalls);
    }

    [Fact]
    public async Task BuildSnapshot_InjectedSingletonResolver_ReusesCollectionCacheAcrossScopes()
    {
        TestPool pool = BuildCedhPool();
        var resolver = new FakeResolver(pool.Cards);
        var services = new ServiceCollection();
        services.AddSingleton<IScryfallCardResolver>(resolver);
        services.AddSingleton(
            new ScryfallReferenceResolver(resolver, new ScryfallCollectionCardCache()));
        services.AddScoped<CutLabResolvedCardCache>();
        services.AddScoped<CutLabDeltaCache>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<CutLabSimulationService>>(
            NullLogger<CutLabSimulationService>.Instance);
        services.AddScoped<ICutLabSimulationService, CutLabSimulationService>();
        using var provider = services.BuildServiceProvider();
        for (int i = 0; i < 2; i++)
        {
            using var scope = provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICutLabSimulationService>();
            await service.BuildSnapshot(pool.WorkingList.Take(2).ToArray(), "cEDH");
        }

        Assert.Equal(1, resolver.CollectionCalls);
        Assert.Equal(0, resolver.ResolveSingleCalls);
    }

    [Fact]
    public async Task BuildSnapshot_CollectionFailure_FallsBackPerCardInWorkingListOrder()
    {
        TestPool pool = BuildCedhPool();
        var resolver = new FakeResolver(pool.Cards) { CollectionStatusCode = HttpStatusCode.ServiceUnavailable };
        CutLabPoolCard[] workingList = [pool.WorkingList[1], pool.WorkingList[0], pool.WorkingList[1]];

        CutLabMetricSnapshot snapshot = await CreateService(resolver).BuildSnapshot(workingList, "cEDH");

        Assert.NotNull(snapshot);
        Assert.Equal(1, resolver.CollectionCalls);
        Assert.Equal(workingList.Length, resolver.ResolveSingleCalls);
        Assert.Equal(workingList.Select(card => card.Name), resolver.ResolveSingleNames);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task BuildSnapshot_UnresolvedName_PreservesException(HttpStatusCode collectionStatusCode)
    {
        TestPool pool = BuildCedhPool();
        var resolver = new FakeResolver([]) { CollectionStatusCode = collectionStatusCode };
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(resolver).BuildSnapshot(pool.WorkingList, "cEDH"));
        Assert.Equal($"Cut Lab simulation could not resolve '{pool.WorkingList[0].Name}'.", error.Message);
    }

    [Fact]
    public async Task ComputeProposalDeltas_AfterBuildSnapshotResultWithSameKey_ReusesCurrentSnapshot()
    {
        TestPool pool = BuildCedhPool();
        int resultCalls = 0;
        var snapshotLists = new List<IReadOnlyList<DeckCardEntry>>();
        var service = new CutLabSimulationService(
            new CutLabResolvedCardCache(), new CutLabDeltaCache(), new FakeResolver(pool.Cards),
            NullLogger<CutLabSimulationService>.Instance,
            (entries, _, _, _, _) => { snapshotLists.Add(entries); return new(); },
            (_, _, _, _, _) => { resultCalls++; return new(); });
        string key = CutLabResolvedCardCache.ComputePoolKey(pool.WorkingList);
        string candidate = pool.WorkingList.First(card => !card.IsCommander).Name;
        await service.BuildSnapshotResult(pool.WorkingList, "cEDH", 4000, key);
        await service.ComputeProposalDeltas(pool.WorkingList, candidate, "cEDH", 4000, key);
        Assert.Equal(2, resultCalls);
        Assert.Empty(snapshotLists);
    }

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
    public async Task BuildSnapshot_CurveChangesByOneWhenOneCardLeavesLargestBucket()
    {
        TestPool pool = BuildContinuousCurvePool();
        var service = CreateService(new FakeResolver(pool.Cards));

        CutLabMetricSnapshot before = await service.BuildSnapshot(pool.WorkingList, "cEDH");

        IReadOnlyList<CutLabPoolCard> afterWorkingList = RemoveCandidate(pool.WorkingList, "MV Three 01");
        CutLabMetricSnapshot after = await service.BuildSnapshot(afterWorkingList, "cEDH");

        double beforeCurve = Assert.Single(before.Metrics, metric => metric.Kind == CutLabMetricKind.Curve).Value;
        double afterCurve = Assert.Single(after.Metrics, metric => metric.Kind == CutLabMetricKind.Curve).Value;
        Assert.Equal(22, beforeCurve);
        Assert.Equal(21, afterCurve);
        Assert.Equal(1, Math.Abs(beforeCurve - afterCurve));
    }

    [Fact]
    public async Task BuildSnapshot_CasualModeMarksEarlyInteractionNotApplicable()
    {
        TestPool pool = BuildCasualPool();
        var service = CreateService(new FakeResolver(pool.Cards));

        CutLabMetricSnapshot snapshot = await service.BuildSnapshot(pool.WorkingList, "Casual");

        Assert.DoesNotContain(snapshot.Metrics, metric => metric.Kind == CutLabMetricKind.EarlyInteraction);
    }

    [Fact]
    public async Task BuildSnapshot_UsesAnyOfPlanRolesForRepresentativeLine()
    {
        TestPool pool = BuildCedhPool();
        var service = CreateService(new FakeResolver(pool.Cards));

        CutLabMetricSnapshot snapshot = await service.BuildSnapshot(pool.WorkingList, "cEDH");
        (ManabaseDeck deck, ManabaseReport report) = BuildDirectProjection(pool.WorkingList, "cEDH", trialsOverride: 4000, pool.Cards);

        PlanRole lineRoles = PlanRole.Engine | PlanRole.Payoff | PlanRole.TutorCombo | PlanRole.Interaction;
        CardCastability[] expectedRows = deck.Spells
            .Where(spell => (spell.PlanRoles & lineRoles) != 0)
            .Select(spell => report.Castability.Single(row => string.Equals(row.Name, spell.Name, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        double expected = expectedRows.Max(row => PercentByTurn(row, CutLabGoalDefaults.RepresentativeLineByTurn));

        CutLabMetricValue representativeLine = Assert.Single(snapshot.Metrics, metric => metric.Kind == CutLabMetricKind.RepresentativeLineByTurn);
        Assert.NotEmpty(expectedRows);
        Assert.True(expected > 0);
        Assert.Equal(expected, representativeLine.Value);
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
    public async Task ComputeProposalDeltas_DifferentPlayExperienceSamePool_DoesNotReuseCachedDeltas()
    {
        TestPool pool = BuildCedhPool();
        var goals = new CutLabGoalSettings { CommanderByTurn = 5 };
        var analysisCounter = new CountingSnapshotBuilder(weightByPlayExperience: true);
        var service = new CutLabSimulationService(
            new CutLabResolvedCardCache(),
            new CutLabDeltaCache(),
            new FakeResolver(pool.Cards),
            NullLogger<CutLabSimulationService>.Instance,
            analysisCounter.Build);

        CutLabProposalDeltas casual = await service.ComputeProposalDeltas(pool.WorkingList, "Utility Land", "Casual", trialsOverride: 100, goals: goals);
        CutLabProposalDeltas cedh = await service.ComputeProposalDeltas(pool.WorkingList, "Utility Land", "cEDH", trialsOverride: 100, goals: goals);

        CutLabMetricDelta casualDelta = Assert.Single(casual.Deltas);
        CutLabMetricDelta cedhDelta = Assert.Single(cedh.Deltas);
        Assert.Equal(casualDelta.Before * 2, cedhDelta.Before);
        Assert.Equal(casualDelta.After * 2, cedhDelta.After);
        Assert.NotEqual(casualDelta.Delta, cedhDelta.Delta);
        Assert.Equal(4, analysisCounter.CallCount);
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

    [Fact]
    public async Task BuildSnapshot_GoalTurnAtOrBeyondOnCurveTurn_UsesCommanderCastPercent()
    {
        TestPool pool = BuildLateCommanderPool();
        var service = CreateService(new FakeResolver(pool.Cards));

        CutLabMetricSnapshot snapshot = await service.BuildSnapshot(
            pool.WorkingList,
            "cEDH",
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: null,
            goals: new CutLabGoalSettings { CommanderByTurn = 6 });
        ManabaseReport report = BuildDirectReport(pool.WorkingList, "cEDH", trialsOverride: 4000, pool.Cards);

        CutLabMetricValue commanderByTurn = Assert.Single(snapshot.Metrics, metric => metric.Kind == CutLabMetricKind.CommanderByTurn);
        CardCastability commander = Assert.Single(report.Castability, row => row.IsCommander);
        Assert.Equal("Commander by turn 6", commanderByTurn.Label);
        Assert.Equal(commander.CastPercent, commanderByTurn.Value);
    }

    [Fact]
    public async Task BuildSnapshot_ChangedGoalTurn_UpdatesCommanderLabelAndValue()
    {
        TestPool pool = BuildLateCommanderPool();
        var service = CreateService(new FakeResolver(pool.Cards));

        CutLabMetricSnapshot earlyGoal = await service.BuildSnapshot(
            pool.WorkingList,
            "cEDH",
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: null,
            goals: new CutLabGoalSettings { CommanderByTurn = 3 });
        CutLabMetricSnapshot lateGoal = await service.BuildSnapshot(
            pool.WorkingList,
            "cEDH",
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: null,
            goals: new CutLabGoalSettings { CommanderByTurn = 6 });

        CutLabMetricValue earlyCommander = Assert.Single(earlyGoal.Metrics, metric => metric.Kind == CutLabMetricKind.CommanderByTurn);
        CutLabMetricValue lateCommander = Assert.Single(lateGoal.Metrics, metric => metric.Kind == CutLabMetricKind.CommanderByTurn);

        Assert.Equal("Commander by turn 3", earlyCommander.Label);
        Assert.Equal("Commander by turn 6", lateCommander.Label);
        Assert.NotEqual(earlyCommander.Value, lateCommander.Value);
    }

    [Fact]
    public async Task BuildSnapshot_NullGoals_MatchesDefaultGoalMetrics()
    {
        TestPool pool = BuildCedhPool();
        var service = CreateService(new FakeResolver(pool.Cards));

        CutLabMetricSnapshot withNullGoals = await service.BuildSnapshot(
            pool.WorkingList,
            "cEDH",
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: null,
            goals: null);
        CutLabMetricSnapshot withDefaultGoals = await service.BuildSnapshot(
            pool.WorkingList,
            "cEDH",
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: null,
            goals: new CutLabGoalSettings());

        Assert.Equal(withDefaultGoals.Metrics, withNullGoals.Metrics);
    }

    [Fact]
    public async Task ComputeProposalDeltas_SameInputsAndGoals_RemainsDeterministic()
    {
        TestPool pool = BuildCedhPool();
        var service = CreateService(new FakeResolver(pool.Cards));
        var goals = new CutLabGoalSettings
        {
            CommanderByTurn = 5,
            EngineByTurn = 3,
            RepresentativeLineByTurn = 7,
        };

        CutLabProposalDeltas first = await service.ComputeProposalDeltas(
            pool.WorkingList,
            "Utility Land",
            "cEDH",
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: null,
            goals: goals);
        CutLabProposalDeltas second = await service.ComputeProposalDeltas(
            pool.WorkingList,
            "Utility Land",
            "cEDH",
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: null,
            goals: goals);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task BuildSnapshot_ReorderedPoolCacheHitAlignsCardsByName()
    {
        TestPool orderedPool = BuildCacheAlignmentPool();
        TestPool shuffledPool = orderedPool with
        {
            WorkingList =
            [
                orderedPool.WorkingList[2],
                orderedPool.WorkingList[0],
                orderedPool.WorkingList[3],
                orderedPool.WorkingList[1],
            ],
        };
        var sharedResolvedCardCache = new CutLabResolvedCardCache();
        var warmResolver = new FakeResolver(orderedPool.Cards);
        var warmService = new CutLabSimulationService(
            sharedResolvedCardCache,
            new CutLabDeltaCache(),
            warmResolver,
            NullLogger<CutLabSimulationService>.Instance);
        var cachedResolver = new FakeResolver(orderedPool.Cards);
        var cachedService = new CutLabSimulationService(
            sharedResolvedCardCache,
            new CutLabDeltaCache(),
            cachedResolver,
            NullLogger<CutLabSimulationService>.Instance);
        var expectedService = CreateService(new FakeResolver(orderedPool.Cards));

        _ = await warmService.BuildSnapshot(orderedPool.WorkingList, "cEDH");
        CutLabMetricSnapshot cachedSnapshot = await cachedService.BuildSnapshot(shuffledPool.WorkingList, "cEDH");
        CutLabMetricSnapshot expectedSnapshot = await expectedService.BuildSnapshot(shuffledPool.WorkingList, "cEDH");

        Assert.Equal(0, cachedResolver.ResolveSingleCalls);
        Assert.Equal(expectedSnapshot.Metrics, cachedSnapshot.Metrics);
    }

    [Fact]
    public void TagPlanRoles_DuplicateNormalizedOverrides_LastWins()
    {
        TestPool pool = BuildCedhPool();
        DeckCardEntry[] entries = pool.Cards.Select(card => new DeckCardEntry
        {
            Card = ScryfallCardDataMapper.ToCardData(card),
            Quantity = 1,
        }).ToArray();
        IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(entries);
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
        CutLabRoleOverride[] overrides =
        [
            new() { Name = "VALUE ENGINE", Added = ["engines"], Removed = ["tutor"] },
            new() { Name = "value-engine", Removed = ["engines", "draw"], Added = ["tutor"] },
        ];
        var method = typeof(CutLabSimulationService).GetMethod("TagPlanRoles",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;

        var actual = (ManabaseDeck)method.Invoke(null, [deck, facts, ManabaseMode.Cedh, overrides])!;

        var spell = Assert.Single(actual.Spells, spell => spell.Name == "Value Engine");
        Assert.False((spell.PlanRoles & PlanRole.Engine) != 0);
        Assert.True((spell.PlanRoles & PlanRole.TutorCombo) != 0);
    }

    [Fact]
    public async Task BuildSnapshotResult_RoleOverrideResimulatesWithSharedCache()
    {
        TestPool pool = BuildCedhPool();
        var sharedDeltaCache = new CutLabDeltaCache();
        var warmService = new CutLabSimulationService(
            new CutLabResolvedCardCache(), sharedDeltaCache, new FakeResolver(pool.Cards),
            NullLogger<CutLabSimulationService>.Instance);
        await warmService.BuildSnapshotResult(pool.WorkingList, "cEDH");

        var coldResolver = new FakeResolver(pool.Cards);
        var coldService = new CutLabSimulationService(
            new CutLabResolvedCardCache(), sharedDeltaCache, coldResolver,
            NullLogger<CutLabSimulationService>.Instance);
        await coldService.BuildSnapshotResult(pool.WorkingList, "cEDH");
        Assert.Equal(0, coldResolver.ResolveSingleCalls);

        CutLabRoleOverride[] roleOverrides =
        [
            new() { Name = "Value Engine", Removed = ["engines", "draw"] },
        ];
        await coldService.BuildSnapshotResult(pool.WorkingList, "cEDH", roleOverrides: roleOverrides);

        Assert.True(coldResolver.CollectionCalls > 0);
    }

    [Fact]
    public async Task BuildSnapshotResult_ReusesCachedFullResultAcrossScopedServiceInstances()
    {
        TestPool pool = BuildCedhPool();
        var sharedDeltaCache = new CutLabDeltaCache();
        var resultCounter = new CountingSimulationResultBuilder();
        var firstResolver = new FakeResolver(pool.Cards);
        var secondResolver = new FakeResolver(pool.Cards);
        var firstService = new CutLabSimulationService(
            new CutLabResolvedCardCache(),
            sharedDeltaCache,
            firstResolver,
            NullLogger<CutLabSimulationService>.Instance,
            BuildCountingSnapshot,
            resultCounter.Build);
        var secondService = new CutLabSimulationService(
            new CutLabResolvedCardCache(),
            sharedDeltaCache,
            secondResolver,
            NullLogger<CutLabSimulationService>.Instance,
            BuildCountingSnapshot,
            resultCounter.Build);

        CutLabSimulationResult first = await firstService.BuildSnapshotResult(pool.WorkingList, "cEDH");
        CutLabSimulationResult second = await secondService.BuildSnapshotResult(pool.WorkingList, "cEDH");

        Assert.Equal(1, resultCounter.CallCount);
        Assert.Equal(1, firstResolver.CollectionCalls);
        Assert.Equal(0, secondResolver.ResolveSingleCalls);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task ComputeProposalDeltas_ReusesPriorAfterSnapshotForNextBeforeSnapshot()
    {
        TestPool pool = BuildCedhPool();
        var sharedResolvedCardCache = new CutLabResolvedCardCache();
        var sharedDeltaCache = new CutLabDeltaCache();
        var resolver = new FakeResolver(pool.Cards);
        var analysisCounter = new CountingSnapshotBuilder();
        var service = new CutLabSimulationService(
            sharedResolvedCardCache,
            sharedDeltaCache,
            resolver,
            NullLogger<CutLabSimulationService>.Instance,
            analysisCounter.Build);

        _ = await service.ComputeProposalDeltas(pool.WorkingList, "Utility Land", "cEDH");

        IReadOnlyList<CutLabPoolCard> afterWorkingList = RemoveCandidate(pool.WorkingList, "Utility Land");
        _ = await service.ComputeProposalDeltas(afterWorkingList, "Filler 01", "cEDH");

        Assert.Equal(3, analysisCounter.CallCount);
        Assert.Equal(1, analysisCounter.CountCallsForPool(afterWorkingList));
    }

    [Fact]
    public async Task ComputeProposalDeltas_DistinctTrialsOverrideValuesDoNotReuseSnapshotCacheEntries()
    {
        TestPool pool = BuildCedhPool();
        var sharedResolvedCardCache = new CutLabResolvedCardCache();
        var sharedDeltaCache = new CutLabDeltaCache();
        var resolver = new FakeResolver(pool.Cards);
        var analysisCounter = new CountingSnapshotBuilder();
        var service = new CutLabSimulationService(
            sharedResolvedCardCache,
            sharedDeltaCache,
            resolver,
            NullLogger<CutLabSimulationService>.Instance,
            analysisCounter.Build);

        _ = await service.ComputeProposalDeltas(pool.WorkingList, "Utility Land", "cEDH", trialsOverride: 4000);
        _ = await service.ComputeProposalDeltas(pool.WorkingList, "Utility Land", "cEDH", trialsOverride: null);

        Assert.Equal(4, analysisCounter.CallCount);
    }

    [Fact]
    public async Task BuildSnapshot_DifferentGoalsOnSamePool_DoNotReuseSnapshotCacheEntries()
    {
        TestPool pool = BuildCedhPool();
        var sharedResolvedCardCache = new CutLabResolvedCardCache();
        var sharedDeltaCache = new CutLabDeltaCache();
        var resolver = new FakeResolver(pool.Cards);
        var analysisCounter = new CountingSnapshotBuilder();
        var service = new CutLabSimulationService(
            sharedResolvedCardCache,
            sharedDeltaCache,
            resolver,
            NullLogger<CutLabSimulationService>.Instance,
            analysisCounter.Build);

        CutLabMetricSnapshot earlyGoal = await service.BuildSnapshot(
            pool.WorkingList,
            "cEDH",
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: null,
            goals: new CutLabGoalSettings { CommanderByTurn = 3 });
        CutLabMetricSnapshot lateGoal = await service.BuildSnapshot(
            pool.WorkingList,
            "cEDH",
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: null,
            goals: new CutLabGoalSettings { CommanderByTurn = 9 });

        Assert.NotEqual(earlyGoal.Metrics, lateGoal.Metrics);
        Assert.Equal(2, analysisCounter.CallCount);
    }

    [Fact]
    public async Task ComputeProposalDeltas_DifferentGoalsOnSamePool_DoNotReuseDeltaCacheEntries()
    {
        TestPool pool = BuildCedhPool();
        var sharedResolvedCardCache = new CutLabResolvedCardCache();
        var sharedDeltaCache = new CutLabDeltaCache();
        var resolver = new FakeResolver(pool.Cards);
        var analysisCounter = new CountingSnapshotBuilder();
        var service = new CutLabSimulationService(
            sharedResolvedCardCache,
            sharedDeltaCache,
            resolver,
            NullLogger<CutLabSimulationService>.Instance,
            analysisCounter.Build);

        CutLabProposalDeltas earlyGoal = await service.ComputeProposalDeltas(
            pool.WorkingList,
            "Utility Land",
            "cEDH",
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: null,
            goals: new CutLabGoalSettings { CommanderByTurn = 3 });
        CutLabProposalDeltas lateGoal = await service.ComputeProposalDeltas(
            pool.WorkingList,
            "Utility Land",
            "cEDH",
            trialsOverride: ICutLabSimulationService.InLoopTrials,
            poolKey: null,
            goals: new CutLabGoalSettings { CommanderByTurn = 9 });

        Assert.NotEqual(earlyGoal.Deltas, lateGoal.Deltas);
        Assert.Equal(4, analysisCounter.CallCount);
    }

    [Fact]
    public async Task BuildSnapshot_CachedPool_ReusesLookupAndPreservesEntries()
    {
        TestPool pool = BuildCedhPool();
        var cards = new EnumerationCountingList<ScryfallCardData>(pool.Cards.Select(ScryfallCardDataMapper.ToCardData).ToArray());
        var cache = new CutLabResolvedCardCache();
        string key = CutLabResolvedCardCache.ComputePoolKey(pool.WorkingList);
        cache.Set(key, cards);
        cards.Enumerations = 0;
        var observed = new List<IReadOnlyList<DeckCardEntry>>();
        var service = new CutLabSimulationService(cache, new CutLabDeltaCache(), new FakeResolver(pool.Cards),
            NullLogger<CutLabSimulationService>.Instance,
            (entries, _, _, _, _) => { observed.Add(entries); return new(); });

        await service.BuildSnapshot(pool.WorkingList, "cEDH", 100, key);
        await service.BuildSnapshot(pool.WorkingList.Reverse().ToArray(), "cEDH", 200, key);

        Assert.Equal(1, cards.Enumerations);
        Assert.Equal(pool.WorkingList.Select(card => (card.Name, card.Quantity, card.IsCommander)),
            observed[0].Select(entry => (entry.Card.Name, entry.Quantity, entry.IsCommander)));
        Assert.Equal(observed[0].Reverse().Select(entry => entry.Card), observed[1].Select(entry => entry.Card));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildSnapshot(
            [new CutLabPoolCard { Name = "Absent", Quantity = 1 }], "cEDH", 300, key));
        Assert.Equal($"Cut Lab resolved-card cache was missing 'Absent' for pool {key}.", error.Message);
    }

    private sealed class EnumerationCountingList<T>(IReadOnlyList<T> items) : IReadOnlyList<T>
    {
        public int Enumerations { get; set; }
        public int Count => items.Count;
        public T this[int index] => items[index];
        public IEnumerator<T> GetEnumerator()
        {
            Enumerations++;
            return items.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
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
        => BuildDirectProjection(workingList, playExperience, trialsOverride, cards).Report;

    private static (ManabaseDeck Deck, ManabaseReport Report) BuildDirectProjection(
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
        ManabaseDeck deck = ManabaseClassifier.Classify(
            facts,
            isSingleton: true,
            rampCreditV2: true,
            landRampSim: true,
            payLifeUntapped: true,
            checkLandUntapped: true,
            restrictedLands: true);
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
        CedhLandContext cedhContext = mode == ManabaseMode.Cedh
            ? new CedhLandContext(null, 0, Enabled: true)
            : CedhLandContext.Disabled;

        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck,
            mode,
            useManaQuantity: true,
            colorAwareMulligan: true,
            gateRampOnCastable: true,
            ritualBurst: true,
            ritualLandCredit: true,
            scryCredit: true,
            colorlessSnow: true,
            keepShapes: true,
            interactionLens: mode == ManabaseMode.Cedh,
            useHealthBandCastability: true,
            useHealthBandHeadlineFloor: true,
            cedhContext: cedhContext,
            trialsOverride: trialsOverride);
        return (deck, report);
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

    private static TestPool BuildContinuousCurvePool()
    {
        List<CutLabPoolCard> workingList =
        [
            PoolCard("Curve Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Island", "Basic Land — Island", quantity: 30),
        ];
        workingList.AddRange(Enumerable.Range(1, 22).Select(index => PoolCard($"MV Three {index:00}", "Creature — Wizard")));
        workingList.AddRange(Enumerable.Range(1, 17).Select(index => PoolCard($"MV One {index:00}", "Artifact")));
        workingList.AddRange(Enumerable.Range(1, 17).Select(index => PoolCard($"MV Two {index:00}", "Artifact")));
        workingList.AddRange(Enumerable.Range(1, 16).Select(index => PoolCard($"MV Four {index:00}", "Artifact")));

        List<ScryfallCard> cards =
        [
            Spell("Curve Commander", "Legendary Creature — Human Wizard", manaCost: "{U}", cmc: 1),
            Spell("Island", "Basic Land — Island", oracleText: "{T}: Add {U}.", producedMana: ["U"]),
        ];
        cards.AddRange(Enumerable.Range(1, 22).Select(index => Spell($"MV Three {index:00}", "Creature — Wizard", manaCost: "{2}{U}", cmc: 3)));
        cards.AddRange(Enumerable.Range(1, 17).Select(index => Spell($"MV One {index:00}", "Artifact", manaCost: "{U}", cmc: 1)));
        cards.AddRange(Enumerable.Range(1, 17).Select(index => Spell($"MV Two {index:00}", "Artifact", manaCost: "{1}{U}", cmc: 2)));
        cards.AddRange(Enumerable.Range(1, 16).Select(index => Spell($"MV Four {index:00}", "Artifact", manaCost: "{3}{U}", cmc: 4)));

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

    private static TestPool BuildCacheAlignmentPool()
        => new(
            [
                PoolCard("Mismatch Commander", "Legendary Creature — Human Wizard", isCommander: true),
                PoolCard("Mismatch Land", "Land", quantity: 34),
                PoolCard("Mismatch Interaction", "Instant"),
                PoolCard("Mismatch Threat", "Creature — Dragon"),
            ],
            [
                Spell("Mismatch Commander", "Legendary Creature — Human Wizard", manaCost: "{2}{U}", oracleText: "Flying", power: "3", cmc: 3),
                Spell("Mismatch Land", "Land", oracleText: "{T}: Add {U}.", producedMana: ["U"]),
                Spell("Mismatch Interaction", "Instant", manaCost: "{U}", oracleText: "Counter target spell.", cmc: 1),
                Spell("Mismatch Threat", "Creature — Dragon", manaCost: "{6}{U}", oracleText: "Flying", power: "7", cmc: 7),
            ]);

    private static TestPool BuildLateCommanderPool()
    {
        List<CutLabPoolCard> workingList =
        [
            PoolCard("Late Commander", "Legendary Creature — Human Wizard", isCommander: true),
            PoolCard("Island", "Basic Land — Island", quantity: 35),
            PoolCard("Swamp", "Basic Land — Swamp", quantity: 29),
            PoolCard("Fast Interaction", "Instant"),
            PoolCard("Value Engine", "Enchantment"),
            PoolCard("Combo Tutor", "Sorcery"),
            PoolCard("Closing Threat", "Creature — Leviathan"),
        ];

        workingList.AddRange(Enumerable.Range(1, 27).Select(index => PoolCard($"Late Filler {index:00}", "Artifact")));

        List<ScryfallCard> cards =
        [
            Spell("Late Commander", "Legendary Creature — Human Wizard", manaCost: "{4}{U}{B}", oracleText: "Whenever you cast your second spell each turn, draw a card.", power: "5", cmc: 6),
            Spell("Island", "Basic Land — Island", oracleText: "{T}: Add {U}.", producedMana: ["U"]),
            Spell("Swamp", "Basic Land — Swamp", oracleText: "{T}: Add {B}.", producedMana: ["B"]),
            Spell("Fast Interaction", "Instant", manaCost: "{U}", oracleText: "Counter target spell.", cmc: 1),
            Spell("Value Engine", "Enchantment", manaCost: "{1}{U}", oracleText: "At the beginning of your upkeep, draw a card.", cmc: 2),
            Spell("Combo Tutor", "Sorcery", manaCost: "{1}{B}", oracleText: "Search your library for a card, put that card into your hand, then shuffle.", cmc: 2),
            Spell("Closing Threat", "Creature — Leviathan", manaCost: "{5}{U}", oracleText: "Whenever this creature attacks, creatures you control get +6/+6 until end of turn.", power: "6", cmc: 6),
        ];

        cards.AddRange(Enumerable.Range(1, 27).Select(index => Spell($"Late Filler {index:00}", "Artifact", manaCost: "{2}", oracleText: "A test artifact.", cmc: 2)));

        return new TestPool(workingList, cards);
    }

    private static double PercentByTurn(CardCastability row, int turn)
    {
        if (row.EarlyCastPercents.Count == 0)
        {
            return turn >= row.OnCurveTurn ? row.CastPercent : 0;
        }

        if (turn >= row.OnCurveTurn)
        {
            return row.CastPercent;
        }

        int index = Math.Clamp(turn - 1, 0, row.EarlyCastPercents.Count - 1);
        return row.EarlyCastPercents[index];
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

    private static IReadOnlyList<CutLabPoolCard> RemoveCandidate(
        IReadOnlyList<CutLabPoolCard> currentWorkingList,
        string candidateCardName)
    {
        bool removed = false;
        return currentWorkingList
            .Where(card =>
            {
                if (!removed && string.Equals(card.Name, candidateCardName, StringComparison.OrdinalIgnoreCase))
                {
                    removed = true;
                    return false;
                }

                return true;
            })
            .ToArray();
    }

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
        public int CollectionCalls { get; private set; }
        public HttpStatusCode CollectionStatusCode { get; init; } = HttpStatusCode.OK;
        public List<string> ResolveSingleNames { get; } = [];

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
            => CountCollection(request);

        private Task<RestResponse<ScryfallCollectionResponse>> CountCollection(RestRequest request)
        {
            CollectionCalls++;
            return Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = CollectionStatusCode,
                Data = new ScryfallCollectionResponse(cards.ToList(), []),
            });
        }

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => ResolveSingleAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => ResolveSingleAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
        {
            ResolveSingleCalls++;
            ResolveSingleNames.Add(cardName);
            return Task.FromResult(cards.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private sealed class CountingSnapshotBuilder(bool weightByPlayExperience = false)
    {
        private readonly Dictionary<string, int> _callsByPoolKey = new(StringComparer.Ordinal);

        public int CallCount { get; private set; }

        public CutLabMetricSnapshot Build(
            IReadOnlyList<DeckCardEntry> deckEntries,
            string? playExperience,
            int? trialsOverride,
            CutLabGoalSettings? goals,
            IReadOnlyList<CutLabRoleOverride>? roleOverrides)
        {
            ArgumentNullException.ThrowIfNull(deckEntries);

            CallCount++;
            string poolKey = CutLabResolvedCardCache.ComputePoolKey(
                deckEntries.Select(entry => (entry.Card.Name, entry.Quantity)).ToArray());
            _callsByPoolKey.TryGetValue(poolKey, out int calls);
            _callsByPoolKey[poolKey] = calls + 1;

            return new CutLabMetricSnapshot
            {
                Metrics =
                [
                    new CutLabMetricValue
                    {
                        Kind = CutLabMetricKind.CommanderByTurn,
                        Family = CutLabMetricFamily.CategoryByTurn,
                        Label = $"Commander by turn {goals?.CommanderByTurn ?? CutLabGoalDefaults.CommanderByTurn}",
                        Value = (deckEntries.Sum(entry => entry.Quantity)
                            + (trialsOverride ?? 20_000)
                            + (goals?.CommanderByTurn ?? CutLabGoalDefaults.CommanderByTurn))
                            * (weightByPlayExperience && playExperience == "cEDH" ? 2.0 : 1.0),
                        Unit = CutLabMetricUnit.Percent,
                    },
                ],
            };
        }

        public int CountCallsForPool(IReadOnlyList<CutLabPoolCard> workingList)
        {
            string poolKey = CutLabResolvedCardCache.ComputePoolKey(workingList.Select(card => (card.Name, card.Quantity)).ToArray());
            return _callsByPoolKey.TryGetValue(poolKey, out int calls) ? calls : 0;
        }
    }

    private sealed class CountingSimulationResultBuilder
    {
        public int CallCount { get; private set; }

        public CutLabSimulationResult Build(
            IReadOnlyList<DeckCardEntry> deckEntries,
            string? playExperience,
            int? trialsOverride,
            CutLabGoalSettings? goals,
            IReadOnlyList<CutLabRoleOverride>? roleOverrides)
        {
            CallCount++;
            CutLabMetricSnapshot snapshot = BuildCountingSnapshot(deckEntries, playExperience, trialsOverride, goals, roleOverrides);
            return new CutLabSimulationResult
            {
                Snapshot = snapshot,
                CastabilityByCardName = deckEntries.ToDictionary(
                    entry => entry.Card.Name,
                    entry => new CutLabSimulationCardView
                    {
                        Cmc = (int?)entry.Card.Cmc,
                        CastPercent = entry.Quantity,
                    },
                    StringComparer.OrdinalIgnoreCase),
                ActualLands = deckEntries.Count(entry => CutLabLockRules.IsLand(entry.Card.TypeLine)),
                TargetLands = deckEntries.Sum(entry => entry.Quantity),
            };
        }
    }

    private static CutLabMetricSnapshot BuildCountingSnapshot(
        IReadOnlyList<DeckCardEntry> deckEntries,
        string? playExperience,
        int? trialsOverride,
        CutLabGoalSettings? goals,
        IReadOnlyList<CutLabRoleOverride>? roleOverrides)
    {
        _ = playExperience;
        return new CutLabMetricSnapshot
        {
            Metrics =
            [
                new CutLabMetricValue
                {
                    Kind = CutLabMetricKind.CommanderByTurn,
                    Family = CutLabMetricFamily.CategoryByTurn,
                    Label = $"Commander by turn {goals?.CommanderByTurn ?? CutLabGoalDefaults.CommanderByTurn}",
                    Value = deckEntries.Sum(entry => entry.Quantity)
                        + (trialsOverride ?? 20_000)
                        + (goals?.CommanderByTurn ?? CutLabGoalDefaults.CommanderByTurn),
                    Unit = CutLabMetricUnit.Percent,
                },
            ],
        };
    }
}
