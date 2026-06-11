using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Serilog;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class CommandRunnerCorpusResetTests
{
    [Fact]
    public async Task RunCorpusResetAsync_DeletesContentAndSiteIndex_PreservesBlockedAndSources()
    {
        var videoStore = new FakeContentVideoStore();
        var siteIndexStore = new FakeContentSiteIndexStore();

        var exitCode = await CommandRunners.RunCorpusResetAsync(
            videoStore,
            siteIndexStore,
            dryRun: false,
            new LoggerConfiguration().CreateLogger(),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, videoStore.DeleteAllVideosCalls);
        Assert.Equal(1, siteIndexStore.DeleteAllRowsCalls);
        Assert.Equal(0, videoStore.DeleteVideoCalls);
        Assert.Equal(0, videoStore.DeleteVideoByYoutubeIdCalls);
        Assert.Equal(0, siteIndexStore.DeleteByIdCalls);
    }

    [Fact]
    public async Task RunCorpusResetAsync_DryRun_DeletesNothing()
    {
        var videoStore = new FakeContentVideoStore();
        var siteIndexStore = new FakeContentSiteIndexStore();

        var exitCode = await CommandRunners.RunCorpusResetAsync(
            videoStore,
            siteIndexStore,
            dryRun: true,
            new LoggerConfiguration().CreateLogger(),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(0, videoStore.DeleteAllVideosCalls);
        Assert.Equal(0, siteIndexStore.DeleteAllRowsCalls);
    }

    private sealed class FakeContentVideoStore : IContentVideoStore
    {
        public int DeleteAllVideosCalls { get; private set; }

        public int DeleteVideoCalls { get; private set; }

        public int DeleteVideoByYoutubeIdCalls { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<long> InsertVideoAsync(long sourceId, string? youtubeVideoId, string? rssGuid, string title, string videoUrl, DateTimeOffset? publishedUtc, string transcriptStatus, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentVideo?> GetVideoByYoutubeIdAsync(long sourceId, string youtubeVideoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(long sourceId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpdateTranscriptStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertTranscriptAsync(long videoId, string source, string body, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentTranscriptBody?> GetLatestTranscriptAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertClipAsync(long videoId, int timestampS, string excerpt, int sortOrder, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<long> InsertTagAsync(long videoId, string dimension, string tagValue, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
        {
            DeleteVideoCalls++;
            return Task.CompletedTask;
        }

        public Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
        {
            DeleteVideoByYoutubeIdCalls++;
            return Task.FromResult(0);
        }

        public Task<int> DeleteAllVideosAsync(CancellationToken cancellationToken = default)
        {
            DeleteAllVideosCalls++;
            return Task.FromResult(1);
        }

        public Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<string?> GetDistillStatusAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task SetDistillStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountTranscriptsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountSummariesByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountClipsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> CountTagsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeContentSiteIndexStore : IContentSiteIndexStore
    {
        public int DeleteAllRowsCalls { get; private set; }

        public int DeleteByIdCalls { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

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
            DeleteByIdCalls++;
            return Task.FromResult(0);
        }

        public Task<int> DeleteAllRowsAsync(CancellationToken cancellationToken = default)
        {
            DeleteAllRowsCalls++;
            return Task.FromResult(1);
        }

        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
