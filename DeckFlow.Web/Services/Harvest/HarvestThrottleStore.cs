using System.Data.Common;
using Dapper;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Relational store for the single persisted Archidekt throttle row.</summary>
public sealed class HarvestThrottleStore : IHarvestThrottleStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private readonly SemaphoreSlim _rateGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>Creates a SQLite-backed store at <paramref name="databasePath"/>.</summary>
    public HarvestThrottleStore(string databasePath) : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>Creates a store for the supplied connection descriptor.</summary>
    public HarvestThrottleStore(RelationalDatabaseConnection connectionInfo)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
        if (_connectionInfo.IsSqlite)
        {
            var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }
    }

    /// <summary>Creates the DI store for the shared harvest-state database.</summary>
    public HarvestThrottleStore(IWebHostEnvironment environment) : this(DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection(environment)) { }

    /// <inheritdoc />
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using (var create = connection.CreateCommand())
            {
                create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await using (var seed = connection.CreateCommand())
            {
                seed.CommandText = _connectionInfo.IsPostgres ? PostgresSeedSql : SqliteSeedSql;
                await seed.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            _schemaReady = true;
        }
        finally { _schemaGate.Release(); }
    }

    /// <inheritdoc />
    public async Task<HarvestThrottleSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("SELECT max_requests_per_minute AS MaxRequestsPerMinute, rate_limited_utc AS RateLimitedUtc, updated_utc AS UpdatedUtc FROM harvest_throttle WHERE id = 1;", cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? throw new InvalidOperationException("harvest_throttle seed row (id=1) is missing.") : new(row.MaxRequestsPerMinute, row.RateLimitedUtc, row.UpdatedUtc);
    }

    /// <inheritdoc />
    public async Task SaveRateAsync(int ratePerMinute, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await _rateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            var changed = await connection.ExecuteAsync(new CommandDefinition("UPDATE harvest_throttle SET max_requests_per_minute = @ratePerMinute, updated_utc = @now WHERE id = 1;", new { ratePerMinute, now = now.ToUniversalTime() }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (changed != 1) throw new InvalidOperationException("harvest_throttle seed row (id=1) is missing.");
            ArchidektThrottle.SetRatePerMinute(ratePerMinute);
        }
        finally { _rateGate.Release(); }
    }

    /// <inheritdoc />
    public async Task MarkRateLimitedAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var changed = await connection.ExecuteAsync(new CommandDefinition("UPDATE harvest_throttle SET rate_limited_utc = COALESCE(rate_limited_utc, @now), updated_utc = @now WHERE id = 1;", new { now = now.ToUniversalTime() }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (changed != 1) throw new InvalidOperationException("harvest_throttle seed row (id=1) is missing.");
            // One transaction keeps the marker and both pauses consistent for the banner and schedulers.
            await HarvestScheduleStore.SetPausedInTransactionAsync(connection, transaction, true, now, cancellationToken).ConfigureAwait(false);
            await HarvestUpdateScheduleStore.SetPausedInTransactionAsync(connection, transaction, true, now, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); throw; }
    }

    /// <inheritdoc />
    public async Task<bool> ResumeAfterRateLimitAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var changed = await connection.ExecuteAsync(new CommandDefinition("UPDATE harvest_throttle SET rate_limited_utc = NULL, updated_utc = @now WHERE id = 1 AND rate_limited_utc IS NOT NULL;", new { now = now.ToUniversalTime() }, transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (changed == 0)
            {
                var exists = await connection.ExecuteScalarAsync<long?>(new CommandDefinition("SELECT id FROM harvest_throttle WHERE id = 1;", transaction: transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
                if (exists is null) throw new InvalidOperationException("harvest_throttle seed row (id=1) is missing.");
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }
            await HarvestScheduleStore.SetPausedInTransactionAsync(connection, transaction, false, now, cancellationToken).ConfigureAwait(false);
            await HarvestUpdateScheduleStore.SetPausedInTransactionAsync(connection, transaction, false, now, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); throw; }
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken) => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS harvest_throttle (
          id INT PRIMARY KEY CONSTRAINT ck_harvest_throttle_single_row CHECK (id = 1),
          max_requests_per_minute INT NOT NULL DEFAULT 20 CONSTRAINT ck_harvest_throttle_rate CHECK (max_requests_per_minute IN (5,10,20)),
          rate_limited_utc TIMESTAMPTZ NULL,
          updated_utc TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """;
    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS harvest_throttle (
          id INTEGER PRIMARY KEY CONSTRAINT ck_harvest_throttle_single_row CHECK (id = 1),
          max_requests_per_minute INTEGER NOT NULL DEFAULT 20 CONSTRAINT ck_harvest_throttle_rate CHECK (max_requests_per_minute IN (5,10,20)),
          rate_limited_utc TEXT NULL,
          updated_utc TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;
    private const string PostgresSeedSql = """
        INSERT INTO harvest_throttle (id, max_requests_per_minute, rate_limited_utc, updated_utc)
        VALUES (1, 20, NULL, now()) ON CONFLICT (id) DO NOTHING;
        """;
    private const string SqliteSeedSql = """
        INSERT INTO harvest_throttle (id, max_requests_per_minute, rate_limited_utc, updated_utc)
        VALUES (1, 20, NULL, datetime('now')) ON CONFLICT (id) DO NOTHING;
        """;

    private sealed class Row
    {
        public int MaxRequestsPerMinute { get; init; }
        public DateTimeOffset? RateLimitedUtc { get; init; }
        public DateTimeOffset UpdatedUtc { get; init; }
    }
}
