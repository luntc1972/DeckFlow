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
            var contentHash = ComputeCanonicalHash(importedEntries);
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
                Size = summary.Size,
                ConfidenceMarker = ConfidenceMarker,
                Entries = importedEntries,
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
            if (!forceRefresh && cacheByDeckId.TryGetValue(summary.PublicId, out var cachedEntry) && !string.IsNullOrWhiteSpace(cachedEntry.ContentHash))
            {
                samples.Add(RebuildSampleFromCache(cachedEntry, source));
                continue;
            }

            var imported = await _moxfieldPacer.RunAsync(
                ct => _moxfieldDeckImporter.ImportWithSourceAsync(summary.PublicId, ct), cancellationToken).ConfigureAwait(false);
            if (imported.Source == MoxfieldImportSource.CommanderSpellbookFallback)
            {
                _logger.LogWarning("Moxfield deck {DeckId} for {CreatorSlug} came from the Spellbook fallback; stopping crawl and leaving freshness stamp untouched.", summary.PublicId, creatorSlug);
                var currentCacheEntries = await _deckCacheStore.GetByCreatorAsync(creatorSlug, cancellationToken).ConfigureAwait(false);
                return RebuildSamplesFromCache(currentCacheEntries, source);
            }

            var size = imported.Entries.Sum(entry => entry.Quantity);
            if (size > StapleStripper.MaxDeckSize)
            {
                // Why: warm reads return every cache row, so oversized decks must never be cached.
                _logger.LogDebug("Skipping oversized Moxfield deck {DeckId} for {CreatorSlug} with {CardCount} cards.", summary.PublicId, creatorSlug, size);
                continue;
            }

            var contentHash = ComputeCanonicalHash(imported.Entries);
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
                Entries = imported.Entries,
                CachedUtc = _nowUtc()
            };
            await _deckCacheStore.UpsertAsync(cacheEntry, cancellationToken).ConfigureAwait(false);
            samples.Add(RebuildSampleFromCache(cacheEntry, source));
        }

        await _profileSourceStore.SetLastCrawledAsync(creatorSlug, _nowUtc(), cancellationToken).ConfigureAwait(false);
        return samples;
    }

    private static IReadOnlyList<CreatorDeckSample> RebuildSamplesFromCache(
        IReadOnlyList<CreatorDeckCacheEntry> cacheEntries,
        CreatorProfileSource source)
    {
        return cacheEntries.Select(entry => RebuildSampleFromCache(entry, source)).ToArray();
    }

    private static CreatorDeckSample RebuildSampleFromCache(CreatorDeckCacheEntry entry, CreatorProfileSource source)
    {
        return new CreatorDeckSample
        {
            DeckId = entry.DeckId,
            Entries = entry.Entries,
            CardCount = entry.Size,
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
