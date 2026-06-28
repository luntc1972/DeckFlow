using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Orchestration for the Review Queue workflow, extracted from the <c>Review</c> page code-behind
/// (H1 god-component split). Owns the queue load (schema-ensure + read), the single and batch
/// approval-status writes, and the security-sensitive artifact path resolution + read. This type
/// performs no rendering and holds no per-page UI state — the page keeps all tab/selection state,
/// the expand cache, busy guards, and <c>StateHasChanged</c>. Behavior is identical to the prior
/// inline implementation.
/// </summary>
public sealed class ReviewCoordinator
{
    private readonly IContentSiteIndexStore _indexStore;
    private readonly ContentKbOrchestratorOptions _options;

    /// <summary>Creates the coordinator with the content-site-index store and orchestrator options.</summary>
    public ReviewCoordinator(IContentSiteIndexStore indexStore, ContentKbOrchestratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(indexStore);
        ArgumentNullException.ThrowIfNull(options);
        _indexStore = indexStore;
        _options = options;
    }

    /// <summary>Ensures the content-kb schema exists, then loads every content-site-index row.</summary>
    public async Task<IReadOnlyList<ContentSiteIndexRow>> LoadRowsAsync(CancellationToken cancellationToken)
    {
        await _indexStore.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        return await _indexStore.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Persists a single row's approval status by natural key.</summary>
    public Task SetApprovalStatusAsync(string keyType, string keyValue, string status, CancellationToken cancellationToken)
        => _indexStore.SetApprovalStatusAsync(keyType, keyValue, status, cancellationToken);

    /// <summary>Persists the approval status for a batch of rows by natural key.</summary>
    public Task SetApprovalStatusAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        string status,
        CancellationToken cancellationToken)
        => _indexStore.SetApprovalStatusAsync(keys, status, cancellationToken);

    /// <summary>
    /// Resolves the absolute artifact path from the stored relative <paramref name="artifactPath"/>
    /// and reads the file content. Returns <see langword="null"/> on any IO failure, containment
    /// rejection, or invalid path — never throws.
    /// <br/>
    /// Why: stored ArtifactPath already carries the content-kb/ prefix; resolve under the data root
    /// (parent of ArtifactRoot) so the segment isn't doubled — combining with ArtifactRoot directly
    /// resolves every file MISSING. Containment guard rejects rooted/".." paths.
    /// </summary>
    public string? ReadArtifactSafe(string artifactPath)
    {
        try
        {
            // Reject rooted paths and paths that contain ".." traversal segments.
            if (Path.IsPathRooted(artifactPath))
            {
                return null;
            }

            var normalizedPath = artifactPath.Replace('\\', '/');
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(s => s == ".."))
            {
                return null;
            }

            // Resolve against the DATA ROOT = parent of ArtifactRoot.
            // ArtifactRoot = {studioDataDir}/content-kb; dataRoot = {studioDataDir}.
            // The stored ArtifactPath already begins with "content-kb/", so combining with
            // the data root yields {studioDataDir}/content-kb/{sourceSlug}/{id}.md — correct.
            var dataRoot = Directory.GetParent(_options.ArtifactRoot)?.FullName ?? _options.ArtifactRoot;
            var artifactAbs = Path.GetFullPath(Path.Combine(dataRoot, artifactPath));

            // CONTAINMENT GUARD: the normalized absolute path must start with the canonical
            // data root followed by the directory separator to prevent escape to parent directories.
            var canonicalDataRoot = Path.GetFullPath(dataRoot);
            if (!artifactAbs.StartsWith(canonicalDataRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return null;
            }

            return File.ReadAllText(artifactAbs);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
