using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for toggling Content KB source enabled state.
/// </summary>
public sealed class ContentSourceStoreSetEnabledTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSourceStore _store;

    public ContentSourceStoreSetEnabledTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-source-enabled-{Guid.NewGuid():N}.db");
        _store = new ContentSourceStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task SetEnabledAsync_TogglesSourceInListEnabledSources()
    {
        var sourceId = await _store.InsertSourceAsync(
            "command-zone",
            "The Command Zone",
            ContentSourceType.Youtube,
            "https://www.youtube.com/@commandzone");

        Assert.Contains(await _store.ListEnabledSourcesAsync(), source => source.Id == sourceId);

        await _store.SetEnabledAsync(sourceId, false);

        var disabledSource = await _store.GetSourceAsync(sourceId);
        Assert.NotNull(disabledSource);
        Assert.False(disabledSource!.IsEnabled);
        Assert.DoesNotContain(await _store.ListEnabledSourcesAsync(), source => source.Id == sourceId);

        await _store.SetEnabledAsync(sourceId, true);

        var enabledSource = await _store.GetSourceAsync(sourceId);
        Assert.NotNull(enabledSource);
        Assert.True(enabledSource!.IsEnabled);
        Assert.Contains(await _store.ListEnabledSourcesAsync(), source => source.Id == sourceId);
    }
}
