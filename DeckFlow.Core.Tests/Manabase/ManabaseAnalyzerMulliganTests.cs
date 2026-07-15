using System.Collections.Generic;

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
}
