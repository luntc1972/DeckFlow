using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio;
using DeckFlow.Studio.Pages;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// bUnit behavioral tests for DirectPush.razor (Direct Prod-DB + SCP publish path).
/// Covers PUB-04 / PUB-05 and SC1–SC5, plus the three Codex-review additions
/// (HIGH-2 diff-read + DB-write secret-leak paths and the MEDIUM-1 Stage-3 hard-guard).
/// All fakes; no live SSH or Postgres connection is ever made.
/// </summary>
public sealed class DirectPushPageTests : BunitContext
{
    // Sentinel connection string used by the HIGH-2 secret-leak tests. If any substring of this
    // reaches the rendered markup, ex.Message leaked through a catch block (D-07 / SC5 violation).
    private const string SentinelSecret = "Host=prod-db.example.com;Username=admin;Password=hunter2";

    private static readonly string[] SentinelSubstrings =
    {
        "Host=", "Password", "hunter2", "prod-db.example.com",
    };

    // ── Setup helpers ────────────────────────────────────────────────────────

    private static ContentSiteIndexRow MakeApprovedRow(long id, string videoId)
        => new ContentSiteIndexRow
        {
            Id = id,
            Source = "test-channel",
            Title = $"Video {id}",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            IndexedUtc = DateTimeOffset.UtcNow,
            ApprovalStatus = "approved",
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
        };

    private (IRenderedComponent<DirectPush> Cut,
             FakeContentSiteIndexStore LocalStore,
             FakeContentSiteIndexStore ProdStore,
             FakeSshArtifactUploader Uploader,
             FakeProdStoreFactory ProdFactory)
        RenderDirectPush(
            IEnumerable<ContentSiteIndexRow>? localApproved = null,
            IEnumerable<ContentSiteIndexRow>? prodRows = null,
            FakeContentSiteIndexStore? prodStoreOverride = null,
            bool isProdConfigured = true,
            bool isScpConfigured = true)
    {
        var localStore = new FakeContentSiteIndexStore();
        var prodStore = prodStoreOverride ?? new FakeContentSiteIndexStore();
        var uploader = new FakeSshArtifactUploader();
        var prodFactory = new FakeProdStoreFactory(prodStore);

        foreach (var r in localApproved ?? Enumerable.Empty<ContentSiteIndexRow>())
        {
            localStore.Rows.Add(r);
        }

        foreach (var r in prodRows ?? Enumerable.Empty<ContentSiteIndexRow>())
        {
            prodStore.Rows.Add(r);
        }

        // Why: an in-memory config with no Studio:ProdConnectionString value — the
        // FakeProdStoreFactory ignores the connection string entirely, so no secret is needed
        // (and none must be present, per SC5).
        var configuration = new ConfigurationBuilder().Build();
        var artifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-tests-dp", "content-kb");

        Services.AddSingleton<IContentSiteIndexStore>(localStore);
        Services.AddSingleton<ISshArtifactUploader>(uploader);
        Services.AddSingleton<IProdStoreFactory>(prodFactory);
        Services.AddSingleton(new StudioConfig(isProdConfigured, isScpConfigured));
        Services.AddSingleton<IConfiguration>(configuration);
        Services.AddSingleton(new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot });

