using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;
using DeckFlow.Studio.Services;
using Xunit;

namespace DeckFlow.Studio.Tests;

public sealed class CreatorSuppressionSyncTests : IDisposable
{
    private readonly string _localPath = Path.Combine(Path.GetTempPath(), $"studio-sync-local-{Guid.NewGuid():N}.db");
    private readonly string _prodPath = Path.Combine(Path.GetTempPath(), $"studio-sync-prod-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task SyncStudioPull_PullFromProdAppliesSnapshotEndToEnd()
    {
        var local = Store(_localPath); var prod = Store(_prodPath);
        await prod.SuppressAsync("alpha", new[] { "Alpha" }, "request", DateTimeOffset.UtcNow, null);
        await CreateCoordinator(local, prod).EnsureCurrentAsync();
        Assert.Equal("alpha", Assert.Single(await local.ListAsync()).Slug);
    }

    [Fact]
    public async Task SyncStudioRunStartRepull_RunStartRepullsWhenRevisionDiffersAndNotWhenEqual()
    {
        var local = Store(_localPath); var prod = Store(_prodPath); var coordinator = CreateCoordinator(local, prod);
        await coordinator.EnsureCurrentAsync();
        var revision = await local.GetSyncedRevisionAsync();
        await coordinator.EnsureCurrentAsync();
        Assert.Equal(revision, await local.GetSyncedRevisionAsync());
        await prod.SuppressAsync("alpha", Array.Empty<string>(), "request", DateTimeOffset.UtcNow, null);
        await coordinator.EnsureCurrentAsync();
        Assert.Equal("alpha", Assert.Single(await local.ListAsync()).Slug);
    }

    public void Dispose()
    {
        ClearPool(_localPath); ClearPool(_prodPath); if (File.Exists(_localPath)) File.Delete(_localPath); if (File.Exists(_prodPath)) File.Delete(_prodPath);
    }

    private static void ClearPool(string path) => Microsoft.Data.Sqlite.SqliteConnection.ClearPool(new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    private static CreatorSuppressionStore Store(string path) => new(RelationalDatabaseConnection.FromSqlitePath(path));
    private static CreatorSuppressionSyncCoordinator CreateCoordinator(ICreatorSuppressionStore local, ICreatorSuppressionStore prod) => new(local, new Factory(prod), new Connection());
    private sealed class Connection : IStudioProdConnectionSource { public string ConnectionString => "test"; }
    private sealed class Factory(ICreatorSuppressionStore suppressionStore) : IProdStoreFactory
    {
        public IContentSiteIndexStore Create(string connectionString) => throw new NotSupportedException();
        public ICreatorSuppressionStore CreateSuppression(string connectionString) => suppressionStore;
    }
}
