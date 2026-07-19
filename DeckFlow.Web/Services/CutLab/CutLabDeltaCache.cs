using DeckFlow.Web.Models.CutLab;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>
/// Dedicated proposal-delta cache for repeated Cut Lab proposal renders.
/// </summary>
public sealed class CutLabDeltaCache
{
    private const int CacheCapacityBytes = 5_000_000;
    private const int KeyPrefixLength = 8;
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly ILogger<CutLabDeltaCache> _logger;

    private sealed record CachedEntry(CutLabProposalDeltas Deltas, int SizeBytes);

    /// <summary>
    /// Initializes a new <see cref="CutLabDeltaCache"/>.
    /// </summary>
    /// <param name="logger">Optional structured logger.</param>
    public CutLabDeltaCache(ILogger<CutLabDeltaCache>? logger = null)
        : this(CacheCapacityBytes, logger)
    {
    }

    internal CutLabDeltaCache(int sizeLimitBytes, ILogger<CutLabDeltaCache>? logger = null)
    {
        _logger = logger ?? NullLogger<CutLabDeltaCache>.Instance;
        // Why: the 512 MB render cap requires disposable proposal deltas to evict independently of every other cache.
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimitBytes });
    }

    /// <summary>
    /// Attempts to retrieve cached proposal deltas for a pool/card pair.
    /// </summary>
    /// <param name="poolKey">The deterministic working-pool hash.</param>
    /// <param name="cardName">The proposed card name.</param>
    /// <param name="deltas">The cached delta payload when present; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> on cache hit; otherwise <see langword="false"/>.</returns>
    public bool TryGet(string poolKey, string cardName, out CutLabProposalDeltas? deltas)
    {
        ArgumentNullException.ThrowIfNull(poolKey);
        ArgumentNullException.ThrowIfNull(cardName);

        string key = ComputeEntryKey(poolKey, cardName);
        if (!_cache.TryGetValue(key, out var raw) || raw is not CachedEntry entry)
        {
            deltas = null;
            LogCacheEvent("miss", key, 0);
            return false;
        }

        deltas = entry.Deltas;
        LogCacheEvent("hit", key, entry.SizeBytes);
        return true;
    }

    /// <summary>
    /// Stores proposal deltas for a pool/card pair.
    /// </summary>
    /// <param name="poolKey">The deterministic working-pool hash.</param>
    /// <param name="cardName">The proposed card name.</param>
    /// <param name="deltas">The delta payload to cache.</param>
    public void Set(string poolKey, string cardName, CutLabProposalDeltas deltas)
    {
        ArgumentNullException.ThrowIfNull(poolKey);
        ArgumentNullException.ThrowIfNull(cardName);
        ArgumentNullException.ThrowIfNull(deltas);

        string key = ComputeEntryKey(poolKey, cardName);
        int sizeBytes = EstimateSizeBytes(deltas);
        var entry = new CachedEntry(deltas, sizeBytes);
        var options = new MemoryCacheEntryOptions
        {
            // Why: delta payloads are disposable and only need to survive short-term re-renders of the same proposal.
            AbsoluteExpirationRelativeToNow = EntryTtl,
            Size = sizeBytes,
        };

        options.RegisterPostEvictionCallback((evictedKey, evictedValue, _, _) =>
        {
            var evictedSize = (evictedValue as CachedEntry)?.SizeBytes ?? 0;
            _logger.LogInformation(
                "Cut Lab delta cache {Outcome} for {KeyPrefix} ({SizeBytes} bytes)",
                "evicted",
                GetKeyPrefix(evictedKey as string ?? string.Empty),
                evictedSize);
        });

        _cache.Set(key, entry, options);
        LogCacheEvent("write", key, sizeBytes);
    }

    private static string ComputeEntryKey(string poolKey, string cardName)
        => PacketSessionCache.ComputeKey(new CacheKey(poolKey, cardName));

    private void LogCacheEvent(string outcome, string key, int sizeBytes)
    {
        _logger.LogInformation(
            "Cut Lab delta cache {Outcome} for {KeyPrefix} ({SizeBytes} bytes)",
            outcome,
            GetKeyPrefix(key),
            sizeBytes);
    }

    private static string GetKeyPrefix(string key)
        => key.Length <= KeyPrefixLength ? key : key[..KeyPrefixLength];

    private static int EstimateSizeBytes(CutLabProposalDeltas deltas)
    {
        int total = deltas.CardName.Length + deltas.ChangedFamilyCount;
        foreach (var delta in deltas.Deltas)
        {
            total += delta.Label.Length;
            total += 32;
        }

        return Math.Max(total, 1);
    }

    private sealed record CacheKey(string PoolKey, string CardName);
}
