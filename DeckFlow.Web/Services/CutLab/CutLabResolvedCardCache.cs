using DeckFlow.Core.Manabase;
using DeckFlow.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>
/// Dedicated resolved-card cache for the Cut Lab working pool.
/// </summary>
public sealed class CutLabResolvedCardCache
{
    private const int CacheCapacityBytes = 20_000_000;
    private const int KeyPrefixLength = 8;
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(30);

    private readonly IMemoryCache _cache;
    private readonly ILogger<CutLabResolvedCardCache> _logger;

    private sealed record CachedEntry(IReadOnlyList<ScryfallCardData> Cards, int SizeBytes);

    /// <summary>
    /// Initializes a new <see cref="CutLabResolvedCardCache"/>.
    /// </summary>
    /// <param name="logger">Optional structured logger.</param>
    public CutLabResolvedCardCache(ILogger<CutLabResolvedCardCache>? logger = null)
        : this(CacheCapacityBytes, logger)
    {
    }

    internal CutLabResolvedCardCache(int sizeLimitBytes, ILogger<CutLabResolvedCardCache>? logger = null)
    {
        _logger = logger ?? NullLogger<CutLabResolvedCardCache>.Instance;
        // Why: the 512 MB render cap requires Cut Lab cache pressure to stay isolated from the shared app cache.
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimitBytes });
    }

    /// <summary>
    /// Computes the canonical pool key for a multiset of card names and quantities.
    /// </summary>
    /// <param name="pool">The working-pool name and quantity pairs.</param>
    /// <returns>A lowercase SHA-256 pool hash.</returns>
    public static string ComputePoolKey(IReadOnlyList<(string Name, int Quantity)> pool)
    {
        ArgumentNullException.ThrowIfNull(pool);

        var normalized = pool
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Quantity)
            .Select(entry => new PoolKeyEntry(entry.Name, entry.Quantity))
            .ToArray();

        return PacketSessionCache.ComputeKey(normalized);
    }

    /// <summary>
    /// Attempts to retrieve the resolved cards for a pool hash.
    /// </summary>
    /// <param name="poolKey">The deterministic working-pool hash.</param>
    /// <param name="cards">The cached resolved cards when present; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> on cache hit; otherwise <see langword="false"/>.</returns>
    public bool TryGet(string poolKey, out IReadOnlyList<ScryfallCardData>? cards)
    {
        ArgumentNullException.ThrowIfNull(poolKey);

        if (!_cache.TryGetValue(poolKey, out var raw) || raw is not CachedEntry entry)
        {
            cards = null;
            LogCacheEvent("miss", poolKey, 0);
            return false;
        }

        cards = entry.Cards;
        LogCacheEvent("hit", poolKey, entry.SizeBytes);
        return true;
    }

    /// <summary>
    /// Stores the resolved cards for a pool hash.
    /// </summary>
    /// <param name="poolKey">The deterministic working-pool hash.</param>
    /// <param name="cards">The resolved cards to cache.</param>
    public void Set(string poolKey, IReadOnlyList<ScryfallCardData> cards)
    {
        ArgumentNullException.ThrowIfNull(poolKey);
        ArgumentNullException.ThrowIfNull(cards);

        int sizeBytes = EstimateSizeBytes(cards);
        var entry = new CachedEntry(cards, sizeBytes);
        var options = new MemoryCacheEntryOptions
        {
            // Why: resolved Scryfall payloads should survive a normal cut session without repeated refetches.
            AbsoluteExpirationRelativeToNow = EntryTtl,
            Size = sizeBytes,
        };

        options.RegisterPostEvictionCallback((evictedKey, evictedValue, _, _) =>
        {
            var evictedSize = (evictedValue as CachedEntry)?.SizeBytes ?? 0;
            _logger.LogInformation(
                "Cut Lab resolved-card cache {Outcome} for {KeyPrefix} ({SizeBytes} bytes)",
                "evicted",
                GetKeyPrefix(evictedKey as string ?? string.Empty),
                evictedSize);
        });

        _cache.Set(poolKey, entry, options);
        LogCacheEvent("write", poolKey, sizeBytes);
    }

    private void LogCacheEvent(string outcome, string key, int sizeBytes)
    {
        _logger.LogInformation(
            "Cut Lab resolved-card cache {Outcome} for {KeyPrefix} ({SizeBytes} bytes)",
            outcome,
            GetKeyPrefix(key),
            sizeBytes);
    }

    private static string GetKeyPrefix(string key)
        => key.Length <= KeyPrefixLength ? key : key[..KeyPrefixLength];

    private static int EstimateSizeBytes(IReadOnlyList<ScryfallCardData> cards)
    {
        int total = 0;
        foreach (var card in cards)
        {
            total += card.Name.Length;
            total += card.ManaCost?.Length ?? 0;
            total += card.TypeLine?.Length ?? 0;
            total += card.OracleText?.Length ?? 0;
            total += card.Rarity?.Length ?? 0;
            total += card.Set?.Length ?? 0;
            total += card.CollectorNumber?.Length ?? 0;
            total += card.Layout?.Length ?? 0;
            total += card.Power?.Length ?? 0;
            total += (card.ProducedMana?.Sum(color => color.Length) ?? 0);

            if (card.CardFaces is not null)
            {
                foreach (var face in card.CardFaces)
                {
                    total += face.Name?.Length ?? 0;
                    total += face.ManaCost?.Length ?? 0;
                    total += face.TypeLine?.Length ?? 0;
                    total += face.OracleText?.Length ?? 0;
                    total += face.Power?.Length ?? 0;
                }
            }
        }

        return Math.Max(total, 1);
    }

    private sealed record PoolKeyEntry(string Name, int Quantity);
}
