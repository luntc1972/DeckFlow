using DeckFlow.Core.Content;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Studio adapter over the local <c>content-kb</c> artifact root for the host-agnostic
/// <see cref="ContentBodyHashBackfill"/> service (D-08). Mirrors
/// <c>ReviewCoordinator.ReadRelativeSafe</c>'s containment-guarded read: a stored
/// <c>content-kb/</c>-relative artifact path is resolved under the DATA ROOT (parent of
/// <see cref="ContentKbOrchestratorOptions.ArtifactRoot"/>), rejecting rooted/<c>".."</c> paths
/// and any resolved path that escapes the data root. Returns <see langword="null"/> on any
/// resolution or read failure — never throws.
/// </summary>
public sealed class StudioContentArtifactBodyResolver : IContentArtifactBodyResolver
{
    private readonly ContentKbOrchestratorOptions _options;

    /// <summary>
    /// Creates a new Studio artifact-body resolver bound to the local artifact root.
    /// </summary>
    /// <param name="options">Resolved orchestrator options carrying the local <c>ArtifactRoot</c>.</param>
    public StudioContentArtifactBodyResolver(ContentKbOrchestratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    /// <inheritdoc />
    public async Task<string?> TryReadArtifactTextAsync(string artifactPath, CancellationToken cancellationToken = default)
    {
        // Reject rooted paths and paths that contain ".." traversal segments.
        if (string.IsNullOrWhiteSpace(artifactPath) || Path.IsPathRooted(artifactPath))
        {
            return null;
        }

        var normalizedPath = artifactPath.Replace('\\', '/');
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            return null;
        }

        // Require the content-kb/ subtree prefix, mirroring the web resolver's guard, so a
        // corrupted or malicious index row can only ever read from the content-kb artifact tree
        // under the data root — never a sibling directory like {dataRoot}/secrets.md.
        if (!normalizedPath.StartsWith("content-kb/", StringComparison.Ordinal))
        {
            return null;
        }

        // Resolve against the DATA ROOT = parent of ArtifactRoot.
        // ArtifactRoot = {studioDataDir}/content-kb; dataRoot = {studioDataDir}.
        // The stored path already begins with "content-kb/", so combining with the data root
        // yields {studioDataDir}/content-kb/{sourceSlug}/{id}.md — correct.
        var dataRoot = Directory.GetParent(_options.ArtifactRoot)?.FullName ?? _options.ArtifactRoot;
        var artifactAbs = Path.GetFullPath(Path.Combine(dataRoot, artifactPath));

        // CONTAINMENT GUARD: the normalized absolute path must start with the canonical
        // data root followed by the directory separator to prevent escape to parent directories.
        var canonicalDataRoot = Path.GetFullPath(dataRoot);
        if (!artifactAbs.StartsWith(canonicalDataRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        if (!File.Exists(artifactAbs))
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(artifactAbs, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Honor the "never throws" contract: a locked/permission-denied artifact reads as
            // unresolved so the startup backfill skips it instead of crashing the host.
            return null;
        }
    }
}
