using System;
using DeckFlow.Core.Analysis;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Covers <see cref="DeckAnalysisPacketService.BuildScoreBlockText"/> — the paste-safe ASCII score
/// artifact (UI-SPEC §10) folded into every analysis prompt variant. Asserts the header, the four
/// "Axis: N/5 Label (rationale)" lines, the cross-check line, the heuristic disclaimer, and that the
/// output is ASCII only (no em/en dashes that would mojibake on paste).
/// </summary>
public sealed class DeckAnalysisScoreBlockTextTests
{
    private static DeckMultiAxisScore Sample() =>
        new(
            PowerBand: 4,
            SpeedBand: 3,
            ControlBand: 2,
            ConsistencyBand: 5,
            PowerRationale: new DeckScoreRationale("4 Game Changers, 2 two-card combos, 9 fast-mana sources"),
            SpeedRationale: new DeckScoreRationale("avg MV 2.6, 9 fast-mana, 7 ramp/draw under 3 MV"),
            ControlRationale: new DeckScoreRationale("11 interaction pieces, 4 board wipes, 3 counters"),
            ConsistencyRationale: new DeckScoreRationale("8 tutors, 2 redundant combo lines, smooth 2.6 curve"),
            BracketNumber: 4,
            BracketCrossCheckText: "score aligns with the Bracket 4 classification.",
            ScoreAlignsBracket: true);

    [Fact]
    public void BuildScoreBlockText_ContainsHeaderAxesCrossCheckAndDisclaimer()
    {
        string block = DeckAnalysisPacketService.BuildScoreBlockText(Sample());

        Assert.Contains("DECK SCORE (coarse 0-5 bands - magnitude, not quality)", block, StringComparison.Ordinal);
        Assert.Contains("Power:", block, StringComparison.Ordinal);
        Assert.Contains("Speed:", block, StringComparison.Ordinal);
        Assert.Contains("Control:", block, StringComparison.Ordinal);
        Assert.Contains("Consistency:", block, StringComparison.Ordinal);
        Assert.Contains("Cross-check: score aligns with the Bracket 4 classification.", block, StringComparison.Ordinal);
        Assert.Contains("heuristic estimates", block, StringComparison.Ordinal);
        Assert.Contains("re-check and refine", block, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildScoreBlockText_EmitsBandFiguresAndLabels()
    {
        string block = DeckAnalysisPacketService.BuildScoreBlockText(Sample());

        Assert.Contains("4/5", block, StringComparison.Ordinal);   // Power
        Assert.Contains("3/5", block, StringComparison.Ordinal);   // Speed
        Assert.Contains("2/5", block, StringComparison.Ordinal);   // Control
        Assert.Contains("5/5", block, StringComparison.Ordinal);   // Consistency
        Assert.Contains("High", block, StringComparison.Ordinal);  // band 4 label
        Assert.Contains("Moderate", block, StringComparison.Ordinal); // band 3 label
        Assert.Contains("Modest", block, StringComparison.Ordinal); // band 2 label
        Assert.Contains("Extreme", block, StringComparison.Ordinal); // band 5 label
    }

    [Fact]
    public void BuildScoreBlockText_CarriesEachAxisRationale()
    {
        string block = DeckAnalysisPacketService.BuildScoreBlockText(Sample());

        Assert.Contains("4 Game Changers, 2 two-card combos, 9 fast-mana sources", block, StringComparison.Ordinal);
        Assert.Contains("avg MV 2.6, 9 fast-mana, 7 ramp/draw under 3 MV", block, StringComparison.Ordinal);
        Assert.Contains("11 interaction pieces, 4 board wipes, 3 counters", block, StringComparison.Ordinal);
        Assert.Contains("8 tutors, 2 redundant combo lines, smooth 2.6 curve", block, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildScoreBlockText_IsAsciiSafe_NoEmOrEnDashes()
    {
        string block = DeckAnalysisPacketService.BuildScoreBlockText(Sample());

        Assert.DoesNotContain('—', block); // em dash
        Assert.DoesNotContain('–', block); // en dash
        Assert.All(block, ch => Assert.True(ch < 128, $"non-ASCII char U+{(int)ch:X4} in score block"));
    }
}
