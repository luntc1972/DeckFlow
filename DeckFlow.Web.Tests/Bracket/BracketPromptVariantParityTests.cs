// Why: ADR-0001 — bracket prompt variants are intentionally decoupled; test instantiates
// each concrete variant directly without a shared helper (mirrors the same principle in production).
using DeckFlow.Core.Bracket;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Bracket;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// 3-platform parity tests (BRACKET-04): asserts that all three prompt variants
/// (ChatGpt / Claude / Gemini) emit the classification block, the balancer block,
/// the effective-date stamp, and the combo-unavailable disclosure under the right
/// conditions. No shared variant helper — each variant is instantiated directly.
/// </summary>
public sealed class BracketPromptVariantParityTests
{
    // Why: ADR-0001 — no shared helper; concrete variants instantiated inline.
    private static BracketPromptVariantRegistry BuildRegistry() =>
        new(new IBracketPromptVariant[]
        {
            new ChatGptBracketPromptVariant(),
            new ClaudeBracketPromptVariant(),
            new GeminiBracketPromptVariant(),
        });

    private static BracketClassification BuildClassification(
        int bracketNumber = 4,
        bool comboAvailable = true) =>
        new(
            BracketNumber: bracketNumber,
            DetectedGameChangers: ["Mana Crypt", "The One Ring", "Jeweled Lotus", "Cyclonic Rift"],
            DetectedMassLandDenial: ["Armageddon"],
            DetectedExtraTurnCards: [],
            TwoCardCombos: comboAvailable
                ? [new TwoCardCombo(["Thassa's Oracle", "Demonic Consultation"], ["Win on the spot"])]
                : null,
            ComboDetectionAvailable: comboAvailable,
            EffectiveDate: "2026-02-09");

    private static IReadOnlyList<BracketTier> BuildTiers() =>
    [
        new(1, "Exhibition", "Bracket 1: Exhibition", "Theme-first showcase decks; optimization takes a back seat. Expect 9+ turns before a win or loss.", "Expect 9+ turns", MaxGameChangers: 0),
        new(2, "Core",       "Bracket 2: Core",       "Unoptimized, straightforward decks with incremental, disruptable wins. Expect 8+ turns.",           "Expect 8+ turns", MaxGameChangers: 0),
        new(3, "Upgraded",   "Bracket 3: Upgraded",   "Strong synergy and card quality with meaningful interaction; explosive but earned wins. Expect 6+ turns.", "Expect 6+ turns", MaxGameChangers: 3),
        new(4, "Optimized",  "Bracket 4: Optimized",  "Fast, lethal, efficient decks with Game Changers, fast mana, and explosive lines. Expect 4+ turns.", "Expect 4+ turns", MaxGameChangers: -1),
        new(5, "cEDH",       "Bracket 5: cEDH",       "Metagame-tuned competitive Commander built for maximum efficiency and consistency. Games can end any turn.", "Games can end any turn", MaxGameChangers: -1),
    ];

    private static GameChangerCatalog BuildCatalog() =>
        new(
            EffectiveDate: new DateOnly(2026, 2, 9),
            GameChangers: ["Cyclonic Rift", "Jeweled Lotus", "Mana Crypt", "The One Ring"],
            MassLandDenialCards: ["Armageddon"],
            ExtraTurnCards: ["Time Warp"],
            Tiers: BuildTiers());

    // ── Classification block present in all three variants ─────────────────

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_ClassificationBlock_AppearsInAllThreeVariants(string platformName)
    {
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);

        var result = registry.Build(platform, BuildClassification(), null, null,
            BuildTiers(), BuildCatalog());

