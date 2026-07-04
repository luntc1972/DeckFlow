namespace DeckFlow.Core.Analysis;

/// <summary>
/// Coarse assembly-speed band for a combo, derived from its <c>ManaValueNeeded</c>.
/// Bands are deliberately coarse (never a turn number) so the surface communicates a
/// heuristic first pass rather than a precise turn-count claim.
/// </summary>
public enum WinConBand
{
    /// <summary>Fast assembly (low mana value needed).</summary>
    Early,

    /// <summary>Moderate assembly speed.</summary>
    Mid,

    /// <summary>Slow assembly speed.</summary>
    Late,

    /// <summary>Assembly speed could not be banded (no mana value data).</summary>
    Unknown,
}

/// <summary>
/// A ranked, banded combo included in the deck.
/// </summary>
/// <param name="CardNames">Card names required to assemble the combo.</param>
/// <param name="Results">The combo's resulting effect(s).</param>
/// <param name="ManaValueNeeded">Total mana value needed to assemble the combo, when known.</param>
/// <param name="Popularity">Popularity signal for the combo, when known.</param>
/// <param name="Band">Coarse assembly-speed band derived from <paramref name="ManaValueNeeded"/>.</param>
public sealed record WinConCombo(
    IReadOnlyList<string> CardNames,
    IReadOnlyList<string> Results,
    int? ManaValueNeeded,
    int? Popularity,
    WinConBand Band);

/// <summary>
/// A one-card-away combo not currently in the deck — a redundancy signal, strictly
/// separate from <see cref="WinConCombo"/> (never merged into the included-combos list).
/// </summary>
/// <param name="MissingCard">The single card missing to complete the combo.</param>
/// <param name="CardsInDeck">The combo's other cards already present in the deck.</param>
/// <param name="Results">The combo's resulting effect(s).</param>
public sealed record WinConNearCombo(
    string MissingCard,
    IReadOnlyList<string> CardsInDeck,
    IReadOnlyList<string> Results);

/// <summary>
/// A non-combo closing-power card and its quantity in the deck.
/// </summary>
/// <param name="Name">Card name.</param>
/// <param name="Quantity">Number of copies of this card in the deck.</param>
public readonly record struct WinConClosingCard(string Name, int Quantity);

/// <summary>
/// The deck's win-condition / combo map: ranked included combos, strictly-separate
/// one-card-away near-combos, an assembly-path count, non-combo closing-power cards,
/// and a sentinel distinguishing "combo data unavailable" from "ran, found none".
/// </summary>
/// <param name="Combos">Included combos, ranked low <c>ManaValueNeeded</c> first, then high <c>Popularity</c> first, then by normalized card names as a final deterministic tie-breaker.</param>
/// <param name="NearCombos">One-card-away near-combos; never merged with <paramref name="Combos"/>.</param>
/// <param name="AssemblyPathCount">Count of complete included combos (near-combos are not assembly paths).</param>
/// <param name="ClosingCards">Non-combo closing-power cards, so a combo-less deck still yields a win-condition read.</param>
/// <param name="ComboDataAvailable"><see langword="true"/> when combo lookup ran (even if it found nothing); <see langword="false"/> when lookup failed/was unavailable.</param>
/// <param name="OverallBand">The band of the fastest (lowest <c>ManaValueNeeded</c>) included combo; <see cref="WinConBand.Unknown"/> when no banded combo exists.</param>
public sealed record WinConMap(
    IReadOnlyList<WinConCombo> Combos,
    IReadOnlyList<WinConNearCombo> NearCombos,
    int AssemblyPathCount,
    IReadOnlyList<WinConClosingCard> ClosingCards,
    bool ComboDataAvailable,
    WinConBand OverallBand);

/// <summary>
/// Input DTO for an included combo, mapped from an upstream combo-lookup result.
/// </summary>
/// <param name="CardNames">Card names required to assemble the combo.</param>
/// <param name="Results">The combo's resulting effect(s).</param>
/// <param name="ManaValueNeeded">Total mana value needed to assemble the combo, when known.</param>
/// <param name="Popularity">Popularity signal for the combo, when known.</param>
public readonly record struct WinConComboInput(
    IReadOnlyList<string> CardNames,
    IReadOnlyList<string> Results,
    int? ManaValueNeeded,
    int? Popularity);

/// <summary>
/// Input DTO for a one-card-away near-combo, mapped from an upstream combo-lookup result.
/// </summary>
/// <param name="MissingCard">The single card missing to complete the combo.</param>
/// <param name="CardsInDeck">The combo's other cards already present in the deck.</param>
/// <param name="Results">The combo's resulting effect(s).</param>
public readonly record struct WinConNearComboInput(
    string MissingCard,
    IReadOnlyList<string> CardsInDeck,
    IReadOnlyList<string> Results);

/// <summary>
/// Input DTO for a candidate closing-power card, evaluated via <see cref="DeckStatClassifier.IsClosingPowerCard"/>.
/// </summary>
/// <param name="Quantity">Number of copies of this card in the deck.</param>
/// <param name="Name">Card name.</param>
/// <param name="TypeLine">Card type line.</param>
/// <param name="OracleText">Normalized oracle text.</param>
public readonly record struct WinConClosingCardInput(int Quantity, string Name, string TypeLine, string OracleText);
