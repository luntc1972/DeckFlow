using System.Data.Common;
using DeckFlow.Core.Storage;
using Npgsql;

namespace DeckFlow.Web.Services.Analytics;

/// <summary>
/// Postgres-only implementation of <see cref="IRequestMetricsStore"/>.
/// Stores per-route/day/status-class hit and error aggregates in
/// <c>request_metrics</c> (D-01) and tracks unique IP hashes per route/day in
/// <c>request_metric_ip_seen</c> (D-03), with no raw IP or PII columns (SC #3).
/// </summary>
/// <remarks>
/// Schema is lazy-initialized via a <see cref="SemaphoreSlim"/> double-check gate,
/// mirroring <c>HarvestRunStore</c> and <c>FeatureFlagStore</c>.
/// Constructor takes an optional <see cref="IServiceProvider"/> per D-14 so the store
/// can be registered without creating a circular DI graph with Wave 2/3 callers
/// (Phase 7.1 dc66a38 errata).
/// When the underlying connection is SQLite (local-dev), <see cref="EnsureSchemaAsync"/>
/// logs a warning and skips DDL — analytics is a paid-tier Postgres-only feature.
/// </remarks>
public sealed class RequestMetricsStore : IRequestMetricsStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly IServiceProvider? _services;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
    /// Intended for test-seam use; analytics DDL will be skipped because the connection
    /// is not Postgres.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file (created if missing).</param>
    /// <param name="services">Optional service provider (D-14 lazy DI pattern).</param>
    public RequestMetricsStore(string databasePath, IServiceProvider? services = null)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath), services) { }

    /// <summary>
    /// Creates a store using the supplied <see cref="RelationalDatabaseConnection"/>
    /// directly. Used by tests or callers that hold a pre-built connection descriptor.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="services">Optional service provider (D-14 lazy DI pattern).</param>
    public RequestMetricsStore(RelationalDatabaseConnection connectionInfo, IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
        _services = services;
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
    /// DI ctor — resolves the connection via
    /// <see cref="DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection"/>,
    /// which shares the harvest/feedback Postgres DB (analytics is co-located per D-01).
    /// </summary>
    /// <param name="environment">Web host environment used by the connection factory.</param>
    /// <param name="services">Optional service provider (D-14 lazy DI pattern).</param>
    public RequestMetricsStore(IWebHostEnvironment environment, IServiceProvider? services = null)
        : this(DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection(environment), services) { }

    /// <inheritdoc />
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaReady)
        {
            return;
        }

        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady)
            {
                return;
            }

            if (!_connectionInfo.IsPostgres)
            {
                // Analytics is Postgres-only (D-01). Local-dev SQLite connections
                // skip DDL gracefully so the app starts without error.
                _schemaReady = true;
                return;
            }

            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            await using (var create = connection.CreateCommand())
            {
                create.CommandText = """
                    CREATE TABLE IF NOT EXISTS request_metrics (
                      route_key    text     NOT NULL,
                      day_utc      date     NOT NULL,
                      status_class smallint NOT NULL,
                      hit_count    bigint   NOT NULL DEFAULT 0,
                      error_count  bigint   NOT NULL DEFAULT 0,
                      PRIMARY KEY (route_key, day_utc, status_class)
                    );
                    CREATE INDEX IF NOT EXISTS ix_request_metrics_day_utc ON request_metrics (day_utc DESC);

                    CREATE TABLE IF NOT EXISTS request_metric_ip_seen (
                      route_key text NOT NULL,
                      day_utc   date NOT NULL,
                      ip_hash   text NOT NULL,
                      PRIMARY KEY (route_key, day_utc, ip_hash)
                    );
                    """;
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpsertBatchAsync(IReadOnlyList<RequestMetricEvent> events, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        if (!_connectionInfo.IsPostgres)
        {
            // No-op on SQLite — analytics is Postgres-only.
            return;
        }

        // Pre-allocate column arrays for unnest UPSERT (Pattern 3 in 08-RESEARCH.md).
        var routeKeys      = new string[events.Count];
        var dayUtcs        = new DateTime[events.Count];
        var statusClasses  = new short[events.Count];
        var errorIncrement = new int[events.Count];
        var ipHashes       = new string?[events.Count];

        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            routeKeys[i]      = e.RouteKey;
            dayUtcs[i]        = e.DayUtc.ToDateTime(TimeOnly.MinValue);
            statusClasses[i]  = e.StatusClass;
            errorIncrement[i] = e.IsError ? 1 : 0;
            ipHashes[i]       = e.IpHash;
        }

        await using var conn = (NpgsqlConnection)await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // (1) request_metrics aggregate UPSERT — increments hit_count and error_count.
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO request_metrics (route_key, day_utc, status_class, hit_count, error_count)
                SELECT u.route_key, u.day_utc, u.status_class, COUNT(*)::bigint, SUM(u.error_inc)::bigint
                  FROM unnest(@routeKeys, @dayUtcs, @statusClasses, @errorInc)
                    AS u(route_key, day_utc, status_class, error_inc)
                 GROUP BY u.route_key, u.day_utc, u.status_class
                ON CONFLICT (route_key, day_utc, status_class) DO UPDATE SET
                  hit_count   = request_metrics.hit_count   + EXCLUDED.hit_count,
                  error_count = request_metrics.error_count + EXCLUDED.error_count;
                """;
            cmd.Parameters.Add(new NpgsqlParameter("routeKeys",     routeKeys));
            cmd.Parameters.Add(new NpgsqlParameter("dayUtcs",       dayUtcs));
            cmd.Parameters.Add(new NpgsqlParameter("statusClasses", statusClasses));
            cmd.Parameters.Add(new NpgsqlParameter("errorInc",      errorIncrement));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // (2) request_metric_ip_seen — INSERT ON CONFLICT DO NOTHING for unique-IP tracking (D-03).
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO request_metric_ip_seen (route_key, day_utc, ip_hash)
                SELECT u.route_key, u.day_utc, u.ip_hash
                  FROM unnest(@routeKeys, @dayUtcs, @ipHashes)
                    AS u(route_key, day_utc, ip_hash)
                 WHERE u.ip_hash IS NOT NULL AND u.ip_hash <> ''
                ON CONFLICT (route_key, day_utc, ip_hash) DO NOTHING;
                """;
            cmd.Parameters.Add(new NpgsqlParameter("routeKeys", routeKeys));
            cmd.Parameters.Add(new NpgsqlParameter("dayUtcs",   dayUtcs));
            cmd.Parameters.Add(new NpgsqlParameter("ipHashes",  ipHashes));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionInfo.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
