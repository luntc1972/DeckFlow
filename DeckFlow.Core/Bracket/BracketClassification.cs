namespace DeckFlow.Core.Bracket;

/// <summary>
/// The result of classifying a Commander deck into an official bracket (1–5)
/// using the Game Changers, Mass Land Denial, and two-card combo signals.
/// </summary>
/// <param name="BracketNumber">The computed bracket number (1–5).</param>
/// <param name="DetectedGameChangers">
/// Card names from the deck that appear in the Game Changers catalog list.
/// </param>
/// <param name="DetectedMassLandDenial">
/// Card names from the deck that appear in the Mass Land Denial curated list.
/// Any match forces Bracket 4 or higher.
/// </param>
/// <param name="DetectedExtraTurnCards">
/// Card names from the deck that appear in the extra-turn curated list.
/// Informational only — does not change the bracket number.
/// </param>
/// <param name="TwoCardCombos">
/// Two-card combos present in the deck; <see langword="null"/> when combo detection was
/// unavailable (service returned null). Do not interpret null as "zero combos."
/// </param>
/// <param name="ComboDetectionAvailable">
/// <see langword="true"/> when a combo detection service was queried and returned a result
/// (even an empty one); <see langword="false"/> when detection was skipped or the service
/// returned null. Consumers must disclose unavailability rather than asserting "no combos."
/// </param>
/// <param name="EffectiveDate">
/// Effective date of the Game Changers catalog used for this classification (e.g., "2026-02-09").
/// Formatted "yyyy-MM-dd" using InvariantCulture.
/// </param>
public sealed record BracketClassification(
    int BracketNumber,
    IReadOnlyList<string> DetectedGameChangers,
    IReadOnlyList<string> DetectedMassLandDenial,
    IReadOnlyList<string> DetectedExtraTurnCards,
    IReadOnlyList<TwoCardCombo>? TwoCardCombos,
    bool ComboDetectionAvailable,
    string EffectiveDate)
{
    /// <summary>
    /// Derives the tier-aware set of floor violations for a given target bracket tier.
    /// </summary>
    /// <remarks>
    /// Rules per target tier:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <strong>Game Changers</strong> — violations only when the target tier caps them
    ///       (<see cref="BracketTier.MaxGameChangers"/> &gt;= 0) AND the deck exceeds that cap.
    ///       For uncapped targets (B4/B5), individual GCs are NOT violations. If the deck is
    ///       B5 via the cEDH 10-GC heuristic and the target is B4, a count advisory is emitted
    ///       instead via <see cref="FloorViolationSet.IsCedhCountAdvisory"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <strong>Two-card combos</strong> — violations only when the target tier number
    ///       is below 4 (combos are a B4 gate; B4 and B5 allow them).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <strong>Mass land denial</strong> — violations only when the target tier number
    ///       is below 4 (MLD is a B4 gate; B4 and B5 allow MLD).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <strong>Extra-turn cards</strong> — NEVER violations; informational only per the
    ///       current WotC rubric.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    /// <param name="targetTier">The bracket tier the user wants to reach.</param>
    /// <returns>A <see cref="FloorViolationSet"/> describing which cards are violations.</returns>
    public FloorViolationSet FloorViolations(BracketTier targetTier)
    {
        ArgumentNullException.ThrowIfNull(targetTier);

        // Why: Game Changers are violations ONLY when the target tier caps them AND the
        // deck exceeds that cap. B4/B5 (MaxGameChangers == -1) are uncapped — individual
        // GCs are not violations there. Exception: if the deck is B5 via the cEDH heuristic
        // (≥ 10 GCs) and target is B4, surface a count advisory instead of per-card violations.
        IReadOnlyList<string> gcViolations;
        bool isCedhAdvisory = false;

        if (targetTier.MaxGameChangers >= 0)
        {
            // Target has a cap — list all GCs when the deck exceeds it.
            gcViolations = DetectedGameChangers.Count > targetTier.MaxGameChangers
                ? DetectedGameChangers
                : (IReadOnlyList<string>)[];
        }
        else
        {
            // Target is uncapped (B4 or B5). No per-GC violations.
            // But if the deck landed at B5 via the GC-count product heuristic, surface
            // an advisory so the user knows what to trim to exit B5.
            isCedhAdvisory = BracketNumber >= 5
                && DetectedGameChangers.Count >= BracketRubricThresholds.CedhGameChangerCount;
            gcViolations = [];
        }

        // Two-card combos: only a violation when target is below B4 (combo forces B4+).
        IReadOnlyList<TwoCardCombo> comboViolations = targetTier.Number < 4
            && TwoCardCombos is { Count: > 0 } combos
                ? combos
                : (IReadOnlyList<TwoCardCombo>)[];

        // Mass land denial: only a violation when target is below B4 (MLD forces B4+).
        IReadOnlyList<string> mldViolations = targetTier.Number < 4
            ? DetectedMassLandDenial
            : (IReadOnlyList<string>)[];

        // Extra-turn cards are informational only — they NEVER constitute floor violations.

        return new FloorViolationSet(
            GameChangerViolations: gcViolations,
            ComboViolations: comboViolations,
            MldViolations: mldViolations,
            IsCedhCountAdvisory: isCedhAdvisory,
            GameChangerCount: DetectedGameChangers.Count);
    }
}
