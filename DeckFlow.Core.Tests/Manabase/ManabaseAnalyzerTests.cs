using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates the §6 mana-base prototype: hypergeometric math, Karsten source/land
/// targets, and the analyzer's verdict against the real "Buffs by Hans" (Xyris Temur)
/// deck that was cross-checked live against the Salubrious Snail manabase tool.
/// </summary>
public sealed class ManabaseAnalyzerTests
{
    [Fact]
    public void Hypergeometric_AtLeast_MatchesKnownTwoLandKeepProbability()
    {
        // 7-card opener from a 60-card deck with 24 lands, P(>= 2 lands) ≈ 0.84.
        double p = Hypergeometric.AtLeast(60, 24, 7, 2);
        Assert.InRange(p, 0.82, 0.86);
    }

    [Fact]
    public void Hypergeometric_AtLeast_ZeroRequirementIsCertain()
    {
        Assert.Equal(1.0, Hypergeometric.AtLeast(99, 36, 8, 0));
    }

    [Theory]
    [InlineData(1, 0.90)]
    [InlineData(2, 0.91)]
    [InlineData(4, 0.93)]
    [InlineData(7, 0.96)]
    public void ConsistencyThreshold_Is89PlusManaValue(int manaValue, double expected)
    {
        Assert.Equal(expected, KarstenManabase.ConsistencyThreshold(manaValue), 3);
    }

    [Fact]
    public void SingletonLandTarget_LowCurveTemurDeck_LandsNearThirtySeven()
    {
        // Buffs by Hans: 100 cards, 1 commander, avgMV 2.59, ~6 cheap ramp/draw, 1 common MDFC.
        double target = KarstenManabase.SingletonLandTarget(
            totalCards: 100,
            commanderCount: 1,
            averageManaValue: 2.59,
            rampAndDrawUnderThree: 6,
            mdfcCommon: 1);

        Assert.InRange(target, 36.0, 38.0);
    }

    [Fact]
    public void SourcesNeeded_SinglePip_SixtyCard_IsAroundFourteen()
    {
        // Karsten's canonical 60-card single-pip one-drop ≈ 14 sources.
        int need = KarstenManabase.SourcesNeeded(deckSize: 60, totalLands: 24, pips: 1, manaValue: 1);
        Assert.InRange(need, 13, 16);
    }

    [Fact]
    public void SourcesNeeded_DoublePip_IsHarderThanSinglePip()
    {
        int single = KarstenManabase.SourcesNeeded(60, 24, pips: 1, manaValue: 2);
        int doublePip = KarstenManabase.SourcesNeeded(60, 24, pips: 2, manaValue: 2);
        Assert.True(doublePip > single, $"double-pip {doublePip} should exceed single-pip {single}");
    }

    [Fact]
    public void Analyze_BuffsByHans_LandCountOk_ColorLimited_BlueStrained()
    {
        ManabaseDeck deck = BuildBuffsByHans();

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        // Land count roughly OK and color-limited (matches the Snail cross-check).
        Assert.Equal(36, report.ActualLands);
        Assert.InRange(report.TargetLands, 36.0, 38.0);
        Assert.NotNull(report.WeakestColor);
        Assert.False(report.WeakestColor!.IsAdequate);

        // COLOR-AGG-01 change: the headline weakest color is now the tail-risk composite, not the
        // raw deficit. The single hardest card is Sunder Shaman (RRGG gold, ~14% cast), so Red
        // leads — but Blue is still surfaced as a real deficit (Surrakar/Selkie UU are strained).
        ColorSourceFinding? blue = report.ColorFindings.FirstOrDefault(f => f.Color == ManaColor.Blue);
        Assert.NotNull(blue);
        Assert.False(blue!.IsAdequate);
    }

