using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>Filters creators against one fresh suppression snapshot per operation.</summary>
public sealed class CreatorSuppressionFilter : ICreatorSuppressionFilter
{
    internal static Lazy<ICreatorIdentityResolver> EmptyResolver { get; } = new(() => new EmptyCreatorIdentityResolver());

    private readonly ICreatorSuppressionStore _store;
    private readonly Lazy<ICreatorIdentityResolver> _resolver;

    /// <summary>Creates a creator suppression filter.</summary>
    public CreatorSuppressionFilter(ICreatorSuppressionStore store, Lazy<ICreatorIdentityResolver> resolver)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);
        _store = store;
        _resolver = resolver;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSiteIndexRow>> FilterRowsAsync(IReadOnlyList<ContentSiteIndexRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0) return rows;
        var representations = rows.SelectMany(GetRepresentations).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var resolved = await _resolver.Value.ResolveManyAsync(representations, cancellationToken).ConfigureAwait(false);
        var matcher = new CreatorSuppressionMatcher(await _store.ListAsync(cancellationToken).ConfigureAwait(false));
        var suppressed = representations.Where(representation => IsSuppressed(representation, resolved.GetValueOrDefault(representation), matcher)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return rows.Where(row => GetRepresentations(row).All(representation => !suppressed.Contains(representation))).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> IsSuppressedAsync(string slug, CancellationToken cancellationToken = default)
    {
        var resolved = await _resolver.Value.ResolveManyAsync([slug], cancellationToken).ConfigureAwait(false);
        var matcher = new CreatorSuppressionMatcher(await _store.ListAsync(cancellationToken).ConfigureAwait(false));
        return IsSuppressed(slug, resolved.GetValueOrDefault(slug), matcher);
    }

    private static bool IsSuppressed(string representation, CreatorIdentity? identity, CreatorSuppressionMatcher matcher)
        => GetIdentityRepresentations(identity).Prepend(representation).Any(matcher.IsSuppressed);

    private static IEnumerable<string> GetRepresentations(ContentSiteIndexRow row)
    {
        yield return row.Source;
        var folder = CreatorArtifactPathParser.GetFolder(row.ArtifactPath);
        if (!string.IsNullOrWhiteSpace(folder)) yield return folder;
    }

    private static IEnumerable<string> GetIdentityRepresentations(CreatorIdentity? identity)
        => identity is null ? [] : [identity.CanonicalSlug, .. identity.DisplayNames, .. identity.FolderSlugs];

    private sealed class EmptyCreatorIdentityResolver : ICreatorIdentityResolver
    {
        public Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default)
            => Task.FromResult<CreatorIdentity?>(null);

        public Task<IReadOnlyDictionary<string, CreatorIdentity?>> ResolveManyAsync(IReadOnlyCollection<string> representations, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, CreatorIdentity?>>(representations.ToDictionary(representation => representation, _ => (CreatorIdentity?)null, StringComparer.OrdinalIgnoreCase));
    }
}
