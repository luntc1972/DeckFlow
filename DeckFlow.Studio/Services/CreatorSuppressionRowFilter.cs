using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Studio.Services;

/// <summary>Filters Studio workflow rows whose creator is locally suppressed.</summary>
public sealed class CreatorSuppressionRowFilter
{
    private readonly ICreatorSuppressionStore _store;
    private readonly ICreatorIdentityResolver _resolver;

    /// <summary>Creates the filter using the local suppression store and identity resolver.</summary>
    public CreatorSuppressionRowFilter(ICreatorSuppressionStore store, ICreatorIdentityResolver resolver)
    {
        _store = store;
        _resolver = resolver;
    }

    /// <summary>Returns rows that have no suppressed creator representation.</summary>
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetAllowedAsync(IReadOnlyList<ContentSiteIndexRow> rows, CancellationToken cancellationToken)
    {
        var allowed = new List<ContentSiteIndexRow>();
        foreach (var row in rows)
        {
            if (!await IsSuppressedAsync(row, cancellationToken).ConfigureAwait(false)) allowed.Add(row);
        }

        return allowed;
    }

    /// <summary>Determines whether any source or artifact-folder representation is suppressed.</summary>
    public async Task<bool> IsSuppressedAsync(ContentSiteIndexRow row, CancellationToken cancellationToken)
    {
        foreach (var representation in GetRepresentations(row))
        {
            var identity = await _resolver.ResolveAsync(representation, cancellationToken).ConfigureAwait(false);
            var candidates = new[] { representation, identity?.CanonicalSlug }
                .Concat(identity?.DisplayNames ?? Array.Empty<string>())
                .Concat(identity?.FolderSlugs ?? Array.Empty<string>())
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            if ((await Task.WhenAll(candidates.Select(candidate => _store.IsSuppressedAsync(candidate!, cancellationToken))).ConfigureAwait(false)).Any(value => value)) return true;
        }

        return false;
    }

    private static IEnumerable<string> GetRepresentations(ContentSiteIndexRow row)
    {
        yield return row.Source;
        var segments = row.ArtifactPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 1) yield return segments[^2];
    }
}
