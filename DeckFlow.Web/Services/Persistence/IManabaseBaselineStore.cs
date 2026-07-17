using DeckFlow.Core.Manabase;

namespace DeckFlow.Web.Services;

/// <summary>
/// Persists and reads confidence-weighting baseline cells for the manabase feature.
/// </summary>
public interface IManabaseBaselineStore
{
    /// <summary>Inserts or updates one baseline cell (PK = commander_slug + bracket + source).</summary>
    /// <param name="row">The cell to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(ManabaseBaselineRow row, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates many baseline cells in a single transaction.</summary>
    /// <param name="rows">The cells to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertRangeAsync(IReadOnlyCollection<ManabaseBaselineRow> rows, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every stored source row (e.g. corpus and/or edhrec) for the given commander at the
    /// given bracket. Pass <see cref="ManabaseBaselineSources.GlobalCommanderSlug"/> for the global row.
    /// </summary>
    /// <param name="commanderSlug">Canonical commander key, or <c>*</c> for the global row.</param>
    /// <param name="bracket">Power bracket 1-5.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ManabaseBaselineRow>> GetAsync(string commanderSlug, int bracket, CancellationToken cancellationToken = default);
}
