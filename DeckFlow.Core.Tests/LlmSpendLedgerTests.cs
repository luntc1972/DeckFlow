using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="LlmSpendLedger"/> using a temporary SQLite content KB database.
/// </summary>
public sealed class LlmSpendLedgerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSourceStore _sourceStore;
    private readonly ContentVideoStore _videoStore;
    private readonly LlmSpendLedger _ledger;

    public LlmSpendLedgerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"llm-spend-test-{Guid.NewGuid():N}.db");
        _sourceStore = new ContentSourceStore(_dbPath);
        _videoStore = new ContentVideoStore(_dbPath);
        _ledger = new LlmSpendLedger(_dbPath);
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
    public async Task GetMonthlyTotalAsync_SumsRecordedLlmCostsWithExactDecimal()
    {
        var videoId = await InsertVideoAsync("exact-decimal");

        await _ledger.RecordCallAsync(videoId, 30000, 1800, LlmSpendLedger.ComputeCostUsd(30000, 1800), "2026-05");
        await _ledger.RecordCallAsync(videoId, 1000, 100, LlmSpendLedger.ComputeCostUsd(1000, 100), "2026-05");

        var total = await _ledger.GetMonthlyTotalAsync("2026-05");

        Assert.Equal(0.00579m, total);
    }

    [Fact]
    public async Task GetMonthlyTotalAsync_IsolatesMonths()
    {
        var videoId = await InsertVideoAsync("month-isolation");

        await _ledger.RecordCallAsync(videoId, 30000, 1800, LlmSpendLedger.ComputeCostUsd(30000, 1800), "2026-05");

        var total = await _ledger.GetMonthlyTotalAsync("2026-06");

        Assert.Equal(0m, total);
    }

    [Fact]
    public async Task WouldExceedCapAsync_ReturnsFalseWhenProjectedCostIsUnderCap()
    {
        var ledger = new LlmSpendLedger(_dbPath, BuildConfiguration("0.50"));
        var videoId = await InsertVideoAsync("cap-under");
        await ledger.RecordCallAsync(videoId, 30000, 1800, 0.10m, "2026-05");

        var wouldExceed = await ledger.WouldExceedCapAsync(0.20m, "2026-05");

        Assert.False(wouldExceed);
    }

    [Fact]
    public async Task WouldExceedCapAsync_ReturnsTrueWhenProjectedCostIsOverCap()
    {
        var ledger = new LlmSpendLedger(_dbPath, BuildConfiguration("0.25"));
        var videoId = await InsertVideoAsync("cap-over");
        await ledger.RecordCallAsync(videoId, 30000, 1800, 0.10m, "2026-05");

        var wouldExceed = await ledger.WouldExceedCapAsync(0.20m, "2026-05");

        Assert.True(wouldExceed);
    }

    [Fact]
    public void ComputeCostUsd_UsesExactInputAndOutputTokenPrices()
    {
        var cost = LlmSpendLedger.ComputeCostUsd(30000, 1800);

        Assert.Equal(0.00558m, cost);
    }

    [Fact]
    public void GetMonthlyCapUsd_ReturnsDefaultWhenNoConfigurationSet()
    {
        // _ledger was constructed with no configurationValueResolver and no env var set
        var cap = _ledger.GetMonthlyCapUsd();

        Assert.Equal(15.00m, cap);
    }

    [Fact]
    public void GetMonthlyCapUsd_ReturnsConfiguredValueWhenResolverProvided()
    {
        var ledger = new LlmSpendLedger(_dbPath, BuildConfiguration("25.00"));

        var cap = ledger.GetMonthlyCapUsd();

        Assert.Equal(25.00m, cap);
    }

    [Fact]
    public async Task WouldExceedCapAsync_RespectsRaisedCapFromResolver()
    {
        // Why: proves the resolver-supplied cap flows into WouldExceedCapAsync (D-03 mechanism):
        // a raised cap permits spend that the lower cap would block.
        const string monthKey = "2026-06";
        var videoId = await InsertVideoAsync("resolver-cap");

        // Low cap: $0.50; after recording $0.40, a $0.40 projected call would exceed the cap.
        var lowCapLedger = new LlmSpendLedger(_dbPath, BuildConfiguration("0.50"));
        await lowCapLedger.RecordCallAsync(videoId, 1000, 100, 0.40m, monthKey);
        var wouldExceedLowCap = await lowCapLedger.WouldExceedCapAsync(0.40m, monthKey);

        Assert.True(wouldExceedLowCap);

        // Raised cap: $5.00 on the same recorded spend; the same projected call no longer exceeds.
        var highCapLedger = new LlmSpendLedger(_dbPath, BuildConfiguration("5.00"));
        var wouldExceedHighCap = await highCapLedger.WouldExceedCapAsync(0.40m, monthKey);

        Assert.False(wouldExceedHighCap);
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

    private static Func<string, string?> BuildConfiguration(string capUsd)
        => key => key == "DECKFLOW_LLM_MONTHLY_CAP_USD" ? capUsd : null;
}
