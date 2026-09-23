using System.Collections.Concurrent;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge.CardGrounding;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>
/// Carries the accepted whitelist names plus the upstream-failure diagnostic from the validation batch.
/// </summary>
public sealed record CreatorWhitelistPoolBuildResult
{
    /// <summary>
    /// Gets the accepted canonical whitelist names in ranked order.
    /// </summary>
    public required IReadOnlyList<string> AcceptedNames { get; init; }

    /// <summary>
    /// Gets a value indicating whether the grounding batch experienced any upstream failures.
    /// </summary>
    public required bool HasUpstreamFailure { get; init; }
}

/// <summary>
/// Builds a constrained creator-whitelist candidate pool from the creator's cached deck corpus.
/// </summary>
public sealed class CreatorWhitelistPoolBuilder
{
    private const string CacheKeyPrefix = "creator-whitelist-pool:";
    private static readonly TimeSpan RawPoolCacheTtl = TimeSpan.FromHours(1);
    private const int WhitelistCap = 25; // Why: 25 keeps the whitelist materially useful while bounding grounding latency and packet size even when lift-metric count changes independently.

    private readonly ICreatorDeckCacheStore _creatorDeckCacheStore;
    private readonly ICreatorSuppressionStore? _suppressionStore;
    private readonly ICreatorSuppressionGate? _suppressionGate;
    private readonly ICardGroundingGuard _cardGroundingGuard;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CreatorWhitelistPoolBuilder> _logger;
    private readonly ConcurrentDictionary<string, byte> _cacheKeys = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a creator-whitelist pool builder.
    /// </summary>
    /// <param name="creatorDeckCacheStore">Creator deck corpus store.</param>
    /// <param name="cardGroundingGuard">Strict guard used to validate ranked candidates.</param>
    /// <param name="cache">In-memory cache for raw per-creator candidate pools.</param>
    /// <param name="logger">Optional logger.</param>
    public CreatorWhitelistPoolBuilder(
        ICreatorDeckCacheStore creatorDeckCacheStore,
        ICardGroundingGuard cardGroundingGuard,
        IMemoryCache cache,
        ILogger<CreatorWhitelistPoolBuilder>? logger = null)
        : this(creatorDeckCacheStore, cardGroundingGuard, cache, suppressionStore: null, logger, isInternal: true)
    {
    }

    /// <summary>Creates a creator-whitelist pool builder that honours identity-aware creator suppression.</summary>
    /// <param name="creatorDeckCacheStore">Creator deck corpus store.</param>
    /// <param name="cardGroundingGuard">Strict guard used to validate ranked candidates.</param>
    /// <param name="cache">In-memory cache for raw per-creator candidate pools.</param>
    /// <param name="suppressionStore">Suppression store whose revision keys the cached pools.</param>
    /// <param name="suppressionGate">Identity-aware gate that decides whether a creator is suppressed.</param>
    /// <param name="logger">Optional logger.</param>
    public CreatorWhitelistPoolBuilder(
        ICreatorDeckCacheStore creatorDeckCacheStore,
        ICardGroundingGuard cardGroundingGuard,
        IMemoryCache cache,
        ICreatorSuppressionStore suppressionStore,
        ICreatorSuppressionGate suppressionGate,
        ILogger<CreatorWhitelistPoolBuilder>? logger = null)
        : this(creatorDeckCacheStore, cardGroundingGuard, cache, suppressionStore, logger, isInternal: true)
    {
        _suppressionGate = suppressionGate ?? throw new ArgumentNullException(nameof(suppressionGate));
    }

    /// <summary>Creates a creator-whitelist pool builder that honours creator suppression.</summary>
    public CreatorWhitelistPoolBuilder(
        ICreatorDeckCacheStore creatorDeckCacheStore,
        ICardGroundingGuard cardGroundingGuard,
        IMemoryCache cache,
        ICreatorSuppressionStore suppressionStore,
        ILogger<CreatorWhitelistPoolBuilder>? logger = null)
        : this(creatorDeckCacheStore, cardGroundingGuard, cache, suppressionStore, logger, isInternal: true)
    {
    }

