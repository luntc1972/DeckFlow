using System.Text.Json.Serialization;

namespace DeckSyncWorkbench.Web.Services;

/// <summary>
/// Container for a Scryfall card search response.
/// </summary>
public sealed record ScryfallSearchResponse(List<ScryfallCard> Data);

/// <summary>
/// Container for a Scryfall collection lookup response.
/// </summary>
public sealed record ScryfallCollectionResponse(List<ScryfallCard> Data, List<ScryfallCollectionIdentifier>? NotFound);

/// <summary>
/// Represents a Scryfall card payload.
/// </summary>

public sealed record ScryfallCard(
    string Name,
    [property: JsonPropertyName("mana_cost")] string? ManaCost,
    [property: JsonPropertyName("type_line")] string TypeLine,
    [property: JsonPropertyName("oracle_text")] string? OracleText,
    [property: JsonPropertyName("power")] string? Power,
    [property: JsonPropertyName("toughness")] string? Toughness);

/// <summary>
/// Represents an identifier Scryfall could not resolve from a collection request.
/// </summary>
public sealed record ScryfallCollectionIdentifier(string? Name);
