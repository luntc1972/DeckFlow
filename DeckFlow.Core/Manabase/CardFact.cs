namespace DeckFlow.Core.Manabase;

/// <summary>
/// The minimal per-card data the classifier needs, shaped after Scryfall's card fields.
/// The Web layer fills these from its Scryfall adapter; <see cref="DeckFlow.Core"/> stays
/// HTTP-free and just consumes the facts.
/// </summary>
public sealed record CardFact
{
    /// <summary>Card name.</summary>
    public required string Name { get; init; }

    /// <summary>Copies of this card in the deck.</summary>
    public required int Quantity { get; init; }

    /// <summary>Scryfall mana cost of the front face (e.g. <c>{2}{U}{U}</c>); null for lands.</summary>
    public string? ManaCost { get; init; }

    /// <summary>Scryfall mana value (cmc) of the front face.</summary>
    public double ManaValue { get; init; }

    /// <summary>Scryfall type line (e.g. "Legendary Creature — Elf Druid", "Land").</summary>
    public required string TypeLine { get; init; }

    /// <summary>Scryfall oracle text (joined across faces), used for ramp/dork/rock heuristics.</summary>
    public string? OracleText { get; init; }

    /// <summary>Scryfall <c>produced_mana</c> letters (e.g. ["U","R","G"]); empty if none.</summary>
    public IReadOnlyList<string> ProducedMana { get; init; } = Array.Empty<string>();

    /// <summary>Scryfall rarity ("common", "uncommon", "rare", "mythic"); used for MDFC weighting.</summary>
    public string? Rarity { get; init; }

    /// <summary>Scryfall layout ("normal", "modal_dfc", "transform", "split", ...).</summary>
    public string? Layout { get; init; }

    /// <summary>True if any face of the card is a land (front, or the back of an MDFC).</summary>
    public bool HasLandFace { get; init; }

    /// <summary>True when in the command zone (commander) rather than the library.</summary>
    public bool IsCommander { get; init; }
}