    private CreatorWhitelistPoolBuilder(
        ICreatorDeckCacheStore creatorDeckCacheStore,
        ICardGroundingGuard cardGroundingGuard,
        IMemoryCache cache,
        ICreatorSuppressionStore? suppressionStore,
        ILogger<CreatorWhitelistPoolBuilder>? logger,
        bool isInternal)
    {
        ArgumentNullException.ThrowIfNull(creatorDeckCacheStore);
        ArgumentNullException.ThrowIfNull(cardGroundingGuard);
        ArgumentNullException.ThrowIfNull(cache);

        _creatorDeckCacheStore = creatorDeckCacheStore;
        _suppressionStore = suppressionStore;
        _cardGroundingGuard = cardGroundingGuard;
        _cache = cache;
        _logger = logger ?? NullLogger<CreatorWhitelistPoolBuilder>.Instance;
    }

    /// <summary>Removes this process's cached raw pool for a creator.</summary>
    public void Invalidate(string creatorSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creatorSlug);
        var cacheKeyPrefix = CacheKeyPrefix + creatorSlug.Trim().ToLowerInvariant() + ":";
        foreach (string cacheKey in _cacheKeys.Keys.Where(cacheKey => cacheKey.StartsWith(cacheKeyPrefix, StringComparison.Ordinal)))
        {
            _cache.Remove(cacheKey);
            _cacheKeys.TryRemove(cacheKey, out _);
        }
    }

    /// <summary>
    /// Builds a guard-validated creator whitelist and returns the upstream-failure diagnostic from the validation batch.
    /// </summary>
    /// <param name="creatorSlug">Creator slug.</param>
    /// <param name="deckContext">Deck-context inputs used by the guard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Accepted canonical card names plus upstream-failure diagnostics.</returns>
    public async Task<CreatorWhitelistPoolBuildResult> BuildWithDiagnosticsAsync(
        string creatorSlug,
        CardGroundingDeckContext deckContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creatorSlug);
        ArgumentNullException.ThrowIfNull(deckContext);

        var rawPool = await GetOrBuildRawPoolAsync(creatorSlug, cancellationToken).ConfigureAwait(false);
        if (rawPool.Count == 0)
        {
            return new CreatorWhitelistPoolBuildResult
            {
                AcceptedNames = [],
                HasUpstreamFailure = false,
            };
        }

        CardGroundingBatchResult validation = await _cardGroundingGuard
            .ValidateAllAsync(rawPool, deckContext, cancellationToken)
            .ConfigureAwait(false);

        if (validation.HasUpstreamFailure)
        {
            _logger.LogWarning(
                "Creator whitelist validation saw upstream failures for creator {CreatorSlug}; returning accepted subset only.",
                creatorSlug);
        }

        return new CreatorWhitelistPoolBuildResult
        {
            AcceptedNames = validation.Verdicts
                .Where(verdict => verdict.Accepted)
                .Select(verdict => verdict.CanonicalName)
                .ToArray(),
            HasUpstreamFailure = validation.HasUpstreamFailure,
        };
    }

    private async Task<IReadOnlyList<string>> GetOrBuildRawPoolAsync(string creatorSlug, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_suppressionGate is not null
            && await _suppressionGate.IsSuppressedAsync(creatorSlug, cancellationToken).ConfigureAwait(false))
        {
            return Array.Empty<string>();
        }

        if (_suppressionGate is null && _suppressionStore is not null
            && await _suppressionStore.IsSuppressedAsync(creatorSlug, cancellationToken).ConfigureAwait(false))
        {
            return Array.Empty<string>();
        }

        long revision = _suppressionStore is null
            ? 0
            : await _suppressionStore.GetRevisionAsync(cancellationToken).ConfigureAwait(false);
        var cacheKey = BuildCacheKey(creatorSlug, revision);
        _cacheKeys.TryAdd(cacheKey, 0);

        // Why (WR-15): the factory below runs under CancellationToken.None, not the calling
        // request's token. IMemoryCache.GetOrCreateAsync gives no stampede protection -
        // concurrent callers for the same key each run their own factory - so if the request
        // that happens to populate the cache is the one that gets cancelled, that must not fault
        // (or leave uncached) an entry that other, still-live callers for the same creator are
        // relying on.
        IReadOnlyList<string>? cached = await _cache.GetOrCreateAsync(
            cacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = RawPoolCacheTtl;
                return await BuildRawPoolAsync(creatorSlug, CancellationToken.None).ConfigureAwait(false);
            }).ConfigureAwait(false);

        return cached ?? Array.Empty<string>();
    }

    private async Task<IReadOnlyList<string>> BuildRawPoolAsync(string creatorSlug, CancellationToken cancellationToken)
    {
        var cachedDecks = await _creatorDeckCacheStore
            .GetByCreatorAsync(creatorSlug, cancellationToken)
            .ConfigureAwait(false);

        var frequencyByName = new Dictionary<string, RankedCandidate>(StringComparer.Ordinal);
        foreach (CreatorDeckCacheEntry cachedDeck in cachedDecks)
        {
            foreach (var candidate in DistinctMainboardCandidates(cachedDeck.Entries))
            {
                if (frequencyByName.TryGetValue(candidate.NormalizedName, out RankedCandidate? existing))
                {
                    frequencyByName[candidate.NormalizedName] = existing with
                    {
                        DisplayName = SelectPreferredDisplayName(existing.DisplayName, candidate.DisplayName),
                        DistinctDeckCount = existing.DistinctDeckCount + 1,
                    };
                }
                else
                {
                    frequencyByName[candidate.NormalizedName] = new RankedCandidate
                    {
                        NormalizedName = candidate.NormalizedName,
                        DisplayName = candidate.DisplayName,
                        DistinctDeckCount = 1,
                    };
                }
            }
        }

        return frequencyByName.Values
            .OrderByDescending(candidate => candidate.DistinctDeckCount)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
            .Take(WhitelistCap)
            .Select(candidate => candidate.DisplayName)
            .ToArray();
    }

    private static IEnumerable<RawCandidate> DistinctMainboardCandidates(IReadOnlyList<DeckEntry> entries)
    {
        var distinctCandidates = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (DeckEntry entry in entries)
        {
            if (!string.Equals(entry.Board, "mainboard", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var normalizedName = string.IsNullOrWhiteSpace(entry.NormalizedName)
                ? CardNormalizer.Normalize(entry.Name)
                : entry.NormalizedName.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                continue;
            }

            var displayName = entry.Name.Trim();
            if (distinctCandidates.TryGetValue(normalizedName, out string? existingDisplayName))
            {
                distinctCandidates[normalizedName] = SelectPreferredDisplayName(existingDisplayName, displayName);
            }
            else
            {
                distinctCandidates[normalizedName] = displayName;
            }
        }

        return distinctCandidates.Select(pair => new RawCandidate
        {
            NormalizedName = pair.Key,
            DisplayName = pair.Value,
        });
    }

    private static string SelectPreferredDisplayName(string left, string right)
        => string.Compare(left, right, StringComparison.Ordinal) <= 0 ? left : right;

    private static string BuildCacheKey(string creatorSlug, long suppressionRevision)
        => CacheKeyPrefix + creatorSlug.Trim().ToLowerInvariant() + ":" + suppressionRevision;

    private sealed record RawCandidate
    {
        public required string NormalizedName { get; init; }

        public required string DisplayName { get; init; }
    }

    private sealed record RankedCandidate
    {
        public required string NormalizedName { get; init; }

        public required string DisplayName { get; init; }

        public required int DistinctDeckCount { get; init; }
    }
}
