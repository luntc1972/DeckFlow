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
/// Crawls creator-scoped Archidekt decks with a creator-level warm-cache short-circuit.
/// </summary>
public sealed class CreatorProfileDeckCrawler
{
    private const string ConfidenceMarker = "ok";
    private static readonly TimeSpan MoxfieldImportInterval = TimeSpan.FromMilliseconds(500);

    private readonly IArchidektOwnerClient _archidektOwnerClient;
    private readonly IArchidektDeckImporter _archidektDeckImporter;
    private readonly IMoxfieldOwnerClient _moxfieldOwnerClient;
    private readonly IMoxfieldDeckImporter _moxfieldDeckImporter;
    private readonly ICreatorProfileSourceStore _profileSourceStore;
    private readonly ICreatorDeckCacheStore _deckCacheStore;
    private readonly ILogger<CreatorProfileDeckCrawler> _logger;
    private readonly TimeSpan _freshnessWindow;
    private readonly Func<DateTimeOffset> _nowUtc;

    /// <summary>
    /// Creates a creator-profile deck crawler.
    /// </summary>
    public CreatorProfileDeckCrawler(
        IArchidektOwnerClient ownerClient,
        IArchidektDeckImporter deckImporter,
        IMoxfieldOwnerClient moxfieldOwnerClient,
        IMoxfieldDeckImporter moxfieldDeckImporter,
        ICreatorProfileSourceStore profileSourceStore,
        ICreatorDeckCacheStore deckCacheStore,
        ILogger<CreatorProfileDeckCrawler>? logger = null,
        TimeSpan? freshnessWindow = null,
        Func<DateTimeOffset>? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(ownerClient);
        ArgumentNullException.ThrowIfNull(deckImporter);
        ArgumentNullException.ThrowIfNull(moxfieldOwnerClient);
        ArgumentNullException.ThrowIfNull(moxfieldDeckImporter);
        ArgumentNullException.ThrowIfNull(profileSourceStore);
        ArgumentNullException.ThrowIfNull(deckCacheStore);
        _archidektOwnerClient = ownerClient;
        _archidektDeckImporter = deckImporter;
        _moxfieldOwnerClient = moxfieldOwnerClient;
        _moxfieldDeckImporter = moxfieldDeckImporter;
        _profileSourceStore = profileSourceStore;
        _deckCacheStore = deckCacheStore;
        _logger = logger ?? NullLogger<CreatorProfileDeckCrawler>.Instance;
        _freshnessWindow = freshnessWindow ?? TimeSpan.FromHours(24);
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Crawls the creator's public Archidekt decks or serves them from the creator cache.
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

        if (!forceRefresh
            && source.LastCrawledUtc is not null
            && (_nowUtc() - source.LastCrawledUtc.Value) < _freshnessWindow)
        {
            _logger.LogDebug("Serving creator {CreatorSlug} entirely from warm cache.", creatorSlug);
            var warmEntries = await _deckCacheStore.GetByCreatorAsync(creatorSlug, cancellationToken).ConfigureAwait(false);
            return RebuildSamplesFromCache(warmEntries, source);
        }

        var cacheEntries = await _deckCacheStore.GetByCreatorAsync(creatorSlug, cancellationToken).ConfigureAwait(false);
        var cacheByDeckId = cacheEntries.ToDictionary(entry => entry.DeckId, StringComparer.Ordinal);

        if (string.Equals(source.Platform, "moxfield", StringComparison.OrdinalIgnoreCase))
        {
            var moxfieldSamples = await CrawlMoxfieldAsync(creatorSlug, source, cacheByDeckId, cancellationToken).ConfigureAwait(false);
            await _profileSourceStore.SetLastCrawledAsync(creatorSlug, _nowUtc(), cancellationToken).ConfigureAwait(false);
            return moxfieldSamples;
        }

        var resolvedUsername = await _archidektOwnerClient.ResolveUsernameAsync(source.ProfileUsername, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(resolvedUsername))
        {
            var fallbackInput = source.ProfileUrl ?? source.ProfileUsername;
            if (!ArchidektOwnerUrl.TryGetUsername(fallbackInput, out resolvedUsername))
            {
                return Array.Empty<CreatorDeckSample>();
            }
        }

        var summaries = await _archidektOwnerClient.ListDeckSummariesAsync(resolvedUsername, cancellationToken).ConfigureAwait(false);
        var samples = new List<CreatorDeckSample>();
        foreach (var summary in summaries)
        {
            if (summary.Size > StapleStripper.MaxDeckSize)
            {
                continue;
            }

            samples.Add(await GetOrImportSampleAsync(
                creatorSlug,
                source,
                cacheByDeckId,
                summary.Id,
                summary.Size,
                summary.ParentFolderId,
                summary.ParentFolderName,
                ct => _archidektDeckImporter.ImportAsync(summary.Id, ct),
                cancellationToken).ConfigureAwait(false));
        }

        await _profileSourceStore.SetLastCrawledAsync(creatorSlug, _nowUtc(), cancellationToken).ConfigureAwait(false);
        return samples;
    }

