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
        => ReadRelativeSafe(artifactPath);

    /// <summary>
    /// Resolves the paste-ready AI prompt for a review row: reads the baked sibling
    /// <c>{id}.prompt.md</c> when present, otherwise reconstructs the prompt from the notes body so
    /// pre-bake entries still show a framed prompt. Returns <see langword="null"/> when the notes
    /// artifact itself is unavailable. Uses the same containment-guarded read as the notes preview.
    /// </summary>
    /// <param name="artifactPath">Stored relative notes artifact path (<c>content-kb/{slug}/{id}.md</c>).</param>
    /// <param name="title">Row title for grounding a reconstructed prompt.</param>
    /// <param name="source">Row source/creator name for grounding a reconstructed prompt.</param>
    /// <param name="videoUrl">Row video URL for provenance in a reconstructed prompt.</param>
    /// <returns>The paste-ready prompt, or <see langword="null"/> when the notes are unavailable.</returns>
    public string? ReadPromptSafe(string artifactPath, string title, string source, string videoUrl)
    {
        var notes = ReadRelativeSafe(artifactPath);
        var promptPath = ContentKbPromptResolver.PromptPathFor(artifactPath);
        var sibling = promptPath is null ? null : ReadRelativeSafe(promptPath);
        return ContentKbPromptResolver.BuildOrReconstruct(sibling, notes, title, source, videoUrl);
    }

    // Containment-guarded read of a stored content-kb-relative path under the Studio data root.
    // Returns null on any rooted/".." path, containment rejection, or IO failure — never throws.
    private string? ReadRelativeSafe(string relativePath)
    {
        try
        {
            // Reject rooted paths and paths that contain ".." traversal segments.
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            {
                return null;
            }

            var normalizedPath = relativePath.Replace('\\', '/');
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(s => s == ".."))
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
            var artifactAbs = Path.GetFullPath(Path.Combine(dataRoot, relativePath));

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
