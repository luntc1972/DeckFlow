using System.Collections.Concurrent;
using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Owns the database schema for the category-knowledge cache: table creation, migrations, and indexes.
/// </summary>
internal sealed class CategoryCacheSchema
{
    internal const int DefaultMinObservationRows = 5;
    private static readonly ConcurrentDictionary<(RelationalDatabaseProvider Provider, string ConnectionString), SchemaState> SchemaStates = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> SearchKeyBackfills = new(StringComparer.Ordinal);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> RefreshIndexAttempts = new(StringComparer.Ordinal);
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly string _directoryPath;
    private readonly ILogger? _logger;

    internal ILogger? Logger => _logger;

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
        var state = SchemaStates.GetOrAdd((_connectionInfo.Provider, _connectionInfo.ConnectionString), _ => new SchemaState());
        if (state.IsComplete)
        {
            return;
        }

        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            if (!state.IsComplete)
            {
                // Why: schema DDL is idempotent (IF NOT EXISTS / IF EXISTS), so a deadlock can safely retry the whole creation.
                await DeadlockRetry.ExecuteAsync(() => CreateSchemaAsync(cancellationToken), _logger, cancellationToken);
                state.IsComplete = true;
            }
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private async Task CreateSchemaAsync(CancellationToken cancellationToken)
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
                content_hash TEXT NULL,
                archidekt_edh_bracket INTEGER NULL,
                archidekt_deck_format INTEGER NULL,
                archidekt_theorycrafted INTEGER NULL,
                archidekt_created_utc TEXT NULL,
                archidekt_updated_utc TEXT NULL,
                archidekt_metadata_captured_utc TEXT NULL,
                refresh_requested_utc TEXT NULL,
                listing_updated_seen_utc TEXT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await AddMissingColumnsAsync(connection, "deck_queue", new[]
                 {
                     ("content_hash", "TEXT NULL"),
                     ("archidekt_edh_bracket", "INTEGER NULL"),
                     ("archidekt_deck_format", "INTEGER NULL"),
                     ("archidekt_theorycrafted", "INTEGER NULL"),
                     ("archidekt_created_utc", "TEXT NULL"),
                     ("archidekt_updated_utc", "TEXT NULL"),
                     ("archidekt_metadata_captured_utc", "TEXT NULL"),
                     ("refresh_requested_utc", "TEXT NULL"),
                     ("listing_updated_seen_utc", "TEXT NULL"),
                 }, cancellationToken);

