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

    [Theory]
    [InlineData("2026-01-02T00:00:05Z", "2026-01-02T00:00:05Z", null, false)]
    [InlineData("2026-01-02T00:00:06Z", "2026-01-02T00:00:05Z", null, true)]
    [InlineData("2026-01-02T00:00:05.900Z", "2026-01-02T00:00:05.100Z", null, false)]
    [InlineData("2026-01-02T00:00:05.999Z", "2026-01-02T00:00:05.000Z", null, false)]
    [InlineData("2026-01-02T00:00:01.100Z", "2026-01-02T00:00:00.900Z", null, true)]
    [InlineData("2026-01-02T00:00:05Z", null, null, true)]
    [InlineData("2026-01-02T00:00:05Z", null, "2026-01-02T00:00:05Z", false)]
    [InlineData("2026-01-02T00:00:07Z", "2026-01-02T00:00:10Z", "2026-01-02T00:00:05Z", false)]
    [InlineData("2026-01-02T00:00:07Z", "2026-01-02T00:00:05Z", "2026-01-02T00:00:10Z", false)]
    [InlineData("2026-01-02T00:00:11Z", "2026-01-02T00:00:05Z", "2026-01-02T00:00:10Z", true)]
    [InlineData("2026-01-02T01:00:06+01:00", "2026-01-02T00:00:05Z", null, true)]
    [InlineData("2026-01-02T01:00:05+01:00", "2026-01-02T00:00:05Z", null, false)]
    [InlineData("2026-01-02T00:00:04Z", "2026-01-02T00:00:05Z", null, false)]
    public void IsNewer_ComparesWholeSecondsAgainstMaxOfStoredAndSeen(string listing, string? stored, string? seen, bool expected)
        => Assert.Equal(expected, DeckQueueRepository.IsNewer(DateTimeOffset.Parse(listing), Parse(stored), Parse(seen)));

    [Theory]
    [InlineData("2026-01-02T00:00:05.0000000+00:00", "2026-01-02T00:00:05.0000000Z")]
    [InlineData("2026-01-02T00:00:05.1234567Z", "2026-01-02T00:00:05.1234567Z")]
    [InlineData("2026-01-02T01:00:05.0000000+01:00", "2026-01-02T00:00:05.0000000Z")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("not-a-date", null)]
    public void ParseStoredUtc_ParsesRoundTripTextAndRejectsBlankOrGarbage(string? input, string? expected)
    {
        var result = DeckQueueRepository.ParseStoredUtc(input);
        Assert.Equal(expected, result?.ToString("O"));
        if (result.HasValue) Assert.Equal(DateTimeKind.Utc, result.Value.Kind);
    }

    [Fact]
    public async Task AddListingRowsAsync_EmptyList_ReturnsZeroAndWritesNothing()
    {
        var (repository, _) = await CreateRepositoryAsync();
        Assert.Equal(new ListingUpsertResult(0, 0), await repository.AddListingRowsAsync(Array.Empty<ArchidektListingDeck>()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddListingRowsAsync(null!));
    }

    [Fact]
    public async Task AddListingRowsAsync_NullUpdatedUtc_InsertsUnseenIdButNeverRequeuesKnownDeck()
    {
        var (repository, _) = await CreateRepositoryAsync();
        Assert.Equal(new ListingUpsertResult(1, 0), await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("null-new", null) }));
        await repository.AddDeckIdsAsync(new[] { "null-old" }); await repository.MarkDeckProcessedAsync("null-old", null);
        Assert.Equal(new ListingUpsertResult(0, 0), await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("null-old", null) }));
    }

    [Fact]
    public async Task AddListingRowsAsync_LegacyNullBaseline_RequeuesExactlyOnce()
    {
        var (repository, _) = await CreateRepositoryAsync(); var time = new DateTimeOffset(2026, 1, 2, 0, 0, 5, TimeSpan.Zero);
        await repository.AddDeckIdsAsync(new[] { "legacy" }); await repository.MarkDeckProcessedAsync("legacy", null);
        Assert.Equal(1, (await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("legacy", time) })).RefreshesRequeued);
        await repository.MarkDeckProcessedAsync("legacy", null); Assert.Equal(0, (await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("legacy", time) })).RefreshesRequeued);
    }

    [Fact]
    public async Task AddListingRowsAsync_WholeSecondAdjacency_RequeuesOnlyTheNextSecond()
    {
        var (repository, _) = await CreateRepositoryAsync(); var time = new DateTimeOffset(2026, 1, 2, 0, 0, 5, TimeSpan.Zero);
        await repository.AddDeckIdsAsync(new[] { "adj" }); await repository.MarkDeckProcessedAsync("adj", null, metadata: Metadata(time));
        Assert.Equal(0, (await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("adj", time.AddMilliseconds(999)) })).RefreshesRequeued);
        Assert.Equal(1, (await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("adj", time.AddSeconds(1)) })).RefreshesRequeued);
    }

    [Fact]
    public async Task AddListingRowsAsync_DuplicateIdsInOneCall_CountOnceAndKeepNewestValue()
    {
        var (repository, databasePath) = await CreateRepositoryAsync(); var time = new DateTimeOffset(2026, 1, 2, 0, 0, 5, TimeSpan.Zero);
        var result = await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("duplicate", time), new ArchidektListingDeck("duplicate", time.AddSeconds(3)) });
        Assert.Equal(new ListingUpsertResult(1, 0), result); Assert.Equal("2026-01-02T00:00:08.0000000+00:00", (await ReadQueueRowAsync(databasePath, "duplicate")).ListingUpdatedSeenUtc);
    }

    [Fact]
    public async Task GetNextRefreshDeckIdsAsync_DrainsRefreshRowsFifoByInsertedUtcThenDeckId()
    {
        var (repository, _) = await CreateRepositoryAsync(); var time = new DateTimeOffset(2026, 1, 2, 0, 0, 5, TimeSpan.Zero);
        await repository.AddDeckIdsAsync(new[] { "rf-a", "rf-b" }); await repository.MarkDeckProcessedAsync("rf-a", null); await repository.MarkDeckProcessedAsync("rf-b", null);
        await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("rf-b", time) }); await Task.Delay(20); await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("rf-a", time) });
        Assert.Equal(new[] { "rf-b", "rf-a" }, await repository.GetNextRefreshDeckIdsAsync(10));
    }

    [Fact]
    public async Task RefreshMarker_ClearsWhenMarkDecksProcessedAsyncProcessesOrSkips()
    {
        var (repository, databasePath) = await CreateRepositoryAsync(); var time = new DateTimeOffset(2026, 1, 2, 0, 0, 5, TimeSpan.Zero);
        await repository.AddDeckIdsAsync(new[] { "bulk-process", "bulk-skip" }); await repository.MarkDeckProcessedAsync("bulk-process", null); await repository.MarkDeckProcessedAsync("bulk-skip", null);
        await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("bulk-process", time), new ArchidektListingDeck("bulk-skip", time) });
        await repository.MarkDecksProcessedAsync(new[] { "bulk-process" }); await repository.MarkDecksProcessedAsync(new[] { "bulk-skip" }, skip: true);
        Assert.Null((await ReadQueueRowAsync(databasePath, "bulk-process")).RefreshRequestedUtc); Assert.Null((await ReadQueueRowAsync(databasePath, "bulk-skip")).RefreshRequestedUtc);
    }

    [Fact]
    public async Task RefreshMarker_ClearsWhenMarkUrlDeckProcessedAsyncLandsTheDeck()
    {
        var (repository, databasePath) = await CreateRepositoryAsync(); var time = new DateTimeOffset(2026, 1, 2, 0, 0, 5, TimeSpan.Zero);
        await repository.AddDeckIdsAsync(new[] { "url" }); await repository.MarkDeckProcessedAsync("url", null); await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("url", time) });
        await repository.MarkUrlDeckProcessedAsync("url", null); var row = await ReadQueueRowAsync(databasePath, "url"); Assert.True(row.Processed); Assert.Null(row.RefreshRequestedUtc);
    }

    [Fact]
    public async Task AddListingRowsAsync_WhitespaceRows_ReturnZero()
    {
        var (repository, _) = await CreateRepositoryAsync();
        Assert.Equal(new ListingUpsertResult(0, 0), await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck(" ", null) }));
    }

    [Fact]
    public async Task AddListingRowsAsync_PendingRow_IsLeftUntouched()
    {
        var (repository, databasePath) = await CreateRepositoryAsync(); var time = new DateTimeOffset(2026, 1, 2, 0, 0, 5, TimeSpan.Zero);
        await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("pending", time) }); var before = await ReadQueueRowAsync(databasePath, "pending");
        Assert.Equal(new ListingUpsertResult(0, 0), await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("pending", time.AddSeconds(5)) }));
        Assert.Equal(before, await ReadQueueRowAsync(databasePath, "pending"));
    }

    [Fact]
    public async Task AddListingRowsAsync_SkippedDeckWithoutMetadata_RequeuesOnceThenSeenBaselineStopsTheLoop()
    {
        var (repository, _) = await CreateRepositoryAsync(); var time = new DateTimeOffset(2026, 1, 2, 0, 0, 5, TimeSpan.Zero);
        await repository.AddDeckIdsAsync(new[] { "skipped" }); await repository.MarkDeckProcessedAsync("skipped", null, skip: true);
        Assert.Equal(1, (await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("skipped", time) })).RefreshesRequeued);
        await repository.MarkDeckProcessedAsync("skipped", null, skip: true);
        Assert.Equal(0, (await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("skipped", time) })).RefreshesRequeued);
    }

    [Fact]
    public async Task GetNextRefreshDeckIdsAsync_ZeroLimit_ReturnsEmpty()
    {
        var (repository, _) = await CreateRepositoryAsync(); Assert.Empty(await repository.GetNextRefreshDeckIdsAsync(0));
    }

    [Fact]
    public async Task Facade_AddListingRowsAndRefreshDrain_RoundTripThroughCategoryKnowledgeRepository()
    {
        var databasePath = Path.Combine(_tempDirectory, "facade.sqlite"); var repository = new CategoryKnowledgeRepository(databasePath);
        await repository.EnsureSchemaAsync(); await repository.AddDeckIdsAsync(new[] { "facade" }); await repository.MarkDeckProcessedAsync("facade", null);
        var result = await repository.AddListingRowsAsync(new[] { new ArchidektListingDeck("facade", new DateTimeOffset(2026, 1, 2, 0, 0, 5, TimeSpan.Zero)) });
        Assert.Equal(new ListingUpsertResult(0, 1), result); Assert.Equal(new[] { "facade" }, await repository.GetNextRefreshDeckIdsAsync(5));
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

    private static DateTime? Parse(string? value) => value is null ? null : DateTimeOffset.Parse(value).UtcDateTime;

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
