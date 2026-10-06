using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Verifies listing-driven queue refresh behavior.
/// </summary>
public sealed class DeckQueueRepositoryRefreshTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));

    // Why: D-01 through D-04 and HARV-10 require listing changes to refresh terminal decks without moving pending FIFO work.
    [Fact]
    public async Task AddListingRowsAsync_KnownProcessedDeckWithNewerListing_RequeuesDrainsAsRefreshAndClearsOnProcessed()
    {
        var (repository, databasePath) = await CreateRepositoryAsync();
        var original = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await repository.AddDeckIdsAsync(new[] { "tracer-1" });
        await repository.MarkDeckProcessedAsync("tracer-1", null, metadata: Metadata(original));

        var result = await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("tracer-1", original.AddSeconds(1.25)) });

        Assert.Equal(0, result.NewIds);
        Assert.Equal(1, result.RefreshesRequeued);
        var row = await ReadQueueRowAsync(databasePath, "tracer-1");
        Assert.False(row.Processed);
        Assert.False(row.Skipped);
        Assert.NotNull(row.RefreshRequestedUtc);
        Assert.Equal("2026-01-01T00:00:01.0000000+00:00", row.ListingUpdatedSeenUtc);
        Assert.Equal(new[] { "tracer-1" }, await repository.GetNextRefreshDeckIdsAsync(10));

        await repository.MarkDeckProcessedAsync("tracer-1", null, metadata: Metadata(original.AddSeconds(1)));
        row = await ReadQueueRowAsync(databasePath, "tracer-1");
        Assert.True(row.Processed);
        Assert.Null(row.RefreshRequestedUtc);
        Assert.Empty(await repository.GetNextRefreshDeckIdsAsync(10));

        result = await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("tracer-1", original.AddSeconds(1.25)) });
        Assert.Equal(0, result.NewIds);
        Assert.Equal(0, result.RefreshesRequeued);
    }

    [Fact]
    public async Task AddListingRowsAsync_UnseenId_InsertsPendingRowWithoutRefreshMarker()
    {
        var (repository, databasePath) = await CreateRepositoryAsync();

        var result = await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("tracer-new", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)) });

        Assert.Equal(1, result.NewIds);
        Assert.Equal(0, result.RefreshesRequeued);
        var row = await ReadQueueRowAsync(databasePath, "tracer-new");
        Assert.False(row.Processed);
        Assert.False(row.Skipped);
        Assert.Null(row.RefreshRequestedUtc);
        Assert.Equal("2026-01-01T00:00:00.0000000+00:00", row.ListingUpdatedSeenUtc);
        Assert.Contains("tracer-new", await repository.GetNextUnprocessedDeckIdsAsync(10));
        Assert.Empty(await repository.GetNextRefreshDeckIdsAsync(10));
    }

    private async Task<(DeckQueueRepository Repository, string DatabasePath)> CreateRepositoryAsync()
    {
        Directory.CreateDirectory(_tempDirectory);
        var databasePath = Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}.db");
        var connectionInfo = RelationalDatabaseConnection.FromSqlitePath(databasePath);
        var schema = new CategoryCacheSchema(connectionInfo, _tempDirectory, logger: null);
        await schema.EnsureSchemaAsync();
        return (new DeckQueueRepository(connectionInfo, schema), databasePath);
    }

    private static ArchidektDeckMetadata Metadata(DateTimeOffset updatedUtc)
        => new(null, null, null, null, updatedUtc, updatedUtc);

    private static async Task<QueueRow> ReadQueueRowAsync(string databasePath, string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT processed, skipped, refresh_requested_utc, listing_updated_seen_utc FROM deck_queue WHERE deck_id = $deckId;";
        command.Parameters.AddWithValue("$deckId", deckId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new(reader.GetBoolean(0), reader.GetBoolean(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3));
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

    private sealed record QueueRow(bool Processed, bool Skipped, string? RefreshRequestedUtc, string? ListingUpdatedSeenUtc);
}
