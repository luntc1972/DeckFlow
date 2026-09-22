namespace DeckFlow.Core.Content;

using DeckFlow.Core.Knowledge;

#pragma warning disable CS1591

/// <summary>Builds creator identities from suppression aliases, source rows, and artifact folders.</summary>
public sealed class CreatorIdentityResolver : ICreatorIdentityResolver
{
    private readonly ICreatorSuppressionStore _suppressionStore;
    private readonly IContentSourceStore _sourceStore;
    private readonly IContentSiteIndexStore _indexStore;

    public CreatorIdentityResolver(ICreatorSuppressionStore suppressionStore, IContentSourceStore sourceStore, IContentSiteIndexStore indexStore)
    {
        _suppressionStore = suppressionStore ?? throw new ArgumentNullException(nameof(suppressionStore));
        _sourceStore = sourceStore ?? throw new ArgumentNullException(nameof(sourceStore));
        _indexStore = indexStore ?? throw new ArgumentNullException(nameof(indexStore));
    }

    public async Task<CreatorIdentity?> ResolveAsync(string anyRepresentation, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anyRepresentation);
        var suppressions = await _suppressionStore.ListAsync(cancellationToken).ConfigureAwait(false);
        var sources = await _sourceStore.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
        var indexRows = await _indexStore.ListCreatorIdentityRowsAsync(cancellationToken).ConfigureAwait(false);
        var match = Normalize(anyRepresentation);
        var candidates = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var suppression in suppressions)
        {
            var candidate = Get(candidates, suppression.Slug);
            candidate.Aliases.UnionWith(suppression.Aliases);
            candidate.DisplayNames.UnionWith(suppression.Aliases.Where(alias => !LooksLikeSlug(alias)));
            candidate.FolderSlugs.UnionWith(suppression.Aliases.Where(LooksLikeSlug));
        }
        foreach (var source in sources)
        {
            var canonical = FindCanonical(suppressions, new[] { source.SourceSlug, source.DisplayName }) ?? source.SourceSlug;
            var candidate = Get(candidates, canonical);
            candidate.SourceIds.Add(source.Id);
            candidate.Aliases.Add(source.SourceSlug);
            candidate.DisplayNames.Add(source.DisplayName);
        }
        foreach (var component in BuildIndexComponents(indexRows))
        {
            var canonical = FindCanonical(suppressions, component.Values)
                ?? FindSourceCanonical(sources, component.Values)
                ?? component.FolderSlugs.Order(StringComparer.Ordinal).First();
            var candidate = Get(candidates, canonical);
            candidate.Aliases.UnionWith(component.Values);
            candidate.DisplayNames.UnionWith(component.DisplayNames);
            candidate.FolderSlugs.UnionWith(component.FolderSlugs);
        }

        var matched = candidates.Values.Where(candidate => Same(candidate.Slug, match) || candidate.SourceIds.Any(id => string.Equals(id.ToString(System.Globalization.CultureInfo.InvariantCulture), match, StringComparison.Ordinal)) || candidate.Aliases.Any(alias => Same(alias, match)) || candidate.DisplayNames.Any(name => Same(name, match)) || candidate.FolderSlugs.Any(folder => Same(folder, match))).ToList();
        if (matched.Count == 0) return null;
        if (matched.Count > 1) throw new CreatorAliasConflictException(anyRepresentation, string.Join(", ", matched.Select(candidate => candidate.Slug)));
        var identity = matched[0];
        return new CreatorIdentity(identity.Slug, identity.SourceIds.Order().ToList(), identity.DisplayNames.Order(StringComparer.OrdinalIgnoreCase).ToList(), identity.FolderSlugs.Order(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static string? FindCanonical(IEnumerable<CreatorSuppression> suppressions, IEnumerable<string> values)
        => FindSingleCanonical(suppressions.Where(row => values.Any(value => Same(row.Slug, value) || row.Aliases.Any(alias => Same(alias, value)))).Select(row => row.Slug), values);

    private static string? FindSourceCanonical(IEnumerable<ContentSource> sources, IEnumerable<string> values)
        => FindSingleCanonical(sources.Where(source => values.Any(value => Same(source.SourceSlug, value))).Select(source => source.SourceSlug), values);

    private static string? FindSingleCanonical(IEnumerable<string> matches, IEnumerable<string> values)
    {
        var canonicalSlugs = matches.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (canonicalSlugs.Count > 1) throw new CreatorAliasConflictException(string.Join(", ", values), string.Join(", ", canonicalSlugs));
        return canonicalSlugs.SingleOrDefault();
    }

    private static IReadOnlyList<IndexComponent> BuildIndexComponents(IEnumerable<ContentCreatorIdentityRow> rows)
    {
        var nodes = new Dictionary<string, IndexComponent>(StringComparer.OrdinalIgnoreCase);
        var components = new List<IndexComponent>();
        foreach (var row in rows)
        {
            var folder = GetFolder(row.ArtifactPath);
            if (folder is null) continue;
            var sourceNode = Normalize(row.Source);
            var folderNode = Normalize(folder);
            nodes.TryGetValue(sourceNode, out var sourceComponent);
            nodes.TryGetValue(folderNode, out var folderComponent);
            var component = sourceComponent ?? folderComponent ?? new IndexComponent();
            if (sourceComponent is not null && folderComponent is not null && sourceComponent != folderComponent)
            {
                component = sourceComponent;
                component.Merge(folderComponent);
                foreach (var value in folderComponent.Values) nodes[Normalize(value)] = component;
                components.Remove(folderComponent);
            }
            if (!components.Contains(component)) components.Add(component);
            component.DisplayNames.Add(row.Source);
            component.FolderSlugs.Add(folder);
            nodes[sourceNode] = component;
            nodes[folderNode] = component;
        }

        return components;
    }
    private static Candidate Get(IDictionary<string, Candidate> candidates, string slug) => candidates.TryGetValue(slug, out var candidate) ? candidate : candidates[slug] = new Candidate(slug);
    private static string? GetFolder(string artifactPath) { var parts = artifactPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries); return parts.Length > 1 ? parts[^2] : null; }
    private static bool LooksLikeSlug(string value) => value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '-');
    private static bool Same(string left, string right) => string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    private sealed class IndexComponent
    {
        public HashSet<string> DisplayNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> FolderSlugs { get; } = new(StringComparer.OrdinalIgnoreCase);
        public IEnumerable<string> Values => DisplayNames.Concat(FolderSlugs);
        public void Merge(IndexComponent other) { DisplayNames.UnionWith(other.DisplayNames); FolderSlugs.UnionWith(other.FolderSlugs); }
    }
    private sealed class Candidate(string slug) { public string Slug { get; } = slug; public HashSet<long> SourceIds { get; } = []; public HashSet<string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase); public HashSet<string> DisplayNames { get; } = new(StringComparer.OrdinalIgnoreCase); public HashSet<string> FolderSlugs { get; } = new(StringComparer.OrdinalIgnoreCase); }
}
#pragma warning restore CS1591
