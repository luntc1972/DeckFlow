namespace DeckFlow.Core.Content;

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
            var canonical = FindCanonical(suppressions, source.SourceSlug, source.DisplayName) ?? source.SourceSlug;
            var candidate = Get(candidates, canonical);
            candidate.SourceIds.Add(source.Id);
            candidate.Aliases.Add(source.SourceSlug);
            candidate.DisplayNames.Add(source.DisplayName);
        }
        foreach (var row in indexRows)
        {
            var folder = GetFolder(row.ArtifactPath);
            if (folder is null) continue;
            var canonical = FindCanonical(suppressions, row.Source, folder) ?? sources.FirstOrDefault(source => Same(source.DisplayName, row.Source) || Same(source.SourceSlug, folder))?.SourceSlug ?? folder;
            var candidate = Get(candidates, canonical);
            candidate.Aliases.Add(row.Source);
            candidate.Aliases.Add(folder);
            candidate.DisplayNames.Add(row.Source);
            candidate.FolderSlugs.Add(folder);
        }

        var matched = candidates.Values.Where(candidate => Same(candidate.Slug, match) || candidate.SourceIds.Any(id => string.Equals(id.ToString(System.Globalization.CultureInfo.InvariantCulture), match, StringComparison.Ordinal)) || candidate.Aliases.Any(alias => Same(alias, match)) || candidate.DisplayNames.Any(name => Same(name, match)) || candidate.FolderSlugs.Any(folder => Same(folder, match))).ToList();
        if (matched.Count == 0) return null;
        if (matched.Count > 1) throw new CreatorAliasConflictException(anyRepresentation, string.Join(", ", matched.Select(candidate => candidate.Slug)));
        var identity = matched[0];
        return new CreatorIdentity(identity.Slug, identity.SourceIds.Order().ToList(), identity.DisplayNames.Order(StringComparer.OrdinalIgnoreCase).ToList(), identity.FolderSlugs.Order(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static string? FindCanonical(IEnumerable<CreatorSuppression> suppressions, params string[] values)
        => suppressions.Where(row => values.Any(value => Same(row.Slug, value) || row.Aliases.Any(alias => Same(alias, value)))).Select(row => row.Slug).Distinct(StringComparer.OrdinalIgnoreCase).SingleOrDefault();
    private static Candidate Get(IDictionary<string, Candidate> candidates, string slug) => candidates.TryGetValue(slug, out var candidate) ? candidate : candidates[slug] = new Candidate(slug);
    private static string? GetFolder(string artifactPath) { var parts = artifactPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries); return parts.Length > 1 ? parts[^2] : null; }
    private static bool LooksLikeSlug(string value) => value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '-');
    private static bool Same(string left, string right) => string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    private sealed class Candidate(string slug) { public string Slug { get; } = slug; public HashSet<long> SourceIds { get; } = []; public HashSet<string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase); public HashSet<string> DisplayNames { get; } = new(StringComparer.OrdinalIgnoreCase); public HashSet<string> FolderSlugs { get; } = new(StringComparer.OrdinalIgnoreCase); }
}
#pragma warning restore CS1591
