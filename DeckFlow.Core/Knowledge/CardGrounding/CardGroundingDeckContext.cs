namespace DeckFlow.Core.Knowledge.CardGrounding;

/// <summary>
/// Deck-context inputs required by the pure card-grounding rules.
/// </summary>
public sealed record CardGroundingDeckContext
{
    /// <summary>
    /// Gets the commander's color identity as WUBRG string symbols matching Scryfall <c>color_identity</c> values.
    /// </summary>
    public required IReadOnlySet<string> CommanderColorIdentity { get; init; }

    /// <summary>
    /// Gets the WUBRG mana colors already produced by the submitted deck's manabase.
    /// </summary>
    public required IReadOnlySet<char> DeckProducedColors { get; init; }

    /// <summary>
    /// Gets the submitted deck's existing card names, populated by the P99 caller with
    /// <see cref="Normalization.CardNormalizer.Normalize(string)"/> outputs so singleton checks match
    /// punctuation-collapsed, DFC-front-face, star/foil-stripped comparisons exactly.
    /// </summary>
    public required IReadOnlySet<string> DeckCardNames { get; init; }
}
