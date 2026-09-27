using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio;
using DeckFlow.Studio.Pages;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Tests;

// Why: M3 — minimal ILogger capture so tests can assert that exceptions reach the Serilog
// sink via ILogger (not the markup), without pulling in a heavyweight logging library.
internal sealed class CapturingLogger : ILogger
{
    public List<(LogLevel Level, Exception? Exception, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, exception, formatter(state, exception)));
    }
}

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public CapturingLogger Logger { get; } = new();

    public ILogger CreateLogger(string categoryName) => Logger;

    public void Dispose() { }
}

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
             FakeProdStoreFactory ProdFactory,
             CapturingLogger CapturedLog)
        RenderDirectPush(
            IEnumerable<ContentSiteIndexRow>? localApproved = null,
            IEnumerable<ContentSiteIndexRow>? prodRows = null,
            FakeContentSiteIndexStore? prodStoreOverride = null,
            bool isProdConfigured = true,
            bool isScpConfigured = true,
            bool isConfirmerConfigured = true,
            FakeContentKbOrchestrator? orchestratorOverride = null,
            IProdContentReader? prodReaderOverride = null,
            bool directPushGitBodyOn = false)
    {
        var localStore = new FakeContentSiteIndexStore();
        var prodStore = prodStoreOverride ?? new FakeContentSiteIndexStore();
        var uploader = new FakeSshArtifactUploader();
        var prodFactory = new FakeProdStoreFactory(prodStore);
        var logProvider = new CapturingLoggerProvider();

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
        Services.AddSingleton(new StudioConfig(isProdConfigured, isScpConfigured, isConfirmerConfigured));
        Services.AddSingleton<IConfiguration>(configuration);
        Services.AddSingleton<IStudioProdConnectionSource>(new StudioProdConnectionSource(configuration));
        Services.AddSingleton<IPrivateKbRootProvider>(new StudioPrivateKbRootProvider(null, Path.GetTempPath()));
        var suppressionStore = new FakeCreatorSuppressionStore();
        Services.AddSingleton<ICreatorSuppressionStore>(suppressionStore);
        Services.AddSingleton<ICreatorIdentityResolver>(new FakeCreatorIdentityResolver());
        Services.AddSingleton(new CreatorSuppressionSyncCoordinator(
            suppressionStore,
            prodFactory,
            new FakeStudioProdConnectionSource()));
        Services.AddSingleton(new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot });
        Services.AddSingleton<IContentKbOrchestrator>(orchestratorOverride ?? new FakeContentKbOrchestrator());
        // Why (90-04): the coordinator's ReadFlagAsync dependency (D-04) — flag OFF by default so
        // [skip render] behavior in these bUnit page tests stays byte-identical to before this flag
        // existed (D-05). Tests that exercise the fail-closed flag guard pass directPushGitBodyOn: true.
        Services.AddSingleton<IProdContentReader>(
            prodReaderOverride ?? new FakeDirectPushFlagReader { FlagValue = directPushGitBodyOn });
        // Why: the page now resolves its orchestration through DirectPushCoordinator (H1 split);
        // register it over the same fakes so the bUnit render wires up identically to production.
        Services.AddScoped<DirectPushCoordinator>();
        Services.AddSingleton<CreatorSuppressionRowFilter>();
        // Why: M3 — wire a capturing logger so tests can assert exceptions reach the
        // Serilog sink (ILogger<DirectPush>) without inspecting rendered markup.
        Services.AddLogging(b => b.AddProvider(logProvider));

        var cut = Render<DirectPush>();
        return (cut, localStore, prodStore, uploader, prodFactory, logProvider.Logger);
    }

    // Drives the page through Stage 1 (Compute Prod Diff) and checks the confirmation box.
    private static void ComputeDiffAndConfirm(IRenderedComponent<DirectPush> cut)
    {
        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Diff Preview"));
        cut.InvokeAsync(() => cut.Find("input#prodReviewed").Change(true));
    }

    // Drives the page through Stage 1 (diff+confirm) → Stage 2 (SCP) → Stage 3 (DB write) →
    // Stage 4 (git commit+push), leaving the page with the Stage 5 button enabled (_gitSuccess) so
    // Stage-5-specific tests can begin there without repeating the same four-stage boilerplate.
    private static void DriveThroughStage4(IRenderedComponent<DirectPush> cut)
    {
        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());
        cut.WaitForState(() => cut.Markup.Contains("written to production"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-outline-primary")[^1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-outline-primary")[^1].Click());
        cut.WaitForAssertion(() => Assert.False(cut.Find("button.btn-success").HasAttribute("disabled")));
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DirectPush_DiffPreview_ShowsNewUpdatedCounts()
    {
        // PUB-05/SC1 (M2): 1 new key + 1 key in prod with different title -> New: 1, Updated: 1.
        // Why (M2): the prod row must have different content (title) from the local row so it is
        // classified as Updated rather than Unchanged (content-aware diff, not presence-only).
        var local = new[] { MakeApprovedRow(1, "vid-new"), MakeApprovedRow(2, "vid-existing") with { Title = "New Title" } };
        var prod = new[] { MakeApprovedRow(2, "vid-existing") with { Title = "Old Title" } };
        var (cut, _, _, _, _, _) = RenderDirectPush(local, prod);

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
        var (cut, _, _, _, _, _) = RenderDirectPush(local);

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
        var (cut, _, _, _, _, _) = RenderDirectPush(local);

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
        // PUB-04/SC3/D-08 (H4): only UpsertContentColumnsOnlyBatchAsync runs on the prod store
        // (a single batch call replaces the old per-row loop — H4 transactional batch).
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var (cut, _, prodStore, _, _, _) = RenderDirectPush(local);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));

        // Click Stage-3 DB write.
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            // Exactly one batch call (not per-row calls).
            Assert.Equal(1, prodStore.UpsertMethodCalls.Count(c => c == "UpsertContentColumnsOnlyBatchAsync"));
            Assert.DoesNotContain("UpsertContentColumnsOnlyAsync", prodStore.UpsertMethodCalls);
            Assert.DoesNotContain("UpsertRowAsync", prodStore.UpsertMethodCalls);
            Assert.DoesNotContain("UpsertRowPreservingVisibilityAsync", prodStore.UpsertMethodCalls);
        });
    }

    [Fact]
    public void DirectPush_ScpPartialFailure_Stage3Locked()
    {
        // PUB-05/SC4: one failed file -> Failed badge + Stage-3 stays locked.
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var (cut, _, _, uploader, _, _) = RenderDirectPush(local);

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
    public void DirectPush_DbBatchFailure_AllOrNothingRollback_MessageShown()
    {
        // PUB-05/SC4 (H4): batch throws ContentSiteIndexBatchUpsertException → all-or-nothing:
        // zero rows committed, failure rows show "Rolled back", rollback copy shown.
        // SCP success summary still present — Stage 2 not re-locked.
        // Why (H4): replaced per-row partial-failure model; a single failure rolls back the
        // entire batch rather than leaving prod partially written.
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var prodStore = new FakeContentSiteIndexStore();
        prodStore.KeysToFailOnUpsert.Add("vid2");
        var (cut, _, _, _, _, _) = RenderDirectPush(local, prodStoreOverride: prodStore);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));

        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            // Rollback message shown (all-or-nothing).
            Assert.Contains("NOTHING was written to production", cut.Markup);
            // Per-row "Rolled back" status shown.
            Assert.Contains("Rolled back", cut.Markup);
            // SCP success summary still present — Stage 2 not re-locked.
            Assert.Contains("uploaded to production /data", cut.Markup);
            // Zero rows committed.
            Assert.Empty(prodStore.Rows);
        });
    }

    [Fact]
    public void DirectPush_Secrets_NeverInMarkup()
    {
        // SC5: presence-only render — no conn-string/host substrings; "PRODUCTION" present.
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _, _) = RenderDirectPush(local);

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
        var (cut, _, _, _, _, _) = RenderDirectPush(
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
    public void DirectPush_ConfirmerNotConfigured_DoesNotBlockStage1()
    {
        // Direct publishing no longer waits for a deployment confirmation.
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _, _) = RenderDirectPush(local, isConfirmerConfigured: false);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));

        Assert.DoesNotContain("Deploy-confirm", cut.Markup);
        Assert.False(cut.Find("button.btn-outline-primary").HasAttribute("disabled"));
    }

    [Fact]
    public void DirectPush_ConfirmerConfigured_DeployConfirmStatusNotShown()
    {
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _, _) = RenderDirectPush(local, isConfirmerConfigured: true);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));

        Assert.DoesNotContain("Deploy-confirm", cut.Markup);
    }

    [Fact]
    public void DirectPush_DiffReadFailure_SecretsNeverSurface()
    {
        // Codex HIGH-2: prod read throws a sentinel-bearing message -> sanitized copy shown,
        // none of the sentinel substrings reach the markup.
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var prodStore = new FakeContentSiteIndexStore { ReadFailureMessage = SentinelSecret };
        var (cut, _, _, _, _, _) = RenderDirectPush(local, prodStoreOverride: prodStore);

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
        // Codex HIGH-2 (H4): prod batch upsert throws ContentSiteIndexBatchUpsertException
        // carrying a sentinel-bearing UpsertFailureMessage in InnerException → sanitized
        // rollback copy shown; none of the sentinel substrings reach the markup (D-07 / SC5).
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var prodStore = new FakeContentSiteIndexStore { UpsertFailureMessage = SentinelSecret };
        prodStore.KeysToFailOnUpsert.Add("vid1");
        var (cut, _, _, _, _, _) = RenderDirectPush(local, prodStoreOverride: prodStore);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));

        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        // H4 rollback copy — never "reconcile only the failed rows" (that was pre-H4).
        cut.WaitForAssertion(() => Assert.Contains("NOTHING was written to production", cut.Markup));

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
        var (cut, _, prodStore, _, _, _) = RenderDirectPush(local);

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









    [Fact]
    public void DirectPush_Success_SetsLocalAwaitingConfirmMarker_NoStampOnEitherStore()
    {
        // D-06/D-07: Stage 3 is now content-only — pushed_to_prod_utc is stamped only in the
        // (not-yet-UI-wired) post-confirm path, never at content-upsert time. The local store gets
        // the durable D-10 awaiting-confirm marker instead.
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var (cut, localStore, prodStore, _, _, _) = RenderDirectPush(local);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));

        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(localStore.StampCalls);
            Assert.Empty(prodStore.StampCalls);

            Assert.Single(localStore.SetAwaitingConfirmCalls);
            Assert.Equal(2, localStore.SetAwaitingConfirmCalls[0].Keys.Count);
            Assert.All(localStore.Rows, row => Assert.Null(row.PushedToProdUtc));
            Assert.All(prodStore.Rows, row => Assert.Null(row.PushedToProdUtc));
        });
    }

    [Fact]
    public void ComputeDiffAsync_ReadOnlyDiff_EnsureSchemaNotCalledOnProdStore()
    {
        // H3: the diff path is strictly read-only — EnsureSchemaAsync must never be called
        // on the prod store (prod schema is managed by the DeckFlow.Web app startup).
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, prodStore, _, _, _) = RenderDirectPush(local);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Diff Preview"));

        Assert.Equal(0, prodStore.EnsureSchemaCallCount);
    }

    [Fact]
    public void ComputeDiffAsync_DiffReadFailure_LogsErrorWithException()
    {
        // M3: when the prod-store read throws, Logger.LogError must be called with the exception
        // (so "see logs" is true). The exception must NOT appear in the rendered markup (SC5/D-07).
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var prodStore = new FakeContentSiteIndexStore { ReadFailureMessage = SentinelSecret };
        var (cut, _, _, _, _, capturedLog) = RenderDirectPush(local, prodStoreOverride: prodStore);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());

        cut.WaitForAssertion(() => Assert.Contains("Could not read production", cut.Markup));

        // Exactly one Error-level entry with a non-null exception must have been logged.
        Assert.True(capturedLog.Entries.Any(e => e.Level == LogLevel.Error && e.Exception is not null),
            "Expected at least one Error-level log entry with an exception from the diff failure");

        // Sentinel substrings must NOT appear in the rendered markup (secret-leak guard).
        foreach (var s in SentinelSubstrings)
        {
            Assert.DoesNotContain(s, cut.Markup);
        }
    }

    [Fact]
    public void DirectPush_Success_RowsStayHidden_AwaitingConfirm_NotYetPublishedVisible()
    {
        // D-06/D-07: Stage 3 (content-only write) must NEVER flip is_visible — that only happens
        // after a deploy-confirm (SYNC-09, not wired to this stage — see Plan 90-06). A row must
        // never go visible before its body is durably in git and deployed.
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var (cut, localStore, prodStore, _, _, _) = RenderDirectPush(local);

        // Precondition: approved rows start hidden (KB ships dark).
        Assert.All(localStore.Rows, row => Assert.False(row.IsVisible));

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));

        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("awaiting Publish", cut.Markup);
            Assert.Empty(localStore.VisibilityKeyCalls);
            Assert.Empty(prodStore.VisibilityKeyCalls);
            Assert.All(localStore.Rows, row => Assert.False(row.IsVisible));
            Assert.All(prodStore.Rows, row => Assert.False(row.IsVisible));
        });
    }

    // ── M2: content-aware diff classification ─────────────────────────────────

    [Fact]
    public void M2_ComputeDiff_ClassifiesNewUpdatedUnchanged_Correctly()
    {
        // New: key absent from prod.
        // Updated: key present in prod but title differs (content changed).
        // Unchanged: key present in prod with identical content signature.
        var localNew = MakeApprovedRow(1, "vid-new");
        var localUpdated = MakeApprovedRow(2, "vid-updated") with { Title = "Updated Title" };
        var localUnchanged = MakeApprovedRow(3, "vid-unchanged");

        // Prod has "vid-updated" with a different title and "vid-unchanged" with the exact same content.
        var prodUpdated = MakeApprovedRow(2, "vid-updated") with { Title = "Old Title" };
        var prodUnchanged = MakeApprovedRow(3, "vid-unchanged");

        var local = new[] { localNew, localUpdated, localUnchanged };
        var prod = new[] { prodUpdated, prodUnchanged };
        var (cut, _, _, _, _, _) = RenderDirectPush(local, prod);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("New: 1", cut.Markup);
            Assert.Contains("Updated: 1", cut.Markup);
            Assert.Contains("Unchanged: 1", cut.Markup);
        });
    }

    [Fact]
    public void M2_ComputeDiff_DifferentKeyTypeSameValue_NotMisclassifiedUnchanged()
    {
        // Regression (Codex MED): keying the diff on the bare value let a prod PODCAST row and a
        // local YOUTUBE row that share a key value collide. With identical content signatures the
        // local row would be misclassified Unchanged and silently skipped (publish data loss).
        // The full (type, value) composite key must treat them as distinct → local row is New.
        var localYoutube = MakeApprovedRow(1, "shared-key");
        // Same content columns, but a podcast natural key (YoutubeVideoId null, RssGuid set) so the
        // content signature is identical while the key TYPE differs.
        var prodPodcast = localYoutube with { Id = 99, YoutubeVideoId = null, RssGuid = "shared-key" };

        var (cut, _, _, _, _, _) = RenderDirectPush(new[] { localYoutube }, new[] { prodPodcast });

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("New: 1", cut.Markup);
            Assert.Contains("Unchanged: 0", cut.Markup);
        });
    }

    [Fact]
    public void M2_BatchWrite_ExcludesUnchangedRows()
    {
        // Only New + Updated rows must be passed to the batch upsert; Unchanged is excluded.
        var localNew = MakeApprovedRow(1, "vid-new");
        var localUpdated = MakeApprovedRow(2, "vid-updated") with { Title = "Updated Title" };
        var localUnchanged = MakeApprovedRow(3, "vid-unchanged");

        var prodUpdated = MakeApprovedRow(2, "vid-updated") with { Title = "Old Title" };
        var prodUnchanged = MakeApprovedRow(3, "vid-unchanged");

        var local = new[] { localNew, localUpdated, localUnchanged };
        var prod = new[] { prodUpdated, prodUnchanged };
        var (cut, _, prodStore, _, _, _) = RenderDirectPush(local, prod);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));

        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(prodStore.BatchUpsertCalls);
            var batchRows = prodStore.BatchUpsertCalls[0];
            Assert.Equal(2, batchRows.Count);
            Assert.Contains(batchRows, r => r.YoutubeVideoId == "vid-new");
            Assert.Contains(batchRows, r => r.YoutubeVideoId == "vid-updated");
            Assert.DoesNotContain(batchRows, r => r.YoutubeVideoId == "vid-unchanged");
        });
    }

    [Fact]
    public void M2_AllUnchanged_Stage2And3CardsDoNotRender()
    {
        // When every approved local row matches prod by content signature, Stage 2 and 3 must
        // not render (no publish needed), and the "already up to date" copy must show.
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var prod = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var (cut, _, prodStore, _, _, _) = RenderDirectPush(local, prod);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Diff Preview", cut.Markup);
            Assert.Contains("already up to date", cut.Markup);
            // Stage 2 and 3 upload/write buttons must not be rendered.
            Assert.DoesNotContain("Upload Artifacts to Prod /data", cut.Markup);
            Assert.DoesNotContain("Write Approved Rows to Prod DB", cut.Markup);
            // No batch call was made.
            Assert.Empty(prodStore.BatchUpsertCalls);
        });
    }

    // ── H4: atomic batch commit ───────────────────────────────────────────────

    [Fact]
    public void H4_Success_BatchMethodCalled_AllRowsWritten_MarkerSet_NoStampOrVisibilityYet()
    {
        // H4/D-06/D-07: all publish rows pass through a single batch call; the local awaiting-confirm
        // marker is set, but stamp/visibility do NOT run at this stage (post-confirm only).
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var (cut, localStore, prodStore, _, _, _) = RenderDirectPush(local);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));

        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            // Exactly one batch call with both rows.
            Assert.Single(prodStore.BatchUpsertCalls);
            Assert.Equal(2, prodStore.BatchUpsertCalls[0].Count);

            // All rows show "Written" in the per-row table.
            Assert.Contains("Written", cut.Markup);

            // D-10: the local awaiting-confirm marker was set for both keys.
            Assert.Single(localStore.SetAwaitingConfirmCalls);
            Assert.Equal(2, localStore.SetAwaitingConfirmCalls[0].Keys.Count);

            // D-06/D-07: stamp and visibility do NOT run at this stage.
            Assert.Empty(prodStore.StampCalls);
            Assert.Empty(localStore.StampCalls);
            Assert.Empty(prodStore.VisibilityKeyCalls);
            Assert.Empty(localStore.VisibilityKeyCalls);
        });
    }

    // ── H4: atomic batch rollback ────────────────────────────────────────────

    [Fact]
    public void H4_BatchRollback_ZeroRowsCommitted_TitleSurfaced_NoSecretLeak_NoStamp()
    {
        // H4: when the batch throws ContentSiteIndexBatchUpsertException, ZERO rows are committed,
        // the failing row's title appears in the UI, nothing was written message shows, no secret
        // substrings reach the markup, and stamp/visibility were NOT called.
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2-bad") };
        var prodStore = new FakeContentSiteIndexStore { UpsertFailureMessage = SentinelSecret };
        prodStore.KeysToFailOnUpsert.Add("vid2-bad");
        var (cut, localStore, _, _, _, capturedLog) = RenderDirectPush(local, prodStoreOverride: prodStore);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));

        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            // Prod store committed ZERO rows (all-or-nothing rollback).
            Assert.Empty(prodStore.Rows);

            // Failing row title surfaced.
            Assert.Contains("Video 2", cut.Markup);

            // "nothing was written" copy present.
            Assert.Contains("NOTHING was written to production", cut.Markup);

            // SentinelSecret substrings must NOT reach the markup (D-07 / SC5 / T-qyc-02).
            foreach (var s in SentinelSubstrings)
            {
                Assert.DoesNotContain(s, cut.Markup);
            }

            // Exception must have been logged (so "see logs" guidance is true).
            Assert.True(capturedLog.Entries.Any(e => e.Level == LogLevel.Error && e.Exception is not null),
                "Expected at least one Error-level log entry with an exception from the batch rollback");

            // Stamp and visibility must NOT have run (PUB-01 / T-qyc-04).
            Assert.Empty(prodStore.StampCalls);
            Assert.Empty(localStore.StampCalls);
            Assert.Empty(prodStore.VisibilityKeyCalls);
            Assert.Empty(localStore.VisibilityKeyCalls);
        });
    }

    // ── Stage 5: Verify Deploy & Publish (SYNC-09/D-06) ────────────────────────

    // ── Awaiting-confirm resume bucket (D-10/Plan 90-06) ────────────────────────

    [Fact]
    public void DirectPush_AfterExpand_RowShowsInAwaitingConfirmBucket_NotVisible()
    {
        // Test (a): after Stage 3 (content-only write / "expand"), the pushed row must appear in the
        // awaiting-confirm resume bucket and stay hidden — it is durably tracked, never lost.
        var local = new[] { MakeApprovedRow(1, "vid1") with { BodySha256 = "hash-vid1" } };
        var (cut, localStore, _, _, _, _) = RenderDirectPush(local);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));

        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Awaiting Publish", cut.Markup);
            Assert.Contains("Video 1", cut.Markup);
            Assert.All(localStore.Rows, row => Assert.False(row.IsVisible));
        });
    }







    [Fact]
    public void DirectPush_FreshLoad_MarkerSetRow_SurfacesResumeAction_NotClassifiedUnchanged()
    {
        // Test (d): a row whose content already matches prod (would classify Unchanged on a diff,
        // per ClassifyDiff's content-signature comparison — 90-RESEARCH Pitfall 4) but still carries
        // the durable awaiting-confirm marker must surface via the resume bucket on a FRESH page
        // load, without the operator ever running Stage 1.
        var local = new[]
        {
            MakeApprovedRow(1, "vid1") with { BodySha256 = "hash-vid1", AwaitingConfirmUtc = DateTimeOffset.UtcNow },
        };
        // Prod already carries identical content — a diff would classify this row Unchanged.
        var prod = new[] { MakeApprovedRow(1, "vid1") with { BodySha256 = "hash-vid1" } };
        var (cut, _, _, _, _, _) = RenderDirectPush(local, prod);

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Resolving configuration", cut.Markup);
            Assert.Contains("Awaiting Publish", cut.Markup);
            Assert.Contains("Video 1", cut.Markup);
            Assert.Contains("Resume", cut.Markup);
        });
    }

    [Fact]
    public void DirectPush_ResumeAwaitingRow_ExportsThenMakesRowVisible()
    {
        var row = MakeApprovedRow(1, "resume-success") with { AwaitingConfirmUtc = DateTimeOffset.UtcNow };
        var orchestrator = new FakeContentKbOrchestrator();
        var exportObservedBeforePublish = false;
        var (cut, localStore, prodStore, _, _, _) = RenderDirectPush(
            new[] { row }, new[] { row }, orchestratorOverride: orchestrator);
        orchestrator.OnCopyArtifacts = () => exportObservedBeforePublish = orchestrator.ExportToFilePaths.Count == 1
            && prodStore.VisibilityKeyCalls.Count == 0
            && localStore.VisibilityKeyCalls.Count == 0;

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-warning").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(prodStore.VisibilityKeyCalls);
            Assert.Single(localStore.VisibilityKeyCalls);
            Assert.Single(localStore.ClearAwaitingConfirmCalls);
            Assert.True(exportObservedBeforePublish);
            Assert.DoesNotContain("Could not publish to the private KB root", cut.Markup);
        });
    }

    [Fact]
    public void DirectPush_ResumeExportFailure_ShowsErrorAndKeepsRowsHidden()
    {
        var row = MakeApprovedRow(1, "resume-export-failure") with { AwaitingConfirmUtc = DateTimeOffset.UtcNow };
        var failedExport = new FakeContentKbOrchestrator { ThrowOnCopy = new IOException("copy failed") };
        var (cut, localStore, prodStore, _, _, _) = RenderDirectPush(
            new[] { row }, new[] { row }, orchestratorOverride: failedExport);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-warning").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Could not publish to the private KB root", cut.Markup);
            Assert.Empty(prodStore.VisibilityKeyCalls);
            Assert.Empty(localStore.VisibilityKeyCalls);
            Assert.Empty(localStore.ClearAwaitingConfirmCalls);
        });
    }

    [Fact]
    public void DirectPush_ResumeFlagOn_ShowsErrorAndKeepsRowsHidden()
    {
        var row = MakeApprovedRow(1, "resume-flag-on") with { AwaitingConfirmUtc = DateTimeOffset.UtcNow };
        var (cut, localStore, prodStore, _, _, _) = RenderDirectPush(
            new[] { row }, new[] { row }, prodReaderOverride: new FakeDirectPushFlagReader { FlagValue = true });

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving configuration", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-warning").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Could not publish to the private KB root", cut.Markup);
            Assert.Empty(prodStore.VisibilityKeyCalls);
            Assert.Empty(localStore.VisibilityKeyCalls);
        });
    }

    // ── F-09: the visibility contract is stated once and cross-referenced ──

    [Fact]
    public void VisibilityContract_IsStatedOnceAndAnchored()
    {
        // F-09: the production visibility mechanic must be stated exactly once, in the
        // anchor-addressable TARGET: PRODUCTION danger banner, with every other site on the
        // page pointing at it via a fragment link. This pins the consolidation so a later
        // copy-editing pass cannot silently reintroduce the eleven-way repetition.
        //
        // A diff must be computed (Stage 2-5 render only once _diffReady) so at least one of
        // the eight downstream cross-reference sites is present in the rendered markup — the
        // Stage 4 card body paragraph renders unconditionally once the diff has New/Updated rows.
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _, _) = RenderDirectPush(local);

        ComputeDiffAndConfirm(cut);

        cut.WaitForAssertion(() =>
        {
            var banners = cut.FindAll("#direct-push-visibility-contract");
            Assert.Single(banners);
            Assert.Contains("alert-danger", banners[0].ClassList);

            var crossRefs = cut.FindAll("a[href='#direct-push-visibility-contract']");
            Assert.NotEmpty(crossRefs);
        });
    }
}
