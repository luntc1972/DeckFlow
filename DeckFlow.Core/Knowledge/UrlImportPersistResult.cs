namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Reports whether URL import was refused because the deck is archived.
/// </summary>
/// <param name="Archived">Whether the deck is archived and no import was written.</param>
public sealed record UrlImportPersistResult(bool Archived);
