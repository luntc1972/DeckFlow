using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Specifies batch retention pruning of stored decks.
/// </summary>
public sealed class DeckPruningServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public DeckPruningServiceTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    [Fact]
    public async Task RunRetentionAsync_CommanderWith101Decks_RemovesExactlyOldest()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var commander = "Atraxa, Praetors' Voice";
        var baseUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var index = 0; index <= 100; index++)
        {
            await SeedStoredDeckAsync(repository, $"deck-{index:D3}", commander, baseUtc.AddDays(index));
        }

        var service = new DeckPruningService(
            repository,
            new DeckPruningOptions { PerCommanderCap = 100, BatchSize = 25, Pause = TimeSpan.Zero },
            static _ => Task.FromResult<long?>(10L * 1024 * 1024 * 1024));

        var result = await service.RunRetentionAsync();

        Assert.Equal(1, result.DecksRemoved);
        Assert.Equal(DeckPruningStopReason.Completed, result.StopReason);
        Assert.Equal(1, result.Batches);
        Assert.Equal(DeckSkipReasons.Retention, await SkipReasonAsync("deck-000"));
        Assert.Equal(100, await repository.GetCommanderDeckCountAsync(commander));
        Assert.Equal(0L, await CountAsync("card_category_observations", "source_id = 'archidekt_live:deck-000'"));
        Assert.Equal(0L, await CountAsync("card_deck_totals", "source_id = 'archidekt_live:deck-000'"));
        await AssertSummaryMatchesRecomputeAsync("card_category_summary", 0);
        await AssertSummaryMatchesRecomputeAsync("card_category_qualified", 5);
    }

    [Fact]
    public void DeckPruningOptions_Defaults_MatchOq6()
    {
        var options = new DeckPruningOptions();

        Assert.Equal(100, options.PerCommanderCap);
        Assert.Equal(25, options.BatchSize);
        Assert.Equal(TimeSpan.FromMilliseconds(500), options.Pause);
        Assert.Equal(1_073_741_824L, options.MinFreeDiskBytes);
        Assert.Null(options.MaxAge);
    }

    private async Task SeedStoredDeckAsync(CategoryKnowledgeRepository repository, string deckId, string commander, DateTimeOffset updatedUtc)
    {
        await repository.AddDeckIdsAsync([deckId]);
        await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(repository, "archidekt_live:" + deckId, [new DeckEntry { Name = "Sol Ring", NormalizedName = CardNormalizer.Normalize("Sol Ring"), Quantity = 1, Board = "mainboard", Category = "Ramp" }]);
        await repository.MarkDeckProcessedAsync(deckId, commander, skip: false, new ArchidektDeckMetadata(null, 3, false, null, updatedUtc, DateTimeOffset.UtcNow));
    }

    private async Task<string?> SkipReasonAsync(string deckId)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT skip_reason FROM deck_queue WHERE deck_id = @deckId";
        command.Parameters.AddWithValue("@deckId", deckId);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : (string?)value;
    }

    private async Task<long> CountAsync(string table, string predicate)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM {table} WHERE {predicate}";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task AssertSummaryMatchesRecomputeAsync(string table, int minimumRows)
    {
        var expected = $"SELECT card_id, category, COUNT(1) AS observation_rows FROM card_category_observations GROUP BY card_id, category {(minimumRows == 0 ? string.Empty : $"HAVING COUNT(1) >= {minimumRows}")}";
        var actual = $"SELECT card_id, category, observation_rows FROM {table}";
        Assert.Equal(0L, await ScalarAsync($"SELECT COUNT(1) FROM ({expected} EXCEPT {actual})"));
        Assert.Equal(0L, await ScalarAsync($"SELECT COUNT(1) FROM ({actual} EXCEPT {expected})"));
    }

    private async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task RunRetentionAsync_ProbeBelowFloor_RemovesNothing()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "floor", "A", DateTimeOffset.UtcNow);
        await SeedStoredDeckAsync(repository, "floor-candidate", "A", DateTimeOffset.UtcNow.AddMinutes(1));
        var result = await CreateService(repository, new DeckPruningOptions { PerCommanderCap = 1 }, [1L]).RunRetentionAsync();
        Assert.Equal(DeckPruningStopReason.DiskFloor, result.StopReason);
        Assert.Equal(0, result.DecksRemoved);
        Assert.Null(await SkipReasonAsync("floor"));
    }

    [Fact]
    public async Task RunRetentionAsync_ProbeReturnsNull_RemovesNothing()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "null", "A", DateTimeOffset.UtcNow);
        await SeedStoredDeckAsync(repository, "null-candidate", "A", DateTimeOffset.UtcNow.AddMinutes(1));
        var result = await CreateService(repository, new DeckPruningOptions { PerCommanderCap = 1 }, [null]).RunRetentionAsync();
        Assert.Equal(DeckPruningStopReason.DiskProbeUnavailable, result.StopReason);
        Assert.Equal(0, result.DecksRemoved);
    }

    [Fact]
    public async Task RunRetentionAsync_ProbeThrows_StopsAsUnavailable()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "throws", "A", DateTimeOffset.UtcNow);
        await SeedStoredDeckAsync(repository, "throws-candidate", "A", DateTimeOffset.UtcNow.AddMinutes(1));
        var service = new DeckPruningService(repository, new DeckPruningOptions { PerCommanderCap = 1 }, static _ => throw new InvalidOperationException());
        var result = await service.RunRetentionAsync();
        Assert.Equal(DeckPruningStopReason.DiskProbeUnavailable, result.StopReason);
        Assert.Equal(0, result.DecksRemoved);
    }

    [Fact]
    public async Task RunRetentionAsync_FloorCrossedAfterFirstBatch_StopsAfterOneBatch()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        for (var i = 0; i < 4; i++) await SeedStoredDeckAsync(repository, $"cross-{i}", "A", DateTimeOffset.UtcNow.AddDays(i));
        var result = await CreateService(repository, new DeckPruningOptions { PerCommanderCap = 1, BatchSize = 1 }, [10L * 1024 * 1024 * 1024, 1L]).RunRetentionAsync();
        Assert.Equal(1, result.DecksRemoved);
        Assert.Equal(DeckPruningStopReason.DiskFloor, result.StopReason);
    }

    [Fact]
    public async Task RunRetentionAsync_RemovalsAcrossCommanders_PausesOnlyBetweenBatches()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        foreach (var commander in new[] { "A", "B" }) for (var i = 0; i < 3; i++) await SeedStoredDeckAsync(repository, $"{commander}-{i}", commander, DateTimeOffset.UtcNow.AddDays(i));
        var delays = new List<TimeSpan>();
        var service = CreateService(repository, new DeckPruningOptions { PerCommanderCap = 1, BatchSize = 3, Pause = TimeSpan.FromSeconds(2) }, [10L * 1024 * 1024 * 1024, 10L * 1024 * 1024 * 1024], (delay, _) => { delays.Add(delay); return Task.CompletedTask; });
        var result = await service.RunRetentionAsync();
        Assert.Equal(2, result.Batches);
        Assert.Equal(4, result.DecksRemoved);
        Assert.Equal([TimeSpan.FromSeconds(2)], delays);
    }

    [Fact]
    public async Task RunRetentionAsync_MaxDecks_StopsWithRemovalsLeft()
    {
        var repository = await SeedCandidatesAsync(5);
        var result = await CreateService(repository, new DeckPruningOptions { PerCommanderCap = 1, BatchSize = 2 }, [10L * 1024 * 1024 * 1024, 10L * 1024 * 1024 * 1024]).RunRetentionAsync(3);
        Assert.Equal(3, result.DecksRemoved);
        Assert.Equal(DeckPruningStopReason.MaxDecksReached, result.StopReason);
    }

    [Fact]
    public async Task RunRetentionAsync_MaxDecksExactlyExhaustsCandidates_Completes()
    {
        var repository = await SeedCandidatesAsync(5);
        var result = await CreateService(repository, new DeckPruningOptions { PerCommanderCap = 1, BatchSize = 2 }, [10L * 1024 * 1024 * 1024, 10L * 1024 * 1024 * 1024]).RunRetentionAsync(4);
        Assert.Equal(4, result.DecksRemoved);
        Assert.Equal(2, result.Batches);
        Assert.Equal(DeckPruningStopReason.Completed, result.StopReason);
    }

    [Fact]
    public async Task RunRetentionAsync_MaxAgeSet_RemovesOldDeckUnderCap()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        var now = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        await SeedStoredDeckAsync(repository, "old", "A", now.AddDays(-40));
        await SeedStoredDeckAsync(repository, "new", "A", now.AddDays(-20));
        var result = await CreateService(repository, new DeckPruningOptions { PerCommanderCap = 100, MaxAge = TimeSpan.FromDays(30) }, [10L * 1024 * 1024 * 1024], utcNow: now).RunRetentionAsync();
        Assert.Equal(1, result.DecksRemoved);
    }

    [Fact]
    public async Task RunRetentionAsync_MaxAgeNull_KeepsOldDeckUnderCap()
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        await SeedStoredDeckAsync(repository, "old", "A", DateTimeOffset.UtcNow.AddDays(-40));
        var result = await CreateService(repository, new DeckPruningOptions { PerCommanderCap = 100 }, [10L * 1024 * 1024 * 1024]).RunRetentionAsync();
        Assert.Equal(0, result.DecksRemoved);
        Assert.Equal(DeckPruningStopReason.Completed, result.StopReason);
    }

    [Theory]
    [InlineData(0, 1, 0, 0, null)]
    [InlineData(1, 0, 0, 0, null)]
    [InlineData(1, 1, -1, 0, null)]
    [InlineData(1, 1, 0, -1, null)]
    [InlineData(1, 1, 0, 0, 0)]
    public void Constructor_InvalidOptions_Throws(int cap, int batch, int pauseMilliseconds, long floor, int? maxAgeDays)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeckPruningOptions(cap, batch, TimeSpan.FromMilliseconds(pauseMilliseconds), floor, maxAgeDays.HasValue ? TimeSpan.FromDays(maxAgeDays.Value) : null));
    }

    [Fact]
    public async Task RunRetentionAsync_CancelledDuringPause_PropagatesAfterFirstBatch()
    {
        var repository = await SeedCandidatesAsync(5);
        var service = CreateService(repository, new DeckPruningOptions { PerCommanderCap = 1, BatchSize = 2, Pause = TimeSpan.FromMilliseconds(1) }, [10L * 1024 * 1024 * 1024], static (_, _) => throw new OperationCanceledException());
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.RunRetentionAsync());
        Assert.Equal(DeckSkipReasons.Retention, await SkipReasonAsync("candidate-0"));
    }

    private async Task<CategoryKnowledgeRepository> SeedCandidatesAsync(int count)
    {
        var repository = new CategoryKnowledgeRepository(_databasePath);
        for (var i = 0; i < count; i++) await SeedStoredDeckAsync(repository, $"candidate-{i}", "A", DateTimeOffset.UtcNow.AddDays(i));
        return repository;
    }

    private static DeckPruningService CreateService(CategoryKnowledgeRepository repository, DeckPruningOptions options, IReadOnlyList<long?> answers, Func<TimeSpan, CancellationToken, Task>? delay = null, DateTimeOffset? utcNow = null)
    {
        var next = 0;
        return new DeckPruningService(repository, options, _ => Task.FromResult(answers[Math.Min(next++, answers.Count - 1)]), delay ?? ((_, _) => Task.CompletedTask), () => utcNow ?? DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A pooled SQLite handle can outlive the completed test on Windows.
        }
    }
}
