using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class ManabaseAnalyzerTrialsOverrideTests
{
    [Fact]
    public void Analyze_DefaultTrialsOverride_IsByteIdenticalToImplicitDefault()
    {
        ManabaseDeck deck = BuildDeck();

        ManabaseReport baseline = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Cedh, keepShapes: true, interactionLens: true);
        ManabaseReport explicitDefault = ManabaseAnalyzer.Analyze(
            deck,
            ManabaseMode.Cedh,
            keepShapes: true,
            interactionLens: true,
            trialsOverride: CastabilitySimulator.DefaultTrials);
        Assert.NotNull(baseline.MulliganEvaluation);
        Assert.NotNull(explicitDefault.MulliganEvaluation);

        Assert.Equal(baseline.MulliganEvaluation!.KeepableHandPercent, explicitDefault.MulliganEvaluation!.KeepableHandPercent);
        Assert.Equal(baseline.LandDelta, explicitDefault.LandDelta);
        Assert.Equal(
            baseline.MulliganEvaluation.PlanPresence?.PlanPresencePercent,
            explicitDefault.MulliganEvaluation.PlanPresence?.PlanPresencePercent);

        Assert.Equal(
            baseline.ColorFindings.Select(finding => (finding.Color, finding.AverageCastPercent)),
            explicitDefault.ColorFindings.Select(finding => (finding.Color, finding.AverageCastPercent)));

        Assert.Equal(
            baseline.Castability.Select(row => (row.Name, row.CastPercent, row.OnCurveTurn)),
            explicitDefault.Castability.Select(row => (row.Name, row.CastPercent, row.OnCurveTurn)));
    }

    [Fact]
    public void Analyze_ReducedTrialsOverride_ReturnsWellFormedReport()
    {
        ManabaseDeck deck = BuildDeck();

        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck,
            ManabaseMode.Cedh,
            keepShapes: true,
            interactionLens: true,
            trialsOverride: 2000);

        Assert.NotNull(report.MulliganEvaluation);
        Assert.NotNull(report.MulliganEvaluation.PlanPresence);
        Assert.NotEmpty(report.ColorFindings);
        Assert.NotEmpty(report.Castability);
        Assert.NotNull(report.InteractionLens);
    }

    [Fact]
    public void Analyze_ReducedTrialsOverride_IsDeterministicAcrossRepeatedRuns()
    {
        ManabaseDeck deck = BuildDeck();

        ManabaseReport first = ManabaseAnalyzer.Analyze(
            deck,
            ManabaseMode.Cedh,
            keepShapes: true,
            interactionLens: true,
            trialsOverride: 2000);
        ManabaseReport second = ManabaseAnalyzer.Analyze(
            deck,
            ManabaseMode.Cedh,
            keepShapes: true,
            interactionLens: true,
            trialsOverride: 2000);

        Assert.NotNull(first.MulliganEvaluation);
        Assert.NotNull(second.MulliganEvaluation);

        Assert.Equal(first.MulliganEvaluation!.KeepableHandPercent, second.MulliganEvaluation!.KeepableHandPercent);
        Assert.Equal(first.LandDelta, second.LandDelta);
        Assert.Equal(
            first.MulliganEvaluation.PlanPresence?.PlanPresencePercent,
            second.MulliganEvaluation.PlanPresence?.PlanPresencePercent);

        Assert.Equal(
            first.ColorFindings.Select(finding => (finding.Color, finding.AverageCastPercent)),
            second.ColorFindings.Select(finding => (finding.Color, finding.AverageCastPercent)));

        Assert.Equal(
            first.Castability.Select(row => (row.Name, row.CastPercent, row.OnCurveTurn)),
            second.Castability.Select(row => (row.Name, row.CastPercent, row.OnCurveTurn)));
    }

    private static ManabaseDeck BuildDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 24; i++)
        {
            sources.Add(new ManaSource { Name = $"Island {i}", Produces = new[] { ManaColor.Blue } });
        }

        for (int i = 0; i < 10; i++)
        {
            sources.Add(new ManaSource { Name = $"Swamp {i}", Produces = new[] { ManaColor.Black } });
        }

        for (int i = 0; i < 2; i++)
        {
            sources.Add(new ManaSource
            {
                Name = $"Signet {i}",
                Produces = new[] { ManaColor.Blue, ManaColor.Black },
                IsLand = false,
                Weight = 0.75,
            });
        }

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.6,
            Sources = sources,
            Spells =
            [
                new SpellRequirement
                {
                    Name = "Commander",
                    ManaValue = 3,
                    Pips = Pip((ManaColor.Blue, 1), (ManaColor.Black, 1)),
                    IsGold = true,
                    IsCommander = true,
                    PlanRoles = PlanRole.Engine,
                },
                new SpellRequirement
                {
                    Name = "Payoff",
                    ManaValue = 4,
                    Pips = Pip((ManaColor.Blue, 2)),
                    PlanRoles = PlanRole.Payoff,
                },
                new SpellRequirement
                {
                    Name = "Tutor",
                    ManaValue = 2,
                    Pips = Pip((ManaColor.Black, 1)),
                    PlanRoles = PlanRole.TutorCombo,
                },
                new SpellRequirement
                {
                    Name = "Interaction",
                    ManaValue = 2,
                    Pips = Pip((ManaColor.Blue, 1)),
                    PlanRoles = PlanRole.Interaction,
                    IsInteractionSpell = true,
                    Kinds = SpellKinds.Instant,
                },
            ],
            IsSingleton = true,
        };
    }

    private static IReadOnlyDictionary<ManaColor, int> Pip(params (ManaColor Color, int Count)[] pips)
        => pips.ToDictionary(pip => pip.Color, pip => pip.Count);
}
