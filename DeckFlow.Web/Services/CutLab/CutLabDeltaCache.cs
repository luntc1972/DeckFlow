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
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    private readonly ILogger<CutLabDeltaCache> _logger;

    private sealed record CachedEntry<TValue>(TValue Value, int SizeBytes) where TValue : class;

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
    /// <param name="trialsOverride">The simulation trial override that produced the cached deltas.</param>
    /// <returns><see langword="true"/> on cache hit; otherwise <see langword="false"/>.</returns>
    public bool TryGet(string poolKey, string cardName, out CutLabProposalDeltas? deltas, int? trialsOverride = null)
    {
        ArgumentNullException.ThrowIfNull(poolKey);
        ArgumentNullException.ThrowIfNull(cardName);

        string key = ComputeDeltaEntryKey(poolKey, cardName, trialsOverride);
        if (!_cache.TryGetValue(key, out var raw) || raw is not CachedEntry<CutLabProposalDeltas> entry)
        {
            deltas = null;
            LogCacheEvent("miss", key, 0);
            return false;
        }

        deltas = entry.Value;
        LogCacheEvent("hit", key, entry.SizeBytes);
        return true;
    }

    /// <summary>
    /// Stores proposal deltas for a pool/card pair.
    /// </summary>
    /// <param name="poolKey">The deterministic working-pool hash.</param>
    /// <param name="cardName">The proposed card name.</param>
    /// <param name="deltas">The delta payload to cache.</param>
    /// <param name="trialsOverride">The simulation trial override that produced the delta payload.</param>
    public void Set(string poolKey, string cardName, CutLabProposalDeltas deltas, int? trialsOverride = null)
    {
        ArgumentNullException.ThrowIfNull(poolKey);
        ArgumentNullException.ThrowIfNull(cardName);
        ArgumentNullException.ThrowIfNull(deltas);

        string key = ComputeDeltaEntryKey(poolKey, cardName, trialsOverride);
        int sizeBytes = EstimateSizeBytes(deltas);
        var entry = new CachedEntry<CutLabProposalDeltas>(deltas, sizeBytes);
        var options = new MemoryCacheEntryOptions
        {
            // Why: delta payloads are disposable and only need to survive short-term re-renders of the same proposal.
            AbsoluteExpirationRelativeToNow = EntryTtl,
            Size = sizeBytes,
        };
        RegisterEvictionLogging<CutLabProposalDeltas>(options);

        _cache.Set(key, entry, options);
        LogCacheEvent("write", key, sizeBytes);
    }

    /// <summary>
    /// Attempts to retrieve a cached Cut Lab metric snapshot for a pool/trials pair.
    /// </summary>
    /// <param name="poolKey">The deterministic working-pool hash.</param>
    /// <param name="playExperience">The Cut Lab play-experience label.</param>
    /// <param name="trialsOverride">The simulation trial override that produced the snapshot.</param>
    /// <param name="snapshot">The cached snapshot when present; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> on cache hit; otherwise <see langword="false"/>.</returns>
    public bool TryGetSnapshot(string poolKey, string? playExperience, int? trialsOverride, out CutLabMetricSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(poolKey);

        string key = ComputeSnapshotEntryKey(poolKey, playExperience, trialsOverride);
        if (!_cache.TryGetValue(key, out var raw) || raw is not CachedEntry<CutLabMetricSnapshot> entry)
        {
            snapshot = null;
            LogCacheEvent("miss", key, 0);
            return false;
        }

        snapshot = entry.Value;
        LogCacheEvent("hit", key, entry.SizeBytes);
        return true;
    }

    /// <summary>
    /// Stores a Cut Lab metric snapshot for a pool/trials pair.
    /// </summary>
    /// <param name="poolKey">The deterministic working-pool hash.</param>
    /// <param name="playExperience">The Cut Lab play-experience label.</param>
    /// <param name="trialsOverride">The simulation trial override that produced the snapshot.</param>
    /// <param name="snapshot">The snapshot to cache.</param>
    public void SetSnapshot(string poolKey, string? playExperience, int? trialsOverride, CutLabMetricSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(poolKey);
        ArgumentNullException.ThrowIfNull(snapshot);

        string key = ComputeSnapshotEntryKey(poolKey, playExperience, trialsOverride);
        int sizeBytes = EstimateSizeBytes(snapshot);
        var entry = new CachedEntry<CutLabMetricSnapshot>(snapshot, sizeBytes);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = EntryTtl,
            Size = sizeBytes,
        };
        RegisterEvictionLogging<CutLabMetricSnapshot>(options);

        _cache.Set(key, entry, options);
        LogCacheEvent("write", key, sizeBytes);
    }

    private static string ComputeDeltaEntryKey(string poolKey, string cardName, int? trialsOverride)
        => PacketSessionCache.ComputeKey(new DeltaCacheKey(poolKey, NormalizeCardName(cardName), trialsOverride));

    private static string ComputeSnapshotEntryKey(string poolKey, string? playExperience, int? trialsOverride)
        => PacketSessionCache.ComputeKey(new SnapshotCacheKey(poolKey, NormalizePlayExperience(playExperience), trialsOverride));

    private static string NormalizeCardName(string cardName)
        => cardName.ToUpperInvariant();

    private static string NormalizePlayExperience(string? playExperience)
        => string.IsNullOrWhiteSpace(playExperience) ? string.Empty : playExperience.Trim().ToUpperInvariant();

    private void LogCacheEvent(string outcome, string key, int sizeBytes)
    {
        _logger.LogInformation(
            "Cut Lab delta cache {Outcome} for {KeyPrefix} ({SizeBytes} bytes)",
            outcome,
            PacketSessionCache.GetKeyPrefix(key),
            sizeBytes);
    }

    private void RegisterEvictionLogging<TValue>(MemoryCacheEntryOptions options) where TValue : class
    {
        options.RegisterPostEvictionCallback((evictedKey, evictedValue, _, _) =>
        {
            int evictedSize = (evictedValue as CachedEntry<TValue>)?.SizeBytes ?? 0;
            LogCacheEvent("evicted", evictedKey as string ?? string.Empty, evictedSize);
        });
    }

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

    private static int EstimateSizeBytes(CutLabMetricSnapshot snapshot)
    {
        int total = 0;
        foreach (CutLabMetricValue metric in snapshot.Metrics)
        {
            total += metric.Label.Length;
            total += 32;
        }

        return Math.Max(total, 1);
    }

    private sealed record DeltaCacheKey(string PoolKey, string CardName, int? TrialsOverride);
    private sealed record SnapshotCacheKey(string PoolKey, string PlayExperience, int? TrialsOverride);
}
