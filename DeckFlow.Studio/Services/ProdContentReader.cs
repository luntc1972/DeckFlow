using Dapper;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Npgsql;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Read-only <see cref="IProdContentReader"/> implementation. Builds the production Npgsql
/// connection on-demand from the raw connection string (the same convention as
/// <see cref="ProdStoreFactory"/>) and runs a SINGLE plain <c>SELECT</c> against
/// <c>content_site_index</c> via Dapper. It runs NO <c>EnsureSchemaAsync</c>, NO <c>CREATE</c>/
/// <c>ALTER</c> DDL, and NO information-schema introspection — the production side is structurally
/// read-only (R1). No connection string or exception detail is logged or surfaced here (D-07).
/// </summary>
public sealed class ProdContentReader : IProdContentReader
{
    // Why: matches ContentSiteIndexStore's read column set 1:1 so the materialized rows are identical
    // to GetAllRowsAsync — but with NO EnsureSchemaAsync call (which would run prod DDL). Plain SELECT,
    // no WHERE on any timestamp column, so the F-51-PG-01 timestamptz-vs-text class cannot recur.
    private static readonly string SelectAllSql = $"SELECT {ContentSiteIndexReadColumns.SelectList} FROM content_site_index;";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSiteIndexRow>> ReadAllAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        await using var connection = await OpenProdConnectionAsync(connectionString, cancellationToken).ConfigureAwait(false);

        var rows = await connection.QueryAsync<ContentSiteIndexReadModel>(
            new CommandDefinition(SelectAllSql, cancellationToken: cancellationToken));

        return rows.Select(ContentSiteIndexRowMapper.ToRow).ToList();
    }

    // Why: single plain SELECT, no WHERE on any timestamp column (no F-51-PG-01 exposure), no
    // DDL/EnsureSchema — the feature_flags read-only twin of SelectAllSql (D-04).
    private const string SelectFlagSql = "SELECT enabled FROM feature_flags WHERE key = @key;";

    /// <inheritdoc />
    public async Task<bool> ReadFlagAsync(
        string connectionString,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return await TryReadFlagAsync(connectionString, key, cancellationToken).ConfigureAwait(false) ?? false;
    }

    /// <inheritdoc />
    public async Task<bool?> TryReadFlagAsync(
        string connectionString,
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            await using var connection = await OpenProdConnectionAsync(connectionString, cancellationToken).ConfigureAwait(false);

            var enabled = await connection.QuerySingleOrDefaultAsync<bool?>(
                new CommandDefinition(SelectFlagSql, new { key }, cancellationToken: cancellationToken));

            // A missing row / null enabled is a DEFINITIVE OFF (false), NOT indeterminate — only a
            // caught read failure below returns null. This lets the DirectPush publish gate fail SAFE
            // (verify the deployed body) on a read blip rather than immediate-publishing.
            return enabled ?? false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Indeterminate: the read itself failed. No connection string / exception detail surfaced
            // (D-07). The publish-gate caller treats null as "cannot prove OFF → must verify".
            return null;
        }
    }

    private static async Task<System.Data.Common.DbConnection> OpenProdConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        // Why: built on-demand, never registered live at DI startup — minimizes the always-live
        // surface (D-03). Normalize handles Render's postgresql:// URL form.
        var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);

        // Why: this reader ONLY ever connects to Render-managed prod Postgres, which requires SSL
        // (28000: SSL/TLS required). Force SslMode=Require UNCONDITIONALLY — regardless of the
        // operator's string form (URL or key-value) or any sslmode it already carries. The previous
        // "leave an explicit Disable as the operator set it" escape hatch was the last plaintext
        // path: a key-value string with Ssl Mode=Disable skipped the override and connected
        // unencrypted, which Render rejects with the EndOfStream-during-Authenticate signature.
        // There is no valid plaintext case here (Render mandates SSL), so honoring Disable only ever
        // breaks the pull. In Npgsql 10, SslMode.Require encrypts WITHOUT validating the server
        // certificate chain (libpq semantics), which is exactly what Render's managed endpoint
        // needs — so TrustServerCertificate is unnecessary (it is a no-op / obsolete in v10).
        var builder = new NpgsqlConnectionStringBuilder(normalized)
        {
            SslMode = SslMode.Require
        };

        var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, builder.ConnectionString);
        return await conn.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    }

}
