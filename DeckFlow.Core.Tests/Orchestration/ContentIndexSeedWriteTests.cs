using System.Text;
using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Pins the byte-shape, LF guarantee, and approved-only membership of
/// <see cref="IContentIndexExporter.ExportIndexToFileAsync"/>.
/// </summary>
public sealed class ContentIndexSeedWriteTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _tempDir;
    private readonly ContentSiteIndexStore _indexStore;

    public ContentIndexSeedWriteTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"deckflow-seed-write-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "index.db");
        _indexStore = new ContentSiteIndexStore(_dbPath);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public async Task ExportIndexToFileAsync_WritesLfOnly()
    {
        var orchestrator = CreateOrchestrator(_indexStore);
        await SeedApprovedAndPendingRows(_indexStore, 2, 1);

        var seedPath = Path.Combine(_tempDir, "seed", "index-seed.json");
        var result = await orchestrator.ExportIndexToFileAsync(seedPath);

        Assert.True(result.Success, result.Message ?? "export failed");
        var bytes = await File.ReadAllBytesAsync(seedPath);
        Assert.DoesNotContain((byte)0x0D, bytes); // no CR byte anywhere
        Assert.Equal((byte)0x0A, bytes[^1]);       // ends with exactly one LF
    }

    [Fact]
    public async Task ExportIndexToFileAsync_ApprovedOnly()
    {
        var orchestrator = CreateOrchestrator(_indexStore);
        await SeedApprovedAndPendingRows(_indexStore, 2, 1);

        var seedPath = Path.Combine(_tempDir, "seed2", "index-seed.json");
        var result = await orchestrator.ExportIndexToFileAsync(seedPath);

        Assert.True(result.Success, result.Message ?? "export failed");
        Assert.Equal(2, result.RowCount);

        var body = await File.ReadAllTextAsync(seedPath);
        Assert.Contains("approved-video-0", body);
        Assert.Contains("approved-video-1", body);
        Assert.DoesNotContain("pending-video-0", body);
    }

    [Fact]
    public async Task ExportIndexToFileAsync_ByteShapeMatchesCliSerializer()
    {
        var orchestrator = CreateOrchestrator(_indexStore);
        // Seed exactly the rows we will compare against the CLI golden serializer
        var rows = await SeedApprovedRows(_indexStore, 2);

        var seedPath = Path.Combine(_tempDir, "seed3", "index-seed.json");
        var result = await orchestrator.ExportIndexToFileAsync(seedPath);

        Assert.True(result.Success, result.Message ?? "export failed");
        var fileBody = await File.ReadAllTextAsync(seedPath);

        // Reproduce the CLI byte-shape inline:
        // ContentKbCommandRunners.SerializeContentIndexExportRows
        var json = JsonSerializer.Serialize(
            rows,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            });
        var expectedBody = json.Replace("\r\n", "\n") + "\n";

        Assert.Equal(expectedBody, fileBody);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ContentKbOrchestrator CreateOrchestrator(IContentSiteIndexStore indexStore)
        => new(
            new ThrowingContentSourceStore(),
            new ThrowingContentVideoStore(),
            indexStore,
            new ThrowingBlockedVideoStore(),
            new ThrowingContentHarvestRunStore(),
            new ThrowingLlmSpendLedger(),
            new ThrowingWhisperSpendLedger(),
            new ThrowingLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker(),
            () => DateTimeOffset.Parse("2026-06-16T00:00:00Z"),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-seed-write-art"),
            });

    private static async Task SeedApprovedAndPendingRows(
        ContentSiteIndexStore store,
        int approvedCount,
        int pendingCount)
    {
        await store.EnsureSchemaAsync();
        for (var i = 0; i < approvedCount; i++)
        {
            var row = BuildRow($"approved-video-{i}", approvalStatus: "approved");
            await store.UpsertRowAsync(row);
            // UpsertRowAsync defaults approval_status to 'pending'; approval is a separate
            // admin action (Phase 43), so mark the row approved via the dedicated mutation.
            await store.SetApprovalStatusAsync(ContentSourceType.Youtube, row.YoutubeVideoId!, "approved");
        }

        for (var i = 0; i < pendingCount; i++)
        {
            var row = BuildRow($"pending-video-{i}", approvalStatus: "pending");
            await store.UpsertRowAsync(row);
        }
    }

    private static async Task<IReadOnlyList<ContentIndexExportRow>> SeedApprovedRows(
        ContentSiteIndexStore store,
        int count)
    {
        await store.EnsureSchemaAsync();
        var rows = new List<ContentIndexExportRow>();
        for (var i = 0; i < count; i++)
        {
            var row = BuildRow($"approved-video-{i}", approvalStatus: "approved");
            await store.UpsertRowAsync(row);
            // UpsertRowAsync defaults approval_status to 'pending'; approval is a separate
            // admin action (Phase 43), so mark the row approved via the dedicated mutation.
            await store.SetApprovalStatusAsync(ContentSourceType.Youtube, row.YoutubeVideoId!, "approved");
            rows.Add(ContentIndexExportRow.From(row));
        }

        return rows;
    }

    private static ContentSiteIndexRow BuildRow(string videoId, string approvalStatus)
        => new()
        {
            Id = 0,
            YoutubeVideoId = videoId,
            RssGuid = null,
            Source = "test-source",
            Title = $"Title for {videoId}",
            VideoUrl = $"https://youtube.com/watch?v={videoId}",
            ArtifactPath = $"content-kb/test-source/{videoId}.md",
            PublishedUtc = null,
            IndexedUtc = DateTimeOffset.Parse("2026-06-16T00:00:00Z"),
            ArchetypeTags = [],
            BracketTags = [],
            CardCategoryTags = [],
            ApprovalStatus = approvalStatus,
        };
}
