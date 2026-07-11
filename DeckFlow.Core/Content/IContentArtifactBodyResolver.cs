namespace DeckFlow.Core.Content;

/// <summary>
/// Host-supplied seam that resolves a site-index row's <c>ArtifactPath</c> to the raw on-disk
/// artifact text (including YAML front matter), so the host-agnostic
/// <see cref="ContentBodyHashBackfill"/> service never needs to know how each host lays out its
/// artifact root. The Web host adapts <c>ContentKbArtifactPathResolver</c> (git root + optional
/// <c>/data</c> overlay); the Studio host adapts its local <c>content-kb</c> artifact directory.
/// </summary>
public interface IContentArtifactBodyResolver
{
    /// <summary>
    /// Attempts to read the raw artifact text for a stored site-index row's <c>ArtifactPath</c>.
    /// </summary>
    /// <param name="artifactPath">Stored relative artifact path (e.g. <c>content-kb/{source}/{id}.md</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw artifact text, including front matter, when resolvable; otherwise <see langword="null"/>.</returns>
    Task<string?> TryReadArtifactTextAsync(string artifactPath, CancellationToken cancellationToken = default);
}
