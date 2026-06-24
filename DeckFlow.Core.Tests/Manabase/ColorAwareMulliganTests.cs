using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// MQ-05 (color-aware London mulligan): when the flag is on, a non-forced keep of a multi-color deck
/// also requires the opening lands to show enough distinct colors (threshold min(C, lands, 2)). The
/// gate is a no-op for mono decks and for the off path, and it never moves the Karsten color counts
/// (the verdict probe path stays count-only).
/// </summary>
public sealed class ColorAwareMulliganTests
{
    private static readonly IReadOnlyList<ManaColor> White = new[] { ManaColor.White };
    private static readonly IReadOnlyList<ManaColor> WhiteBlue = new[] { ManaColor.White, ManaColor.Blue };
    private static readonly IReadOnlyList<ManaColor> Empty = System.Array.Empty<ManaColor>();

    // ---- the color-keep threshold (pure gate) ---------------------------------------------

    [Fact]
    public void MonoDeck_GateIsNoOp()
    {
        // C <= 1: the gate always passes, even with zero colored lands in the opener.
        Assert.True(CastabilitySimulator.ColorKeepSatisfiedForTest(Empty, lands: 3, deckColorCount: 1));
        Assert.True(CastabilitySimulator.ColorKeepSatisfiedForTest(Empty, lands: 3, deckColorCount: 0));
    }

    [Fact]
    public void ThreeColorDeck_OneColorOpener_FailsGate()
    {
        // 3-color deck, 3 lands all one color → needs min(3,3,2)=2 distinct → reject.
        Assert.False(CastabilitySimulator.ColorKeepSatisfiedForTest(White, lands: 3, deckColorCount: 3));
    }

    [Fact]
    public void ThreeColorDeck_TwoColorOpener_PassesGate()
    {
        // Same deck, opener shows W+U (2 colors) → 2 >= min(3,3,2)=2 → keep. KCap=2 never demands the
        // third color in the opener.
        Assert.True(CastabilitySimulator.ColorKeepSatisfiedForTest(WhiteBlue, lands: 3, deckColorCount: 3));
    }

    [Fact]
    public void TwoColorDeck_SingleLand_ThresholdClampedToLands()
    {
        // Only one land in the opener can show at most one color; min(C,lands,Cap)=min(2,1,2)=1 so a
        // single colored land suffices (the gate never demands more colors than lands held).
        Assert.True(CastabilitySimulator.ColorKeepSatisfiedForTest(White, lands: 1, deckColorCount: 2));
    }

    [Fact]
    public void FiveColorDeck_TwoColorOpener_PassesGate()
    {
        // KCap caps the demand at 2 even for a 5-color deck.
        Assert.True(CastabilitySimulator.ColorKeepSatisfiedForTest(WhiteBlue, lands: 4, deckColorCount: 5));
        Assert.False(CastabilitySimulator.ColorKeepSatisfiedForTest(White, lands: 4, deckColorCount: 5));
    }

    // ---- analyzer-level integration -------------------------------------------------------

    [Fact]
    public void MonoColorDeck_Identical_EvenWhenOn()
    {
        ManabaseDeck deck = MonoBlueDeck();

        int off = CastOf(deck, colorAware: false, "Blue Spell");
        int on = CastOf(deck, colorAware: true, "Blue Spell");

        // Mono deck (C=1) → the gate is a no-op → byte-identical cast% with the flag on.
        Assert.Equal(off, on);
    }

