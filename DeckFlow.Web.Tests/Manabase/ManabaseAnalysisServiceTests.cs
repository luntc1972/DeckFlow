using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Validates <see cref="ManabaseAnalysisService"/>: board filtering, printing-preferred
/// resolution (alternate names), unresolved handling, and report production — all with
/// faked deck loading and Scryfall HTTP.
/// </summary>
public sealed class ManabaseAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_ProducesReport_FiltersSideboard_ResolvesByPrinting()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Tymna the Weaver", 1, "commander", set: "cmr", cn: "1"),
            Land("Plains", 12),
            Land("Island", 10),
            Entry("Swords to Plowshares", 1, "mainboard"),
            // Alternate (flavor) name; resolves only via its printing.
            Entry("Godzilla, King of the Monsters", 1, "mainboard", set: "iko", cn: "275"),
            // Sideboard card must be excluded from the analysis.
            Entry("Black Lotus", 1, "sideboard"),
        };

        var cards = new List<ScryfallCard>
        {
            BasicLand("Plains", "W"),
            BasicLand("Island", "U"),
            Spell("Tymna the Weaver", "{1}{W}", 2, "Legendary Creature — Human Cleric"),
            Spell("Swords to Plowshares", "{W}", 1, "Instant"),
            // Canonical name differs from the deck entry; matched by set+collector.
            Spell("Zilortha, Strength Incarnate", "{2}{R}{R}", 4, "Legendary Creature — Dinosaur",
                set: "iko", cn: "275"),
        };

        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var result = await service.AnalyzeAsync(
            "https://archidekt.com/decks/1", "Test Deck", options: null, CancellationToken.None);

        Assert.NotNull(result.Report);
        Assert.Equal(22, result.Report.ActualLands); // 12 Plains + 10 Island; sideboard excluded.
        Assert.Empty(result.Unresolved); // Godzilla resolved via printing.
        Assert.Contains("Test Deck", result.PromptSwapPrompt);
        Assert.NotEmpty(result.Report.ColorFindings);
        // Default profile is Casual so existing output is unchanged.
        Assert.Equal(ManabaseMode.Casual, result.Report.Mode);
    }

    [Fact]
    public async Task AnalyzeAsync_RampCreditV2Flag_DropsOneShotRitualFromLandTarget()
    {
        // MQ-03 plumbing: the flag is read BEFORE classification → narrows the ramp/draw credit. A
        // one-shot ritual (Dark Ritual, an Instant) loses the credit under v2; a mana rock (Sol Ring)
        // keeps it. Confirms the bool reaches ManabaseClassifier and fails safe OFF without the cache.
        static ScryfallCard Oracle(string name, string cost, double cmc, string type, string oracle) => new(
            Name: name, ManaCost: cost, TypeLine: type, OracleText: oracle,
            Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
            SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
            Layout: "normal", Cmc: cmc, ProducedMana: null, Rarity: "rare");

        var entries = new List<DeckEntry>
        {
            Entry("Tymna the Weaver", 1, "commander", set: "cmr", cn: "1"),
            Land("Swamp", 33),
            Entry("Dark Ritual", 1, "mainboard"),
            Entry("Sol Ring", 1, "mainboard"),
        };
        static List<ScryfallCard> Cards() => new()
        {
            BasicLand("Swamp", "B"),
            Spell("Tymna the Weaver", "{1}{W}", 2, "Legendary Creature — Human Cleric"),
            Oracle("Dark Ritual", "{B}", 1, "Instant", "Add {B}{B}{B}."),
            Oracle("Sol Ring", "{1}", 1, "Artifact", "{T}: Add {C}{C}."),
        };

        var off = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(Cards()));
        var on = new ManabaseAnalysisService(
            new FakeLoader(entries), new FakeResolver(Cards()),
            new FakeFeatureFlagCache(new Dictionary<string, bool> { ["analysis.manabase.ramp-credit-v2"] = true }));

        var rOff = await off.AnalyzeAsync("x", null);
        var rOn = await on.AnalyzeAsync("x", null);

        // Off (no cache → fail-safe off): broad predicate counts ritual + rock.
        Assert.Equal(2, rOff.Report.LandTarget!.RampAndDrawUnderThree);
        // On: the one-shot ritual is dropped, the rock is kept.
        Assert.Equal(1, rOn.Report.LandTarget!.RampAndDrawUnderThree);
        Assert.True(rOn.Report.TargetLands >= rOff.Report.TargetLands); // less ramp credit → higher target
    }

    [Fact]
    public async Task AnalyzeAsync_HealthBandHeadlineFloorFlag_ThreadsToReport()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Tymna the Weaver", 1, "commander", set: "cmr", cn: "1"),
            Land("Plains", 30),
            Entry("Swords to Plowshares", 1, "mainboard"),
        };
        var cards = new List<ScryfallCard>
        {
            BasicLand("Plains", "W"),
            Spell("Tymna the Weaver", "{1}{W}", 2, "Legendary Creature — Human Cleric"),
            Spell("Swords to Plowshares", "{W}", 1, "Instant"),
        };

        var off = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));
        var on = new ManabaseAnalysisService(
            new FakeLoader(entries), new FakeResolver(cards),
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [ManabaseAnalysisService.HealthBandHeadlineFloorFlagKey] = true,
            }));

        ManabaseAnalysisResult offResult = await off.AnalyzeAsync("x", null);
        ManabaseAnalysisResult onResult = await on.AnalyzeAsync("x", null);

        Assert.False(offResult.Report.UseHealthBandHeadlineFloor);
        Assert.True(onResult.Report.UseHealthBandHeadlineFloor);
    }

    [Fact]
    public async Task AnalyzeAsync_LandRampSimFlag_RaisesPayoffCast_FailsSafeOff()
    {
        // 70-03b plumbing: the flag is read via IsFlagOn (fail-safe OFF) and threaded into Classify, so
        // repeatable land-ramp is modeled as colorless ramp in the sim. On a Forest + Cultivate deck the
        // expensive {6}{G} payoff casts more often when the flag is on; without a cache it does not.
        static ScryfallCard Oracle(string name, string cost, double cmc, string type, string oracle) => new(
            Name: name, ManaCost: cost, TypeLine: type, OracleText: oracle,
            Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
            SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
            Layout: "normal", Cmc: cmc, ProducedMana: null, Rarity: "rare");
        const string ramp = "Search your library for a basic land card, put it onto the battlefield tapped, then shuffle.";

        var entries = new List<DeckEntry>
        {
            Entry("Azusa, Lost but Seeking", 1, "commander", set: "chk", cn: "212"),
            Land("Forest", 33),
            Entry("Rampant Growth", 1, "mainboard"),
            Entry("Nature's Lore", 1, "mainboard"),
            Entry("Three Visits", 1, "mainboard"),
            Entry("Cultivate", 1, "mainboard"),
            Entry("Kodama's Reach", 1, "mainboard"),
            Entry("Big Green", 1, "mainboard"),
        };
        for (int i = 0; i < 55; i++)
        {
            entries.Add(Entry($"Filler {i}", 1, "mainboard"));
        }

        List<ScryfallCard> Cards()
        {
            var cards = new List<ScryfallCard>
            {
                BasicLand("Forest", "G"),
                Spell("Azusa, Lost but Seeking", "{2}{G}", 3, "Legendary Creature — Human Monk"),
                Oracle("Rampant Growth", "{1}{G}", 2, "Sorcery", ramp),
                Oracle("Nature's Lore", "{1}{G}", 2, "Sorcery", ramp),
                Oracle("Three Visits", "{1}{G}", 2, "Sorcery", ramp),
                Oracle("Cultivate", "{2}{G}", 3, "Sorcery", ramp),
                Oracle("Kodama's Reach", "{2}{G}", 3, "Sorcery", ramp),
                Oracle("Big Green", "{6}{G}", 7, "Creature — Hydra", "Trample."),
            };
            for (int i = 0; i < 55; i++)
            {
                cards.Add(Oracle($"Filler {i}", "{3}", 3, "Artifact", "Does nothing."));
            }

            return cards;
        }

        var off = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(Cards()));
        var on = new ManabaseAnalysisService(
            new FakeLoader(entries), new FakeResolver(Cards()),
            new FakeFeatureFlagCache(new Dictionary<string, bool> { ["analysis.manabase.land-ramp-sim"] = true }));

        var rOff = await off.AnalyzeAsync("x", null);
        var rOn = await on.AnalyzeAsync("x", null);

        int castOff = rOff.Report.Castability.First(c => c.Name == "Big Green").CastPercent;
        int castOn = rOn.Report.Castability.First(c => c.Name == "Big Green").CastPercent;

        Assert.True(castOn > castOff, $"land-ramp sim should raise the payoff's cast% (off={castOff}, on={castOn})");
        // Colorless ramp source → land total + color verdict unchanged.
        Assert.Equal(rOff.Report.TargetLands, rOn.Report.TargetLands);
        Assert.Equal(rOff.Report.ActualLands, rOn.Report.ActualLands);
    }

    [Fact]
    public async Task AnalyzeAsync_ColorAwareMulliganFlag_ChangesCast_FailsSafeOff()
    {
        // MQ-05 plumbing: the flag is read via IsFlagOn (fail-safe OFF) and threaded into the analyzer
        // → the castability rows' London mulligan becomes color-aware. On a White-skewed WU deck (blue
        // scarce) the color-aware keep guarantees an Island in every kept opener, so the {U} spell
        // casts more often when the flag is on; without a cache it stays count-only.
        static ScryfallCard Oracle(string name, string cost, double cmc, string type, string oracle) => new(
            Name: name, ManaCost: cost, TypeLine: type, OracleText: oracle,
            Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
            SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
            Layout: "normal", Cmc: cmc, ProducedMana: null, Rarity: "rare");

        var entries = new List<DeckEntry>
        {
            Entry("Tymna the Weaver", 1, "commander", set: "cmr", cn: "1"),
            Land("Plains", 29),
            Land("Island", 5),
            Entry("Blue One", 1, "mainboard"),
            Entry("White One", 1, "mainboard"),
        };
        // Pad to a realistic ~96-card deck: ~35% lands so 7-card openers land in the count band
        // (an all-land deck busts the band every time and force-mulligans past the color gate).
        for (int i = 0; i < 60; i++)
        {
            entries.Add(Entry($"Filler {i}", 1, "mainboard"));
        }

        List<ScryfallCard> Cards()
        {
            var cards = new List<ScryfallCard>
            {
                BasicLand("Plains", "W"),
                BasicLand("Island", "U"),
                Spell("Tymna the Weaver", "{1}{W}", 2, "Legendary Creature — Human Cleric"),
                Oracle("Blue One", "{U}", 1, "Instant", "Draw a card."),
                Oracle("White One", "{W}", 1, "Instant", "Gain 1 life."),
            };
            for (int i = 0; i < 60; i++)
            {
                cards.Add(Oracle($"Filler {i}", "{3}", 3, "Artifact", "Does nothing."));
            }

            return cards;
        }

        var off = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(Cards()));
        var on = new ManabaseAnalysisService(
            new FakeLoader(entries), new FakeResolver(Cards()),
            new FakeFeatureFlagCache(new Dictionary<string, bool> { ["analysis.manabase.color-aware-mulligan"] = true }));

        var rOff = await off.AnalyzeAsync("x", null);
        var rOn = await on.AnalyzeAsync("x", null);

        int castOff = rOff.Report.Castability.First(c => c.Name == "Blue One").CastPercent;
        int castOn = rOn.Report.Castability.First(c => c.Name == "Blue One").CastPercent;

        Assert.True(castOn > castOff, $"color-aware mulligan should raise scarce-color cast% (off={castOff}, on={castOn})");
        // Color counts must not move with the flag (verdict probe path stays count-only).
        Assert.Equal(rOff.Report.TargetLands, rOn.Report.TargetLands);
    }

    [Fact]
    public async Task AnalyzeAsync_SourceManaQuantityFlag_RaisesAffordability_FailsSafeOff()
    {
        // MQ-02 plumbing: the flag "analysis.manabase.source-mana-quantity" is read via IsFlagOn (fail-safe OFF)
        // and threaded as useManaQuantity into ManabaseAnalyzer.Analyze → CastabilitySimulator. When ON
        // each colorless burst source (oracle "{T}: Add {C}{C}.") contributes ManaAmount=2 so a big
        // colorless payoff casts more often. Without a cache the key is absent → IsFlagOn returns false
        // → same result as explicit OFF. Mirrors the Core ManaQuantityTests.ManaQuantity_RaisesAffordability
        // deck shape: many burst rocks + thin land base + expensive colorless payoff.
        //
        // Rocks MUST carry ProducedMana: ["C"] — the classifier's IsRockOrDork gate short-circuits when
        // ProducedMana.Count == 0, so rocks without it are never added to deck.Sources and ManaAmount
        // never reaches the simulator.
        static ScryfallCard Oracle(string name, string cost, double cmc, string type, string oracle) => new(
            Name: name, ManaCost: cost, TypeLine: type, OracleText: oracle,
            Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
            SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
            Layout: "normal", Cmc: cmc, ProducedMana: null, Rarity: "rare");

        static ScryfallCard ColorlessRock(string name) => new(
            Name: name, ManaCost: "{1}", TypeLine: "Artifact", OracleText: "{T}: Add {C}{C}.",
            Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
            SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
            Layout: "normal", Cmc: 1,
            // "C" in ProducedMana is required: IsRockOrDork checks ProducedMana.Count > 0 as a gate.
            // ManaProductionAmount.Parse("{T}: Add {C}{C}.") == 2 → ManaAmount=2 when flag ON.
            ProducedMana: new[] { "C" }, Rarity: "rare");

        // 30 Islands + 20 burst colorless rocks + 1 expensive payoff = 51 cards; pad to ~99.
        var entries = new List<DeckEntry>
        {
            Entry("Commander Guy", 1, "commander"),
            Land("Island", 30),
            Entry("Big Colorless", 1, "mainboard"),
        };
        // 20 distinct rock names so the Scryfall resolver returns each; all produce {C}{C}.
        for (int i = 0; i < 20; i++)
        {
            entries.Add(Entry($"Burst Rock {i}", 1, "mainboard"));
        }
        for (int i = 0; i < 47; i++)
        {
            entries.Add(Entry($"Filler {i}", 1, "mainboard"));
        }

        List<ScryfallCard> Cards()
        {
            var cards = new List<ScryfallCard>
            {
                BasicLand("Island", "U"),
                Spell("Commander Guy", "{2}{U}", 3, "Legendary Creature — Human"),
                // MV=6 pure generic payoff — the sim must scrape together 6 mana on turn 6.
                Oracle("Big Colorless", "{6}", 6, "Artifact", "Does nothing."),
            };
            // 20 burst rocks with ProducedMana=["C"] so they reach deck.Sources and ManaAmount wires in.
            for (int i = 0; i < 20; i++)
            {
                cards.Add(ColorlessRock($"Burst Rock {i}"));
            }
            for (int i = 0; i < 47; i++)
            {
                cards.Add(Oracle($"Filler {i}", "{3}", 3, "Artifact", "Does nothing."));
            }

            return cards;
        }

        // OFF path: no cache at all → IsFlagOn("analysis.manabase.source-mana-quantity") returns false.
        var off = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(Cards()));

        // ON path: cache present with the flag enabled.
        var on = new ManabaseAnalysisService(
            new FakeLoader(entries), new FakeResolver(Cards()),
            new FakeFeatureFlagCache(new Dictionary<string, bool> { [ManabaseAnalysisService.ManaQuantityFlagKey] = true }));

        var rOff = await off.AnalyzeAsync("x", null);
        var rOn = await on.AnalyzeAsync("x", null);

        int castOff = rOff.Report.Castability.First(c => c.Name == "Big Colorless").CastPercent;
        int castOn = rOn.Report.Castability.First(c => c.Name == "Big Colorless").CastPercent;

        // Fail-safe OFF: absent cache must behave identically to explicit false.
        var explicitOff = new ManabaseAnalysisService(
            new FakeLoader(entries), new FakeResolver(Cards()),
            new FakeFeatureFlagCache(new Dictionary<string, bool> { [ManabaseAnalysisService.ManaQuantityFlagKey] = false }));
        var rExplicitOff = await explicitOff.AnalyzeAsync("x", null);
        int castExplicitOff = rExplicitOff.Report.Castability.First(c => c.Name == "Big Colorless").CastPercent;

        Assert.Equal(castExplicitOff, castOff); // absent key == explicit false (fail-safe off)
        Assert.True(castOn > castOff, $"source-mana-quantity ON should raise payoff cast% (off={castOff}, on={castOn})");
    }

    [Fact]
    public async Task AnalyzeAsync_TapAnalyzerFlagAbsent_ShowTapAnalyzerFalse()
    {
        var (entries, cards) = CurveFixture();
        // No cache at all → IsFlagOn(TapAnalyzerFlagKey) returns false (fail-safe OFF).
        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var result = await service.AnalyzeAsync("paste", "Curve Deck");

        Assert.False(GetResultShowTapAnalyzer(result));
    }

    [Fact]
    public async Task AnalyzeAsync_TapAnalyzerFlagExplicitlyFalse_ShowTapAnalyzerFalse()
    {
        var (entries, cards) = CurveFixture();
        var service = new ManabaseAnalysisService(
            new FakeLoader(entries), new FakeResolver(cards),
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [ManabaseAnalysisService.TapAnalyzerFlagKey] = false,
            }));

        var result = await service.AnalyzeAsync("paste", "Curve Deck");

        Assert.False(GetResultShowTapAnalyzer(result));
    }

    [Fact]
    public async Task AnalyzeAsync_TapAnalyzerFlagOn_ShowTapAnalyzerTrue()
    {
        var (entries, cards) = CurveFixture();
        var service = new ManabaseAnalysisService(
            new FakeLoader(entries), new FakeResolver(cards),
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [ManabaseAnalysisService.TapAnalyzerFlagKey] = true,
            }));

        var result = await service.AnalyzeAsync("paste", "Curve Deck");

        Assert.True(GetResultShowTapAnalyzer(result));
    }

    [Fact]
    public async Task AnalyzeAsync_DefaultMode_IsCasual()
    {
        var (entries, cards) = CurveFixture();
        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var result = await service.AnalyzeAsync("paste", null);

        Assert.Equal(ManabaseMode.Casual, result.Report.Mode);
    }

    [Fact]
    public async Task AnalyzeAsync_PlainLanguageFlagOff_LeavesResultNullAndPromptByteIdentical()
    {
        var (entries, cards) = CurveFixture();
        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var result = await service.AnalyzeAsync("paste", "Curve Deck");

        Assert.Null(GetResultVerdict(result));
        Assert.Null(GetResultBudget(result));
        Assert.False(GetResultShowPlainLanguage(result));

        string expectedPrompt = ManabaseSwapPromptBuilder.Build(
            result.Report, "Curve Deck", BuildDecklistText(entries), result.Report.Mode);
        Assert.Equal(expectedPrompt, result.PromptSwapPrompt);
    }

    [Fact]
    public async Task AnalyzeAsync_CommanderCastabilityFlagOff_LeavesReportAndPromptByteIdentical()
    {
        var (entries, cards) = CommanderBackgroundCompanionFixture();

        var baseline = new ManabaseAnalysisService(
            new FakeLoader(entries, detectedCompanionName: "Jegantha, the Wellspring"),
            new FakeResolver(cards));
        var explicitOff = new ManabaseAnalysisService(
            new FakeLoader(entries, detectedCompanionName: "Jegantha, the Wellspring"),
            new FakeResolver(cards),
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [ManabaseAnalysisService.CommanderCastabilityFlagKey] = false,
            }));

        var baselineResult = await baseline.AnalyzeAsync("paste", "Command Zone Deck");
        var offResult = await explicitOff.AnalyzeAsync("paste", "Command Zone Deck");

        Assert.Equal(
            baselineResult.Report.Castability.Select(FormatCastabilityRow),
            offResult.Report.Castability.Select(FormatCastabilityRow));
        Assert.Equal(baselineResult.Report.AvgOnCurvePercent, offResult.Report.AvgOnCurvePercent);
        Assert.Equal(baselineResult.Report.Health, offResult.Report.Health);
        Assert.Equal(baselineResult.PromptSwapPrompt, offResult.PromptSwapPrompt);
        Assert.False(GetResultCommanderCastabilityEnabled(offResult));
        Assert.Null(GetResultCompanionRow(offResult));
    }

    [Fact]
    public async Task AnalyzeAsync_CommanderCastabilityFlagOn_UsesDesignatorPrecedence_ExcludesCompanionAndKeepsTwoCommanders()
    {
        var (entries, cards) = CommanderBackgroundCompanionFixture();
        var service = new ManabaseAnalysisService(
            new FakeLoader(entries, detectedCompanionName: "Jegantha, the Wellspring"),
            new FakeResolver(cards),
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [ManabaseAnalysisService.CommanderCastabilityFlagKey] = true,
            }));

        var result = await service.AnalyzeAsync(
            "paste",
            "Command Zone Deck",
            new ManabaseAnalysisOptions
            {
                CompanionDesignator = "  Kaheera, the Orphanguard  ",
            });

        CardCastability? companion = GetResultCompanionRow(result);
        Assert.True(GetResultCommanderCastabilityEnabled(result));
        Assert.NotNull(companion);
        Assert.Equal("Kaheera, the Orphanguard", companion!.Name);
        Assert.Equal(6, companion.ManaValue);
        Assert.DoesNotContain(result.Report.Castability, row => row.Name == "Kaheera, the Orphanguard");
        Assert.Equal(2, result.Report.LandTarget!.CommanderCount);
        Assert.Equal(2, result.Report.Castability.Count(row => row.IsCommander));
        Assert.True(
            result.PromptSwapPrompt.Contains("Command-zone castability:", StringComparison.Ordinal),
            result.PromptSwapPrompt);
        Assert.Contains("Companion: Kaheera, the Orphanguard", result.PromptSwapPrompt);
    }

    [Fact]
    public async Task AnalyzeAsync_CommanderCastabilityFlagOn_ManualCompanionResolveFailure_TreatedAsNoCompanion()
    {
        var (entries, cards) = CommanderBackgroundCompanionFixture();
        var service = new ManabaseAnalysisService(
            new FakeLoader(entries, detectedCompanionName: null),
            new FakeResolver(cards),
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [ManabaseAnalysisService.CommanderCastabilityFlagKey] = true,
            }));

        var result = await service.AnalyzeAsync(
            "paste",
            "Command Zone Deck",
            new ManabaseAnalysisOptions
            {
                CompanionDesignator = "Unknown Companion",
            });

        Assert.True(GetResultCommanderCastabilityEnabled(result));
        Assert.Null(GetResultCompanionRow(result));
        Assert.DoesNotContain("Companion:", result.PromptSwapPrompt);
    }

    [Fact]
    public async Task AnalyzeAsync_PlainLanguageFlagOn_Casual_ThreadsVerdictBudgetAndPrompt()
    {
        var (entries, cards) = StrainedCommanderFixture();
        var service = new ManabaseAnalysisService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [ManabaseAnalysisService.PlainLanguageVerdictFlagKey] = true,
            }));

        var result = await service.AnalyzeAsync("paste", "Strained Deck");

        Assert.True(GetResultShowPlainLanguage(result));
        Assert.NotNull(GetResultVerdict(result));
        Assert.NotNull(GetResultBudget(result));
        Assert.Contains("Reading your deck", result.PromptSwapPrompt);
    }

    [Fact]
    public async Task AnalyzeAsync_PlainLanguageFlagOn_Cedh_ShowsGlossesOnly()
    {
        var (entries, cards) = StrainedCommanderFixture();
        var service = new ManabaseAnalysisService(
            new FakeLoader(entries),
            new FakeResolver(cards),
            new FakeFeatureFlagCache(new Dictionary<string, bool>
            {
                [ManabaseAnalysisService.PlainLanguageVerdictFlagKey] = true,
            }));

        var result = await service.AnalyzeAsync(
            "paste", "Strained Deck", new ManabaseAnalysisOptions { Mode = ManabaseMode.Cedh });

        Assert.True(GetResultShowPlainLanguage(result));
        Assert.Null(GetResultVerdict(result));
        Assert.Null(GetResultBudget(result));
        Assert.DoesNotContain("Reading your deck", result.PromptSwapPrompt);
    }

    [Fact]
    public async Task AnalyzeAsync_CedhMode_LowersTargetLands_AndEchoesMode()
    {
        var (entries, cards) = CurveFixture();

        var casual = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));
        var cedh = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var casualResult = await casual.AnalyzeAsync(
            "paste", null, new ManabaseAnalysisOptions { Mode = ManabaseMode.Casual });
        var cedhResult = await cedh.AnalyzeAsync(
            "paste", null, new ManabaseAnalysisOptions { Mode = ManabaseMode.Cedh });

        Assert.Equal(ManabaseMode.Cedh, cedhResult.Report.Mode);
        Assert.True(
            cedhResult.Report.TargetLands < casualResult.Report.TargetLands,
            $"cEDH target {cedhResult.Report.TargetLands} should be below casual {casualResult.Report.TargetLands}");
    }

    // A full ~99-card singleton fixture (so the Karsten regression target sits well above the
    // cEDH floor of 28 and the two modes genuinely differ). 36 lands + 63 distinct spells across
    // a normal curve gives a casual target around the mid-30s; cEDH cuts ~3.5 off it.
    private static (List<DeckEntry> Entries, List<ScryfallCard> Cards) CurveFixture()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Tymna the Weaver", 1, "commander", set: "cmr", cn: "1"),
            Land("Plains", 18),
            Land("Island", 18),
        };
        var cards = new List<ScryfallCard>
        {
            BasicLand("Plains", "W"),
            BasicLand("Island", "U"),
            Spell("Tymna the Weaver", "{1}{W}", 2, "Legendary Creature — Human Cleric"),
        };

        // 63 single-copy spells on a mid curve (avg MV ~3) so the regression is realistic.
        for (int i = 0; i < 63; i++)
        {
            int mv = 2 + (i % 4); // 2,3,4,5 repeating
            string name = $"Filler Spell {i}";
            entries.Add(Entry(name, 1, "mainboard"));
            cards.Add(Spell(name, $"{{{mv - 1}}}{{U}}", mv, "Sorcery"));
        }

        return (entries, cards);
    }

    private static string BuildDecklistText(IEnumerable<DeckEntry> entries) =>
        string.Join("\n", entries
            .Where(entry => entry.Quantity > 0)
            .Select(entry => $"{entry.Quantity} {entry.Name}"));

    [Fact]
    public async Task AnalyzeAsync_CommanderImportance_ThreadsThroughToTheReport()
    {
        // The service must forward options.CommanderImportance to the analyzer. A WU commander on a
        // blue-thin base diverges: Central tightens the commander's blue bar (more under-supported)
        // versus Low. Same deck, only the importance knob differs.
        var (entries, cards) = StrainedCommanderFixture();

        var central = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));
        var low = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var centralResult = await central.AnalyzeAsync(
            "paste", null, new ManabaseAnalysisOptions { CommanderImportance = CommanderImportance.Central });
        var lowResult = await low.AnalyzeAsync(
            "paste", null, new ManabaseAnalysisOptions { CommanderImportance = CommanderImportance.Low });

        // Land target is importance-orthogonal — identical regardless of the knob.
        Assert.Equal(centralResult.Report.TargetLands, lowResult.Report.TargetLands);

        var centralBlue = centralResult.Report.ColorFindings.FirstOrDefault(f => f.Color == ManaColor.Blue);
        var lowBlue = lowResult.Report.ColorFindings.FirstOrDefault(f => f.Color == ManaColor.Blue);
        Assert.NotNull(centralBlue);
        Assert.NotNull(lowBlue);
        Assert.True(centralBlue!.UnderSupportedCount >= lowBlue!.UnderSupportedCount,
            "Central must hold the commander's blue to at least as strict a bar as Low");
    }

    // A WU commander with thin blue support so Central vs Low diverges on the blue finding.
    private static (List<DeckEntry> Entries, List<ScryfallCard> Cards) StrainedCommanderFixture()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Brago, King Eternal", 1, "commander"),
            Land("Plains", 24),
            Land("Island", 9),
            Spell("Blue Spell", "{2}{U}", 3, "Sorcery").ToEntry(),
            Spell("White Spell", "{1}{W}", 2, "Sorcery").ToEntry(),
        };
        var cards = new List<ScryfallCard>
        {
            BasicLand("Plains", "W"),
            BasicLand("Island", "U"),
            Spell("Brago, King Eternal", "{2}{W}{U}", 4, "Legendary Creature — Spirit Noble"),
            Spell("Blue Spell", "{2}{U}", 3, "Sorcery"),
            Spell("White Spell", "{1}{W}", 2, "Sorcery"),
        };

        return (entries, cards);
    }

    private static (List<DeckEntry> Entries, List<ScryfallCard> Cards) CommanderBackgroundCompanionFixture()
    {
        var entries = new List<DeckEntry>
        {
            Entry("Wilson, Refined Grizzly", 1, "commander"),
            Entry("Passionate Archaeologist", 1, "commander", category: "Background"),
            Land("Forest", 18),
            Land("Mountain", 18),
            Entry("Kaheera, the Orphanguard", 1, "mainboard"),
            Entry("Cultivate", 1, "mainboard"),
            Entry("Arcane Signet", 1, "mainboard"),
        };
        for (int i = 0; i < 20; i++)
        {
            entries.Add(Entry($"Filler Spell {i}", 1, "mainboard"));
        }

        var cards = new List<ScryfallCard>
        {
            BasicLand("Forest", "G"),
            BasicLand("Mountain", "R"),
            Spell("Wilson, Refined Grizzly", "{1}{G}", 2, "Legendary Creature — Bear Warrior"),
            Spell("Passionate Archaeologist", "{2}{R}", 3, "Legendary Enchantment — Background"),
            Spell("Kaheera, the Orphanguard", "{1}{G/W}{G/W}", 3, "Legendary Creature — Cat Beast"),
            Spell("Jegantha, the Wellspring", "{4}{R/G}", 5, "Legendary Creature — Elemental Elk"),
            Spell("Cultivate", "{2}{G}", 3, "Sorcery"),
            Spell("Arcane Signet", "{2}", 2, "Artifact"),
        };
        for (int i = 0; i < 20; i++)
        {
            cards.Add(Spell($"Filler Spell {i}", "{2}{R}", 3, "Sorcery"));
        }

        return (entries, cards);
    }

    private static ManabaseVerdict? GetResultVerdict(ManabaseAnalysisResult result) =>
        GetOptionalProperty<ManabaseVerdict>(result, "Verdict");

    private static ManabaseRampDrawBudget? GetResultBudget(ManabaseAnalysisResult result) =>
        GetOptionalProperty<ManabaseRampDrawBudget>(result, "Budget");

    private static bool GetResultShowPlainLanguage(ManabaseAnalysisResult result)
    {
        PropertyInfo property = typeof(ManabaseAnalysisResult).GetProperty("ShowPlainLanguage")
            ?? throw new Xunit.Sdk.XunitException("ManabaseAnalysisResult.ShowPlainLanguage property missing.");
        return (bool)(property.GetValue(result) ?? false);
    }

    private static bool GetResultCommanderCastabilityEnabled(ManabaseAnalysisResult result)
    {
        PropertyInfo property = typeof(ManabaseAnalysisResult).GetProperty("CommanderCastabilityEnabled")
            ?? throw new Xunit.Sdk.XunitException("ManabaseAnalysisResult.CommanderCastabilityEnabled property missing.");
        return (bool)(property.GetValue(result) ?? false);
    }

    private static bool GetResultShowTapAnalyzer(ManabaseAnalysisResult result)
    {
        PropertyInfo property = typeof(ManabaseAnalysisResult).GetProperty("ShowTapAnalyzer")
            ?? throw new Xunit.Sdk.XunitException("ManabaseAnalysisResult.ShowTapAnalyzer property missing.");
        return (bool)(property.GetValue(result) ?? false);
    }

    private static CardCastability? GetResultCompanionRow(ManabaseAnalysisResult result) =>
        GetOptionalProperty<CardCastability>(result, "CompanionRow");

    private static T? GetOptionalProperty<T>(object target, string name)
        where T : class
    {
        PropertyInfo property = target.GetType().GetProperty(name)
            ?? throw new Xunit.Sdk.XunitException($"{target.GetType().Name}.{name} property missing.");
        return property.GetValue(target) as T;
    }

    [Fact]
    public async Task AnalyzeAsync_UnresolvedCard_ListedNotThrown()
    {
        var entries = new List<DeckEntry>
        {
            Land("Plains", 1),
            Spell("Swords to Plowshares", "{W}", 1, "Instant").ToEntry(),
            Entry("Totally Made Up Card", 1, "mainboard"),
        };
        var cards = new List<ScryfallCard>
        {
            BasicLand("Plains", "W"),
            Spell("Swords to Plowshares", "{W}", 1, "Instant"),
        };

        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        var result = await service.AnalyzeAsync("paste", null);

        Assert.Contains("Totally Made Up Card", result.Unresolved);
        Assert.NotNull(result.Report);
    }

    [Fact]
    public async Task AnalyzeAsync_BlankSource_Throws()
    {
        var service = new ManabaseAnalysisService(new FakeLoader(new List<DeckEntry>()), new FakeResolver(new List<ScryfallCard>()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeAsync("   ", null));
    }

    [Fact]
    public async Task AnalyzeAsync_OnlySideboard_Throws()
    {
        var entries = new List<DeckEntry> { Entry("Black Lotus", 1, "sideboard") };
        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(new List<ScryfallCard>()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeAsync("paste", null));
    }

    [Fact]
    public async Task AnalyzeAsync_OversizeDeckSource_Throws()
    {
        var service = new ManabaseAnalysisService(new FakeLoader(new List<DeckEntry>()), new FakeResolver(new List<ScryfallCard>()));
        string huge = new string('x', 100_001);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeAsync(huge, null));
    }

    [Fact]
    public async Task AnalyzeAsync_TooManyCards_Throws()
    {
        var entries = Enumerable.Range(0, 501)
            .Select(i => Entry($"Card {i}", 1, "mainboard"))
            .ToList();
        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(new List<ScryfallCard>()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AnalyzeAsync("paste", null));
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsSuggestions_AndAppliesOverride()
    {
        // Blue is deliberately thin (only 10 Islands in a ~60-card library) so a 5-MV {U}{U}
        // Force of Will is hard to cast on curve — leaving real room for the free override to lift it.
        var entries = new List<DeckEntry>
        {
            Entry("Commander Guy", 1, "commander"),
            Land("Island", 10),
            Spell("Force of Will", "{3}{U}{U}", 5, "Instant").ToEntry(),
        };
        for (int i = 0; i < 50; i++)
        {
            entries.Add(Entry($"Filler {i}", 1, "mainboard"));
        }

        var fow = new ScryfallCard(
            Name: "Force of Will", ManaCost: "{3}{U}{U}", TypeLine: "Instant",
            OracleText: "You may pay 1 life and exile a blue card from your hand rather than pay this spell's mana cost. Counter target spell.",
            Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
            SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
            Layout: "normal", Cmc: 5, ProducedMana: null, Rarity: "rare");
        var cards = new List<ScryfallCard>
        {
            BasicLand("Island", "U"),
            Spell("Commander Guy", "{2}{U}", 3, "Legendary Creature — Human"),
            fow,
        };
        for (int i = 0; i < 50; i++)
        {
            cards.Add(Spell($"Filler {i}", "{2}", 3, "Sorcery"));
        }

        // P3 auto-apply (debug session manabase-too-optimistic): a SELF-ANCHORED free cast ("rather
        // than pay this spell's mana cost") is now auto-applied to the default analysis, so the
        // detect-only path already casts Force of Will at effective MV 0 and marks it overridden — it is
        // surfaced as a suggestion AND applied, no longer a false "demanding" {U}{U} row.
        var detectOnly = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));
        var detect = await detectOnly.AnalyzeAsync("paste", null);
        Assert.Contains(detect.Suggestions, s => s.Name == "Force of Will" && s.EffectiveCost == "0");
        CardCastability before = detect.Report.Castability.Single(c => c.Name == "Force of Will");
        Assert.True(before.IsCostOverridden);   // auto-applied free cost (was: not overridden pre-P3)
        Assert.Equal(0, before.ManaValue);

        // An explicit override to the same "0" is consistent with the auto-applied state: still
        // overridden, still MV 0, and at least as castable (it cannot be made harder).
        var applied = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));
        var withOverride = await applied.AnalyzeAsync(
            "paste", null,
            new ManabaseAnalysisOptions
            {
                CostOverrides = new Dictionary<string, string> { ["Force of Will"] = "0" },
            });
        CardCastability after = withOverride.Report.Castability.Single(c => c.Name == "Force of Will");
        Assert.True(after.IsCostOverridden);
        Assert.Equal(0, after.ManaValue);
        Assert.True(after.CastPercent >= before.CastPercent);
    }

    [Fact]
    public async Task LoadAsync_DetectsSuggestions_WithoutRunningAnalysis()
    {
        // Load mirrors the detect-only half of AnalyzeAsync: it resolves the deck and surfaces the
        // same cost suggestions (Force of Will → 0) plus a card/land summary, but produces no report.
        var entries = new List<DeckEntry>
        {
            Entry("Commander Guy", 1, "commander"),
            Land("Island", 10),
            Spell("Force of Will", "{3}{U}{U}", 5, "Instant").ToEntry(),
        };

        var fow = new ScryfallCard(
            Name: "Force of Will", ManaCost: "{3}{U}{U}", TypeLine: "Instant",
            OracleText: "You may pay 1 life and exile a blue card from your hand rather than pay this spell's mana cost. Counter target spell.",
            Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
            SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
            Layout: "normal", Cmc: 5, ProducedMana: null, Rarity: "rare");
        var cards = new List<ScryfallCard>
        {
            BasicLand("Island", "U"),
            Spell("Commander Guy", "{2}{U}", 3, "Legendary Creature — Human"),
            fow,
        };

        var service = new ManabaseAnalysisService(new FakeLoader(entries), new FakeResolver(cards));

        ManabaseLoadResult result = await service.LoadAsync("paste", CancellationToken.None);

        Assert.Contains(result.Suggestions, s => s.Name == "Force of Will" && s.EffectiveCost == "0");
        Assert.Contains("10 lands", result.InputSummary);
        Assert.Empty(result.Unresolved);
    }

    [Fact]
    public async Task LoadAsync_BlankSource_Throws()
    {
        var service = new ManabaseAnalysisService(new FakeLoader(new List<DeckEntry>()), new FakeResolver(new List<ScryfallCard>()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadAsync("   "));
    }

    // --- helpers -------------------------------------------------------------

    private static string FormatCastabilityRow(CardCastability row)
        => $"{row.Name}|{row.ManaValue}|{row.OnCurveTurn}|{row.CastPercent}|{row.IsCommander}";

    private static DeckEntry Entry(string name, int qty, string board, string? set = null, string? cn = null, string? category = null) => new()
    {
        Name = name,
        NormalizedName = name.ToLowerInvariant(),
        Quantity = qty,
        Board = board,
        SetCode = set,
        CollectorNumber = cn,
        Category = category,
    };

    private static DeckEntry Land(string name, int qty) => Entry(name, qty, "mainboard");

    private static ScryfallCard BasicLand(string name, string color) => new(
        Name: name, ManaCost: null, TypeLine: $"Basic Land — {name}", OracleText: null,
        Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
        SetCode: null, SetName: null, CollectorNumber: null, CardFaces: null, Id: null,
        Layout: "normal", Cmc: 0, ProducedMana: new[] { color }, Rarity: "common");

    private static ScryfallCard Spell(string name, string manaCost, double cmc, string typeLine, string? set = null, string? cn = null) => new(
        Name: name, ManaCost: manaCost, TypeLine: typeLine, OracleText: "...",
        Power: null, Toughness: null, Keywords: null, ColorIdentity: null,
        SetCode: set, SetName: null, CollectorNumber: cn, CardFaces: null, Id: null,
        Layout: "normal", Cmc: cmc, ProducedMana: null, Rarity: "rare");

    private sealed class FakeLoader : IDeckEntryLoader
    {
        private readonly List<DeckEntry> _entries;
        private readonly string? _detectedCompanionName;

        public FakeLoader(List<DeckEntry> entries, string? detectedCompanionName = null)
        {
            _entries = entries;
            _detectedCompanionName = detectedCompanionName;
        }

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeckSourceLoadResult(_entries, null, _detectedCompanionName));

        public Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100)
        {
        }
    }

    private sealed class FakeResolver : IScryfallCardResolver
    {
        private readonly List<ScryfallCard> _cards;

        public FakeResolver(List<ScryfallCard> cards) => _cards = cards;

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(_cards, null),
            });

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult(_cards.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase)));

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult<ScryfallCard?>(null);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult(_cards.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase)));
    }
}

