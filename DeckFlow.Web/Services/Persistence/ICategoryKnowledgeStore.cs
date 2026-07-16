using DeckFlow.Core.Reporting;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Services;

/// <summary>
/// Persists and queries card-category observations and processed deck metadata.
/// </summary>
public interface ICategoryKnowledgeStore
{
    /// <summary>
    /// Returns stored category observations for a card, optionally narrowed to one board.
    /// </summary>
    /// <param name="cardName">Card name to query.</param>
    /// <param name="boardFilter">Optional board name used to filter observations.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    /// <returns>The matching category observation rows for the card.</returns>
    Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default);
    /// <summary>Returns all card-category observations from decks led by the specified commander.</summary>
    Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default);
    /// <summary>Returns per-deck card-category memberships from decks led by the specified commander.</summary>
    Task<IReadOnlyList<CategoryDeckMembership>> GetCategoryDeckMembershipForCommanderAsync(string commanderName, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns the number of decks already processed by the category cache.
    /// </summary>
    Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default);
    /// <summary>Returns the count of processed decks led by the specified commander.</summary>
    Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default);
    /// <summary>
    /// Runs a bounded Archidekt cache sweep and persists observed categories.
    /// </summary>
    /// <param name="logger">Logger that receives sweep progress and diagnostics.</param>
    /// <param name="durationSeconds">Maximum sweep duration in seconds.</param>
    /// <param name="cancellationToken">Token used to cancel the sweep.</param>
    /// <param name="progress">Optional progress reporter for processed deck counts.</param>
    /// <returns>The number of decks swept during the run.</returns>
    Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null);
    /// <summary>
    /// Returns cached category names for a card.
    /// </summary>
    /// <param name="cardName">Card name to query.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns cached per-category deck counts for a card, keyed by canonical category label.
    /// </summary>
    /// <param name="cardName">Card name to query.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    Task<IReadOnlyDictionary<string, int>> GetCategoryDeckCountsAsync(string cardName, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns cached category names for many cards in a single round-trip. Prefer this over calling
    /// <see cref="GetCategoriesAsync"/> in a loop: a per-card loop over a full decklist issues one
    /// database query per card, which can exhaust a request timeout on a large deck. The result is
    /// keyed by the original requested name (case-insensitive) and every distinct name gets an entry.
    /// </summary>
    /// <param name="cardNames">Card names to resolve.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesForNamesAsync(IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken = default);
    /// <summary>
    /// Stores category observations discovered during lookup or harvest work.
    /// </summary>
    /// <param name="source">Source that produced the category observations.</param>
    /// <param name="cardName">Card name associated with the observations.</param>
    /// <param name="categories">Category names to persist.</param>
    /// <param name="quantity">Observed card quantity for the deck slot.</param>
    /// <param name="board">Board where the card was observed.</param>
    /// <param name="deckCountIncrement">Amount to add to deck-level processed counts.</param>
    /// <param name="cancellationToken">Token used to cancel persistence.</param>
    Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default);
    /// <summary>
    /// Marks an imported deck as processed for cache accounting.
    /// </summary>
    /// <param name="deckId">External deck identifier to mark processed.</param>
    /// <param name="commanderName">Commander name associated with the processed deck, when known.</param>
    /// <param name="cancellationToken">Token used to cancel the update.</param>
    Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns the total number of processed decks across the category cache.
    /// </summary>
    Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns the number of processed decks inserted at or after a UTC cutoff.
    /// </summary>
    /// <param name="cutoffUtc">UTC cutoff used to filter processed deck rows.</param>
    /// <param name="cancellationToken">Token used to cancel the count query.</param>
    Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns the total number of stored card-category observations.
    /// </summary>
    Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns the commanders with the most processed decks.
    /// </summary>
    /// <param name="n">Maximum number of commander rows to return.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    /// <returns>The top commander deck-count rows, ordered by processed deck count.</returns>
    Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns one page of processed harvested commander aggregates for the admin grid.
    /// </summary>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Maximum number of commander rows to return.</param>
    /// <param name="cancellationToken">Token used to cancel the page query.</param>
    /// <returns>The processed commander aggregate rows for the requested page.</returns>
    Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns the number of distinct commanders with processed decks.
    /// </summary>
    Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns the current Postgres database size when the store is backed by Postgres.
    /// </summary>
    /// <returns>The database size in bytes, or null when the active provider is not Postgres.</returns>
    Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns deck-level totals for a card, optionally narrowed to one board.
    /// </summary>
    /// <param name="cardName">Card name to total.</param>
    /// <param name="boardFilter">Optional board name used to filter deck totals.</param>
    /// <param name="cancellationToken">Token used to cancel the totals query.</param>
    /// <returns>The aggregate deck totals for the requested card.</returns>
    Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default);
}
