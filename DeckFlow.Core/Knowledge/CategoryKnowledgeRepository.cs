using Microsoft.Extensions.Logging;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Reads and writes card-category knowledge rows in the SQLite or Postgres knowledge-cache database.
/// </summary>
public sealed class CategoryKnowledgeRepository
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly ILogger? _logger;
    private readonly string? _databasePath;
    private readonly string _directoryPath;
    private readonly CategoryCacheSchema _schema;
    private readonly DeckQueueRepository _deckQueue;
    private readonly CardCategoryRepository _cardCategory;

    /// <summary>
    /// Initializes the repository for the provided SQLite database path.
    /// </summary>
    public CategoryKnowledgeRepository(string databasePath, ILogger? logger = null)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath), logger)
    {
    }

    /// <summary>
    /// Initializes the repository for the provided relational connection information.
    /// </summary>
    /// <param name="connectionInfo">Provider and connection string details for the knowledge database.</param>
    /// <param name="logger">Optional logger for schema and index warnings.</param>
    public CategoryKnowledgeRepository(RelationalDatabaseConnection connectionInfo, ILogger? logger = null)
    {
        _connectionInfo = connectionInfo;
        _logger = logger;
        _databasePath = connectionInfo.IsSqlite
            ? connectionInfo.ExtractSqlitePath()
            : null;
        _directoryPath = _databasePath is null
            ? string.Empty
            : Path.GetDirectoryName(_databasePath) ?? Directory.GetCurrentDirectory();
        _schema = new CategoryCacheSchema(connectionInfo, _directoryPath, logger);
        _deckQueue = new DeckQueueRepository(connectionInfo, _schema);
        _cardCategory = new CardCategoryRepository(connectionInfo, _schema);
    }

    /// <summary>
    /// Gets the SQLite database path when the repository is configured for SQLite storage.
    /// </summary>
    public string? DatabasePath => _databasePath;

    /// <summary>
    /// Ensures the database schema and required tables exist.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
        => _schema.EnsureSchemaAsync(cancellationToken);

    /// <summary>
    /// Retrieves previously observed categories for the specified card.
    /// </summary>
    /// <param name="cardName">Card name to look up.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
        => _cardCategory.GetCategoriesAsync(cardName, cancellationToken);

    /// <summary>
    /// Batch equivalent of <see cref="GetCategoriesAsync"/>: resolves categories for many cards in a
    /// single query. Returns a dictionary keyed by the original requested name (case-insensitive).
    /// </summary>
    /// <param name="cardNames">Card names to resolve.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesForNamesAsync(
        IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken = default)
        => _cardCategory.GetCategoriesForNamesAsync(cardNames, cancellationToken);

    /// <summary>
    /// Retrieves detail rows for a card, including display name and count.
    /// </summary>
    /// <param name="cardName">Card name to query.</param>
    /// <param name="boardFilter">Optional board name to filter the category rows.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCardAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
        => _cardCategory.GetCategoryRowsForCardAsync(cardName, boardFilter, cancellationToken);

    /// <summary>
    /// Returns all card-category observations from decks led by <paramref name="commanderName"/>,
    /// aggregated across every harvested deck that has this commander in the commander zone.
    /// </summary>
    public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
        => _cardCategory.GetCategoryRowsForCommanderAsync(commanderName, cancellationToken);

    /// <summary>
    /// Returns the processed-deck-only global baseline used for category lift calculations.
    /// </summary>
    public Task<GlobalCategoryBaseline> GetGlobalCategoryBaselineAsync(CancellationToken cancellationToken = default)
        => _cardCategory.GetGlobalCategoryBaselineAsync(cancellationToken);

    /// <summary>
    /// Returns per-deck card-category memberships from decks led by <paramref name="commanderName"/>.
    /// </summary>
    public Task<IReadOnlyList<CategoryDeckMembership>> GetCategoryDeckMembershipForCommanderAsync(
        string commanderName,
        CancellationToken cancellationToken = default,
        string? boardFilter = null)
        => _cardCategory.GetCategoryDeckMembershipForCommanderAsync(commanderName, cancellationToken, boardFilter);

    /// <summary>
    /// Returns the count of processed decks in <c>deck_queue</c> that are led by <paramref name="commanderName"/>.
    /// </summary>
    public Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
        => _deckQueue.GetCommanderDeckCountAsync(commanderName, cancellationToken);

    /// <summary>
    /// Returns a paged slice of processed commander aggregates for the harvested-commanders admin grid.
    /// </summary>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Maximum number of rows to return.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public Task<IReadOnlyList<(string CommanderName, int DeckCount, string? LastProcessedUtc)>> GetPagedProcessedCommanderRowsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => _deckQueue.GetPagedProcessedCommanderRowsAsync(page, pageSize, cancellationToken);

    /// <summary>
    /// Counts distinct processed commanders in the deck queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
        => _deckQueue.GetDistinctProcessedCommanderCountAsync(cancellationToken);

    /// <summary>
    /// Replaces all observations for a source with the provided rows.
    /// </summary>
    /// <param name="source">Source label for the data.</param>
    /// <param name="rows">Rows to persist.</param>
    /// <param name="board">Board name applied to each persisted row.</param>
    /// <param name="deckCount">Deck-count total applied to each persisted row.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public Task ReplaceSourceRowsAsync(string source, IReadOnlyList<CategoryKnowledgeRow> rows, string board = "mainboard", int deckCount = 0, CancellationToken cancellationToken = default)
        => _cardCategory.ReplaceSourceRowsAsync(source, rows, board, deckCount, cancellationToken);

    /// <summary>
    /// Removes all cached observation and deck total rows for the provided source.
    /// </summary>
    /// <param name="source">Source label to remove.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public Task DeleteSourceDataAsync(string source, CancellationToken cancellationToken = default)
        => _cardCategory.DeleteSourceDataAsync(source, cancellationToken);

    /// <summary>
    /// Persists observed categories for a specific card occurrence.
    /// </summary>
    /// <param name="source">Data source label.</param>
    /// <param name="cardName">Card name.</param>
    /// <param name="categories">Categories to record.</param>
    /// <param name="quantity">Quantity observed.</param>
    /// <param name="board">Board name containing the observed card.</param>
    /// <param name="deckCountIncrement">Deck-total increment applied to the card totals for this observation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
        => _cardCategory.PersistObservedCategoriesAsync(source, cardName, categories, quantity, board, deckCountIncrement, cancellationToken);

    /// <summary>
    /// Persists the number of decks that contain the given card on the specified board.
    /// </summary>
    public Task PersistCardDeckTotalsAsync(string source, string cardName, string board = "mainboard", int deckCountIncrement = 1, CancellationToken cancellationToken = default)
        => _cardCategory.PersistCardDeckTotalsAsync(source, cardName, board, deckCountIncrement, cancellationToken);

    internal Task PersistDeckCategoryBatchAsync(
        string source,
        IReadOnlyList<(string CardName, string Category, string Board, int Quantity, int DeckCountIncrement)> observations,
        IReadOnlyList<(string CardName, string Board)> cardBoardTotals,
        CancellationToken cancellationToken = default)
        => _cardCategory.PersistDeckCategoryBatchAsync(source, observations, cardBoardTotals, cancellationToken);

    /// <summary>
    /// Retrieves deck totals for the card, optionally filtered by board.
    /// </summary>
    public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
        => _cardCategory.GetCardDeckTotalsAsync(cardName, boardFilter, cancellationToken);

    /// <summary>
    /// Checks whether the repository already contains entries for the source.
    /// </summary>
    /// <param name="source">Source label to check.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public Task<bool> HasSourceDataAsync(string source, CancellationToken cancellationToken = default)
        => _cardCategory.HasSourceDataAsync(source, cancellationToken);

    /// <summary>
    /// Inserts new deck IDs into the queue for processing.
    /// </summary>
    /// <param name="deckIds">Deck IDs to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task AddDeckIdsAsync(IEnumerable<string> deckIds, CancellationToken cancellationToken = default)
        => _deckQueue.AddDeckIdsAsync(deckIds, cancellationToken);

    /// <summary>
    /// Gets the next batch of deck IDs that have not been processed or skipped.
    /// </summary>
    /// <param name="count">Maximum number of deck IDs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<string>> GetNextUnprocessedDeckIdsAsync(int count, CancellationToken cancellationToken = default)
        => _deckQueue.GetNextUnprocessedDeckIdsAsync(count, cancellationToken);

    /// <summary>
    /// Retrieves the total number of unprocessed deck IDs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<int> GetUnprocessedCountAsync(CancellationToken cancellationToken = default)
        => _deckQueue.GetUnprocessedCountAsync(cancellationToken);

    /// <summary>
    /// Counts the number of decks that have been processed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
        => _deckQueue.GetProcessedDeckCountAsync(cancellationToken);

    /// <summary>
    /// Gets the next recent Archidekt search page to crawl after page one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<int> GetRecentDeckCrawlPageAsync(CancellationToken cancellationToken = default)
        => _deckQueue.GetRecentDeckCrawlPageAsync(cancellationToken);

    /// <summary>
    /// Persists the next recent Archidekt search page to crawl.
    /// </summary>
    /// <param name="page">Page number to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task SetRecentDeckCrawlPageAsync(int page, CancellationToken cancellationToken = default)
        => _deckQueue.SetRecentDeckCrawlPageAsync(page, cancellationToken);

    /// <summary>
    /// Marks a single deck as processed and captures its commander identity in the
    /// same UPDATE so the harvest stats panel (Plan 06 top-10 commanders) can read
    /// <c>deck_queue.commander_name</c> directly without joining
    /// <c>card_category_observations</c> (Phase 7 D-17). NULL <paramref name="commanderName"/>
    /// writes SQL NULL — the top-N query already filters <c>commander_name IS NOT NULL</c>.
    /// </summary>
    /// <param name="deckId">Deck ID to update.</param>
    /// <param name="commanderName">Commander card name extracted from the imported deck, or null on skip / unknown.</param>
    /// <param name="skip">Whether the deck should be marked as skipped after failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task MarkDeckProcessedAsync(
        string deckId,
        string? commanderName,
        bool skip = false,
        CancellationToken cancellationToken = default)
        => _deckQueue.MarkDeckProcessedAsync(deckId, commanderName, skip, cancellationToken);

    /// <summary>
    /// Gets the stored canonical content hash for a queued Archidekt deck.
    /// </summary>
    /// <param name="deckId">Deck ID to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<string?> GetContentHashAsync(string deckId, CancellationToken cancellationToken = default)
        => _deckQueue.GetContentHashAsync(deckId, cancellationToken);

    /// <summary>
    /// Gets canonical content hashes for queued Archidekt decks keyed by <c>deck_queue.id</c>.
    /// Missing rows are absent from the returned dictionary; existing rows with no hash map to null.
    /// </summary>
    public Task<IReadOnlyDictionary<long, string?>> GetContentHashesByIdsAsync(
        IReadOnlyCollection<long> deckQueueIds,
        CancellationToken cancellationToken = default)
        => _deckQueue.GetContentHashesByIdsAsync(deckQueueIds, cancellationToken);

    /// <summary>
    /// Sets the stored canonical content hash for a queued Archidekt deck; passing null clears it.
    /// </summary>
    /// <param name="deckId">Deck ID to update.</param>
    /// <param name="hash">Hash value to store, or null to clear the stored hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task SetContentHashAsync(string deckId, string? hash, CancellationToken cancellationToken = default)
        => _deckQueue.SetContentHashAsync(deckId, hash, cancellationToken);

    /// <summary>
    /// B2 / D-17: idempotently records a URL-imported deck as processed with its commander name.
    /// Mirrors the <see cref="AddDeckIdsAsync"/> UPSERT idiom but always lands processed=1
    /// (URL flow has no queueing step) so Plan 04 SubmitUrl can ship a deck_queue row in one
    /// round-trip and SC #2 ("commander appears in top-commanders list after URL submit") is
    /// provable. <c>COALESCE(excluded.commander_name, deck_queue.commander_name)</c> preserves
    /// a previously-captured name if a re-import fails to extract one.
    /// </summary>
    /// <param name="deckId">Archidekt deck ID validated upstream by ArchidektApiUrl.TryGetDeckId.</param>
    /// <param name="commanderName">Commander name extracted from the imported deck, or null when extraction failed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task MarkUrlDeckProcessedAsync(
        string deckId,
        string? commanderName,
        CancellationToken cancellationToken = default)
        => _deckQueue.MarkUrlDeckProcessedAsync(deckId, commanderName, cancellationToken);

    /// <summary>
    /// Marks the provided deck IDs as processed, optionally skipping them.
    /// </summary>
    /// <param name="deckIds">Deck IDs to update.</param>
    /// <param name="skip">Whether the decks should be skipped after failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task MarkDecksProcessedAsync(IEnumerable<string> deckIds, bool skip = false, CancellationToken cancellationToken = default)
        => _deckQueue.MarkDecksProcessedAsync(deckIds, skip, cancellationToken);

}
