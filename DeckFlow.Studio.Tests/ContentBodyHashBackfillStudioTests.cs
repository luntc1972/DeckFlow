using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// In-memory <see cref="ILogger{TCategoryName}"/> test double that records every logged entry
/// (level + formatted message) so tests can assert a specific warning fired — or did not.
/// </summary>
internal sealed class FakeLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Add((logLevel, formatter(state, exception)));
    }
}

/// <summary>
/// Proves D-08's LOCAL host path: a real SQLite-backed local <see cref="ContentSiteIndexStore"/>
/// (mirroring Studio's line-81 registration) plus <see cref="StudioContentArtifactBodyResolver"/>
/// over a temp artifact root, driven through the host-agnostic
/// <see cref="ContentBodyHashBackfill"/> service. Covers null-row-with-a-real-local-.md gets
/// hashed, missing-file row is skipped+warned (no throw), and a second run is idempotent
/// (writes nothing) — the exact behavior the Studio startup wiring depends on.
/// </summary>
public sealed class ContentBodyHashBackfillStudioTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _dataRoot;
    private readonly string _artifactRoot;

    public ContentBodyHashBackfillStudioTests()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "deckflow-body-hash-backfill-" + Guid.NewGuid().ToString("N"));
        _artifactRoot = Path.Combine(_dataRoot, "content-kb");
        Directory.CreateDirectory(_artifactRoot);
        _dbPath = Path.Combine(_dataRoot, "content-kb.db");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dataRoot))
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Directory.Delete(_dataRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task RunAsync_LegacyLocalRowWithRealMdFile_HashesFromDisk()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        const string videoId = "vid-local-legacy";
        var artifactPath = $"content-kb/test-channel/{videoId}.md";
        await store.UpsertContentColumnsOnlyAsync(CreateRow(videoId, artifactPath, bodySha256: null));

        const string rawArtifactText = "---\ntitle: \"x\"\n---\nLocal legacy body text.";
        WriteLocalArtifact(artifactPath, rawArtifactText);

        var resolver = new StudioContentArtifactBodyResolver(
            new ContentKbOrchestratorOptions { ArtifactRoot = _artifactRoot });
        var backfill = new ContentBodyHashBackfill(store, resolver, new FakeLogger<ContentBodyHashBackfill>());

        await backfill.RunAsync();

        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoId);
        Assert.NotNull(row);
        var expectedHash = ContentSiteIndexContentSignature.ComputeBodySha256(rawArtifactText);
        Assert.Equal(expectedHash, row!.BodySha256);
    }

    [Fact]
    public async Task RunAsync_LocalRowWithMissingMdFile_SkipsAndLogsWarningWithoutThrowing()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        const string videoId = "vid-local-missing";
        var artifactPath = $"content-kb/test-channel/{videoId}.md";
        await store.UpsertContentColumnsOnlyAsync(CreateRow(videoId, artifactPath, bodySha256: null));
        // Deliberately never write the .md file for this row.

        var resolver = new StudioContentArtifactBodyResolver(
            new ContentKbOrchestratorOptions { ArtifactRoot = _artifactRoot });
        var logger = new FakeLogger<ContentBodyHashBackfill>();
        var backfill = new ContentBodyHashBackfill(store, resolver, logger);

        var exception = await Record.ExceptionAsync(() => backfill.RunAsync());

        Assert.Null(exception);
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoId);
        Assert.Null(row!.BodySha256);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning
                && entry.Message.Contains(row.Id.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_SecondRun_IsIdempotentAndWritesNothing()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        const string videoId = "vid-local-idempotent";
        var artifactPath = $"content-kb/test-channel/{videoId}.md";
        await store.UpsertContentColumnsOnlyAsync(CreateRow(videoId, artifactPath, bodySha256: null));

        const string rawArtifactText = "---\ntitle: \"x\"\n---\nIdempotent local body text.";
        WriteLocalArtifact(artifactPath, rawArtifactText);

        var resolver = new StudioContentArtifactBodyResolver(
            new ContentKbOrchestratorOptions { ArtifactRoot = _artifactRoot });
        var backfill = new ContentBodyHashBackfill(store, resolver, new FakeLogger<ContentBodyHashBackfill>());

        await backfill.RunAsync();
        var afterFirstRun = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoId);
        var expectedHash = ContentSiteIndexContentSignature.ComputeBodySha256(rawArtifactText);
        Assert.Equal(expectedHash, afterFirstRun!.BodySha256);

        // Delete the on-disk file — if the second run ever re-reads a non-null row, this would
        // surface as a spurious skip+warning, which the assertions below rule out.
        File.Delete(Path.Combine(_dataRoot, artifactPath));

        await backfill.RunAsync();

        var afterSecondRun = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, videoId);
        Assert.Equal(expectedHash, afterSecondRun!.BodySha256);
    }

    private void WriteLocalArtifact(string artifactPath, string text)
    {
        var fullPath = Path.Combine(_dataRoot, artifactPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, text);
    }

    private static ContentSiteIndexRow CreateRow(string youtubeVideoId, string artifactPath, string? bodySha256)
        => new()
        {
            Id = 0,
            Source = "Test Channel",
            Title = $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = artifactPath,
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = ["combo"],
            BracketTags = ["cEDH"],
            CardCategoryTags = ["win-cons"],
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null,
            ApprovalStatus = "approved",
            BodySha256 = bodySha256,
        };
}
