using System.Security.Cryptography;
using System.Text;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>
/// Crawls creator-scoped Archidekt or Moxfield decks with a creator-level warm-cache short-circuit.
/// </summary>
public sealed class CreatorProfileDeckCrawler
{
    private const string ConfidenceMarker = "ok";
    // Why: a tarpit must not indefinitely hold the process-wide Moxfield request gate.
    private static readonly TimeSpan MoxfieldImportTimeout = TimeSpan.FromSeconds(60);

    private readonly IArchidektOwnerClient _archidektOwnerClient;
    private readonly IArchidektDeckImporter _archidektDeckImporter;
    private readonly IMoxfieldOwnerClient _moxfieldOwnerClient;
    private readonly IMoxfieldDeckImporter _moxfieldDeckImporter;
    private readonly MoxfieldRequestPacer _moxfieldPacer;
    private readonly ICreatorProfileSourceStore _profileSourceStore;
    private readonly ICreatorDeckCacheStore _deckCacheStore;
    private readonly ILogger<CreatorProfileDeckCrawler> _logger;
    private readonly TimeSpan _freshnessWindow;
    private readonly Func<DateTimeOffset> _nowUtc;
    private readonly TimeSpan _moxfieldImportTimeout;

    /// <summary>
    /// Creates a creator-profile deck crawler.
    /// </summary>
    public CreatorProfileDeckCrawler(
        IArchidektOwnerClient archidektOwnerClient,
        IArchidektDeckImporter archidektDeckImporter,
        IMoxfieldOwnerClient moxfieldOwnerClient,
        IMoxfieldDeckImporter moxfieldDeckImporter,
        MoxfieldRequestPacer moxfieldPacer,
        ICreatorProfileSourceStore profileSourceStore,
        ICreatorDeckCacheStore deckCacheStore,
        ILogger<CreatorProfileDeckCrawler>? logger = null,
        TimeSpan? freshnessWindow = null,
        Func<DateTimeOffset>? nowUtc = null)
        : this(
            archidektOwnerClient,
            archidektDeckImporter,
            moxfieldOwnerClient,
            moxfieldDeckImporter,
            moxfieldPacer,
            profileSourceStore,
            deckCacheStore,
            logger,
            freshnessWindow,
            nowUtc,
            MoxfieldImportTimeout)
    {
    }

    internal CreatorProfileDeckCrawler(
        IArchidektOwnerClient archidektOwnerClient,
        IArchidektDeckImporter archidektDeckImporter,
        IMoxfieldOwnerClient moxfieldOwnerClient,
        IMoxfieldDeckImporter moxfieldDeckImporter,
        MoxfieldRequestPacer moxfieldPacer,
        ICreatorProfileSourceStore profileSourceStore,
        ICreatorDeckCacheStore deckCacheStore,
        ILogger<CreatorProfileDeckCrawler>? logger,
        TimeSpan? freshnessWindow,
        Func<DateTimeOffset>? nowUtc,
        TimeSpan moxfieldImportTimeout)
    {
        ArgumentNullException.ThrowIfNull(archidektOwnerClient);
        ArgumentNullException.ThrowIfNull(archidektDeckImporter);
        ArgumentNullException.ThrowIfNull(moxfieldOwnerClient);
        ArgumentNullException.ThrowIfNull(moxfieldDeckImporter);
        ArgumentNullException.ThrowIfNull(moxfieldPacer);
        ArgumentNullException.ThrowIfNull(profileSourceStore);
        ArgumentNullException.ThrowIfNull(deckCacheStore);
        _archidektOwnerClient = archidektOwnerClient;
        _archidektDeckImporter = archidektDeckImporter;
        _moxfieldOwnerClient = moxfieldOwnerClient;
        _moxfieldDeckImporter = moxfieldDeckImporter;
        _moxfieldPacer = moxfieldPacer;
        _profileSourceStore = profileSourceStore;
        _deckCacheStore = deckCacheStore;
        _logger = logger ?? NullLogger<CreatorProfileDeckCrawler>.Instance;
        _freshnessWindow = freshnessWindow ?? TimeSpan.FromHours(24);
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
        _moxfieldImportTimeout = moxfieldImportTimeout;
    }

    /// <summary>
    /// Crawls the creator's public Archidekt or Moxfield decks or serves them from the creator cache.
    /// </summary>
    /// <param name="creatorSlug">Creator slug.</param>
    /// <param name="forceRefresh">When <see langword="true"/>, bypasses the creator-level freshness short-circuit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The creator deck samples.</returns>
    public async Task<IReadOnlyList<CreatorDeckSample>> CrawlAsync(string creatorSlug, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creatorSlug);

