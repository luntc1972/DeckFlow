using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Fast unit tests for <see cref="PullFromProdCoordinator"/> — the read-only prod pull + local-only
/// adopt orchestration extracted from the page code-behind (H1 split). These exercise the pull/classify
/// sequence and the adopt apply (content upsert + approval mirror + git-tree body copy)
/// directly with fakes, without the bUnit render the logic previously required.
/// </summary>
public sealed class PullFromProdCoordinatorTests : IDisposable
{
    private readonly string _dataRoot;
    private readonly string _artifactRoot;
    private readonly string _repoRoot;

    public PullFromProdCoordinatorTests()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "deckflow-pull-coord-" + Guid.NewGuid().ToString("N"));
        _artifactRoot = Path.Combine(_dataRoot, "content-kb");
        _repoRoot = Path.Combine(_dataRoot, "repo");
        Directory.CreateDirectory(_artifactRoot);
        Directory.CreateDirectory(_repoRoot);
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

    private static ContentSiteIndexRow Youtube(
        long id,
        string videoId,
        string status = "approved",
        DateTimeOffset? indexedUtc = null,
        string? bodySha256 = null,
        bool isVisible = false,
        bool isHidden = false)
        => new()
        {
            Id = id,
            Source = "test-channel",
            Title = $"Video {id}",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            IndexedUtc = indexedUtc ?? new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            ApprovalStatus = status,
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
            BodySha256 = bodySha256,
            IsVisible = isVisible,
            IsHidden = isHidden,
        };

    private PullFromProdCoordinator Build(
        FakeContentSiteIndexStore localStore,
        FakeProdContentReader prodReader,
        FakeGitRepository? git = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Studio:ProdConnectionString"] = "Host=fake;Database=fake",
            })
            .Build();

        return new PullFromProdCoordinator(
            localStore,
            git ?? new FakeGitRepository { CannedRepoRoot = _repoRoot },
            prodReader,
            configuration,
            new ContentKbOrchestratorOptions { ArtifactRoot = _artifactRoot },
            NullLogger<PullFromProdCoordinator>.Instance);
    }

    private static SyncDiffEntry AdoptEntry(
        ContentSiteIndexRow prodRow,
        bool artifactDownloaded,
        BodyDivergenceStatus bodyDivergence = BodyDivergenceStatus.Clean,
        SyncDiffKind kind = SyncDiffKind.ProdNewer)
        => new()
        {
            NaturalKeyType = ContentSourceType.Youtube,
            NaturalKeyValue = prodRow.YoutubeVideoId!,
            Kind = kind,
            Title = prodRow.Title,
            ProdRow = prodRow,
            LocalRow = null,
            ArtifactPath = prodRow.ArtifactPath,
            ArtifactDownloaded = artifactDownloaded,
            BodyDivergence = bodyDivergence,
        };

    // ── ResolvePaths ─────────────────────────────────────────────────────────

    [Fact]
    public void ResolvePaths_ReturnsDataRootParent()
    {
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeProdContentReader());

        var paths = coordinator.ResolvePaths();

        Assert.Equal(_dataRoot, paths.DataRoot);
    }

    // ── PullAndClassifyAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task PullAndClassifyAsync_MissingLocally_BodyPresentInGitTree_ClassifiesAvailable()
    {
        var prodReader = new FakeProdContentReader();
        var rawBody = "---\ntitle: Video 1\n---\nrepo body";
        var row = Youtube(1, "vid1", bodySha256: ContentSiteIndexContentSignature.ComputeBodySha256(rawBody));
        prodReader.Rows.Add(row);
        WriteRepoBody(row.ArtifactPath, rawBody);
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader);
        var log = new ListProgress<string>();
        var stage = new List<string>();

        var result = await coordinator.PullAndClassifyAsync(log, stage.Add, CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(SyncDiffKind.MissingLocally, entry.Kind);
        Assert.True(entry.ArtifactDownloaded);
        Assert.Equal(BodyDivergenceStatus.Clean, entry.BodyDivergence);
        Assert.Equal(PullFreshnessKind.Fresh, result.Freshness.Kind);
        Assert.Equal(1, prodReader.ReadCallCount);
        Assert.Contains("classify", stage);
        Assert.Contains(log.Items, l => l.StartsWith("Done — 1 differing", StringComparison.Ordinal));
        Assert.Contains(log.Items, l => l.Contains("body present: content-kb/test-channel/vid1.md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PullAndClassifyAsync_BodyAbsentFromGitTree_StampsArtifactUnavailableAndIndeterminate()
    {
        var prodReader = new FakeProdContentReader();
        prodReader.Rows.Add(Youtube(1, "vid1", bodySha256: "prod-hash"));
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader);
        var log = new ListProgress<string>();
        var stage = new List<string>();

        var result = await coordinator.PullAndClassifyAsync(log, stage.Add, CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.False(entry.ArtifactDownloaded);
        Assert.Equal(BodyDivergenceStatus.Indeterminate, entry.BodyDivergence);
        Assert.Contains(log.Items, l => l.Contains("body not in local git repo (prod-only/unpublished): content-kb/test-channel/vid1.md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PullAndClassifyAsync_InvalidArtifactPath_StampsUnavailableAndDoesNotEchoPath()
    {
        var malicious = Youtube(1, "vid1", bodySha256: "prod-hash") with { ArtifactPath = "content-kb/../../evil.md" };
        var prodReader = new FakeProdContentReader();
        prodReader.Rows.Add(malicious);
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader);
        var log = new ListProgress<string>();
        var stage = new List<string>();

        var result = await coordinator.PullAndClassifyAsync(log, stage.Add, CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.False(entry.ArtifactDownloaded);
        Assert.Equal(BodyDivergenceStatus.Indeterminate, entry.BodyDivergence);
        Assert.Contains(log.Items, l => l == "  body SKIPPED (invalid path)");
        Assert.DoesNotContain(log.Items, l => l.Contains(malicious.ArtifactPath, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PullAndClassifyAsync_ReadFails_StageReflectsReadStageSynchronously()
    {
        var prodReader = new FakeProdContentReader { ReadFailureMessage = "Host=secret-prod-db;Password=hunter2" };
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader);
        var log = new ListProgress<string>();
        var stage = new List<string>();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            coordinator.PullAndClassifyAsync(log, stage.Add, CancellationToken.None));

        Assert.Equal("read production content_site_index", stage[^1]);
    }

    [Fact]
    public async Task PullAndClassifyAsync_BehindOrigin_WarnsAndProceeds()
    {
        var prodReader = new FakeProdContentReader();
        var row = Youtube(1, "vid1");
        prodReader.Rows.Add(row);
        WriteRepoBody(row.ArtifactPath, "---\ntitle: Video 1\n---\nrepo body");
        var git = new FakeGitRepository
        {
            CannedRepoRoot = _repoRoot,
            CannedBranch = "main",
            CannedBehindCount = 3,
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader, git);
        var log = new ListProgress<string>();

        var result = await coordinator.PullAndClassifyAsync(log, _ => { }, CancellationToken.None);

        Assert.Equal(PullFreshnessKind.Behind, result.Freshness.Kind);
        Assert.Equal(3, result.Freshness.BehindCount);
        Assert.Equal("main", result.Freshness.Branch);
        Assert.Single(result.Entries);
        Assert.Contains(log.Items, l => l.Contains("3 commit(s) behind origin/main", StringComparison.Ordinal));
        var fetch = Assert.Single(git.FetchCalls);
        Assert.Equal("origin", fetch.Remote);
        Assert.Equal("main", fetch.Branch);
    }

    [Fact]
    public async Task PullAndClassifyAsync_FetchFails_MarksUnverifiedAndProceeds()
    {
        var prodReader = new FakeProdContentReader();
        prodReader.Rows.Add(Youtube(1, "vid1"));
        var git = new FakeGitRepository
        {
            CannedRepoRoot = _repoRoot,
            ThrowOnFetch = new GitCommandException("fetch failed"),
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader, git);
        var log = new ListProgress<string>();

        var result = await coordinator.PullAndClassifyAsync(log, _ => { }, CancellationToken.None);

        Assert.Equal(PullFreshnessKind.Unverified, result.Freshness.Kind);
        Assert.Single(result.Entries);
        Assert.Contains(log.Items, l => l.Contains("Could not verify checkout freshness (fetch failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PullAndClassifyAsync_FetchTimesOut_MarksUnverifiedAndProceeds()
    {
        var prodReader = new FakeProdContentReader();
        prodReader.Rows.Add(Youtube(1, "vid1"));
        var git = new FakeGitRepository
        {
            CannedRepoRoot = _repoRoot,
            ThrowOnFetch = new OperationCanceledException(),
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader, git);
        var log = new ListProgress<string>();

        var result = await coordinator.PullAndClassifyAsync(log, _ => { }, CancellationToken.None);

        Assert.Equal(PullFreshnessKind.Unverified, result.Freshness.Kind);
        Assert.Single(result.Entries);
        Assert.Contains(log.Items, l => l.Contains("Could not verify checkout freshness (fetch timed out", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PullAndClassifyAsync_PageCancellationDuringFreshness_Propagates()
    {
        var prodReader = new FakeProdContentReader();
        prodReader.Rows.Add(Youtube(1, "vid1"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var git = new FakeGitRepository
        {
            CannedRepoRoot = _repoRoot,
            ThrowOnFetch = new OperationCanceledException(cts.Token),
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader, git);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.PullAndClassifyAsync(new ListProgress<string>(), _ => { }, cts.Token));
    }

    [Fact]
    public async Task PullAndClassifyAsync_CleanFreshness_EmitsNoFreshnessWarning()
    {
        var prodReader = new FakeProdContentReader();
        prodReader.Rows.Add(Youtube(1, "vid1"));
        var git = new FakeGitRepository
        {
            CannedRepoRoot = _repoRoot,
            CannedBranch = "main",
            CannedBehindCount = 0,
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader, git);
        var log = new ListProgress<string>();

        var result = await coordinator.PullAndClassifyAsync(log, _ => { }, CancellationToken.None);

        Assert.Equal(PullFreshnessKind.Fresh, result.Freshness.Kind);
        Assert.DoesNotContain(log.Items, l => l.Contains("Could not verify checkout freshness", StringComparison.Ordinal)
            || l.Contains("behind origin/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PullAndClassifyAsync_MismatchedBodyHash_StampsConfirmed()
    {
        var prodReader = new FakeProdContentReader();
        prodReader.Rows.Add(Youtube(1, "vid1", bodySha256: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
        WriteRepoBody("content-kb/test-channel/vid1.md", "---\ntitle: Video 1\n---\nrepo body");
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader);

        var result = await coordinator.PullAndClassifyAsync(new ListProgress<string>(), _ => { }, CancellationToken.None);

        Assert.Equal(BodyDivergenceStatus.Confirmed, Assert.Single(result.Entries).BodyDivergence);
    }

    [Fact]
    public async Task PullAndClassifyAsync_NullProdBodyHash_StampsIndeterminate()
    {
        var prodReader = new FakeProdContentReader();
        var rawBody = "---\ntitle: Video 1\n---\nrepo body";
        prodReader.Rows.Add(Youtube(1, "vid1", bodySha256: null));
        WriteRepoBody("content-kb/test-channel/vid1.md", rawBody);
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader);

        var result = await coordinator.PullAndClassifyAsync(new ListProgress<string>(), _ => { }, CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(BodyDivergenceStatus.Indeterminate, entry.BodyDivergence);
    }

    [Fact]
    public async Task PullAndClassifyAsync_ProdNewerMismatch_StillStampsConfirmed()
    {
        var indexedUtc = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var localStore = new FakeContentSiteIndexStore();
        localStore.Rows.Add(Youtube(1, "vid1", indexedUtc: indexedUtc));
        var prodReader = new FakeProdContentReader();
        prodReader.Rows.Add(Youtube(1, "vid1", indexedUtc: indexedUtc.AddHours(1), bodySha256: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
        WriteRepoBody("content-kb/test-channel/vid1.md", "---\ntitle: Video 1\n---\nrepo body");
        var coordinator = Build(localStore, prodReader);

        var result = await coordinator.PullAndClassifyAsync(new ListProgress<string>(), _ => { }, CancellationToken.None);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(SyncDiffKind.ProdNewer, entry.Kind);
        Assert.Equal(BodyDivergenceStatus.Confirmed, entry.BodyDivergence);
    }

    [Fact]
    public async Task PullAndClassifyAsync_UnreadableBody_StampsIndeterminateWithoutFaulting()
    {
        var prodReader = new FakeProdContentReader();
        var row = Youtube(1, "vid1", bodySha256: "prod-hash");
        prodReader.Rows.Add(row);
        var path = Path.Combine(_repoRoot, row.ArtifactPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Directory.CreateDirectory(path);
        var coordinator = Build(new FakeContentSiteIndexStore(), prodReader);

        var result = await coordinator.PullAndClassifyAsync(new ListProgress<string>(), _ => { }, CancellationToken.None);

        Assert.Equal(BodyDivergenceStatus.Indeterminate, Assert.Single(result.Entries).BodyDivergence);
    }

    // ── ApplyAdoptionsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAdoptionsAsync_CleanBodyPresentInGitTree_CopiesBodyAndPreservesVisibilityWrites()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(Youtube(99, "vid1", isVisible: true, isHidden: true));
        var coordinator = Build(store, new FakeProdContentReader());
        var prodRow = Youtube(1, "vid1", "approved", bodySha256: "prod-hash", isVisible: false, isHidden: false);
        var repoPath = WriteRepoBody(prodRow.ArtifactPath, "---\ntitle: Video 1\n---\nrepo content");
        var adopt = new[] { AdoptEntry(prodRow, artifactDownloaded: true, bodyDivergence: BodyDivergenceStatus.Clean) };
        var progress = new ListProgress<IReadOnlyList<PullApplyRowResult>>();

        var results = await coordinator.ApplyAdoptionsAsync(
            adopt,
            _dataRoot,
            progress,
            new HashSet<string>(StringComparer.Ordinal),
            CancellationToken.None);

        var rr = Assert.Single(results);
        Assert.True(rr.Success);
        Assert.Contains("body copied from local repo", rr.Note, StringComparison.Ordinal);
        var liveDest = Path.Combine(_dataRoot, prodRow.ArtifactPath);
        Assert.True(File.Exists(liveDest));
        Assert.True(File.Exists(repoPath));
        Assert.Equal("---\ntitle: Video 1\n---\nrepo content", File.ReadAllText(liveDest));
        Assert.Contains("UpsertContentColumnsOnlyAsync", store.UpsertMethodCalls);
        Assert.DoesNotContain("UpsertRowAsync", store.UpsertMethodCalls);
        Assert.DoesNotContain("UpsertRowPreservingVisibilityAsync", store.UpsertMethodCalls);
        var appendedRow = store.Rows[^1];
        Assert.Equal(prodRow.Title, appendedRow.Title);
        Assert.Equal(prodRow.ArtifactPath, appendedRow.ArtifactPath);
        Assert.Equal(prodRow.BodySha256, appendedRow.BodySha256);
        var approval = Assert.Single(store.SingleApprovalCalls);
        Assert.Equal("approved", approval.Status);
        Assert.Single(progress.Items);
    }

    [Fact]
    public async Task ApplyAdoptionsAsync_InvalidArtifactPath_UpsertsAndDoesNotCopyOutsideDataRoot()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store, new FakeProdContentReader());
        var prodRow = Youtube(1, "vid1", "approved") with { ArtifactPath = "content-kb/../../evil.md" };
        var sourceOutsideRepo = Path.GetFullPath(Path.Combine(_repoRoot, prodRow.ArtifactPath));
        var traversalDest = Path.GetFullPath(Path.Combine(_dataRoot, prodRow.ArtifactPath));
        Directory.CreateDirectory(Path.GetDirectoryName(sourceOutsideRepo)!);
        File.WriteAllText(sourceOutsideRepo, "outside repo body");

        var results = await coordinator.ApplyAdoptionsAsync(
            new[] { AdoptEntry(prodRow, artifactDownloaded: true) },
            _dataRoot,
            new ListProgress<IReadOnlyList<PullApplyRowResult>>(),
            new HashSet<string>(StringComparer.Ordinal),
            CancellationToken.None);

        var rr = Assert.Single(results);
        Assert.True(rr.Success);
        Assert.Equal("row updated; body path invalid, not copied; approval mirrored from prod", rr.Note);
        Assert.Contains("UpsertContentColumnsOnlyAsync", store.UpsertMethodCalls);
        Assert.Single(store.SingleApprovalCalls);
        Assert.False(File.Exists(traversalDest));
    }

    [Fact]
    public async Task ApplyAdoptionsAsync_ConfirmedWithoutAcknowledgement_IsSkipped()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store, new FakeProdContentReader());
        var prodRow = Youtube(1, "vid1", "approved", bodySha256: "prod-hash");

        var results = await coordinator.ApplyAdoptionsAsync(
            new[] { AdoptEntry(prodRow, artifactDownloaded: true, bodyDivergence: BodyDivergenceStatus.Confirmed) },
            _dataRoot,
            new ListProgress<IReadOnlyList<PullApplyRowResult>>(),
            new HashSet<string>(StringComparer.Ordinal),
            CancellationToken.None);

        var rr = Assert.Single(results);
        Assert.True(rr.Success);
        Assert.Equal("Skipped (divergent, not acknowledged)", rr.Action);
        Assert.Empty(store.UpsertMethodCalls);
        Assert.Empty(store.SingleApprovalCalls);
    }

    [Fact]
    public async Task ApplyAdoptionsAsync_ConfirmedWithAcknowledgement_IsAdopted()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store, new FakeProdContentReader());
        var prodRow = Youtube(1, "vid1", "approved", bodySha256: "prod-hash");
        var acknowledged = new HashSet<string>(StringComparer.Ordinal) { $"{ContentSourceType.Youtube}:{prodRow.YoutubeVideoId}" };

        var results = await coordinator.ApplyAdoptionsAsync(
            new[] { AdoptEntry(prodRow, artifactDownloaded: false, bodyDivergence: BodyDivergenceStatus.Confirmed) },
            _dataRoot,
            new ListProgress<IReadOnlyList<PullApplyRowResult>>(),
            acknowledged,
            CancellationToken.None);

        Assert.Equal("Adopted", Assert.Single(results).Action);
        Assert.Contains("UpsertContentColumnsOnlyAsync", store.UpsertMethodCalls);
        Assert.Single(store.SingleApprovalCalls);
    }

    [Fact]
    public async Task ApplyAdoptionsAsync_BodyMissingIndeterminate_IsNotDefaultAdopted()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store, new FakeProdContentReader());
        var prodRow = Youtube(1, "vid1", "approved", bodySha256: "prod-hash");

        var results = await coordinator.ApplyAdoptionsAsync(
            new[] { AdoptEntry(prodRow, artifactDownloaded: false, bodyDivergence: BodyDivergenceStatus.Indeterminate) },
            _dataRoot,
            new ListProgress<IReadOnlyList<PullApplyRowResult>>(),
            new HashSet<string>(StringComparer.Ordinal),
            CancellationToken.None);

        var rr = Assert.Single(results);
        Assert.Equal("Skipped (divergent, not acknowledged)", rr.Action);
        Assert.Empty(store.UpsertMethodCalls);
    }

    [Fact]
    public async Task ApplyAdoptionsAsync_BodyMissingIndeterminate_WithAcknowledgement_UpsertsWithoutCopy()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store, new FakeProdContentReader());
        var prodRow = Youtube(1, "vid1", "approved", bodySha256: "prod-hash");
        var acknowledged = new HashSet<string>(StringComparer.Ordinal) { $"{ContentSourceType.Youtube}:{prodRow.YoutubeVideoId}" };

        var results = await coordinator.ApplyAdoptionsAsync(
            new[] { AdoptEntry(prodRow, artifactDownloaded: false, bodyDivergence: BodyDivergenceStatus.Indeterminate) },
            _dataRoot,
            new ListProgress<IReadOnlyList<PullApplyRowResult>>(),
            acknowledged,
            CancellationToken.None);

        var rr = Assert.Single(results);
        Assert.True(rr.Success);
        Assert.Equal("Adopted", rr.Action);
        Assert.Equal("row updated; body not in local git repo (prod-only or unpublished), not copied; approval mirrored from prod", rr.Note);
        Assert.Contains("UpsertContentColumnsOnlyAsync", store.UpsertMethodCalls);
        Assert.Single(store.SingleApprovalCalls);
        Assert.False(File.Exists(Path.Combine(_dataRoot, prodRow.ArtifactPath)));
    }

    [Fact]
    public async Task ApplyAdoptionsAsync_LocalOnlyOrNullProd_AreSkipped()
    {
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(store, new FakeProdContentReader());
        var localOnly = new SyncDiffEntry
        {
            NaturalKeyType = ContentSourceType.Youtube,
            NaturalKeyValue = "vidLocal",
            Kind = SyncDiffKind.LocalOnly,
            Title = "Local Only",
            ProdRow = null,
            LocalRow = Youtube(9, "vidLocal"),
            ArtifactPath = "content-kb/test-channel/vidLocal.md",
            BodyDivergence = BodyDivergenceStatus.NotApplicable,
        };
        var progress = new ListProgress<IReadOnlyList<PullApplyRowResult>>();

        var results = await coordinator.ApplyAdoptionsAsync(
            new[] { localOnly },
            _dataRoot,
            progress,
            new HashSet<string>(StringComparer.Ordinal),
            CancellationToken.None);

        Assert.Empty(results);
        Assert.Empty(store.UpsertMethodCalls);
        Assert.Empty(store.SingleApprovalCalls);
    }

    private string WriteRepoBody(string artifactPath, string body)
    {
        var path = Path.Combine(_repoRoot, artifactPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body);
        return path;
    }
}
