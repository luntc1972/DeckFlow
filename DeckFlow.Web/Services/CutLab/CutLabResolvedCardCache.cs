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
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(30);

    private readonly IMemoryCache _cache;
    private readonly ILogger<CutLabResolvedCardCache> _logger;

    private sealed record CachedEntry(IReadOnlyList<ScryfallCardData> Cards, IReadOnlySet<string> MissingCardNames, int SizeBytes);

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
            .Select(entry => new PoolKeyEntry(CutLabCardNames.Normalize(entry.Name), entry.Quantity))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.Quantity)
            .ToArray();

        return PacketSessionCache.ComputeKey(normalized);
    }

    /// <summary>
    /// Seeds a derived working-list key from a resolved superset payload using every target card that is present.
    /// </summary>
    /// <param name="targetPool">The target working-list multiset to seed.</param>
    /// <param name="sourceCards">Resolved cards from a larger or equal pool.</param>
    /// <param name="seededCards">The filtered payload written under the target key.</param>
    /// <returns><see langword="true"/> when the target key was seeded.</returns>
    public bool TrySeedFromSuperset(
        IReadOnlyList<(string Name, int Quantity)> targetPool,
        IReadOnlyList<ScryfallCardData> sourceCards,
        out IReadOnlyList<ScryfallCardData>? seededCards)
    {
        ArgumentNullException.ThrowIfNull(targetPool);
        ArgumentNullException.ThrowIfNull(sourceCards);

        IReadOnlyDictionary<string, ScryfallCardData> sourceByName = CutLabCardNames.ToLastWinsDictionary(
            sourceCards,
            card => card.Name,
            card => card);
        List<ScryfallCardData> filteredCards = new(targetPool.Count);
        HashSet<string> seen = new(CutLabCardNames.Comparer);

        foreach ((string Name, _) in targetPool)
        {
            string normalizedName = CutLabCardNames.Normalize(Name);
            if (!seen.Add(normalizedName))
            {
                continue;
            }

            if (sourceByName.TryGetValue(normalizedName, out ScryfallCardData? card))
            {
                filteredCards.Add(card);
            }
        }

        seededCards = filteredCards;
        int distinctTargetCount = targetPool
            .Select(entry => CutLabCardNames.Normalize(entry.Name))
            .Distinct(CutLabCardNames.Comparer)
            .Count();
        if (filteredCards.Count != distinctTargetCount)
        {
            return false;
        }

        Set(ComputePoolKey(targetPool), filteredCards);
        return true;
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

    internal bool TryGetKnownMissingNames(string poolKey, out IReadOnlySet<string>? missingCardNames)
    {
        ArgumentNullException.ThrowIfNull(poolKey);

        if (!_cache.TryGetValue(poolKey, out var raw) || raw is not CachedEntry entry)
        {
            missingCardNames = null;
            return false;
        }

        missingCardNames = entry.MissingCardNames;
        return true;
    }

    /// <summary>
    /// Stores the resolved cards for a pool hash.
    /// </summary>
    /// <param name="poolKey">The deterministic working-pool hash.</param>
    /// <param name="cards">The resolved cards to cache.</param>
    /// <param name="missingCardNames">Optional normalized misses already attempted for this exact pool.</param>
    public void Set(
        string poolKey,
        IReadOnlyList<ScryfallCardData> cards,
        IReadOnlyCollection<string>? missingCardNames = null)
    {
        ArgumentNullException.ThrowIfNull(poolKey);
        ArgumentNullException.ThrowIfNull(cards);

        int sizeBytes = EstimateSizeBytes(cards);
        IReadOnlySet<string> normalizedMissingCardNames = missingCardNames is null
            ? new HashSet<string>(CutLabCardNames.Comparer)
            : missingCardNames
                .Select(CutLabCardNames.Normalize)
                .ToHashSet(CutLabCardNames.Comparer);
        var entry = new CachedEntry(cards, normalizedMissingCardNames, sizeBytes);
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
                PacketSessionCache.GetKeyPrefix(evictedKey as string ?? string.Empty),
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
            PacketSessionCache.GetKeyPrefix(key),
            sizeBytes);
    }

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
