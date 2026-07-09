using DeckFlow.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Content;

/// <summary>
/// Pure, I/O-free classifier that compares already-loaded prod rows, the set of existing git-tree
/// body artifact paths, and an availability-aware seed read against each other and emits the four
/// SYNC-11 discrepancy classes: published-orphan, file-orphan, seed-drift, body-hash-mismatch. No
/// DB, no git process, no file I/O — the orchestrator (a later plan) performs all of that and
/// passes in-memory collections. Mirrors <see cref="ContentSyncDiffClassifier"/>'s shape exactly
/// (static class, single entry point, optional logger for skip warnings) but does NOT import its
/// timestamp-direction (<c>SyncDiffKind</c>) branching — seed-drift is a plain set-difference, not
/// a newer/older comparison (91-RESEARCH.md Pitfall 3).
/// </summary>
public static class ContentKbReconcileClassifier
{
    /// <summary>
    /// Classifies <paramref name="prodRows"/> against the supplied git-tree and seed inputs.
    /// </summary>
    /// <param name="prodRows">Already-loaded rows read from the production <c>content_site_index</c>.</param>
    /// <param name="existingGitBodyRelPaths">
    /// The set of content-kb-relative artifact paths for <c>.md</c> body files that exist in the
    /// operator's git checkout. Built and <c>File.Exists</c>-verified by the orchestrator; these ARE
    /// artifact paths, normalized identically to <see cref="ContentSiteIndexRow.ArtifactPath"/>.
    /// </param>
    /// <param name="seedIndex">
    /// The availability-aware seed read (<see cref="SeedIndexFileReader.Read"/> result). Seed-drift
    /// is computed ONLY when <see cref="SeedIndexReadResult.SeedAvailable"/> is <see langword="true"/> —
    /// see the seed-drift branch below for why (T-91-25, Codex BLOCK).
    /// </param>
    /// <param name="gitBodyByRelPath">
    /// Artifact path -&gt; body text map for rows whose git body is present, used for body-hash-mismatch.
    /// Supplied by the orchestrator (it does the file read); a row whose path is absent from this map
    /// has no body to hash and is not evaluated for body-hash-mismatch (it may already be a
    /// published-orphan if visible/approved).
    /// </param>
    /// <param name="logger">
    /// Optional logger; a structured warning is emitted once per prod row that has no derivable
    /// natural key (mirrors <see cref="ContentSyncDiffClassifier"/> D-08), and once for the whole run
    /// when the seed is unavailable. Passing no logger keeps the method effectively pure.
    /// </param>
    /// <returns>The discrepancies found, in no particular guaranteed order (IDs are deterministic; the list itself is not sorted).</returns>
    public static IReadOnlyList<ContentKbReconcileDiscrepancy> Classify(
        IReadOnlyList<ContentSiteIndexRow> prodRows,
        IReadOnlySet<string> existingGitBodyRelPaths,
        SeedIndexReadResult seedIndex,
        IReadOnlyDictionary<string, string> gitBodyByRelPath,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(prodRows);
        ArgumentNullException.ThrowIfNull(existingGitBodyRelPaths);
        ArgumentNullException.ThrowIfNull(seedIndex);
        ArgumentNullException.ThrowIfNull(gitBodyByRelPath);

        var discrepancies = new List<ContentKbReconcileDiscrepancy>();
        var prodArtifactPaths = new HashSet<string>(StringComparer.Ordinal);

        if (!seedIndex.SeedAvailable)
        {
            // Why (T-91-25, Codex BLOCK): an unavailable/parse-failed seed collapsed to an empty
            // key set would otherwise look identical to a seed that legitimately removed every
            // key, mass-flagging EVERY seed_managed=true row as drift and driving a mass-hide.
            // Seed-drift is skipped entirely below; published-orphan, file-orphan, and
            // body-hash-mismatch do NOT consult seed membership and are still computed.
            logger?.LogWarning(
                "Content KB seed unavailable; seed-drift detection skipped for this reconcile pass.");
        }

        foreach (var row in prodRows)
        {
            prodArtifactPaths.Add(row.ArtifactPath);

            var hasKey = ContentNaturalKey.TryDerive(row, out var naturalKey);
            if (!hasKey)
            {
                // Mirrors ContentSyncDiffClassifier.IndexByNaturalKey (D-08): a row with neither a
                // YouTube id nor an RSS guid cannot be ID'd deterministically for any row-keyed
                // discrepancy class, so it is skipped (but surfaced) rather than silently dropped.
                logger?.LogWarning(
                    "Skipping content row with no natural key (neither YouTube id nor RSS guid) during reconcile classification: {Title} [{Source}]",
                    row.Title,
                    row.Source);
                continue;
            }

            if (IsPublishedOrphan(row) && !existingGitBodyRelPaths.Contains(row.ArtifactPath))
            {
                discrepancies.Add(BuildRowDiscrepancy(ContentKbReconcileKind.PublishedOrphan, naturalKey, row));
            }

            if (seedIndex.SeedAvailable && row.SeedManaged == true)
            {
                var compositeKey = $"{naturalKey.Type}\u0000{naturalKey.Value}";
                if (!seedIndex.NaturalKeys.Contains(compositeKey))
                {
                    discrepancies.Add(BuildRowDiscrepancy(ContentKbReconcileKind.SeedDrift, naturalKey, row));
                }
            }

            if (!string.IsNullOrEmpty(row.BodySha256)
                && gitBodyByRelPath.TryGetValue(row.ArtifactPath, out var body))
            {
                var computedHash = ContentSiteIndexContentSignature.ComputeBodySha256(body);
                if (!string.Equals(row.BodySha256, computedHash, StringComparison.Ordinal))
                {
                    discrepancies.Add(BuildRowDiscrepancy(ContentKbReconcileKind.BodyHashMismatch, naturalKey, row));
                }
            }
        }

        // FILE-ORPHAN IDENTITY (D-07/T-91-17): the ARTIFACT PATH is the PRIMARY and ONLY match key
        // here — a git artifact path present in existingGitBodyRelPaths but absent from the set of
        // prod rows' own artifact paths is a file-orphan. ContentNaturalKey.TryDerive is never
        // invoked in this direction: a bare git .md path carries no trusted row metadata (no
        // YouTube id, no RSS guid) to derive a natural key from, so path-inference would be an ad
        // hoc guessing scheme, not a natural-key derivation. Artifact-path matching is therefore
        // authoritative for the file->row direction; TryDerive remains the row->natural-key path
        // used above for row-keyed classes only.
        foreach (var relPath in existingGitBodyRelPaths)
        {
            if (!prodArtifactPaths.Contains(relPath))
            {
                discrepancies.Add(new ContentKbReconcileDiscrepancy(
                    ContentKbReconcileDiscrepancy.BuildId(ContentKbReconcileKind.FileOrphan, null, null, relPath),
                    ContentKbReconcileKind.FileOrphan,
                    NaturalKeyType: null,
                    NaturalKeyValue: null,
                    ArtifactPath: relPath,
                    Title: null));
            }
        }

        return discrepancies;
    }

    // Why: mirrors GitBodyCoverageAudit's published-orphan gate exactly (approved + visible) — the
    // same "every visible row's body is actually in git" precondition, lifted into the pure
    // classifier per 91-PATTERNS.md.
    private static bool IsPublishedOrphan(ContentSiteIndexRow row)
        => string.Equals(row.ApprovalStatus, "approved", StringComparison.Ordinal) && row.IsVisible;

    private static ContentKbReconcileDiscrepancy BuildRowDiscrepancy(
        ContentKbReconcileKind kind,
        (string Type, string Value) naturalKey,
        ContentSiteIndexRow row)
        => new(
            ContentKbReconcileDiscrepancy.BuildId(kind, naturalKey.Type, naturalKey.Value, null),
            kind,
            naturalKey.Type,
            naturalKey.Value,
            row.ArtifactPath,
            row.Title);
}
