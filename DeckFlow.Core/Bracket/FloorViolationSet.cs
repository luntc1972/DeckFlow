namespace DeckFlow.Core.Bracket;

/// <summary>
/// The tier-aware set of floor violations for a given target bracket tier.
/// Derived via <see cref="BracketClassification.FloorViolations"/>; do not construct
/// directly — use the domain method so rules are applied consistently across the view
/// and all three prompt variants.
/// </summary>
/// <param name="GameChangerViolations">
/// Game Changers that exceed the target tier's cap. Empty when the target tier is
/// uncapped (B4/B5: <see cref="BracketTier.MaxGameChangers"/> == <c>-1</c>).
/// For uncapped targets, see <see cref="IsCedhCountAdvisory"/> instead.
/// </param>
/// <param name="ComboViolations">
/// Two-card combos that are floor violations for the target tier. Only populated
/// when the target tier is below B4 — two-card combos are a B4 gate.
/// </param>
/// <param name="MldViolations">
/// Mass land denial cards that are floor violations for the target tier. Only populated
/// when the target tier is below B4 — MLD is a B4 gate.
/// </param>
/// <param name="IsCedhCountAdvisory">
/// <see langword="true"/> when the deck is B5 via the cEDH GC-count heuristic
/// (≥ 10 Game Changers) and the target tier is B4 (uncapped). In this case no
/// individual Game Changers are listed as violations; a count advisory is surfaced
/// instead: "Trim Game Changers below 10 (currently N) to drop from B5 to B4."
/// </param>
/// <param name="GameChangerCount">
/// Total Game Changers detected in the deck. Used to populate the count advisory
/// text and the "you run N" suffix in GC-excess cut messages.
/// </param>
public sealed record FloorViolationSet(
    IReadOnlyList<string> GameChangerViolations,
    IReadOnlyList<TwoCardCombo> ComboViolations,
    IReadOnlyList<string> MldViolations,
    bool IsCedhCountAdvisory,
    int GameChangerCount);
