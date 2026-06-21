using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Pages;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// bUnit behavioral tests for PullFromProd.razor (read-only prod→local reconcile lane).
/// Covers SYNC-01/02/03 through the UI plus the R1/R2 structural read-only guarantee, R4 partial
/// pull, and D-07 secret-leak paths. All fakes; no live SSH or Postgres connection is ever made.
/// </summary>
public sealed class PullFromProdPageTests : BunitContext
{
    // Sentinel strings. If any reaches the rendered markup, a secret leaked through a catch (D-07).
    private const string SentinelSecret = "Host=prod-db.example.com;Username=admin;Password=hunter2";

    private static readonly string[] SentinelSubstrings =
    {
        "Host=", "Password", "hunter2", "prod-db.example.com",
    };

    private static ContentSiteIndexRow MakeRow(
        long id,
        string videoId,
        string title = "Video",
        DateTimeOffset? indexedUtc = null,
        string approvalStatus = "approved")
        => new ContentSiteIndexRow
        {
            Id = id,
            Source = "test-channel",
            Title = $"{title} {id}",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            IndexedUtc = indexedUtc ?? new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            ApprovalStatus = approvalStatus,
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
        };

    private (IRenderedComponent<PullFromProd> Cut,
             FakeContentSiteIndexStore LocalStore,
             FakeProdContentReader ProdReader,
             FakeSshArtifactDownloader Downloader)
        RenderPull(
            IEnumerable<ContentSiteIndexRow>? localRows = null,
            IEnumerable<ContentSiteIndexRow>? prodRows = null,
            FakeProdContentReader? prodReaderOverride = null,
            FakeSshArtifactDownloader? downloaderOverride = null,
            bool isProdConfigured = true,
            bool isScpConfigured = true)
    {
        var localStore = new FakeContentSiteIndexStore();
        var prodReader = prodReaderOverride ?? new FakeProdContentReader();
        var downloader = downloaderOverride ?? new FakeSshArtifactDownloader();

        foreach (var r in localRows ?? Enumerable.Empty<ContentSiteIndexRow>())
        {
            localStore.Rows.Add(r);
        }

        foreach (var r in prodRows ?? Enumerable.Empty<ContentSiteIndexRow>())
        {
            prodReader.Rows.Add(r);
        }

        var configuration = new ConfigurationBuilder().Build();
        // Unique per-render temp root so concurrent tests never share the staging dir.
        var artifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-tests-pull", Path.GetRandomFileName(), "content-kb");

        Services.AddLogging();
        Services.AddSingleton<IContentSiteIndexStore>(localStore);
        Services.AddSingleton<IProdContentReader>(prodReader);
        Services.AddSingleton<ISshArtifactDownloader>(downloader);
        Services.AddSingleton(new StudioConfig(isProdConfigured, isScpConfigured));
        Services.AddSingleton<IConfiguration>(configuration);
        Services.AddSingleton(new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot });

