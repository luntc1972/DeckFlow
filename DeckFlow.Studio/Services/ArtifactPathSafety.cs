namespace DeckFlow.Studio.Services;

/// <summary>
/// Shared Studio path-safety guard for repo-relative content-kb artifact paths. Extracted
/// verbatim (behavior-preserving) from <c>PullFromProdCoordinator</c>'s former private
/// <c>IsSafeArtifactPath</c>/<c>TryBuildContainedPath</c> pair so it has exactly ONE Studio
/// implementation — both <c>PullFromProdCoordinator</c> and <c>GitBodyCoverageAudit</c> call
/// this helper instead of each carrying its own copy (90-CONTEXT.md D-11 / T-90-05).
/// Rejects rooted (Unix or Windows-drive), backslash-rooted, and <c>..</c>-traversal paths, and
/// requires the path to start with the literal <c>content-kb/</c> prefix; containment under a
/// caller-supplied root is then verified with a case-insensitive prefix match on the resolved
/// full path.
/// </summary>
internal static class ArtifactPathSafety
{
    /// <summary>
    /// Resolves <paramref name="artifactPath"/> against <paramref name="root"/> and returns
    /// <see langword="true"/> with the resolved full path in <paramref name="resolvedPath"/> only
    /// when the path is safe (see <see cref="IsSafeArtifactPath"/>) AND the resolved path is
    /// actually contained under <paramref name="root"/>.
    /// </summary>
    public static bool TryBuildContainedPath(string root, string artifactPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (!IsSafeArtifactPath(artifactPath))
        {
            return false;
        }

        var rootFull = Path.GetFullPath(root);
        var rootWithSeparator = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(rootFull, artifactPath));
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        resolvedPath = candidate;
        return true;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="artifactPath"/> is a non-rooted,
    /// non-traversal, <c>content-kb/</c>-prefixed relative path — the shape every stored
    /// <c>ArtifactPath</c> must have before it is safe to combine with a filesystem root.
    /// </summary>
    public static bool IsSafeArtifactPath(string artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            return false;
        }

        if (Path.IsPathRooted(artifactPath)
            || IsWindowsRootedPath(artifactPath)
            || artifactPath[0] == '/'
            || artifactPath[0] == '\\')
        {
            return false;
        }

        if (!artifactPath.StartsWith("content-kb/", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = artifactPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0
            && !segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
    }

    private static bool IsWindowsRootedPath(string artifactPath)
        => artifactPath.Length >= 3
            && char.IsLetter(artifactPath[0])
            && artifactPath[1] == ':'
            && (artifactPath[2] == '\\' || artifactPath[2] == '/');
}
