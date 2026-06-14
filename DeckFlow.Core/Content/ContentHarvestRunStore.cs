using System.Data.Common;
using Dapper;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default implementation of <see cref="IContentHarvestRunStore"/> backed by the local Content KB database.
/// </summary>
public sealed class ContentHarvestRunStore : IContentHarvestRunStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed run store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public ContentHarvestRunStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a run store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    public ContentHarvestRunStore(RelationalDatabaseConnection connectionInfo)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
        if (_connectionInfo.IsSqlite)
        {
            var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    /// <inheritdoc />
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            // Why: schema creation is an intentional raw ADO.NET carve-out for this phase.
            await using var create = connection.CreateCommand();
            create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<long> StartRunAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO content_harvest_runs (started_utc, spend_usd)
            VALUES (@startedUtc, @spendUsd)
            RETURNING id;
            """,
            new { startedUtc = DateTimeOffset.UtcNow, spendUsd = 0m },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CompleteRunAsync(
        long runId,
        int sourcesProcessed,
        int videosProcessed,
        int transcriptsFetched,
        int whisperCalls,
        decimal spendUsd,
        string? abortedReason,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(runId, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(sourcesProcessed);
        ArgumentOutOfRangeException.ThrowIfNegative(videosProcessed);
        ArgumentOutOfRangeException.ThrowIfNegative(transcriptsFetched);
        ArgumentOutOfRangeException.ThrowIfNegative(whisperCalls);
        ArgumentOutOfRangeException.ThrowIfNegative(spendUsd);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_harvest_runs
               SET completed_utc = @completedUtc,
                   sources_processed = @sourcesProcessed,
                   videos_processed = @videosProcessed,
                   transcripts_fetched = @transcriptsFetched,
                   whisper_calls = @whisperCalls,
                   spend_usd = @spendUsd,
                   aborted_reason = @abortedReason
             WHERE id = @runId;
            """,
            new
            {
                completedUtc = DateTimeOffset.UtcNow,
                sourcesProcessed,
                videosProcessed,
                transcriptsFetched,
                whisperCalls,
                spendUsd,
                abortedReason,
                runId
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (affected == 0)
        {
            throw new InvalidOperationException($"No content harvest run with id {runId} to complete.");
        }
    }

    /// <inheritdoc />
    public async Task<ContentHarvestRun?> GetRunAsync(long runId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(runId, 1);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<ContentHarvestRun>(new CommandDefinition(
            """
            SELECT id,
                   started_utc,
                   completed_utc,
                   sources_processed,
                   videos_processed,
                   transcripts_fetched,
                   whisper_calls,
                   spend_usd,
                   aborted_reason
              FROM content_harvest_runs
             WHERE id = @runId;
            """,
            new { runId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_harvest_runs (
          id                  BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          started_utc         TIMESTAMPTZ NOT NULL DEFAULT now(),
          completed_utc       TIMESTAMPTZ NULL,
          sources_processed   INT NOT NULL DEFAULT 0,
          videos_processed    INT NOT NULL DEFAULT 0,
          transcripts_fetched INT NOT NULL DEFAULT 0,
          whisper_calls       INT NOT NULL DEFAULT 0,
          spend_usd           DECIMAL(10,6) NOT NULL DEFAULT 0,
          aborted_reason      TEXT NULL
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_harvest_runs (
          id                  INTEGER PRIMARY KEY AUTOINCREMENT,
          started_utc         TEXT NOT NULL DEFAULT (datetime('now')),
          completed_utc       TEXT NULL,
          sources_processed   INTEGER NOT NULL DEFAULT 0,
          videos_processed    INTEGER NOT NULL DEFAULT 0,
          transcripts_fetched INTEGER NOT NULL DEFAULT 0,
          whisper_calls       INTEGER NOT NULL DEFAULT 0,
          spend_usd           TEXT NOT NULL DEFAULT '0',
          aborted_reason      TEXT NULL
        );
        """;
}
