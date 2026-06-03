using System.Data.Common;
using System.Globalization;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Shared persistence skeleton for the per-call spend ledgers (<see cref="WhisperSpendLedger"/> and
/// <see cref="LlmSpendLedger"/>): schema bootstrap gating, monthly-total aggregation, cap checks,
/// and the SQLite/Postgres value formatting rules. Derived ledgers supply only their table name,
/// cap configuration key, DDL, and call-recording column set.
/// </summary>
public abstract class SpendLedgerBase
{
    private static readonly decimal DefaultMonthlyCapUsd = 15.00m;

    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly Func<string, string?>? _configurationValueResolver;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a spend ledger using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="configurationValueResolver">Optional configuration value resolver for the monthly spend cap.</param>
    protected SpendLedgerBase(RelationalDatabaseConnection connectionInfo, Func<string, string?>? configurationValueResolver)
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

    /// <summary>Ledger table name interpolated into the shared monthly-total query.</summary>
    protected abstract string TableName { get; }

    /// <summary>Configuration/environment key holding the monthly USD cap for this ledger.</summary>
    protected abstract string MonthlyCapConfigurationKey { get; }

    /// <summary>Postgres CREATE TABLE/INDEX DDL for this ledger.</summary>
    protected abstract string PostgresCreateTableSql { get; }

    /// <summary>SQLite CREATE TABLE/INDEX DDL for this ledger.</summary>
    protected abstract string SqliteCreateTableSql { get; }

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

    /// <summary>
    /// Sums recorded call costs for the given <c>yyyy-MM</c> month key.
    /// </summary>
    /// <param name="yearMonth">Month key in <c>yyyy-MM</c> form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total recorded USD cost for the month.</returns>
    public async Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yearMonth);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT cost_usd
              FROM {TableName}
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

    /// <summary>
    /// Returns true when adding the projected call cost to the month's total would exceed the configured cap.
    /// </summary>
    /// <param name="projectedCallCostUsd">Projected USD cost of the next call.</param>
    /// <param name="monthKey">Month key in <c>yyyy-MM</c> form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the projected total exceeds the cap.</returns>
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

    /// <summary>Opens a connection using the ledger's provider descriptor.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open connection.</returns>
    protected async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Formats a decimal for the active provider (native decimal on Postgres, invariant string on SQLite).</summary>
    /// <param name="value">Decimal value to format.</param>
    /// <returns>Provider-appropriate parameter value.</returns>
    protected object FormatDecimal(decimal value)
        => _connectionInfo.IsPostgres ? value : value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Formats a timestamp for the active provider (UTC DateTime on Postgres, round-trip string on SQLite).</summary>
    /// <param name="value">Timestamp to format.</param>
    /// <returns>Provider-appropriate parameter value.</returns>
    protected object FormatTimestamp(DateTimeOffset value)
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
}
