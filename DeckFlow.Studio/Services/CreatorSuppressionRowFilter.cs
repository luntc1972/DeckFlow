using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Studio.Services;

/// <summary>Filters Studio workflow rows whose creator is locally suppressed.</summary>
public sealed class CreatorSuppressionRowFilter
{
    private readonly CreatorSuppressionFilter _filter;

    /// <summary>Creates the filter using the local suppression store and identity resolver.</summary>
    public CreatorSuppressionRowFilter(ICreatorSuppressionStore store, ICreatorIdentityResolver resolver)
    {
        _filter = new CreatorSuppressionFilter(store, new Lazy<ICreatorIdentityResolver>(() => resolver));
    }

    /// <summary>Returns rows that have no suppressed creator representation.</summary>
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetAllowedAsync(IReadOnlyList<ContentSiteIndexRow> rows, CancellationToken cancellationToken)
        => await _filter.FilterRowsAsync(rows, cancellationToken).ConfigureAwait(false);

    /// <summary>Determines whether any source or artifact-folder representation is suppressed.</summary>
    public async Task<bool> IsSuppressedAsync(ContentSiteIndexRow row, CancellationToken cancellationToken)
        => !(await _filter.FilterRowsAsync([row], cancellationToken).ConfigureAwait(false)).Any();
}
