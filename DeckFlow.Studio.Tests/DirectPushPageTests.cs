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
            FakeGitRepository? gitOverride = null,
            FakeContentKbOrchestrator? orchestratorOverride = null)
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
        Services.AddSingleton(new StudioConfig(isProdConfigured, isScpConfigured));
        Services.AddSingleton<IConfiguration>(configuration);
        Services.AddSingleton(new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot });
        // Why: the git durability stage (Stage 4) resolves IGitRepository + IContentKbOrchestrator
        // through the coordinator; register fakes so no real git process or file copy runs in bUnit.
        Services.AddSingleton<DeckFlow.Core.Integration.IGitRepository>(gitOverride ?? new FakeGitRepository());
        Services.AddSingleton<IContentKbOrchestrator>(orchestratorOverride ?? new FakeContentKbOrchestrator());
        // Why: the page now resolves its orchestration through DirectPushCoordinator (H1 split);
        // register it over the same fakes so the bUnit render wires up identically to production.
        Services.AddScoped<DirectPushCoordinator>();
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
    public void DirectPush_Stage4InvokedBeforeDbWrite_NoCommitOrPush()
    {
        // The git durability stage must early-return before the prod DB write succeeds — no commit,
        // no push (the disabled button alone is not sufficient; a stale render must not reach git).
        var git = new FakeGitRepository();
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _, _) = RenderDirectPush(local, gitOverride: git);

        // Compute diff + confirm, but never run SCP or the DB write — Stage 4 is still locked.
        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.Instance.InvokeCommitAndPushForTest());

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(git.CommitCalls);
            Assert.Empty(git.PushCalls);
        });
    }

    [Fact]
    public void DirectPush_Stage4_AfterDbWrite_CommitsBodiesAndPushes_NoRedeploy()
    {
        var git = new FakeGitRepository { CannedBranch = "main", CannedCommitSha = "cafe123" };
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _, _) = RenderDirectPush(local, gitOverride: git);

        // Drive Stage 1 (diff+confirm) → Stage 2 (SCP) → Stage 3 (DB write).
        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());
        cut.WaitForState(() => cut.Markup.Contains("written to production"));

        // Stage 4 button (btn-outline-primary) is now the LAST outline-primary button (Stage 1 is first).
        cut.WaitForAssertion(() =>
        {
            var outlineButtons = cut.FindAll("button.btn-outline-primary");
            Assert.False(outlineButtons[^1].HasAttribute("disabled"));
        });
        cut.InvokeAsync(() => cut.FindAll("button.btn-outline-primary")[^1].Click());

        cut.WaitForState(() => cut.Markup.Contains("pushed to"));
        cut.WaitForAssertion(() =>
        {
            // Committed exactly the pushed body path (never the seed) and pushed to origin/main.
            var commit = Assert.Single(git.CommitCalls);
            Assert.Equal(new[] { "content-kb/test-channel/vid1.md" }, commit.Paths);
            Assert.DoesNotContain(commit.Paths, p => p.Contains("index-seed.json", StringComparison.Ordinal));
            Assert.Contains("[skip render]", commit.Message);

            var push = Assert.Single(git.PushCalls);
            Assert.Equal("origin", push.Remote);
            Assert.Equal("main", push.Branch);

            Assert.Contains("origin/main", cut.Markup);
        });
    }

    [Fact]
    public void DirectPush_Stage4_AlreadyInSync_DoesNotClaimAPush()
    {
        // Review R2-3: when the bodies are already committed AND the branch is in sync with origin,
        // the coordinator returns AlreadyInSync WITHOUT pushing — the success alert must NOT claim a
        // push happened.
        var git = new FakeGitRepository
        {
            CannedBranch = "main",
            CannedWorkingChangeCount = 0,   // nothing to commit
            // CannedSubjectsAhead defaults empty = in sync → no push.
        };
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _, _) = RenderDirectPush(local, gitOverride: git);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());
        cut.WaitForState(() => cut.Markup.Contains("written to production"));

        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-outline-primary")[^1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-outline-primary")[^1].Click());

        cut.WaitForState(() => cut.Markup.Contains("in sync"));
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(git.PushCalls);                              // coordinator did not push
            Assert.Contains("nothing to push", cut.Markup);          // honest copy
            Assert.DoesNotContain("pushed to", cut.Markup);          // no false push claim
        });
    }

    [Fact]
    public void DirectPush_Stage4_PushedExistingCommits_ReportsCatchUpPush()
    {
        // UIAUDIT-03: the third Stage-4 outcome (PushedExistingCommits) — bodies are already committed
        // (nothing new to commit this run) but the branch has previously-unpushed durability commit(s)
        // ahead of origin. The coordinator performs a catch-up push; the alert must report that honestly
        // (not the Committed copy, not the AlreadyInSync no-op copy).
        var git = new FakeGitRepository
        {
            CannedBranch = "main",
            CannedWorkingChangeCount = 0,                                    // nothing new to commit this run
            // A prior run's OWN durability commit, still unpushed — must match the exact durability
            // subject shape (content: direct-push N bod(y|ies) to prod [skip render]) so the foreign-commit
            // guard accepts it and the coordinator does a catch-up push (outcome PushedExistingCommits).
            CannedSubjectsAhead = new() { "content: direct-push 1 body to prod [skip render]" },
        };
        var local = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _, _, _) = RenderDirectPush(local, gitOverride: git);

        // Drive Stage 1 (diff+confirm) → Stage 2 (SCP) → Stage 3 (DB write).
        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());
        cut.WaitForState(() => cut.Markup.Contains("written to production"));

        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-outline-primary")[^1].HasAttribute("disabled")));
        cut.InvokeAsync(() => cut.FindAll("button.btn-outline-primary")[^1].Click());

        cut.WaitForState(() => cut.Markup.Contains("previously-unpushed"));
        cut.WaitForAssertion(() =>
        {
            // Nothing new was committed this run (distinguishes from the Committed variant)...
            Assert.Empty(git.CommitCalls);
            // ...but a catch-up push happened to origin/main...
            var push = Assert.Single(git.PushCalls);
            Assert.Equal("origin", push.Remote);
            Assert.Equal("main", push.Branch);
            // ...reported with the catch-up copy, NOT the no-op "nothing to push" variant.
            Assert.Contains("previously-unpushed", cut.Markup);
            Assert.DoesNotContain("nothing to push", cut.Markup);
        });
    }

    [Fact]
    public void DirectPush_Success_StampsLocalAndProd_WithSameInstant()
    {
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var (cut, localStore, prodStore, _, _, _) = RenderDirectPush(local);

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));

        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(localStore.StampCalls);
            Assert.Single(prodStore.StampCalls);

            var localStamp = localStore.StampCalls[0];
            var prodStamp = prodStore.StampCalls[0];

            Assert.Equal(localStamp.PushedUtc, prodStamp.PushedUtc);
            Assert.Equal(2, localStamp.Keys.Count);
            Assert.Equal(2, prodStamp.Keys.Count);
            Assert.All(localStore.Rows, row => Assert.Equal(localStamp.PushedUtc, row.PushedToProdUtc));
            Assert.All(prodStore.Rows, row => Assert.Equal(prodStamp.PushedUtc, row.PushedToProdUtc));
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
        var errorEntries = capturedLog.Entries
            .Where(e => e.Level == LogLevel.Error && e.Exception is not null)
            .ToList();

        Assert.True(errorEntries.Count >= 1,
            "Expected at least one Error-level log entry with an exception from the diff failure");

        // Sentinel substrings must NOT appear in the rendered markup (secret-leak guard).
        foreach (var s in SentinelSubstrings)
        {
            Assert.DoesNotContain(s, cut.Markup);
        }
    }

    [Fact]
    public void DirectPush_Success_PublishesRowsVisible_LocalAndProd()
    {
        var local = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var (cut, localStore, prodStore, _, _, _) = RenderDirectPush(local);

        // Precondition: approved rows start hidden (KB ships dark) — Studio would derive Pushed-hidden
        // without the publish-visible step.
        Assert.All(localStore.Rows, row => Assert.False(row.IsVisible));

        ComputeDiffAndConfirm(cut);
        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[0].Click());
        cut.WaitForState(() => cut.Markup.Contains("uploaded to production /data"));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button.btn-danger")[1].HasAttribute("disabled")));

        cut.InvokeAsync(() => cut.FindAll("button.btn-danger")[1].Click());

        cut.WaitForAssertion(() =>
        {
            // DirectPush publishes visible: both stores get one keyed SetVisibility(true) and every
            // pushed row is now visible, so the Studio badge derives Published just like prod /Admin.
            Assert.Single(localStore.VisibilityKeyCalls);
            Assert.Single(prodStore.VisibilityKeyCalls);
            Assert.True(localStore.VisibilityKeyCalls[0].Visible);
            Assert.True(prodStore.VisibilityKeyCalls[0].Visible);
            Assert.Equal(2, localStore.VisibilityKeyCalls[0].Keys.Count);
            Assert.All(localStore.Rows, row => Assert.True(row.IsVisible));
            Assert.All(prodStore.Rows, row => Assert.True(row.IsVisible));
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
    public void H4_Success_BatchMethodCalled_AllRowsWritten_StampAndVisibilityRan()
    {
        // H4: all publish rows pass through a single batch call; stamp + visibility run on success.
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

            // Stamp and visibility ran on both stores.
            Assert.Single(prodStore.StampCalls);
            Assert.Single(localStore.StampCalls);
            Assert.Single(prodStore.VisibilityKeyCalls);
            Assert.Single(localStore.VisibilityKeyCalls);
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
            var errorEntries = capturedLog.Entries
                .Where(e => e.Level == LogLevel.Error && e.Exception is not null)
                .ToList();
            Assert.True(errorEntries.Count >= 1,
                "Expected at least one Error-level log entry with an exception from the batch rollback");

            // Stamp and visibility must NOT have run (PUB-01 / T-qyc-04).
            Assert.Empty(prodStore.StampCalls);
            Assert.Empty(localStore.StampCalls);
            Assert.Empty(prodStore.VisibilityKeyCalls);
            Assert.Empty(localStore.VisibilityKeyCalls);
        });
    }
}
