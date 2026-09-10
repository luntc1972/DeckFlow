using DeckFlow.Core.Knowledge.StatedRulesExtraction;

namespace DeckFlow.Core.Content;

/// <summary>
/// Persists stated rules extracted from creator content, keyed by creator slug.
/// </summary>
public interface ICreatorStyleStatedRuleStore
{
    /// <summary>
    /// Ensures the stated-rules schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates a stated rule for the given creator slug, keyed by metric and condition.
    /// </summary>
    /// <param name="rule">Stated rule candidate to insert or update.</param>
    /// <param name="slug">Creator slug the rule belongs to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(StatedRuleCandidate rule, string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all stated rules for a creator slug.
    /// </summary>
    /// <param name="slug">Creator slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stated rule candidates for the slug, or an empty list when none exist.</returns>
    Task<IReadOnlyList<StatedRuleCandidate>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