        var source = await _profileSourceStore.GetBySlugAsync(creatorSlug, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return Array.Empty<CreatorDeckSample>();
        }

        var platform = source.Platform.Trim();
        if (!string.Equals(platform, "archidekt", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(platform, "moxfield", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Creator {CreatorSlug} has unsupported platform {Platform}; skipping crawl.", creatorSlug, source.Platform);
            return Array.Empty<CreatorDeckSample>();
        }

        if (!forceRefresh
            && source.LastCrawledUtc is not null
            && (_nowUtc() - source.LastCrawledUtc.Value) < _freshnessWindow)
        {
            _logger.LogDebug("Serving creator {CreatorSlug} entirely from warm cache.", creatorSlug);
            var warmEntries = await _deckCacheStore.GetByCreatorAsync(creatorSlug, cancellationToken).ConfigureAwait(false);
            return RebuildSamplesFromCache(warmEntries, source);
        }

        return string.Equals(platform, "moxfield", StringComparison.OrdinalIgnoreCase)
            ? await CrawlMoxfieldAsync(creatorSlug, source, forceRefresh, cancellationToken).ConfigureAwait(false)
            : await CrawlArchidektAsync(creatorSlug, source, forceRefresh, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<CreatorDeckSample>> CrawlArchidektAsync(string creatorSlug, CreatorProfileSource source, bool forceRefresh, CancellationToken cancellationToken)
    {
        var resolvedUsername = await _archidektOwnerClient.ResolveUsernameAsync(source.ProfileUsername, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(resolvedUsername))
        {
            var fallbackInput = source.ProfileUrl ?? source.ProfileUsername;
            if (!ArchidektOwnerUrl.TryGetUsername(fallbackInput, out resolvedUsername))
            {
                return Array.Empty<CreatorDeckSample>();
            }
        }

        var listing = await _archidektOwnerClient.ListDeckSummariesAsync(resolvedUsername, cancellationToken).ConfigureAwait(false);
        var cacheEntries = await _deckCacheStore.GetByCreatorAsync(creatorSlug, cancellationToken).ConfigureAwait(false);
        if (listing.HasUpstreamFailure)
        {
            _logger.LogWarning("Archidekt enumeration failed for {CreatorSlug}; leaving freshness stamp untouched.", creatorSlug);
            return RebuildSamplesFromCache(cacheEntries, source);
        }

        var cacheByDeckId = cacheEntries.ToDictionary(entry => entry.DeckId, StringComparer.Ordinal);
        var samples = new List<CreatorDeckSample>();

        foreach (var summary in listing.Decks)
        {
            if (summary.Size > StapleStripper.MaxDeckSize)
            {
                continue;
            }

            if (!forceRefresh
                && cacheByDeckId.TryGetValue(summary.Id, out var cachedEntry)
                && !string.IsNullOrWhiteSpace(cachedEntry.ContentHash))
            {
                samples.Add(RebuildSampleFromCache(cachedEntry, source));
                continue;
            }

            var importedEntries = await _archidektDeckImporter.ImportAsync(summary.Id, cancellationToken).ConfigureAwait(false);
            // Why: StapleStripper.FilterOversized sums every board downstream, so cache only the main deck.
            var entries = CreatorMainDeck.GetEntries(importedEntries);
            var size = entries.Sum(entry => entry.Quantity);
            if (size > StapleStripper.MaxDeckSize)
            {
                // Why: warm reads return every cache row, so oversized decks must never be cached.
                _logger.LogDebug("Skipping oversized Archidekt deck {DeckId} for {CreatorSlug} with {CardCount} cards.", summary.Id, creatorSlug, size);
                continue;
            }

            var contentHash = ComputeCanonicalHash(entries);
            cacheByDeckId.TryGetValue(summary.Id, out var cachedEntryForHashCompare);
            if (cachedEntryForHashCompare is not null && string.Equals(cachedEntryForHashCompare.ContentHash, contentHash, StringComparison.Ordinal))
            {
                samples.Add(RebuildSampleFromCache(cachedEntryForHashCompare, source));
                continue;
            }

            var cacheEntry = new CreatorDeckCacheEntry
            {
                CreatorSlug = creatorSlug,
                DeckId = summary.Id,
                ContentHash = contentHash,
                FolderId = summary.ParentFolderId,
                FolderName = summary.ParentFolderName,
                Size = size,
                ConfidenceMarker = ConfidenceMarker,
                Entries = entries,
                CachedUtc = _nowUtc()
            };

            await _deckCacheStore.UpsertAsync(cacheEntry, cancellationToken).ConfigureAwait(false);
            samples.Add(RebuildSampleFromCache(cacheEntry, source));
        }

        await _profileSourceStore.SetLastCrawledAsync(creatorSlug, _nowUtc(), cancellationToken).ConfigureAwait(false);
        return samples;
    }

    private async Task<IReadOnlyList<CreatorDeckSample>> CrawlMoxfieldAsync(string creatorSlug, CreatorProfileSource source, bool forceRefresh, CancellationToken cancellationToken)
    {
        var listing = await _moxfieldOwnerClient.ListDeckSummariesAsync(source.ProfileUsername, cancellationToken).ConfigureAwait(false);
        var cacheEntries = await _deckCacheStore.GetByCreatorAsync(creatorSlug, cancellationToken).ConfigureAwait(false);
        if (listing.HasUpstreamFailure)
        {
            _logger.LogWarning("Moxfield enumeration failed for {CreatorSlug}; leaving freshness stamp untouched.", creatorSlug);
            return RebuildSamplesFromCache(cacheEntries, source);
        }

        var cacheByDeckId = cacheEntries.ToDictionary(entry => entry.DeckId, StringComparer.Ordinal);
        var samples = new List<CreatorDeckSample>();
        foreach (var summary in listing.Decks)
        {
            // Why: Mirror the Archidekt size check to avoid a paced request and let a complete listing prune stale rows.
            if (summary.MainboardCount > StapleStripper.MaxDeckSize)
            {
                _logger.LogDebug("Skipping oversized Moxfield deck {DeckId} for {CreatorSlug} with listing count {MainboardCount}.", summary.PublicId, creatorSlug, summary.MainboardCount);
                continue;
            }

            if (!forceRefresh && cacheByDeckId.TryGetValue(summary.PublicId, out var cachedEntry) && !string.IsNullOrWhiteSpace(cachedEntry.ContentHash))
            {
                samples.Add(RebuildSampleFromCache(cachedEntry, source));
                continue;
            }

            using var importTimeout = new CancellationTokenSource(_moxfieldImportTimeout);
            using var importCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, importTimeout.Token);
            MoxfieldImportResult imported;
            try
            {
                imported = await _moxfieldPacer.RunAsync(
                    ct => _moxfieldDeckImporter.ImportWithSourceAsync(summary.PublicId, ct), importCancellation.Token).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested && importTimeout.IsCancellationRequested)
            {
                _logger.LogWarning("Moxfield deck {DeckId} import timed out for {CreatorSlug}; stopping crawl and leaving freshness stamp untouched.", summary.PublicId, creatorSlug);
                var currentCacheEntries = await _deckCacheStore.GetByCreatorAsync(creatorSlug, cancellationToken).ConfigureAwait(false);
                // Why: a complete listing is authoritative even when importing a deck fails.
                return RebuildSamplesFromCache(FilterCacheForListing(currentCacheEntries, listing), source);
            }
            if (imported.Source == MoxfieldImportSource.CommanderSpellbookFallback)
            {
                _logger.LogWarning("Moxfield deck {DeckId} for {CreatorSlug} came from the Spellbook fallback; stopping crawl and leaving freshness stamp untouched.", summary.PublicId, creatorSlug);
                var currentCacheEntries = await _deckCacheStore.GetByCreatorAsync(creatorSlug, cancellationToken).ConfigureAwait(false);
                // Why: a complete listing is authoritative even when importing a deck fails.
                return RebuildSamplesFromCache(FilterCacheForListing(currentCacheEntries, listing), source);
            }

            // Why: StapleStripper.FilterOversized sums every board downstream, so the stored set must already be the main deck (l8z D-01).
            var entries = CreatorMainDeck.GetEntries(imported.Entries);
            var size = entries.Sum(entry => entry.Quantity);
            if (size > StapleStripper.MaxDeckSize)
            {
                // Why: warm reads return every cache row, so oversized decks must never be cached.
                _logger.LogDebug("Skipping oversized Moxfield deck {DeckId} for {CreatorSlug} with {CardCount} cards.", summary.PublicId, creatorSlug, size);
                continue;
            }

            var contentHash = ComputeCanonicalHash(entries);
            cacheByDeckId.TryGetValue(summary.PublicId, out var cachedEntryForHashCompare);
            if (cachedEntryForHashCompare is not null && string.Equals(cachedEntryForHashCompare.ContentHash, contentHash, StringComparison.Ordinal))
            {
                samples.Add(RebuildSampleFromCache(cachedEntryForHashCompare, source));
                continue;
            }

            var cacheEntry = new CreatorDeckCacheEntry
            {
                CreatorSlug = creatorSlug,
                DeckId = summary.PublicId,
                ContentHash = contentHash,
                FolderId = null,
                FolderName = null,
                Size = size,
                ConfidenceMarker = ConfidenceMarker,
                Entries = entries,
                CachedUtc = _nowUtc()
            };
            await _deckCacheStore.UpsertAsync(cacheEntry, cancellationToken).ConfigureAwait(false);
            samples.Add(RebuildSampleFromCache(cacheEntry, source));
        }

        if (listing.IsTruncated)
        {
            _logger.LogWarning("Moxfield listing for {CreatorSlug} was truncated; preserving cache rows not returned by the listing.", creatorSlug);
        }
        else
        {
            // Why: a complete Moxfield listing is authoritative for this creator's cache membership.
            await _deckCacheStore.DeleteExceptAsync(creatorSlug, samples.Select(sample => sample.DeckId).ToArray(), cancellationToken).ConfigureAwait(false);
        }
        await _profileSourceStore.SetLastCrawledAsync(creatorSlug, _nowUtc(), cancellationToken).ConfigureAwait(false);
        return samples;
    }

    private static IReadOnlyList<CreatorDeckCacheEntry> FilterCacheForListing(
        IReadOnlyList<CreatorDeckCacheEntry> cacheEntries,
        MoxfieldDeckListResult listing)
    {
        // Why: timeout and Spellbook-fallback returns never prune, so a pre-skipped deck's stale row would otherwise be served.
        var preSkippedIds = listing.Decks
            .Where(summary => summary.MainboardCount > StapleStripper.MaxDeckSize)
            .Select(summary => summary.PublicId)
            .ToHashSet(StringComparer.Ordinal);
        if (listing.IsTruncated)
        {
            return cacheEntries.Where(entry => !preSkippedIds.Contains(entry.DeckId)).ToArray();
        }

        var publicIds = listing.Decks.Select(summary => summary.PublicId).ToHashSet(StringComparer.Ordinal);
        return cacheEntries.Where(entry => publicIds.Contains(entry.DeckId) && !preSkippedIds.Contains(entry.DeckId)).ToArray();
    }

    private static IReadOnlyList<CreatorDeckSample> RebuildSamplesFromCache(
        IReadOnlyList<CreatorDeckCacheEntry> cacheEntries,
        CreatorProfileSource source)
    {
        return cacheEntries.Select(entry => RebuildSampleFromCache(entry, source)).ToArray();
    }

    private static CreatorDeckSample RebuildSampleFromCache(CreatorDeckCacheEntry entry, CreatorProfileSource source)
    {
        // Why: legacy rows cached before stripping at crawl contain all boards, and production has no force-refresh path.
        var entries = CreatorMainDeck.GetEntries(entry.Entries);
        return new CreatorDeckSample
        {
            DeckId = entry.DeckId,
            Entries = entries,
            CardCount = entries.Sum(deckEntry => deckEntry.Quantity),
            FolderId = entry.FolderId,
            FolderName = entry.FolderName,
            FolderWeight = ResolveFolderWeight(source, entry.FolderId),
            ConfidenceMarker = entry.ConfidenceMarker
        };
    }

    private static double ResolveFolderWeight(CreatorProfileSource source, int? folderId)
    {
        if (folderId is not null && source.FolderWeights.TryGetValue(folderId.Value, out var configuredWeight))
        {
            return configuredWeight;
        }

        return 1.0;
    }

    private static string ComputeCanonicalHash(IEnumerable<DeckEntry> entries)
    {
        var canonical = entries
            .OrderBy(entry => entry.NormalizedName, StringComparer.Ordinal)
            .ThenBy(entry => entry.Board, StringComparer.Ordinal)
            .ThenBy(entry => entry.Category, StringComparer.Ordinal)
            .ThenBy(entry => entry.SetCode, StringComparer.Ordinal)
            .ThenBy(entry => entry.CollectorNumber, StringComparer.Ordinal)
            .Select(entry => string.Join(
                "|",
                entry.NormalizedName ?? string.Empty,
                entry.Quantity,
                entry.Board ?? string.Empty,
                entry.Category ?? string.Empty,
                entry.SetCode ?? string.Empty,
                entry.CollectorNumber ?? string.Empty,
                entry.IsFoil));
        var payload = string.Join("\n", canonical);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