    [Fact]
    public void Analyze_TurnOnePip_CountsOnlyUntappedSources()
    {
        // Green one-drop; two green lands but one enters tapped → only 1 untapped source.
        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 1.0,
            Sources = new List<ManaSource>
            {
                new() { Name = "Forest", Produces = new[] { ManaColor.Green }, EntersUntapped = true },
                new() { Name = "Tapped Dual", Produces = new[] { ManaColor.Green }, EntersUntapped = false },
            },
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Llanowar Elves", ManaValue = 1, Pips = Pip((ManaColor.Green, 1)) },
            },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);
        ColorSourceFinding green = Assert.Single(report.ColorFindings);

        // MEDIUM-4: ActualSources is now the color's FULL weighted supply (both green lands = 2.0),
        // not the driver's turn-specific untapped number. The untapped-only restriction still
        // drives the one-drop's requirement internally — here it leaves a positive deficit because
        // only 1 of the 2 sources is available on turn 1.
        Assert.Equal(2.0, green.ActualSources);
        Assert.True(green.RequiredSources >= 1);
    }

    [Fact]
    public void MdfcCountsLowerTheLandTarget()
    {
        double withoutMdfc = KarstenManabase.SingletonLandTarget(100, 1, 3.0, 8);
        double withMdfc = KarstenManabase.SingletonLandTarget(100, 1, 3.0, 8, mdfcCommon: 4);

        // Four common MDFCs shave ~0.74 land each.
        Assert.True(withMdfc < withoutMdfc - 2.5, $"{withMdfc} should be well below {withoutMdfc}");
    }

    [Fact]
    public void Classify_TapsCountsMdfcBackFaces()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Bala Ged Recovery // Bala Ged Sanctuary",
                Quantity = 1,
                ManaCost = "{2}{G}",
                ManaValue = 3,
                TypeLine = "Sorcery // Land",
                OracleText = "Return target permanent card... // Bala Ged Sanctuary enters the battlefield tapped.",
                ProducedMana = new[] { "G" },
                Rarity = "uncommon",
                Layout = "modal_dfc",
                HasLandFace = true,
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        Assert.Equal(1, deck.MdfcCommon);
        Assert.Equal(0, deck.MdfcMythic);
        // The land back counts as a partial (0.8) source, not a land slot.
        ManaSource back = Assert.Single(deck.Sources);
        Assert.False(back.IsLand);
        Assert.Equal(0.8, back.Weight, 2);
    }

    [Fact]
    public void Analyze_DefaultCasual_KeepsLandTargetIdentical_ToModeOverload()
    {
        ManabaseDeck deck = BuildBuffsByHans();

        ManabaseReport casualDefault = ManabaseAnalyzer.Analyze(deck);
        ManabaseReport casualExplicit = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual);

        // Default overload == explicit Casual: land target is a pure regression, byte-identical.
        Assert.Equal(casualDefault.TargetLands, casualExplicit.TargetLands);
        Assert.Equal(ManabaseMode.Casual, casualDefault.Mode);
    }

    [Fact]
    public void Analyze_CedhMode_LowersLandTarget_AndRecordsMode()
    {
        ManabaseDeck deck = BuildBuffsByHans();

        ManabaseReport casual = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual);
        ManabaseReport cedh = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Cedh);

        Assert.True(cedh.TargetLands < casual.TargetLands, "cEDH target should be lower than casual");
        Assert.True(cedh.TargetLands >= 28.0, "cEDH target never below the 28 floor");
        Assert.Equal(ManabaseMode.Cedh, cedh.Mode);
        Assert.Contains("cEDH", cedh.Summary);
    }

    [Fact]
    public void Analyze_CastabilityList_IsNonEmpty_AndSortedAscending_NoRocksOrDorks()
    {
        ManabaseDeck deck = BuildBuffsByHans();

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.NotEmpty(report.Castability);
        // Every non-source spell produces exactly one row (rocks/dorks are excluded elsewhere).
        int expected = deck.Spells.Count(s => !s.IsManaSource || s.IsCommander);
        Assert.Equal(expected, report.Castability.Count);
        // Non-commander rows ascend by cast %.
        var nonCommander = report.Castability.Where(c => !c.IsCommander).Select(c => c.CastPercent).ToList();
        for (int i = 1; i < nonCommander.Count; i++)
        {
            Assert.True(nonCommander[i] >= nonCommander[i - 1], "castability rows must ascend by %");
        }
    }

    [Fact]
    public void Analyze_SingleUncastableBomb_StillSurfacesViaWorstSpell_DespiteHealthyMean()
    {
        // A color with many easy 1-drops (healthy mean) plus one CCC bomb that is starved.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 40; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }

        // Only 4 black sources — a BBB bomb is brutal, but the white spells are all fine.
        for (int i = 0; i < 4; i++)
        {
            sources.Add(new ManaSource { Name = "Swamp", Produces = new[] { ManaColor.Black } });
        }

        var spells = new List<SpellRequirement>
        {
            new() { Name = "Bomb BBB", ManaValue = 5, Pips = Pip((ManaColor.Black, 3)) },
        };
        for (int i = 0; i < 8; i++)
        {
            spells.Add(new SpellRequirement { Name = $"Easy White {i}", ManaValue = 1, Pips = Pip((ManaColor.White, 1)) });
        }

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = spells,
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.NotNull(report.WeakestColor);
        Assert.Equal(ManaColor.Black, report.WeakestColor!.Color);
        Assert.True(report.WeakestColor.UnderSupportedCount >= 1);
        Assert.Equal("Bomb BBB", report.WeakestColor.WorstSpell);
        Assert.Contains("Bomb BBB", report.Summary);
    }

    [Fact]
    public void Analyze_ColorUnderSupportedByComposite_ButNoRawDeficit_StillWeakest_AndUnhealthy()
    {
        // HIGH-1 guard: a color with ENOUGH raw sources (deficit <= 0) but whose only spell is a
        // high-MV double-pip bomb has a low overall CastPercent (mana-quantity risk drags it under
        // the 80% mode bar). The composite must still flag it as WeakestColor and make IsHealthy
        // false — the verdict must NOT revert to raw source deficit.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 6.0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Big Blue Bomb", ManaValue = 6, Pips = Pip((ManaColor.Blue, 2)) },
            },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        ColorSourceFinding blue = Assert.Single(report.ColorFindings);
        // Raw color sources cover the requirement: no source deficit.
        Assert.True(blue.Deficit <= 0, $"expected no raw deficit, got {blue.Deficit}");
        // But the bomb falls below the mode threshold, so the composite flags it.
        Assert.True(blue.UnderSupportedCount >= 1, "the high-MV bomb must count as under-supported");
        Assert.True(blue.IsCompositeProblem);

        // The verdict keys off the composite, not the raw deficit.
        Assert.NotNull(report.WeakestColor);
        Assert.Equal(ManaColor.Blue, report.WeakestColor!.Color);
        Assert.False(report.IsHealthy);
    }

    [Fact]
    public void Analyze_CostReducer_ShiftsColorFinding_ToReducedOnCurveTurn()
    {
        // HIGH-2 guard: a 5-MV instant with a valid {1}-less reducer is evaluated at OnCurveTurn 4
        // for BOTH its castability row AND its color finding. The color's RequiredSources must be
        // computed from the reduced turn (4), NOT the printed mana value (5) — matching the table.
        // (An earlier on-curve turn legitimately needs a DIFFERENT source count: fewer cards seen
        // by turn 4 at a higher consistency fraction, so the two values diverge — proving the
        // finding keys off OnCurveTurn rather than ManaValue.)
        ManabaseDeck withReducer = BuildReducerDeck(withReducer: true);

        ManabaseReport reducedReport = ManabaseAnalyzer.Analyze(withReducer);

        CardCastability reducedRow = reducedReport.Castability.Single(c => c.Name == "Big Spell");
        Assert.Equal(4, reducedRow.OnCurveTurn);

        ColorSourceFinding reducedBlue = reducedReport.ColorFindings.Single(f => f.Color == ManaColor.Blue);
        Assert.Equal("Big Spell", reducedBlue.DrivingSpell);

        // 36 Islands → librarySize 99, totalLands 36, single blue pip. RequiredSources must match
        // SourcesNeeded at the REDUCED turn (4), and differ from the printed-turn (5) value.
        int needAtReducedTurn = KarstenManabase.SourcesNeeded(deckSize: 99, totalLands: 36, pips: 1, manaValue: 4);
        int needAtPrintedTurn = KarstenManabase.SourcesNeeded(deckSize: 99, totalLands: 36, pips: 1, manaValue: 5);
        Assert.Equal(needAtReducedTurn, reducedBlue.RequiredSources);
        Assert.NotEqual(needAtPrintedTurn, reducedBlue.RequiredSources);
    }

    [Fact]
    public void Analyze_CostReducer_ShiftsEffectiveTurnEarlier_AndRaisesCastability()
    {
        ManabaseDeck withReducer = BuildReducerDeck(withReducer: true);
        ManabaseDeck without = BuildReducerDeck(withReducer: false);

        CardCastability reduced = ManabaseAnalyzer.Analyze(withReducer).Castability.Single(c => c.Name == "Big Spell");
        CardCastability normal = ManabaseAnalyzer.Analyze(without).Castability.Single(c => c.Name == "Big Spell");

        Assert.Equal(5, normal.OnCurveTurn);
        Assert.Equal(4, reduced.OnCurveTurn);                  // {1} less, capped, MV gate satisfied
        Assert.True(reduced.CastPercent > normal.CastPercent); // earlier turn → higher cast %
    }

    [Fact]
    public void Analyze_Granter_DoesNotFlipWeakestColor_OnDorkHeavyShell()
    {
        // Mono-green dork shell + a Cryptolith Rite granter. Weakest color must stay green (the
        // only demanded color); the any-color grant must not invent a spurious worse color.
        var cards = new List<CardFact>
        {
            new() { Name = "Cryptolith Rite", Quantity = 1, ManaCost = "{1}{G}", ManaValue = 2, TypeLine = "Enchantment", OracleText = "Creatures you control have \"{T}: Add one mana of any color.\"", ProducedMana = System.Array.Empty<string>() },
            new() { Name = "Craterhoof", Quantity = 1, ManaCost = "{5}{G}{G}{G}", ManaValue = 8, TypeLine = "Creature — Beast", OracleText = "Haste.", ProducedMana = System.Array.Empty<string>() },
        };
        for (int i = 0; i < 30; i++)
        {
            cards.Add(new CardFact { Name = "Forest", Quantity = 1, TypeLine = "Basic Land — Forest", OracleText = "{T}: Add {G}.", ProducedMana = new[] { "G" }, ManaValue = 0, HasLandFace = true });
        }

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);
        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        if (report.WeakestColor is not null)
        {
            Assert.Equal(ManaColor.Green, report.WeakestColor.Color);
        }
    }

    [Fact]
    public void Analyze_StandardCommander_DoesNotOverride_AWorseNonCommanderColor()
    {
        // Commander is WU and very well supported; an off-commander black bomb is the true worst.
        ManabaseDeck deck = BuildCommanderVsBombDeck();

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard);

        Assert.NotNull(report.WeakestColor);
        Assert.Equal(ManaColor.Black, report.WeakestColor!.Color); // non-commander color still wins
    }

    [Fact]
    public void Analyze_CentralVsLow_CommanderImportance_ChangesVerdict_NotLandTarget()
    {
        // Commander colors are under-supported; Central tightens them, Low relaxes.
        ManabaseDeck deck = BuildStrainedCommanderDeck();

        ManabaseReport central = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Central);
        ManabaseReport low = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Low);

        // Importance is orthogonal to mode — land target identical across both.
        Assert.Equal(central.TargetLands, low.TargetLands);

        // Central holds commander colors to a stricter bar → more under-supported there.
        ColorSourceFinding? centralBlue = central.ColorFindings.FirstOrDefault(f => f.Color == ManaColor.Blue);
        ColorSourceFinding? lowBlue = low.ColorFindings.FirstOrDefault(f => f.Color == ManaColor.Blue);
        Assert.NotNull(centralBlue);
        Assert.NotNull(lowBlue);
        Assert.True(centralBlue!.UnderSupportedCount >= lowBlue!.UnderSupportedCount);
    }

    [Fact]
    public void Analyze_CasualCentral_vs_CedhStandard_ProduceDistinctVerdicts()
    {
        ManabaseDeck deck = BuildStrainedCommanderDeck();

        ManabaseReport casualCentral = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Central);
        ManabaseReport cedhStandard = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Cedh, CommanderImportance.Standard);

        // Casual+Central keeps the casual land target; cEDH lowers it. Distinct land verdicts.
        Assert.True(cedhStandard.TargetLands < casualCentral.TargetLands);
        Assert.Equal(ManabaseMode.Casual, casualCentral.Mode);
        Assert.Equal(ManabaseMode.Cedh, cedhStandard.Mode);
    }

    [Fact]
    public void Analyze_LandTargetBreakdown_PopulatedWithDeckTerms_AndSumsToTarget()
    {
        // FORMULA-01 (MEDIUM-2): the additive breakdown carries this deck's real regression inputs
        // and its terms reproduce TargetLands exactly (no recomputation drift).
        ManabaseDeck deck = BuildBuffsByHans();

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual);

        Assert.NotNull(report.LandTarget);
        ManabaseLandTargetBreakdown lt = report.LandTarget;

        // Inputs echo the deck verbatim.
        Assert.Equal(2.59, lt.AverageManaValue, 3);
        Assert.Equal(6, lt.RampAndDrawUnderThree);
        Assert.Equal(0, lt.FastMana);
        Assert.Equal(0, lt.MdfcCommon);
        Assert.Equal(0, lt.MdfcMythic);
        Assert.Equal(1, lt.CommanderCount);
        Assert.Equal(99, lt.LibrarySize); // 100 cards − 1 commander

        // Casual: base == final == the reported target, and no cEDH adjustment.
        Assert.Equal(report.TargetLands, lt.FinalTarget);
        Assert.Equal(lt.BaseTarget, lt.FinalTarget);
        Assert.Equal(0.0, lt.CedhAdjustment);

        // The rendered formula reconstructs FinalTarget from the surfaced terms.
        double scale = lt.LibrarySize / 60.0;
        double reconstructed = (scale * (19.59 + (1.90 * lt.AverageManaValue) + (0.27 * lt.CommanderCount)))
            - (0.28 * lt.RampAndDrawUnderThree)
            - lt.FastMana
            - (0.74 * lt.MdfcCommon)
            - (0.38 * lt.MdfcMythic)
            - 1.35;
        Assert.Equal(lt.FinalTarget, reconstructed, 6);
    }

    [Fact]
    public void Analyze_CedhBreakdown_RecordsFlooredAdjustment_FromBaseToFinal()
    {
        // The cEDH adjustment is the signed delta from the singleton base to the floored final
        // target, so the panel can show "−3.5 (floor 28)" honestly.
        ManabaseDeck deck = BuildBuffsByHans();

        ManabaseReport casual = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual);
        ManabaseReport cedh = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Cedh);

        Assert.NotNull(cedh.LandTarget);
        ManabaseLandTargetBreakdown lt = cedh.LandTarget;

        // Base equals the casual (singleton) target; final equals the reported cEDH target.
        Assert.Equal(casual.TargetLands, lt.BaseTarget, 6);
        Assert.Equal(cedh.TargetLands, lt.FinalTarget, 6);
        Assert.Equal(lt.FinalTarget - lt.BaseTarget, lt.CedhAdjustment, 6);
        Assert.True(lt.CedhAdjustment < 0, "cEDH must lower the target");
    }

    private static ManabaseDeck BuildReducerDeck(bool withReducer)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 36; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        var spells = new List<SpellRequirement>
        {
            new()
            {
                Name = "Big Spell",
                ManaValue = 5,
                Pips = Pip((ManaColor.Blue, 1)),
                Kinds = SpellKinds.Instant,
            },
        };

        var reducers = withReducer
            ? new List<CostReducer> { new() { GenericReduction = 1, Scope = ReductionScope.InstantSorcery, SourceManaValue = 2 } }
            : new List<CostReducer>();

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 3.0,
            Sources = sources,
            Spells = spells,
            CostReduction = reducers,
        };
    }

    private static ManabaseDeck BuildCommanderVsBombDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }

        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        // Only 3 black sources for a brutal BB bomb.
        for (int i = 0; i < 3; i++)
        {
            sources.Add(new ManaSource { Name = "Swamp", Produces = new[] { ManaColor.Black } });
        }

        var spells = new List<SpellRequirement>
        {
            new() { Name = "Brago", ManaValue = 4, Pips = Pip((ManaColor.White, 1), (ManaColor.Blue, 1)), IsGold = true, IsCommander = true },
            new() { Name = "Black Bomb", ManaValue = 4, Pips = Pip((ManaColor.Black, 2)) },
        };

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 3.0,
            Sources = sources,
            Spells = spells,
        };
    }

    private static ManabaseDeck BuildStrainedCommanderDeck()
    {
        // A WU commander with thin blue support so Central vs Low diverges on blue.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 22; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }

        for (int i = 0; i < 9; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        var spells = new List<SpellRequirement>
        {
            new() { Name = "Brago", ManaValue = 4, Pips = Pip((ManaColor.White, 1), (ManaColor.Blue, 1)), IsGold = true, IsCommander = true },
            new() { Name = "Blue Spell", ManaValue = 3, Pips = Pip((ManaColor.Blue, 1)) },
            new() { Name = "White Spell", ManaValue = 2, Pips = Pip((ManaColor.White, 1)) },
        };

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 3.0,
            Sources = sources,
            Spells = spells,
        };
    }

    /// <summary>
    /// Minimal classified model of the real deck: 36 lands with the actual per-color
    /// supply (G 18 / U 15 / R 15 including tri-lands, duals and fetches), plus the
    /// double-pip spells that strain each color.
    /// </summary>
    private static ManabaseDeck BuildBuffsByHans()
    {
        var sources = new List<ManaSource>();
        void AddLands(string name, int count, params ManaColor[] colors)
        {
            for (int i = 0; i < count; i++)
            {
                sources.Add(new ManaSource { Name = name, Produces = colors });
            }
        }

        // Basics
        AddLands("Forest", 10, ManaColor.Green);
        AddLands("Island", 6, ManaColor.Blue);
        AddLands("Mountain", 6, ManaColor.Red);
        // Tri / any-color
        AddLands("Command Tower", 1, ManaColor.Blue, ManaColor.Red, ManaColor.Green);
        AddLands("Frontier Bivouac", 1, ManaColor.Blue, ManaColor.Red, ManaColor.Green);
        // Duals (Karluk lands, temples, guildgates, bounce)
        AddLands("Simic Growth Chamber", 1, ManaColor.Blue, ManaColor.Green);
        AddLands("Gruul Turf", 1, ManaColor.Red, ManaColor.Green);
        AddLands("Izzet Boilerworks", 1, ManaColor.Blue, ManaColor.Red);
        AddLands("Temple of Epiphany", 1, ManaColor.Blue, ManaColor.Red);
        AddLands("Temple of Abandon", 1, ManaColor.Red, ManaColor.Green);
        AddLands("Temple of Mystery", 1, ManaColor.Blue, ManaColor.Green);
        AddLands("Izzet Guildgate", 1, ManaColor.Blue, ManaColor.Red);
        AddLands("Gruul Guildgate", 1, ManaColor.Red, ManaColor.Green);
        AddLands("Simic Guildgate", 1, ManaColor.Blue, ManaColor.Green);
        AddLands("Kessig Wolf Run", 1, ManaColor.Red);
        // Fetches — count as all three at full weight here (basic fetch in a tri deck).
        AddLands("Evolving Wilds", 1, ManaColor.Blue, ManaColor.Red, ManaColor.Green);
        AddLands("Terramorphic Expanse", 1, ManaColor.Blue, ManaColor.Red, ManaColor.Green);

        var spells = new List<SpellRequirement>
        {
            // Blue double-pip — the strain the tool flagged (Surrakar Spellblade 1UU).
            new() { Name = "Surrakar Spellblade", ManaValue = 3, Pips = Pip((ManaColor.Blue, 2)) },
            new() { Name = "Cold-Eyed Selkie", ManaValue = 3, Pips = Pip((ManaColor.Blue, 2)) },
            // Green double-pip.
            new() { Name = "Ohran Viper", ManaValue = 3, Pips = Pip((ManaColor.Green, 2)) },
            // Red double-pip in a gold cost (Sunder Shaman RRGG).
            new() { Name = "Sunder Shaman", ManaValue = 4, Pips = Pip((ManaColor.Red, 2), (ManaColor.Green, 2)), IsGold = true },
            new() { Name = "Neheb, Dreadhorde Champion", ManaValue = 4, Pips = Pip((ManaColor.Red, 2)) },
        };

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            Sources = sources,
            Spells = spells,
            AverageManaValue = 2.59,
            RampAndDrawUnderThree = 6,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck SingleSpellDeck(SpellRequirement spell, ManaColor sourceColor)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 20; i++)
        {
            sources.Add(new ManaSource { Name = "Src", Produces = new[] { sourceColor } });
        }

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 3.0,
            Sources = sources,
            Spells = new List<SpellRequirement> { spell },
        };
    }

    [Fact]
    public void Analyze_FreeOverride_ZeroesMv_DropsColorDriver_AndMarksRow()
    {
        var spell = new SpellRequirement { Name = "Force of Will", ManaValue = 5, Pips = Pip((ManaColor.Blue, 2)) };
        ManabaseDeck deck = SingleSpellDeck(spell, ManaColor.Blue);
        var overrides = new Dictionary<string, string> { ["Force of Will"] = "0" };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard, overrides);

        CardCastability row = report.Castability.Single(c => c.Name == "Force of Will");
        Assert.Equal(0, row.ManaValue);
        Assert.True(row.IsCostOverridden);
        Assert.True(row.CastPercent >= 95, $"free spell should be ~always castable, got {row.CastPercent}");
        // HIGH-2: the freed spell must no longer drive any color finding.
        Assert.All(report.ColorFindings, f => Assert.NotEqual("Force of Will", f.DrivingSpell));
    }

    [Fact]
    public void Analyze_ColoredOverride_KeepsColorPip_AndLowersMv()
    {
        var spell = new SpellRequirement { Name = "Blasphemous Act", ManaValue = 9, Pips = Pip((ManaColor.Red, 1)) };
        ManabaseDeck deck = SingleSpellDeck(spell, ManaColor.Red);
        var overrides = new Dictionary<string, string> { ["Blasphemous Act"] = "{R}" };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard, overrides);

        CardCastability row = report.Castability.Single(c => c.Name == "Blasphemous Act");
        Assert.Equal(1, row.ManaValue);
        Assert.Equal(1, row.OnCurveTurn);
        Assert.True(row.IsCostOverridden);
        // Still demands red (MV 1 >= 1 pip): red remains a finding.
        Assert.Contains(report.ColorFindings, f => f.Color == ManaColor.Red);
    }

    [Fact]
    public void Analyze_NoOverride_IsUnchanged()
    {
        var spell = new SpellRequirement { Name = "Counterspell", ManaValue = 2, Pips = Pip((ManaColor.Blue, 2)) };
        ManabaseDeck deck = SingleSpellDeck(spell, ManaColor.Blue);

        CardCastability baseline = ManabaseAnalyzer.Analyze(deck).Castability.Single(c => c.Name == "Counterspell");
        CardCastability withNull = ManabaseAnalyzer
            .Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard, null)
            .Castability.Single(c => c.Name == "Counterspell");

        Assert.Equal(baseline.ManaValue, withNull.ManaValue);
        Assert.Equal(baseline.CastPercent, withNull.CastPercent);
        Assert.False(withNull.IsCostOverridden);
    }

    [Fact]
    public void Analyze_Override_MatchesCaseInsensitively()
    {
        var spell = new SpellRequirement { Name = "Force of Will", ManaValue = 5, Pips = Pip((ManaColor.Blue, 2)) };
        ManabaseDeck deck = SingleSpellDeck(spell, ManaColor.Blue);
        var overrides = new Dictionary<string, string> { ["force of will"] = "0" };

        CardCastability row = ManabaseAnalyzer
            .Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard, overrides)
            .Castability.Single(c => c.Name == "Force of Will");

        Assert.Equal(0, row.ManaValue);
        Assert.True(row.IsCostOverridden);
    }

    [Fact]
    public void Analyze_Override_WinsOnReducerDeck()
    {
        // Big Spell is printed MV5 with a deck {1} reducer (→ turn 4 without an override). An
        // override to MV2 must take over: effective MV 2, and the turn is no worse than 2.
        ManabaseDeck withReducer = BuildReducerDeck(withReducer: true);
        var overrides = new Dictionary<string, string> { ["Big Spell"] = "{1}{U}" };

        CardCastability row = ManabaseAnalyzer
            .Analyze(withReducer, ManabaseMode.Casual, CommanderImportance.Standard, overrides)
            .Castability.Single(c => c.Name == "Big Spell");

        Assert.True(row.IsCostOverridden);
        Assert.Equal(2, row.ManaValue);
        Assert.True(row.OnCurveTurn <= 2, $"override base + reducer must not exceed turn 2, got {row.OnCurveTurn}");
    }

    [Fact]
    public void Mulligan_SingletonFreeMull_RaisesCastability_VsStandardLondon()
    {
        // Same deck and spell; only IsSingleton differs. Commander's free first mulligan can only
        // help a colour-tight card (more chances at a keepable hand without bottoming), so the
        // singleton cast% must be >= the standard-London cast%.
        var spell = new SpellRequirement { Name = "WW Two-Drop", ManaValue = 2, Pips = Pip((ManaColor.White, 2)) };
        var sources = new List<ManaSource>();
        for (int i = 0; i < 18; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }
        for (int i = 0; i < 18; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        ManabaseDeck Mk(bool singleton) => new()
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.5,
            Sources = sources,
            Spells = new List<SpellRequirement> { spell },
            IsSingleton = singleton,
        };

        int singletonPct = ManabaseAnalyzer.Analyze(Mk(true)).Castability.Single(c => c.Name == "WW Two-Drop").CastPercent;
        int standardPct = ManabaseAnalyzer.Analyze(Mk(false)).Castability.Single(c => c.Name == "WW Two-Drop").CastPercent;

        Assert.True(singletonPct >= standardPct, $"singleton {singletonPct} should be >= standard London {standardPct}");
    }

    [Fact]
    public void Mulligan_IsDeterministic_AcrossRuns()
    {
        var spell = new SpellRequirement { Name = "WW Two-Drop", ManaValue = 2, Pips = Pip((ManaColor.White, 2)) };
        ManabaseDeck deck = SingleSpellDeck(spell, ManaColor.White);

        int first = ManabaseAnalyzer.Analyze(deck).Castability.Single(c => c.Name == "WW Two-Drop").CastPercent;
        int second = ManabaseAnalyzer.Analyze(deck).Castability.Single(c => c.Name == "WW Two-Drop").CastPercent;

        Assert.Equal(first, second);
    }

    private static IReadOnlyDictionary<ManaColor, int> Pip(params (ManaColor Color, int Count)[] pips)
        => pips.ToDictionary(p => p.Color, p => p.Count);
}