        var crawlStateCommand = connection.CreateCommand();
        crawlStateCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS crawl_state (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        await crawlStateCommand.ExecuteNonQueryAsync(cancellationToken);

        await CreateCardCategoryObservationsTableAsync(connection, _connectionInfo.Dialect.SurrogateIdColumnType, cancellationToken);
        await CreateCardCategorySummaryTableAsync(connection, cancellationToken);
        await CreateCardCategoryQualifiedTableAsync(connection, cancellationToken);
        await CreateCardDeckTotalsTableAsync(connection, _connectionInfo.Dialect.SurrogateIdColumnType, cancellationToken);
        if (_connectionInfo.IsSqlite)
        {
            await BackfillCardCategorySummaryAsync(connection, cancellationToken);
        }

        // Why: this table backs the harvested-commanders admin grid and is maintained
        // incrementally by DeckQueueRepository on every processed=1 write, so it must exist
        // unconditionally — it cannot live inside the optional-index swallow-failures block below,
        // or a timed-out backfill would roll back the CREATE TABLE with it (F-52-PG-01).
        var summaryTableCommand = connection.CreateCommand();
        summaryTableCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS processed_commander_summary (
                commander_name TEXT NOT NULL PRIMARY KEY,
                deck_count INTEGER NOT NULL,
                last_processed_utc TEXT NULL,
                commander_name_search_key TEXT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ux_processed_commander_summary_lower ON processed_commander_summary(LOWER(commander_name));
            """;
        await summaryTableCommand.ExecuteNonQueryAsync(cancellationToken);

        await AddMissingColumnsAsync(connection, "processed_commander_summary", new[]
        {
            ("commander_name_search_key", "TEXT NULL")
        }, cancellationToken);

        // Why: one-time backfill of pre-existing processed rows; can legitimately take a while
        // over a large deck_queue, so it gets its own generous timeout and swallows failures
        // separately from table creation — a failed backfill just leaves historical commanders
        // out of the grid until reprocessed, rather than taking the table down with it.
        var backfillCommand = connection.CreateCommand();
        backfillCommand.CommandText = """
            INSERT INTO processed_commander_summary (commander_name, deck_count, last_processed_utc)
            SELECT MAX(commander_name), COUNT(1), MAX(last_checked_utc)
            FROM deck_queue
            WHERE processed = 1 AND commander_name IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM processed_commander_summary)
            GROUP BY LOWER(commander_name)
            ON CONFLICT (commander_name) DO NOTHING;
            """;
        backfillCommand.CommandTimeout = 60;
        try
        {
            await backfillCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or OperationCanceledException or TimeoutException)
        {
            _logger?.LogWarning(
                exception,
                "processed_commander_summary one-time backfill failed; table exists but pre-existing commanders are missing until reprocessed.");
        }

        var searchKeyBackfillKey = $"{_connectionInfo.Provider}:{_connectionInfo.ConnectionString}";
        if (SearchKeyBackfills.TryAdd(searchKeyBackfillKey, 0))
        {
            try
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                var missingSearchKeys = await connection.QueryAsync<string>(new CommandDefinition(
                    "SELECT commander_name FROM processed_commander_summary WHERE commander_name_search_key IS NULL;",
                    transaction: transaction,
                    commandTimeout: 60,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
                foreach (var commanderName in missingSearchKeys)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "UPDATE processed_commander_summary SET commander_name_search_key = @searchKey WHERE commander_name = @commanderName;",
                        new { commanderName, searchKey = CommanderSearchKey.Normalize(commanderName) ?? string.Empty },
                        transaction: transaction,
                        commandTimeout: 60,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is DbException or OperationCanceledException or TimeoutException)
            {
                _logger?.LogWarning(
                    exception,
                    "processed_commander_summary search-key backfill failed; rows without keys are excluded until a later process starts.");
            }
        }

        var requiredIndexCommand = connection.CreateCommand();
        requiredIndexCommand.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS ux_cards_normalized ON cards(normalized_card_name);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_sources_source ON sources(source);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_deck_queue_deck_id ON deck_queue(deck_id);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_obs_grain ON card_category_observations(source_id, card_id, category, board);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_totals_grain ON card_deck_totals(source_id, card_id, board);
            """;
        requiredIndexCommand.CommandTimeout = 15;
        // Why: unique indexes are required by ON CONFLICT writes, so a failed required batch must be retried.
        try
        {
            await requiredIndexCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or OperationCanceledException or TimeoutException)
        {
            _logger?.LogWarning(
                exception,
                "Category knowledge required index creation failed during schema startup; retrying on the next ensure.");
            throw;
        }

        var secondaryIndexCommand = connection.CreateCommand();
        secondaryIndexCommand.CommandText = """
            CREATE INDEX IF NOT EXISTS ix_sources_deck_queue ON sources(deck_queue_id);
            -- Why: ix_obs_card, ix_totals_card, and ix_deck_queue_processed were removed as left-prefix duplicates of wider indexes; production drops them out-of-band with DROP INDEX CONCURRENTLY.
            CREATE INDEX IF NOT EXISTS ix_deck_queue_processed_inserted_deck ON deck_queue(processed, inserted_utc, deck_id);
            -- Why: a failed statement aborts the rest of the secondary batch, so create the replacement before the drops and keep the old indexes if it fails.
            CREATE INDEX IF NOT EXISTS ix_deck_queue_commander_lower_processed ON deck_queue(LOWER(commander_name)) WHERE processed = 1;
            DROP INDEX IF EXISTS ix_deck_queue_processed_commander;
            DROP INDEX IF EXISTS ix_deck_queue_processed_commander_lower;
            CREATE INDEX IF NOT EXISTS ix_obs_card_board ON card_category_observations(card_id, board);
            -- Why: production builds this out-of-band with CREATE INDEX CONCURRENTLY before deploy because 22M rows exceed the 15 s batch timeout.
            CREATE INDEX IF NOT EXISTS ix_obs_card_category ON card_category_observations(card_id, category);
            CREATE INDEX IF NOT EXISTS ix_obs_source ON card_category_observations(source_id);
            CREATE INDEX IF NOT EXISTS ix_totals_card_board ON card_deck_totals(card_id, board);
            -- Why: pending rows are a tiny slice behind ~670k skipped rows, so dequeue and count need an index excluding skipped rows; production builds it out-of-band with CREATE INDEX CONCURRENTLY before deploy.
            CREATE INDEX IF NOT EXISTS ix_deck_queue_pending ON deck_queue(inserted_utc, id) WHERE processed = 0 AND skipped = 0;
            """;
        secondaryIndexCommand.CommandTimeout = 15;
        // Why: secondary indexes improve read performance, but failure must not block category operations.
        // Build heavy production indexes out-of-band with CREATE INDEX CONCURRENTLY.
        try
        {
            await secondaryIndexCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or TimeoutException ||
            (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger?.LogWarning(
                exception,
                "Category knowledge secondary index creation failed during schema startup; reads may be slower until indexes are created.");
        }

        var refreshIndexKey = $"{_connectionInfo.Provider}:{_connectionInfo.ConnectionString}";
        if (!RefreshIndexAttempts.TryAdd(refreshIndexKey, 0))
        {
            return;
        }

        // Why: the drain remains correct without this index; retrying a locking non-concurrent build can stall harvest writes, so production builds it out-of-band.
        var refreshIndexCommand = connection.CreateCommand();
        refreshIndexCommand.CommandText = "CREATE INDEX IF NOT EXISTS ix_deck_queue_refresh_pending ON deck_queue(inserted_utc, deck_id) WHERE processed = 0 AND skipped = 0 AND refresh_requested_utc IS NOT NULL;";
        refreshIndexCommand.CommandTimeout = 15;
        try
        {
            await refreshIndexCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or OperationCanceledException or TimeoutException)
        {
            _logger?.LogWarning(exception, "deck_queue refresh index creation failed; refresh drain continues without it.");
        }
    }

    private sealed class SchemaState
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal volatile bool IsComplete;
        internal volatile bool IsQualifiedBackfilled;
    }