    [Fact]
    public void ColorCounts_AreInvariant_ToColorAwareFlag()
    {
        ManabaseDeck deck = SkewedWhiteBlueDeck();

        ManabaseReport off = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, colorAwareMulligan: false);
        ManabaseReport on = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, colorAwareMulligan: true);

        // MQ-05 only changes which openers the display sim keeps; the Karsten color verdict (driven by
        // the count-only probe path) must not move.
        Assert.Equal(off.TargetLands, on.TargetLands);
        Assert.Equal(off.ColorFindings.Count, on.ColorFindings.Count);
        for (int i = 0; i < off.ColorFindings.Count; i++)
        {
            Assert.Equal(off.ColorFindings[i].Color, on.ColorFindings[i].Color);
            Assert.Equal(off.ColorFindings[i].ActualSources, on.ColorFindings[i].ActualSources);
            Assert.Equal(off.ColorFindings[i].RequiredSources, on.ColorFindings[i].RequiredSources);
        }
    }

    [Fact]
    public void ColorAware_RaisesScarceColorCast_OnSkewedMulticolorDeck()
    {
        // Heavily White-skewed WU deck (blue is the only non-white source). OFF keeps all-Plains
        // openers that cannot make blue; ON requires 2 distinct colors, so every kept opener holds an
        // Island → the {U} spell casts on curve far more often. Large, deterministic-direction gap.
        ManabaseDeck deck = SkewedWhiteBlueDeck();

        int off = CastOf(deck, colorAware: false, "Blue One");
        int on = CastOf(deck, colorAware: true, "Blue One");

        Assert.True(on > off, $"expected color-aware mulligan to raise scarce-color cast% (off={off}, on={on})");
    }

    [Fact]
    public void FlagOff_IsDeterministic_OnMulticolorDeck()
    {
        // Off path: the seeded Monte-Carlo is reproducible and unaffected by MQ-05 — two flag-off runs
        // of the same multicolor deck produce identical cast% for every row (locks byte-identical-off).
        ManabaseDeck deck = SkewedWhiteBlueDeck();

        ManabaseReport a = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, colorAwareMulligan: false);
        ManabaseReport b = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, colorAwareMulligan: false);

        Assert.Equal(a.Castability.Count, b.Castability.Count);
        for (int i = 0; i < a.Castability.Count; i++)
        {
            Assert.Equal(a.Castability[i].Name, b.Castability[i].Name);
            Assert.Equal(a.Castability[i].CastPercent, b.Castability[i].CastPercent);
        }
    }

    [Fact]
    public void ForcedKeepDepth_StillReturnsPlayableHand_WhenGateNeverSatisfiable()
    {
        // Pathological deck: C=2 (spells demand W and U) but the ONLY color source is White, so no
        // opener can ever show 2 colors. With the flag on, every non-forced depth fails the color gate
        // and the mulligan must fall through to the FORCED final keep (which bypasses the gate and
        // bottoms correctly). The white spell stays castable (> 0) — proof the forced path returns a
        // real hand and never loops — and color-aware cannot beat count-only here.
        ManabaseDeck deck = AllWhiteLandsTwoColorDemandDeck();

        int offWhite = CastOf(deck, colorAware: false, "White One");
        int onWhite = CastOf(deck, colorAware: true, "White One");

        Assert.True(onWhite > 0, $"forced keep must return a playable hand (onWhite={onWhite})");
        Assert.True(onWhite <= offWhite, $"unsatisfiable color gate cannot raise cast% (off={offWhite}, on={onWhite})");
    }

    // ---- builders -------------------------------------------------------------------------

    private static int CastOf(ManabaseDeck deck, bool colorAware, string spellName)
        => ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, colorAwareMulligan: colorAware)
            .Castability.First(c => c.Name == spellName).CastPercent;

    private static SpellRequirement Spell(string name, int mv, params (ManaColor Color, int Count)[] pips) => new()
    {
        Name = name,
        ManaValue = mv,
        Pips = pips.ToDictionary(p => p.Color, p => p.Count),
    };

    private static ManabaseDeck MonoBlueDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 36; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement> { Spell("Blue Spell", 3, (ManaColor.Blue, 1)) },
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck SkewedWhiteBlueDeck()
    {
        var sources = new List<ManaSource>();

        // White-heavy fixing: 30 Plains, only 5 Islands. An untuned keep happily keeps all-Plains 7s.
        for (int i = 0; i < 30; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }

        for (int i = 0; i < 5; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        return new ManabaseDeck
        {
            TotalCards = 99,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                Spell("Blue One", 1, (ManaColor.Blue, 1)),
                Spell("White One", 1, (ManaColor.White, 1)),
            },
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck AllWhiteLandsTwoColorDemandDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 35; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }

        return new ManabaseDeck
        {
            TotalCards = 99, // padded with filler → ~35% lands so openers land in the count band
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                Spell("White One", 1, (ManaColor.White, 1)),
                Spell("Blue One", 1, (ManaColor.Blue, 1)), // makes C=2, but no blue source exists
            },
            AverageManaValue = 2.5,
            IsSingleton = true,
        };
    }
}
