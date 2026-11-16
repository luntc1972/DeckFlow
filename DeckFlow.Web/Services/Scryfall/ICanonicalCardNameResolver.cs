namespace DeckFlow.Web.Services.Scryfall;

/// <summary>Resolves requested names to canonical Scryfall card names.</summary>
internal interface ICanonicalCardNameResolver
{
    /// <summary>
    /// Returns canonical names keyed by the exact request names, compared using
    /// <see cref="StringComparer.OrdinalIgnoreCase"/>. Unresolved names are absent.
    /// Empty input returns an empty map without calling Scryfall. May throw; callers own fail-open handling.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> ResolveCanonicalNamesAsync(
        IReadOnlyList<string> cardNames,
        CancellationToken cancellationToken = default);
}
