using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
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
             FakeGitRepository Git)
        RenderPull(
            IEnumerable<ContentSiteIndexRow>? localRows = null,
            IEnumerable<ContentSiteIndexRow>? prodRows = null,
            FakeProdContentReader? prodReaderOverride = null,
            IEnumerable<string>? missingRepoBodies = null,
            bool isProdConfigured = true,
            bool isScpConfigured = true)
    {
        var localStore = new FakeContentSiteIndexStore();
        var prodReader = prodReaderOverride ?? new FakeProdContentReader();
        var repoRoot = Path.Combine(Path.GetTempPath(), "deckflow-tests-pull-repo", Path.GetRandomFileName());
        var git = new FakeGitRepository { CannedRepoRoot = repoRoot };
        var missing = new HashSet<string>(missingRepoBodies ?? Enumerable.Empty<string>(), StringComparer.Ordinal);

        foreach (var r in localRows ?? Enumerable.Empty<ContentSiteIndexRow>())
        {
            localStore.Rows.Add(r);
        }

        foreach (var r in prodRows ?? Enumerable.Empty<ContentSiteIndexRow>())
        {
            prodReader.Rows.Add(r);
            if (!missing.Contains(r.ArtifactPath))
            {
                var repoBody = Path.Combine(repoRoot, r.ArtifactPath);
                Directory.CreateDirectory(Path.GetDirectoryName(repoBody)!);
                File.WriteAllText(repoBody, $"repo body for {r.ArtifactPath}");
            }
        }

        var configuration = new ConfigurationBuilder().Build();
        // Unique per-render temp root so concurrent tests never share the staging dir.
        var artifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-tests-pull", Path.GetRandomFileName(), "content-kb");

        Services.AddLogging();
        Services.AddSingleton<IContentSiteIndexStore>(localStore);
        Services.AddSingleton<IProdContentReader>(prodReader);
        Services.AddSingleton<IGitRepository>(git);
        Services.AddSingleton(new StudioConfig(isProdConfigured, isScpConfigured));
        Services.AddSingleton<IConfiguration>(configuration);
        Services.AddSingleton(new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot });
        // Why: the page now resolves its prod-pull + local-apply orchestration through
        // PullFromProdCoordinator (H1 split); the coordinator is built from the fakes registered
        // above (ILogger comes from AddLogging), so page behavior is unchanged.
        Services.AddSingleton<DeckFlow.Studio.ViewModels.PullFromProdCoordinator>();

        var cut = Render<PullFromProd>();
        return (cut, localStore, prodReader, git);
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
    public void Pull_ScpUnconfigured_DoesNotGatePull()
    {
        var (cut, _, _, _) = RenderPull(isScpConfigured: false);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        Assert.DoesNotContain("SCP: not configured", cut.Markup);
        Assert.False(cut.Find("button.btn-outline-primary").HasAttribute("disabled"));
    }

    // ── Stage 1 wiring + R2 ─────────────────────────────────────────────────

    [Fact]
    public void Pull_InvokesReadOnlyProdReaderAndResolvesBodiesFromRepo()
    {
        var prod = new[] { MakeRow(1, "vid-a"), MakeRow(2, "vid-b") };
        var (cut, _, prodReader, _) = RenderPull(prodRows: prod);

        Pull(cut);

        Assert.Equal(1, prodReader.ReadCallCount);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("body present: content-kb/test-channel/vid-a.md", cut.Markup);
            Assert.Contains("body present: content-kb/test-channel/vid-b.md", cut.Markup);
        });
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
    public void Resolve_AdoptProd_BodyAbsentFromRepo_StillUpserts_SkipsPromotion()
    {
        var prod = new[] { MakeRow(1, "vid-a", approvalStatus: "approved") };
        var (cut, localStore, _, _) = RenderPull(
            prodRows: prod,
            missingRepoBodies: new[] { "content-kb/test-channel/vid-a.md" });

        Pull(cut);
        // adopt-prod stays selectable even though the body is missing from the local repo tree (R4).
        cut.InvokeAsync(() => cut.Find("input[id='adopt-youtube:vid-a']").Change(true));
        cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Resolutions applied"));

        Assert.Contains("UpsertContentColumnsOnlyAsync", localStore.UpsertMethodCalls);
        Assert.Single(localStore.SingleApprovalCalls);
        Assert.Contains("body not in local repo", cut.Markup); // File.Copy skipped, no exception
        Assert.Contains("git pull", cut.Markup);
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
    public void Pull_MissingRepoBody_ProgressLineContainsOnlyRelativePath()
    {
        var (cut, _, _, _) = RenderPull(
            prodRows: new[] { MakeRow(1, "vid-a") },
            missingRepoBodies: new[] { "content-kb/test-channel/vid-a.md" });

        Pull(cut);

        cut.WaitForAssertion(() =>
            Assert.Contains("body MISSING (run 'git pull'): content-kb/test-channel/vid-a.md", cut.Markup));
        Assert.DoesNotContain(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, '/'), cut.Markup,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SSH download failed", cut.Markup);
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
        Assert.Contains("Reading production content_site_index", cut.Markup);
        Assert.Contains("Resolving", cut.Markup);
        Assert.Contains("Classifying diff", cut.Markup);
        Assert.Contains("Done —", cut.Markup);
    }

    [Fact]
    public void Pull_RendersPerBodyPresenceLines_WithRepoRelativePath()
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
            Assert.Contains("body present: content-kb/test-channel/vid-a.md", cut.Markup);
        });
    }

    [Fact]
    public void Pull_MissingBody_ProgressLine_UsesRepoRelativePath_NotLocalPath()
    {
        var prod = new[] { MakeRow(1, "vid-a") };
        var (cut, _, _, _) = RenderPull(
            prodRows: prod,
            missingRepoBodies: new[] { "content-kb/test-channel/vid-a.md" });

        Pull(cut);

        // Missing body: "body MISSING ... <ArtifactPath>" — no local filesystem path in markup.
        // WaitForAssertion: per-body line renders via a fire-and-forget Progress<T> hop.
        cut.WaitForAssertion(() =>
            Assert.Contains("body MISSING (run 'git pull'): content-kb/test-channel/vid-a.md", cut.Markup));
    }

    [Fact]
    public void Pull_ProgressPanel_NeverContainsLocalPath()
    {
        // Arrange: successful repo body resolution. The progress panel must never render the
        // absolute repo path.
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
