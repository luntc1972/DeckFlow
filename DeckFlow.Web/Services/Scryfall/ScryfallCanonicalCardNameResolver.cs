namespace DeckFlow.Web.Services.Scryfall;

/// <summary>Resolves canonical names through the shared Scryfall collection cache and reference resolver.</summary>
internal sealed class ScryfallCanonicalCardNameResolver : ICanonicalCardNameResolver
{
    private readonly ScryfallReferenceResolver _referenceResolver;
    private readonly Func<string, CancellationToken, Task<ScryfallCard?>> _fallbackStrategy;

    /// <summary>Creates a canonical-name resolver using the supplied Scryfall resolvers.</summary>
    public ScryfallCanonicalCardNameResolver(
        ScryfallReferenceResolver referenceResolver,
        IScryfallCardResolver scryfallCardResolver)
    {
        ArgumentNullException.ThrowIfNull(referenceResolver);
        ArgumentNullException.ThrowIfNull(scryfallCardResolver);
        _referenceResolver = referenceResolver;
        _fallbackStrategy = scryfallCardResolver.ResolveSingleAsync;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> ResolveCanonicalNamesAsync(
        IReadOnlyList<string> cardNames,
        CancellationToken cancellationToken = default)
    {
        // Why: single-card fallback accepts a cached DFC by its front face, avoiding another Scryfall call.
        var resolution = await _referenceResolver.ResolveBatchAsync(
            cardNames,
            _fallbackStrategy,
            normalizeForScryfall: false,
            cancellationToken: cancellationToken,
            batchFallbackStrategy: null);
        return resolution.OracleNameMap;
    }
}
