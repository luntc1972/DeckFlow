using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;

namespace DeckFlow.Studio.Services;

/// <summary>
/// Creates an on-demand prod <see cref="IContentSiteIndexStore"/> from a connection string.
/// </summary>
public interface IProdStoreFactory
{
    /// <summary>Builds a Postgres-backed store from <paramref name="connectionString"/>.</summary>
    /// <param name="connectionString">Raw prod Postgres connection string (URL or key-value form).</param>
    /// <returns>A Postgres-backed <see cref="IContentSiteIndexStore"/>.</returns>
    IContentSiteIndexStore Create(string connectionString);
}

/// <summary>Production implementation that wires the Postgres dialect.</summary>
public sealed class ProdStoreFactory : IProdStoreFactory
{
    /// <inheritdoc />
    public IContentSiteIndexStore Create(string connectionString)
    {
        // Why: built on-demand inside the publish action, never registered with a live
        // connection at DI startup (D-03) — this minimizes the always-live accidental-write
        // surface. Normalize handles the postgresql:// URL form from Render DATABASE_URL.
        var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);
        var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, normalized);
        // Why: every prod-pointed store disables schema-ensure (D-10) so Studio NEVER issues
        // CREATE/ALTER/DROP against prod on reads OR writes — prod schema is owned by the web app's
        // startup/seed path (SYNC-06). The zero-DDL invariant is locked by the recording-connection
        // test in Plan 88-01 Task 3; this factory is the only prod-store construction site.
        return new ContentSiteIndexStore(conn, ensureSchemaEnabled: false);
    }
}
