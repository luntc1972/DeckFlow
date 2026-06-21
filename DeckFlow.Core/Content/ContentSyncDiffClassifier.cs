using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// Pure, side-effect-free classifier that compares production and local
/// <see cref="ContentSiteIndexRow"/> sets by natural key and labels each <em>differing</em> entry
/// with exactly one <see cref="SyncDiffKind"/>. Entries that are already in sync (identical content,
/// equal timestamp) are omitted — in-sync is not a diff. No I/O, no DI, no exceptions for valid input.
/// </summary>
public static class ContentSyncDiffClassifier
{
    /// <summary>
    /// Classifies the differences between a production and a local content index.
    /// </summary>
    /// <param name="prodRows">Rows read from the production <c>content_site_index</c>.</param>
    /// <param name="localRows">Rows from the local store.</param>
    /// <returns>
    /// One <see cref="SyncDiffEntry"/> per natural key that differs between the two sides. Keys whose
    /// production and local rows are identical (equal index timestamp and identical content
    /// fingerprint) are omitted.
    /// </returns>
    public static IReadOnlyList<SyncDiffEntry> Classify(
        IReadOnlyList<ContentSiteIndexRow> prodRows,
        IReadOnlyList<ContentSiteIndexRow> localRows)
    {
        ArgumentNullException.ThrowIfNull(prodRows);
        ArgumentNullException.ThrowIfNull(localRows);

        var prodByKey = IndexByNaturalKey(prodRows);
        var localByKey = IndexByNaturalKey(localRows);

        var entries = new List<SyncDiffEntry>();

        foreach (var (key, prod) in prodByKey)
        {
            if (!localByKey.TryGetValue(key, out var local))
            {
                entries.Add(BuildEntry(key, SyncDiffKind.MissingLocally, prod, localRow: null, localIsNewer: false));
                continue;
            }

            // Compare in UTC so an offset/representation mismatch at the prod boundary
            // (the F-51-PG-01 timestamptz-vs-text class of bug) cannot misclassify equal instants.
            var prodUtc = prod.IndexedUtc.ToUniversalTime().UtcDateTime;
            var localUtc = local.IndexedUtc.ToUniversalTime().UtcDateTime;

            if (prodUtc > localUtc)
            {
                entries.Add(BuildEntry(key, SyncDiffKind.ProdNewer, prod, local, localIsNewer: false));
            }
            else if (localUtc > prodUtc)
            {
                entries.Add(BuildEntry(key, SyncDiffKind.Diverged, prod, local, localIsNewer: true));
            }
            else if (!string.Equals(Fingerprint(prod), Fingerprint(local), StringComparison.Ordinal))
            {
                // Equal timestamps but different content — diverged without a clear direction.
                entries.Add(BuildEntry(key, SyncDiffKind.Diverged, prod, local, localIsNewer: false));
            }

            // Equal timestamps AND identical fingerprint => in sync => emit nothing (R3).
        }

        foreach (var (key, local) in localByKey)
        {
            if (!prodByKey.ContainsKey(key))
            {
                entries.Add(BuildEntry(key, SyncDiffKind.LocalOnly, prodRow: null, local, localIsNewer: false));
            }
        }

        return entries;
    }

    private static Dictionary<string, ContentSiteIndexRow> IndexByNaturalKey(IReadOnlyList<ContentSiteIndexRow> rows)
    {
        var map = new Dictionary<string, ContentSiteIndexRow>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var key = row.PinId;
            if (string.IsNullOrEmpty(key))
            {
                // A row with neither a YouTube id nor an RSS guid has no natural key to reconcile on; skip it.
                continue;
            }

            // First occurrence wins; the store does not emit duplicate natural keys.
            map.TryAdd(key, row);
        }

        return map;
    }

    private static SyncDiffEntry BuildEntry(
        string key,
        SyncDiffKind kind,
        ContentSiteIndexRow? prodRow,
        ContentSiteIndexRow? localRow,
        bool localIsNewer)
    {
        var source = prodRow ?? localRow!;
        var keyType = source.YoutubeVideoId is not null ? "youtube" : "podcast";

        return new SyncDiffEntry
        {
            NaturalKeyType = keyType,
            NaturalKeyValue = key,
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
