using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// RED spec for Phase 75 tap-analysis (Wave 0 baseline). Validates the two tap-quality metrics
/// surfaced via <see cref="ManabaseAnalyzer.Analyze(ManabaseDeck)"/> on synthetic decks:
/// TAP-01 untapped-source composition (overall + per color) and TAP-02 turn-1 untapped
/// availability. These tests FAIL until plan 75-02 wires <c>ComputeTapAnalysis</c> — the
/// <see cref="ManabaseReport.TapAnalysis"/> field is null at this baseline.
///
/// Locked design decisions encoded here so later waves implement them as specified:
/// - D1/D3: Turn-1 availability averages across NON-COMMANDER castability rows (fall back to all
///   rows only when there are zero non-commander rows).
/// - D4: the ✓/⚠ threshold is a flat 80% and informational only — it NEVER affects health.
/// - D5: "Untapped %" denominator is ALL colored sources via the existing EffectiveSources
///   weighting (ManaSource.EntersUntapped is authoritative).
/// - TAP-02 is deck-level: "share of simulated games with ≥1 untapped mana source to spend on
///   turn 1" — NOT per-color.
/// </summary>
public sealed class ManabaseTapAnalysisTests
{
    [Fact]
    public void Analyze_AllUntappedDeck_OverallUntappedPercent_Is100()
    {
        // D5: every colored source enters untapped → 100% overall.
        ManabaseDeck deck = MonoGreenDeck(untapped: 8, tapped: 0);

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.NotNull(report.TapAnalysis);
        Assert.Equal(100, report.TapAnalysis!.OverallUntappedPercent);
    }

    [Fact]
    public void Analyze_AllTappedDeck_OverallUntappedPercent_Is0()
    {
        // D5: no colored source enters untapped → 0% overall.
        ManabaseDeck deck = MonoGreenDeck(untapped: 0, tapped: 8);

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.NotNull(report.TapAnalysis);
        Assert.Equal(0, report.TapAnalysis!.OverallUntappedPercent);
    }

    [Fact]
    public void Analyze_MixedDeck_OverallUntappedPercent_MatchesWeightedFraction()
    {
        // D5: 6 untapped + 2 tapped green sources (all weight 1.0) → 6/8 = 75%.
        ManabaseDeck deck = MonoGreenDeck(untapped: 6, tapped: 2);

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.NotNull(report.TapAnalysis);
        Assert.InRange(report.TapAnalysis!.OverallUntappedPercent, 74, 76);
    }

    [Fact]
    public void Analyze_AllUntappedLands_Turn1UntappedPercent_IsNearCertain()
    {
        // TAP-02: a deck flooded with untapped lands almost always has a T1 untapped source.
        ManabaseDeck deck = MonoGreenDeck(untapped: 40, tapped: 0);

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.NotNull(report.TapAnalysis);
        Assert.True(
            report.TapAnalysis!.Turn1UntappedPercent >= 95,
            $"expected >= 95, got {report.TapAnalysis.Turn1UntappedPercent}");
    }

    [Fact]
    public void Analyze_AllTappedLands_NoFastMana_Turn1UntappedPercent_IsZero()
    {
        // TAP-02: every land enters tapped and there is no 0-cost fast mana → never an untapped
        // source to spend on turn 1.
        ManabaseDeck deck = MonoGreenDeck(untapped: 0, tapped: 40);

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.NotNull(report.TapAnalysis);
        Assert.Equal(0, report.TapAnalysis!.Turn1UntappedPercent);
    }

    [Fact]
    public void Analyze_SingleColorDeck_ColorTap_HasOneEntry()
    {
        ManabaseDeck deck = MonoGreenDeck(untapped: 6, tapped: 2);

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.NotNull(report.TapAnalysis);
        Assert.Single(report.TapAnalysis!.ColorTap);
        Assert.True(report.TapAnalysis.ColorTap.ContainsKey(ManaColor.Green));
    }

