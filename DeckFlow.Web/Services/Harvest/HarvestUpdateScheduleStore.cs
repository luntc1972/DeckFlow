using System.Data.Common;
using Dapper;
using DeckFlow.Core.Storage;
using Microsoft.AspNetCore.Hosting;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Parallel update schedule store; separate table is accepted debt because bulk's row is CHECK-pinned, while named checks keep a future promote cheap.</summary>
public sealed class HarvestUpdateScheduleStore : IHarvestUpdateScheduleStore
{
    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS harvest_update_schedule (
            id INT PRIMARY KEY CONSTRAINT ck_harvest_update_schedule_single_row CHECK (id = 1),
            interval_minutes INT NULL CONSTRAINT ck_harvest_update_schedule_interval CHECK (interval_minutes IS NULL OR interval_minutes IN (15,30,60,120)),
            paused BOOLEAN NOT NULL DEFAULT FALSE,
            updated_utc TIMESTAMPTZ NOT NULL DEFAULT now());
        """;
    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS harvest_update_schedule (
            id INTEGER PRIMARY KEY CONSTRAINT ck_harvest_update_schedule_single_row CHECK (id = 1),
            interval_minutes INTEGER NULL CONSTRAINT ck_harvest_update_schedule_interval CHECK (interval_minutes IS NULL OR interval_minutes IN (15,30,60,120)),
            paused INTEGER NOT NULL DEFAULT 0,
            updated_utc TEXT NOT NULL DEFAULT (datetime('now')));
        """;
    private const string SeedSql = "INSERT INTO harvest_update_schedule (id, interval_minutes, paused, updated_utc) VALUES (1, NULL, FALSE, now()) ON CONFLICT (id) DO NOTHING;";
    private const string SqliteSeedSql = "INSERT INTO harvest_update_schedule (id, interval_minutes, paused, updated_utc) VALUES (1, NULL, 0, datetime('now')) ON CONFLICT (id) DO NOTHING;";
    private const string PostgresUpsertSql = "INSERT INTO harvest_update_schedule (id, interval_minutes, paused, updated_utc) VALUES (1, @interval, @paused, @now) ON CONFLICT (id) DO UPDATE SET interval_minutes = EXCLUDED.interval_minutes, paused = EXCLUDED.paused, updated_utc = EXCLUDED.updated_utc;";
    private const string SqliteUpsertSql = "INSERT INTO harvest_update_schedule (id, interval_minutes, paused, updated_utc) VALUES (1, @interval, @paused, @now) ON CONFLICT (id) DO UPDATE SET interval_minutes = excluded.interval_minutes, paused = excluded.paused, updated_utc = excluded.updated_utc;";
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>Creates a SQLite store at the supplied file path.</summary>
    public HarvestUpdateScheduleStore(string databasePath) : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }
    /// <summary>Creates a store from connection details.</summary>
    public HarvestUpdateScheduleStore(RelationalDatabaseConnection connectionInfo)
    {
        _connectionInfo = connectionInfo;
        if (!_connectionInfo.IsPostgres)
        {
            var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }
    }
    /// <summary>Creates the DI store against shared harvest-state storage.</summary>
    public HarvestUpdateScheduleStore(IWebHostEnvironment environment) : this(DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection(environment)) { }
    /// <inheritdoc />
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition(_connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition(_connectionInfo.IsPostgres ? SeedSql : SqliteSeedSql, cancellationToken: cancellationToken)).ConfigureAwait(false);
            _schemaReady = true;
        }
        finally { _schemaGate.Release(); }
    }
    /// <inheritdoc />
    public async Task<HarvestUpdateScheduleSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("SELECT interval_minutes, paused, updated_utc FROM harvest_update_schedule WHERE id = 1;", cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? throw new InvalidOperationException("harvest_update_schedule seed row (id=1) is missing.") : new(row.IntervalMinutes, row.Paused, row.UpdatedUtc);
    }
    /// <inheritdoc />
    public async Task SaveAsync(int? intervalMinutes, bool paused, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(_connectionInfo.IsPostgres ? PostgresUpsertSql : SqliteUpsertSql, new { interval = intervalMinutes, paused, now = now.ToUniversalTime() }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken) => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    private sealed class Row { public int? IntervalMinutes { get; init; } public bool Paused { get; init; } public DateTimeOffset UpdatedUtc { get; init; } }
}
