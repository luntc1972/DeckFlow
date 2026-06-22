using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// Per-row artifact-presence classification produced by <see cref="ContentKbOrphanScanner"/>.
/// </summary>
/// <param name="ArtifactPath">The relative <c>content-kb/{slug}/{id}.md</c> artifact path from the row.</param>
/// <param name="Exists">Whether the artifact file exists under the supplied content base.</param>
/// <param name="IsVisible">Whether the row is published to the public Content KB surface.</param>
/// <param name="IsHidden">Whether the row is deliberately hidden from the public surface.</param>
/// <param name="ApprovalStatus">Approval workflow status of the row.</param>
/// <param name="IsPublishedOrphan">True when the artifact is missing AND the row is publicly visible (<see cref="IsVisible"/> and not <see cref="IsHidden"/>).</param>
public sealed record ContentKbRowCheck(
    string ArtifactPath,
    bool Exists,
    bool IsVisible,
    bool IsHidden,
    string ApprovalStatus,
    bool IsPublishedOrphan);

/// <summary>
/// Aggregate result of scanning a content_site_index row set against local artifact files.
/// </summary>
/// <param name="TotalRows">Total rows scanned.</param>
/// <param name="RowsWithArtifact">Rows whose artifact file exists on disk.</param>
/// <param name="MissingCount">Rows whose artifact file is absent.</param>
/// <param name="PublishedOrphanCount">Missing-artifact rows that are publicly visible (the severity gate).</param>
/// <param name="HiddenOrphanCount">Missing-artifact rows that are not publicly visible.</param>
/// <param name="Rows">Per-row classification detail in input order.</param>
public sealed record ContentKbOrphanScanResult(
    int TotalRows,
    int RowsWithArtifact,
    int MissingCount,
    int PublishedOrphanCount,
    int HiddenOrphanCount,
    IReadOnlyList<ContentKbRowCheck> Rows);

/// <summary>
/// Pure, IO-light orphan detector that classifies each <see cref="ContentSiteIndexRow"/> as
/// OK / published-orphan / hidden-orphan by checking whether its artifact file exists under a
/// content base, mirroring the live serving resolution
/// (<c>ContentKbArtifactPathResolver.ResolveArtifactFullPath</c>:
/// <c>Path.GetFullPath(Path.Combine(contentBase, artifactPath))</c>). <see cref="ContentSiteIndexRow.ArtifactPath"/>
/// already begins with <c>content-kb/</c>, so the content base is the directory that CONTAINS
/// <c>content-kb/</c> — never the <c>content-kb</c> directory itself.
/// </summary>
public static class ContentKbOrphanScanner
{
    /// <summary>
    /// Classifies each row by artifact presence under <paramref name="contentBase"/>.
    /// </summary>
    /// <param name="rows">Already-loaded content_site_index rows (no DB access performed here).</param>
    /// <param name="contentBase">
    /// The base directory that contains the <c>content-kb</c> artifact tree (the PARENT of
    /// <c>content-kb/</c>), mirroring <c>ContentKbArtifactPathResolver.ContentBase</c>.
    /// </param>
    /// <returns>The aggregate scan result with counts and per-row detail.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when any row's <see cref="ContentSiteIndexRow.ArtifactPath"/> is rooted or contains a
    /// <c>..</c> path segment — rejected before any path combination, matching
    /// <c>ContentSiteIndexStore.ValidateArtifactPath</c>.
    /// </exception>
    public static ContentKbOrphanScanResult Scan(IReadOnlyList<ContentSiteIndexRow> rows, string contentBase)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentBase);

        var checks = new List<ContentKbRowCheck>(rows.Count);
        foreach (var row in rows)
        {
            // Why: reject traversal/rooted artifact paths before combining with the content base,
            // matching ContentSiteIndexStore.ValidateArtifactPath so the scanner cannot be steered
            // outside the artifact tree.
            ValidateArtifactPath(row.ArtifactPath);

            var resolved = Path.GetFullPath(Path.Combine(contentBase, row.ArtifactPath));
            var exists = File.Exists(resolved);
            var missing = !exists;
            var publishedOrphan = missing && row.IsVisible && !row.IsHidden;

            checks.Add(new ContentKbRowCheck(
                row.ArtifactPath,
                exists,
                row.IsVisible,
                row.IsHidden,
                row.ApprovalStatus,
                publishedOrphan));
        }

        var rowsWithArtifact = checks.Count(check => check.Exists);
        var missingCount = checks.Count - rowsWithArtifact;
        var publishedOrphanCount = checks.Count(check => check.IsPublishedOrphan);
        var hiddenOrphanCount = checks.Count(check => !check.Exists && !check.IsPublishedOrphan);

        return new ContentKbOrphanScanResult(
            checks.Count,
            rowsWithArtifact,
            missingCount,
            publishedOrphanCount,
            hiddenOrphanCount,
            checks);
    }

    private static void ValidateArtifactPath(string artifactPath)
    {
        if (Path.IsPathRooted(artifactPath) || IsWindowsRootedPath(artifactPath))
        {
            throw new ArgumentException(
                "Artifact path must be relative.",
                nameof(ContentSiteIndexRow.ArtifactPath));
        }

        var segments = artifactPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Artifact path must not contain '..' path segments.",
                nameof(ContentSiteIndexRow.ArtifactPath));
        }
    }

    private static bool IsWindowsRootedPath(string artifactPath)
        => artifactPath.Length >= 3
            && char.IsLetter(artifactPath[0])
            && artifactPath[1] == ':'
            && (artifactPath[2] == '\\' || artifactPath[2] == '/');
}
