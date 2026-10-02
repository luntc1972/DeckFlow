using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Services.CommanderCategoryNorms;

/// <summary>
/// Loads, bounds, and caches harvested commander category norms for analysis prompts.
/// </summary>
public sealed class CommanderCategoryNormsProvider : ICommanderCategoryNormsProvider
{
    // Why: D-12 gives the whole optional lookup a short request-time budget.
    private static readonly TimeSpan DefaultLookupTimeout = TimeSpan.FromMilliseconds(2500);
    // Why: Harvested norms change at harvest cadence, not per analysis request.
    private static readonly TimeSpan NormsCacheDuration = TimeSpan.FromMinutes(30);
    // Why: Database failures need suppression without delaying recovery for long.
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(3);
    // Why: Sixteen stripes bound concurrent cold lookups without one lock per key.
    private const int StripeCount = 16;
    // Why: Versioning prevents stale payloads after a future cache-shape change.
    private const string CacheKeyPrefix = "commander-category-norms:v1:";
    private static readonly SemaphoreSlim[] Stripes = Enumerable.Range(0, StripeCount).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private readonly ICommanderCategoryService _categoryService;
    private readonly ICategoryKnowledgeStore _knowledgeStore;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CommanderCategoryNormsProvider> _logger;
    private readonly TimeSpan _lookupTimeout;

    /// <summary>
    /// Initializes a provider with the production lookup timeout.
    /// </summary>
    public CommanderCategoryNormsProvider(
        ICommanderCategoryService categoryService,
        ICategoryKnowledgeStore knowledgeStore,
        IMemoryCache memoryCache,
        ILogger<CommanderCategoryNormsProvider> logger)
        : this(categoryService, knowledgeStore, memoryCache, logger, DefaultLookupTimeout)
    {
    }

    internal CommanderCategoryNormsProvider(
        ICommanderCategoryService categoryService,
        ICategoryKnowledgeStore knowledgeStore,
        IMemoryCache memoryCache,
        ILogger<CommanderCategoryNormsProvider> logger,
        TimeSpan lookupTimeout)
    {
        ArgumentNullException.ThrowIfNull(categoryService);
        ArgumentNullException.ThrowIfNull(knowledgeStore);
        ArgumentNullException.ThrowIfNull(memoryCache);
        ArgumentNullException.ThrowIfNull(logger);
        _categoryService = categoryService;
        _knowledgeStore = knowledgeStore;
        _memoryCache = memoryCache;
        _logger = logger;
        _lookupTimeout = lookupTimeout;
    }

    /// <summary>
    /// Returns harvested category norms for an accepted harvest key, or null when unavailable.
    /// </summary>
    public async Task<CommanderCategoryNormsResult?> GetNormsAsync(string harvestKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(harvestKey))
        {
            _logger.LogDebug("Commander category norms key rejected: length {KeyLength}", harvestKey?.Length ?? 0);
            return null;
        }

        var trimmed = harvestKey.Trim();
        if (trimmed.Length > CommanderCategoryNormsBlock.MaxHarvestKeyLength || harvestKey.Any(char.IsControl))
        {
            _logger.LogDebug("Commander category norms key rejected: length {KeyLength}", trimmed.Length);
            return null;
        }

        var cacheKey = CacheKeyFor(trimmed);
        if (_memoryCache.TryGetValue<CommanderCategoryNormsResult?>(cacheKey, out var cached))
        {
            return cached;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_lookupTimeout);
        var stripe = Stripes[StripeIndexFor(cacheKey)];
        try
        {
            await stripe.WaitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Commander category norms lookup for {HarvestKey} timed out waiting for a concurrent lookup", trimmed);
            return null;
        }

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            if (_memoryCache.TryGetValue<CommanderCategoryNormsResult?>(cacheKey, out cached))
            {
                return cached;
            }

            var deckCount = await _knowledgeStore.GetCommanderDeckCountAsync(trimmed, timeoutSource.Token);
            if (deckCount < CommanderCategoryNormsBlock.MinimumDeckCount)
            {
                Cache(cacheKey, null, NormsCacheDuration);
                _logger.LogDebug("Commander category norms skipped for {HarvestKey}: {DeckCount} harvested decks is below the floor", trimmed, deckCount);
                return null;
            }

            var lookup = await _categoryService.LookupAsync(trimmed, timeoutSource.Token, includeProcessedDeckCount: false);
            deckCount = lookup.CardDeckTotals.TotalDeckCount;
            if (deckCount < CommanderCategoryNormsBlock.MinimumDeckCount)
            {
                Cache(cacheKey, null, NormsCacheDuration);
                return null;
            }

            var result = new CommanderCategoryNormsResult(trimmed, deckCount, lookup.Summaries.Take(CommanderCategoryNormsBlock.MaxCategories).ToList());
            Cache(cacheKey, result, NormsCacheDuration);
            _logger.LogInformation(
                "Commander category norms loaded for {HarvestKey}: {DeckCount} decks, {CategoryCount} categories in {ElapsedMs} ms",
                trimmed,
                deckCount,
                result.Categories.Count,
                (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return result;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "Commander category norms lookup failed for {HarvestKey} after {ElapsedMs} ms; returning no norms",
                trimmed,
                (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            Cache(cacheKey, null, FailureCacheDuration);
            return null;
        }
        finally
        {
            stripe.Release();
        }
    }

    internal static string CacheKeyFor(string trimmedKey) => CacheKeyPrefix + trimmedKey.ToLowerInvariant();

    internal static int StripeIndexFor(string cacheKey) => (StringComparer.Ordinal.GetHashCode(cacheKey) & int.MaxValue) % StripeCount;

    private void Cache(string cacheKey, CommanderCategoryNormsResult? norms, TimeSpan duration)
    {
        // Why: Size is forward compatibility if the shared cache later gains a SizeLimit.
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duration,
            Size = 1,
        };
        _memoryCache.Set(cacheKey, norms, options);
    }
}