    [Fact]
    public void Analyze_MultiColorDeck_ColorTap_HasOneEntryPerColor()
    {
        // Green: all untapped (100%). Blue: half tapped (50%).
        var sources = new List<ManaSource>();
        for (int i = 0; i < 6; i++)
        {
            sources.Add(new ManaSource { Name = "Forest", Produces = new[] { ManaColor.Green }, EntersUntapped = true });
        }

        for (int i = 0; i < 3; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue }, EntersUntapped = true });
        }

        for (int i = 0; i < 3; i++)
        {
            sources.Add(new ManaSource { Name = "Tapped Island", Produces = new[] { ManaColor.Blue }, EntersUntapped = false });
        }

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Green One-Drop", ManaValue = 1, Pips = Pip((ManaColor.Green, 1)) },
                new() { Name = "Blue One-Drop", ManaValue = 1, Pips = Pip((ManaColor.Blue, 1)) },
            },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.NotNull(report.TapAnalysis);
        Assert.Equal(2, report.TapAnalysis!.ColorTap.Count);
        Assert.True(report.TapAnalysis.ColorTap.ContainsKey(ManaColor.Green));
        Assert.True(report.TapAnalysis.ColorTap.ContainsKey(ManaColor.Blue));
        Assert.Equal(100, report.TapAnalysis.ColorTap[ManaColor.Green].UntappedPercent);
        Assert.InRange(report.TapAnalysis.ColorTap[ManaColor.Blue].UntappedPercent, 49, 51);
    }

    // --- TAP-02 color-matched regression (overridden 2026-06-28 after Codex review) ---------------
    // The turn-1 untapped metric must require an untapped source of a NEEDED COLOR, not merely any
    // untapped mana. Colorless / off-color untapped sources do NOT credit a colored spell.

    [Fact]
    public void Analyze_ColorlessOnlyUntapped_ColoredSpell_Turn1Untapped_IsZero()
    {
        // The only untapped sources are COLORLESS (Wastes); every colored (green) source enters tapped.
        // Under the old "any untapped mana" rule this scored ~100%. Color-matched, a colorless source
        // (mask 0) cannot pay the green pip on turn 1 → 0%.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource { Name = "Wastes", Produces = new[] { ManaColor.Colorless }, EntersUntapped = true });
        }

        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource { Name = "Tapped Green", Produces = new[] { ManaColor.Green }, EntersUntapped = false });
        }

        ManabaseReport report = ManabaseAnalyzer.Analyze(GreenSpellDeck(sources));

        Assert.NotNull(report.TapAnalysis);
        Assert.Equal(0, report.TapAnalysis!.Turn1UntappedPercent);
    }

    [Fact]
    public void Analyze_OffColorUntapped_ColoredSpell_Turn1Untapped_IsZero()
    {
        // The only untapped sources are OFF-COLOR (blue) for a green spell; green is tapped. An
        // untapped blue source on turn 1 does not advance a green spell, so it must not count.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue }, EntersUntapped = true });
        }

        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource { Name = "Tapped Green", Produces = new[] { ManaColor.Green }, EntersUntapped = false });
        }

        ManabaseReport report = ManabaseAnalyzer.Analyze(GreenSpellDeck(sources));

        Assert.NotNull(report.TapAnalysis);
        Assert.Equal(0, report.TapAnalysis!.Turn1UntappedPercent);
    }

    [Fact]
    public void Analyze_OnColorUntapped_ColoredSpell_Turn1Untapped_IsNearCertain()
    {
        // An ON-COLOR untapped source (untapped Forest) DOES credit the green spell on turn 1.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 40; i++)
        {
            sources.Add(new ManaSource { Name = "Forest", Produces = new[] { ManaColor.Green }, EntersUntapped = true });
        }

        ManabaseReport report = ManabaseAnalyzer.Analyze(GreenSpellDeck(sources));

        Assert.NotNull(report.TapAnalysis);
        Assert.True(
            report.TapAnalysis!.Turn1UntappedPercent >= 95,
            $"expected >= 95, got {report.TapAnalysis.Turn1UntappedPercent}");
    }

    [Fact]
    public void Analyze_ColorlessSpell_AnyUntappedSource_Counts()
    {
        // A colorless spell (no colored pips) preserves the old behavior: ANY untapped source on
        // turn 1 qualifies, including colorless Wastes.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 40; i++)
        {
            sources.Add(new ManaSource { Name = "Wastes", Produces = new[] { ManaColor.Colorless }, EntersUntapped = true });
        }

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 1.0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                // {1} generic — no colored pips.
                new() { Name = "Ornithopter Ramp", ManaValue = 1, Pips = Pip() },
            },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.NotNull(report.TapAnalysis);
        Assert.True(
            report.TapAnalysis!.Turn1UntappedPercent >= 95,
            $"expected >= 95, got {report.TapAnalysis.Turn1UntappedPercent}");
    }

    // A deck whose single tracked spell is a green one-drop, over a caller-supplied source list.
    private static ManabaseDeck GreenSpellDeck(List<ManaSource> sources) => new()
    {
        TotalCards = 100,
        CommanderCount = 1,
        AverageManaValue = 1.0,
        Sources = sources,
        Spells = new List<SpellRequirement>
        {
            new() { Name = "Llanowar Elves", ManaValue = 1, Pips = Pip((ManaColor.Green, 1)) },
        },
    };

    private static ManabaseDeck MonoGreenDeck(int untapped, int tapped)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < untapped; i++)
        {
            sources.Add(new ManaSource { Name = "Forest", Produces = new[] { ManaColor.Green }, EntersUntapped = true });
        }

        for (int i = 0; i < tapped; i++)
        {
            sources.Add(new ManaSource { Name = "Tapped Green", Produces = new[] { ManaColor.Green }, EntersUntapped = false });
        }

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 1.0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Llanowar Elves", ManaValue = 1, Pips = Pip((ManaColor.Green, 1)) },
            },
        };
    }

    private static IReadOnlyDictionary<ManaColor, int> Pip(params (ManaColor Color, int Count)[] pips)
        => pips.ToDictionary(p => p.Color, p => p.Count);
}
