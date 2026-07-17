using System.Data.Common;
using Dapper;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services;

/// <inheritdoc/>
public sealed class ManabaseBaselineStore : IManabaseBaselineStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly ManabaseBaselineDialect _dialect;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    // Portable across SQLite + Postgres: both support ON CONFLICT ... DO UPDATE SET c = excluded.c.
    private const string UpsertSql = """
        INSERT INTO manabase_baseline
          (commander_slug, bracket, source, avg_lands, avg_ramp, avg_draw, deck_count, computed_utc)
        VALUES
          (@commanderSlug, @bracket, @source, @avgLands, @avgRamp, @avgDraw, @deckCount, @computedUtc)
        ON CONFLICT (commander_slug, bracket, source) DO UPDATE SET
          avg_lands   = excluded.avg_lands,
          avg_ramp    = excluded.avg_ramp,
          avg_draw    = excluded.avg_draw,
          deck_count  = excluded.deck_count,
          computed_utc = excluded.computed_utc;
        """;

    private const string SelectSql = """
        SELECT commander_slug, bracket, source, avg_lands, avg_ramp, avg_draw, deck_count, computed_utc
        FROM manabase_baseline
        WHERE commander_slug = @commanderSlug AND bracket = @bracket;
        """;

    /// <summary>Initializes the store from a SQLite database path.</summary>
    /// <param name="databasePath">Path to the SQLite database file.</param>
    public ManabaseBaselineStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath))
    {
    }

    /// <summary>Initializes the store from a resolved relational connection.</summary>
    /// <param name="connectionInfo">Database provider and connection details.</param>
    public ManabaseBaselineStore(RelationalDatabaseConnection connectionInfo)
    {
        _connectionInfo = connectionInfo;
        _dialect = ManabaseBaselineDialect.For(_connectionInfo);
        if (_connectionInfo.IsSqlite)
        {
            var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    /// <summary>Initializes the store from the web host environment configuration.</summary>
    /// <param name="environment">Web host environment used to resolve the database.</param>
    public ManabaseBaselineStore(IWebHostEnvironment environment)
        : this(DeckFlowDatabaseConnectionFactory.CreateManabaseBaselineConnection(environment))
    {
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(ManabaseBaselineRow row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(UpsertSql, ToParameters(row), cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpsertRangeAsync(IReadOnlyCollection<ManabaseBaselineRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return;
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(UpsertSql, ToParameters(row), transaction, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ManabaseBaselineRow>> GetAsync(string commanderSlug, int bracket, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(commanderSlug);
        await EnsureSchemaAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ManabaseBaselineRow>(new CommandDefinition(
            SelectSql,
            new { commanderSlug, bracket },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    private static object ToParameters(ManabaseBaselineRow row) => new
    {
        commanderSlug = row.CommanderSlug,
        bracket = row.Bracket,
        source = row.Source,
        avgLands = row.AvgLands,
        avgRamp = row.AvgRamp,
        avgDraw = row.AvgDraw,
        deckCount = row.DeckCount,
        computedUtc = row.ComputedUtc,
    };

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionInfo.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (_schemaReady)
        {
            return;
        }

        await _schemaGate.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady)
            {
                return;
            }

            await using var connection = await OpenConnectionAsync(cancellationToken);
            // Why: schema management is an intentional raw ADO.NET carve-out, matching FeedbackStore.
            await using (var create = connection.CreateCommand())
            {
                create.CommandText = """
                    CREATE TABLE IF NOT EXISTS manabase_baseline (
                      commander_slug TEXT    NOT NULL,
                      bracket        INTEGER NOT NULL,
                      source         TEXT    NOT NULL,
                      avg_lands      __REAL_COLUMN_TYPE__ NOT NULL,
                      avg_ramp       __REAL_COLUMN_TYPE__ NOT NULL,
                      avg_draw       __REAL_COLUMN_TYPE__ NOT NULL,
                      deck_count     INTEGER NOT NULL,
                      computed_utc   __COMPUTED_UTC_COLUMN_TYPE__ NOT NULL,
                      PRIMARY KEY (commander_slug, bracket, source)
                    );
                    """;
                create.CommandText = create.CommandText
                    .Replace("__REAL_COLUMN_TYPE__", _dialect.RealColumnType, StringComparison.Ordinal)
                    .Replace("__COMPUTED_UTC_COLUMN_TYPE__", _dialect.ComputedUtcColumnType, StringComparison.Ordinal);
                await create.ExecuteNonQueryAsync(cancellationToken);
            }

            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }
}
