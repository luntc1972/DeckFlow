using DeckFlow.Core.Bracket;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for <see cref="BracketClassifier"/> encoding the official Commander bracket rubric
/// (WotC Brackets Beta, Oct 2025 / Feb 2026 update). Covers all hard-floor conditions,
/// zero-signal default, B5 product heuristic, extra-turn informational-only, and null-combo
/// disclosure (Pitfall 1: null must never be silently treated as "zero combos").
/// </summary>
public sealed class BracketClassifierTests
{
    // -----------------------------------------------------------------------
    // Combined gating Theory — GC threshold / combo / MLD / zero-signal / B5
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, false, false, 2)]    // zero signals → B2 (ZeroSignalBracket)
    [InlineData(1, false, false, 3)]    // 1 GC, no combo, no MLD → B3
    [InlineData(3, false, false, 3)]    // 3 GCs, no combo, no MLD → B3
    [InlineData(4, false, false, 4)]    // 4 GCs → hard floor B4
    [InlineData(9, false, false, 4)]    // 9 GCs → still B4 (< CedhGameChangerCount)
    [InlineData(10, false, false, 5)]   // 10 GCs → B5 (CedhGameChangerCount product heuristic)
    [InlineData(0, true, false, 4)]     // two-card combo → hard floor B4
    [InlineData(0, false, true, 4)]     // MLD → hard floor B4
    [InlineData(3, true, false, 4)]     // 3 GCs + combo → B4 (combo trumps B3)
    public void Classify_BracketNumber_FromCombination(
        int gcCount, bool hasCombo, bool hasMld, int expectedBracket)
    {
        var catalog = BuildCatalog(gcCount, hasMld ? ["Armageddon"] : []);
        var entries = BuildEntries(catalog, hasMld: hasMld, hasExtraTurn: false);
        var combos = hasCombo
            ? BuildCombos(twoCardCount: 1)
            : (IReadOnlyList<TwoCardCombo>)[];
        var result = BracketClassifier.Classify(entries, catalog, combos);
        Assert.Equal(expectedBracket, result.BracketNumber);
    }

    // -----------------------------------------------------------------------
    // DetectedGameChangers is populated
    // -----------------------------------------------------------------------

    [Fact]
    public void Classify_DetectedGameChangers_MatchesDeckIntersect()
    {
        var catalog = BuildCatalog(gcCount: 3);
        var entries = BuildEntries(catalog);
        var result = BracketClassifier.Classify(entries, catalog, []);
        Assert.Equal(3, result.DetectedGameChangers.Count);
    }

    // -----------------------------------------------------------------------
    // MLD detection
    // -----------------------------------------------------------------------

    [Fact]
    public void Classify_DetectedMld_PopulatedWhenMldInDeck()
    {
        var catalog = BuildCatalog(gcCount: 0, mldCards: ["Armageddon", "Ravages of War"]);
        var entries = BuildEntries(catalog, hasMld: true);
        var result = BracketClassifier.Classify(entries, catalog, []);
        Assert.Equal(2, result.DetectedMassLandDenial.Count);
        Assert.Equal(4, result.BracketNumber);
    }

    // -----------------------------------------------------------------------
    // Extra-turn cards — informational only; no bracket change
    // -----------------------------------------------------------------------

    [Fact]
    public void Classify_ExtraTurnCards_AreInformationalOnly_DoNotRaiseBracket()
    {
        // A deck with only extra-turn cards (no GC, no combo, no MLD) must stay at B2
        // and DetectedExtraTurnCards must be populated.
        var catalog = BuildCatalog(gcCount: 0);
        var entries = BuildEntries(catalog, hasMld: false, hasExtraTurn: true);
        var result = BracketClassifier.Classify(entries, catalog, []);
        Assert.Equal(2, result.BracketNumber);
        Assert.True(result.DetectedExtraTurnCards.Count > 0,
            "DetectedExtraTurnCards should be populated when extra-turn cards are in the deck.");
    }

    // -----------------------------------------------------------------------
    // Null combo — Pitfall 1 (BRACKET-03 critical requirement)
    // -----------------------------------------------------------------------

    [Fact]
    public void Classify_NullCombo_SetsComboDetectionAvailableFalse()
    {
        var catalog = BuildCatalog(gcCount: 0);
        var result = BracketClassifier.Classify([], catalog, twoCardCombos: null);
        Assert.False(result.ComboDetectionAvailable,
            "ComboDetectionAvailable must be false when twoCardCombos is null.");
    }

    [Fact]
    public void Classify_NullComboResult_SetsComboDetectionAvailableFalse_AndDoesNotClaimZeroCombos()
    {
        // Pitfall 1: null must not be silently treated as "no combos found."
        // TwoCardCombos on the result must be null (not an empty list) — the caller must
        // disclose unavailability, not assert "0 two-card combos."
        var catalog = BuildCatalog(gcCount: 0);
        var result = BracketClassifier.Classify([], catalog, twoCardCombos: null);
        Assert.False(result.ComboDetectionAvailable);
        Assert.Null(result.TwoCardCombos);
    }

    [Fact]
    public void Classify_EmptyComboList_SetsComboDetectionAvailableTrue_AndZeroCombos()
    {
        // Empty list (non-null) means detection was available and returned no two-card combos.
        var catalog = BuildCatalog(gcCount: 0);
        var result = BracketClassifier.Classify([], catalog, twoCardCombos: []);
        Assert.True(result.ComboDetectionAvailable,
            "ComboDetectionAvailable must be true when an empty (non-null) list is passed.");
        Assert.NotNull(result.TwoCardCombos);
        Assert.Empty(result.TwoCardCombos);
    }

    // -----------------------------------------------------------------------
    // EffectiveDate formatting
    // -----------------------------------------------------------------------

    [Fact]
    public void Classify_EffectiveDate_IsFormattedYyyyMmDd()
    {
        var catalog = BuildCatalog(gcCount: 0);
        var result = BracketClassifier.Classify([], catalog, []);
        Assert.Equal("2026-02-09", result.EffectiveDate);
    }

    // -----------------------------------------------------------------------
    // Sideboard entries are excluded from bracket signals
    // -----------------------------------------------------------------------

    [Fact]
    public void Classify_SideboardEntries_AreIgnored()
    {
        // GCs / MLD cards on the sideboard must not trigger B4.
        var catalog = BuildCatalog(gcCount: 5, mldCards: ["Armageddon"]);
        var sideboardEntries = catalog.GameChangers
            .Concat(catalog.MassLandDenialCards)
            .Select(name => new DeckEntry
            {
                Name = name,
                NormalizedName = name.ToLowerInvariant(),
                Quantity = 1,
                Board = "sideboard",
            })
            .ToList();
        var result = BracketClassifier.Classify(sideboardEntries, catalog, []);
        Assert.Equal(2, result.BracketNumber);
        Assert.Empty(result.DetectedGameChangers);
        Assert.Empty(result.DetectedMassLandDenial);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static GameChangerCatalog BuildCatalog(int gcCount, IReadOnlyList<string>? mldCards = null)
    {
        var gameChangers = Enumerable.Range(0, gcCount)
            .Select(i => $"GameChanger_{i}")
            .ToList<string>();

        var extraTurnCards = new List<string> { "Time Walk", "Time Warp" };

        var tiers = new List<BracketTier>
        {
            new(1, "Exhibition", "Bracket 1: Exhibition", "Exhibition.", "9+ turns.", 0),
            new(2, "Core", "Bracket 2: Core", "Core.", "8+ turns.", 0),
            new(3, "Upgraded", "Bracket 3: Upgraded", "Upgraded.", "6+ turns.", 3),
            new(4, "Optimized", "Bracket 4: Optimized", "Optimized.", "4+ turns.", -1),
            new(5, "cEDH", "Bracket 5: cEDH", "cEDH.", "Any turn.", -1),
        };

        return new GameChangerCatalog(
            new DateOnly(2026, 2, 9),
            gameChangers,
            mldCards ?? [],
            extraTurnCards,
            tiers);
    }

    private static IReadOnlyList<DeckEntry> BuildEntries(
        GameChangerCatalog catalog,
        bool hasMld = false,
        bool hasExtraTurn = false)
    {
        var entries = new List<DeckEntry>();

        foreach (string gc in catalog.GameChangers)
        {
            entries.Add(new DeckEntry
            {
                Name = gc,
                NormalizedName = gc.ToLowerInvariant(),
                Quantity = 1,
                Board = "mainboard",
            });
        }

        if (hasMld)
        {
            foreach (string mld in catalog.MassLandDenialCards)
            {
                entries.Add(new DeckEntry
                {
                    Name = mld,
                    NormalizedName = mld.ToLowerInvariant(),
                    Quantity = 1,
                    Board = "mainboard",
                });
            }
        }

        if (hasExtraTurn)
        {
            foreach (string et in catalog.ExtraTurnCards)
            {
                entries.Add(new DeckEntry
                {
                    Name = et,
                    NormalizedName = et.ToLowerInvariant(),
                    Quantity = 1,
                    Board = "mainboard",
                });
            }
        }

        return entries;
    }

    private static IReadOnlyList<TwoCardCombo> BuildCombos(int twoCardCount) =>
        Enumerable.Range(0, twoCardCount)
            .Select(i => new TwoCardCombo(
                [$"Card_{i}_A", $"Card_{i}_B"],
                ["Win the game"]))
            .ToList();

    // -----------------------------------------------------------------------
    // FloorViolations — tier-aware violation derivation (FIX C)
    // -----------------------------------------------------------------------

    // Helper: build a BracketClassification directly (bypasses classifier logic)
    // so tests can set exact bracket + GC count independently.
    private static BracketClassification BuildClassification(
        int bracketNumber,
        IReadOnlyList<string>? gameChangers = null,
        IReadOnlyList<string>? mld = null,
        IReadOnlyList<string>? extraTurns = null,
        IReadOnlyList<TwoCardCombo>? combos = null) =>
        new(
            BracketNumber: bracketNumber,
            DetectedGameChangers: gameChangers ?? [],
            DetectedMassLandDenial: mld ?? [],
            DetectedExtraTurnCards: extraTurns ?? [],
            TwoCardCombos: combos,
            ComboDetectionAvailable: combos is not null,
            EffectiveDate: "2026-02-09");

    private static BracketTier BuildTier(int number, int maxGc = -1) =>
        new(number, $"T{number}", $"Bracket {number}", "Summary.", "N/A.", maxGc);

    [Fact]
    public void FloorViolations_B4Target_Uncapped_NoGCViolations()
    {
        // B4 deck (4 GCs) targeting B4 would not be "over target," but we can
        // still call FloorViolations to verify uncapped target returns no GC violations.
        var gcNames = new[] { "GC_1", "GC_2", "GC_3", "GC_4" };
        var cl = BuildClassification(bracketNumber: 4, gameChangers: gcNames);
        var targetTier = BuildTier(number: 4, maxGc: -1); // B4 is uncapped

        var result = cl.FloorViolations(targetTier);

        Assert.Empty(result.GameChangerViolations);
        Assert.False(result.IsCedhCountAdvisory, "No cEDH advisory for a B4 deck (< 10 GCs).");
    }

    [Fact]
    public void FloorViolations_B4Target_B5ByHeuristic_ReturnsCedhAdvisory()
    {
        // Deck with 10+ GCs classifies as B5 via the cEDH heuristic.
        // Target = B4 (uncapped). Expect advisory, no per-GC violations.
        var gcNames = Enumerable.Range(0, 10).Select(i => $"GC_{i}").ToArray();
        var cl = BuildClassification(bracketNumber: 5, gameChangers: gcNames);
        var targetTier = BuildTier(number: 4, maxGc: -1);

        var result = cl.FloorViolations(targetTier);

        Assert.True(result.IsCedhCountAdvisory);
        Assert.Equal(10, result.GameChangerCount);
        Assert.Empty(result.GameChangerViolations);
        Assert.Empty(result.ComboViolations);
        Assert.Empty(result.MldViolations);
    }

    [Fact]
    public void FloorViolations_B3Target_ExcessGCs_ReturnsGCViolations()
    {
        // 5 GCs, target B3 (cap = 3). Expect all 5 GCs listed as violations.
        var gcNames = new[] { "GC_1", "GC_2", "GC_3", "GC_4", "GC_5" };
        var cl = BuildClassification(bracketNumber: 4, gameChangers: gcNames);
        var targetTier = BuildTier(number: 3, maxGc: 3); // B3: cap 3

        var result = cl.FloorViolations(targetTier);

        Assert.Equal(5, result.GameChangerViolations.Count);
        Assert.False(result.IsCedhCountAdvisory);
    }

    [Fact]
    public void FloorViolations_B3Target_GCsAtOrBelowCap_NoGCViolations()
    {
        // 3 GCs, target B3 (cap = 3). Deck is AT cap — no GC violations.
        var gcNames = new[] { "GC_1", "GC_2", "GC_3" };
        var cl = BuildClassification(bracketNumber: 3, gameChangers: gcNames);
        var targetTier = BuildTier(number: 3, maxGc: 3);

        var result = cl.FloorViolations(targetTier);

        Assert.Empty(result.GameChangerViolations);
        Assert.False(result.IsCedhCountAdvisory);
    }

    [Fact]
    public void FloorViolations_B4Target_CombosNotViolations()
    {
        // Combos are a B4 gate; targeting B4 (which allows combos) means combos are NOT violations.
        var combo = new TwoCardCombo(["Card A", "Card B"], ["Win the game"]);
        var cl = BuildClassification(bracketNumber: 4, combos: [combo]);
        var targetTier = BuildTier(number: 4, maxGc: -1);

        var result = cl.FloorViolations(targetTier);

        Assert.Empty(result.ComboViolations);
    }

    [Fact]
    public void FloorViolations_B3Target_CombosAreViolations()
    {
        // Combos force B4+; if target is B3 (below B4), combos are violations.
        var combo = new TwoCardCombo(["Card A", "Card B"], ["Win the game"]);
        var cl = BuildClassification(bracketNumber: 4, combos: [combo]);
        var targetTier = BuildTier(number: 3, maxGc: 3);

        var result = cl.FloorViolations(targetTier);

        Assert.Single(result.ComboViolations);
    }

    [Fact]
    public void FloorViolations_B4Target_MldNotViolations()
    {
        // MLD is a B4 gate; targeting B4 (which allows MLD) means MLD is NOT a violation.
        var cl = BuildClassification(bracketNumber: 4, mld: ["Armageddon"]);
        var targetTier = BuildTier(number: 4, maxGc: -1);

        var result = cl.FloorViolations(targetTier);

        Assert.Empty(result.MldViolations);
    }

    [Fact]
    public void FloorViolations_B3Target_MldIsViolation()
    {
        // MLD forces B4+; if target is B3, MLD is a violation.
        var cl = BuildClassification(bracketNumber: 4, mld: ["Armageddon"]);
        var targetTier = BuildTier(number: 3, maxGc: 3);

        var result = cl.FloorViolations(targetTier);

        Assert.Single(result.MldViolations);
        Assert.Equal("Armageddon", result.MldViolations[0]);
    }

    [Fact]
    public void FloorViolations_ExtraTurnCards_NeverViolations()
    {
        // FIX B: extra-turn cards are informational only and must NEVER appear as violations
        // regardless of the target tier. This verifies the FloorViolationSet design.
        var cl = BuildClassification(
            bracketNumber: 4,
            extraTurns: ["Time Warp", "Time Walk"],
            gameChangers: ["GC_1", "GC_2", "GC_3", "GC_4"]);
        var targetTier = BuildTier(number: 2, maxGc: 0); // B2: cap 0

        var result = cl.FloorViolations(targetTier);

        // FloorViolationSet has no extra-turn field — they are simply absent by design.
        // Confirm the violation lists only contain GC, combo, and MLD entries.
        Assert.Equal(4, result.GameChangerViolations.Count); // all 4 GCs are violations at B2
        Assert.Empty(result.ComboViolations);
        Assert.Empty(result.MldViolations);
        // No assertion needed for extra turns — they have no place in FloorViolationSet.
    }
}
