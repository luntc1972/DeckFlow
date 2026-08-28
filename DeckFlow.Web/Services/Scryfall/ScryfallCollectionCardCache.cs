using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DeckFlow.Web.Services.FeatureFlags;

namespace DeckFlow.Web.Services.Scryfall;

/// <summary>
/// Process-local cache of individual <c>cards/collection</c> results.
/// </summary>
public sealed class ScryfallCollectionCardCache
{
    private const int CacheCapacityChars = 10_000_000;
    private const string FlagKey = "service.scryfall-collection-cache.enabled";
    internal const long StatisticsLogInterval = 1_000;
    private static readonly TimeSpan PositiveTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan CollectionMissTtl = TimeSpan.FromHours(1);
    private static readonly object CollectionMissMarker = new();
    private readonly IMemoryCache _cache;
    private readonly IFeatureFlagCache? _featureFlags;
    private readonly ILogger<ScryfallCollectionCardCache> _logger;
    private long _operations;
    private long _hits;
    private long _misses;
    private long _stores;
    private long _bypasses;

    private bool IsEnabled => _featureFlags?.IsEnabled(FlagKey) ?? true;

    /// <summary>
    /// Creates a bounded collection-result cache.
    /// </summary>
    public ScryfallCollectionCardCache(IFeatureFlagCache? featureFlags = null, ILogger<ScryfallCollectionCardCache>? logger = null)
        : this(CacheCapacityChars, featureFlags, logger)
    {
    }

    internal ScryfallCollectionCardCache(int capacityChars, IFeatureFlagCache? featureFlags = null, ILogger<ScryfallCollectionCardCache>? logger = null)
        : this(capacityChars, TimeProvider.System, featureFlags, logger)
    {
    }

    internal ScryfallCollectionCardCache(int capacityChars, TimeProvider timeProvider, IFeatureFlagCache? featureFlags = null, ILogger<ScryfallCollectionCardCache>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _featureFlags = featureFlags;
        _logger = logger ?? NullLogger<ScryfallCollectionCardCache>.Instance;
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            Clock = new TimeProviderSystemClock(timeProvider),
            SizeLimit = capacityChars,
        });
    }

    /// <summary>Attempts to read a name-identifier result; returns no result when the cache flag is off.</summary>
    public bool TryGetName(string identifier, out ScryfallCard? card)
    {
        if (!IsEnabledOrRecordBypass())
        {
            card = null;
            return false;
        }

        return TryGet(NameKey(identifier), out card);
    }

    /// <summary>Stores a successful name-identifier result when the cache flag is on.</summary>
    public void SetNamePositive(string identifier, ScryfallCard card)
    {
        if (!IsEnabledOrRecordBypass())
        {
            return;
        }

        SetPositive(NameKey(identifier), card);
    }

    /// <summary>Stores an explicitly returned name-identifier collection miss when the cache flag is on.</summary>
    public void SetNameCollectionMiss(string identifier)
    {
        if (!IsEnabledOrRecordBypass())
        {
            return;
        }

        SetCollectionMiss(NameKey(identifier));
    }

    /// <summary>Attempts to read a printing-identifier result; returns no result when the cache flag is off.</summary>
    public bool TryGetPrinting(string setCode, string collectorNumber, out ScryfallCard? card)
    {
        if (!IsEnabledOrRecordBypass())
        {
            card = null;
            return false;
        }

        return TryGet(PrintingKey(setCode, collectorNumber), out card);
    }

    /// <summary>Stores a successful printing-identifier result when the cache flag is on.</summary>
    public void SetPrintingPositive(string setCode, string collectorNumber, ScryfallCard card)
    {
        if (!IsEnabledOrRecordBypass())
        {
            return;
        }

        SetPositive(PrintingKey(setCode, collectorNumber), card);
    }

    /// <summary>Returns a thread-safe snapshot of cache activity and the current flag state.</summary>
    public ScryfallCollectionCacheStatistics GetStatistics() => new(
        IsEnabled,
        Interlocked.Read(ref _hits),
        Interlocked.Read(ref _misses),
        Interlocked.Read(ref _stores),
        Interlocked.Read(ref _bypasses));

    private bool IsEnabledOrRecordBypass()
    {
        if (IsEnabled)
        {
            return true;
        }

        Record(ref _bypasses);
        return false;
    }

    private void LogStatistics()
    {
        ScryfallCollectionCacheStatistics statistics = GetStatistics();
        _logger.LogInformation(
            "Scryfall collection cache statistics: enabled {Enabled}, hits {Hits}, misses {Misses}, stores {Stores}, bypasses {Bypasses}",
            statistics.Enabled, statistics.Hits, statistics.Misses, statistics.Stores, statistics.Bypasses);
    }

    private void Record(ref long counter)
    {
        Interlocked.Increment(ref counter);
        // Why: this long-lived singleton has no run boundary, so the snapshot is sampled rather
        // than emitted once; per-call logging is rejected because a single run resolves thousands
        // of cards. _operations is a separate counter, not the sum of the other four, because only
        // one atomic sequence guarantees exactly one caller observes each interval boundary.
        if (Interlocked.Increment(ref _operations) % StatisticsLogInterval == 0)
        {
            LogStatistics();
        }
    }

    private bool TryGet(string key, out ScryfallCard? card)
    {
        if (!_cache.TryGetValue(key, out var value))
        {
            Record(ref _misses);
            card = null;
            return false;
        }

        Record(ref _hits);
        card = value as ScryfallCard;
        return true;
    }

    private void SetPositive(string key, ScryfallCard card)
    {
        Record(ref _stores);
        _cache.Set(key, card, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = PositiveTtl,
            Size = EstimateSizeChars(card),
        });
    }

    private void SetCollectionMiss(string key)
    {
        Record(ref _stores);
        _cache.Set(key, CollectionMissMarker, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CollectionMissTtl,
            // Why: although the marker is shared, the cache retains this entry's key string.
            Size = Math.Max(key.Length, 1),
        });
    }

    private static string NameKey(string identifier) => $"name:{identifier}";

    private static string PrintingKey(string setCode, string collectorNumber) => $"printing:{setCode}:{collectorNumber}";

    private static int EstimateSizeChars(ScryfallCard card)
    {
        var total = card.Name.Length + card.TypeLine.Length;
        total += card.ManaCost?.Length ?? 0;
        total += card.OracleText?.Length ?? 0;
        total += card.Power?.Length ?? 0;
        total += card.Toughness?.Length ?? 0;
        total += card.SetCode?.Length ?? 0;
        total += card.SetName?.Length ?? 0;
        total += card.CollectorNumber?.Length ?? 0;
        total += card.Id?.Length ?? 0;
        total += card.Layout?.Length ?? 0;
        total += card.ReleasedAt?.Length ?? 0;
        total += card.ProducedMana?.Sum(value => value.Length) ?? 0;
        total += card.Rarity?.Length ?? 0;
        // Why: Cmc is numeric and contributes no characters to this char-denominated budget.
        total += card.Keywords?.Sum(value => value.Length) ?? 0;
        total += card.ColorIdentity?.Sum(value => value.Length) ?? 0;
        total += card.CardFaces?.Sum(face => (face.Name?.Length ?? 0) + (face.ManaCost?.Length ?? 0) + (face.TypeLine?.Length ?? 0) + (face.OracleText?.Length ?? 0) + (face.Power?.Length ?? 0) + (face.Toughness?.Length ?? 0)) ?? 0;
        return Math.Max(total, 1);
    }

    private sealed class TimeProviderSystemClock(TimeProvider timeProvider) : ISystemClock
    {
        public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    }
}