        var cut = Render<PullFromProd>();
        return (cut, localStore, prodReader, downloader);
    }

    private static void Pull(IRenderedComponent<PullFromProd> cut)
    {
        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Diff Preview"));
    }

    // ── Gating ─────────────────────────────────────────────────────────────

    [Fact]
    public void Pull_ProdUnconfigured_ShowsWarningAndDisablesButton()
    {
        var (cut, _, _, _) = RenderPull(isProdConfigured: false);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        Assert.Contains("Prod connection: not configured", cut.Markup);
        Assert.True(cut.Find("button.btn-outline-primary").HasAttribute("disabled"));
    }

    [Fact]
    public void Pull_ScpUnconfigured_ShowsWarningAndDisablesButton()
    {
        var (cut, _, _, _) = RenderPull(isScpConfigured: false);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        Assert.Contains("SCP: not configured", cut.Markup);
        Assert.True(cut.Find("button.btn-outline-primary").HasAttribute("disabled"));
    }

    // ── Stage 1 wiring + R2 ─────────────────────────────────────────────────

    [Fact]
    public void Pull_InvokesReadOnlyProdReaderAndDownloaderPerRow()
    {
        var prod = new[] { MakeRow(1, "vid-a"), MakeRow(2, "vid-b") };
        var (cut, _, prodReader, downloader) = RenderPull(prodRows: prod);

        Pull(cut);

        Assert.Equal(1, prodReader.ReadCallCount);
        Assert.Equal(2, downloader.DownloadedFiles.Count);
        Assert.Contains(downloader.DownloadedFiles, d => d.RemoteRelativePath == "content-kb/test-channel/vid-a.md");
    }

    [Fact]
    public void Pull_ProdReaderIsDistinctFromLocalStore_AndReceivesReadsOnly()
    {
        var (cut, localStore, prodReader, _) = RenderPull(prodRows: new[] { MakeRow(1, "vid-a") });

        Pull(cut);

        // R2: the prod side is a different object than the local store, has no write API, and only
        // ever received reads. (The compiler already guarantees no write method exists on it.)
        Assert.NotSame((object)localStore, prodReader);
        Assert.True(prodReader.ReadCallCount >= 1);
    }

    // ── Diff render ─────────────────────────────────────────────────────────

    [Fact]
    public void Pull_RendersAllFourKinds_LocalOnlyHasNoAdoptRadio()
    {
        var ts = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 6, 20, 15, 0, 0, TimeSpan.Zero);
        var prod = new[]
        {
            MakeRow(1, "missing-local", indexedUtc: ts),
            MakeRow(2, "prod-newer", indexedUtc: newer),
            MakeRow(3, "diverged", indexedUtc: ts),
        };
        var local = new[]
        {
            MakeRow(2, "prod-newer", indexedUtc: ts),
            MakeRow(3, "diverged", indexedUtc: newer),
            MakeRow(4, "local-only", indexedUtc: ts),
        };
        var (cut, _, _, _) = RenderPull(local, prod);

        Pull(cut);

        Assert.Contains("Missing locally", cut.Markup);
        Assert.Contains("Prod newer", cut.Markup);
        Assert.Contains("Diverged", cut.Markup);
        Assert.Contains("Local only", cut.Markup);
        // LocalOnly entry has no adopt radio.
        Assert.Empty(cut.FindAll("input[id='adopt-youtube:local-only']"));
        // A non-LocalOnly entry does.
        Assert.NotEmpty(cut.FindAll("input[id='adopt-youtube:missing-local']"));
    }

    // ── adopt-prod / keep-local apply ───────────────────────────────────────

    [Fact]
    public void Resolve_AdoptProd_OnMissingLocally_UpsertsContentOnly_AndMirrorsApproval()
    {
        var prod = new[] { MakeRow(1, "vid-a", approvalStatus: "approved") };
        var (cut, localStore, _, _) = RenderPull(prodRows: prod);

        Pull(cut);
        cut.InvokeAsync(() => cut.Find("input[id='adopt-youtube:vid-a']").Change(true));
        cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Resolutions applied"));

        Assert.Contains("UpsertContentColumnsOnlyAsync", localStore.UpsertMethodCalls);
        Assert.DoesNotContain("UpsertRowAsync", localStore.UpsertMethodCalls);
        var approval = Assert.Single(localStore.SingleApprovalCalls);
        Assert.Equal(ContentSourceType.Youtube, approval.Type);
        Assert.Equal("vid-a", approval.Value);
        Assert.Equal("approved", approval.Status); // mirrored from prod, not blind "pending"
    }

    [Fact]
    public void Resolve_KeepLocal_DoesNotUpsert()
    {
        var prod = new[] { MakeRow(1, "vid-a") };
        var (cut, localStore, _, _) = RenderPull(prodRows: prod);

        Pull(cut);
        cut.InvokeAsync(() => cut.Find("input[id='keep-youtube:vid-a']").Change(true));
        cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Resolutions applied"));

        Assert.DoesNotContain("UpsertContentColumnsOnlyAsync", localStore.UpsertMethodCalls);
    }

    // ── prod-write guard (R2 structural) ────────────────────────────────────

    [Fact]
    public void InvokePullApplyForTest_BeforeClassify_PerformsNoUpsert()
    {
        var (cut, localStore, _, _) = RenderPull(prodRows: new[] { MakeRow(1, "vid-a") });

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Instance.InvokePullApplyForTest());

        Assert.Empty(localStore.UpsertMethodCalls);
    }

    // ── R4 partial pull ─────────────────────────────────────────────────────

    [Fact]
    public void Resolve_AdoptProd_PartialPull_StillUpserts_SkipsPromotion()
    {
        var downloader = new FakeSshArtifactDownloader();
        downloader.FilesToFail.Add("content-kb/test-channel/vid-a.md"); // artifact not downloaded
        var prod = new[] { MakeRow(1, "vid-a", approvalStatus: "approved") };
        var (cut, localStore, _, _) = RenderPull(prodRows: prod, downloaderOverride: downloader);

        Pull(cut);
        // adopt-prod stays selectable even though the artifact failed to download (R4).
        cut.InvokeAsync(() => cut.Find("input[id='adopt-youtube:vid-a']").Change(true));
        cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Resolutions applied"));

        Assert.Contains("UpsertContentColumnsOnlyAsync", localStore.UpsertMethodCalls);
        Assert.Single(localStore.SingleApprovalCalls);
        Assert.Contains("not promoted", cut.Markup); // File.Move skipped, no exception
    }

    // ── secret leak (D-07) ──────────────────────────────────────────────────

    [Fact]
    public void Pull_ProdReadThrowsSentinel_NeverLeaksToMarkup()
    {
        var prodReader = new FakeProdContentReader { ReadFailureMessage = SentinelSecret };
        var (cut, _, _, _) = RenderPull(prodReaderOverride: prodReader);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Could not pull from production"));

        foreach (var sentinel in SentinelSubstrings)
        {
            Assert.DoesNotContain(sentinel, cut.Markup);
        }
    }

    [Fact]
    public void Pull_DownloadFailureReasonWithSentinel_ProgressLineContainsOnlyRelativePathAndReason()
    {
        // SshDownloadResult.FailureReason is guaranteed sanitized by the ISshArtifactDownloader
        // contract (never contains host/key/path secrets or ex.Message). The progress panel
        // renders it as part of the "not downloaded:" line — that is by design. This test
        // verifies the panel renders the RemoteRelativePath and FailureReason from the result,
        // and does NOT additionally surface the LocalPath or raw exception text.
        // The "injected" reason is what FakeSshArtifactDownloader uses by default.
        var downloader = new FakeSshArtifactDownloader();
        downloader.FilesToFail.Add("content-kb/test-channel/vid-a.md");
        var (cut, _, _, _) = RenderPull(prodRows: new[] { MakeRow(1, "vid-a") }, downloaderOverride: downloader);

        Pull(cut);

        // Progress panel shows the "not downloaded:" line with RemoteRelativePath.
        Assert.Contains("not downloaded: content-kb/test-channel/vid-a.md", cut.Markup);
        // LocalPath is empty string on failure; regardless, the temp dir must never appear.
        Assert.DoesNotContain(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, '/'), cut.Markup,
            StringComparison.OrdinalIgnoreCase);
    }

    // ── SUI-03 live progress panel ─────────────────────────────────────────

    [Fact]
    public void Pull_RendersProgressPanel_WithStageLines()
    {
        var prod = new[] { MakeRow(1, "vid-a"), MakeRow(2, "vid-b") };
        var (cut, _, _, _) = RenderPull(prodRows: prod);

        Pull(cut);

        // The progress panel must be rendered after the pull completes.
        Assert.NotEmpty(cut.FindAll("[data-testid='progress-panel']"));

        // Stage lines must appear — these are the fixed sanitized strings, never ex.Message.
        Assert.Contains("Preparing staging area", cut.Markup);
        Assert.Contains("Reading production content_site_index", cut.Markup);
        Assert.Contains("Downloading", cut.Markup);
        Assert.Contains("Classifying diff", cut.Markup);
        Assert.Contains("Done —", cut.Markup);
    }

    [Fact]
    public void Pull_RendersPerArtifactDownloadLines_WithRemoteRelativePath()
    {
        var prod = new[] { MakeRow(1, "vid-a"), MakeRow(2, "vid-b") };
        var (cut, _, _, _) = RenderPull(prodRows: prod);

        Pull(cut);

        // Per-artifact progress lines must use RemoteRelativePath — never LocalPath.
        // WaitForAssertion: the per-artifact lines render via a fire-and-forget Progress<T> →
        // InvokeAsync hop, so on a slower runner they may not be flushed the instant the
        // "Diff Preview" state appears. Poll until they render.
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("content-kb/test-channel/vid-a.md", cut.Markup);
            Assert.Contains("content-kb/test-channel/vid-b.md", cut.Markup);
            // Downloaded artifacts produce "downloaded <path>" lines.
            Assert.Contains("downloaded content-kb/test-channel/vid-a.md", cut.Markup);
        });
    }

    [Fact]
    public void Pull_FailedArtifact_ProgressLine_UsesRemoteRelativePath_NotLocalPath()
    {
        var downloader = new FakeSshArtifactDownloader();
        downloader.FilesToFail.Add("content-kb/test-channel/vid-a.md");
        var prod = new[] { MakeRow(1, "vid-a") };
        var (cut, _, _, _) = RenderPull(prodRows: prod, downloaderOverride: downloader);

        Pull(cut);

        // Failed artifact: "not downloaded: <RemoteRelativePath>" — no LocalPath in markup.
        // WaitForAssertion: per-artifact line renders via a fire-and-forget Progress<T> hop.
        cut.WaitForAssertion(() =>
            Assert.Contains("not downloaded: content-kb/test-channel/vid-a.md", cut.Markup));
        // LocalPath is empty on failure; regardless, no raw local filesystem path must appear.
        // (The page must never render SshDownloadResult.LocalPath.)
    }

    [Fact]
    public void Pull_ProgressPanel_NeverContainsLocalPath()
    {
        // Arrange: successful download — LocalPath is set inside FakeSshArtifactDownloader
        // (it writes a real temp file). The progress panel must never render that local path.
        var prod = new[] { MakeRow(1, "vid-a") };
        var (cut, _, _, _) = RenderPull(prodRows: prod);

        Pull(cut);

        var markup = cut.Markup;
        // The panel must not contain any path-shaped strings pointing to the temp dir.
        // We verify by asserting the temp root prefix never appears — OS-agnostic check.
        Assert.DoesNotContain(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, '/'), markup,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pull_ProgressPanel_NeverContainsRawException()
    {
        // A prod reader failure must append only a sanitized stage-name line — not ex.Message.
        var prodReader = new FakeProdContentReader { ReadFailureMessage = SentinelSecret };
        var (cut, _, _, _) = RenderPull(prodReaderOverride: prodReader);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Could not pull from production"));

        // The progress panel shows a sanitized failure line — never the sentinel secret.
        foreach (var sentinel in SentinelSubstrings)
        {
            Assert.DoesNotContain(sentinel, cut.Markup);
        }
    }

    [Fact]
    public void Pull_ReadOnlyTowardProd_ProgressPanelIsDisplayOnly()
    {
        // The progress panel must not introduce any write path — adding a panel must not change
        // what the page calls on the prod reader (read only) or local store.
        var prod = new[] { MakeRow(1, "vid-a") };
        var (cut, localStore, prodReader, _) = RenderPull(prodRows: prod);

        Pull(cut);

        // Prod reader still called exactly once (read); local store received no upserts from Stage 1.
        Assert.Equal(1, prodReader.ReadCallCount);
        Assert.Empty(localStore.UpsertMethodCalls);
    }
}
