using System.Data.Common;
using System.Globalization;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default implementation of <see cref="IWhisperSpendLedger"/> backed by the local Content KB database.
/// </summary>
public sealed class WhisperSpendLedger : IWhisperSpendLedger
{
    private const string MonthlyCapConfigurationKey = "DECKFLOW_WHISPER_MONTHLY_CAP_USD";
    private static readonly decimal DefaultMonthlyCapUsd = 15.00m;

    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly Func<string, string?>? _configurationValueResolver;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed spend ledger using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    /// <param name="configurationValueResolver">Optional configuration value resolver for the monthly spend cap.</param>
    public WhisperSpendLedger(string databasePath, Func<string, string?>? configurationValueResolver = null)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath), configurationValueResolver) { }

    /// <summary>
    /// Creates a spend ledger using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="configurationValueResolver">Optional configuration value resolver for the monthly spend cap.</param>
    public WhisperSpendLedger(RelationalDatabaseConnection connectionInfo, Func<string, string?>? configurationValueResolver = null)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
        _configurationValueResolver = configurationValueResolver;
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
    /// Ensures the spend ledger schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;

            // Why: REVIEW #1 requires content_videos to exist before the spend ledger
            // declares its FK parent, and Postgres rejects FKs to missing parent tables.
            var videoStore = new ContentVideoStore(_connectionInfo);
            await videoStore.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
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
    public async Task RecordCallAsync(
        long videoId,
        int secondsBilled,
        decimal costUsd,
        string monthKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(videoId, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(secondsBilled);
        ArgumentOutOfRangeException.ThrowIfNegative(costUsd);
        ArgumentException.ThrowIfNullOrWhiteSpace(monthKey);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO whisper_spend_ledger (
              video_id,
              seconds_billed,
              cost_usd,
              month_key,
              created_utc)
            VALUES (
              @videoId,
              @secondsBilled,
              @costUsd,
              @monthKey,
              @createdUtc);
            """;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);
        RelationalDatabaseConnection.AddParameter(command, "@secondsBilled", secondsBilled);
        RelationalDatabaseConnection.AddParameter(command, "@costUsd", FormatDecimal(costUsd));
        RelationalDatabaseConnection.AddParameter(command, "@monthKey", monthKey);
        RelationalDatabaseConnection.AddParameter(command, "@createdUtc", FormatTimestamp(DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT cost_usd
              FROM whisper_spend_ledger
             WHERE month_key = @monthKey;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@monthKey", yearMonth);

        var total = 0m;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            total += ReadDecimal(reader, 0);
        }

        return total;
    }

    /// <inheritdoc />
    public async Task<bool> WouldExceedCapAsync(
        decimal projectedCallCostUsd,
        string monthKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(projectedCallCostUsd);
        ArgumentException.ThrowIfNullOrWhiteSpace(monthKey);

        var total = await GetMonthlyTotalAsync(monthKey, cancellationToken).ConfigureAwait(false);
        var cap = ReadMonthlyCapUsd();
        return total + projectedCallCostUsd > cap;
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    private object FormatDecimal(decimal value)
        => _connectionInfo.IsPostgres ? value : value.ToString(CultureInfo.InvariantCulture);

    private object FormatTimestamp(DateTimeOffset value)
        => _connectionInfo.IsPostgres
            ? value.UtcDateTime
            : value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private decimal ReadMonthlyCapUsd()
    {
        var configured = _configurationValueResolver?.Invoke(MonthlyCapConfigurationKey);
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Environment.GetEnvironmentVariable(MonthlyCapConfigurationKey);
        }

        if (!string.IsNullOrWhiteSpace(configured)
            && decimal.TryParse(configured, NumberStyles.Number, CultureInfo.InvariantCulture, out var cap)
            && cap >= 0m)
        {
            return cap;
        }

        return DefaultMonthlyCapUsd;
    }

    private static decimal ReadDecimal(DbDataReader reader, int ordinal)
    {
        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            decimal d => d,
            double d => Convert.ToDecimal(d, CultureInfo.InvariantCulture),
            float f => Convert.ToDecimal(f, CultureInfo.InvariantCulture),
            string text => decimal.Parse(text, CultureInfo.InvariantCulture),
            _ => Convert.ToDecimal(raw, CultureInfo.InvariantCulture)
        };
    }

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS whisper_spend_ledger (
          id             BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          video_id       BIGINT NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          seconds_billed INT NOT NULL,
          cost_usd       DECIMAL(10,6) NOT NULL,
          month_key      TEXT NOT NULL,
          created_utc    TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        CREATE INDEX IF NOT EXISTS ix_spend_month ON whisper_spend_ledger(month_key);
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS whisper_spend_ledger (
          id             INTEGER PRIMARY KEY AUTOINCREMENT,
          video_id       INTEGER NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          seconds_billed INTEGER NOT NULL,
          cost_usd       TEXT NOT NULL,
          month_key      TEXT NOT NULL,
          created_utc    TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE INDEX IF NOT EXISTS ix_spend_month ON whisper_spend_ledger(month_key);
        """;
}
