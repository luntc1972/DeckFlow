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
    public void BuildCompanionSpell_AddsTaxAndPreservesPrintedMonoColorShape()
    {
        ParsedManaCost printedCost = ManaCostParser.Parse("{1}{U}");

        SpellRequirement companion = ManabaseAnalyzer.BuildCompanionSpell("Gyruda, Doom of Depths", printedCost, 2);

        Assert.Equal("Gyruda, Doom of Depths", companion.Name);
        Assert.Equal(5, companion.ManaValue);
        Assert.False(companion.IsGold);
        Assert.False(companion.IsCommander);
        Assert.Equal(printedCost.Pips, companion.Pips);
    }

    [Fact]
    public void BuildCompanionSpell_TwoColorPrintedCost_IsGold()
    {
        ParsedManaCost printedCost = ManaCostParser.Parse("{W}{U}");

        SpellRequirement companion = ManabaseAnalyzer.BuildCompanionSpell("Yorion, Sky Nomad", printedCost, 2);

        Assert.True(companion.IsGold);
    }

    [Fact]
    public void BuildCompanionSpell_ClampsAdversarialPrintedManaValueBeforeTax()
    {
        ParsedManaCost printedCost = ManaCostParser.Parse("{5}{B}");

        SpellRequirement companion = ManabaseAnalyzer.BuildCompanionSpell("Jegantha, the Wellspring", printedCost, 25);

        Assert.Equal(23, companion.ManaValue);
    }

    [Fact]
    public void BuildCompanionSpell_ZeroPrintedManaValue_StillPaysTax()
    {
        ParsedManaCost printedCost = ManaCostParser.Parse("{G}");

        SpellRequirement companion = ManabaseAnalyzer.BuildCompanionSpell("Kaheera, the Orphanguard", printedCost, 0);

        Assert.Equal(3, companion.ManaValue);
    }

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
        // Buffs by Hans: 100 cards, 1 commander, avgMV 2.59, ~6 cheap ramp/draw.
        double target = KarstenManabase.SingletonLandTarget(
            totalCards: 100,
            commanderCount: 1,
            averageManaValue: 2.59,
            rampAndDrawUnderThree: 6);

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
    public void Analyze_CedhBaselineRangeFields_PopulateWhenBaselineHasMeanSdAndUsableSample()
    {
        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            IsSingleton = true,
            AverageManaValue = 2.0,
            Sources = Enumerable.Range(0, 28)
                .Select(i => new ManaSource
                {
                    Name = $"Plains {i}",
                    IsLand = true,
                    Produces = new[] { ManaColor.White },
                    EntersUntapped = true,
                })
                .ToList(),
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Winota, Joiner of Forces", ManaValue = 4, Pips = Pip((ManaColor.Red, 1), (ManaColor.White, 1)), IsCommander = true },
                new() { Name = "Esper Sentinel", ManaValue = 1, Pips = Pip((ManaColor.White, 1)) },
            },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck,
            ManabaseMode.Cedh,
            cedhContext: new CedhLandContext(27.5, 33, Enabled: true, BaselineSd: 1.6, BaselineMonth: "2026-07"));

        Assert.NotNull(report.TargetLandsRangeLow);
        Assert.NotNull(report.TargetLandsRangeHigh);
        Assert.Equal(33, report.BaselineDeckCount);
        Assert.NotNull(report.BaselineLandsMean);
        Assert.NotNull(report.BaselineLandsSd);
        Assert.Equal("2026-07", report.BaselineMonth);
        Assert.Equal(25.9, report.TargetLandsRangeLow.Value, 3);
        Assert.Equal(29.1, report.TargetLandsRangeHigh.Value, 3);
        Assert.Equal(27.5, report.BaselineLandsMean.Value, 3);
        Assert.Equal(1.6, report.BaselineLandsSd.Value, 3);
        Assert.Equal(22.0, report.LandTarget!.CedhSafetyFloor);
        Assert.True(report.LandTarget.CedhBaselineBlended);
    }

    [Theory]
    [InlineData(true, 9, 27.5, 1.6)]
    [InlineData(true, 33, 27.5, null)]
    [InlineData(false, 33, 27.5, 1.6)]
    public void Analyze_CedhBaselineRangeFields_StayNullWhenBaselineUnavailable(
        bool enabled,
        int baselineN,
        double? baselineMean,
        double? baselineSd)
    {
        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            IsSingleton = true,
            AverageManaValue = 2.0,
            Sources = Enumerable.Range(0, 28)
                .Select(i => new ManaSource
                {
                    Name = $"Plains {i}",
                    IsLand = true,
                    Produces = new[] { ManaColor.White },
                    EntersUntapped = true,
                })
                .ToList(),
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Winota, Joiner of Forces", ManaValue = 4, Pips = Pip((ManaColor.Red, 1), (ManaColor.White, 1)), IsCommander = true },
                new() { Name = "Esper Sentinel", ManaValue = 1, Pips = Pip((ManaColor.White, 1)) },
            },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck,
            ManabaseMode.Cedh,
            cedhContext: new CedhLandContext(baselineMean, baselineN, Enabled: enabled, BaselineSd: baselineSd));

        Assert.Null(report.TargetLandsRangeLow);
        Assert.Null(report.TargetLandsRangeHigh);
        Assert.Null(report.BaselineDeckCount);
        Assert.Null(report.BaselineLandsMean);
        Assert.Null(report.BaselineLandsSd);
        Assert.Null(report.BaselineMonth);
        Assert.Equal(enabled ? 22.0 : 28.0, report.LandTarget!.CedhSafetyFloor);
        Assert.False(report.LandTarget.CedhBaselineBlended);
    }

    [Fact]
    public void UnmatchedOverrideNames_ReturnsOnlyKeysThatBindNoSpell()
    {
        // MEDIUM-11: a case-insensitive / normalized match counts as applied; a name no spell
        // matches is reported so the UI can surface it instead of dropping it silently.
        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = new List<ManaSource>(),
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Blasphemous Act", ManaValue = 8, Pips = Pip((ManaColor.Red, 1)) },
                new() { Name = "Force of Will", ManaValue = 5, Pips = Pip((ManaColor.Blue, 2)) },
            },
        };

        var overrides = new Dictionary<string, string>
        {
            ["blasphemous act"] = "{R}", // case-insensitive exact match -> applied
            ["Force of Will"] = "0",     // exact match -> applied
            ["Totally Fake Card"] = "0", // matches nothing -> unmatched
        };

        IReadOnlyList<string> unmatched = ManabaseAnalyzer
            .Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard, overrides)
            .UnmatchedOverrideNames;

        Assert.Equal(new[] { "Totally Fake Card" }, unmatched);
    }

    [Fact]
    public void UnmatchedOverrideNames_NullOrEmpty_ReturnsEmpty()
    {
        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = new List<ManaSource>(),
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Sol Ring", ManaValue = 1, Pips = Pip((ManaColor.Colorless, 1)) },
            },
        };

        Assert.Empty(ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard, null).UnmatchedOverrideNames);
        Assert.Empty(ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, CommanderImportance.Standard, new Dictionary<string, string>()).UnmatchedOverrideNames);
    }

    [Fact]
    public void Classify_MdfcBackFace_IsRealTappedLand()
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
                LandFaceOracleText = "Bala Ged Sanctuary enters the battlefield tapped.",
                ProducedMana = new[] { "G" },
                Rarity = "uncommon",
                Layout = "modal_dfc",
                HasLandFace = true,
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        // The land back is a real land (full weight 1.0), entering tapped per its land face.
        ManaSource back = Assert.Single(deck.Sources);
        Assert.True(back.IsLand);
        Assert.False(back.EntersUntapped);
        Assert.Equal(1.0, back.Weight, 2);
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
    public void BuildSummary_FractionalLandShortfall_UsesRoundedCount_AndNoPluralArtifact()
    {
        var findings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.Blue,
                ActualSources = 20.0,
                RequiredSources = 18,
                DrivingSpell = "Counterspell",
            },
        };
        var castability = new List<CardCastability>
        {
            new()
            {
                Name = "Counterspell",
                ManaValue = 2,
                OnCurveTurn = 2,
                CastPercent = 88,
                LimitingFactor = "color:U",
            },
        };
        var colorSpellCounts = new Dictionary<ManaColor, int> { [ManaColor.Blue] = 4 };

        string summary = (string)typeof(ManabaseAnalyzer)
            .GetMethod("BuildSummary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [36, 37.05, findings, castability, colorSpellCounts, ManabaseMode.Casual, CommanderImportance.Standard])!;

        Assert.Contains("Lands: 36 vs ~37.0 target (add ~1 land).", summary);
        Assert.DoesNotContain("(s)", summary, StringComparison.Ordinal);
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

        // RequiredSources is now the mulligan-aware sim figure for the driver at its REDUCED on-curve
        // turn (4, asserted above) — a sane positive count, not the old mulligan-blind hypergeometric.
        // With 36 Islands the single blue pip is comfortably covered, so there is no deficit.
        Assert.InRange(reducedBlue.RequiredSources, 1, 36);
        Assert.True(reducedBlue.Deficit <= 0, $"36 blue sources should cover a single blue pip; deficit {reducedBlue.Deficit}");
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
    public void Analyze_RampSourceCount_CountsOnlyRocksAndDorks_NotGrantedOrMdfcBacks()
    {
        // The at-a-glance "N mana rock(s)/dork(s)" count must count only genuine rocks/dorks — not
        // a vanilla creature handed a mana ability by a granter (Cryptolith Rite), and not an MDFC
        // land-back (a land, not a ramp piece). Here: Sol Ring + Llanowar Elves = 2.
        var cards = new List<CardFact>
        {
            new() { Name = "Sol Ring", Quantity = 1, ManaCost = "{1}", ManaValue = 1, TypeLine = "Artifact", OracleText = "{T}: Add {C}{C}.", ProducedMana = new[] { "C" } },
            new() { Name = "Llanowar Elves", Quantity = 1, ManaCost = "{G}", ManaValue = 1, TypeLine = "Creature — Elf Druid", OracleText = "{T}: Add {G}.", ProducedMana = new[] { "G" } },
            new() { Name = "Cryptolith Rite", Quantity = 1, ManaCost = "{1}{G}", ManaValue = 2, TypeLine = "Enchantment", OracleText = "Creatures you control have \"{T}: Add one mana of any color.\"", ProducedMana = System.Array.Empty<string>() },
            new() { Name = "Craterhoof Behemoth", Quantity = 1, ManaCost = "{5}{G}{G}{G}", ManaValue = 8, TypeLine = "Creature — Beast", OracleText = "Haste.", ProducedMana = System.Array.Empty<string>() },
            new() { Name = "Boseiju, Who Endures", Quantity = 1, ManaCost = "{1}{G}", ManaValue = 2, TypeLine = "Sorcery // Legendary Land", OracleText = "Destroy target artifact.\n{T}: Add {G}.", ProducedMana = new[] { "G" }, HasLandFace = true },
        };
        for (int i = 0; i < 30; i++)
        {
            cards.Add(new CardFact { Name = "Forest", Quantity = 1, TypeLine = "Basic Land — Forest", OracleText = "{T}: Add {G}.", ProducedMana = new[] { "G" }, ManaValue = 0, HasLandFace = true });
        }

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);
        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        // Sol Ring (rock) + Llanowar Elves (dork) only — Craterhoof gets a granted source but is not
        // itself a rock/dork; Boseiju's spell face has a land-back and is not a ramp piece.
        Assert.Equal(2, report.RampSourceCount);
    }

    [Fact]
    public void Analyze_SourceBreakdown_SplitsDirectSharedConditional_AndSumsToActual()
    {
        // Green sources of three kinds: a mono Forest (direct), a low-support Mox Opal (shared,
        // weight 0.40 after the conditional-Mox manabase pass), and a granted creature via
        // Cryptolith Rite (conditional, weight 0.25). The
        // breakdown must bucket each correctly and sum (within rounding) to ActualSources, which
        // itself is unchanged.
        var cards = new List<CardFact>
        {
            new() { Name = "Mox Opal", Quantity = 1, ManaCost = "{0}", ManaValue = 0, TypeLine = "Artifact", OracleText = "{T}: Add one mana of any color.", ProducedMana = new[] { "W", "U", "B", "R", "G" } },
            new() { Name = "Cryptolith Rite", Quantity = 1, ManaCost = "{1}{G}", ManaValue = 2, TypeLine = "Enchantment", OracleText = "Creatures you control have \"{T}: Add one mana of any color.\"", ProducedMana = System.Array.Empty<string>() },
            new() { Name = "Vanilla Bear", Quantity = 1, ManaCost = "{2}{G}", ManaValue = 3, TypeLine = "Creature — Bear", OracleText = "Vanilla.", ProducedMana = System.Array.Empty<string>() },
        };
        for (int i = 0; i < 30; i++)
        {
            cards.Add(new CardFact { Name = "Forest", Quantity = 1, TypeLine = "Basic Land — Forest", OracleText = "{T}: Add {G}.", ProducedMana = new[] { "G" }, ManaValue = 0, HasLandFace = true });
        }

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);
        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);
        ColorSourceFinding green = report.ColorFindings.Single(f => f.Color == ManaColor.Green);

        Assert.Equal(30.0, green.DirectSources, 1);                 // 30 Forests, weight 1.0 each
        Assert.Equal(0.40, green.SharedSources, 2);                 // Mox Opal (weak artifact support tier)
        Assert.Equal(0.25, green.ConditionalSources, 2);           // Vanilla Bear granted by the Rite
        Assert.Equal(
            green.ActualSources,
            Math.Round(green.DirectSources + green.SharedSources + green.ConditionalSources, 1),
            1);
    }

    [Fact]
    public void Analyze_CommanderOnlyColorSource_NotDrawnIntoLibrary_ButCountedInSupply()
    {
        // The commander is the deck's ONLY red source (a red dork). It starts in the command zone,
        // so the simulator must NOT draw it — meaning a red spell can never be cast in the sim and
        // its cast% is exactly 0 (the teeth: if the commander leaked into the library, red would be
        // drawable some games and cast% would be > 0). It MUST still count toward red supply, since
        // a commander mana source is reliably castable in real play.
        // Normal-sized ~99 deck so the mulligan doesn't pathologically bottom the lone dork, and a
        // {4}{R} target so raw mana stays short on the ramp-up turns — meaning if the commander dork
        // leaked into the library and were drawn, the sim WOULD deploy it (TryDeployRamp fires while
        // availableNow < cost) and red would become castable. With the fix it never enters the
        // library, so red is unreachable and cast% is exactly 0. (Teeth verified: disabling the fix
        // flips this to > 0.)
        var cards = new List<CardFact>
        {
            new() { Name = "Red Cmdr", Quantity = 1, IsCommander = true, ManaCost = "{G}", ManaValue = 1, TypeLine = "Legendary Creature — Elf Druid", OracleText = "{T}: Add {R}.", ProducedMana = new[] { "R" } },
            new() { Name = "Red Spell", Quantity = 1, ManaCost = "{4}{R}", ManaValue = 5, TypeLine = "Sorcery", OracleText = string.Empty, ProducedMana = System.Array.Empty<string>() },
            new() { Name = "Forest", Quantity = 36, TypeLine = "Basic Land — Forest", OracleText = "{T}: Add {G}.", ProducedMana = new[] { "G" }, ManaValue = 0, HasLandFace = true },
            new() { Name = "Filler", Quantity = 61, ManaCost = "{2}{G}", ManaValue = 3, TypeLine = "Creature — Bear", OracleText = string.Empty, ProducedMana = System.Array.Empty<string>() },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(ManabaseClassifier.Classify(cards));

        // Commander red source never drawn → no red in the simulated library → red spell uncastable.
        Assert.Equal(0, report.Castability.Single(c => c.Name == "Red Spell").CastPercent);

        // ...but red supply still counts the commander dork (0.5) — kept in the color count.
        ColorSourceFinding red = report.ColorFindings.Single(f => f.Color == ManaColor.Red);
        Assert.Equal(0.5, red.ActualSources, 1);
    }

    [Fact]
    public void Analyze_UnsupportedInteractions_SurfacesXAndHybridCards()
    {
        // X/variable spells are skipped from castability; hybrid/Phyrexian pips carry no hard color
        // requirement. Both must be DISCLOSED (MQ-04), not silently absorbed. A plain card is not.
        var cards = new List<CardFact>
        {
            new() { Name = "Hydra X", Quantity = 1, ManaCost = "{X}{G}{G}", ManaValue = 2, TypeLine = "Creature — Hydra", OracleText = string.Empty, ProducedMana = System.Array.Empty<string>() },
            new() { Name = "Hybrid Bolt", Quantity = 1, ManaCost = "{R/G}", ManaValue = 1, TypeLine = "Instant", OracleText = string.Empty, ProducedMana = System.Array.Empty<string>() },
            new() { Name = "Phyrexian Card", Quantity = 1, ManaCost = "{1}{G/P}", ManaValue = 2, TypeLine = "Instant", OracleText = string.Empty, ProducedMana = System.Array.Empty<string>() },
            new() { Name = "Plain Bear", Quantity = 1, ManaCost = "{1}{G}", ManaValue = 2, TypeLine = "Creature — Bear", OracleText = string.Empty, ProducedMana = System.Array.Empty<string>() },
        };
        for (int i = 0; i < 36; i++)
        {
            cards.Add(new CardFact { Name = "Forest", Quantity = 1, TypeLine = "Basic Land — Forest", OracleText = "{T}: Add {G}.", ProducedMana = new[] { "G" }, ManaValue = 0, HasLandFace = true });
        }

        ManabaseReport report = ManabaseAnalyzer.Analyze(ManabaseClassifier.Classify(cards));
        var names = report.UnsupportedInteractions.Select(u => u.Name).ToList();

        Assert.Contains("Hydra X", names);          // X cost
        Assert.Contains("Hybrid Bolt", names);      // hybrid pip
        Assert.Contains("Phyrexian Card", names);   // Phyrexian pip
        Assert.DoesNotContain("Plain Bear", names); // fully modeled — not disclosed
        Assert.DoesNotContain("Forest", names);
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
    public void SelectHeadlineSpell_CentralImportance_UsesWorstCommanderCastability()
    {
        var castability = new List<CardCastability>
        {
            new() { Name = "Partner A", ManaValue = 4, OnCurveTurn = 4, CastPercent = 80, LimitingFactor = "mana", IsCommander = true },
            new() { Name = "Partner B", ManaValue = 6, OnCurveTurn = 6, CastPercent = 30, LimitingFactor = "color:Blue", IsCommander = true },
            new() { Name = "Support Spell", ManaValue = 2, OnCurveTurn = 2, CastPercent = 10, LimitingFactor = "mana" },
        };

        CardCastability? headline = InvokeSelectHeadlineSpell(castability, CommanderImportance.Central);

        Assert.NotNull(headline);
        Assert.Equal("Partner B", headline!.Name);
        Assert.Equal(30, headline.CastPercent);
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
            - 1.35;
        Assert.Equal(lt.FinalTarget, reconstructed, 6);
    }

    [Fact]
    public void Analyze_CedhBreakdown_RecordsFlooredAdjustment_FromBaseToFinal()
    {
        // The cEDH adjustment is the signed delta from the singleton base to the floored/clamped
        // final target, so the panel can show the net cEDH adjustment honestly.
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

    [Theory]
    [InlineData(-1, 0.0)]
    [InlineData(0, 0.0)]
    [InlineData(1, 0.5)]
    [InlineData(7, 3.0)]
    public void RitualLandCreditAmount_MatchesExpectedCurve(int netPositiveRitualCount, double expected)
    {
        Assert.Equal(expected, KarstenManabase.RitualLandCreditAmount(netPositiveRitualCount), 6);
    }

    [Fact]
    public void Analyze_CedhBreakdown_PopulatesRitualLandCredit_WhenEnabledInSingletonCedh()
    {
        ManabaseDeck deck = BuildBuffsByHans() with
        {
            OneShots = new List<OneShotMana>
            {
                MakeOneShot("Dark Ritual"),
                MakeOneShot("Seething Song"),
            },
        };
        CedhLandContext context = new(27.5, 33, Enabled: true, BaselineSd: 1.6, BaselineMonth: "2026-07");

        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck,
            ManabaseMode.Cedh,
            ritualLandCredit: true,
            cedhContext: context);

        Assert.NotNull(report.LandTarget);
        ManabaseLandTargetBreakdown lt = report.LandTarget;
        double expectedCredit = KarstenManabase.RitualLandCreditAmount(deck.OneShots.Count);

        Assert.Equal(expectedCredit, lt.RitualLandCredit, 6);
        Assert.Equal(deck.OneShots.Count, lt.NetPositiveRitualCount);
        Assert.Equal(lt.FinalTarget - lt.BaseTarget, lt.CedhAdjustment, 6);
    }

    [Fact]
    public void Analyze_CedhBreakdown_LeavesRitualLandCreditZero_WhenNotApplied()
    {
        ManabaseDeck ritualDeck = BuildBuffsByHans() with
        {
            OneShots = new List<OneShotMana>
            {
                MakeOneShot("Dark Ritual"),
            },
        };
        CedhLandContext enabledContext = new(27.5, 33, Enabled: true, BaselineSd: 1.6, BaselineMonth: "2026-07");

        ManabaseReport casual = ManabaseAnalyzer.Analyze(
            ritualDeck,
            ManabaseMode.Casual,
            ritualLandCredit: true,
            cedhContext: enabledContext);
        ManabaseReport flagOff = ManabaseAnalyzer.Analyze(
            ritualDeck,
            ManabaseMode.Cedh,
            ritualLandCredit: false,
            cedhContext: enabledContext);
        ManabaseReport noRituals = ManabaseAnalyzer.Analyze(
            BuildBuffsByHans(),
            ManabaseMode.Cedh,
            ritualLandCredit: true,
            cedhContext: enabledContext);

        Assert.Equal(0.0, casual.LandTarget!.RitualLandCredit);
        Assert.Equal(0, casual.LandTarget.NetPositiveRitualCount);
        Assert.Equal(0.0, flagOff.LandTarget!.RitualLandCredit);
        Assert.Equal(0, flagOff.LandTarget.NetPositiveRitualCount);
        Assert.Equal(0.0, noRituals.LandTarget!.RitualLandCredit);
        Assert.Equal(0, noRituals.LandTarget.NetPositiveRitualCount);
    }

    [Fact]
    public void Analyze_ScrySourceCredit_AddsPointTwoPerDetectedCopy_WhenEnabled()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Island",
                Quantity = 10,
                ManaCost = string.Empty,
                ManaValue = 0,
                TypeLine = "Basic Land — Island",
                OracleText = "{T}: Add {U}.",
                ProducedMana = new[] { "U" },
                HasLandFace = true,
            },
            new()
            {
                Name = "Preordain",
                Quantity = 2,
                ManaCost = "{U}",
                ManaValue = 1,
                TypeLine = "Sorcery",
                OracleText = "Scry 2, then draw a card.",
                ProducedMana = System.Array.Empty<string>(),
            },
            new()
            {
                Name = "Counterspell",
                Quantity = 1,
                ManaCost = "{U}{U}",
                ManaValue = 2,
                TypeLine = "Instant",
                OracleText = "Counter target spell.",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManabaseReport off = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, scryCredit: false);
        ManabaseReport on = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, scryCredit: true);

        ColorSourceFinding offBlue = Assert.Single(off.ColorFindings);
        ColorSourceFinding onBlue = Assert.Single(on.ColorFindings);

        Assert.Equal(2, deck.ScrySourceCreditCopies);
        Assert.Equal(0.0, off.ScrySourceCredit);
        Assert.Equal(0, off.ScrySourceCreditCopies);
        Assert.Equal(0.4, on.ScrySourceCredit, 6);
        Assert.Equal(2, on.ScrySourceCreditCopies);
        Assert.Equal(offBlue.ActualSources + 0.4, onBlue.ActualSources, 6);
    }

    [Fact]
    public void Analyze_ScrySourceCredit_DoesNotInflateUntappedNumerator()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Island",
                Quantity = 19,
                ManaCost = string.Empty,
                ManaValue = 0,
                TypeLine = "Basic Land — Island",
                OracleText = "{T}: Add {U}.",
                ProducedMana = new[] { "U" },
                HasLandFace = true,
            },
            new()
            {
                Name = "Opt",
                Quantity = 5,
                ManaCost = "{U}",
                ManaValue = 1,
                TypeLine = "Instant",
                OracleText = "Scry 1, then draw a card.",
                ProducedMana = System.Array.Empty<string>(),
            },
            new()
            {
                Name = "Ponder",
                Quantity = 1,
                ManaCost = "{U}",
                ManaValue = 1,
                TypeLine = "Sorcery",
                OracleText = "Draw a card.",
                ProducedMana = System.Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, scryCredit: true);

        ColorSourceFinding blue = Assert.Single(report.ColorFindings, finding => finding.Color == ManaColor.Blue);

        Assert.Equal(20.0, blue.ActualSources);
        Assert.Equal(19.0, blue.UntappedSources, 6);
        Assert.NotNull(report.TapAnalysis);
        Assert.Equal(19.0, report.TapAnalysis!.UntappedSources, 6);
        Assert.Equal(20.0, report.TapAnalysis.TotalSources, 6);
        Assert.Equal(95, report.TapAnalysis.OverallUntappedPercent);
    }

    [Fact]
    public void Analyze_ColorlessSnowFlagOn_AddsDedicatedRequirementRows_ForColorlessAndSnow()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Wastes",
                Quantity = 10,
                ManaCost = string.Empty,
                ManaValue = 0,
                TypeLine = "Basic Land — Wastes",
                OracleText = "{T}: Add {C}.",
                ProducedMana = new[] { "C" },
                HasLandFace = true,
            },
            new()
            {
                Name = "Snow-Covered Island",
                Quantity = 14,
                ManaCost = string.Empty,
                ManaValue = 0,
                TypeLine = "Snow Land — Island",
                OracleText = "{T}: Add {U}.",
                ProducedMana = new[] { "U" },
                HasLandFace = true,
            },
            new()
            {
                Name = "Thought-Knot Seer",
                Quantity = 1,
                ManaCost = "{3}{C}",
                ManaValue = 4,
                TypeLine = "Creature — Eldrazi",
                OracleText = string.Empty,
                ProducedMana = Array.Empty<string>(),
            },
            new()
            {
                Name = "Arcum's Astrolabe",
                Quantity = 1,
                ManaCost = "{S}",
                ManaValue = 1,
                TypeLine = "Artifact",
                OracleText = string.Empty,
                ProducedMana = Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManabaseReport off = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, colorlessSnow: false);
        ManabaseReport on = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, colorlessSnow: true);

        Assert.DoesNotContain(off.ColorFindings, finding => finding.DisplayColor is "Colorless" or "Snow");
        Assert.Contains(
            on.ColorFindings,
            finding => finding.DisplayColor == "Colorless"
                && finding.ActualSources == 10.0
                && finding.DrivingSpell == "Thought-Knot Seer");
        Assert.Contains(
            on.ColorFindings,
            finding => finding.DisplayColor == "Snow"
                && finding.ActualSources == 14.0
                && finding.DrivingSpell == "Arcum's Astrolabe");
    }

    [Fact]
    public void Analyze_ColorlessSnowFlagOn_AllowsSnowManaSourceToDriveSnowFinding()
    {
        var cards = new List<CardFact>
        {
            new()
            {
                Name = "Snow-Covered Island",
                Quantity = 14,
                ManaCost = string.Empty,
                ManaValue = 0,
                TypeLine = "Snow Land — Island",
                OracleText = "{T}: Add {U}.",
                ProducedMana = new[] { "U" },
                HasLandFace = true,
            },
            new()
            {
                Name = "Arcum's Astrolabe",
                Quantity = 1,
                ManaCost = "{S}",
                ManaValue = 1,
                TypeLine = "Snow Artifact",
                OracleText = "When Arcum's Astrolabe enters, draw a card.\n{1}, {T}: Add one mana of any color.",
                ProducedMana = Array.Empty<string>(),
            },
        };

        ManabaseDeck deck = ManabaseClassifier.Classify(cards);

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, colorlessSnow: true);

        ColorSourceFinding snow = Assert.Single(report.ColorFindings, finding => finding.DisplayColor == "Snow");
        Assert.Equal("Arcum's Astrolabe", snow.DrivingSpell);
        Assert.Equal(1, snow.EvaluatedCardCount);
    }

    [Fact]
    public void Analyze_ColorlessSnowFlagOn_SetsSpecialCategoryDenominatorToEvaluatedSpellCount()
    {
        var sources = new List<ManaSource>
        {
            new()
            {
                Name = "Snow-Covered Wastes 0",
                Produces = System.Array.Empty<ManaColor>(),
                ProducesColorless = true,
                IsSnow = true,
                EntersUntapped = true,
                IsLand = true,
            },
        };

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 1.5,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new()
                {
                    Name = "Icehide Golem",
                    ManaValue = 1,
                    Pips = Pip(),
                    SnowPips = 1,
                },
            },
            IsSingleton = true,
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual, colorlessSnow: true);

        ColorSourceFinding snow = Assert.Single(report.ColorFindings, finding => finding.DisplayColor == "Snow");
        Assert.Equal(1, snow.EvaluatedCardCount);
    }

    [Fact]
    public void Analyze_InteractionLens_IsNull_InCasualMode_EvenWhenFlagOn()
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            BuildInteractionLensDeck(withReducer: false),
            ManabaseMode.Casual,
            interactionLens: true);

        Assert.Null(report.InteractionLens);
    }

    [Fact]
    public void Analyze_InteractionLens_IsNull_InCedhMode_WhenFlagOff()
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            BuildInteractionLensDeck(withReducer: false),
            ManabaseMode.Cedh,
            interactionLens: false);

        Assert.Null(report.InteractionLens);
    }

    [Fact]
    public void Analyze_InteractionLens_FiltersByInteractionAndPostOverrideManaValue_AndSortsWorstFirst()
    {
        var overrides = new Dictionary<string, string>
        {
            ["Fierce Guardianship"] = "0",
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(
            BuildInteractionLensDeck(withReducer: true),
            ManabaseMode.Cedh,
            costOverrides: overrides,
            interactionLens: true);

        ManabaseInteractionLens lens = Assert.IsType<ManabaseInteractionLens>(report.InteractionLens);

        Assert.Equal(3, lens.QualifyingCount);
        Assert.Equal(2, lens.OnTargetCount);
        Assert.Equal(88, lens.Threshold);
        Assert.Equal(
            new[] { "Red Blast", "Counterspell", "Fierce Guardianship" },
            lens.Rows.Select(r => r.Name).ToArray());
        Assert.Equal(0, lens.Rows[0].HoldablePercent);
        Assert.InRange(lens.Rows[1].HoldablePercent, 88, 100);
        Assert.Equal(100, lens.Rows[2].HoldablePercent);
        Assert.True(lens.Rows[0].HoldablePercent <= lens.Rows[1].HoldablePercent);
        Assert.True(lens.Rows[1].HoldablePercent <= lens.Rows[2].HoldablePercent);
        Assert.True(lens.Rows.Single(r => r.Name == "Fierce Guardianship").IsCostOverridden);
        Assert.DoesNotContain(lens.Rows, r => r.Name == "Exclude Printed Three");
        Assert.DoesNotContain(lens.Rows, r => r.Name == "Reducer Only Three");

        CardCastability reducerOnlyRow = report.Castability.Single(c => c.Name == "Reducer Only Three");
        Assert.True(reducerOnlyRow.OnCurveTurn <= 2, $"reducer-only test needs an early reduced turn, got {reducerOnlyRow.OnCurveTurn}");
    }

    [Fact]
    public void Analyze_InteractionLens_WithNoQualifyingSpells_ReturnsPopulatedEmptyState()
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            BuildNoQualifyingInteractionLensDeck(),
            ManabaseMode.Cedh,
            interactionLens: true);

        ManabaseInteractionLens lens = Assert.IsType<ManabaseInteractionLens>(report.InteractionLens);

        Assert.Equal(0, lens.QualifyingCount);
        Assert.Equal(0, lens.OnTargetCount);
        Assert.Equal(88, lens.Threshold);
        Assert.Empty(lens.Rows);
    }

    [Fact]
    public void Analyze_InteractionLens_IncludesPreGateInstantInteraction_WhenPlanRoleWasStripped()
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            BuildPreGateInteractionLensDeck(),
            ManabaseMode.Cedh,
            interactionLens: true);

        ManabaseInteractionLens lens = Assert.IsType<ManabaseInteractionLens>(report.InteractionLens);

        Assert.Equal(1, lens.QualifyingCount);
        Assert.Contains(lens.Rows, row => row.Name == "Counterspell");
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

    private static ManabaseDeck BuildInteractionLensDeck(bool withReducer)
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 36; i++)
        {
            sources.Add(new ManaSource { Name = $"Island {i}", Produces = new[] { ManaColor.Blue } });
        }

        var spells = new List<SpellRequirement>
        {
            new()
            {
                Name = "Fierce Guardianship",
                ManaValue = 4,
                Pips = Pip((ManaColor.Blue, 1)),
                PlanRoles = PlanRole.Interaction,
                Kinds = SpellKinds.Instant,
            },
            new()
            {
                Name = "Counterspell",
                ManaValue = 2,
                Pips = Pip((ManaColor.Blue, 2)),
                PlanRoles = PlanRole.Interaction,
                Kinds = SpellKinds.Instant,
            },
            new()
            {
                Name = "Red Blast",
                ManaValue = 1,
                Pips = Pip((ManaColor.Red, 1)),
                PlanRoles = PlanRole.Interaction,
                Kinds = SpellKinds.Instant,
            },
            new()
            {
                Name = "Exclude Printed Three",
                ManaValue = 3,
                Pips = Pip((ManaColor.Blue, 1)),
                PlanRoles = PlanRole.Interaction,
                Kinds = SpellKinds.Instant,
            },
            new()
            {
                Name = "Reducer Only Three",
                ManaValue = 3,
                Pips = Pip((ManaColor.Blue, 1)),
                PlanRoles = PlanRole.Interaction,
                Kinds = SpellKinds.Instant,
            },
            new()
            {
                Name = "Rhystic Study",
                ManaValue = 3,
                Pips = Pip((ManaColor.Blue, 1)),
                PlanRoles = PlanRole.Engine,
            },
        };

        var reducers = withReducer
            ? new List<CostReducer> { new() { GenericReduction = 1, Scope = ReductionScope.InstantSorcery, SourceManaValue = 2 } }
            : new List<CostReducer>();

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = spells,
            CostReduction = reducers,
            IsSingleton = true,
        };
    }

    private static ManabaseDeck BuildNoQualifyingInteractionLensDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 36; i++)
        {
            sources.Add(new ManaSource { Name = $"Plains {i}", Produces = new[] { ManaColor.White } });
        }

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.5,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new()
                {
                    Name = "Rhystic Study",
                    ManaValue = 3,
                    Pips = Pip((ManaColor.Blue, 1)),
                    PlanRoles = PlanRole.Engine,
                },
                new()
                {
                    Name = "Force of Negation",
                    ManaValue = 3,
                    Pips = Pip((ManaColor.Blue, 1)),
                    PlanRoles = PlanRole.Interaction,
                    Kinds = SpellKinds.Instant,
                },
            },
            IsSingleton = true,
        };
    }

    private static ManabaseDeck BuildPreGateInteractionLensDeck()
    {
        var sources = new List<ManaSource>();
        for (int i = 0; i < 36; i++)
        {
            sources.Add(new ManaSource { Name = $"Island {i}", Produces = new[] { ManaColor.Blue } });
        }

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = new List<SpellRequirement>
            {
                new()
                {
                    Name = "Counterspell",
                    ManaValue = 2,
                    Pips = Pip((ManaColor.Blue, 2)),
                    PlanRoles = PlanRole.None,
                    IsInteractionSpell = true,
                    Kinds = SpellKinds.Instant,
                },
            },
            IsSingleton = true,
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

    private static OneShotMana MakeOneShot(string name) => new()
    {
        Name = name,
        ProducedColors = new[] { ManaColor.Black },
        ProducedAmount = 3,
        OwnPips = Pip((ManaColor.Black, 1)),
        OwnManaValue = 1,
    };

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

    [Fact]
    public void RequiredSources_IsMulliganAware_NotInflated_ForTurnTwoDoublePip()
    {
        // A turn-2 {W}{W} on a healthy white base. The mulligan-aware requirement must be a realistic
        // count (the mulligan-blind hypergeometric used to say ~30); 33 white sources cover it.
        var spell = new SpellRequirement { Name = "WW", ManaValue = 2, Pips = Pip((ManaColor.White, 2)) };
        var sources = new List<ManaSource>();
        for (int i = 0; i < 33; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.5,
            Sources = sources,
            Spells = new List<SpellRequirement> { spell },
            IsSingleton = true,
        };

        ColorSourceFinding white = ManabaseAnalyzer.Analyze(deck).ColorFindings.Single(f => f.Color == ManaColor.White);
        Assert.InRange(white.RequiredSources, 2, 30);
        Assert.True(white.Deficit <= 0, $"33 white sources should cover WW by turn 2; deficit {white.Deficit}");
    }

    [Fact]
    public void RequiredSources_RespondsToSourceCount_DeficitWhenThin()
    {
        var spell = new SpellRequirement { Name = "WW", ManaValue = 2, Pips = Pip((ManaColor.White, 2)) };
        var sources = new List<ManaSource>();
        for (int i = 0; i < 8; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }
        for (int i = 0; i < 28; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.5,
            Sources = sources,
            Spells = new List<SpellRequirement> { spell },
            IsSingleton = true,
        };

        ColorSourceFinding white = ManabaseAnalyzer.Analyze(deck).ColorFindings.Single(f => f.Color == ManaColor.White);
        Assert.True(white.Deficit > 0, $"8 white sources cannot support WW by turn 2; deficit {white.Deficit}");
    }

    [Fact]
    public void Analyze_CleanLowCurveMonoDeck_IsHealthy()
    {
        // Task 3: land-adequate + no color under-supported → Healthy (and IsHealthy true).
        var sources = new List<ManaSource>();
        for (int i = 0; i < 39; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }

        var spells = new List<SpellRequirement>();
        for (int i = 0; i < 12; i++)
        {
            spells.Add(new SpellRequirement { Name = $"Easy White {i}", ManaValue = 1, Pips = Pip((ManaColor.White, 1)) });
        }

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 1.2,
            Sources = sources,
            Spells = spells,
            IsSingleton = true,
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.True(report.LandDelta >= -1, $"land count must be adequate, delta {report.LandDelta:F1}");
        Assert.All(report.ColorFindings, f => Assert.Equal(0, f.UnderSupportedCount));
        Assert.Equal(ManabaseHealth.Healthy, report.Health);
        Assert.True(report.IsHealthy);
        Assert.Empty(report.DemandingCards);
    }

    [Fact]
    public void Analyze_LandAdequate_OneDemandingCard_NoColorDeficit_IsFunctional()
    {
        // Task 3: land-adequate, no mulligan-aware source deficit on any color, only a single
        // high-MV demanding card under the bar (within the 15%-of-colorCards tolerance) → Functional,
        // NOT NeedsWork. The demanding card is surfaced in DemandingCards.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 39; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }

        var spells = new List<SpellRequirement>();
        for (int i = 0; i < 10; i++)
        {
            spells.Add(new SpellRequirement { Name = $"Easy White {i}", ManaValue = 1, Pips = Pip((ManaColor.White, 1)) });
        }

        // A 7-MV triple-white bomb: colour is fully covered by 39 white sources (no deficit), but the
        // mana-quantity risk of needing 7 lands by turn 7 drags its cast % below the 80% bar.
        spells.Add(new SpellRequirement { Name = "Heavy Bomb", ManaValue = 7, Pips = Pip((ManaColor.White, 3)) });

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = spells,
            IsSingleton = true,
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.True(report.LandDelta >= -1, $"land count must be adequate, delta {report.LandDelta:F1}");
        ColorSourceFinding white = report.ColorFindings.Single(f => f.Color == ManaColor.White);
        Assert.True(white.Deficit <= 0, $"39 white sources cover the requirement; deficit {white.Deficit}");
        Assert.Equal(1, white.UnderSupportedCount);
        Assert.Equal(ManabaseHealth.Functional, report.Health);
        Assert.False(report.IsHealthy);
        Assert.Contains(report.DemandingCards, d => d.Name == "Heavy Bomb");
    }

    [Fact]
    public void Analyze_WhiteScrewedDeck_IsNeedsWork()
    {
        // Task 3: a color with a real mulligan-aware source deficit forces NeedsWork even when the
        // land count is adequate.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 8; i++)
        {
            sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
        }
        for (int i = 0; i < 31; i++)
        {
            sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
        }

        var spells = new List<SpellRequirement>
        {
            new() { Name = "Grand Abolisher", ManaValue = 2, Pips = Pip((ManaColor.White, 2)) },
            new() { Name = "Double White Two", ManaValue = 2, Pips = Pip((ManaColor.White, 2)) },
        };

        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = sources,
            Spells = spells,
            IsSingleton = true,
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.True(report.LandDelta >= -1, $"land count must be adequate, delta {report.LandDelta:F1}");
        ColorSourceFinding white = report.ColorFindings.Single(f => f.Color == ManaColor.White);
        Assert.True(white.Deficit > 0, $"8 white sources cannot support WW by turn 2; deficit {white.Deficit}");
        Assert.Equal(ManabaseHealth.NeedsWork, report.Health);
        Assert.False(report.IsHealthy);
    }

    [Fact]
    public void Analyze_GoldDriver_RequiresAtLeastAsManySources_AsMonoSinglePip()
    {
        // Codex HIGH-1: a gold {W}{U} card needs a white AND a blue source simultaneously, so its
        // blue requirement must account for that contention — it can never be LOWER than a mono {U}
        // single-pip on the same base (the old mono-probe ignored the white pip and under-counted).
        static ManabaseDeck Build(SpellRequirement driver)
        {
            var sources = new List<ManaSource>();
            for (int i = 0; i < 12; i++)
            {
                sources.Add(new ManaSource { Name = "Plains", Produces = new[] { ManaColor.White } });
            }
            for (int i = 0; i < 12; i++)
            {
                sources.Add(new ManaSource { Name = "Island", Produces = new[] { ManaColor.Blue } });
            }

            return new ManabaseDeck
            {
                TotalCards = 100,
                CommanderCount = 1,
                AverageManaValue = 2.5,
                Sources = sources,
                Spells = new List<SpellRequirement> { driver },
                IsSingleton = true,
            };
        }

        var gold = Build(new SpellRequirement
        {
            Name = "Gold WU",
            ManaValue = 2,
            Pips = Pip((ManaColor.White, 1), (ManaColor.Blue, 1)),
            IsGold = true,
        });
        var mono = Build(new SpellRequirement { Name = "Mono U", ManaValue = 2, Pips = Pip((ManaColor.Blue, 1)) });

        ColorSourceFinding goldBlue = ManabaseAnalyzer.Analyze(gold).ColorFindings.Single(f => f.Color == ManaColor.Blue);
        ColorSourceFinding monoBlue = ManabaseAnalyzer.Analyze(mono).ColorFindings.Single(f => f.Color == ManaColor.Blue);

        Assert.True(goldBlue.RequiredSources >= monoBlue.RequiredSources,
            $"gold blue requirement ({goldBlue.RequiredSources}) must model the white contention and not fall below mono ({monoBlue.RequiredSources})");
        Assert.True(goldBlue.RequiredSources > 0);
    }

    [Fact]
    public void Analyze_ManaLimitedHighMvSpell_ColorRequirementClampsToPips_NotLandCeiling()
    {
        // Codex review cap-guard: a 7-MV single-pip spell on a ramp-free 30-land/99 deck is limited by
        // mana QUANTITY (you rarely have 7 lands by turn 7), not blue access — even an all-blue base
        // can't clear the bar. The per-color requirement must clamp to the pip minimum (1), NOT run up
        // to the land ceiling and resurrect a phantom deficit. The card's difficulty shows in its cast %.
        var sources = new List<ManaSource>();
        for (int i = 0; i < 30; i++)
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
                new() { Name = "Big Blue Seven", ManaValue = 7, Pips = Pip((ManaColor.Blue, 1)) },
            },
            IsSingleton = true,
        };

        ColorSourceFinding blue = ManabaseAnalyzer.Analyze(deck).ColorFindings.Single(f => f.Color == ManaColor.Blue);

        // Clamped to the single pip — not the 30-land ceiling.
        Assert.Equal(1, blue.RequiredSources);
    }

    [Fact]
    public void Analyze_ThreeColorGold_AddsTwoSourceContentionBump_OverMono()
    {
        // Codex review: a 3-color {W}{U}{B} card needs a source of BOTH other colors at once, so its
        // per-color requirement is the isolated mono figure plus exactly two (one per other color),
        // versus a mono single-pip on the same base. Locks the otherColors == 2 bump against off-by-one.
        static ManabaseDeck Build(SpellRequirement driver)
        {
            var sources = new List<ManaSource>();
            foreach (ManaColor c in new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black })
            {
                for (int i = 0; i < 12; i++)
                {
                    sources.Add(new ManaSource { Name = c.ToString(), Produces = new[] { c } });
                }
            }

            return new ManabaseDeck
            {
                TotalCards = 100,
                CommanderCount = 1,
                AverageManaValue = 2.5,
                Sources = sources,
                Spells = new List<SpellRequirement> { driver },
                IsSingleton = true,
            };
        }

        var gold = Build(new SpellRequirement
        {
            Name = "WUB Gold",
            ManaValue = 3,
            Pips = Pip((ManaColor.White, 1), (ManaColor.Blue, 1), (ManaColor.Black, 1)),
            IsGold = true,
        });
        var mono = Build(new SpellRequirement { Name = "Mono U", ManaValue = 3, Pips = Pip((ManaColor.Blue, 1)) });

        ColorSourceFinding goldBlue = ManabaseAnalyzer.Analyze(gold).ColorFindings.Single(f => f.Color == ManaColor.Blue);
        ColorSourceFinding monoBlue = ManabaseAnalyzer.Analyze(mono).ColorFindings.Single(f => f.Color == ManaColor.Blue);

        Assert.Equal(monoBlue.RequiredSources + 2, goldBlue.RequiredSources);
    }

    private static IReadOnlyDictionary<ManaColor, int> Pip(params (ManaColor Color, int Count)[] pips)
        => pips.ToDictionary(p => p.Color, p => p.Count);

    private static CardCastability? InvokeSelectHeadlineSpell(
        IReadOnlyList<CardCastability> castability,
        CommanderImportance importance)
    {
        global::System.Reflection.MethodInfo method = typeof(ManabaseAnalyzer).GetMethod(
            "SelectHeadlineSpell",
            global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Static)!;

        return (CardCastability?)method.Invoke(null, new object?[] { castability, importance });
    }
}
