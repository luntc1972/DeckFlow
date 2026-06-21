using System;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Core.Content;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="SkippedVideoStore"/> using a temporary SQLite content KB database,
/// including the HSEL-02 invariant that skipping never touches the block list or any artifact.
/// </summary>
public sealed class SkippedVideoStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SkippedVideoStore _store;

    public SkippedVideoStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"skipped-video-test-{Guid.NewGuid():N}.db");
        _store = new SkippedVideoStore(_dbPath);
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
    public async Task AddSkip_ThenIsSkipped_RoundTrips()
    {
        Assert.False(await _store.IsSkippedAsync("vid-1"));

        await _store.AddSkipAsync("vid-1", "low signal");

        Assert.True(await _store.IsSkippedAsync("vid-1"));
        var row = Assert.Single(await _store.ListSkippedAsync());
        Assert.Equal("vid-1", row.YoutubeVideoId);
        Assert.Equal("low signal", row.Reason);
    }

    [Fact]
    public async Task AddSkip_IsIdempotent()
    {
        await _store.AddSkipAsync("vid-1", null);
        await _store.AddSkipAsync("vid-1", null);

        Assert.Single(await _store.ListSkippedAsync());
    }

    [Fact]
    public async Task RemoveSkip_ReturnsFalseForUnknown_TrueAfterSkip()
    {
        Assert.False(await _store.RemoveSkipAsync("vid-1"));

        await _store.AddSkipAsync("vid-1", null);
        Assert.True(await _store.RemoveSkipAsync("vid-1"));
        Assert.False(await _store.IsSkippedAsync("vid-1"));
    }

    [Fact]
    public async Task Skip_NeverTouchesBlockListOrArtifacts_HSEL02Invariant()
    {
        // Pre-seed the block list on the SAME db and an on-disk artifact sentinel.
        var blocked = new BlockedVideoStore(_dbPath);
        await blocked.AddBlockAsync("blocked-vid", "spam");

        var sentinelPath = Path.Combine(Path.GetTempPath(), $"artifact-sentinel-{Guid.NewGuid():N}.md");
        const string sentinelContents = "artifact body that must remain byte-identical";
        await File.WriteAllTextAsync(sentinelPath, sentinelContents);
        try
        {
            // Skip + un-skip a DIFFERENT video.
            await _store.AddSkipAsync("skipped-vid", "low signal");
            await _store.RemoveSkipAsync("skipped-vid");
            await _store.AddSkipAsync("skipped-vid", null);

            // The block row is untouched.
            Assert.True(await blocked.IsBlockedAsync("blocked-vid"));
            var blockRow = Assert.Single(await blocked.ListBlockedAsync());
            Assert.Equal("blocked-vid", blockRow.YoutubeVideoId);
            Assert.Equal("spam", blockRow.Reason);

            // Skipping never blocks, and the artifact sentinel is byte-identical.
            Assert.False(await blocked.IsBlockedAsync("skipped-vid"));
            Assert.Equal(sentinelContents, await File.ReadAllTextAsync(sentinelPath));
        }
        finally
        {
            if (File.Exists(sentinelPath))
            {
                File.Delete(sentinelPath);
            }
        }
    }

    [Fact]
    public async Task EnsureSchemaAsync_IsIdempotent()
    {
        await _store.EnsureSchemaAsync();
        await _store.EnsureSchemaAsync();

        await _store.AddSkipAsync("vid-1", null);
        Assert.True(await _store.IsSkippedAsync("vid-1"));
    }
}