        var cut = Render<DirectPush>();
        return (cut, localStore, prodStore, uploader, prodFactory);
    }

    // Drives the page through Stage 1 (Compute Prod Diff) and checks the confirmation box.
    private static void ComputeDiffAndConfirm(IRenderedComponent<DirectPush> cut)
    {
        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Diff Preview"));
        cut.InvokeAsync(() => cut.Find("input#prodReviewed").Change(true));
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DirectPush_DiffPreview_ShowsNewUpdatedCounts()
    {
        // PUB-05/SC1: 1 new key + 1 key already in prod -> New: 1, Updated: 1.
        var local = new[] { MakeApprovedRow(1, "vid-new"), MakeApprovedRow(2, "vid-existing") };
        var prod = new[] { MakeApprovedRow(2, "vid-existing") };
        var (cut, _, _, _, _) = RenderDirectPush(local, prod);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("New: 1", cut.Markup);
            Assert.Contains("Updated: 1", cut.Markup);
        });
    }

    [Fact]
    public void DirectPush_CheckboxGates_ScpButton()
    {
        // PUB-04/SC2: the SCP (btn-danger) button is disabled until the confirmation checkbox is checked.
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _) = RenderDirectPush(local);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Diff Preview"));

        // Before checking: SCP button disabled.
        cut.WaitForAssertion(() =>
        {
            var scpBtn = cut.Find("button.btn-danger");
            Assert.True(scpBtn.HasAttribute("disabled"),
                "Stage-2 SCP button must be disabled until the confirmation checkbox is checked");
        });

        // Check the box.
        cut.InvokeAsync(() => cut.Find("input#prodReviewed").Change(true));

        cut.WaitForAssertion(() =>
        {
            var scpBtn = cut.Find("button.btn-danger");
            Assert.False(scpBtn.HasAttribute("disabled"),
                "Stage-2 SCP button must enable once the confirmation checkbox is checked");
        });
    }

    [Fact]
    public void DirectPush_Stage3Locked_UntilScpSuccess()
    {
        // PUB-04/SC2: Stage-3 (DB) button disabled before SCP; enabled after full SCP success.
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _) = RenderDirectPush(local);

        ComputeDiffAndConfirm(cut);

        // After diff + checkbox but before SCP: Stage-3 disabled (it is the second btn-danger).
        cut.WaitForAssertion(() =>
        {
            var dbBtn = cut.FindAll("button.btn-danger")[1];
            Assert.True(dbBtn.HasAttribute("disabled"),
                "Stage-3 DB button must be disabled before SCP succeeds");
        });

        // Run a fully-successful SCP.
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());

        cut.WaitForAssertion(() =>
        {
            var dbBtn = cut.FindAll("button.btn-danger")[1];
            Assert.False(dbBtn.HasAttribute("disabled"),
                "Stage-3 DB button must enable after full SCP success");
        });
    }

    [Fact]
    public void DirectPush_UsesContentColumnsOnlyUpsert()
    {
        // PUB-04/SC3/D-08: only UpsertContentColumnsOnlyAsync runs on the prod store.
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var (cut, _, prodStore, _, _) = RenderDirectPush(local);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));

        // Click Stage-3 DB write.
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, prodStore.UpsertMethodCalls.Count(c => c == "UpsertContentColumnsOnlyAsync"));
            Assert.DoesNotContain("UpsertRowAsync", prodStore.UpsertMethodCalls);
            Assert.DoesNotContain("UpsertRowPreservingVisibilityAsync", prodStore.UpsertMethodCalls);
        });
    }

    [Fact]
    public void DirectPush_ScpPartialFailure_Stage3Locked()
    {
        // PUB-05/SC4: one failed file -> Failed badge + Stage-3 stays locked.
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var (cut, _, _, uploader, _) = RenderDirectPush(local);

        // Fail the second row's artifact (keyed by remote relative path = ArtifactPath).
        uploader.FilesToFail.Add("content-kb/test-channel/vid2.md");

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed", cut.Markup);
            Assert.Contains("see the per-file list below", cut.Markup);
            var dbBtn = cut.FindAll("button.btn-danger")[1];
            Assert.True(dbBtn.HasAttribute("disabled"),
                "Stage-3 DB button must stay locked after an SCP partial failure");
        });
    }

    [Fact]
    public void DirectPush_DbPartialFailure_PerRowListShown()
    {
        // PUB-05/SC4: one failed row -> per-row Failed; SCP success summary not re-locked/removed.
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var prodStore = new FakeContentSiteIndexStore();
        prodStore.KeysToFailOnUpsert.Add("vid2");
        var (cut, _, _, _, _) = RenderDirectPush(local, prodStoreOverride: prodStore);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));

        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            // Per-row failure surfaced.
            Assert.Contains("Failed", cut.Markup);
            Assert.Contains("reconcile only the failed rows", cut.Markup);
            // SCP success summary still present — Stage 2 not re-locked.
            Assert.Contains("uploaded to production /data", cut.Markup);
        });
    }

    [Fact]
    public void DirectPush_Secrets_NeverInMarkup()
    {
        // SC5: presence-only render — no conn-string/host substrings; "PRODUCTION" present.
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _) = RenderDirectPush(local);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));

        Assert.Contains("PRODUCTION", cut.Markup);
        Assert.DoesNotContain("postgres", cut.Markup, StringComparison.OrdinalIgnoreCase);
        foreach (var s in SentinelSubstrings)
        {
            Assert.DoesNotContain(s, cut.Markup);
        }
    }

    // PUB-04/SC2/D-09: not-configured (prod and/or SCP missing) -> warning banner names the
    // missing item(s) and the Stage-1 button is disabled. Driven as a [Theory] so each variant
    // renders into its own BunitContext — a single test instance cannot register new services
    // after the first Render<DirectPush>() resolves them (bUnit one-render-per-context rule).
    [Theory]
    [InlineData(false, false, "Prod connection: not configured", "SCP: not configured")]
    [InlineData(true, false, "SCP: not configured", null)]
    [InlineData(false, true, "Prod connection: not configured", null)]
    public void DirectPush_NotConfigured_ButtonsDisabled(
        bool isProdConfigured,
        bool isScpConfigured,
        string expectedBanner,
        string? alsoExpectedBanner)
    {
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _) = RenderDirectPush(
            local, isProdConfigured: isProdConfigured, isScpConfigured: isScpConfigured);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));

        Assert.Contains(expectedBanner, cut.Markup);
        if (alsoExpectedBanner is not null)
        {
            Assert.Contains(alsoExpectedBanner, cut.Markup);
        }

        Assert.True(cut.Find("button.btn-outline-primary").HasAttribute("disabled"),
            "Compute Prod Diff must be disabled when prod and/or SCP is not configured");
    }

    [Fact]
    public void DirectPush_DiffReadFailure_SecretsNeverSurface()
    {
        // Codex HIGH-2: prod read throws a sentinel-bearing message -> sanitized copy shown,
        // none of the sentinel substrings reach the markup.
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var prodStore = new FakeContentSiteIndexStore { ReadFailureMessage = SentinelSecret };
        var (cut, _, _, _, _) = RenderDirectPush(local, prodStoreOverride: prodStore);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());

        cut.WaitForAssertion(() => Assert.Contains("Could not read production", cut.Markup));

        foreach (var s in SentinelSubstrings)
        {
            Assert.DoesNotContain(s, cut.Markup);
        }
    }

    [Fact]
    public void DirectPush_DbWriteFailure_SecretsNeverSurface()
    {
        // Codex HIGH-2: prod upsert throws a sentinel-bearing message on one row -> sanitized
        // Reason cell shown, none of the sentinel substrings reach the markup.
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var prodStore = new FakeContentSiteIndexStore { UpsertFailureMessage = SentinelSecret };
        prodStore.KeysToFailOnUpsert.Add("vid1");
        var (cut, _, _, _, _) = RenderDirectPush(local, prodStoreOverride: prodStore);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));

        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() => Assert.Contains("Prod upsert failed for this row", cut.Markup));

        foreach (var s in SentinelSubstrings)
        {
            Assert.DoesNotContain(s, cut.Markup);
        }
    }

    [Fact]
    public void DirectPush_Stage3InvokedBeforeScp_NoUpsert()
    {
        // Codex MEDIUM-1: invoking Stage 3 before SCP success must early-return; prod upsert
        // is never called (UpsertMethodCalls stays empty).
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, prodStore, _, _) = RenderDirectPush(local);

        // Compute diff + confirm, but do NOT run SCP — Stage 3 is still locked.
        ComputeDiffAndConfirm(cut);

        // Force-invoke the Stage-3 handler directly (bypassing the disabled button) to exercise
        // the hard-guard. The component instance method is private, so drive it via the click on
        // the disabled button which the dispatcher will route to the handler; the guard must
        // return early because _scpSuccess is false.
        cut.InvokeAsync(() => cut.Instance.InvokeWriteRowsForTest());

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(prodStore.UpsertMethodCalls);
        });
    }
}
