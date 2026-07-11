using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// MQ-03 70-03b (landRampSim): repeatable land-ramp-to-battlefield spells (Cultivate / Rampant Growth)
/// are modeled in the castability simulator as colorless, non-land ramp sources so the fetched land's
/// mana is credited — closing the sim ↔ regression gap. Colorless + non-land → color counts, land
/// total, and RampSourceCount stay put; the flag-off path adds no source (byte-identical sim).
/// </summary>
public sealed class LandRampSimTests
{
    private static CardFact Land(string name, int qty, string color) => new()
    {
        Name = name,
        Quantity = qty,
        TypeLine = $"Basic Land — {name}",
        OracleText = $"{{T}}: Add {{{color}}}.",
        ProducedMana = new[] { color },
        ManaValue = 0,
        HasLandFace = true,
    };

    private static CardFact Spell(string name, double mv, string typeLine, string oracle, string manaCost, int qty = 1) => new()
    {
        Name = name,
        Quantity = qty,
        ManaValue = mv,
        TypeLine = typeLine,
        OracleText = oracle,
        ManaCost = manaCost,
    };

    private const string CultivateOracle =
        "Search your library for up to two basic land cards, put one onto the battlefield tapped and the other into your hand, then shuffle.";
    private const string RampantGrowthOracle =
        "Search your library for a basic land card, put it onto the battlefield tapped, then shuffle.";

    private static IReadOnlyList<CardFact> RampDeck() => new List<CardFact>
    {
        Land("Forest", 32, "G"),
        Spell("Rampant Growth", 2, "Sorcery", RampantGrowthOracle, "{1}{G}"),
        Spell("Nature's Lore", 2, "Sorcery", RampantGrowthOracle, "{1}{G}"),
        Spell("Three Visits", 2, "Sorcery", RampantGrowthOracle, "{1}{G}"),
        Spell("Farseek", 2, "Sorcery", RampantGrowthOracle, "{1}{G}"),
        Spell("Cultivate", 3, "Sorcery", CultivateOracle, "{2}{G}"),
        Spell("Kodama's Reach", 3, "Sorcery", CultivateOracle, "{2}{G}"),
        // Expensive green payoff — high generic so persistent ramp matters.
        Spell("Big Green", 7, "Creature — Hydra", "Trample", "{6}{G}"),
        // Inert filler to a realistic ~99-card, ~33% land ratio (padded out of the sim's library).
        Spell("Filler", 3, "Creature — Bear", "Vanilla.", "{3}", qty: 58),
    };

