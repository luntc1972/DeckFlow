namespace DeckFlow.Core.Content;

/// <summary>
/// The four prod&lt;-&gt;git&lt;-&gt;seed discrepancy classes emitted by
/// <see cref="ContentKbReconcileClassifier"/> (SYNC-11). Values map 1:1 to the
/// <c>content_kb_reconcile_discrepancy.kind</c> TEXT column vocabulary
/// (<c>published_orphan</c> / <c>file_orphan</c> / <c>seed_drift</c> / <c>body_hash_mismatch</c>)
/// persisted by the Studio-local discrepancy store built in a later plan.
/// </summary>
public enum ContentKbReconcileKind
{
    /// <summary>A visible/approved prod row whose git body file is absent.</summary>
    PublishedOrphan,

    /// <summary>A git <c>.md</c> artifact whose path matches no prod row.</summary>
    FileOrphan,

    /// <summary>A seed-managed prod row whose natural key is absent from the available seed.</summary>
    SeedDrift,

    /// <summary>A prod row whose stored body hash differs from the git body's computed hash.</summary>
    BodyHashMismatch
}

/// <summary>
/// One discrepancy discovered by <see cref="ContentKbReconcileClassifier"/>. Mirrors the
/// record-with-derived-flags shape of <see cref="ContentKbOrphanScanner"/>'s
/// <c>ContentKbRowCheck</c> (D-07: mine the shape, do not extend the scanner itself).
/// </summary>
/// <param name="Id">
/// Deterministic identifier from <see cref="BuildId"/> — stable across input ordering and
/// re-runs, enabling idempotent upsert and resolution-by-absence in the downstream store.
/// </param>
/// <param name="Kind">Which of the four discrepancy classes this is.</param>
/// <param name="NaturalKeyType">
/// Natural-key type (<c>youtube_channel</c> / <c>podcast_rss</c>) for row-keyed kinds;
/// <see langword="null"/> for <see cref="ContentKbReconcileKind.FileOrphan"/>, which has no row.
/// </param>
/// <param name="NaturalKeyValue">Natural-key value for row-keyed kinds; <see langword="null"/> for file-orphan.</param>
/// <param name="ArtifactPath">The content-kb-relative artifact path, when known.</param>
/// <param name="Title">The row's title, when known.</param>
public sealed record ContentKbReconcileDiscrepancy(
    string Id,
    ContentKbReconcileKind Kind,
    string? NaturalKeyType,
    string? NaturalKeyValue,
    string? ArtifactPath,
    string? Title)
{
    /// <summary>
    /// Delimiter joining ID components. Matches the U+0000 NULL separator convention already
    /// established by <see cref="ContentNaturalKey"/> / <see cref="ContentSyncDiffClassifier"/>
    /// (SYNC-05 anti-collision format) — NULL cannot appear in a kind token, natural-key
    /// component, or artifact path, so a component boundary can never be forged.
    /// </summary>
    private const char FieldDelimiter = '\u0000';

    /// <summary>
    /// Builds a deterministic discrepancy ID. Row-keyed kinds (<see cref="ContentKbReconcileKind.PublishedOrphan"/>,
    /// <see cref="ContentKbReconcileKind.SeedDrift"/>, <see cref="ContentKbReconcileKind.BodyHashMismatch"/>)
    /// join the kind token with the natural key's <paramref name="naturalKeyType"/> and
    /// <paramref name="naturalKeyValue"/>. <see cref="ContentKbReconcileKind.FileOrphan"/> has no
    /// row/natural key and is instead keyed by <paramref name="artifactPath"/>. Two discrepancies of
    /// the same kind and key (or, for file-orphan, the same path) always produce byte-identical IDs
    /// regardless of construction order or which run produced them.
    /// </summary>
    /// <param name="kind">The discrepancy class.</param>
    /// <param name="naturalKeyType">Natural-key type; required for every kind except file-orphan.</param>
    /// <param name="naturalKeyValue">Natural-key value; required for every kind except file-orphan.</param>
    /// <param name="artifactPath">Artifact path; required for file-orphan, ignored otherwise.</param>
    /// <returns>A stable, U+0000-delimited discrepancy ID.</returns>
    public static string BuildId(
        ContentKbReconcileKind kind,
        string? naturalKeyType,
        string? naturalKeyValue,
        string? artifactPath)
    {
        var kindToken = KindToken(kind);

        if (kind == ContentKbReconcileKind.FileOrphan)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);
            // Why: file-orphan has no derivable natural key (D-07/T-91-17) — the artifact path IS
            // the identity, joined after a literal "path" token so a file-orphan ID can never
            // collide with a row-keyed ID even if a natural key value happened to equal a path.
            return $"{kindToken}{FieldDelimiter}path{FieldDelimiter}{artifactPath}";
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyValue);
        return $"{kindToken}{FieldDelimiter}{naturalKeyType}{FieldDelimiter}{naturalKeyValue}";
    }

    private static string KindToken(ContentKbReconcileKind kind) => kind switch
    {
        ContentKbReconcileKind.PublishedOrphan => "published_orphan",
        ContentKbReconcileKind.FileOrphan => "file_orphan",
        ContentKbReconcileKind.SeedDrift => "seed_drift",
        ContentKbReconcileKind.BodyHashMismatch => "body_hash_mismatch",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown reconcile discrepancy kind.")
    };
}
