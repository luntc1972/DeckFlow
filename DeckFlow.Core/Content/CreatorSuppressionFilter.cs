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
        var rawRepresentations = rows.SelectMany(GetRepresentations).ToList();
        var representations = rawRepresentations.Distinct(CreatorSuppressionMatcher.ValueComparer).ToList();
        var resolved = await _resolver.Value.ResolveManyAsync(representations, cancellationToken).ConfigureAwait(false);
        var matcher = new CreatorSuppressionMatcher(await _store.ListAsync(cancellationToken).ConfigureAwait(false));
        // Why: build the OIC set from raw values so a ValueComparer-collapsed value still widens.
        var suppressedClasses = representations
            .Where(representation => IsSuppressed(representation, resolved.GetValueOrDefault(representation), matcher))
            .ToHashSet(CreatorSuppressionMatcher.ValueComparer);
        var suppressed = SuppressedRepresentations.Create(rawRepresentations.Where(suppressedClasses.Contains));
        return rows.Where(row => GetRepresentations(row).All(representation => !suppressed.Contains(representation))).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> IsSuppressedAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var resolved = await _resolver.Value.ResolveManyAsync([slug], cancellationToken).ConfigureAwait(false);
        var matcher = new CreatorSuppressionMatcher(await _store.ListAsync(cancellationToken).ConfigureAwait(false));
        return IsSuppressed(slug, resolved.GetValueOrDefault(slug), matcher);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUnsuppressedAsync(IReadOnlyList<string> slugs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slugs);
        foreach (var slug in slugs) ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        if (slugs.Count == 0) return slugs;
        var representations = slugs.Distinct(CreatorSuppressionMatcher.ValueComparer).ToList();
        var resolved = await _resolver.Value.ResolveManyAsync(representations, cancellationToken).ConfigureAwait(false);
        var matcher = new CreatorSuppressionMatcher(await _store.ListAsync(cancellationToken).ConfigureAwait(false));
        return slugs.Where(slug => !IsSuppressed(slug, resolved.GetValueOrDefault(slug), matcher)).ToList();
    }

    private static bool IsSuppressed(string representation, CreatorIdentity? identity, CreatorSuppressionMatcher matcher)
        => ExpandIdentityRepresentations(identity).Prepend(representation).Any(matcher.IsSuppressed);

    private static IEnumerable<string> GetRepresentations(ContentSiteIndexRow row)
    {
        yield return row.Source;
        var folder = CreatorArtifactPathParser.GetFolder(row.ArtifactPath);
        if (!string.IsNullOrWhiteSpace(folder)) yield return folder;
    }

    private static IEnumerable<string> ExpandIdentityRepresentations(CreatorIdentity? identity)
        => identity is null ? [] : [identity.CanonicalSlug, .. identity.DisplayNames, .. identity.FolderSlugs];

    private sealed class SuppressedRepresentations
    {
        private readonly HashSet<string> _valueComparer;
        private readonly HashSet<string> _ordinalIgnoreCase;

        private SuppressedRepresentations(IEnumerable<string> representations)
        {
            var values = representations.ToList();
            _valueComparer = values.ToHashSet(CreatorSuppressionMatcher.ValueComparer);
            _ordinalIgnoreCase = values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public static SuppressedRepresentations Create(IEnumerable<string> representations) => new(representations);

        public bool Contains(string representation)
            => _valueComparer.Contains(representation) || _ordinalIgnoreCase.Contains(representation);
    }

    private sealed class EmptyCreatorIdentityResolver : ICreatorIdentityResolver
    {
        public Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default)
            => Task.FromResult<CreatorIdentity?>(null);

        public Task<IReadOnlyDictionary<string, CreatorIdentity?>> ResolveManyAsync(IReadOnlyCollection<string> representations, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, CreatorIdentity?>>(representations.ToDictionary(representation => representation, _ => (CreatorIdentity?)null, CreatorSuppressionMatcher.ValueComparer));
    }
}
