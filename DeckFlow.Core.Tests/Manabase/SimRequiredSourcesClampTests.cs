using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Guards the invariant that the mulligan-aware sim-derived source requirement never exceeds
/// Karsten's mulligan-blind table. Regression: an {2}{R}{R} card in a 99-card Gruul deck reported
/// "need ~35 of 36 red sources" because the Monte-Carlo cast% sat depressed and the binary search
/// climbed toward totalLands. The sim's free-first-mulligan model can only LOWER the requirement.
/// </summary>
public sealed class SimRequiredSourcesClampTests
{
    private static ManaSource Land(ManaColor color) =>
        new() { Name = $"{color} land", Produces = new[] { color }, IsLand = true };

    // 99-card deck, 36 lands skewed off-red (30 green / 6 red), with one {2}{R}{R} MV4 bomb. Pre-clamp
    // this drove the red requirement to the totalLands sentinel; post-clamp it is bounded by Karsten.
    private static ManabaseDeck BuildRedPoorDoublePipDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 30; i++)
        {
            sources.Add(Land(ManaColor.Green));
        }

        for (int i = 0; i < 6; i++)
        {
            sources.Add(Land(ManaColor.Red));
        }

        var bomb = new SpellRequirement
        {
            Name = "RR Bomb",
            ManaValue = 4,
            Pips = new Dictionary<ManaColor, int> { [ManaColor.Red] = 2 },
        };

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement> { bomb },
            AverageManaValue = 3.0,
            IsSingleton = true,
        };
    }

    [Fact]
    public void RedDoublePipRequirement_NeverExceedsKarstenCeiling()
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(deck: BuildRedPoorDoublePipDeck(),
            mode: ManabaseMode.Casual, importance: CommanderImportance.Standard);

        ColorSourceFinding red = report.ColorFindings.Single(f => f.Color == ManaColor.Red);
        int turn = report.Castability.Single(c => c.Name == "RR Bomb").OnCurveTurn;

        // librarySize = TotalCards - CommanderCount = 99; totalLands = 36.
        int karstenCeiling = KarstenManabase.SourcesNeeded(99, 36, pips: 2, manaValue: turn);

        Assert.True(red.RequiredSources <= karstenCeiling,
            $"required {red.RequiredSources} must not exceed Karsten ceiling {karstenCeiling} (turn {turn})");
    }

    [Fact]
    public void RedDoublePipRequirement_IsSane_NotTheTotalLandsSentinel()
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(deck: BuildRedPoorDoublePipDeck(),
            mode: ManabaseMode.Casual, importance: CommanderImportance.Standard);

        int required = report.ColorFindings.Single(f => f.Color == ManaColor.Red).RequiredSources;

        // Pre-clamp this was ~35 (≈ totalLands 36). A double red pip by ~turn 4 in 99 cards is a
        // Karsten-table value in the low 20s at most — never "almost every land must be red".
        Assert.InRange(required, 2, 24);
    }

    [Fact]
    public void KarstenCeiling_ForDoubleRedByTurnFour_IsInTheExpectedBand()
    {
        // Premise of the clamp: Karsten's own figure for {..}{R}{R} on turn 4 in a 99-card / 36-land
        // deck is a sane number, so clamping to it cannot reintroduce the sentinel.
        int needed = KarstenManabase.SourcesNeeded(deckSize: 99, totalLands: 36, pips: 2, manaValue: 4);

        Assert.InRange(needed, 14, 24);
    }
}
