using DeckFlow.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Content;

/// <summary>
/// Pure classifier that compares production and local <see cref="ContentSiteIndexRow"/> sets by natural
/// key and labels each <em>differing</em> entry with exactly one <see cref="SyncDiffKind"/>. Entries that
/// are already in sync (identical content, equal timestamp) are omitted — in-sync is not a diff. No I/O,
/// no DI; the only side channel is an OPTIONAL <see cref="ILogger"/> used to warn on rows that have no
/// natural key to reconcile on (D-08). Passing no logger keeps the method effectively pure.
/// </summary>
public static class ContentSyncDiffClassifier
{
    /// <summary>
    /// Classifies the differences between a production and a local content index.
    /// </summary>
    /// <param name="prodRows">Rows read from the production <c>content_site_index</c>.</param>
    /// <param name="localRows">Rows from the local store.</param>
    /// <param name="logger">
    /// Optional logger; when supplied, a structured warning is emitted for each row skipped because it has
    /// no natural key (D-08). Defaults to <see langword="null"/> so existing call sites stay pure.
    /// </param>
    /// <returns>
    /// One <see cref="SyncDiffEntry"/> per natural key that differs between the two sides. Keys whose
    /// production and local rows are identical (equal index timestamp and identical content
    /// fingerprint) are omitted.
    /// </returns>
    public static IReadOnlyList<SyncDiffEntry> Classify(
        IReadOnlyList<ContentSiteIndexRow> prodRows,
        IReadOnlyList<ContentSiteIndexRow> localRows,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(prodRows);
        ArgumentNullException.ThrowIfNull(localRows);

        var prodByKey = IndexByNaturalKey(prodRows, logger);
        var localByKey = IndexByNaturalKey(localRows, logger);

        var entries = new List<SyncDiffEntry>();

        foreach (var (key, prod) in prodByKey)
        {
            if (!localByKey.TryGetValue(key, out var local))
            {
                entries.Add(BuildEntry(SyncDiffKind.MissingLocally, prod, localRow: null, localIsNewer: false));
                continue;
            }

            // Compare in UTC so an offset/representation mismatch at the prod boundary
            // (the F-51-PG-01 timestamptz-vs-text class of bug) cannot misclassify equal instants.
            var prodUtc = prod.IndexedUtc.ToUniversalTime().UtcDateTime;
            var localUtc = local.IndexedUtc.ToUniversalTime().UtcDateTime;

            if (prodUtc > localUtc)
            {
                entries.Add(BuildEntry(SyncDiffKind.ProdNewer, prod, local, localIsNewer: false));
            }
            else if (localUtc > prodUtc)
            {
                entries.Add(BuildEntry(SyncDiffKind.Diverged, prod, local, localIsNewer: true));
            }
            else if (!string.Equals(Fingerprint(prod), Fingerprint(local), StringComparison.Ordinal))
            {
                // Equal timestamps but different content — diverged without a clear direction.
                entries.Add(BuildEntry(SyncDiffKind.Diverged, prod, local, localIsNewer: false));
            }

            // Equal timestamps AND identical fingerprint => in sync => emit nothing (R3).
        }

        foreach (var (key, local) in localByKey)
        {
            if (!prodByKey.ContainsKey(key))
            {
                entries.Add(BuildEntry(SyncDiffKind.LocalOnly, prodRow: null, local, localIsNewer: false));
            }
        }

        return entries;
    }

    private static Dictionary<string, ContentSiteIndexRow> IndexByNaturalKey(
        IReadOnlyList<ContentSiteIndexRow> rows,
        ILogger? logger)
    {
        var map = new Dictionary<string, ContentSiteIndexRow>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!ContentNaturalKey.TryDerive(row, out var nk))
            {
                // A row with neither a YouTube id nor an RSS guid has no natural key to reconcile on; skip it,
                // but surface it (D-08) rather than dropping silently.
                logger?.LogWarning(
                    "Skipping content row with no natural key (neither YouTube id nor RSS guid): {Title} [{Source}]",
                    row.Title,
                    row.Source);
                continue;
            }

            // Composite key uses the U+0000 NULL separator (the \u0000 escape) so a YouTube id can never
            // collide with a podcast guid; identical to the DirectPushCoordinator key format (SYNC-05).
            // First occurrence wins; the store does not emit duplicate natural keys.
            map.TryAdd($"{nk.Type}\u0000{nk.Value}", row);
        }

        return map;
    }

    private static SyncDiffEntry BuildEntry(
        SyncDiffKind kind,
        ContentSiteIndexRow? prodRow,
        ContentSiteIndexRow? localRow,
        bool localIsNewer)
    {
        var source = prodRow ?? localRow!;
        // Derive (type, value) from the SAME shared helper the index keys on, so the emitted entry carries
        // the stored vocabulary ("youtube_channel"/"podcast_rss") — D-07. The source always keys (it produced
        // a composite dictionary key), so TryDerive returns true here.
        ContentNaturalKey.TryDerive(source, out var nk);

        return new SyncDiffEntry
        {
            NaturalKeyType = nk.Type,
            NaturalKeyValue = nk.Value,
            Kind = kind,
            Title = prodRow?.Title ?? localRow!.Title,
            ProdRow = prodRow,
            LocalRow = localRow,
            ArtifactPath = prodRow?.ArtifactPath ?? localRow!.ArtifactPath,
            LocalIsNewer = localIsNewer,
            ArtifactDownloaded = false
        };
    }

    private static string Fingerprint(ContentSiteIndexRow row) =>
        string.Join(
            '',
            row.Title,
            row.ArtifactPath,
            string.Join(',', row.ArchetypeTags),
            string.Join(',', row.BracketTags),
            string.Join(',', row.CardCategoryTags));
}