        Assert.Contains("WHY THIS BRACKET", result, StringComparison.Ordinal);
        Assert.Contains("Game Changers list effective", result, StringComparison.Ordinal);
    }

    // ── Balancer block present when target is below classified bracket ──────

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_BalancerBlock_AppearsInAllThreeVariants_WhenTargetBelowClassified(string platformName)
    {
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);
        // B4 classification with B2 target → over-target → balancer block required
        var result = registry.Build(platform, BuildClassification(bracketNumber: 4),
            targetBracketNumber: 2, null, BuildTiers(), BuildCatalog());

        Assert.Contains("FLOOR VIOLATIONS", result, StringComparison.Ordinal);
        Assert.Contains("STARTER CUTS", result, StringComparison.Ordinal);
    }

    // ── Balancer block absent when at or below target ───────────────────────

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_BalancerBlock_AbsentWhenAtOrBelowTarget(string platformName)
    {
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);
        // B3 classification with B3 target → meets target → no balancer block
        var result = registry.Build(platform, BuildClassification(bracketNumber: 3),
            targetBracketNumber: 3, null, BuildTiers(), BuildCatalog());

        Assert.DoesNotContain("FLOOR VIOLATIONS", result, StringComparison.Ordinal);
    }

    // ── Effective-date stamp present in all three variants ──────────────────

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_EffectiveDateStamp_AppearsInAllThreeVariants(string platformName)
    {
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);

        var result = registry.Build(platform, BuildClassification(), null, null,
            BuildTiers(), BuildCatalog());

        Assert.Contains("2026-02-09", result, StringComparison.Ordinal);
    }

    // ── Combo-unavailable disclosure (BRACKET-03, T-76-07) ──────────────────

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_ComboUnavailable_DisclosedInAllThreeVariants_WhenDetectionUnavailable(string platformName)
    {
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);
        var classification = BuildClassification(comboAvailable: false);

        var result = registry.Build(platform, classification, null, null,
            BuildTiers(), BuildCatalog());

        // Must disclose unavailability
        Assert.Contains("combo detection", result, StringComparison.OrdinalIgnoreCase);
        // Must NOT claim zero combos when detection was unavailable
        Assert.DoesNotContain("0 two-card combos", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no combos found", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── FIX C: B5→B4 via cEDH heuristic → count advisory, no per-GC violations ─────

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_B4Target_B5ByHeuristic_EmitsCedhAdvisory_NotPerGcViolations(string platformName)
    {
        // Deck with 10 GCs classifies as B5 via the cEDH heuristic.
        // Target = B4 (uncapped). All three variants must emit the count advisory,
        // NOT individual GC names in the violations block.
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);

        var gcNames = Enumerable.Range(0, 10).Select(i => $"GC_{i:D2}").ToList<string>();
        var classification = new BracketClassification(
            BracketNumber: 5,
            DetectedGameChangers: gcNames,
            DetectedMassLandDenial: [],
            DetectedExtraTurnCards: [],
            TwoCardCombos: null,
            ComboDetectionAvailable: false,
            EffectiveDate: "2026-02-09");

        var result = registry.Build(platform, classification,
            targetBracketNumber: 4, null, BuildTiers(), BuildCatalog());

        // Advisory must appear
        Assert.Contains("Trim Game Changers below 10", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("10", result, StringComparison.Ordinal);

        // Individual GC names must NOT appear as violations (the count advisory replaces them)
        foreach (var gc in gcNames)
        {
            // The GC names may appear in the classification reasons block (GC count line)
            // but should NOT appear in a [Game Changer] violation entry
            Assert.DoesNotContain($"{gc} [Game Changer]", result, StringComparison.Ordinal);
        }
    }

    // ── FIX C: B3 target with >3 GCs → GCs listed as violations ────────────

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_B3Target_ExcessGCs_ListsGCsAsViolations(string platformName)
    {
        // Deck with 5 GCs, target B3 (cap = 3). All three variants must list the
        // individual GC names as Game Changer violations.
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);

        var gcNames = new List<string>
        {
            "Mana Crypt", "Cyclonic Rift", "The One Ring", "Jeweled Lotus", "Rhystic Study",
        };
        var classification = new BracketClassification(
            BracketNumber: 4,
            DetectedGameChangers: gcNames,
            DetectedMassLandDenial: [],
            DetectedExtraTurnCards: [],
            TwoCardCombos: [],
            ComboDetectionAvailable: true,
            EffectiveDate: "2026-02-09");

        var result = registry.Build(platform, classification,
            targetBracketNumber: 3, null, BuildTiers(), BuildCatalog());

        Assert.Contains("FLOOR VIOLATIONS", result, StringComparison.Ordinal);
        // All 5 GCs must appear as violations
        foreach (var gc in gcNames)
        {
            Assert.Contains(gc, result, StringComparison.Ordinal);
        }
        // Must NOT show the cEDH count advisory (that is only for B5→B4)
        Assert.DoesNotContain("Trim Game Changers below 10", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── FIX B: extra-turn cards never appear as violations ──────────────────

    [Theory]
    [InlineData("ChatGPT")]
    [InlineData("Claude")]
    [InlineData("Gemini")]
    public void Build_ExtraTurnCards_NeverAppearAsFloorViolations(string platformName)
    {
        // A B4 deck with extra-turn cards, targeting B2. Extra-turn cards must appear
        // in the WHY THIS BRACKET informational line but must NOT appear as violations
        // (no "[Extra turns]" / "bracket-violation" entry in any variant).
        var registry = BuildRegistry();
        var platform = AiPlatform.Normalize(platformName);

        var classification = new BracketClassification(
            BracketNumber: 4,
            DetectedGameChangers: ["Mana Crypt", "Cyclonic Rift", "The One Ring", "Jeweled Lotus"],
            DetectedMassLandDenial: [],
            DetectedExtraTurnCards: ["Time Warp", "Walk the Aeons"],
            TwoCardCombos: [],
            ComboDetectionAvailable: true,
            EffectiveDate: "2026-02-09");

        var result = registry.Build(platform, classification,
            targetBracketNumber: 2, null, BuildTiers(), BuildCatalog());

        // Extra-turn count must appear in informational section
        Assert.Contains("extra-turn", result, StringComparison.OrdinalIgnoreCase);

        // Extra-turn cards must NOT appear as violations (no [Extra turns] tag)
        Assert.DoesNotContain("[Extra turns]", result, StringComparison.Ordinal);
        Assert.DoesNotContain("bracket-violation__tag--extraturns", result, StringComparison.Ordinal);
        // The specific card names may appear in the informational section but not as violations
        Assert.DoesNotContain("Time Warp [", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Walk the Aeons [", result, StringComparison.Ordinal);
    }
}
