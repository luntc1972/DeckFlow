using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Extensions.Logging;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Reads and writes card-category knowledge rows in the SQLite or Postgres knowledge-cache database.
/// </summary>
public sealed class CategoryKnowledgeRepository
{
    private const string ArchidektLiveSourcePrefix = "archidekt_live:";
    private static readonly TimeSpan DeckRefreshCooldown = TimeSpan.FromDays(5);
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly ILogger? _logger;
    private readonly string? _databasePath;
    private readonly string _directoryPath;

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
    }

    /// <summary>
    /// Gets the SQLite database path when the repository is configured for SQLite storage.
    /// </summary>
    public string? DatabasePath => _databasePath;

    /// <summary>
    /// Ensures the database schema and required tables exist.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_connectionInfo.IsSqlite)
        {
            Directory.CreateDirectory(_directoryPath);
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await CreateCardsTableAsync(connection, _connectionInfo.Dialect.SurrogateIdColumnType, cancellationToken);
        await CreateSourcesTableAsync(connection, _connectionInfo.Dialect.SurrogateIdColumnType, cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS deck_queue (
                id {_connectionInfo.Dialect.SurrogateIdColumnType},
                deck_id TEXT NOT NULL,
                inserted_utc TEXT NOT NULL,
                processed INTEGER NOT NULL DEFAULT 0,
                skipped INTEGER NOT NULL DEFAULT 0,
                last_checked_utc TEXT,
                commander_name TEXT NULL,
                content_hash TEXT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        var deckQueueColumns = await GetTableColumnsAsync(connection, "deck_queue", cancellationToken);
        if (!deckQueueColumns.Contains("content_hash"))
        {
            var addContentHashCommand = connection.CreateCommand();
            addContentHashCommand.CommandText = "ALTER TABLE deck_queue ADD COLUMN content_hash TEXT NULL;";
            await addContentHashCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var crawlStateCommand = connection.CreateCommand();
        crawlStateCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS crawl_state (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        await crawlStateCommand.ExecuteNonQueryAsync(cancellationToken);

        await CreateCardCategoryObservationsTableAsync(connection, _connectionInfo.Dialect.SurrogateIdColumnType, cancellationToken);
        await CreateCardDeckTotalsTableAsync(connection, _connectionInfo.Dialect.SurrogateIdColumnType, cancellationToken);

        var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS ux_cards_normalized ON cards(normalized_card_name);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_sources_source ON sources(source);
            CREATE INDEX IF NOT EXISTS ix_sources_deck_queue ON sources(deck_queue_id);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_deck_queue_deck_id ON deck_queue(deck_id);
            CREATE INDEX IF NOT EXISTS ix_deck_queue_processed ON deck_queue(processed);
            CREATE INDEX IF NOT EXISTS ix_deck_queue_processed_inserted_deck ON deck_queue(processed, inserted_utc, deck_id);
            -- Why: this batched DDL runs inside a try/catch that swallows index-creation failures, so create the replacement first; if it fails, the batch aborts before the drops execute and the old indexes survive.
            CREATE INDEX IF NOT EXISTS ix_deck_queue_commander_lower_processed ON deck_queue(LOWER(commander_name)) WHERE processed = 1;
            DROP INDEX IF EXISTS ix_deck_queue_processed_commander;
            DROP INDEX IF EXISTS ix_deck_queue_processed_commander_lower;
            CREATE UNIQUE INDEX IF NOT EXISTS ux_obs_grain ON card_category_observations(source_id, card_id, category, board);
            CREATE INDEX IF NOT EXISTS ix_obs_card ON card_category_observations(card_id);
            CREATE INDEX IF NOT EXISTS ix_obs_card_board ON card_category_observations(card_id, board);
            CREATE INDEX IF NOT EXISTS ix_obs_source ON card_category_observations(source_id);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_totals_grain ON card_deck_totals(source_id, card_id, board);
            CREATE INDEX IF NOT EXISTS ix_totals_card ON card_deck_totals(card_id);
            CREATE INDEX IF NOT EXISTS ix_totals_card_board ON card_deck_totals(card_id, board);
            """;
        indexCommand.CommandTimeout = 15;
        // Why: indexes are startup optimizations; large production tables should have heavy
        // indexes built out-of-band with CREATE INDEX CONCURRENTLY instead of crashing deploys.
        try
        {
            await indexCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or OperationCanceledException or TimeoutException)
        {
            _logger?.LogWarning(
                exception,
                "Category knowledge index creation failed during schema startup; continuing without optional indexes.");
        }
    }

    private static async Task CreateCardsTableAsync(DbConnection connection, string surrogateIdColumnType, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS cards (
                id {surrogateIdColumnType},
                normalized_card_name TEXT NOT NULL,
                display_name TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateSourcesTableAsync(DbConnection connection, string surrogateIdColumnType, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        // Why: every source string is interned once so facts carry source_id;
        // deck_queue remains the harvest queue and URL/EDHREC sources stay out of it.
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS sources (
                id {surrogateIdColumnType},
                source TEXT NOT NULL,
                deck_queue_id INTEGER NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateCardCategoryObservationsTableAsync(DbConnection connection, string surrogateIdColumnType, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        // Why: the write path owns fact/dimension integrity uniformly across dialects;
        // hard DB constraints would behave differently across SQLite and Postgres.
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS card_category_observations (
                id {surrogateIdColumnType},
                source_id INTEGER NOT NULL,
                card_id INTEGER NOT NULL,
                card_name TEXT NOT NULL,
                category TEXT NOT NULL,
                board TEXT NOT NULL DEFAULT 'mainboard',
                deck_count INTEGER NOT NULL DEFAULT 0,
                count INTEGER NOT NULL,
                last_seen_utc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateCardDeckTotalsTableAsync(DbConnection connection, string surrogateIdColumnType, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS card_deck_totals (
                id {surrogateIdColumnType},
                source_id INTEGER NOT NULL,
                card_id INTEGER NOT NULL,
                board TEXT NOT NULL DEFAULT 'mainboard',
                deck_count INTEGER NOT NULL DEFAULT 0,
                last_seen_utc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves previously observed categories for the specified card.
    /// </summary>
    /// <param name="cardName">Card name to look up.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var categories = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT o.category
            FROM card_category_observations o
            JOIN cards c ON c.id = o.card_id
            WHERE c.normalized_card_name = @normalized
            GROUP BY o.category
            ORDER BY LOWER(o.category), o.category
            """,
            new { normalized = CardNormalizer.Normalize(cardName) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return CategoryFilter.IncludedOrFallback(categories);
    }

    /// <summary>
    /// Retrieves detail rows for a card, including display name and count.
    /// </summary>
    /// <param name="cardName">Card name to query.</param>
    /// <param name="boardFilter">Optional board name to filter the category rows.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCardAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var queryTemplate = """
            SELECT o.category, o.card_name, SUM(o.count) AS total, SUM(o.deck_count) AS deck_total
            FROM card_category_observations o
            JOIN cards c ON c.id = o.card_id
            WHERE c.normalized_card_name = @normalized
            {0}
            GROUP BY o.category, o.card_name
            ORDER BY total DESC, LOWER(o.category), o.category;
        """;
        var filterClause = boardFilter is null
            ? string.Empty
            : "AND o.board = @board";
        var rawRows = await connection.QueryAsync<CategoryKnowledgeAggregateRow>(new CommandDefinition(
            string.Format(queryTemplate, filterClause),
            new
            {
                normalized = CardNormalizer.Normalize(cardName),
                board = boardFilter is null ? null : NormalizeBoard(boardFilter)
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var rows = rawRows
            .Select(row => new CategoryKnowledgeRow(row.Category, row.CardName, checked((int)row.Total), checked((int)row.DeckTotal)))
            .ToList();

        return FilterGenericCategoryRowsWithFallback(rows);
    }

    /// <summary>
    /// Returns all card-category observations from decks led by <paramref name="commanderName"/>,
    /// aggregated across every harvested deck that has this commander in the commander zone.
    /// </summary>
    public async Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commanderName);
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rawRows = await connection.QueryAsync<CategoryKnowledgeAggregateRow>(new CommandDefinition(
            """
            SELECT o.category, o.card_name, SUM(o.count) AS total, COUNT(DISTINCT q.id) AS deck_total
            FROM card_category_observations o
            JOIN sources s ON s.id = o.source_id
            JOIN deck_queue q ON q.id = s.deck_queue_id
            WHERE LOWER(q.commander_name) = LOWER(@commanderName)
              AND q.processed = 1
            GROUP BY o.category, o.card_name
            ORDER BY total DESC, LOWER(o.category), o.category;
            """,
            new { commanderName },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var rows = rawRows
            .Select(row => new CategoryKnowledgeRow(row.Category, row.CardName, checked((int)row.Total), checked((int)row.DeckTotal)))
            .ToList();

        return FilterGenericCategoryRowsWithFallback(rows);
    }

    /// <summary>
    /// Returns the count of processed decks in <c>deck_queue</c> that are led by <paramref name="commanderName"/>.
    /// </summary>
    public async Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commanderName);
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM deck_queue
            WHERE LOWER(commander_name) = LOWER(@commanderName)
              AND processed = 1;
            """,
            new { commanderName },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return checked((int)result);
    }

    /// <summary>
    /// Returns a paged slice of processed commander aggregates for the harvested-commanders admin grid.
    /// </summary>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Maximum number of rows to return.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task<IReadOnlyList<(string CommanderName, int DeckCount, string? LastProcessedUtc)>> GetPagedProcessedCommanderRowsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Max(pageSize, 1);
        var offset = ((long)page - 1) * pageSize;

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ProcessedCommanderAggregateRow>(new CommandDefinition(
            """
            SELECT MAX(commander_name) AS commander_name, COUNT(1) AS deck_count, MAX(last_checked_utc) AS last_processed_utc
            FROM deck_queue
            WHERE processed = 1 AND commander_name IS NOT NULL
            GROUP BY LOWER(commander_name)
            ORDER BY deck_count DESC, last_processed_utc DESC, LOWER(commander_name) ASC
            LIMIT @limit OFFSET @offset;
            """,
            new { limit = pageSize, offset },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows
            .Select(row => (row.CommanderName, checked((int)row.DeckCount), row.LastProcessedUtc))
            .ToList();
    }

    /// <summary>
    /// Counts distinct processed commanders in the deck queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT COUNT(DISTINCT LOWER(commander_name))
            FROM deck_queue
            WHERE processed = 1 AND commander_name IS NOT NULL;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return checked((int)result);
    }

    /// <summary>
    /// Replaces all observations for a source with the provided rows.
    /// </summary>
    /// <param name="source">Source label for the data.</param>
    /// <param name="rows">Rows to persist.</param>
    /// <param name="board">Board name applied to each persisted row.</param>
    /// <param name="deckCount">Deck-count total applied to each persisted row.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task ReplaceSourceRowsAsync(string source, IReadOnlyList<CategoryKnowledgeRow> rows, string board = "mainboard", int deckCount = 0, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (rows is null)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sourceId = rows.Count == 0
            ? await ResolveSourceIdForReadAsync(connection, transaction, source, cancellationToken)
            : await ResolveSourceIdAsync(connection, transaction, source, cancellationToken);
        if (sourceId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM card_category_observations WHERE source_id = @sourceId;",
            new { sourceId = sourceId.Value },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var cardIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var normalizedBoard = NormalizeBoard(board);
        var lastSeenUtc = DateTime.UtcNow;
        foreach (var row in rows)
        {
            var cardId = await ResolveCardIdAsync(connection, transaction, row.CardName, cardIds, cancellationToken);
            var deckCountValue = row.DeckCount > 0 ? row.DeckCount : deckCount;
            await UpsertCategoryObservationAsync(
                connection,
                transaction,
                sourceId.Value,
                cardId,
                row.CardName,
                row.Category,
                normalizedBoard,
                row.Count,
                deckCountValue,
                lastSeenUtc,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Removes all cached observation and deck total rows for the provided source.
    /// </summary>
    /// <param name="source">Source label to remove.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task DeleteSourceDataAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sourceId = await ResolveSourceIdForReadAsync(connection, transaction, source, cancellationToken);
        if (sourceId is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM card_category_observations WHERE source_id = @sourceId;",
            new { sourceId = sourceId.Value },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM card_deck_totals WHERE source_id = @sourceId;",
            new { sourceId = sourceId.Value },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken);
    }

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
    public async Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(cardName) || categories.Count == 0 || quantity <= 0)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sourceId = await ResolveSourceIdAsync(connection, transaction, source, cancellationToken);
        var cardIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var cardId = await ResolveCardIdAsync(connection, transaction, cardName, cardIds, cancellationToken);
        var normalizedBoard = NormalizeBoard(board);
        var lastSeenUtc = DateTime.UtcNow;
        foreach (var category in categories)
        {
            await UpsertCategoryObservationAsync(
                connection,
                transaction,
                sourceId,
                cardId,
                cardName,
                category,
                normalizedBoard,
                quantity,
                deckCountIncrement,
                lastSeenUtc,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Persists the number of decks that contain the given card on the specified board.
    /// </summary>
    public async Task PersistCardDeckTotalsAsync(string source, string cardName, string board = "mainboard", int deckCountIncrement = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(cardName) || deckCountIncrement <= 0)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sourceId = await ResolveSourceIdAsync(connection, transaction, source, cancellationToken);
        var cardIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var cardId = await ResolveCardIdAsync(connection, transaction, cardName, cardIds, cancellationToken);
        await UpsertCardDeckTotalAsync(
            connection,
            transaction,
            sourceId,
            cardId,
            NormalizeBoard(board),
            deckCountIncrement,
            DateTime.UtcNow,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    internal async Task PersistDeckCategoryBatchAsync(
        string source,
        IReadOnlyList<(string CardName, string Category, string Board, int Quantity, int DeckCountIncrement)> observations,
        IReadOnlyList<(string CardName, string Board)> cardBoardTotals,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source) || (observations.Count == 0 && cardBoardTotals.Count == 0))
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sourceId = await ResolveSourceIdAsync(connection, transaction, source, cancellationToken);
        var cardIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var lastSeenUtc = DateTime.UtcNow;

        foreach (var observation in observations)
        {
            if (string.IsNullOrWhiteSpace(observation.CardName) || observation.Quantity <= 0)
            {
                continue;
            }

            var cardId = await ResolveCardIdAsync(connection, transaction, observation.CardName, cardIds, cancellationToken);
            await UpsertCategoryObservationAsync(
                connection,
                transaction,
                sourceId,
                cardId,
                observation.CardName,
                observation.Category,
                NormalizeBoard(observation.Board),
                observation.Quantity,
                observation.DeckCountIncrement,
                lastSeenUtc,
                cancellationToken);
        }

        foreach (var cardBoardTotal in cardBoardTotals)
        {
            if (string.IsNullOrWhiteSpace(cardBoardTotal.CardName))
            {
                continue;
            }

            var cardId = await ResolveCardIdAsync(connection, transaction, cardBoardTotal.CardName, cardIds, cancellationToken);
            await UpsertCardDeckTotalAsync(
                connection,
                transaction,
                sourceId,
                cardId,
                NormalizeBoard(cardBoardTotal.Board),
                1,
                lastSeenUtc,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves deck totals for the card, optionally filtered by board.
    /// </summary>
    public async Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var filterClause = boardFilter is null ? string.Empty : "AND t.board = @board";
        var rows = await connection.QueryAsync<BoardDeckTotalRow>(new CommandDefinition(
            $"""
            SELECT t.board, SUM(t.deck_count) AS total
            FROM card_deck_totals t
            JOIN cards c ON c.id = t.card_id
            WHERE c.normalized_card_name = @normalized
            {filterClause}
            GROUP BY t.board;
            """,
            new
            {
                normalized = CardNormalizer.Normalize(cardName),
                board = boardFilter is null ? null : NormalizeBoard(boardFilter)
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var boardCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            boardCounts[row.Board] = checked((int)row.Total);
        }

        var totalDecks = boardCounts.Values.Sum();
        return new CardDeckTotals(totalDecks, boardCounts);
    }

    /// <summary>
    /// Checks whether the repository already contains entries for the source.
    /// </summary>
    /// <param name="source">Source label to check.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task<bool> HasSourceDataAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sourceId = await ResolveSourceIdForReadAsync(connection, null, source, cancellationToken);
        if (sourceId is null)
        {
            return false;
        }

        var result = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(1) FROM card_category_observations WHERE source_id = @sourceId;",
            new { sourceId = sourceId.Value },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return result > 0L;
    }

    /// <summary>
    /// Inserts new deck IDs into the queue for processing.
    /// </summary>
    /// <param name="deckIds">Deck IDs to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AddDeckIdsAsync(IEnumerable<string> deckIds, CancellationToken cancellationToken = default)
    {
        var unique = deckIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal);
        var insertedUtc = DateTime.UtcNow;
        var requeueBeforeUtc = insertedUtc.Subtract(DeckRefreshCooldown);

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Why: last_checked_utc is a TEXT column on both dialects, but the Dapper DateTime
        // handler binds @requeueBeforeUtc as a native timestamptz on Postgres — and Postgres
        // has no `text <= timestamptz` operator (42883), so the comparison must cast the column
        // to timestamptz there. `::timestamptz` parses every datetime text format the column can
        // hold (ISO-8601 "O" and Postgres' own coercion form), so no data backfill is needed.
        // SQLite keeps its lexical TEXT comparison unchanged. (F-51-PG-01)
        var lastChecked = _connectionInfo.IsSqlite
            ? "deck_queue.last_checked_utc"
            : "deck_queue.last_checked_utc::timestamptz";

        foreach (var deckId in unique)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"""
                INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc)
                VALUES (@deckId, @insertedUtc, 0, 0, NULL)
                ON CONFLICT(deck_id)
                DO UPDATE SET
                    inserted_utc = excluded.inserted_utc,
                    processed = CASE
                        WHEN deck_queue.processed = 0 AND deck_queue.skipped = 0 THEN 0
                        WHEN deck_queue.last_checked_utc IS NULL OR {lastChecked} <= @requeueBeforeUtc THEN 0
                        ELSE deck_queue.processed
                    END,
                    skipped = CASE
                        WHEN deck_queue.processed = 0 AND deck_queue.skipped = 0 THEN 0
                        WHEN deck_queue.last_checked_utc IS NULL OR {lastChecked} <= @requeueBeforeUtc THEN 0
                        ELSE deck_queue.skipped
                    END;
                """,
                new { deckId, insertedUtc, requeueBeforeUtc },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the next batch of deck IDs that have not been processed or skipped.
    /// </summary>
    /// <param name="count">Maximum number of deck IDs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<string>> GetNextUnprocessedDeckIdsAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var deckIds = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT deck_id
            FROM deck_queue
            WHERE processed = 0 AND skipped = 0
            ORDER BY inserted_utc
            LIMIT @count;
            """,
            new { count },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return deckIds.ToList();
    }

    /// <summary>
    /// Retrieves the total number of unprocessed deck IDs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<int> GetUnprocessedCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(1) FROM deck_queue WHERE processed = 0 AND skipped = 0;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return checked((int)result);
    }

    /// <summary>
    /// Counts the number of decks that have been processed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(1) FROM deck_queue WHERE processed = 1;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return checked((int)result);
    }

    /// <summary>
    /// Gets the next recent Archidekt search page to crawl after page one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<int> GetRecentDeckCrawlPageAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value FROM crawl_state WHERE key = 'archidekt_recent_page';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (int.TryParse(result, out var page) && page >= 2)
        {
            return page;
        }

        return 2;
    }

    /// <summary>
    /// Persists the next recent Archidekt search page to crawl.
    /// </summary>
    /// <param name="page">Page number to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetRecentDeckCrawlPageAsync(int page, CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(2, page);
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO crawl_state (key, value)
            VALUES ('archidekt_recent_page', @page)
            ON CONFLICT(key)
            DO UPDATE SET value = excluded.value;
            """,
            new { page = normalizedPage.ToString() },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

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
    public async Task MarkDeckProcessedAsync(
        string deckId,
        string? commanderName,
        bool skip = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        // D-17: capture commander identity in the same UPDATE that flips processed=1 so the
        // harvest stats panel (top-10 commanders) can read deck_queue.commander_name without
        // a join into card_category_observations.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE deck_queue
               SET processed = 1,
                   skipped = @skipped,
                   last_checked_utc = @now,
                   commander_name = @commanderName
             WHERE deck_id = @deckId;
            """,
            new
            {
                deckId,
                now = DateTime.UtcNow,
                skipped = skip ? 1 : 0,
                commanderName
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the stored canonical content hash for a queued Archidekt deck.
    /// </summary>
    /// <param name="deckId">Deck ID to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string?> GetContentHashAsync(string deckId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT content_hash FROM deck_queue WHERE deck_id = @deckId;",
            new { deckId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the stored canonical content hash for a queued Archidekt deck; passing null clears it.
    /// </summary>
    /// <param name="deckId">Deck ID to update.</param>
    /// <param name="hash">Hash value to store, or null to clear the stored hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetContentHashAsync(string deckId, string? hash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE deck_queue SET content_hash = @hash WHERE deck_id = @deckId;",
            new { deckId, hash },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

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
    public async Task MarkUrlDeckProcessedAsync(
        string deckId,
        string? commanderName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var now = DateTime.UtcNow;
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name)
            VALUES (@deckId, @now, 1, 0, @now, @commanderName)
            ON CONFLICT(deck_id) DO UPDATE
            SET processed = 1,
                skipped = 0,
                last_checked_utc = excluded.last_checked_utc,
                commander_name = COALESCE(excluded.commander_name, deck_queue.commander_name);
            """,
            new { deckId, now, commanderName },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks the provided deck IDs as processed, optionally skipping them.
    /// </summary>
    /// <param name="deckIds">Deck IDs to update.</param>
    /// <param name="skip">Whether the decks should be skipped after failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task MarkDecksProcessedAsync(IEnumerable<string> deckIds, bool skip = false, CancellationToken cancellationToken = default)
    {
        var unique = deckIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (unique.Count == 0)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var deckId in unique)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE deck_queue
                SET processed = 1,
                    skipped = @skipped,
                    last_checked_utc = @now
                WHERE deck_id = @deckId;
                """,
                new
                {
                    deckId,
                    now = DateTime.UtcNow,
                    skipped = skip ? 1 : 0
                },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<long> ResolveCardIdAsync(
        DbConnection connection,
        DbTransaction transaction,
        string cardName,
        IDictionary<string, long> cache,
        CancellationToken cancellationToken)
    {
        var normalized = CardNormalizer.Normalize(cardName);
        if (cache.TryGetValue(normalized, out var cachedId))
        {
            return cachedId;
        }

        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO cards (normalized_card_name, display_name)
            VALUES (@normalized, @display)
            ON CONFLICT(normalized_card_name)
            DO UPDATE SET display_name = excluded.display_name
            RETURNING id;
            """,
            new { normalized, display = cardName },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        cache[normalized] = id;
        return id;
    }

    private static async Task<long?> ResolveSourceIdForReadAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string source,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT id FROM sources WHERE source = @source;",
            new { source },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<long> ResolveSourceIdAsync(
        DbConnection connection,
        DbTransaction transaction,
        string source,
        CancellationToken cancellationToken)
    {
        var deckQueueId = await ResolveDeckQueueIdForSourceAsync(connection, transaction, source, cancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO sources (source, deck_queue_id)
            VALUES (@source, @deckQueueId)
            ON CONFLICT(source)
            DO UPDATE SET deck_queue_id = COALESCE(sources.deck_queue_id, excluded.deck_queue_id)
            RETURNING id;
            """,
            new { source, deckQueueId },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<long?> ResolveDeckQueueIdForSourceAsync(
        DbConnection connection,
        DbTransaction transaction,
        string source,
        CancellationToken cancellationToken)
    {
        if (!source.StartsWith(ArchidektLiveSourcePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var deckId = source[ArchidektLiveSourcePrefix.Length..];
        if (string.IsNullOrWhiteSpace(deckId))
        {
            return null;
        }

        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT id FROM deck_queue WHERE deck_id = @deckId;",
            new { deckId },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task UpsertCategoryObservationAsync(
        DbConnection connection,
        DbTransaction transaction,
        long sourceId,
        long cardId,
        string cardName,
        string category,
        string board,
        int quantity,
        int deckCountIncrement,
        DateTime lastSeenUtc,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO card_category_observations (source_id, card_id, card_name, category, board, deck_count, count, last_seen_utc)
            VALUES (@sourceId, @cardId, @cardName, @category, @board, @deckCount, @quantity, @lastSeenUtc)
            ON CONFLICT(source_id, card_id, category, board)
            DO UPDATE SET
                count = card_category_observations.count + excluded.count,
                deck_count = card_category_observations.deck_count + excluded.deck_count,
                card_name = excluded.card_name,
                last_seen_utc = excluded.last_seen_utc;
            """,
            new
            {
                sourceId,
                cardId,
                cardName,
                category,
                board,
                deckCount = deckCountIncrement,
                quantity,
                lastSeenUtc
            },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static async Task UpsertCardDeckTotalAsync(
        DbConnection connection,
        DbTransaction transaction,
        long sourceId,
        long cardId,
        string board,
        int deckCountIncrement,
        DateTime lastSeenUtc,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO card_deck_totals (source_id, card_id, board, deck_count, last_seen_utc)
            VALUES (@sourceId, @cardId, @board, @deckCount, @lastSeenUtc)
            ON CONFLICT(source_id, card_id, board)
            DO UPDATE SET
                deck_count = card_deck_totals.deck_count + excluded.deck_count,
                last_seen_utc = excluded.last_seen_utc;
            """,
            new
            {
                sourceId,
                cardId,
                board,
                deckCount = deckCountIncrement,
                lastSeenUtc
            },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static string NormalizeBoard(string? board)
    {
        if (string.IsNullOrWhiteSpace(board))
        {
            return "mainboard";
        }

        return board.Trim().ToLowerInvariant();
    }

    private static IReadOnlyList<CategoryKnowledgeRow> FilterGenericCategoryRowsWithFallback(IReadOnlyList<CategoryKnowledgeRow> rows)
    {
        var categoriesByCard = rows
            .GroupBy(row => row.CardName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => CategoryFilter.IncludedOrFallback(group.Select(row => row.Category)),
                StringComparer.OrdinalIgnoreCase);

        return rows
            .Where(row => categoriesByCard[row.CardName].Contains(row.Category, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private DbConnection CreateConnection() => _connectionInfo.CreateConnection();

    private async Task<IReadOnlySet<string>> GetTableColumnsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_connectionInfo.IsSqlite)
        {
            var rows = await connection.QueryAsync<SqliteTableInfoRow>(new CommandDefinition(
                $"PRAGMA table_info({tableName});",
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            foreach (var row in rows)
            {
                if (!string.IsNullOrWhiteSpace(row.Name))
                {
                    columns.Add(row.Name);
                }
            }

            return columns;
        }

        var pgColumns = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = @tableName
            ORDER BY ordinal_position;
            """,
            new { tableName },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        foreach (var column in pgColumns)
        {
            if (!string.IsNullOrWhiteSpace(column))
            {
                columns.Add(column);
            }
        }

        return columns;
    }

    private sealed class CategoryKnowledgeAggregateRow
    {
        public string Category { get; init; } = string.Empty;
        public string CardName { get; init; } = string.Empty;
        public long Total { get; init; }
        public long DeckTotal { get; init; }
    }

    private sealed class ProcessedCommanderAggregateRow
    {
        public string CommanderName { get; init; } = string.Empty;
        public long DeckCount { get; init; }
        public string? LastProcessedUtc { get; init; }
    }

    private sealed class BoardDeckTotalRow
    {
        public string Board { get; init; } = string.Empty;
        public long Total { get; init; }
    }

    private sealed class SqliteTableInfoRow
    {
        public string Name { get; init; } = string.Empty;
    }
}
