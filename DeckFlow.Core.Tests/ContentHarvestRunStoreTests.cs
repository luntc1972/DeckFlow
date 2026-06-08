using System.IO;
using DeckFlow.Core.Content;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="ContentHarvestRunStore"/> using a temporary SQLite content KB database.
/// </summary>
public sealed class ContentHarvestRunStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentHarvestRunStore _store;

    public ContentHarvestRunStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-harvest-run-test-{Guid.NewGuid():N}.db");
        _store = new ContentHarvestRunStore(_dbPath);
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
        await _store.EnsureSchemaAsync();
        await _store.EnsureSchemaAsync();
    }

    [Fact]
    public async Task StartRunAsync_ThenCompleteRunAsync_RoundTripsRunSummary()
    {
        var runId = await _store.StartRunAsync();

        await _store.CompleteRunAsync(
            runId,
            sourcesProcessed: 2,
            videosProcessed: 7,
            transcriptsFetched: 5,
            whisperCalls: 3,
            spendUsd: 0.30m,
            abortedReason: "manual stop");

        var run = await _store.GetRunAsync(runId);

        Assert.NotNull(run);
        Assert.Equal(runId, run!.Id);
        Assert.NotEqual(default, run.StartedUtc);
        Assert.NotNull(run.CompletedUtc);
        Assert.Equal(2, run.SourcesProcessed);
        Assert.Equal(7, run.VideosProcessed);
        Assert.Equal(5, run.TranscriptsFetched);
        Assert.Equal(3, run.WhisperCalls);
        Assert.Equal(0.30m, run.SpendUsd);
        Assert.Equal("manual stop", run.AbortedReason);
    }

    [Fact]
    public async Task CompleteRunAsync_UnknownRunId_Throws()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _store.CompleteRunAsync(
            999_999,
            sourcesProcessed: 1,
            videosProcessed: 1,
            transcriptsFetched: 1,
            whisperCalls: 1,
            spendUsd: 0.01m,
            abortedReason: null));

        Assert.Equal("No content harvest run with id 999999 to complete.", exception.Message);
    }
}
