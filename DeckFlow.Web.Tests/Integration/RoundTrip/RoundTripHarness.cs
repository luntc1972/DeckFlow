using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Tests.Integration.RoundTrip;

/// <summary>
/// Reusable round-trip harness for the SYNC-16 integration test (Plan 93-02): pre-creates the
/// Postgres prod schema once over a Testcontainers connection, then hands out schema-ensure-OFF
/// prod stores and a distinct local (Studio-side) SQLite store over real connections — mirroring
/// the production <c>ProdStoreFactory</c> shape (D-02). Git-repo lifecycle and deploy-copy members
/// land in a later task (D-03/D-04/D-05/D-06). Zero production-code change: every member here
/// only calls existing public constructors and interfaces.
/// </summary>
public sealed class RoundTripHarness : IDisposable
{
    private readonly string _localDbPath;
    private bool _disposed;

    /// <summary>
    /// Creates a harness instance with a fresh, uniquely-named local SQLite database path for
    /// this test's lifetime.
    /// </summary>
    public RoundTripHarness()
    {
        _localDbPath = Path.Combine(Path.GetTempPath(), $"roundtrip-local-{Guid.NewGuid():N}.db");
    }

    /// <summary>Gets the local (Studio-side) SQLite database file path this harness instance owns.</summary>
    public string LocalDbPath => _localDbPath;

    /// <summary>
    /// Builds a Postgres <see cref="RelationalDatabaseConnection"/> descriptor from a raw
    /// connection string (mirrors <c>PostgresStorageTests.CreateConnection</c>).
    /// </summary>
    /// <param name="connectionString">Raw Postgres connection string.</param>
    /// <returns>A Postgres-provider connection descriptor.</returns>
    public static RelationalDatabaseConnection CreateConnection(string connectionString)
        => new(RelationalDatabaseProvider.Postgres, connectionString);

    /// <summary>
    /// Pre-creates the <c>content_site_index</c> schema ONCE over <paramref name="connectionString"/>
    /// by constructing a schema-ensuring <see cref="ContentSiteIndexStore"/> and calling
    /// <see cref="ContentSiteIndexStore.EnsureSchemaAsync"/> — the production
    /// <c>ProdStoreFactory</c> store runs schema-ensure OFF (D-10), so the schema must already
    /// exist before <see cref="CreateProdStore"/> is used, exactly as the web app's startup path
    /// owns prod schema in production.
    /// </summary>
    /// <param name="connectionString">Raw Postgres connection string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureProdSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var schemaEnsuringStore = new ContentSiteIndexStore(CreateConnection(connectionString), ensureSchemaEnabled: true);
        await schemaEnsuringStore.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the prod (Postgres, schema-ensure OFF) content-site-index store — the exact shape
    /// <c>ProdStoreFactory.Create</c> uses in production (D-02).
    /// </summary>
    /// <param name="connectionString">Raw Postgres connection string.</param>
    /// <returns>A schema-ensure-OFF Postgres-backed store.</returns>
    public IContentSiteIndexStore CreateProdStore(string connectionString)
        => new ContentSiteIndexStore(CreateConnection(connectionString), ensureSchemaEnabled: false);

    /// <summary>
    /// Builds the local (Studio-side) SQLite content-site-index store this harness instance owns —
    /// the distill + Publish-export SOURCE, distinct from <see cref="CreateProdStore"/> (D-02a).
    /// </summary>
    /// <returns>A SQLite-backed store over this harness's local database file.</returns>
    public IContentSiteIndexStore CreateLocalStore()
        => new ContentSiteIndexStore(_localDbPath);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Why: release SQLite file handles before deleting so the temp .db file isn't left locked.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_localDbPath))
        {
            File.Delete(_localDbPath);
        }
    }
}
