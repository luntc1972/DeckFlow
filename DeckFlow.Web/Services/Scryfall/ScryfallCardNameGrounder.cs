using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using Microsoft.Extensions.Caching.Memory;

namespace DeckFlow.Web.Services.Scryfall;

/// <summary>
/// Grounds candidate card names through the shared, throttled Scryfall resolver path.
/// </summary>
public sealed class ScryfallCardNameGrounder(IScryfallCardResolver resolver, IMemoryCache cache) : ICardNameGrounder
{
    private static readonly TimeSpan PositiveCacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromHours(1);

    /// <inheritdoc />
    public async Task<CardGroundingResult> TryGroundAsync(string candidateName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return new CardGroundingResult(false, candidateName);
        }

        var cacheKey = BuildCacheKey(candidateName);
        if (cache.TryGetValue<CardGroundingResult>(cacheKey, out var cachedResult))
        {
            return cachedResult!;
        }

        CardGroundingResult result;
        try
        {
            var card = await resolver.SearchPrintingFallbackCardAsync(candidateName, cancellationToken).ConfigureAwait(false);
            result = card is not null
                ? new CardGroundingResult(true, card.Name)
                : new CardGroundingResult(false, candidateName);
        }
        catch
        {
            result = new CardGroundingResult(false, candidateName);
        }

        cache.Set(
            cacheKey,
            result,
            result.Resolved ? PositiveCacheTtl : NegativeCacheTtl);

        return result;
    }

    private static string BuildCacheKey(string candidateName)
        => "card-grounder:" + candidateName.Trim().ToLowerInvariant();
}
