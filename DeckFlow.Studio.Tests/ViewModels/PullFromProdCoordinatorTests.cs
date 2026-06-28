using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Fast unit tests for <see cref="PullFromProdCoordinator"/> — the read-only prod pull + local-only
/// adopt orchestration extracted from the page code-behind (H1 split). These exercise the pull/classify
/// sequence and the adopt apply (content upsert + approval mirror + staged-artifact promotion)
/// directly with fakes, without the bUnit render the logic previously required.
/// </summary>
public sealed class PullFromProdCoordinatorTests : IDisposable
{
    private readonly string _dataRoot;
    private readonly string _artifactRoot;
    private readonly string _stagingRoot;

    public PullFromProdCoordinatorTests()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "deckflow-pull-coord-" + Guid.NewGuid().ToString("N"));
        _artifactRoot = Path.Combine(_dataRoot, "content-kb");
        _stagingRoot = Path.Combine(_dataRoot, "pull-staging");
        Directory.CreateDirectory(_artifactRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dataRoot))
            {
                Directory.Delete(_dataRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    // Synchronous IProgress<T> capture so progress lines are deterministic in-test (no sync context).
    private sealed class ListProgress<T> : IProgress<T>
    {
        public List<T> Items { get; } = new();

        public void Report(T value) => Items.Add(value);
    }

    private static ContentSiteIndexRow Youtube(long id, string videoId, string status = "approved")
        => new()
        {
            Id = id,
            Source = "test-channel",
            Title = $"Video {id}",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            IndexedUtc = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            ApprovalStatus = status,
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
        };

    private PullFromProdCoordinator Build(
        FakeContentSiteIndexStore localStore,
        FakeProdContentReader prodReader,
        FakeSshArtifactDownloader downloader)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Studio:ProdConnectionString"] = "Host=fake;Database=fake",
            })
            .Build();

        return new PullFromProdCoordinator(
            localStore,
            downloader,
            prodReader,
            configuration,
            new ContentKbOrchestratorOptions { ArtifactRoot = _artifactRoot },
            NullLogger<PullFromProdCoordinator>.Instance);
    }

    private static SyncDiffEntry AdoptEntry(ContentSiteIndexRow prodRow, bool artifactDownloaded)
        => new()
        {
            NaturalKeyType = "youtube",
            NaturalKeyValue = prodRow.YoutubeVideoId!,
            Kind = SyncDiffKind.ProdNewer,
            Title = prodRow.Title,
            ProdRow = prodRow,
            LocalRow = null,
            ArtifactPath = prodRow.ArtifactPath,
            ArtifactDownloaded = artifactDownloaded,
        };

    // ── ResolvePaths ─────────────────────────────────────────────────────────

    [Fact]
    public void ResolvePaths_ReturnsDataRootParentAndStagingDir()
    {
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeProdContentReader(), new FakeSshArtifactDownloader());

        var paths = coordinator.ResolvePaths();

        Assert.Equal(_dataRoot, paths.DataRoot);
        Assert.Equal(Path.Combine(_dataRoot, "pull-staging"), paths.StagingRoot);
    }

    // ── PullAndClassifyAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task PullAndClassifyAsync_MissingLocally_DownloadsAndClassifies()
    {
        var prodReader = new FakeProdContentReader();
        prodReader.Rows.Add(Youtube(1, "vid1"));
        var downloader = new FakeSshArtifactDownloader();
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader, downloader);
        var log = new ListProgress<string>();
        var stage = new List<string>();

        var entries = await coordinator.PullAndClassifyAsync(_stagingRoot, log, stage.Add, CancellationToken.None);

        var entry = Assert.Single(entries);
        Assert.Equal(SyncDiffKind.MissingLocally, entry.Kind);
        Assert.True(entry.ArtifactDownloaded);
        Assert.Equal(1, prodReader.ReadCallCount);
        // Stage names drive the diagnostic copy on failure.
        Assert.Contains("classify", stage);
        // Human-readable log includes a completion summary and the per-artifact downloaded line.
        Assert.Contains(log.Items, l => l.StartsWith("Done — 1 differing", StringComparison.Ordinal));
        Assert.Contains(log.Items, l => l.Contains("downloaded content-kb/test-channel/vid1.md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PullAndClassifyAsync_FailedDownload_StampsArtifactNotDownloaded()
    {
        var prodReader = new FakeProdContentReader();
        prodReader.Rows.Add(Youtube(1, "vid1"));
        var downloader = new FakeSshArtifactDownloader();
        downloader.FilesToFail.Add("content-kb/test-channel/vid1.md");
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader, downloader);
        var log = new ListProgress<string>();
        var stage = new List<string>();

        var entries = await coordinator.PullAndClassifyAsync(_stagingRoot, log, stage.Add, CancellationToken.None);

        var entry = Assert.Single(entries);
        Assert.False(entry.ArtifactDownloaded);
        Assert.Contains(log.Items, l => l.Contains("not downloaded: content-kb/test-channel/vid1.md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PullAndClassifyAsync_ReadFails_StageReflectsReadStageSynchronously()
    {
        // Why: the stage callback is synchronous (not Progress<T>) so a fault leaves the stage list's
        // last entry equal to the stage in flight — this is what the page's failure copy reads (Codex MED).
        var prodReader = new FakeProdContentReader { ReadFailureMessage = "Host=secret-prod-db;Password=hunter2" };
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader, new FakeSshArtifactDownloader());
        var log = new ListProgress<string>();
        var stage = new List<string>();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            coordinator.PullAndClassifyAsync(_stagingRoot, log, stage.Add, CancellationToken.None));

        Assert.Equal("read production content_site_index", stage[^1]);
    }

    // ── ApplyAdoptionsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAdoptionsAsync_NotDownloaded_UpsertsRowAndMirrorsApproval()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store, new FakeProdContentReader(), new FakeSshArtifactDownloader());
        var prodRow = Youtube(1, "vid1", "approved");
        var adopt = new[] { AdoptEntry(prodRow, artifactDownloaded: false) };
        var progress = new ListProgress<IReadOnlyList<PullApplyRowResult>>();

        var results = await coordinator.ApplyAdoptionsAsync(adopt, _stagingRoot, _dataRoot, progress, CancellationToken.None);

        var rr = Assert.Single(results);
        Assert.True(rr.Success);
        Assert.Equal("Adopted", rr.Action);
        Assert.Contains("not promoted", rr.Note, StringComparison.Ordinal);
        Assert.Contains("UpsertContentColumnsOnlyAsync", store.UpsertMethodCalls);
        Assert.Single(store.SingleApprovalCalls);
        Assert.Equal("approved", store.SingleApprovalCalls[0].Status);
        // Incremental progress fired once (one entry).
        Assert.Single(progress.Items);
    }

    [Fact]
    public async Task ApplyAdoptionsAsync_Downloaded_PromotesStagedArtifactIntoLiveTree()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store, new FakeProdContentReader(), new FakeSshArtifactDownloader());
        var prodRow = Youtube(1, "vid1", "approved");

        // Stage a downloaded artifact at {staging}/{artifactPath}; adopt must promote it to {dataRoot}/{artifactPath}.
        var stagedPath = Path.Combine(_stagingRoot, prodRow.ArtifactPath);
        Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
        File.WriteAllText(stagedPath, "staged content");

        var adopt = new[] { AdoptEntry(prodRow, artifactDownloaded: true) };
        var progress = new ListProgress<IReadOnlyList<PullApplyRowResult>>();

        var results = await coordinator.ApplyAdoptionsAsync(adopt, _stagingRoot, _dataRoot, progress, CancellationToken.None);

        var rr = Assert.Single(results);
        Assert.True(rr.Success);
        Assert.Contains("artifact promoted", rr.Note, StringComparison.Ordinal);
        var liveDest = Path.Combine(_dataRoot, prodRow.ArtifactPath);
        Assert.True(File.Exists(liveDest));
        Assert.False(File.Exists(stagedPath));
    }

    [Fact]
    public async Task ApplyAdoptionsAsync_LocalOnlyOrNullProd_AreSkipped()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store, new FakeProdContentReader(), new FakeSshArtifactDownloader());
        var localOnly = new SyncDiffEntry
        {
            NaturalKeyType = "youtube",
            NaturalKeyValue = "vidLocal",
            Kind = SyncDiffKind.LocalOnly,
            Title = "Local Only",
            ProdRow = null,
            LocalRow = Youtube(9, "vidLocal"),
            ArtifactPath = "content-kb/test-channel/vidLocal.md",
        };
        var progress = new ListProgress<IReadOnlyList<PullApplyRowResult>>();

        var results = await coordinator.ApplyAdoptionsAsync(new[] { localOnly }, _stagingRoot, _dataRoot, progress, CancellationToken.None);

        Assert.Empty(results);
        Assert.Empty(store.UpsertMethodCalls);
        Assert.Empty(store.SingleApprovalCalls);
    }
}
