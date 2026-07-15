using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class ManabaseAnalyzerMulliganTests
{
    [Fact]
    public void Cedh_KeepShapes_SurfacesPlanKeepable()
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(ExplosivePayoffDeck(), ManabaseMode.Cedh, keepShapes: true);

        Assert.NotNull(report.MulliganEvaluation);
        Assert.NotNull(report.MulliganEvaluation!.PlanPresence);
        Assert.Equal(report.MulliganEvaluation.PlanPresence.PlanKeepablePercent, report.MulliganEvaluation.PlanKeepablePercent);
        Assert.Equal(report.MulliganEvaluation.PlanPresence.PlanKeepableBand, report.MulliganEvaluation.PlanKeepableBand);
        Assert.True(report.MulliganEvaluation.PlanKeepablePercent <= report.MulliganEvaluation.KeepableHandPercent);
    }

    [Fact]
    public void Casual_OrFlagOff_PlanKeepableIsZero()
    {
        ManabaseReport casual = ManabaseAnalyzer.Analyze(ExplosivePayoffDeck(), ManabaseMode.Casual, keepShapes: true);
        ManabaseReport flagOff = ManabaseAnalyzer.Analyze(ExplosivePayoffDeck(), ManabaseMode.Cedh, keepShapes: false);

        Assert.NotNull(casual.MulliganEvaluation);
        Assert.NotNull(flagOff.MulliganEvaluation);
        Assert.Equal(0, casual.MulliganEvaluation!.PlanKeepablePercent);
        Assert.Equal(string.Empty, casual.MulliganEvaluation.PlanKeepableBand);
        Assert.Equal(0, flagOff.MulliganEvaluation!.PlanKeepablePercent);
        Assert.Equal(string.Empty, flagOff.MulliganEvaluation.PlanKeepableBand);
    }

    [Fact]
    public void Cedh_Turn6Payoff_NotSurfacedAsWorkable()
    {
        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(
            DeckWithCommander(PlanRole.Payoff),
            new[] { Row("Late Payoff", manaValue: 6, onCurveTurn: 6, isCommander: false) },
            defaultTrials: 1000,
            mode: ManabaseMode.Cedh,
            keepShapes: true,
            planPresence: new ManabasePlanPresence
            {
                PayoffPercent = 0,
                PayoffBand = "low",
                PlanPresencePercent = 0,
                Band = "low",
                RolePercents = new Dictionary<PlanRole, int>(),
                RepresentativeOpeners =
                [
                    Opener("keep 7", trackedSpell: string.Empty, trackedTurn: 0, hasPlan: false, shapeLabel: "no plan by turn 4 - mulligan"),
                ],
            });

        OpeningHandSample opener = Assert.Single(result.RepresentativeOpeners);
        Assert.False(opener.HasPlan);
        Assert.False(opener.OnCurveCastable);
        Assert.Equal(string.Empty, opener.TrackedSpellName);
        Assert.Equal("no plan by turn 4 - mulligan", opener.ShapeLabel);
    }

    [Fact]
    public void Cedh_Central_CommanderSurfacesAsOpener()
    {
        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(
            DeckWithCommander(PlanRole.Engine),
            new[]
            {
                Row("Commander", manaValue: 4, onCurveTurn: 3, isCommander: true, castPercent: 95,
                    openers: [Opener("keep 7", "Commander", 3, hasPlan: true)]),
                Row("Support", manaValue: 2, onCurveTurn: 2, isCommander: false, castPercent: 90,
                    openers: [Opener("keep 7", "Support", 2, hasPlan: true)]),
            },
            defaultTrials: 1000,
            mode: ManabaseMode.Cedh,
            keepShapes: true);

        OpeningHandSample opener = Assert.Single(result.RepresentativeOpeners);
        Assert.Equal("Commander", opener.TrackedSpellName);
    }

    [Fact]
    public void Cedh_NonCentral_CommanderNotForced()
    {
        ManabaseMulliganEvaluation result = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(
            DeckWithCommander(PlanRole.None, supportRole: PlanRole.Payoff),
            new[]
            {
                Row("Commander", manaValue: 4, onCurveTurn: 3, isCommander: true, castPercent: 95,
                    openers: [Opener("keep 7", "Commander", 3, hasPlan: true)]),
                Row("Support", manaValue: 2, onCurveTurn: 2, isCommander: false, castPercent: 90,
                    openers: [Opener("keep 7", "Support", 2, hasPlan: true)]),
            },
            defaultTrials: 1000,
            mode: ManabaseMode.Cedh,
            keepShapes: true);

        OpeningHandSample opener = Assert.Single(result.RepresentativeOpeners);
        Assert.Equal("Support", opener.TrackedSpellName);
    }

    [Fact]
    public void Casual_OpenerSelection_Unchanged()
    {
        CardCastability[] rows =
        [
            Row("Commander", manaValue: 4, onCurveTurn: 3, isCommander: true, castPercent: 95,
                openers: [Opener("keep 7", "Commander", 3, hasPlan: true)]),
            Row("Support", manaValue: 2, onCurveTurn: 2, isCommander: false, castPercent: 90,
                openers:
                [
                    Opener("keep 7", "Support", 2, hasPlan: true),
                    Opener("mulligan to 6", "Support", 2, hasPlan: false),
                ]),
        ];

        ManabaseMulliganEvaluation keepShapesOff = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(
            DeckWithCommander(PlanRole.Engine),
            rows,
            defaultTrials: 1000,
            mode: ManabaseMode.Casual,
            keepShapes: false);
        ManabaseMulliganEvaluation casualKeepShapesOn = ManabaseAnalyzer.ComputeMulliganEvaluationForTest(
            DeckWithCommander(PlanRole.Engine),
            rows,
            defaultTrials: 1000,
            mode: ManabaseMode.Casual,
            keepShapes: true);

        Assert.Equal(
            keepShapesOff.RepresentativeOpeners.Select(SerializeOpener),
            casualKeepShapesOn.RepresentativeOpeners.Select(SerializeOpener));
        Assert.All(casualKeepShapesOn.RepresentativeOpeners, opener => Assert.Equal(string.Empty, opener.ShapeLabel));
    }

    private static ManabaseDeck ExplosivePayoffDeck()
        => new()
        {
            TotalCards = 7,
            CommanderCount = 0,
            AverageManaValue = 2.0,
            Sources = new List<ManaSource>
            {
                new() { Name = "Forest A", Produces = new[] { ManaColor.Green }, EntersUntapped = true },
                new() { Name = "Forest B", Produces = new[] { ManaColor.Green }, EntersUntapped = true },
                new() { Name = "Sol Ring", Produces = new[] { ManaColor.Green }, IsLand = false, Weight = 0.75 },
            },
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Sol Ring", ManaValue = 1, Pips = new Dictionary<ManaColor, int>(), IsManaSource = true },
                new() { Name = "Payoff", ManaValue = 4, Pips = new Dictionary<ManaColor, int> { [ManaColor.Green] = 1 }, PlanRoles = PlanRole.Payoff },
                new() { Name = "Filler 0", ManaValue = 1, Pips = new Dictionary<ManaColor, int> { [ManaColor.Green] = 1 } },
                new() { Name = "Filler 1", ManaValue = 1, Pips = new Dictionary<ManaColor, int> { [ManaColor.Green] = 1 } },
            },
            IsSingleton = true,
        };

    private static ManabaseDeck DeckWithCommander(PlanRole commanderRole, PlanRole supportRole = PlanRole.None) => new()
    {
        TotalCards = 99,
        CommanderCount = 1,
        AverageManaValue = 2.5,
        Sources = new List<ManaSource>(),
        Spells = new List<SpellRequirement>
        {
            new()
            {
                Name = "Commander",
                ManaValue = 4,
                Pips = new Dictionary<ManaColor, int> { [ManaColor.Red] = 1 },
                IsCommander = true,
                PlanRoles = commanderRole,
            },
            new()
            {
                Name = "Support",
                ManaValue = 2,
                Pips = new Dictionary<ManaColor, int> { [ManaColor.Red] = 1 },
                PlanRoles = supportRole,
            },
        },
        IsSingleton = true,
    };

    private static CardCastability Row(
        string name,
        int manaValue,
        int onCurveTurn,
        bool isCommander,
        int castPercent = 90,
        IReadOnlyList<OpeningHandSample>? openers = null) => new()
        {
            Name = name,
            ManaValue = manaValue,
            OnCurveTurn = onCurveTurn,
            CastPercent = castPercent,
            LimitingFactor = "mana",
            IsCommander = isCommander,
            KeepableTrials = 800,
            Kept7Trials = 600,
            MulliganTo6Trials = 200,
            RepresentativeOpeners = openers ?? [],
        };

    private static OpeningHandSample Opener(
        string decision,
        string trackedSpell,
        int trackedTurn,
        bool hasPlan,
        string shapeLabel = "") => new()
        {
            Lands = 3,
            Colors = 2,
            RampPieces = 0,
            OtherCards = decision == "keep 7" ? 4 : decision == "mulligan to 6" ? 3 : 2,
            KeptCards = decision switch { "keep 7" => 7, "mulligan to 6" => 6, _ => 5 },
            Decision = decision,
            TrackedSpellName = trackedSpell,
            TrackedOnCurveTurn = trackedTurn,
            OnCurveCastable = hasPlan,
            HasPlan = hasPlan,
            ShapeLabel = shapeLabel,
        };

    private static string SerializeOpener(OpeningHandSample opener)
        => string.Join("|",
            opener.Lands,
            opener.Colors,
            opener.RampPieces,
            opener.OtherCards,
            opener.KeptCards,
            opener.Decision,
            opener.TrackedSpellName,
            opener.TrackedOnCurveTurn,
            opener.OnCurveCastable,
            opener.HasPlan,
            opener.ShapeLabel);
}
