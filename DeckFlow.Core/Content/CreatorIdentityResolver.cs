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
        var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return Resolve(snapshot, anyRepresentation);
    }

    public async Task<IReadOnlyDictionary<string, CreatorIdentity?>> ResolveManyAsync(
        IReadOnlyCollection<string> representations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(representations);
        var snapshot = await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var resolved = new Dictionary<string, CreatorIdentity?>(StringComparer.OrdinalIgnoreCase);
        foreach (var representation in representations.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            resolved[representation] = Resolve(snapshot, representation);
        }

        return resolved;
    }

    private async Task<Snapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var suppressions = await _suppressionStore.ListAsync(cancellationToken).ConfigureAwait(false);
        var sources = await _sourceStore.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
        var indexRows = await _indexStore.ListCreatorIdentityRowsAsync(cancellationToken).ConfigureAwait(false);
        var components = BuildIndexComponents(indexRows).ToList();
        foreach (var source in sources)
        {
            var matches = components.Where(component => component.Values.Any(value => Same(value, source.SourceSlug) || Same(value, source.DisplayName))).ToList();
            var component = matches.FirstOrDefault() ?? new IndexComponent();
            foreach (var other in matches.Skip(1))
            {
                component.Merge(other);
                components.Remove(other);
            }
            if (!components.Contains(component)) components.Add(component);
            component.SourceIds.Add(source.Id);
            component.SourceSlugs.Add(source.SourceSlug);
            component.DisplayNames.Add(source.DisplayName);
        }

        foreach (var suppression in suppressions)
        {
            foreach (var component in components.Where(component => component.Values.Any(value => SuppressionMatches(suppression, value))))
            {
                AddSuppressionValues(component, suppression);
            }
        }

        return new Snapshot(suppressions, components);
    }

    private static CreatorIdentity? Resolve(Snapshot snapshot, string anyRepresentation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anyRepresentation);
        var match = Normalize(anyRepresentation);
        var suppressions = snapshot.Suppressions;
        var components = snapshot.Components;

        var matchingComponents = components.Where(component => component.Values.Any(value => Same(value, match)) || component.SourceIds.Any(id => string.Equals(id.ToString(System.Globalization.CultureInfo.InvariantCulture), match, StringComparison.Ordinal))).ToList();
        if (matchingComponents.Count == 0)
        {
            var suppression = suppressions.Where(row => Same(row.Slug, match) || row.Aliases.Any(alias => Same(alias, match))).ToList();
            if (suppression.Count == 0) return null;
            if (suppression.Select(row => row.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1) throw new CreatorAliasConflictException(anyRepresentation, string.Join(", ", suppression.Select(row => row.Slug)));
            var row = suppression[0];
            return new CreatorIdentity(row.Slug, [], DistinctOrdinalIgnoringCase(row.Aliases.Where(alias => !LooksLikeSlug(alias))), DistinctOrdinalIgnoringCase(row.Aliases.Where(LooksLikeSlug)));
        }

        var relevantSuppressions = suppressions.Where(row => matchingComponents.Any(component => component.Values.Any(value => Same(row.Slug, value) || row.Aliases.Any(alias => Same(alias, value))))).ToList();
        foreach (var component in components.Where(component => relevantSuppressions.Any(row => component.Values.Any(value => Same(row.Slug, value) || row.Aliases.Any(alias => Same(alias, value))))))
        {
            if (!matchingComponents.Contains(component)) matchingComponents.Add(component);
        }

        var candidates = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in matchingComponents)
        {
            var canonical = FindCanonical(suppressions, component.Values)
                ?? FindSourceCanonical(component.SourceSlugs)
                ?? component.FolderSlugs.Order(StringComparer.Ordinal).First();
            var candidate = Get(candidates, canonical);
            candidate.Aliases.UnionWith(component.Values);
            candidate.DisplayNames.UnionWith(component.DisplayNames);
            candidate.FolderSlugs.UnionWith(component.FolderSlugs);
            candidate.SourceIds.UnionWith(component.SourceIds);
        }

        if (candidates.Count > 1) throw new CreatorAliasConflictException(anyRepresentation, string.Join(", ", candidates.Keys));
        var identity = candidates.Values.Single();
        return new CreatorIdentity(identity.Slug, identity.SourceIds.Order().ToList(), DistinctOrdinalIgnoringCase(identity.DisplayNames), DistinctOrdinalIgnoringCase(identity.FolderSlugs));
    }

    private sealed record Snapshot(IReadOnlyList<CreatorSuppression> Suppressions, IReadOnlyList<IndexComponent> Components);

    private static string? FindCanonical(IEnumerable<CreatorSuppression> suppressions, IEnumerable<string> values)
        => FindSingleCanonical(suppressions.Where(row => values.Any(value => Same(row.Slug, value) || row.Aliases.Any(alias => Same(alias, value)))).Select(row => row.Slug), values);

    private static bool SuppressionMatches(CreatorSuppression suppression, string value)
        => Same(suppression.Slug, value) || suppression.Aliases.Any(alias => Same(alias, value));

    private static void AddSuppressionValues(IndexComponent component, CreatorSuppression suppression)
    {
        foreach (var value in suppression.Aliases.Prepend(suppression.Slug))
        {
            if (LooksLikeSlug(value)) component.FolderSlugs.Add(value);
            else component.DisplayNames.Add(value);
        }
    }

    private static string? FindSourceCanonical(IEnumerable<string> sourceSlugs)
    {
        var canonicalSlugs = sourceSlugs.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
        // Why: ordinal selection makes multi-channel creators deterministic while preserving salubrious-snail.
        return canonicalSlugs.FirstOrDefault();
    }

    private static string? FindSingleCanonical(IEnumerable<string> matches, IEnumerable<string> values)
    {
        var canonicalSlugs = matches.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
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
    private static string? GetFolder(string artifactPath) => CreatorArtifactPathParser.GetFolder(artifactPath);
    private static bool LooksLikeSlug(string value) => value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '-');
    private static bool Same(string left, string right) => string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    private static IReadOnlyList<string> DistinctOrdinalIgnoringCase(IEnumerable<string> values) => values.GroupBy(Normalize).Select(group => group.Order(StringComparer.Ordinal).First()).Order(StringComparer.Ordinal).ToList();
    private sealed class IndexComponent
    {
        public HashSet<string> DisplayNames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> FolderSlugs { get; } = new(StringComparer.Ordinal);
        public HashSet<string> SourceSlugs { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<long> SourceIds { get; } = [];
        public IEnumerable<string> Values => DisplayNames.Concat(FolderSlugs).Concat(SourceSlugs);
        public void Merge(IndexComponent other) { DisplayNames.UnionWith(other.DisplayNames); FolderSlugs.UnionWith(other.FolderSlugs); SourceSlugs.UnionWith(other.SourceSlugs); SourceIds.UnionWith(other.SourceIds); }
    }
    private sealed class Candidate(string slug) { public string Slug { get; } = slug; public HashSet<long> SourceIds { get; } = []; public HashSet<string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase); public HashSet<string> DisplayNames { get; } = new(StringComparer.OrdinalIgnoreCase); public HashSet<string> FolderSlugs { get; } = new(StringComparer.OrdinalIgnoreCase); }
}
#pragma warning restore CS1591