public sealed class ManabaseControllerCompanionTests
{
    [Fact]
    public async Task Post_ThreadsCompanionDesignator_AndMapsCommanderCastabilityFields()
    {
        var companion = new CardCastability
        {
            Name = "Kaheera, the Orphanguard",
            ManaValue = 6,
            OnCurveTurn = 6,
            CastPercent = 55,
            LimitingFactor = "curve",
        };
        var service = new CapturingControllerService(ManabaseControllerModeTestsAccessor.CasualReport(), companion, commanderCastabilityEnabled: true);
        var controller = BuildController(service);

        var result = await controller.Manabase(new ManabaseRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            DeckText = "1 Kaheera, the Orphanguard",
            CompanionName = " Kaheera, the Orphanguard ",
        });

        Assert.NotNull(service.LastOptions);
        Assert.Equal(" Kaheera, the Orphanguard ", service.LastOptions!.CompanionDesignator);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ManabaseViewModel>(view.Model);
        Assert.True(model.ShowCommanderCastability);
        Assert.Same(companion, model.CompanionCallout);
    }

    private static ManabaseController BuildController(IManabaseAnalysisService service)
    {
        var controller = new ManabaseController(service, NullLogger<ManabaseController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        return controller;
    }

    private sealed class CapturingControllerService : IManabaseAnalysisService
    {
        private readonly ManabaseReport _report;
        private readonly CardCastability? _companionRow;
        private readonly bool _commanderCastabilityEnabled;

        public CapturingControllerService(
            ManabaseReport report,
            CardCastability? companionRow,
            bool commanderCastabilityEnabled)
        {
            _report = report;
            _companionRow = companionRow;
            _commanderCastabilityEnabled = commanderCastabilityEnabled;
        }

        public ManabaseAnalysisOptions? LastOptions { get; private set; }

        public Task<ManabaseAnalysisResult> AnalyzeAsync(
            string deckSource,
            string? deckName,
            ManabaseAnalysisOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options ?? new ManabaseAnalysisOptions();
            return Task.FromResult(new ManabaseAnalysisResult(
                _report,
                "1 cards · 36 lands",
                Array.Empty<string>(),
                null,
                "prompt",
                Array.Empty<CostSuggestion>(),
                null,
                null,
                false)
            {
                CommanderCastabilityEnabled = _commanderCastabilityEnabled,
                CompanionRow = _companionRow,
            });
        }

        public Task<ManabaseLoadResult> LoadAsync(
            string deckSource,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ManabaseLoadResult(
                "1 cards · 36 lands", Array.Empty<string>(), null, Array.Empty<CostSuggestion>()));
    }

    private static class ManabaseControllerModeTestsAccessor
    {
        public static ManabaseReport CasualReport() => new()
        {
            ActualLands = 36,
            TargetLands = 37.0,
            ColorFindings = Array.Empty<ColorSourceFinding>(),
            Mode = ManabaseMode.Casual,
            Castability = new[]
            {
                new CardCastability { Name = "Counterspell", ManaValue = 2, OnCurveTurn = 2, CastPercent = 62, LimitingFactor = "color:U" },
            },
            Summary = "ok",
        };
    }
}

internal static class ManabaseTestExtensions
{
    // Build a mainboard entry whose name matches a spell card, for terse arrange blocks.
    public static DeckEntry ToEntry(this ScryfallCard card) => new()
    {
        Name = card.Name,
        NormalizedName = card.Name.ToLowerInvariant(),
        Quantity = 1,
        Board = "mainboard",
    };
}
