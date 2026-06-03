using System.Data;
using System.Data.Common;
using System.Globalization;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services;

/// <summary>
/// Postgres-backed (or SQLite-backed in tests) per-partition throttle for admin basic-auth
/// brute-force protection (BUG-02 / Phase 5). Single fixed 15-minute window: 10 failures
/// per partition_key trigger 429 with Retry-After until window-end. Lazy expiry — a stale
/// row (window older than 15 min) is reset by the next RecordFailureAsync call.
/// </summary>
public interface IAdminBruteForceTrackerStore
{
    /// <summary>
    /// Returns (true, retryAfterSeconds) if partition_key has &gt;= 10 failures within an
    /// active 15-min window, else (false, 0). Reads atomically; window-expired rows
    /// return (false, 0) and will be reset on the next RecordFailureAsync.
    /// </summary>
    Task<(bool Throttled, int RetryAfterSeconds)> IsThrottledAsync(
        string partitionKey, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the failure count for partition_key. If no row exists, INSERTs with
    /// count=1, window_start=now. If row exists with window expired (now - window_start
    /// &gt;= 15 min), resets count=1, window_start=now. Otherwise increments count, leaves
    /// window_start unchanged.
    /// </summary>
    Task RecordFailureAsync(
        string partitionKey, DateTimeOffset now, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation backed by RelationalDatabaseConnection (Postgres in production,
/// SQLite in tests). Schema lazy-initialized on first call via SemaphoreSlim gate, mirroring
/// FeedbackStore's pattern. UPSERT uses ON CONFLICT(partition_key) DO UPDATE with a CASE
/// expression for atomic lazy-expiry.
/// </summary>
public sealed class AdminBruteForceTrackerStore : IAdminBruteForceTrackerStore
{
    private const int PermitLimit = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly int WindowSeconds = (int)Window.TotalSeconds;

    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Initializes the tracker store using a SQLite database path.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite feedback database.</param>
    public AdminBruteForceTrackerStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Initializes the tracker store using a resolved relational database connection.
    /// </summary>
    /// <param name="connectionInfo">Database provider and connection details for throttle persistence.</param>
    public AdminBruteForceTrackerStore(RelationalDatabaseConnection connectionInfo)
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

    /// <summary>
    /// Initializes the tracker store from the web host environment configuration.
    /// </summary>
    /// <param name="environment">Web host environment used to resolve admin throttle database settings.</param>
    public AdminBruteForceTrackerStore(IWebHostEnvironment environment)
        : this(DeckFlowDatabaseConnectionFactory.CreateAdminThrottleConnection(environment)) { }

    /// <inheritdoc/>
    public async Task<(bool Throttled, int RetryAfterSeconds)> IsThrottledAsync(
        string partitionKey, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(partitionKey);
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count, window_start FROM admin_brute_force_buckets WHERE partition_key = @key";
        RelationalDatabaseConnection.AddParameter(command, "@key", partitionKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (false, 0);
        }

        var count = reader.GetInt32(0);
        var windowStart = ReadTimestamp(reader, 1);
        var elapsed = now - windowStart;
        if (elapsed >= Window)
        {
            return (false, 0);
        }

        if (count >= PermitLimit)
        {
            var remaining = (int)Math.Ceiling((Window - elapsed).TotalSeconds);
            if (remaining < 1) remaining = 1;
            if (remaining > WindowSeconds) remaining = WindowSeconds;
            return (true, remaining);
        }

        return (false, 0);
    }

    /// <inheritdoc/>
    public async Task RecordFailureAsync(
        string partitionKey, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(partitionKey);
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = _connectionInfo.IsPostgres ? PostgresUpsertSql : SqliteUpsertSql;
        RelationalDatabaseConnection.AddParameter(command, "@key", partitionKey);
        RelationalDatabaseConnection.AddParameter(
            command, "@now",
            _connectionInfo.IsPostgres
                ? (object)now.UtcDateTime
                : now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DateTimeOffset ReadTimestamp(DbDataReader reader, int ordinal)
    {
        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
            DateTimeOffset dto => dto.ToUniversalTime(),
            string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime(),
            _ => new DateTimeOffset(Convert.ToDateTime(raw, CultureInfo.InvariantCulture), TimeSpan.Zero)
        };
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionInfo.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady) return;
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var create = connection.CreateCommand();
            create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
            await create.ExecuteNonQueryAsync(cancellationToken);
            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS admin_brute_force_buckets (
          partition_key TEXT PRIMARY KEY,
          count         INT NOT NULL,
          window_start  TIMESTAMPTZ NOT NULL
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS admin_brute_force_buckets (
          partition_key TEXT PRIMARY KEY,
          count         INTEGER NOT NULL,
          window_start  TEXT NOT NULL
        );
        """;

    private const string PostgresUpsertSql = """
        INSERT INTO admin_brute_force_buckets (partition_key, count, window_start)
        VALUES (@key, 1, @now)
        ON CONFLICT(partition_key)
        DO UPDATE SET
            count = CASE
                WHEN @now - admin_brute_force_buckets.window_start >= INTERVAL '15 minutes' THEN 1
                ELSE admin_brute_force_buckets.count + 1
            END,
            window_start = CASE
                WHEN @now - admin_brute_force_buckets.window_start >= INTERVAL '15 minutes' THEN @now
                ELSE admin_brute_force_buckets.window_start
            END;
        """;

    private const string SqliteUpsertSql = """
        INSERT INTO admin_brute_force_buckets (partition_key, count, window_start)
        VALUES (@key, 1, @now)
        ON CONFLICT(partition_key)
        DO UPDATE SET
            count = CASE
                WHEN (julianday(@now) - julianday(admin_brute_force_buckets.window_start)) * 86400 >= 900 THEN 1
                ELSE admin_brute_force_buckets.count + 1
            END,
            window_start = CASE
                WHEN (julianday(@now) - julianday(admin_brute_force_buckets.window_start)) * 86400 >= 900 THEN @now
                ELSE admin_brute_force_buckets.window_start
            END;
        """;
}
