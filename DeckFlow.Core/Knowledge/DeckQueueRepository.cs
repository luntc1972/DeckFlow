using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Logging;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Owns all deck_queue and crawl_state operations: harvest queue add/mark/dedup,
/// content-hash read/write, crawl-page state, and commander aggregate queries.
/// </summary>
internal sealed class DeckQueueRepository
{
    private static readonly TimeSpan DeckRefreshCooldown = TimeSpan.FromDays(5);
    // Why: SQLite and PostgreSQL use opposite default null placement; explicit CASE keys keep page boundaries identical.
    private const string DeckCountDescendingOrderBy = "ORDER BY deck_count DESC, CASE WHEN last_processed_utc IS NULL THEN 1 ELSE 0 END ASC, last_processed_utc DESC, CASE WHEN commander_name_search_key IS NULL THEN 1 ELSE 0 END ASC, commander_name_search_key ASC, commander_name ASC";
    private const string DeckCountAscendingOrderBy = "ORDER BY deck_count ASC, CASE WHEN commander_name_search_key IS NULL THEN 1 ELSE 0 END ASC, commander_name_search_key ASC, commander_name ASC";
    private const string NameAscendingOrderBy = "ORDER BY CASE WHEN commander_name_search_key IS NULL THEN 1 ELSE 0 END ASC, commander_name_search_key ASC, commander_name ASC";
    private const string NameDescendingOrderBy = "ORDER BY CASE WHEN commander_name_search_key IS NULL THEN 1 ELSE 0 END ASC, commander_name_search_key DESC, commander_name DESC";
    private const string LastProcessedDescendingOrderBy = "ORDER BY CASE WHEN last_processed_utc IS NULL THEN 1 ELSE 0 END ASC, last_processed_utc DESC, CASE WHEN commander_name_search_key IS NULL THEN 1 ELSE 0 END ASC, commander_name_search_key ASC, commander_name ASC";
    private const string LastProcessedAscendingOrderBy = "ORDER BY CASE WHEN last_processed_utc IS NULL THEN 1 ELSE 0 END ASC, last_processed_utc ASC, CASE WHEN commander_name_search_key IS NULL THEN 1 ELSE 0 END ASC, commander_name_search_key ASC, commander_name ASC";
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly CategoryCacheSchema _schema;

    /// <summary>
    /// Initializes the deck-queue collaborator.
    /// </summary>
    /// <param name="connectionInfo">Provider and connection string details for the knowledge database.</param>
    /// <param name="schema">Shared schema collaborator used to initialize tables on first access.</param>
    internal DeckQueueRepository(RelationalDatabaseConnection connectionInfo, CategoryCacheSchema schema)
    {
        _connectionInfo = connectionInfo;
        _schema = schema;
    }

