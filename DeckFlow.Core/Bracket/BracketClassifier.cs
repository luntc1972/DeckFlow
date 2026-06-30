using System.Globalization;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Bracket;

/// <summary>
/// Pure static classifier that maps a list of <see cref="DeckEntry"/> records to a
/// <see cref="BracketClassification"/> using the official Commander bracket rubric
/// (WotC Brackets Beta, October 2025 / February 2026).
/// </summary>
/// <remarks>
/// No DI, no HTTP — pure transform. Consumes the pre-loaded <see cref="GameChangerCatalog"/>
/// and an optional list of two-card combos sourced from the Web layer's combo detection service.
/// The Web orchestrator (plan 76-04) maps <c>SpellbookCombo</c> → <c>TwoCardCombo</c>
/// before calling this method, keeping <c>DeckFlow.Core</c> free of any <c>DeckFlow.Web</c>
/// reference.
/// </remarks>
public static class BracketClassifier
{
    /// <summary>
    /// Classifies a Commander deck into an official bracket (1–5).
    /// </summary>
    /// <param name="entries">All entries in the deck (any board).</param>
    /// <param name="catalog">Versioned Game Changers + tier data loaded at startup.</param>
    /// <param name="twoCardCombos">
    /// Two-card combos detected for this deck, or <see langword="null"/> when combo detection
    /// was unavailable. <c>null</c> and an empty list have different semantics: null means
    /// "detection unavailable"; empty means "detection ran and found no two-card combos."
    /// Passing null sets <see cref="BracketClassification.ComboDetectionAvailable"/> to false
    /// and does NOT gate the bracket downward (avoiding a false "no combos" assertion).
    /// </param>
    /// <returns>The computed <see cref="BracketClassification"/>.</returns>
    public static BracketClassification Classify(
        IReadOnlyList<DeckEntry> entries,
        GameChangerCatalog catalog,
        IReadOnlyList<TwoCardCombo>? twoCardCombos)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(catalog);

        // Board filter: only mainboard and commander cards contribute bracket signals.
        // Sideboard / maybeboard entries are ignored — they are not in the active deck.
        var deckNames = entries
            .Where(static e => e.Board is "mainboard" or "commander")
            .Select(static e => e.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Intersect catalog lists against deck card names.
        var detectedGCs = catalog.GameChangers
            .Where(gc => deckNames.Contains(gc))
            .ToList();

        var detectedMld = catalog.MassLandDenialCards
            .Where(c => deckNames.Contains(c))
            .ToList();

        // Extra-turn detection is informational only — populated in the result but never
        // used as a B4 hard floor per the current WotC rubric (scrollvault.net cross-check).
        var detectedExtraTurns = catalog.ExtraTurnCards
            .Where(c => deckNames.Contains(c))
            .ToList();

        bool comboAvailable = twoCardCombos is not null;

        // When unavailable (null), treat as empty for gating purposes — the absence of combo
        // data must NOT be interpreted as "zero combos." Only a non-null list with Count > 0
        // triggers the B4 hard floor (see Pitfall 1, RESEARCH §3.2).
        var combos = twoCardCombos ?? (IReadOnlyList<TwoCardCombo>)[];

        // --- Bracket gating (official WotC rubric + CedhGameChangerCount product heuristic) ---

        int bracketNumber;

        // Why: CedhGameChangerCount (10) is a PRODUCT HEURISTIC, not an official WotC threshold.
        // WotC defines Bracket 5 as "metagame-tuned cEDH" without a crisp card-count gate.
        // DeckFlow uses >= 10 Game Changers as a pragmatic auto-B5 signal. The downstream
        // classification artifact (76-04) explicitly instructs the AI to re-confirm B5 / cEDH
        // placement at the meta level rather than accepting this threshold as authoritative.
        if (detectedGCs.Count >= BracketRubricThresholds.CedhGameChangerCount)
        {
            bracketNumber = 5;
        }
        else if (detectedMld.Count > 0
            || combos.Count > 0
            || detectedGCs.Count >= BracketRubricThresholds.HardFloorGameChangerCount)
        {
            // B4 hard floor: MLD present, two-card combo present, or 4+ Game Changers.
            bracketNumber = 4;
        }
        else if (detectedGCs.Count >= BracketRubricThresholds.MinGameChangersForB3)
        {
            // B3: 1–3 Game Changers with no combo and no MLD.
            bracketNumber = 3;
        }
        else
        {
            // Zero signals → ZeroSignalBracket (B2/Core). Bracket 1 (Exhibition) requires
            // explicit self-declaration and is never auto-assigned per the WotC rubric.
            bracketNumber = BracketRubricThresholds.ZeroSignalBracket;
        }

        return new BracketClassification(
            BracketNumber: bracketNumber,
            DetectedGameChangers: detectedGCs,
            DetectedMassLandDenial: detectedMld,
            DetectedExtraTurnCards: detectedExtraTurns,
            TwoCardCombos: comboAvailable ? combos : null,
            ComboDetectionAvailable: comboAvailable,
            EffectiveDate: catalog.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
