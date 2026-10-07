using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>Filters creator content using the current suppression snapshot.</summary>
public interface ICreatorSuppressionFilter
{
    /// <summary>Returns rows whose creator representations are not suppressed.</summary>
    Task<IReadOnlyList<ContentSiteIndexRow>> FilterRowsAsync(IReadOnlyList<ContentSiteIndexRow> rows, CancellationToken cancellationToken = default);

    /// <summary>Returns whether a slug is suppressed.</summary>
    Task<bool> IsSuppressedAsync(string slug, CancellationToken cancellationToken = default);
}
