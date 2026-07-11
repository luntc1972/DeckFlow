namespace DeckFlow.Core.Content;

/// <summary>
/// Shared path-safety guard for repo-relative content-kb artifact paths. Rejects rooted (Unix or
/// Windows-drive), backslash-rooted, and <c>..</c>-traversal paths, and requires the path to
/// start with the literal <c>content-kb/</c> prefix; containment under a caller-supplied root is
/// then verified with a case-insensitive prefix match on the resolved full path.
/// </summary>
public static class ContentKbArtifactPath
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="artifactPath"/> is a non-rooted,
    /// non-traversal, <c>content-kb/</c>-prefixed relative path.
    /// </summary>
    /// <param name="artifactPath">Stored relative artifact path to validate.</param>
    /// <returns>
    /// <see langword="true"/> when the path shape is safe to combine with a filesystem root;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsSafe(string artifactPath)
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

    /// <summary>
    /// Resolves <paramref name="artifactPath"/> against <paramref name="root"/> and returns
    /// <see langword="true"/> with the resolved full path in <paramref name="resolvedPath"/> only
    /// when the path is safe (see <see cref="IsSafe(string)"/>) and the resolved path is actually
    /// contained under <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Filesystem root that must contain the resolved path.</param>
    /// <param name="artifactPath">Stored relative artifact path.</param>
    /// <param name="resolvedPath">Resolved absolute path when successful; otherwise an empty string.</param>
    /// <returns>
    /// <see langword="true"/> when the artifact path is safe and contained under the supplied
    /// root; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryResolveContained(string root, string artifactPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (!IsSafe(artifactPath))
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
    /// Returns <see langword="true"/> when the path is Windows-drive-rooted (for example
    /// <c>C:\</c> or <c>C:/</c>).
    /// </summary>
    /// <param name="artifactPath">Path to inspect.</param>
    /// <returns><see langword="true"/> when the path is Windows-drive-rooted; otherwise, <see langword="false"/>.</returns>
    public static bool IsWindowsRootedPath(string artifactPath)
        => artifactPath.Length >= 3
            && char.IsLetter(artifactPath[0])
            && artifactPath[1] == ':'
            && (artifactPath[2] == '\\' || artifactPath[2] == '/');
}
