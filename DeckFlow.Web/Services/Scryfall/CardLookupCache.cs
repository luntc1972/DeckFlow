using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services;

/// <summary>
/// Dedicated per-card response cache for <see cref="ScryfallCardLookupService"/> fallback lookups.
/// Owns a private <see cref="MemoryCache"/> instance with a fixed size cap so it never
/// interferes with the shared <c>IMemoryCache</c> (which carries no <c>SizeLimit</c>).
/// </summary>
public sealed class CardLookupCache
{
    // Sentinel stored for negative (not-found) entries so we can distinguish
    // "cache hit, card is null" from "cache miss" via MemoryCache.TryGetValue.
    private static readonly object NegativeMarker = new();

    private readonly IMemoryCache _cache;
    private readonly ILogger<CardLookupCache> _logger;

    /// <summary>
    /// Initialises a new <see cref="CardLookupCache"/> with an optional logger.
    /// </summary>
    /// <param name="logger">Optional structured logger; falls back to <see cref="NullLogger{T}"/> when not provided.</param>
    public CardLookupCache(ILogger<CardLookupCache>? logger = null)
    {
        _logger = logger ?? NullLogger<CardLookupCache>.Instance;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10_000 });
    }

    /// <summary>
    /// Attempts to retrieve a previously cached card lookup result.
    /// </summary>
    /// <param name="cardName">The card name as supplied by the caller (normalised internally).</param>
    /// <param name="card">
    /// When this method returns <see langword="true"/>, contains the cached <see cref="ScryfallCard"/>
    /// or <see langword="null"/> for a negative-cache entry (card not found on Scryfall).
    /// </param>
    /// <returns>
    /// <see langword="true"/> on a cache hit (including negative entries); <see langword="false"/> on a miss.
    /// </returns>
    public bool TryGetCard(string cardName, out ScryfallCard? card)
    {
        var key = CacheKey(cardName);
        if (!_cache.TryGetValue(key, out var raw))
        {
            card = null;
            return false;
        }

        if (ReferenceEquals(raw, NegativeMarker))
        {
            _logger.LogDebug("CardLookupCache negative-hit for {CardName}", cardName);
            card = null;
            return true;
        }

        _logger.LogDebug("CardLookupCache positive-hit for {CardName}", cardName);
        card = raw as ScryfallCard;
        return true;
    }

    /// <summary>
    /// Caches a resolved <see cref="ScryfallCard"/> for 24 hours.
    /// </summary>
    /// <param name="cardName">The card name used for the lookup.</param>
    /// <param name="card">The resolved card object.</param>
    public void SetPositive(string cardName, ScryfallCard card)
    {
        var key = CacheKey(cardName);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            Size = 1,
        };
        _cache.Set(key, card, options);
    }

    /// <summary>
    /// Caches a negative result (card not found on Scryfall) for 1 hour.
    /// </summary>
    /// <param name="cardName">The card name that failed to resolve.</param>
    public void SetNegative(string cardName)
    {
        var key = CacheKey(cardName);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            Size = 1,
        };
        _cache.Set(key, NegativeMarker, options);
    }

    private static string CacheKey(string cardName)
        => "card:" + CardNameNormalizer.Normalize(cardName);
}
