using System.Text.Json.Serialization;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// The subset of a Scryfall card payload the mana-base mapper needs, shaped after the
/// Scryfall JSON so a Web adapter can deserialize a card response straight into it.
/// Lives in <see cref="DeckFlow.Core"/> so the mapper stays HTTP-free; the Web layer owns
/// the actual fetch.
/// </summary>
public sealed record ScryfallCardData
{
    /// <summary>Card name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Card-level mana cost (e.g. <c>{2}{U}{U}</c>); null for lands and most MDFCs.</summary>
    [JsonPropertyName("mana_cost")]
    public string? ManaCost { get; init; }

    /// <summary>Converted mana value / mana value of the card.</summary>
    [JsonPropertyName("cmc")]
    public double Cmc { get; init; }

    /// <summary>Card-level type line (e.g. "Sorcery // Land", "Basic Land — Forest").</summary>
    [JsonPropertyName("type_line")]
    public string? TypeLine { get; init; }

    /// <summary>Card-level oracle text (single-faced cards).</summary>
    [JsonPropertyName("oracle_text")]
    public string? OracleText { get; init; }

    /// <summary>Scryfall <c>produced_mana</c> letters (e.g. ["U","R","G"]); colors this card can tap for.</summary>
    [JsonPropertyName("produced_mana")]
    public IReadOnlyList<string>? ProducedMana { get; init; }

    /// <summary>Scryfall <c>color_identity</c> letters (e.g. ["W","U"]); the commander's deckbuilding colors for this card.</summary>
    [JsonPropertyName("color_identity")]
    public IReadOnlyList<string>? ColorIdentity { get; init; }

    /// <summary>Rarity ("common", "uncommon", "rare", "mythic").</summary>
    [JsonPropertyName("rarity")]
    public string? Rarity { get; init; }

    /// <summary>Set code of the resolved printing (e.g. "iko"); used for printing-based lookup.</summary>
    [JsonPropertyName("set")]
    public string? Set { get; init; }

    /// <summary>Collector number within the set; used for printing-based lookup.</summary>
    [JsonPropertyName("collector_number")]
    public string? CollectorNumber { get; init; }

    /// <summary>Layout ("normal", "modal_dfc", "transform", "split", "adventure", ...).</summary>
    [JsonPropertyName("layout")]
    public string? Layout { get; init; }

    /// <summary>Card-level printed power for single-faced creatures (e.g. "5", "*"); null otherwise.</summary>
    [JsonPropertyName("power")]
    public string? Power { get; init; }

    /// <summary>Card-level printed toughness for single-faced creatures (e.g. "5", "*"); null otherwise.</summary>
    [JsonPropertyName("toughness")]
    public string? Toughness { get; init; }

    /// <summary>Per-face payloads for multi-faced cards (MDFC, split, adventure, transform).</summary>
    [JsonPropertyName("card_faces")]
    public IReadOnlyList<ScryfallFaceData>? CardFaces { get; init; }
}

/// <summary>One face of a multi-faced Scryfall card.</summary>
public sealed record ScryfallFaceData
{
    /// <summary>Face name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Face mana cost.</summary>
    [JsonPropertyName("mana_cost")]
    public string? ManaCost { get; init; }

    /// <summary>Face type line.</summary>
    [JsonPropertyName("type_line")]
    public string? TypeLine { get; init; }

    /// <summary>Face oracle text.</summary>
    [JsonPropertyName("oracle_text")]
    public string? OracleText { get; init; }

    /// <summary>Face printed power for a creature face (e.g. "5", "*"); null otherwise.</summary>
    [JsonPropertyName("power")]
    public string? Power { get; init; }

    /// <summary>Face printed toughness for a creature face (e.g. "5", "*"); null otherwise.</summary>
    [JsonPropertyName("toughness")]
    public string? Toughness { get; init; }
}
