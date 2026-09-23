using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

public sealed class CreatorSuppressionConsumerTests : IDisposable
{
    private static void ClearPool(string path) => SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"deckflow-consumer-suppression-{Guid.NewGuid():N}");
    private readonly string _databasePath;

    public CreatorSuppressionConsumerTests()
    {
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "content.db");
    }

    public void Dispose()
    {
        ClearPool(_databasePath);
        Directory.Delete(_tempDirectory, recursive: true);
    }

    [Fact]
    public async Task ExportIndexAsync_FactoryFiltersSuppressedCreatorMatchedByArtifactFolderAlias()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(_databasePath);
        var suppressions = new CreatorSuppressionStore(connection);
        await suppressions.SuppressAsync("suppressed-creator", ["Suppressed Creator"], "request", DateTimeOffset.UtcNow, null);
        var sources = new ContentSourceStore(connection, suppressions);
        await sources.InsertSourceAsync("folder-alias", "Suppressed Creator", "youtube_channel", "https://youtube.com/@suppressed");
        var index = new ContentSiteIndexStore(connection, suppressionStore: suppressions);
        await index.UpsertRowAsync(Row("suppressed-video", "unrelated-export-label", "content-kb/folder-alias/suppressed-video.md"));
        await index.UpsertRowAsync(Row("control-video", "control", "content-kb/control/control-video.md"));
        await index.SetApprovalStatusAsync(ContentSourceType.Youtube, "suppressed-video", "approved");
        await index.SetApprovalStatusAsync(ContentSourceType.Youtube, "control-video", "approved");

        var orchestrator = ContentKbOrchestratorFactory.Create(
            connection,
            _tempDirectory,
            new ThrowingLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker());

        var result = await orchestrator.ExportIndexAsync();

        Assert.True(result.Success, result.Message);
        var row = Assert.Single(result.Rows);
        Assert.Equal("control-video", row.NaturalKeyValue);
    }

    [Fact]
    public async Task CopyApprovedArtifactsToRepoAsync_SuppressedCreatorIsNotCopiedAndControlIsCopied()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(_databasePath);
        var suppressions = new CreatorSuppressionStore(connection);
        await suppressions.SuppressAsync("suppressed-creator", ["Suppressed Creator"], "request", DateTimeOffset.UtcNow, null);
        var sources = new ContentSourceStore(connection, suppressions);
        await sources.InsertSourceAsync("suppressed-folder", "Suppressed Creator", "youtube_channel", "https://youtube.com/@suppressed");
        var index = new ContentSiteIndexStore(connection, suppressionStore: suppressions);
        await index.UpsertRowAsync(Row("suppressed-copy", "unrelated", "content-kb/suppressed-folder/suppressed-copy.md"));
        await index.UpsertRowAsync(Row("control-copy", "control", "content-kb/control/control-copy.md"));
        await index.SetApprovalStatusAsync(ContentSourceType.Youtube, "suppressed-copy", "approved");
        await index.SetApprovalStatusAsync(ContentSourceType.Youtube, "control-copy", "approved");

        Directory.CreateDirectory(Path.Combine(_tempDirectory, "content-kb", "suppressed-folder"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, "content-kb", "control"));
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "content-kb", "suppressed-folder", "suppressed-copy.md"), "suppressed");
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory, "content-kb", "control", "control-copy.md"), "control");
        var destination = Path.Combine(_tempDirectory, "destination");
        var orchestrator = ContentKbOrchestratorFactory.Create(connection, _tempDirectory, new ThrowingLlmDistillationService(), new ThrowingYouTubeChannelVideoLister(), new ThrowingTranscriptSource(), new ThrowingFfmpegAudioChunker());

        var copied = await orchestrator.CopyApprovedArtifactsToRepoAsync(_tempDirectory, destination);

        Assert.Single(copied);
        Assert.True(File.Exists(Path.Combine(destination, "content-kb", "control", "control-copy.md")));
        Assert.False(File.Exists(Path.Combine(destination, "content-kb", "suppressed-folder", "suppressed-copy.md")));
    }

    [Fact]
    public async Task DistillAsync_SuppressedCreatorDoesNotWritePromptAndControlDoes()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(_databasePath);
        var suppressions = new CreatorSuppressionStore(connection);
        await suppressions.SuppressAsync("suppressed-creator", ["Suppressed Creator"], "request", DateTimeOffset.UtcNow, null);
        var sources = new ContentSourceStore(connection, suppressions);
        var suppressedId = await sources.InsertSourceAsync("suppressed-creator", "Suppressed Creator", "youtube_channel", "https://youtube.com/@suppressed");
        var controlId = await sources.InsertSourceAsync("control-creator", "Control Creator", "youtube_channel", "https://youtube.com/@control");
        var videos = new ContentVideoStore(connection, suppressions);
        await InsertPendingVideoAsync(videos, suppressedId, "suppressed-video");
        await InsertPendingVideoAsync(videos, controlId, "control-video");

        var result = await CreateOrchestrator(connection).DistillAsync(10, false, true);

        Assert.True(result.Success, result.AbortedReason);
        // Why: distill writes prompts under <artifactRoot>/<sourceSlug>/, so exactly the control's prompt may exist.
        var promptFile = Assert.Single(Directory.EnumerateFiles(_tempDirectory, "*.prompt.md", SearchOption.AllDirectories));
        Assert.Equal(Path.Combine(_tempDirectory, "control-creator"), Path.GetDirectoryName(promptFile));
        Assert.StartsWith("control-video", Path.GetFileName(promptFile));
    }

    [Fact]
    public async Task ExportIndexToFileAsync_ThrowingSuppressionStoreReturnsFailureWithoutSeed()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(_databasePath);
        var index = new ContentSiteIndexStore(connection);
        await index.UpsertRowAsync(Row("seed-video", "control", "content-kb/control/seed-video.md"));
        await index.SetApprovalStatusAsync(ContentSourceType.Youtube, "seed-video", "approved");
        var seedPath = Path.Combine(_tempDirectory, "seed", "index.json");
        var orchestrator = CreateOrchestrator(connection);

        var saneResult = await orchestrator.ExportIndexToFileAsync(seedPath);
        Assert.True(saneResult.Success, saneResult.Message);
        File.Delete(seedPath);
        await CreateUnreadableSuppressionStoreAsync();

        var result = await orchestrator.ExportIndexToFileAsync(seedPath);

        Assert.False(result.Success);
        Assert.Contains("aliases", result.Message);
        Assert.Empty(result.Rows);
        Assert.False(File.Exists(seedPath));
    }

    [Fact]
    public async Task CopyApprovedArtifactsToRepoAsync_ThrowingSuppressionStoreThrowsWithoutCopying()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(_databasePath);
        var index = new ContentSiteIndexStore(connection);
        await index.UpsertRowAsync(Row("approved-video", "control", "content-kb/control/approved-video.md"));
        await index.SetApprovalStatusAsync(ContentSourceType.Youtube, "approved-video", "approved");
        var sourcePath = Path.Combine(_tempDirectory, "content-kb", "control", "approved-video.md");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "approved");
        var orchestrator = CreateOrchestrator(connection);
        var readableDestination = Path.Combine(_tempDirectory, "readable-destination");

        Assert.Single(await orchestrator.CopyApprovedArtifactsToRepoAsync(_tempDirectory, readableDestination));
        await CreateUnreadableSuppressionStoreAsync();
        var unreadableDestination = Path.Combine(_tempDirectory, "unreadable-destination");

        // SqliteException is propagated because this consumer must fail closed.
        await Assert.ThrowsAsync<SqliteException>(() => orchestrator.CopyApprovedArtifactsToRepoAsync(_tempDirectory, unreadableDestination));
        Assert.False(Directory.Exists(unreadableDestination));
    }

    [Fact]
    public async Task DistillAsync_ThrowingSuppressionStoreRefusesWithoutPrompt()
    {
        var connection = RelationalDatabaseConnection.FromSqlitePath(_databasePath);
        var sources = new ContentSourceStore(connection);
        var sourceId = await sources.InsertSourceAsync("control-creator", "Control Creator", "youtube_channel", "https://youtube.com/@control");
        await InsertPendingVideoAsync(new ContentVideoStore(connection), sourceId, "control-video");
        await CreateUnreadableSuppressionStoreAsync();

        var result = await CreateOrchestrator(connection).DistillAsync(10, false, true);

        Assert.False(result.Success);
        Assert.Contains("aliases", result.AbortedReason);
        Assert.Empty(Directory.EnumerateFiles(_tempDirectory, "*.prompt.md", SearchOption.AllDirectories));
    }


    private static ContentSiteIndexRow Row(string videoId, string source, string artifactPath) => new()
    {
        Id = 0,
        YoutubeVideoId = videoId,
        RssGuid = null,
        Source = source,
        Title = videoId,
        VideoUrl = $"https://youtube.com/watch?v={videoId}",
        ArtifactPath = artifactPath,
        IndexedUtc = DateTimeOffset.UtcNow,
        ArchetypeTags = [],
        BracketTags = [],
        CardCategoryTags = [],
        ApprovalStatus = "approved",
    };

    private ContentKbOrchestrator CreateOrchestrator(RelationalDatabaseConnection connection)
        => ContentKbOrchestratorFactory.Create(
            connection,
            _tempDirectory,
            new FixedLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker());

    private static async Task InsertPendingVideoAsync(ContentVideoStore videos, long sourceId, string videoId)
    {
        var id = await videos.InsertVideoAsync(sourceId, videoId, null, videoId, $"https://youtube.com/watch?v={videoId}", DateTimeOffset.UtcNow, TranscriptStatus.Captions);
        await videos.InsertTranscriptAsync(id, TranscriptSource.Captions, "A complete transcript for testing.");
    }

    private async Task CreateUnreadableSuppressionStoreAsync()
    {
        await new CreatorSuppressionStore(RelationalDatabaseConnection.FromSqlitePath(_databasePath))
            .SuppressAsync("unrelated-creator", [], "test", DateTimeOffset.UtcNow, null);
        await using var connection = new SqliteConnection($"Data Source={Path.GetFullPath(_databasePath)}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE creator_suppression; CREATE VIEW creator_suppression AS SELECT 'unrelated-creator' AS slug;";
        await command.ExecuteNonQueryAsync();
    }

    private sealed class FixedLlmDistillationService : ILlmDistillationService
    {
        public Task<ClassificationResult> ClassifyAsync(string transcript, CancellationToken cancellationToken = default)
            => Task.FromResult(new ClassificationResult("keep", "test"));

        public Task<CombinedExtractionResult> ExtractCombinedAsync(string transcript, CancellationToken cancellationToken = default)
            => Task.FromResult(new CombinedExtractionResult(
                "This video explains a compact combo deck plan.",
                [new ClipItem(60, "The opening explains the deck plan."), new ClipItem(null, "The middle highlights the interaction suite."), new ClipItem(180, "The closing covers win conditions.")],
                ["combo"],
                ["cEDH"],
                ["win-cons"],
                new TokenUsage(330, 33)));

        public Task<SummaryResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ClipsResult> ExtractClipsAsync(string transcript, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TagsResult> InferTagsAsync(string transcript, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

}
