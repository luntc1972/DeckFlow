using System.IO;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services.Content;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Integration tests for <see cref="WhisperSpendLedger"/> using a temporary SQLite content KB database.
/// </summary>
public sealed class WhisperSpendLedgerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSourceStore _sourceStore;
    private readonly ContentVideoStore _videoStore;
    private readonly WhisperSpendLedger _ledger;

    public WhisperSpendLedgerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"whisper-spend-test-{Guid.NewGuid():N}.db");
        _sourceStore = new ContentSourceStore(_dbPath);
        _videoStore = new ContentVideoStore(_dbPath);
        _ledger = new WhisperSpendLedger(_dbPath);
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
    public async Task EnsureSchemaAsync_IsIdempotent()
    {
        await _ledger.EnsureSchemaAsync();
        await _ledger.EnsureSchemaAsync();
    }

    [Fact]
    public async Task GetMonthlyTotalAsync_SumsCostsWithExactDecimal()
    {
        var videoId = await InsertVideoAsync("exact-decimal");

        await _ledger.RecordCallAsync(videoId, 60, 0.10m, "2026-05");
        await _ledger.RecordCallAsync(videoId, 120, 0.20m, "2026-05");

        var total = await _ledger.GetMonthlyTotalAsync("2026-05");

        Assert.Equal(0.30m, total);
    }

    [Fact]
    public async Task GetMonthlyTotalAsync_IsolatesMonths()
    {
        var videoId = await InsertVideoAsync("month-isolation");

        await _ledger.RecordCallAsync(videoId, 60, 0.10m, "2026-05");
        await _ledger.RecordCallAsync(videoId, 120, 0.20m, "2026-05");

        var total = await _ledger.GetMonthlyTotalAsync("2026-06");

        Assert.Equal(0m, total);
    }

    [Fact]
    public async Task WouldExceedCapAsync_ReturnsFalseWhenProjectedCostIsUnderCap()
    {
        var ledger = new WhisperSpendLedger(_dbPath, BuildConfiguration("0.50"));
        var videoId = await InsertVideoAsync("cap-under");
        await ledger.RecordCallAsync(videoId, 30, 0.10m, "2026-05");

        var wouldExceed = await ledger.WouldExceedCapAsync(0.20m, "2026-05");

        Assert.False(wouldExceed);
    }

    [Fact]
    public async Task WouldExceedCapAsync_ReturnsTrueWhenProjectedCostIsOverCap()
    {
        var ledger = new WhisperSpendLedger(_dbPath, BuildConfiguration("0.25"));
        var videoId = await InsertVideoAsync("cap-over");
        await ledger.RecordCallAsync(videoId, 30, 0.10m, "2026-05");

        var wouldExceed = await ledger.WouldExceedCapAsync(0.20m, "2026-05");

        Assert.True(wouldExceed);
    }

    private async Task<long> InsertVideoAsync(string slug)
    {
        var sourceId = await _sourceStore.InsertSourceAsync(
            slug,
            $"Source {slug}",
            ContentSourceType.Youtube,
            $"https://example.test/{slug}");

        return await _videoStore.InsertVideoAsync(
            sourceId,
            $"{slug}-video",
            null,
            $"Video {slug}",
            $"https://www.youtube.com/watch?v={slug}-video",
            DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            TranscriptStatus.Pending);
    }

    private static IConfiguration BuildConfiguration(string capUsd)
    {
        var values = new Dictionary<string, string?>
        {
            ["DECKFLOW_WHISPER_MONTHLY_CAP_USD"] = capUsd
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
