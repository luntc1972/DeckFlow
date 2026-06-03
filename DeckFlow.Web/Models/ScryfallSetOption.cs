namespace DeckFlow.Web.Models;

/// <summary>Scryfall set option shown when filtering card search or lookup flows.</summary>
/// <param name="Code">Scryfall set code.</param>
/// <param name="Name">Display name of the Scryfall set.</param>
/// <param name="ReleasedAt">Release date text returned by Scryfall, when available.</param>
/// <param name="SetType">Scryfall set type, when available.</param>
public sealed record ScryfallSetOption(
    string Code,
    string Name,
    string? ReleasedAt,
    string? SetType = null)
{
    /// <summary>Combined set name, code, and optional release date for select lists.</summary>
    public string DisplayLabel
        => string.IsNullOrWhiteSpace(ReleasedAt)
            ? $"{Name} ({Code.ToUpperInvariant()})"
            : $"{Name} ({Code.ToUpperInvariant()}) - {ReleasedAt}";
}
