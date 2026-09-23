using Microsoft.Data.Sqlite;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Tests.Knowledge;

public sealed class DeckQueueRepositoryCommanderSummaryTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AddDeckIdsAsync_ReaddingQueuedIdsCountsOnlyNewRows()
    {
        Directory.CreateDirectory(_tempDirectory);
        var databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
        var connectionInfo = RelationalDatabaseConnection.FromSqlitePath(databasePath);
        var schema = new CategoryCacheSchema(connectionInfo, _tempDirectory, logger: null);
        var repository = new DeckQueueRepository(connectionInfo, schema);

        await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2" });

        var enqueued = await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2", "deck-3" });

        Assert.Equal(1, enqueued);
    }

    [Fact]
    public async Task MarkDeckProcessedAsync_UpdatesCommanderSummaryWithNormalizedAggregate()
    {
        Directory.CreateDirectory(_tempDirectory);
        var databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
        var connectionInfo = RelationalDatabaseConnection.FromSqlitePath(databasePath);
        var schema = new CategoryCacheSchema(connectionInfo, _tempDirectory, logger: null);
        var repository = new DeckQueueRepository(connectionInfo, schema);

        await repository.AddDeckIdsAsync(new[] { "deck-1", "deck-2", "deck-3" });
        await repository.MarkDeckProcessedAsync("deck-1", "Kinnan, Bonder Prodigy");
        await repository.MarkDeckProcessedAsync("deck-2", "kinnan, bonder prodigy");
        await repository.MarkDeckProcessedAsync("deck-3", "Tymna the Weaver");

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT deck_count FROM processed_commander_summary WHERE commander_name = 'kinnan, bonder prodigy';";

        Assert.Equal(2L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        Assert.Equal(2, await repository.GetDistinctProcessedCommanderCountAsync());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }
}
