using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using Dapper;
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

    [Fact]
    public async Task HasMoreThanUnprocessedDecksAsync_ExcludesRefreshRows()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(["refresh-only"]);
        await repository.MarkDeckProcessedAsync("refresh-only", null, metadata: null);
        await repository.AddListingRowsAsync([new ArchidektListingDeck("refresh-only", DateTimeOffset.UtcNow)]);

        Assert.False(await repository.HasMoreThanUnprocessedDecksAsync(0));
    }

    [Fact]
    public async Task RecordTransientDeckFailureAsync_SkipsDeckAfterThirdFailureAndSuccessResetsCounter()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        await repository.AddDeckIdsAsync(["transient"]);

        Assert.False(await repository.RecordTransientDeckFailureAsync("transient"));
        Assert.False(await repository.RecordTransientDeckFailureAsync("transient"));
        await repository.MarkDeckProcessedAsync("transient", null, skip: false, metadata: null);

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var failures = await connection.ExecuteScalarAsync<long>("SELECT transient_failure_count FROM deck_queue WHERE deck_id = 'transient';");
        Assert.Equal(0, failures);

        await repository.AddDeckIdsAsync(["skipped"]);
        Assert.False(await repository.RecordTransientDeckFailureAsync("skipped"));
        Assert.False(await repository.RecordTransientDeckFailureAsync("skipped"));
        Assert.True(await repository.RecordTransientDeckFailureAsync("skipped"));
        Assert.Empty(await repository.GetNextUnprocessedDeckIdsAsync(1));
    }

    [Fact]
    public async Task AddListingRowsAsync_RequeuedDeck_ResetsTransientFailureCount()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        var initialUpdatedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        await repository.AddListingRowsAsync([new ArchidektListingDeck("refresh-transient", initialUpdatedUtc)]);
        await repository.MarkDeckProcessedAsync("refresh-transient", null, metadata: null);
        await using (var setupConnection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await setupConnection.OpenAsync();
            await setupConnection.ExecuteAsync("UPDATE deck_queue SET transient_failure_count = 2 WHERE deck_id = 'refresh-transient';");
        }

        var result = await repository.AddListingRowsAsync([new ArchidektListingDeck("refresh-transient", initialUpdatedUtc.AddMinutes(1))]);

        Assert.Equal(1, result.RefreshesRequeued);
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        var failures = await connection.ExecuteScalarAsync<long>("SELECT transient_failure_count FROM deck_queue WHERE deck_id = 'refresh-transient';");
        Assert.Equal(0, failures);
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