    private async Task<IReadOnlyList<CreatorDeckSample>> CrawlMoxfieldAsync(
        string creatorSlug,
        CreatorProfileSource source,
        IReadOnlyDictionary<string, CreatorDeckCacheEntry> cacheByDeckId,
        CancellationToken cancellationToken)
    {
        var summaries = await _moxfieldOwnerClient.ListDeckSummariesAsync(source.ProfileUsername, cancellationToken).ConfigureAwait(false);
        var samples = new List<CreatorDeckSample>();
        var hasImportedDeck = false;

        foreach (var summary in summaries)
        {
            CreatorDeckSample sample;
            if (TryGetCachedSample(cacheByDeckId, summary.PublicId, source, out var cachedSample))
            {
                sample = cachedSample;
            }
            else
            {
                if (hasImportedDeck)
                {
                    await Task.Delay(MoxfieldImportInterval, cancellationToken).ConfigureAwait(false);
                }

                sample = await GetOrImportSampleAsync(
                    creatorSlug,
                    source,
                    cacheByDeckId,
                    summary.PublicId,
                    null,
                    null,
                    null,
                    ct => _moxfieldDeckImporter.ImportAsync(summary.PublicId, ct),
                    cancellationToken).ConfigureAwait(false);
                hasImportedDeck = true;
            }

            if (sample.CardCount > StapleStripper.MaxDeckSize)
            {
                continue;
            }

            samples.Add(sample);
        }

        return samples;
    }

    private async Task<CreatorDeckSample> GetOrImportSampleAsync(
        string creatorSlug,
        CreatorProfileSource source,
        IReadOnlyDictionary<string, CreatorDeckCacheEntry> cacheByDeckId,
        string deckId,
        int? summarySize,
        int? folderId,
        string? folderName,
        Func<CancellationToken, Task<List<DeckEntry>>> importAsync,
        CancellationToken cancellationToken)
    {
        if (TryGetCachedSample(cacheByDeckId, deckId, source, out var cachedSample))
        {
            return cachedSample;
        }

        var importedEntries = await importAsync(cancellationToken).ConfigureAwait(false);
        var cacheEntry = new CreatorDeckCacheEntry
        {
            CreatorSlug = creatorSlug,
            DeckId = deckId,
            ContentHash = ComputeCanonicalHash(importedEntries),
            FolderId = folderId,
            FolderName = folderName,
            Size = summarySize ?? importedEntries.Sum(entry => entry.Quantity),
            ConfidenceMarker = ConfidenceMarker,
            Entries = importedEntries,
            CachedUtc = _nowUtc()
        };

        await _deckCacheStore.UpsertAsync(cacheEntry, cancellationToken).ConfigureAwait(false);
        return RebuildSampleFromCache(cacheEntry, source);
    }

    private static bool TryGetCachedSample(
        IReadOnlyDictionary<string, CreatorDeckCacheEntry> cacheByDeckId,
        string deckId,
        CreatorProfileSource source,
        out CreatorDeckSample sample)
    {
        if (cacheByDeckId.TryGetValue(deckId, out var cachedEntry)
            && !string.IsNullOrWhiteSpace(cachedEntry.ContentHash))
        {
            sample = RebuildSampleFromCache(cachedEntry, source);
            return true;
        }

        sample = default!;
        return false;
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
