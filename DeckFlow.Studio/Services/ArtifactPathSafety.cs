using DeckFlow.Core.Content;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Studio-side forwarder retained for existing call sites; the shared path-safety logic now
/// lives in <see cref="ContentKbArtifactPath"/>.
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
        => ContentKbArtifactPath.TryResolveContained(root, artifactPath, out resolvedPath);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="artifactPath"/> is a non-rooted,
    /// non-traversal, <c>content-kb/</c>-prefixed relative path — the shape every stored
    /// <c>ArtifactPath</c> must have before it is safe to combine with a filesystem root.
    /// </summary>
    public static bool IsSafeArtifactPath(string artifactPath)
        => ContentKbArtifactPath.IsSafe(artifactPath);
}