    internal async Task EnsureCardCategoryQualifiedBackfilledAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        var state = SchemaStates.GetOrAdd((_connectionInfo.Provider, _connectionInfo.ConnectionString), _ => new SchemaState());
        if (state.IsQualifiedBackfilled)
        {
            return;
        }

        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!state.IsQualifiedBackfilled)
            {
                // Why: this scan can cover millions of summary rows, so readers only probe and fall back; writers and startup invoke it explicitly.
                await using var connection = _connectionInfo.CreateConnection();
                await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
                await BackfillCardCategoryQualifiedAsync(connection, CancellationToken.None).ConfigureAwait(false);
                state.IsQualifiedBackfilled = true;
            }
        }
        finally
        {
            state.Gate.Release();
        }
    }

    internal async Task<bool> IsCardCategoryQualifiedBackfilledAsync(
        CancellationToken cancellationToken = default,
        int? commandTimeoutSeconds = null)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        var state = SchemaStates.GetOrAdd((_connectionInfo.Provider, _connectionInfo.ConnectionString), _ => new SchemaState());
        if (state.IsQualifiedBackfilled)
        {
            return true;
        }

        await using var connection = _connectionInfo.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM card_category_qualified);";
        if (commandTimeoutSeconds is int timeoutSeconds)
        {
            command.CommandTimeout = timeoutSeconds;
        }
        if (!Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)))
        {
            return false;
        }

        state.IsQualifiedBackfilled = true;
        return true;
    }

    private async Task AddMissingColumnsAsync(
        DbConnection connection,
        string table,
        IEnumerable<(string Name, string Definition)> columns,
        CancellationToken cancellationToken)
    {
        var existingColumns = await GetTableColumnsAsync(connection, table, cancellationToken);
        foreach (var (name, definition) in columns)
        {
            if (existingColumns.Contains(name))
            {
                continue;
            }

            var addColumnCommand = connection.CreateCommand();
            addColumnCommand.CommandText = $"ALTER TABLE {table} ADD COLUMN {name} {definition};";
            try
            {
                await addColumnCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (DbException exception) when (IsDuplicateColumn(exception))
            {
                _logger?.LogDebug(exception, "Ignoring concurrent duplicate {ColumnName} column migration on {TableName}.", name, table);
            }
        }
    }

    private static bool IsDuplicateColumn(DbException exception)
        => exception is PostgresException postgresException && postgresException.SqlState == "42701"
           || exception.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase);

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

    private static async Task CreateCardCategorySummaryTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS card_category_summary (
                card_id INTEGER NOT NULL,
                category TEXT NOT NULL,
                observation_rows INTEGER NOT NULL,
                PRIMARY KEY (card_id, category)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateCardCategoryQualifiedTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS card_category_qualified (
                card_id INTEGER NOT NULL,
                category TEXT NOT NULL,
                observation_rows INTEGER NOT NULL,
                PRIMARY KEY (card_id, category)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task BackfillCardCategorySummaryAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        // Why: a populated summary is maintained incrementally, so avoid a full observation-index scan on every schema check.
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM card_category_summary);";
        if (Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)))
        {
            return;
        }

        command.CommandText = """
            INSERT INTO card_category_summary (card_id, category, observation_rows)
            SELECT card_id, category, COUNT(*)
            FROM card_category_observations
            WHERE EXISTS (SELECT 1 FROM card_category_observations)
              AND NOT EXISTS (SELECT 1 FROM card_category_summary)
            GROUP BY card_id, category;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task BackfillCardCategoryQualifiedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM card_category_qualified);";
        if (Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)))
        {
            return;
        }

        if (connection is NpgsqlConnection)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            command.Transaction = transaction;
            // Why: writers invoke this before their first summary write; summary-then-qualified locking preserves a complete snapshot and prevents deadlocks.
            command.CommandText = "LOCK TABLE card_category_summary IN SHARE ROW EXCLUSIVE MODE; LOCK TABLE card_category_qualified IN SHARE ROW EXCLUSIVE MODE;";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            command.CommandText = "SELECT EXISTS(SELECT 1 FROM card_category_qualified);";
            if (Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            command.CommandText = """
                INSERT INTO card_category_qualified (card_id, category, observation_rows)
                SELECT card_id, category, observation_rows
                FROM card_category_summary
                WHERE observation_rows >= @minObservationRows
                  AND NOT EXISTS (SELECT 1 FROM card_category_qualified)
                ON CONFLICT (card_id, category) DO NOTHING;
                """;
            var postgresParameter = command.CreateParameter();
            postgresParameter.ParameterName = "@minObservationRows";
            postgresParameter.Value = DefaultMinObservationRows;
            command.Parameters.Add(postgresParameter);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        command.CommandText = """
            INSERT INTO card_category_qualified (card_id, category, observation_rows)
            SELECT card_id, category, observation_rows
            FROM card_category_summary
            WHERE observation_rows >= @minObservationRows
              AND NOT EXISTS (SELECT 1 FROM card_category_qualified)
            ON CONFLICT (card_id, category) DO NOTHING;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@minObservationRows";
        parameter.Value = DefaultMinObservationRows;
        command.Parameters.Add(parameter);
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

    /// <summary>Maps SQLite table-info rows so cache migrations can detect existing columns.</summary>
    private sealed class SqliteTableInfoRow
    {
        public string Name { get; init; } = string.Empty;
    }
}
