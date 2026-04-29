using System;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Knowledge;

public sealed class CategoryKnowledgeRepository
{
    private static readonly TimeSpan DeckRefreshCooldown = TimeSpan.FromDays(1);
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly string? _databasePath;
    private readonly string _directoryPath;

    /// <summary>
    /// Initializes the repository for the provided SQLite database path.
    /// </summary>
    public CategoryKnowledgeRepository(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath))
    {
    }

    public CategoryKnowledgeRepository(RelationalDatabaseConnection connectionInfo)
    {
        _connectionInfo = connectionInfo;
        _databasePath = connectionInfo.IsSqlite
            ? ExtractSqlitePath(connectionInfo.ConnectionString)
            : null;
        _directoryPath = _databasePath is null
            ? string.Empty
            : Path.GetDirectoryName(_databasePath) ?? Directory.GetCurrentDirectory();
    }

    public string DatabasePath => _databasePath ?? string.Empty;

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

        await CreateCardCategoryObservationsTableAsync(connection, cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS deck_queue (
                deck_id TEXT PRIMARY KEY,
                inserted_utc TEXT NOT NULL,
                processed INTEGER NOT NULL DEFAULT 0,
                skipped INTEGER NOT NULL DEFAULT 0,
                last_checked_utc TEXT
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        var crawlStateCommand = connection.CreateCommand();
        crawlStateCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS crawl_state (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        await crawlStateCommand.ExecuteNonQueryAsync(cancellationToken);

        await EnsureDeckQueueColumnsAsync(connection, cancellationToken);
        await EnsureCategoryObservationSchemaAsync(connection, cancellationToken);
        await CreateCardDeckTotalsTableAsync(connection, cancellationToken);
    }

    /// <summary>
    /// Verifies the deck queue table includes the latest needed columns.
    /// </summary>
    /// <param name="connection">Open relational database connection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task EnsureDeckQueueColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var columns = await GetTableColumnsAsync(connection, "deck_queue", cancellationToken);
        var hasSkipped = columns.Contains("skipped");

        if (!hasSkipped)
        {
            var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE deck_queue ADD COLUMN skipped INTEGER NOT NULL DEFAULT 0;";
            await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task EnsureCategoryObservationSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var columns = await GetTableColumnsAsync(connection, "card_category_observations", cancellationToken);
        if (columns.Count == 0)
        {
            return;
        }

        if (!columns.Contains("board") || !columns.Contains("deck_count"))
        {
            await MigrateCategoryObservationsTableAsync(connection, cancellationToken);
        }
    }

    private static async Task CreateCardCategoryObservationsTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS card_category_observations (
                source TEXT NOT NULL,
                card_name TEXT NOT NULL,
                normalized_card_name TEXT NOT NULL,
                category TEXT NOT NULL,
                board TEXT NOT NULL DEFAULT 'mainboard',
                deck_count INTEGER NOT NULL DEFAULT 0,
                count INTEGER NOT NULL,
                last_seen_utc TEXT NOT NULL,
                PRIMARY KEY (source, normalized_card_name, category, board)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateCardDeckTotalsTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS card_deck_totals (
                source TEXT NOT NULL,
                card_name TEXT NOT NULL,
                normalized_card_name TEXT NOT NULL,
                board TEXT NOT NULL DEFAULT 'mainboard',
                deck_count INTEGER NOT NULL DEFAULT 0,
                last_seen_utc TEXT NOT NULL,
                PRIMARY KEY (source, normalized_card_name, board)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MigrateCategoryObservationsTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var renameCommand = connection.CreateCommand();
        renameCommand.CommandText = "ALTER TABLE card_category_observations RENAME TO card_category_observations_old;";
        await renameCommand.ExecuteNonQueryAsync(cancellationToken);

        await CreateCardCategoryObservationsTableAsync(connection, cancellationToken);

        var copyCommand = connection.CreateCommand();
        copyCommand.CommandText = """
            INSERT INTO card_category_observations (source, card_name, normalized_card_name, category, board, deck_count, count, last_seen_utc)
            SELECT source, card_name, normalized_card_name, category, 'mainboard', 0, count, last_seen_utc
            FROM card_category_observations_old;
            """;
        await copyCommand.ExecuteNonQueryAsync(cancellationToken);

        var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = "DROP TABLE card_category_observations_old;";
        await dropCommand.ExecuteNonQueryAsync(cancellationToken);
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

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT category
            FROM card_category_observations
            WHERE normalized_card_name = @normalized
            GROUP BY category
            ORDER BY LOWER(category), category
            """;
        AddParameter(command, "@normalized", CardNormalizer.Normalize(cardName));

        var categories = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(reader.GetString(0));
        }

        return categories;
    }

    /// <summary>
    /// Retrieves detail rows for a card, including display name and count.
    /// </summary>
    /// <param name="cardName">Card name to query.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCardAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        var queryTemplate = """
            SELECT category, card_name, SUM(count) AS total, SUM(deck_count) AS deck_total
            FROM card_category_observations
            WHERE normalized_card_name = @normalized
            {0}
            GROUP BY category, card_name
            ORDER BY total DESC, LOWER(category), category;
            """;
        var filterClause = boardFilter is null
            ? string.Empty
            : "AND board = @board";
        command.CommandText = string.Format(queryTemplate, filterClause);
        AddParameter(command, "@normalized", CardNormalizer.Normalize(cardName));
        if (boardFilter is not null)
        {
            AddParameter(command, "@board", NormalizeBoard(boardFilter));
        }

        var rows = new List<CategoryKnowledgeRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var category = reader.GetString(0);
            var displayName = reader.GetString(1);
            var total = reader.GetInt32(2);
            var deckTotal = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            rows.Add(new CategoryKnowledgeRow(category, displayName, total, deckTotal));
        }

        return rows;
    }

    /// <summary>
    /// Replaces all observations for a source with the provided rows.
    /// </summary>
    /// <param name="source">Source label for the data.</param>
    /// <param name="rows">Rows to persist.</param>
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

        var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = "DELETE FROM card_category_observations WHERE source = @source;";
        AddParameter(deleteCommand, "@source", source);
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

        foreach (var row in rows)
        {
            var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO card_category_observations (source, card_name, normalized_card_name, category, board, deck_count, count, last_seen_utc)
                VALUES (@source, @cardName, @normalizedCardName, @category, @board, @deckCount, @count, @lastSeenUtc)
                """;
            AddParameter(insertCommand, "@source", source);
            AddParameter(insertCommand, "@cardName", row.CardName);
            AddParameter(insertCommand, "@normalizedCardName", CardNormalizer.Normalize(row.CardName));
            AddParameter(insertCommand, "@category", row.Category);
            AddParameter(insertCommand, "@board", NormalizeBoard(board));
            var deckCountValue = row.DeckCount > 0 ? row.DeckCount : deckCount;
            AddParameter(insertCommand, "@deckCount", deckCountValue);
            AddParameter(insertCommand, "@count", row.Count);
            AddParameter(insertCommand, "@lastSeenUtc", DateTimeOffset.UtcNow.ToString("O"));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
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

        var deleteObservationsCommand = connection.CreateCommand();
        deleteObservationsCommand.Transaction = transaction;
        deleteObservationsCommand.CommandText = "DELETE FROM card_category_observations WHERE source = @source;";
        AddParameter(deleteObservationsCommand, "@source", source);
        await deleteObservationsCommand.ExecuteNonQueryAsync(cancellationToken);

        var deleteTotalsCommand = connection.CreateCommand();
        deleteTotalsCommand.Transaction = transaction;
        deleteTotalsCommand.CommandText = "DELETE FROM card_deck_totals WHERE source = @source;";
        AddParameter(deleteTotalsCommand, "@source", source);
        await deleteTotalsCommand.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Persists observed categories for a specific card occurrence.
    /// </summary>
    /// <param name="source">Data source label.</param>
    /// <param name="cardName">Card name.</param>
    /// <param name="categories">Categories to record.</param>
    /// <param name="quantity">Quantity observed.</param>
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

        foreach (var category in categories)
        {
            if (!CategoryFilter.IsIncluded(category))
            {
                continue;
            }

            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO card_category_observations (source, card_name, normalized_card_name, category, board, deck_count, count, last_seen_utc)
                VALUES (@source, @cardName, @normalizedCardName, @category, @board, @deckCount, @quantity, @lastSeenUtc)
                ON CONFLICT(source, normalized_card_name, category, board)
                DO UPDATE SET
                    count = count + excluded.count,
                    deck_count = deck_count + excluded.deck_count,
                    card_name = excluded.card_name,
                    last_seen_utc = excluded.last_seen_utc
                """;
            AddParameter(command, "@source", source);
            AddParameter(command, "@cardName", cardName);
            AddParameter(command, "@normalizedCardName", CardNormalizer.Normalize(cardName));
            AddParameter(command, "@category", category);
            AddParameter(command, "@board", NormalizeBoard(board));
            AddParameter(command, "@deckCount", deckCountIncrement);
            AddParameter(command, "@quantity", quantity);
            AddParameter(command, "@lastSeenUtc", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
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

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO card_deck_totals (source, card_name, normalized_card_name, board, deck_count, last_seen_utc)
            VALUES (@source, @cardName, @normalizedCardName, @board, @deckCount, @lastSeenUtc)
            ON CONFLICT(source, normalized_card_name, board)
            DO UPDATE SET
                deck_count = deck_count + excluded.deck_count,
                card_name = excluded.card_name,
                last_seen_utc = excluded.last_seen_utc;
            """;
        AddParameter(command, "@source", source);
        AddParameter(command, "@cardName", cardName);
        AddParameter(command, "@normalizedCardName", CardNormalizer.Normalize(cardName));
        AddParameter(command, "@board", NormalizeBoard(board));
        AddParameter(command, "@deckCount", deckCountIncrement);
        AddParameter(command, "@lastSeenUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
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

        var filterClause = boardFilter is null ? string.Empty : "AND board = @board";
        var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT board, SUM(deck_count) AS total
            FROM card_deck_totals
            WHERE normalized_card_name = @normalized
            {filterClause}
            GROUP BY board;
            """;
        AddParameter(command, "@normalized", CardNormalizer.Normalize(cardName));
        if (boardFilter is not null)
        {
            AddParameter(command, "@board", NormalizeBoard(boardFilter));
        }

        var boardCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var board = reader.GetString(0);
            var total = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            boardCounts[board] = total;
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

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM card_category_observations WHERE source = @source;";
        AddParameter(command, "@source", source);
        var result = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
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
        var insertedUtc = DateTimeOffset.UtcNow;
        var requeueBeforeUtc = insertedUtc.Subtract(DeckRefreshCooldown);

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var deckId in unique)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc)
                VALUES (@deckId, @insertedUtc, 0, 0, NULL)
                ON CONFLICT(deck_id)
                DO UPDATE SET
                    inserted_utc = excluded.inserted_utc,
                    processed = CASE
                        WHEN deck_queue.processed = 0 AND deck_queue.skipped = 0 THEN 0
                        WHEN deck_queue.last_checked_utc IS NULL OR deck_queue.last_checked_utc <= @requeueBeforeUtc THEN 0
                        ELSE deck_queue.processed
                    END,
                    skipped = CASE
                        WHEN deck_queue.processed = 0 AND deck_queue.skipped = 0 THEN 0
                        WHEN deck_queue.last_checked_utc IS NULL OR deck_queue.last_checked_utc <= @requeueBeforeUtc THEN 0
                        ELSE deck_queue.skipped
                    END;
                """;
            AddParameter(command, "@deckId", deckId);
            AddParameter(command, "@insertedUtc", insertedUtc.ToString("O"));
            AddParameter(command, "@requeueBeforeUtc", requeueBeforeUtc.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
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

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT deck_id
            FROM deck_queue
            WHERE processed = 0 AND skipped = 0
            ORDER BY inserted_utc
            LIMIT @count;
            """;
        AddParameter(command, "@count", count);

        var deckIds = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            deckIds.Add(reader.GetString(0));
        }

        return deckIds;
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

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM deck_queue WHERE processed = 0 AND skipped = 0;";
        var result = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        return (int)result;
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

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM deck_queue WHERE processed = 1;";
        var result = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        return (int)result;
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

        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM crawl_state WHERE key = 'archidekt_recent_page';";
        var result = await command.ExecuteScalarAsync(cancellationToken) as string;

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

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO crawl_state (key, value)
            VALUES ('archidekt_recent_page', @page)
            ON CONFLICT(key)
            DO UPDATE SET value = excluded.value;
            """;
        AddParameter(command, "@page", normalizedPage.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
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
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE deck_queue
                SET processed = 1,
                    skipped = @skipped,
                    last_checked_utc = @now
                WHERE deck_id = @deckId;
                """;
            AddParameter(command, "@deckId", deckId);
            AddParameter(command, "@now", DateTimeOffset.UtcNow.ToString("O"));
            AddParameter(command, "@skipped", skip ? 1 : 0);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static string NormalizeBoard(string? board)
    {
        if (string.IsNullOrWhiteSpace(board))
        {
            return "mainboard";
        }

        return board.Trim().ToLowerInvariant();
    }

    private DbConnection CreateConnection() => _connectionInfo.CreateConnection();

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private async Task<IReadOnlySet<string>> GetTableColumnsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_connectionInfo.IsSqlite)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(1))
                {
                    columns.Add(reader.GetString(1));
                }
            }

            return columns;
        }

        var pgCommand = connection.CreateCommand();
        pgCommand.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = @tableName
            ORDER BY ordinal_position;
            """;
        AddParameter(pgCommand, "@tableName", tableName);
        await using var pgReader = await pgCommand.ExecuteReaderAsync(cancellationToken);
        while (await pgReader.ReadAsync(cancellationToken))
        {
            if (!pgReader.IsDBNull(0))
            {
                columns.Add(pgReader.GetString(0));
            }
        }

        return columns;
    }

    private static string ExtractSqlitePath(string connectionString)
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        return Path.GetFullPath(builder.DataSource);
    }
}
