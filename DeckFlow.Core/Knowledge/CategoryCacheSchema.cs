using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Logging;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Owns the database schema for the category-knowledge cache: table creation, migrations, and indexes.
/// </summary>
internal sealed class CategoryCacheSchema
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly string _directoryPath;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes the schema collaborator.
    /// </summary>
    /// <param name="connectionInfo">Provider and connection string details for the knowledge database.</param>
    /// <param name="directoryPath">Directory path used for SQLite directory creation; empty for non-SQLite providers.</param>
    /// <param name="logger">Optional logger for schema and index warnings.</param>
    internal CategoryCacheSchema(RelationalDatabaseConnection connectionInfo, string directoryPath, ILogger? logger)
    {
        _connectionInfo = connectionInfo;
        _directoryPath = directoryPath;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the database schema and required tables exist.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    internal async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_connectionInfo.IsSqlite)
        {
            Directory.CreateDirectory(_directoryPath);
        }

        await using var connection = _connectionInfo.CreateConnection();
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

    private sealed class SqliteTableInfoRow
    {
        public string Name { get; init; } = string.Empty;
    }
}
