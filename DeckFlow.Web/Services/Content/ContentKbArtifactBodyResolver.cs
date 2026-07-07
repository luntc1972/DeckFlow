using DeckFlow.Core.Content;

namespace DeckFlow.Web.Services;

/// <summary>
/// Web adapter over <see cref="ContentKbArtifactPathResolver"/> for the host-agnostic
/// <see cref="ContentBodyHashBackfill"/> service (D-08). Resolves a row's <c>ArtifactPath</c>
/// against the git root / optional <c>/data</c> overlay and reads the raw artifact text; returns
/// <see langword="null"/> on <see cref="ContentKbArtifactResolution.MissingFile"/> or
/// <see cref="ContentKbArtifactResolution.InvalidPath"/> — never throws.
/// </summary>
public sealed class ContentKbArtifactBodyResolver : IContentArtifactBodyResolver
{
    private readonly ContentKbArtifactPathResolver _pathResolver;

    /// <summary>
    /// Creates a new Web artifact-body resolver.
    /// </summary>
    /// <param name="pathResolver">Underlying artifact path resolver.</param>
    public ContentKbArtifactBodyResolver(ContentKbArtifactPathResolver pathResolver)
    {
        ArgumentNullException.ThrowIfNull(pathResolver);

        _pathResolver = pathResolver;
    }

    /// <inheritdoc />
    public async Task<string?> TryReadArtifactTextAsync(string artifactPath, CancellationToken cancellationToken = default)
    {
        var resolution = _pathResolver.TryResolveExistingArtifact(artifactPath, out var resolvedFullPath);
        if (resolution != ContentKbArtifactResolution.Resolved)
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(resolvedFullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Honor the "never throws" contract: a locked/permission-denied artifact reads as
            // unresolved so the caller (startup backfill) skips it instead of crashing.
            return null;
        }
    }
}
