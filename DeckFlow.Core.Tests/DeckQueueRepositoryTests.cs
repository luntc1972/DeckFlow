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

    [Fact]
    public async Task MarkDeckProcessedAsync_SkipWithReason_StoresReason()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(["one"]);
        await repository.MarkDeckProcessedAsync("one", null, skip: true, metadata: null, skipReason: DeckSkipReasons.WrongSize);
        Assert.Equal(DeckSkipReasons.WrongSize, await GetSkipReasonAsync("one"));
    }

    [Fact]
    public async Task MarkDeckProcessedAsync_SkipWithoutReason_StoresFetchFailure()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(["one"]);
        await repository.MarkDeckProcessedAsync("one", null, skip: true, metadata: null);
        Assert.Equal(DeckSkipReasons.FetchFailure, await GetSkipReasonAsync("one"));
    }

    [Fact]
    public async Task MarkDeckProcessedAsync_SuccessAfterSkip_ClearsReason()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(["one"]);
        await repository.MarkDeckProcessedAsync("one", null, skip: true, metadata: null, skipReason: DeckSkipReasons.WrongSize);
        await repository.MarkDeckProcessedAsync("one", null, metadata: null);
        Assert.Null(await GetSkipReasonAsync("one"));
    }

    [Fact]
    public async Task MarkDeckProcessedAsync_SuccessWithReason_ThrowsAndWritesNothing()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(["one"]);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.MarkDeckProcessedAsync("one", null, metadata: null, skipReason: DeckSkipReasons.WrongSize));
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT processed FROM deck_queue WHERE deck_id = 'one';"));
    }

    [Fact]
    public async Task MarkDecksProcessedAsync_SkipWithReason_StoresReasonOnEveryDeck()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(["one", "two"]);
        await repository.MarkDecksProcessedAsync(["one", "two"], skip: true, skipReason: DeckSkipReasons.WrongSize);
        Assert.Equal(DeckSkipReasons.WrongSize, await GetSkipReasonAsync("one"));
        Assert.Equal(DeckSkipReasons.WrongSize, await GetSkipReasonAsync("two"));
    }

    [Fact]
    public async Task MarkDecksProcessedAsync_SkipWithoutReason_StoresFetchFailureOnEveryDeck()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(["one", "two"]);
        await repository.MarkDecksProcessedAsync(["one", "two"], skip: true);
        Assert.Equal(DeckSkipReasons.FetchFailure, await GetSkipReasonAsync("one"));
        Assert.Equal(DeckSkipReasons.FetchFailure, await GetSkipReasonAsync("two"));
    }

    [Fact]
    public async Task MarkDecksProcessedAsync_SuccessAfterSkip_ClearsReasonOnEveryDeck()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(["one", "two"]);
        await repository.MarkDecksProcessedAsync(["one", "two"], skip: true, skipReason: DeckSkipReasons.WrongSize);
        await repository.MarkDecksProcessedAsync(["one", "two"]);
        Assert.Null(await GetSkipReasonAsync("one"));
        Assert.Null(await GetSkipReasonAsync("two"));
    }

    [Fact]
    public async Task MarkDecksProcessedAsync_SuccessWithReason_ThrowsAndWritesNothing()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.AddDeckIdsAsync(["one", "two"]);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.MarkDecksProcessedAsync(["one", "two"], skipReason: DeckSkipReasons.WrongSize));
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM deck_queue WHERE processed = 1;"));
    }

    [Fact]
    public async Task EnsureSchema_ExistingDeckQueueWithoutSkipReason_AddsColumn()
    {
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync("CREATE TABLE deck_queue (id INTEGER, deck_id TEXT NOT NULL, inserted_utc TEXT NOT NULL, processed INTEGER NOT NULL DEFAULT 0, skipped INTEGER NOT NULL DEFAULT 0, transient_failure_count INTEGER NOT NULL DEFAULT 0, last_checked_utc TEXT, commander_name TEXT NULL, content_hash TEXT NULL, archidekt_edh_bracket INTEGER NULL, archidekt_deck_format INTEGER NULL, archidekt_theorycrafted INTEGER NULL, archidekt_created_utc TEXT NULL, archidekt_updated_utc TEXT NULL, archidekt_metadata_captured_utc TEXT NULL, refresh_requested_utc TEXT NULL, listing_updated_seen_utc TEXT NULL);");
        }

        var repository = new CategoryKnowledgeRepository(_databasePath);
        await repository.EnsureSchemaAsync();
        await using var verifyConnection = new SqliteConnection($"Data Source={_databasePath}");
        var columns = await verifyConnection.QueryAsync<string>("SELECT name FROM pragma_table_info('deck_queue');");
        Assert.Contains("skip_reason", columns);
    }

    private async Task<string?> GetSkipReasonAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        return await connection.ExecuteScalarAsync<string?>("SELECT skip_reason FROM deck_queue WHERE deck_id = @deckId;", new { deckId });
    }
}
