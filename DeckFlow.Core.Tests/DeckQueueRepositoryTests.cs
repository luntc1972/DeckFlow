using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

/// <summary>
/// SQLite coverage for bounded deck-queue backlog probes.
/// </summary>
public sealed class DeckQueueRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"deck-queue-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task HasMoreThanUnprocessedDecksAsync_UsesExclusiveThresholdAndExcludesTerminalRows()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(new[] { "one", "two" });

        Assert.False(await repository.HasMoreThanUnprocessedDecksAsync(2));
        Assert.True(await repository.HasMoreThanUnprocessedDecksAsync(1));

        await repository.MarkDeckProcessedAsync("one", null, skip: true, metadata: null);
        await repository.MarkDeckProcessedAsync("two", null, skip: false, metadata: null);
        Assert.False(await repository.HasMoreThanUnprocessedDecksAsync(0));
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_databasePath);
        }
    }
}
