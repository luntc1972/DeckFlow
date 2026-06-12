using System.IO;
using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;
using Serilog;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Integration tests for <see cref="BlockedVideoStore"/> using a temporary SQLite content KB database.
/// </summary>
public sealed class BlockedVideoStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly BlockedVideoStore _store;

    public BlockedVideoStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"blocked-video-test-{Guid.NewGuid():N}.db");
        _store = new BlockedVideoStore(_dbPath);
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
    public async Task AddBlockAsync_AddsRow_AndTracksBlockedState()
    {
        await _store.AddBlockAsync("vid-1", "spam");

        Assert.True(await _store.IsBlockedAsync("vid-1"));
        Assert.False(await _store.IsBlockedAsync("vid-2"));
    }

    [Fact]
    public async Task AddBlockAsync_DuplicateId_IsIdempotent_AndListsOnce()
    {
        await _store.AddBlockAsync("vid-1", "spam");
        await _store.AddBlockAsync("vid-1", "duplicate");

        var blocked = await _store.ListBlockedAsync();

        var row = Assert.Single(blocked);
        Assert.Equal("vid-1", row.YoutubeVideoId);
    }

    [Fact]
    public async Task RemoveBlockAsync_RemovesExistingRow()
    {
        await _store.AddBlockAsync("vid-1", "spam");

        var removed = await _store.RemoveBlockAsync("vid-1");

        Assert.True(removed);
        Assert.False(await _store.IsBlockedAsync("vid-1"));
    }

    [Fact]
    public async Task ListBlockedAsync_ReturnsReason_AndBlockedUtc()
    {
        await _store.AddBlockAsync("vid-1", "spam");
        await _store.AddBlockAsync("vid-2", null);

        var blocked = await _store.ListBlockedAsync();

        Assert.Collection(
            blocked.OrderBy(row => row.YoutubeVideoId),
            first =>
            {
                Assert.Equal("vid-1", first.YoutubeVideoId);
                Assert.Equal("spam", first.Reason);
                Assert.NotEqual(default, first.BlockedUtc);
            },
            second =>
            {
                Assert.Equal("vid-2", second.YoutubeVideoId);
                Assert.Null(second.Reason);
                Assert.NotEqual(default, second.BlockedUtc);
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddBlockAsync_BlankId_ThrowsArgumentException(string? youtubeVideoId)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.AddBlockAsync(youtubeVideoId!, "spam"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_UsesBlockedVideosTableName_ForPhase37Point5ResetCarveOut()
    {
        await _store.EnsureSchemaAsync();

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
              FROM sqlite_master
             WHERE type = 'table'
               AND name = 'blocked_videos';
            """;

        var result = await command.ExecuteScalarAsync();

        Assert.Equal(1L, (long)result!);
    }

    [Fact]
    public async Task DeleteVideoByYoutubeIdAsync_DeletesDisabledSourceVideo_AndReturnsZeroForUnknownId()
    {
        var sourceStore = new ContentSourceStore(_dbPath);
        var videoStore = new ContentVideoStore(_dbPath);
        var sourceId = await sourceStore.InsertSourceAsync(
            "disabled-source",
            "Disabled Source",
            ContentSourceType.Youtube,
            "https://www.youtube.com/@disabled-source");
        await sourceStore.SetEnabledAsync(sourceId, isEnabled: false);
        await videoStore.InsertVideoAsync(
            sourceId,
            "disabled-video",
            rssGuid: null,
            "Disabled Video",
            "https://www.youtube.com/watch?v=disabled-video",
            DateTimeOffset.Parse("2026-06-10T12:00:00Z"),
            TranscriptStatus.Pending);

        var deleted = await videoStore.DeleteVideoByYoutubeIdAsync("disabled-video");
        var missingDeleted = await videoStore.DeleteVideoByYoutubeIdAsync("missing-video");
        var video = await videoStore.GetVideoByYoutubeIdAsync(sourceId, "disabled-video");

        Assert.Equal(1, deleted);
        Assert.Equal(0, missingDeleted);
        Assert.Null(video);
    }

    [Fact]
    public async Task DeleteByIdAsync_RemovesSiteIndexRow_AndReturnsZeroForMissingId()
    {
        var siteIndexStore = new ContentSiteIndexStore(_dbPath);
        await siteIndexStore.UpsertRowAsync(CreateYoutubeRow("yt-delete"));
        var row = await siteIndexStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-delete");

        var deleted = await siteIndexStore.DeleteByIdAsync(row!.Id);
        var missingDeleted = await siteIndexStore.DeleteByIdAsync(987_654);
        var deletedRow = await siteIndexStore.GetByIdAsync(row.Id);

        Assert.Equal(1, deleted);
        Assert.Equal(0, missingDeleted);
        Assert.Null(deletedRow);
    }

    [Fact]
    public async Task RunBlockVideoAsync_BlocksFirst_ThenDeletesContent_ThenDeletesSiteIndex()
    {
        var operations = new List<string>();
        var blockedStore = new SpyBlockedVideoStore(operations);
        var videoStore = new SpyContentVideoStore(operations) { DeleteResult = 1 };
        var siteIndexStore = new SpyContentSiteIndexStore(operations)
        {
            Row = CreateYoutubeRow("video-1") with { Id = 42 }
        };

        var exitCode = await ContentKbCommandRunners.RunBlockVideoAsync(
            "video-1",
            "spam",
            blockedStore,
            videoStore,
            siteIndexStore,
            new LoggerConfiguration().CreateLogger(),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["block:video-1:spam", "delete-video:video-1", "get-index:youtube_channel:video-1", "delete-index:42"],
            operations);
    }

    [Fact]
    public async Task RunBlockVideoAsync_UnknownVideoStillWritesBlock_AndReturnsZero()
    {
        var operations = new List<string>();
        var blockedStore = new SpyBlockedVideoStore(operations);
        var videoStore = new SpyContentVideoStore(operations) { DeleteResult = 0 };
        var siteIndexStore = new SpyContentSiteIndexStore(operations);

        var exitCode = await ContentKbCommandRunners.RunBlockVideoAsync(
            "video-missing",
            null,
            blockedStore,
            videoStore,
            siteIndexStore,
            new LoggerConfiguration().CreateLogger(),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["block:video-missing:", "delete-video:video-missing", "get-index:youtube_channel:video-missing"],
            operations);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunBlockVideoAsync_BlankId_ThrowsArgumentException(string? youtubeVideoId)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => ContentKbCommandRunners.RunBlockVideoAsync(
            youtubeVideoId!,
            "spam",
            new SpyBlockedVideoStore([]),
            new SpyContentVideoStore([]),
            new SpyContentSiteIndexStore([]),
            new LoggerConfiguration().CreateLogger(),
            CancellationToken.None));
    }

    [Fact]
    public async Task RunUnblockVideoAsync_RemovesBlockOnly()
    {
        var operations = new List<string>();
        var blockedStore = new SpyBlockedVideoStore(operations) { RemoveResult = true };

        var exitCode = await ContentKbCommandRunners.RunUnblockVideoAsync(
            "video-1",
            blockedStore,
            new LoggerConfiguration().CreateLogger(),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(["unblock:video-1"], operations);
    }

    [Fact]
    public async Task RunUnblockVideoAsync_NeverBlockedId_IsCleanNoOp()
    {
        var operations = new List<string>();
        var blockedStore = new SpyBlockedVideoStore(operations) { RemoveResult = false };

        var exitCode = await ContentKbCommandRunners.RunUnblockVideoAsync(
            "never-blocked",
            blockedStore,
            new LoggerConfiguration().CreateLogger(),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(["unblock:never-blocked"], operations);
    }

    [Fact]
    public async Task RunListBlockedAsync_WritesRowsToWriter()
    {
        var blockedStore = new SpyBlockedVideoStore([])
        {
            ListedRows =
            [
                new BlockedVideo
                {
                    YoutubeVideoId = "video-1",
                    Reason = "spam",
                    BlockedUtc = DateTimeOffset.Parse("2026-06-10T12:00:00Z")
                }
            ]
        };
        using var writer = new StringWriter();

        var exitCode = await ContentKbCommandRunners.RunListBlockedAsync(
            blockedStore,
            writer,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("video-1", writer.ToString(), StringComparison.Ordinal);
        Assert.Contains("spam", writer.ToString(), StringComparison.Ordinal);
    }

    private static ContentSiteIndexRow CreateYoutubeRow(string youtubeVideoId)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = $"content-kb/command-zone/{youtubeVideoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = new[] { "combo", "control" },
            BracketTags = new[] { "cEDH", "Optimized" },
            CardCategoryTags = new[] { "win-cons", "counter" },
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null
        };

    private sealed class SpyBlockedVideoStore : IBlockedVideoStore
    {
        private readonly List<string> _operations;

        public SpyBlockedVideoStore(List<string> operations)
        {
            _operations = operations;
        }

        public bool RemoveResult { get; init; }

        public IReadOnlyList<BlockedVideo> ListedRows { get; init; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task AddBlockAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default)
        {
            _operations.Add($"block:{youtubeVideoId}:{reason}");
            return Task.CompletedTask;
        }

        public Task<bool> RemoveBlockAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
        {
            _operations.Add($"unblock:{youtubeVideoId}");
            return Task.FromResult(RemoveResult);
        }

        public Task<bool> IsBlockedAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<BlockedVideo>> ListBlockedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ListedRows);
    }

    private sealed class SpyContentVideoStore : IContentVideoStore
    {
        private readonly List<string> _operations;

        public SpyContentVideoStore(List<string> operations)
        {
            _operations = operations;
        }

        public int DeleteResult { get; init; }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> InsertVideoAsync(long sourceId, string? youtubeVideoId, string? rssGuid, string title, string videoUrl, DateTimeOffset? publishedUtc, string transcriptStatus, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentVideo?> GetVideoByYoutubeIdAsync(long sourceId, string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateTranscriptStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertTranscriptAsync(long videoId, string source, string body, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertClipAsync(long videoId, int timestampS, string excerpt, int sortOrder, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertTagAsync(long videoId, string dimension, string tagValue, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
        {
            _operations.Add($"delete-video:{youtubeVideoId}");
            return Task.FromResult(DeleteResult);
        }

        public Task<int> CountTranscriptsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountSummariesByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountClipsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountTagsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class SpyContentSiteIndexStore : IContentSiteIndexStore
    {
        private readonly List<string> _operations;

        public SpyContentSiteIndexStore(List<string> operations)
        {
            _operations = operations;
        }

        public ContentSiteIndexRow? Row { get; init; }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default)
        {
            _operations.Add($"get-index:{naturalKeyType}:{naturalKeyValue}");
            return Task.FromResult(Row);
        }

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            _operations.Add($"delete-index:{id}");
            return Task.FromResult(1);
        }

        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