    /// <summary>
    /// Returns the count of processed decks in <c>deck_queue</c> that are led by <paramref name="commanderName"/>.
    /// </summary>
    internal async Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commanderName);
        await _schema.EnsureSchemaAsync(cancellationToken);

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
    internal async Task<IReadOnlyList<(string CommanderName, int DeckCount, string? LastProcessedUtc)>> GetPagedProcessedCommanderRowsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Max(pageSize, 1);
        var offset = ((long)page - 1) * pageSize;

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var rows = await connection.QueryAsync<ProcessedCommanderAggregateRow>(new CommandDefinition(
            """
            SELECT commander_name, deck_count, last_processed_utc
            FROM processed_commander_summary
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
    internal async Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default)
    {
        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM processed_commander_summary;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return checked((int)result);
    }

    internal async Task<IReadOnlyList<(string CommanderName, int DeckCount, string? LastProcessedUtc)>> GetFilteredProcessedCommanderRowsAsync(
        int page,
        int pageSize,
        CommanderGridQuery query,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Max(pageSize, 1);
        var offset = ((long)page - 1) * pageSize;
        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var prefix = query.SqlPrefixPattern;
        var orderBy = GetCommanderOrderBy(query);
        var sql = prefix is null
            ? $"""
            SELECT commander_name, deck_count, last_processed_utc
            FROM processed_commander_summary
            {orderBy}
            LIMIT @limit OFFSET @offset;
            """
            : $"""
            SELECT commander_name, deck_count, last_processed_utc
            FROM processed_commander_summary
            WHERE commander_name_search_key LIKE @prefix ESCAPE '\'
            {orderBy}
            LIMIT @limit OFFSET @offset;
            """;
        var rows = await connection.QueryAsync<ProcessedCommanderAggregateRow>(new CommandDefinition(
            sql,
            new { limit = pageSize, offset, prefix },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(row => (row.CommanderName, checked((int)row.DeckCount), row.LastProcessedUtc)).ToList();
    }

    private static string GetCommanderOrderBy(CommanderGridQuery query)
        => query.SortBy switch
        {
            CommanderSortColumn.Name => query.Descending ? NameDescendingOrderBy : NameAscendingOrderBy,
            CommanderSortColumn.LastProcessed => query.Descending ? LastProcessedDescendingOrderBy : LastProcessedAscendingOrderBy,
            _ => query.Descending ? DeckCountDescendingOrderBy : DeckCountAscendingOrderBy
        };

    internal async Task<int> GetFilteredProcessedCommanderCountAsync(CommanderGridQuery query, CancellationToken cancellationToken = default)
    {
        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var prefix = query.SqlPrefixPattern;
        var sql = prefix is null
            ? "SELECT COUNT(1) FROM processed_commander_summary;"
            : "SELECT COUNT(1) FROM processed_commander_summary WHERE commander_name_search_key LIKE @prefix ESCAPE '\\';";
        return checked((int)await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { prefix }, cancellationToken: cancellationToken)).ConfigureAwait(false));
    }

    /// <summary>Inserts new deck IDs into the queue for processing.</summary>
    /// <param name="deckIds">Deck IDs to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Number of IDs with no existing queue row. Requeued IDs, including already processed IDs, are
    /// deliberately excluded because this count represents IDs discovered by the crawler.
    /// </returns>
    internal async Task<int> AddDeckIdsAsync(IEnumerable<string> deckIds, CancellationToken cancellationToken = default)
    {
        var unique = deckIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var insertedUtc = DateTime.UtcNow;
        var requeueBeforeUtc = insertedUtc.Subtract(DeckRefreshCooldown);

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var existingIds = new List<string>();

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
            var rowsInserted = await connection.ExecuteAsync(new CommandDefinition(
                $"""
                INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc)
                VALUES (@deckId, @insertedUtc, 0, 0, NULL)
                ON CONFLICT(deck_id) DO NOTHING;
                """,
                new { deckId, insertedUtc, requeueBeforeUtc },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);

            if (rowsInserted == 0)
            {
                existingIds.Add(deckId);
            }
        }

        foreach (var deckId in existingIds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"""
                UPDATE deck_queue
                SET inserted_utc = @insertedUtc,
                    processed = CASE
                        WHEN deck_queue.processed = 0 AND deck_queue.skipped = 0 THEN 0
                        WHEN deck_queue.last_checked_utc IS NULL OR {lastChecked} <= @requeueBeforeUtc THEN 0
                        ELSE deck_queue.processed
                    END,
                    skipped = CASE
                        WHEN deck_queue.processed = 0 AND deck_queue.skipped = 0 THEN 0
                        WHEN deck_queue.last_checked_utc IS NULL OR {lastChecked} <= @requeueBeforeUtc THEN 0
                        ELSE deck_queue.skipped
                    END
                WHERE deck_id = @deckId;
                """,
                new { deckId, insertedUtc, requeueBeforeUtc },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken);
        return unique.Count - existingIds.Count;
    }

    /// <summary>
    /// Gets the next batch of deck IDs that have not been processed or skipped.
    /// </summary>
    /// <param name="count">Maximum number of deck IDs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task<IReadOnlyList<string>> GetNextUnprocessedDeckIdsAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var deckIds = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT deck_id
            FROM deck_queue
            WHERE processed = 0 AND skipped = 0
            -- Why: inserted_utc is shared by every row in a queued batch, so id preserves FIFO order for ties.
            ORDER BY inserted_utc, id
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
    internal async Task<int> GetUnprocessedCountAsync(CancellationToken cancellationToken = default)
    {
        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(1) FROM deck_queue WHERE processed = 0 AND skipped = 0;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return checked((int)result);
    }

    /// <summary>
    /// Determines whether the unprocessed queue contains more than a supplied threshold.
    /// </summary>
    /// <param name="threshold">Exclusive queue threshold.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task<bool> HasMoreThanUnprocessedDecksAsync(int threshold, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threshold);
        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT 1 FROM deck_queue WHERE processed = 0 AND skipped = 0 LIMIT 1 OFFSET @threshold;",
            new { threshold },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return result.HasValue;
    }

    /// <summary>
    /// Counts the number of decks that have been processed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default)
    {
        await _schema.EnsureSchemaAsync(cancellationToken);
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
    internal async Task<int> GetRecentDeckCrawlPageAsync(CancellationToken cancellationToken = default)
    {
        await _schema.EnsureSchemaAsync(cancellationToken);
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
    internal async Task SetRecentDeckCrawlPageAsync(int page, CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(2, page);
        await _schema.EnsureSchemaAsync(cancellationToken);
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
    /// <c>card_category_observations</c> (Phase 7 D-17). A null <paramref name="commanderName"/>
    /// preserves a previously captured commander during a skipped retry.
    /// </summary>
    /// <remarks>
    /// When <paramref name="metadata"/> is non-null, metadata capture is last-capture-wins per field,
    /// not per-field COALESCE. The fresh <c>archidekt_metadata_captured_utc</c> truthfully records the
    /// most recent recognizable payload, so null fields in that payload are trusted over stale values.
    /// </remarks>
    /// <param name="deckId">Deck ID to update.</param>
    /// <param name="commanderName">Commander card name extracted from the imported deck, or null on skip / unknown.</param>
    /// <param name="skip">Whether the deck should be marked as skipped after failure.</param>
    /// <param name="metadata">Captured Archidekt metadata, or null when no capture occurred.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task MarkDeckProcessedAsync(
        string deckId,
        string? commanderName,
        bool skip = false,
        ArchidektDeckMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var priorCommanderName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT commander_name FROM deck_queue WHERE deck_id = @deckId;",
            new { deckId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        // D-17: capture commander identity in the same UPDATE that flips processed=1 so the
        // harvest stats panel (top-10 commanders) can read deck_queue.commander_name without
        // a join into card_category_observations.
        var metadataParameters = metadata is null ? null : ArchidektDeckMetadataParameters.From(metadata);
        var sql = metadataParameters is null
            ? """
            UPDATE deck_queue
               SET processed = 1,
                   skipped = @skipped,
                   last_checked_utc = @now,
                   commander_name = COALESCE(@commanderName, deck_queue.commander_name)
             WHERE deck_id = @deckId;
            """
            : """
            UPDATE deck_queue
               SET processed = 1,
                   skipped = @skipped,
                   last_checked_utc = @now,
                   commander_name = @commanderName,
                   archidekt_edh_bracket = @EdhBracket,
                   archidekt_deck_format = @DeckFormat,
                   archidekt_theorycrafted = @Theorycrafted,
                   archidekt_created_utc = @CreatedUtc,
                   archidekt_updated_utc = @UpdatedUtc,
                   archidekt_metadata_captured_utc = @CapturedUtc
             WHERE deck_id = @deckId;
            """;
        var parameters = new
        {
            deckId,
            now = DateTime.UtcNow,
            skipped = skip ? 1 : 0,
            commanderName,
            // Why: These member names must match the SQL parameter names exactly for Dapper binding.
            EdhBracket = metadataParameters?.EdhBracket,
            DeckFormat = metadataParameters?.DeckFormat,
            Theorycrafted = metadataParameters?.Theorycrafted,
            CreatedUtc = metadataParameters?.CreatedUtc,
            UpdatedUtc = metadataParameters?.UpdatedUtc,
            CapturedUtc = metadataParameters?.CapturedUtc,
        };
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        await using var summaryTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var currentCommanderName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT commander_name FROM deck_queue WHERE deck_id = @deckId;",
            new { deckId },
            transaction: summaryTransaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        await RefreshCommanderSummaryAsync(connection, summaryTransaction, new[] { priorCommanderName, currentCommanderName }, _connectionInfo.IsPostgres, cancellationToken).ConfigureAwait(false);
        await summaryTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the stored canonical content hash for a queued Archidekt deck.
    /// </summary>
    /// <param name="deckId">Deck ID to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task<string?> GetContentHashAsync(string deckId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT content_hash FROM deck_queue WHERE deck_id = @deckId;",
            new { deckId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets stored canonical content hashes for queued Archidekt decks keyed by <c>deck_queue.id</c>.
    /// </summary>
    internal async Task<IReadOnlyDictionary<long, string?>> GetContentHashesByIdsAsync(
        IReadOnlyCollection<long> deckQueueIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deckQueueIds);
        if (deckQueueIds.Count == 0)
        {
            return new Dictionary<long, string?>();
        }

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        string query = _connectionInfo.IsPostgres
            ? "SELECT id, content_hash FROM deck_queue WHERE id = ANY(@deckQueueIds);"
            : "SELECT id, content_hash FROM deck_queue WHERE id IN @deckQueueIds;";
        var rows = await connection.QueryAsync<DeckQueueContentHashRow>(new CommandDefinition(
            query,
            new { deckQueueIds = deckQueueIds.ToList() },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.ToDictionary(row => row.Id, row => row.ContentHash);
    }

    /// <summary>
    /// Sets the stored canonical content hash for a queued Archidekt deck; passing null clears it.
    /// </summary>
    /// <param name="deckId">Deck ID to update.</param>
    /// <param name="hash">Hash value to store, or null to clear the stored hash.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task SetContentHashAsync(string deckId, string? hash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

        await _schema.EnsureSchemaAsync(cancellationToken);
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
    /// <param name="metadata">Captured Archidekt metadata, or null when no capture occurred.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task MarkUrlDeckProcessedAsync(
        string deckId,
        string? commanderName,
        ArchidektDeckMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

        await _schema.EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var priorCommanderName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT commander_name FROM deck_queue WHERE deck_id = @deckId;",
            new { deckId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var metadataParameters = metadata is null ? null : ArchidektDeckMetadataParameters.From(metadata);
        var parameters = new
        {
            deckId,
            now,
            commanderName,
            // Why: These member names must match the SQL parameter names exactly for Dapper binding.
            EdhBracket = metadataParameters?.EdhBracket,
            DeckFormat = metadataParameters?.DeckFormat,
            Theorycrafted = metadataParameters?.Theorycrafted,
            CreatedUtc = metadataParameters?.CreatedUtc,
            UpdatedUtc = metadataParameters?.UpdatedUtc,
            CapturedUtc = metadataParameters?.CapturedUtc,
        };
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name,
                archidekt_edh_bracket, archidekt_deck_format, archidekt_theorycrafted, archidekt_created_utc,
                archidekt_updated_utc, archidekt_metadata_captured_utc)
            VALUES (@deckId, @now, 1, 0, @now, @commanderName, @EdhBracket, @DeckFormat, @Theorycrafted,
                @CreatedUtc, @UpdatedUtc, @CapturedUtc)
            ON CONFLICT(deck_id) DO UPDATE
            SET processed = 1,
                skipped = 0,
                last_checked_utc = excluded.last_checked_utc,
                commander_name = COALESCE(excluded.commander_name, deck_queue.commander_name),
                archidekt_edh_bracket = CASE WHEN excluded.archidekt_metadata_captured_utc IS NULL THEN deck_queue.archidekt_edh_bracket ELSE excluded.archidekt_edh_bracket END,
                archidekt_deck_format = CASE WHEN excluded.archidekt_metadata_captured_utc IS NULL THEN deck_queue.archidekt_deck_format ELSE excluded.archidekt_deck_format END,
                archidekt_theorycrafted = CASE WHEN excluded.archidekt_metadata_captured_utc IS NULL THEN deck_queue.archidekt_theorycrafted ELSE excluded.archidekt_theorycrafted END,
                archidekt_created_utc = CASE WHEN excluded.archidekt_metadata_captured_utc IS NULL THEN deck_queue.archidekt_created_utc ELSE excluded.archidekt_created_utc END,
                archidekt_updated_utc = CASE WHEN excluded.archidekt_metadata_captured_utc IS NULL THEN deck_queue.archidekt_updated_utc ELSE excluded.archidekt_updated_utc END,
                archidekt_metadata_captured_utc = CASE WHEN excluded.archidekt_metadata_captured_utc IS NULL THEN deck_queue.archidekt_metadata_captured_utc ELSE excluded.archidekt_metadata_captured_utc END;
            """,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        await using var summaryTransaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var currentCommanderName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT commander_name FROM deck_queue WHERE deck_id = @deckId;",
            new { deckId },
            transaction: summaryTransaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        await RefreshCommanderSummaryAsync(connection, summaryTransaction, new[] { priorCommanderName, currentCommanderName }, _connectionInfo.IsPostgres, cancellationToken).ConfigureAwait(false);
        await summaryTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks the provided deck IDs as processed, optionally skipping them.
    /// </summary>
    /// <param name="deckIds">Deck IDs to update.</param>
    /// <param name="skip">Whether the decks should be skipped after failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task MarkDecksProcessedAsync(IEnumerable<string> deckIds, bool skip = false, CancellationToken cancellationToken = default)
    {
        var unique = deckIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (unique.Count == 0)
        {
            return;
        }

        await _schema.EnsureSchemaAsync(cancellationToken);
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

            var commanderName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT commander_name FROM deck_queue WHERE deck_id = @deckId;",
                new { deckId },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            await RefreshCommanderSummaryAsync(connection, transaction, new[] { commanderName }, _connectionInfo.IsPostgres, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private DbConnection CreateConnection() => _connectionInfo.CreateConnection();

    private static string ToAsciiLowerInvariant(string value)
    {
        var characters = value.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (characters[index] is >= 'A' and <= 'Z')
            {
                characters[index] = (char)(characters[index] + ('a' - 'A'));
            }
        }

        return new string(characters);
    }

    private static async Task RefreshCommanderSummaryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IEnumerable<string?> commanderNames,
        bool isPostgres,
        CancellationToken cancellationToken)
    {
        var normalizedNames = commanderNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            // Why: SQLite LOWER folds ASCII only, while PostgreSQL LOWER follows Unicode case rules.
            .SelectMany(name => new[] { name!.ToLowerInvariant(), ToAsciiLowerInvariant(name) })
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedNames.Count == 0)
        {
            return;
        }

        var membershipOperator = isPostgres ? "= ANY(@normalizedNames)" : "IN @normalizedNames";
        // Why: Dapper binds lists as arrays on Npgsql, so PostgreSQL requires ANY instead of IN.
        if (isPostgres)
        {
            foreach (var normalizedName in normalizedNames.OrderBy(name => name, StringComparer.Ordinal))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "SELECT pg_advisory_xact_lock(hashtext(lower(@commanderName)));",
                    new { commanderName = normalizedName },
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
        }

        await connection.ExecuteAsync(new CommandDefinition(
            $"DELETE FROM processed_commander_summary WHERE LOWER(commander_name) {membershipOperator};",
            new { normalizedNames },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ProcessedCommanderAggregateRow>(new CommandDefinition(
            $"""
            SELECT MAX(commander_name) AS commander_name, COUNT(1) AS deck_count, MAX(last_checked_utc) AS last_processed_utc
            FROM deck_queue
            WHERE processed = 1 AND commander_name IS NOT NULL AND LOWER(commander_name) {membershipOperator}
            GROUP BY LOWER(commander_name);
            """,
            new { normalizedNames },
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        foreach (var row in rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO processed_commander_summary (commander_name, deck_count, last_processed_utc, commander_name_search_key)
                VALUES (@commanderName, @deckCount, @lastProcessedUtc, @searchKey)
                ON CONFLICT (LOWER(commander_name)) DO UPDATE SET
                    deck_count = excluded.deck_count,
                    last_processed_utc = excluded.last_processed_utc,
                    commander_name_search_key = excluded.commander_name_search_key;
                """,
                new
                {
                    commanderName = row.CommanderName,
                    deckCount = row.DeckCount,
                    lastProcessedUtc = row.LastProcessedUtc,
                    searchKey = CommanderSearchKey.Normalize(row.CommanderName)
                },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    private sealed class ProcessedCommanderAggregateRow
    {
        public string CommanderName { get; init; } = string.Empty;
        public long DeckCount { get; init; }
        public string? LastProcessedUtc { get; init; }
    }

    private sealed class DeckQueueContentHashRow
    {
        public long Id { get; init; }
        public string? ContentHash { get; init; }
    }
}
