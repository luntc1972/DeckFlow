using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;
using Microsoft.Extensions.Configuration;

namespace DeckFlow.Web.Tests.Integration.RoundTrip;

/// <summary>
/// Reusable round-trip harness for the SYNC-16 integration test (Plan 93-02): pre-creates the
/// Postgres prod schema once over a Testcontainers connection, hands out schema-ensure-OFF prod
/// stores and a distinct local (Studio-side) SQLite store over real connections (D-02), drives a
/// deploy-copies the private-root tree into distinct <c>/app</c> and <c>/data</c> stand-in directories.
/// </summary>
public sealed class RoundTripHarness : IDisposable
{
    private static void ClearPool(string path) => Microsoft.Data.Sqlite.SqliteConnection.ClearPool(new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    private readonly string _localDbPath;
    private bool _disposed;

    /// <summary>
    /// Creates a harness instance with fresh, uniquely-named temp paths (local SQLite database,
    /// private-root, deploy-copy <c>/app</c> stand-in, and data root) for this test's lifetime.
    /// </summary>
    public RoundTripHarness()
    {
        var stamp = Guid.NewGuid().ToString("N");
        _localDbPath = Path.Combine(Path.GetTempPath(), $"roundtrip-local-{stamp}.db");
        RepoRoot = Path.Combine(Path.GetTempPath(), $"roundtrip-repo-{stamp}");
        AppRoot = Path.Combine(Path.GetTempPath(), $"roundtrip-app-{stamp}");
        DataRoot = Path.Combine(Path.GetTempPath(), $"roundtrip-data-{stamp}");
    }

    /// <summary>Gets the local (Studio-side) SQLite database file path this harness instance owns.</summary>
    public string LocalDbPath => _localDbPath;

    /// <summary>Gets the temporary private-root directory this harness drives.</summary>
    public string RepoRoot { get; }

    /// <summary>Gets the distinct deploy-copy stand-in directory simulating Render's <c>/app</c> checkout.</summary>
    public string AppRoot { get; }

    /// <summary>Gets the distinct data-overlay stand-in directory simulating Render's <c>/data</c>.</summary>
    public string DataRoot { get; }


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

    /// <summary>
    /// Creates the private-root, app, and data directories used by the round-trip scenario.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task InitRepoAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.Combine(RepoRoot, "content-kb", "seed"));
        Directory.CreateDirectory(AppRoot);
        Directory.CreateDirectory(DataRoot);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Copies the private-root <c>content-kb/**</c> tree from <see cref="RepoRoot"/> into the distinct
    /// <see cref="AppRoot"/> stand-in directory to simulate deployment.
    /// </summary>
    public Task DeployToAppAsync()
    {
        var sourceRoot = Path.Combine(RepoRoot, "content-kb");
        var targetRoot = Path.Combine(AppRoot, "content-kb");
        if (Directory.Exists(sourceRoot))
        {
            CopyDirectoryRecursive(sourceRoot, targetRoot);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds an in-memory <see cref="IConfiguration"/> pointing <c>ContentKb:ContentBase</c> at
    /// <see cref="AppRoot"/> and <c>MTG_DATA_DIR</c> at <see cref="DataRoot"/> so web body resolution
    /// reads its <c>/app</c> root and <c>/data</c> overlay, with a
    /// fixture-ignored <c>Studio:ProdConnectionString</c> placeholder (mirrors
    /// <c>ReconcileFixtureDriveTests</c>'s configuration construction).
    /// </summary>
    /// <returns>An in-memory configuration for the round-trip coordinators.</returns>
    public IConfiguration BuildConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ContentKb:ContentBase"] = AppRoot,
                ["MTG_DATA_DIR"] = DataRoot,
                ["Studio:ProdConnectionString"] = "fixture-ignored",
            })
            .Build();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Why: release SQLite file handles before deleting so the temp .db file isn't left locked.
        ClearPool(_localDbPath);
        if (File.Exists(_localDbPath))
        {
            File.Delete(_localDbPath);
        }

        foreach (var directory in new[] { RepoRoot, AppRoot, DataRoot })
        {
            ForceDeleteDirectory(directory);
        }
    }

    private static void ForceDeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(filePath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
            }
        }

        Directory.Delete(directory, recursive: true);
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var filePath in Directory.EnumerateFiles(sourceDir))
        {
            File.Copy(filePath, Path.Combine(targetDir, Path.GetFileName(filePath)), overwrite: true);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDir))
        {
            CopyDirectoryRecursive(directoryPath, Path.Combine(targetDir, Path.GetFileName(directoryPath)));
        }
    }

}