    private static ManabaseReport Analyze(bool landRampSim)
    {
        ManabaseDeck deck = ManabaseClassifier.Classify(RampDeck(), isSingleton: true, landRampSim: landRampSim);
        return ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard);
    }

    private static int Cast(ManabaseReport r, string name) => r.Castability.First(c => c.Name == name).CastPercent;

    // ---- classifier source shape ----------------------------------------------------------

    [Fact]
    public void Off_AddsNoLandRampSource()
    {
        ManabaseDeck off = ManabaseClassifier.Classify(RampDeck(), landRampSim: false);
        Assert.DoesNotContain(off.Sources, s => s.Name == "Cultivate");
        Assert.DoesNotContain(off.Sources, s => !s.IsLand && s.DeployCost is not null);
    }

    [Fact]
    public void On_AddsColorlessNonLandRampSource_WithMvDeployCost()
    {
        ManabaseDeck on = ManabaseClassifier.Classify(RampDeck(), landRampSim: true);

        ManaSource cultivate = on.Sources.Single(s => s.Name == "Cultivate");
        Assert.False(cultivate.IsLand);          // non-land → no land-count / mulligan inflation
        Assert.Empty(cultivate.Produces);        // colorless → no color-count change
        Assert.Equal(3, cultivate.DeployCost);   // MV3 → deploy cost 3 (not the default 2)

        ManaSource rampant = on.Sources.Single(s => s.Name == "Rampant Growth");
        Assert.Equal(2, rampant.DeployCost);
    }

    [Fact]
    public void LandSearchToHand_NotModeled()
    {
        // "into your hand" (no "onto the battlefield") is not persistent access → no source even on.
        var deck = new List<CardFact>
        {
            Land("Forest", 33, "G"),
            Spell("Land Grant", 1, "Sorcery",
                "Search your library for a basic land card, reveal it, and put it into your hand.", "{G}"),
        };
        ManabaseDeck on = ManabaseClassifier.Classify(deck, landRampSim: true);
        Assert.DoesNotContain(on.Sources, s => s.Name == "Land Grant");
    }

    [Fact]
    public void MdfcSpellBack_HasNullDeployCost_NeverSelfExcluded()
    {
        // Regression guard (Codex HIGH): an MDFC spell-land adds a source with the SAME name as its
        // castability row, via AddPartialSources. Since the MDFC-as-real-lands refactor its back face is
        // a REAL land (IsLand == true, weight 1.0, no land-ramp DeployCost marker). Self-exclusion keys
        // on the land-ramp marker (non-land AND DeployCost set), NOT on name alone, so scoring that
        // MDFC's own row must NOT drop its back-face land source — and a real land trivially fails the
        // non-land clause anyway. DeployCost stays null (it is a land, not modeled land-ramp).
        var deck = new List<CardFact>
        {
            Land("Plains", 33, "W"),
            new()
            {
                Name = "Sejiri Shelter",
                Quantity = 1,
                ManaValue = 1,
                TypeLine = "Instant",
                ManaCost = "{W}",
                OracleText = "Target creature you control gains protection from a color until end of turn.",
                ProducedMana = new[] { "W" }, // the land back taps for W
                HasLandFace = true,
            },
            Spell("Filler", 3, "Creature — Bear", "Vanilla.", "{3}", qty: 60),
        };

        foreach (bool flag in new[] { false, true })
        {
            ManaSource src = ManabaseClassifier.Classify(deck, landRampSim: flag)
                .Sources.Single(s => s.Name == "Sejiri Shelter");
            Assert.True(src.IsLand); // MDFC back is a real land since the real-lands refactor
            Assert.Null(src.DeployCost); // not modeled land-ramp → exclusion predicate skips it
        }
    }

    // ---- simulator behavior ---------------------------------------------------------------

    [Fact]
    public void On_RaisesExpensivePayoffCast()
    {
        // Modeling the fetched land's mana lets the {6}{G} payoff resolve on curve more often.
        int off = Cast(Analyze(landRampSim: false), "Big Green");
        int on = Cast(Analyze(landRampSim: true), "Big Green");
        Assert.True(on > off, $"land-ramp sim should raise the expensive payoff's cast% (off={off}, on={on})");
    }

    [Fact]
    public void OwnRow_NotSelfInflated()
    {
        // Self-exclusion in isolation: a deck whose ONLY land-ramp is Cultivate. Cultivate cannot ramp
        // ITSELF out, so its own cast% must not move with the flag — while a separate expensive payoff
        // (which the Cultivate source CAN help) does rise, proving the source is really present.
        var deck = new List<CardFact>
        {
            Land("Forest", 33, "G"),
            Spell("Cultivate", 3, "Sorcery", CultivateOracle, "{2}{G}"),
            Spell("Big Green", 7, "Creature — Hydra", "Trample", "{6}{G}"),
            Spell("Filler", 3, "Creature — Bear", "Vanilla.", "{3}", qty: 58),
        };
        ManabaseReport off = ManabaseAnalyzer.Analyze(
            ManabaseClassifier.Classify(deck, landRampSim: false), ManabaseMode.Casual);
        ManabaseReport on = ManabaseAnalyzer.Analyze(
            ManabaseClassifier.Classify(deck, landRampSim: true), ManabaseMode.Casual);

        int ownOff = Cast(off, "Cultivate"), ownOn = Cast(on, "Cultivate");
        int otherOff = Cast(off, "Big Green"), otherOn = Cast(on, "Big Green");

        Assert.True(System.Math.Abs(ownOn - ownOff) <= 2,
            $"Cultivate must not self-inflate its own row (off={ownOff}, on={ownOn})");
        Assert.True(otherOn > otherOff,
            $"the Cultivate source must still help OTHER payoffs (off={otherOff}, on={otherOn})");
    }

    [Fact]
    public void DeployFriction_RampStillHelps_ButDoesNotPushPayoffToNearCertain()
    {
        // DEPLOY-FRICTION guard (debug session manabase-too-optimistic): drawn ramp must come online
        // with real cost — playing a rock consumes that turn's mana, so it cannot both pay for itself
        // AND power the payoff the same turn (only its OUTPUT lands, next turn). A green deck whose ONLY
        // way to reach a {6}{G} payoff "on curve" leans on land-ramp must therefore still leave the
        // payoff well short of near-certain on-curve casts — the pre-fix free-deploy model inflated it.
        var deck = new List<CardFact>
        {
            Land("Forest", 30, "G"),
            Spell("Rampant Growth", 2, "Sorcery", RampantGrowthOracle, "{1}{G}", qty: 4),
            Spell("Cultivate", 3, "Sorcery", CultivateOracle, "{2}{G}", qty: 4),
            Spell("Big Green", 7, "Creature — Hydra", "Trample", "{6}{G}"),
            Spell("Filler", 3, "Creature — Bear", "Vanilla.", "{3}", qty: 57),
        };

        int off = Cast(
            ManabaseAnalyzer.Analyze(ManabaseClassifier.Classify(deck, landRampSim: false), ManabaseMode.Casual),
            "Big Green");
        int on = Cast(
            ManabaseAnalyzer.Analyze(ManabaseClassifier.Classify(deck, landRampSim: true), ManabaseMode.Casual),
            "Big Green");

        // Ramp still helps the expensive payoff (the source is real)...
        Assert.True(on > off, $"land-ramp must still help the {{6}}{{G}} payoff (off={off}, on={on})");
        // ...but deploy friction keeps it honest: a turn-7 payoff reached largely via ramp is NOT a
        // near-certain on-curve cast. The pre-fix free-deploy model pushed cases like this far higher.
        Assert.True(on <= 90, $"deploy friction should keep the ramp-reliant payoff realistic, got on={on}%");
    }

    [Fact]
    public void ColorCountsAndLandTotal_Invariant()
    {
        ManabaseReport off = Analyze(landRampSim: false);
        ManabaseReport on = Analyze(landRampSim: true);

        // Non-land, colorless source → land total and per-color Karsten math unchanged.
        Assert.Equal(off.ActualLands, on.ActualLands);
        Assert.Equal(off.TargetLands, on.TargetLands);
        Assert.Equal(off.ColorFindings.Count, on.ColorFindings.Count);
        for (int i = 0; i < off.ColorFindings.Count; i++)
        {
            Assert.Equal(off.ColorFindings[i].Color, on.ColorFindings[i].Color);
            Assert.Equal(off.ColorFindings[i].ActualSources, on.ColorFindings[i].ActualSources);
            Assert.Equal(off.ColorFindings[i].RequiredSources, on.ColorFindings[i].RequiredSources);
        }
    }
}
