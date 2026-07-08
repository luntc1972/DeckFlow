using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Fast unit tests for <see cref="DirectPushCoordinator"/> — the DirectPush orchestration extracted
/// from the page code-behind (H1 split). These exercise the content-diff classification and the
/// prod read/write sequences directly with fakes, without the bUnit render the logic previously
/// required.
/// </summary>
public sealed class DirectPushCoordinatorTests
{
    // Fixed timestamps so content signatures are deterministic across rows (no UtcNow drift).
    private static readonly DateTimeOffset IndexedAt = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PublishedAt = new(2026, 5, 30, 8, 0, 0, TimeSpan.Zero);

    private static ContentSiteIndexRow Youtube(long id, string videoId, string title = "Title")
        => new()
        {
            Id = id,
            Source = "test-channel",
            Title = title,
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            PublishedUtc = PublishedAt,
            IndexedUtc = IndexedAt,
            ApprovalStatus = "approved",
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
        };

    private static ContentSiteIndexRow Podcast(long id, string guid, string title = "Title")
        => new()
        {
            Id = id,
            Source = "test-podcast",
            Title = title,
            VideoUrl = $"https://pod.example/{guid}",
            ArtifactPath = $"content-kb/test-podcast/{guid}.md",
            PublishedUtc = PublishedAt,
            IndexedUtc = IndexedAt,
            ApprovalStatus = "approved",
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            RssGuid = guid,
        };

    private static DirectPushCoordinator Build(
        FakeContentSiteIndexStore local,
        FakeContentSiteIndexStore prod,
        FakeSshArtifactUploader? uploader = null,
        string artifactRoot = "/data/content-kb",
        FakeGitRepository? git = null,
        FakeContentKbOrchestrator? orchestrator = null,
        IProdContentReader? prodReader = null,
        IDeployedBodyConfirmer? confirmer = null)
        => new(
            local,
            uploader ?? new FakeSshArtifactUploader(),
            new FakeProdStoreFactory(prod),
            new ConfigurationBuilder().Build(),
            new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot },
            git ?? new FakeGitRepository(),
            orchestrator ?? new FakeContentKbOrchestrator(),
            // D-05: flag OFF by default — [skip render] behavior stays byte-identical unless a test
            // explicitly turns the flag on (see TestDoubles/FakeDirectPushFlagReader.cs).
            prodReader ?? new FakeDirectPushFlagReader(),
            // SYNC-09/D-09 REVISED: confirmed by default so tests exercising WriteContentAsync/
            // ConfirmAndPublishAsync/CommitAndPushBodiesAsync directly are unaffected; the
            // VerifyAndPublishAsync tests override this explicitly.
            confirmer ?? new FakeDeployedBodyConfirmer());

    // ── ClassifyDiff (pure) ─────────────────────────────────────────────────

    [Fact]
    public void ClassifyDiff_RowNotInProd_IsNew_AndInPublishSet()
    {
        var local = new[] { Youtube(1, "aaa") };
        var prod = Array.Empty<ContentSiteIndexRow>();

        var diff = DirectPushCoordinator.ClassifyDiff(local, prod);

        Assert.Equal(1, diff.NewCount);
        Assert.Equal(0, diff.UpdatedCount);
        Assert.Equal(0, diff.UnchangedCount);
        Assert.Single(diff.PublishRows);
        Assert.True(diff.DiffRows[0].IsNew);
    }

    [Fact]
    public void ClassifyDiff_SameKeyDifferentContent_IsUpdated_AndInPublishSet()
    {
        var local = new[] { Youtube(1, "aaa", title: "New Title") };
        var prod = new[] { Youtube(99, "aaa", title: "Old Title") };

        var diff = DirectPushCoordinator.ClassifyDiff(local, prod);

        Assert.Equal(0, diff.NewCount);
        Assert.Equal(1, diff.UpdatedCount);
        Assert.Equal(0, diff.UnchangedCount);
        Assert.Single(diff.PublishRows);
        Assert.False(diff.DiffRows[0].IsNew);
    }

    [Fact]
    public void ClassifyDiff_SameKeyIdenticalContent_IsUnchanged_AndExcludedFromPublish()
    {
        var local = new[] { Youtube(1, "aaa", title: "Same") };
        var prod = new[] { Youtube(99, "aaa", title: "Same") };

        var diff = DirectPushCoordinator.ClassifyDiff(local, prod);

        Assert.Equal(0, diff.NewCount);
        Assert.Equal(0, diff.UpdatedCount);
        Assert.Equal(1, diff.UnchangedCount);
        Assert.Empty(diff.PublishRows);
        Assert.Empty(diff.DiffRows);
    }

    [Fact]
    public void ClassifyDiff_YoutubeAndPodcastShareKeyValue_DoNotCollide()
    {
        // Why: the composite-key data-loss regression (Codex MED). A local youtube row and a prod
        // podcast row sharing the same natural-key VALUE must NOT match — otherwise the local row
        // could be misclassified Unchanged and silently skip its publish.
        var local = new[] { Youtube(1, "shared") };
        var prod = new[] { Podcast(99, "shared") };

        var diff = DirectPushCoordinator.ClassifyDiff(local, prod);

        Assert.Equal(1, diff.NewCount);
        Assert.Equal(0, diff.UnchangedCount);
        Assert.Single(diff.PublishRows);
        // The diff now carries the stored vocabulary discriminator (D-07), not the short "youtube".
        Assert.Equal(ContentSourceType.Youtube, diff.DiffRows[0].KeyType);
    }

    [Fact]
    public void ClassifyDiff_RowWithNoNaturalKey_IsSkipped_AndWarns_WhenLoggerSupplied()
    {
        // D-08 (Codex MED-3): a local row with neither a YouTube id nor an RSS guid is skipped, and a
        // structured warning naming the row is logged when a logger is supplied.
        var orphan = Youtube(1, "keyed") with { YoutubeVideoId = null, RssGuid = null, Title = "Orphan row" };
        var logger = new RecordingTestLogger();

        var diff = DirectPushCoordinator.ClassifyDiff(new[] { orphan }, Array.Empty<ContentSiteIndexRow>(), logger);

        Assert.Equal(0, diff.NewCount);
        Assert.Empty(diff.PublishRows);
        var warning = Assert.Single(logger.Warnings);
        Assert.Contains("Orphan row", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void ProdStoreFactory_Create_BuildsSchemaEnsureDisabledStore_NoDdlOnDeadConnection()
    {
        // D-10 wiring proof: the factory builds a schema-ensure-DISABLED store, so EnsureSchemaAsync
        // early-returns without ever opening a connection. Against an unreachable-but-well-formed prod
        // connection string, that completes without throwing; a regression to schema-ensure-ON would
        // attempt the dead connection and throw. (The zero-DDL invariant itself is locked by 88-01's
        // recording-connection test.)
        var store = new ProdStoreFactory().Create(
            "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1");

        // Should NOT throw — the switch is off, so no connection is opened.
        var ex = Record.Exception(() => store.EnsureSchemaAsync().GetAwaiter().GetResult());
        Assert.Null(ex);
    }

    [Fact]
    public void ClassifyDiff_MixedSet_CountsEachBucket()
    {
        var local = new[]
        {
            Youtube(1, "new1"),
            Youtube(2, "upd", title: "Local"),
            Youtube(3, "same", title: "Same"),
        };
        var prod = new[]
        {
            Youtube(20, "upd", title: "Prod"),
            Youtube(30, "same", title: "Same"),
        };

        var diff = DirectPushCoordinator.ClassifyDiff(local, prod);

        Assert.Equal(1, diff.NewCount);
        Assert.Equal(1, diff.UpdatedCount);
        Assert.Equal(1, diff.UnchangedCount);
        Assert.Equal(2, diff.PublishRows.Count);
    }

    // ── LoadInitDataAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task LoadInitDataAsync_ReturnsApprovedCount_AndDataRootParentOfArtifactRoot()
    {
        var local = new FakeContentSiteIndexStore();
        local.Rows.Add(Youtube(1, "aaa"));
        local.Rows.Add(Youtube(2, "bbb"));
        local.Rows.Add(Youtube(3, "ccc") with { ApprovalStatus = "pending" });
        var coordinator = Build(local, new FakeContentSiteIndexStore(), artifactRoot: "/data/content-kb");

        var init = await coordinator.LoadInitDataAsync(CancellationToken.None);

        Assert.Equal(2, init.ApprovedCount);
        Assert.Equal(Path.GetDirectoryName("/data/content-kb"), init.DataRoot);
    }

    // ── ComputeDiffAsync (read + classify) ──────────────────────────────────

    [Fact]
    public async Task ComputeDiffAsync_ReadsApprovedLocalAndAllProd_AndClassifies()
    {
        var local = new FakeContentSiteIndexStore();
        local.Rows.Add(Youtube(1, "new1"));
        local.Rows.Add(Youtube(2, "same", title: "Same"));
        local.Rows.Add(Youtube(3, "skip") with { ApprovalStatus = "pending" });
        var prod = new FakeContentSiteIndexStore();
        prod.Rows.Add(Youtube(20, "same", title: "Same"));
        var coordinator = Build(local, prod);

        var diff = await coordinator.ComputeDiffAsync(CancellationToken.None);

        Assert.Equal(1, diff.NewCount);
        Assert.Equal(1, diff.UnchangedCount);
        Assert.Equal(0, prod.EnsureSchemaCallCount); // H3: diff issues no DDL on prod
    }

    // ── WriteContentAsync (content-only batch + awaiting-confirm marker, D-06/D-07) ──────────

    [Fact]
    public async Task WriteContentAsync_HappyPath_UsesContentColumnsOnlyBatch_SetsMarker_NoStampOrVisibility()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow>
        {
            Youtube(1, "aaa") with { ApprovalStatus = "approved" },
            Youtube(2, "bbb") with { ApprovalStatus = "approved" },
        };
        // Seed prod so the local marker-set pass has rows to match.
        prod.Rows.Add(Youtube(1, "aaa"));
        prod.Rows.Add(Youtube(2, "bbb"));
        var coordinator = Build(local, prod);

        await coordinator.WriteContentAsync(publish, CancellationToken.None);

        // SC3 / D-08: only the content-columns-only BATCH upsert ran on prod — never a full-row upsert.
        Assert.Equal(new[] { "UpsertContentColumnsOnlyBatchAsync" }, prod.UpsertMethodCalls);
        Assert.Single(prod.BatchUpsertCalls);
        // D-03: approval_status mirrored via the content-only upsert (P88 approval mirror preserved
        // by the split — this method still calls UpsertContentColumnsOnlyBatchAsync).
        Assert.All(prod.BatchUpsertCalls[0], r => Assert.Equal("approved", r.ApprovalStatus));
        // D-06/D-07: neither store is stamped or made visible by the content-only write.
        Assert.Empty(prod.StampCalls);
        Assert.Empty(prod.VisibilityKeyCalls);
        Assert.Empty(local.StampCalls);
        Assert.Empty(local.VisibilityKeyCalls);
        // D-10: the local awaiting-confirm marker was set for the pushed keys.
        Assert.Single(local.SetAwaitingConfirmCalls);
        Assert.Equal(2, local.SetAwaitingConfirmCalls[0].Keys.Count);
    }

    [Fact]
    public async Task WriteContentAsync_BatchRollback_Throws_AndDoesNotSetMarker()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa"), Youtube(2, "boom") };
        prod.KeysToFailOnUpsert.Add("boom");
        var coordinator = Build(local, prod);

        await Assert.ThrowsAsync<ContentSiteIndexBatchUpsertException>(
            () => coordinator.WriteContentAsync(publish, CancellationToken.None));

        // PUB-01: nothing was stamped/made-visible, and the marker was never set — the whole batch
        // rolled back before the marker-set call is reached.
        Assert.Empty(prod.StampCalls);
        Assert.Empty(prod.VisibilityKeyCalls);
        Assert.Empty(local.StampCalls);
        Assert.Empty(local.VisibilityKeyCalls);
        Assert.Empty(local.SetAwaitingConfirmCalls);
    }

    // ── ConfirmAndPublishAsync (post-confirm stamp/visibility + marker clear, D-06/D-07/D-10) ──

    [Fact]
    public async Task ConfirmAndPublishAsync_StampsAndFlipsVisible_ProdAndLocal_ClearsMarker()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa"), Youtube(2, "bbb") };
        prod.Rows.Add(Youtube(1, "aaa"));
        prod.Rows.Add(Youtube(2, "bbb"));
        var coordinator = Build(local, prod);

        await coordinator.ConfirmAndPublishAsync(publish, CancellationToken.None);

        // PUB-01/HIGH-3: prod stamped + made visible, then local mirrors the same (order preserved
        // from the pre-split WritePublishAsync across the Task 1 split).
        Assert.Single(prod.StampCalls);
        Assert.Single(prod.VisibilityKeyCalls);
        Assert.Single(local.StampCalls);
        Assert.Single(local.VisibilityKeyCalls);
        // D-10: the local awaiting-confirm marker is cleared once the row is fully published.
        Assert.Single(local.ClearAwaitingConfirmCalls);
        Assert.Equal(2, local.ClearAwaitingConfirmCalls[0].Count);
    }

    // ── GetAwaitingConfirmRowsAsync (D-10 resume support, Plan 90-06) ────────

    [Fact]
    public async Task GetAwaitingConfirmRowsAsync_ReturnsOnlyApprovedRowsWithMarkerSet()
    {
        var local = new FakeContentSiteIndexStore();
        local.Rows.Add(Youtube(1, "marked") with { AwaitingConfirmUtc = DateTimeOffset.UtcNow, ApprovalStatus = "approved" });
        local.Rows.Add(Youtube(2, "unmarked") with { AwaitingConfirmUtc = null, ApprovalStatus = "approved" });
        local.Rows.Add(Youtube(3, "marked-not-approved") with { AwaitingConfirmUtc = DateTimeOffset.UtcNow, ApprovalStatus = "pending" });
        var coordinator = Build(local, new FakeContentSiteIndexStore());

        var result = await coordinator.GetAwaitingConfirmRowsAsync(CancellationToken.None);

        var row = Assert.Single(result);
        Assert.Equal("marked", row.YoutubeVideoId);
    }

    [Fact]
    public async Task GetAwaitingConfirmRowsAsync_NoMarkedRows_ReturnsEmpty()
    {
        var local = new FakeContentSiteIndexStore();
        local.Rows.Add(Youtube(1, "aaa"));
        var coordinator = Build(local, new FakeContentSiteIndexStore());

        var result = await coordinator.GetAwaitingConfirmRowsAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    // ── UploadArtifactsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UploadArtifactsAsync_BuildsRequestsFromPublishRows_AndDataRoot()
    {
        var uploader = new FakeSshArtifactUploader();
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), uploader);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var results = await coordinator.UploadArtifactsAsync(
            publish, "/data", progress: null!, CancellationToken.None);

        Assert.Single(results);
        Assert.True(results[0].Success);
        var req = Assert.Single(uploader.UploadedFiles);
        Assert.Equal("content-kb/test-channel/aaa.md", req.RemoteRelativePath);
        Assert.Equal(Path.GetFullPath(Path.Combine("/data", "content-kb/test-channel/aaa.md")), req.LocalPath);
    }

    // ── CommitAndPushBodiesAsync (git durability stage) ─────────────────────

    [Fact]
    public async Task CommitAndPushBodiesAsync_CommitsOnlyPushedBodies_ThenPushesCurrentBranch()
    {
        var git = new FakeGitRepository { CannedRepoRoot = "/repo", CannedBranch = "main", CannedCommitSha = "deadbee" };
        var orchestrator = new FakeContentKbOrchestrator();
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git, orchestrator: orchestrator);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa"), Youtube(2, "bbb") };

        var result = await coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None);

        // Only the pushed bodies were copied into the repo (never the full approved set).
        var copied = Assert.Single(orchestrator.CopyArtifactsCalls);
        Assert.Equal(
            new[] { "content-kb/test-channel/aaa.md", "content-kb/test-channel/bbb.md" },
            copied);

        // D-08/SYNC-08: the seed is re-exported via the SAME shared factory Publish uses, into the
        // repo tree, and staged alongside the copied bodies.
        var exportedPath = Assert.Single(orchestrator.ExportToFilePaths);
        Assert.Equal(Path.GetFullPath(Path.Combine("/repo", "content-kb/seed/index-seed.json")), exportedPath);
        var commit = Assert.Single(git.CommitCalls);
        Assert.Contains("content-kb/seed/index-seed.json", commit.Paths);
        Assert.Equal(
            new[] { "content-kb/seed/index-seed.json", "content-kb/test-channel/aaa.md", "content-kb/test-channel/bbb.md" },
            commit.Paths);

        // D-05: flag OFF by default (no test override) — commit message carries the Render
        // deploy-skip phrase byte-identical to before this plan (NOT [skip ci]).
        Assert.Contains("[skip render]", commit.Message);
        Assert.DoesNotContain("[skip ci]", commit.Message);

        // Pushed the current branch to origin.
        var push = Assert.Single(git.PushCalls);
        Assert.Equal("origin", push.Remote);
        Assert.Equal("main", push.Branch);

        Assert.Equal("deadbee", result.Sha);
        Assert.Equal("main", result.Branch);
        Assert.Equal(2, result.BodyCount);
        Assert.Equal(DirectPushGitOutcome.Committed, result.Outcome);

        // The APPROVED-ONLY full-set copy path (CopyApprovedArtifactsToRepoAsync — Publish's own
        // artifact-copy step, distinct from the seed export) is still NEVER invoked here: DirectPush
        // copies only the pushed bodies via CopyArtifactsToRepoAsync, not the full approved set.
        Assert.Equal(0, orchestrator.CopyApprovedCallCount);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_FlagOn_DropsSkipRenderPhrase()
    {
        // D-09: sync.directpush-gitbody ON drops [skip render] so the push triggers a real redeploy —
        // required for SYNC-09's hash-gated deploy-confirm step to ever succeed.
        var git = new FakeGitRepository { CannedBranch = "main" };
        var flagReader = new FakeDirectPushFlagReader { FlagValue = true };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git, prodReader: flagReader);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var result = await coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None);

        var commit = Assert.Single(git.CommitCalls);
        Assert.DoesNotContain("[skip render]", commit.Message);
        Assert.Equal(DirectPushGitOutcome.Committed, result.Outcome);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_FlagOffThenOn_BothCommitSubjects_RecognizedAsOwnDurabilityCommit()
    {
        // D-09 correctness: a PRIOR run's flag-ON commit (no trailing phrase) must still be
        // recognized as OUR OWN durability commit on a later ahead-of-origin check, not misclassified
        // foreign — otherwise every flag-ON push would permanently block on the NEXT DirectPush run.
        var git = new FakeGitRepository
        {
            CannedBranch = "main",
            CannedWorkingChangeCount = 0,
            CannedSubjectsAhead = { "content: direct-push 1 body to prod" },
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var result = await coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None);

        Assert.Equal(DirectPushGitOutcome.PushedExistingCommits, result.Outcome);
        Assert.Empty(git.CommitCalls);
        Assert.Single(git.PushCalls);   // recognized as own commit → catch-up push, not a foreign-commit refusal
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_SeedExportFails_ThrowsBeforeCopyOrCommitOrPush()
    {
        // D-08: a seed-export failure must surface, never fall through to a silent bodies-only
        // commit.
        var git = new FakeGitRepository { CannedBranch = "main" };
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedExportResult = new ContentIndexExportResult
            {
                Success = false,
                Message = "disk full",
                RowCount = 0,
                Rows = Array.Empty<ContentIndexExportRow>(),
            },
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git, orchestrator: orchestrator);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None));

        Assert.Contains("disk full", ex.Message, StringComparison.Ordinal);
        Assert.Empty(orchestrator.CopyArtifactsCalls);
        Assert.Empty(git.CommitCalls);
        Assert.Empty(git.PushCalls);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_BodiesIdenticalAndInSync_NoCommitNoPush()
    {
        // Review F4: body byte-identical to HEAD AND the branch is in sync with origin (empty
        // ahead-list) → truly nothing to do. No commit AND no push, so a transient push failure can
        // never raise a false "run git push by hand" alarm on an already-durable state.
        var git = new FakeGitRepository { CannedBranch = "main", CannedWorkingChangeCount = 0 };
        // CannedSubjectsAhead defaults to empty = branch in sync with origin.
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var result = await coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None);

        Assert.Equal(DirectPushGitOutcome.AlreadyInSync, result.Outcome);
        Assert.Null(result.Sha);
        Assert.Empty(git.CommitCalls);
        Assert.Empty(git.PushCalls);   // in sync → no push at all
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_SeedOnlyChange_CommitsAndPushesSeed()
    {
        // Codex MED regression: a metadata-only edit (retitle/retag) changes the re-exported
        // index-seed.json but leaves every .md body byte-identical (CannedWorkingChangeCount = 0).
        // The row must NOT return AlreadyInSync with the modified seed left uncommitted (D-08) — the
        // seed-only change is detected and committed (staged with the bodies), then pushed.
        var git = new FakeGitRepository
        {
            CannedBranch = "main",
            CannedWorkingChangeCount = 0,     // no body changed
            CannedSeedWorkingChangeCount = 1, // the seed did
            CannedCommitSha = "seedsha",
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var result = await coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None);

        Assert.Equal(DirectPushGitOutcome.Committed, result.Outcome);
        Assert.Equal("seedsha", result.Sha);
        Assert.Equal(0, result.BodyCount);                      // body-only count stays 0
        var commit = Assert.Single(git.CommitCalls);
        Assert.Contains("content-kb/seed/index-seed.json", commit.Paths); // seed was staged
        Assert.Contains("0 bodies", commit.Message);            // durability subject shape preserved (\d+ bodies)
        Assert.Single(git.PushCalls);                           // and pushed
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_NoCommitButOwnDurabilityCommitUnpushed_StillPushes()
    {
        // Review F4 (catch-up preserved): a PRIOR run committed a durability commit but its push
        // failed. Now the body is unchanged (no new commit) but the branch is ahead of origin by OUR
        // OWN [skip render] commit → the catch-up push still runs to complete durability.
        var git = new FakeGitRepository
        {
            CannedBranch = "main",
            CannedWorkingChangeCount = 0,
            CannedSubjectsAhead = { "content: direct-push 1 body to prod [skip render]" },
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var result = await coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None);

        Assert.Equal(DirectPushGitOutcome.PushedExistingCommits, result.Outcome);
        Assert.Empty(git.CommitCalls);
        Assert.Single(git.PushCalls);   // our own unpushed durability commit → catch-up push
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_ForeignCommitAhead_RefusesBeforeCopyOrCommitOrPush()
    {
        // Review F2: a commit ahead of origin that this stage did NOT author (not a [skip render]
        // durability commit) → refuse BEFORE copying/committing/pushing, so Stage 4 never silently
        // publishes unreviewed commits.
        var git = new FakeGitRepository
        {
            CannedBranch = "main",
            CannedSubjectsAhead = { "refactor: unrelated local work" },
        };
        var orchestrator = new FakeContentKbOrchestrator();
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git, orchestrator: orchestrator);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var ex = await Assert.ThrowsAsync<DirectPushUnreviewedCommitsException>(
            () => coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None));

        Assert.Equal(1, ex.ForeignCommitCount);
        Assert.Equal("main", ex.Branch);
        Assert.Empty(orchestrator.CopyArtifactsCalls);   // guard runs before any copy
        Assert.Empty(git.CommitCalls);
        Assert.Empty(git.PushCalls);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_MixedOwnAndForeignAhead_RefusesOnForeign()
    {
        // One of our durability commits plus one foreign commit ahead → still refuse (foreign count 1).
        var git = new FakeGitRepository
        {
            CannedBranch = "main",
            CannedSubjectsAhead =
            {
                "content: direct-push 2 bodies to prod [skip render]",
                "fix: some unrelated commit",
            },
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var ex = await Assert.ThrowsAsync<DirectPushUnreviewedCommitsException>(
            () => coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None));

        Assert.Equal(1, ex.ForeignCommitCount);
        Assert.Empty(git.PushCalls);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_AheadStateUnknown_FailsClosed_NoCommitNoPush()
    {
        // Review R2-1: remote-tracking ref missing (GetSubjectsAheadOfRemoteAsync throws) → the stage
        // cannot prove a push would publish only its own commits, so it FAILS CLOSED before copying,
        // committing, or pushing. The operator fetches, then retries.
        var git = new FakeGitRepository
        {
            CannedBranch = "main",
            ThrowOnSubjectsAhead = new GitCommandException("bad revision origin/main"),
        };
        var orchestrator = new FakeContentKbOrchestrator();
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git, orchestrator: orchestrator);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var ex = await Assert.ThrowsAsync<DirectPushPushBlockedException>(
            () => coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None));

        Assert.Equal("main", ex.Branch);
        Assert.Empty(orchestrator.CopyArtifactsCalls);   // blocked before any copy
        Assert.Empty(git.CommitCalls);
        Assert.Empty(git.PushCalls);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_SubjectSpoofNotExactShape_TreatedAsForeign()
    {
        // Review R2-2: a subject that merely starts with the prefix and contains the token but is NOT
        // the exact "{n} body|bodies to prod" shape (e.g. "content: direct-push notes [skip render]")
        // must be classified FOREIGN, not an own durability commit.
        var git = new FakeGitRepository
        {
            CannedBranch = "main",
            CannedSubjectsAhead = { "content: direct-push notes [skip render]" },
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var ex = await Assert.ThrowsAsync<DirectPushUnreviewedCommitsException>(
            () => coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None));

        Assert.Equal(1, ex.ForeignCommitCount);
        Assert.Empty(git.PushCalls);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_PushCancelled_PropagatesCancellation_NotPushException()
    {
        // Review F3: a genuine cancellation from PushAsync must surface AS OperationCanceledException,
        // not be wrapped into DirectPushPushException (which would report a hard push failure).
        var git = new FakeGitRepository
        {
            CannedBranch = "main",
            ThrowOnPush = new OperationCanceledException(),
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None));
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_PushFailsAfterCommit_ThrowsWithSha()
    {
        // Codex MED: commit landed, push failed → surface the SHA so the operator can push by hand.
        var git = new FakeGitRepository
        {
            CannedBranch = "main",
            CannedCommitSha = "committed1",
            ThrowOnPush = new GitCommandException("network unreachable"),
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var ex = await Assert.ThrowsAsync<DirectPushPushException>(
            () => coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None));

        Assert.Equal("committed1", ex.Sha);
        Assert.Equal("main", ex.Branch);
        Assert.Single(git.CommitCalls);   // the commit DID land
        Assert.Single(git.PushCalls);     // the push was attempted (and threw)
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_DetachedHead_ThrowsBeforeAnyCopyOrCommit()
    {
        // Codex LOW: rev-parse --abbrev-ref returns "HEAD" when detached — must fail fast before
        // copying/committing, never push to a bogus refs/heads/HEAD.
        var git = new FakeGitRepository { CannedBranch = "HEAD" };
        var orchestrator = new FakeContentKbOrchestrator();
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git, orchestrator: orchestrator);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None));

        Assert.Empty(orchestrator.CopyArtifactsCalls);
        Assert.Empty(git.CommitCalls);
        Assert.Empty(git.PushCalls);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_DeduplicatesSharedArtifactPaths()
    {
        var git = new FakeGitRepository();
        var orchestrator = new FakeContentKbOrchestrator();
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git, orchestrator: orchestrator);
        // Two rows pointing at the same artifact path — must copy/stage it once.
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "dup"), Youtube(2, "dup") };

        await coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None);

        var copied = Assert.Single(orchestrator.CopyArtifactsCalls);
        Assert.Equal(new[] { "content-kb/test-channel/dup.md" }, copied);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_CommitThrows_DoesNotPush()
    {
        var git = new FakeGitRepository { ThrowOnCommit = new GitCommandException("nothing to commit") };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        await Assert.ThrowsAsync<GitCommandException>(
            () => coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None));

        // Commit failure must short-circuit before the push.
        Assert.Empty(git.PushCalls);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_CommittedCount_ReflectsActuallyChangedFiles_NotCopied()
    {
        // Review R3-2: two bodies are copied but only ONE actually differs from HEAD (the other is a
        // byte-identical Updated row) → the commit message and BodyCount report 1, not 2.
        var git = new FakeGitRepository { CannedBranch = "main", CannedWorkingChangeCount = 1 };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa"), Youtube(2, "bbb") };

        var result = await coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None);

        Assert.Equal(DirectPushGitOutcome.Committed, result.Outcome);
        Assert.Equal(1, result.BodyCount);                 // accurate committed count, not the copied 2
        var commit = Assert.Single(git.CommitCalls);
        Assert.Equal(3, commit.Paths.Count);               // 2 bodies + the seed staged; git commits only the changed 1 body
        Assert.Contains("1 body", commit.Message);
        Assert.DoesNotContain("2 bodies", commit.Message);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_AllArtifactPathsBlank_Throws_NoFalseDurability()
    {
        // Review R3-3: rows exist but every ArtifactPath is blank/whitespace → nothing to back up.
        // Refuse rather than fall through to a false "in sync / git-durable" success on zero bodies.
        var git = new FakeGitRepository { CannedBranch = "main" };
        var orchestrator = new FakeContentKbOrchestrator();
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git, orchestrator: orchestrator);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") with { ArtifactPath = "   " } };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None));

        Assert.Empty(orchestrator.CopyArtifactsCalls);
        Assert.Empty(git.CommitCalls);
        Assert.Empty(git.PushCalls);
    }

    [Fact]
    public async Task CommitAndPushBodiesAsync_BlockedReason_MentionsInitialPushForNeverPushedBranch()
    {
        // Review R3-1: the fail-closed reason must give a WORKING recovery for a never-pushed branch
        // ('git push -u'), not only 'git fetch' (which cannot create a remote branch that never existed).
        var git = new FakeGitRepository
        {
            CannedBranch = "feature-x",
            ThrowOnSubjectsAhead = new GitCommandException("unknown revision origin/feature-x"),
        };
        var coordinator = Build(new FakeContentSiteIndexStore(), new FakeContentSiteIndexStore(), git: git);
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") };

        var ex = await Assert.ThrowsAsync<DirectPushPushBlockedException>(
            () => coordinator.CommitAndPushBodiesAsync(publish, "/data", CancellationToken.None));

        Assert.Contains("git push -u origin feature-x", ex.Reason);
    }

    // ── VerifyAndPublishAsync (deploy-confirm gate, SYNC-09/D-09 REVISED) ────
    // FakeDeployedBodyConfirmer lives in TestDoubles/FakeDeployedBodyConfirmer.cs (shared with
    // DirectPushPageTests, mirroring FakeDirectPushFlagReader's placement).

    [Fact]
    public async Task VerifyAndPublishAsync_ConfirmerFalse_NoStampOrVisibility_RowStaysNotConfirmed()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") with { BodySha256 = "expected-hash" } };
        prod.Rows.Add(Youtube(1, "aaa"));
        var confirmer = new FakeDeployedBodyConfirmer { ConfirmedResult = false };
        // Codex-HIGH fix: VerifyAndPublishAsync only polls the /app deploy-confirm endpoint when
        // sync.directpush-gitbody is ON. These tests exercise that ON poll path, so the flag reader
        // is turned ON explicitly (Build defaults it OFF per D-05).
        var coordinator = Build(local, prod, prodReader: new FakeDirectPushFlagReader { FlagValue = true }, confirmer: confirmer);

        var result = await coordinator.VerifyAndPublishAsync(publish, CancellationToken.None);

        Assert.Empty(result.Confirmed);
        Assert.Single(result.NotConfirmed);
        Assert.Empty(prod.StampCalls);
        Assert.Empty(prod.VisibilityKeyCalls);
        var call = Assert.Single(confirmer.Calls);
        Assert.Equal(ContentSourceType.Youtube, call.Type);
        Assert.Equal("aaa", call.Value);
        Assert.Equal("expected-hash", call.ExpectedHash);
    }

    [Fact]
    public async Task VerifyAndPublishAsync_ConfirmerTrue_RunsConfirmAndPublish_ClearsMarker()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") with { BodySha256 = "expected-hash" } };
        prod.Rows.Add(Youtube(1, "aaa"));
        var confirmer = new FakeDeployedBodyConfirmer { ConfirmedResult = true };
        // Codex-HIGH fix: VerifyAndPublishAsync only polls the /app deploy-confirm endpoint when
        // sync.directpush-gitbody is ON. These tests exercise that ON poll path, so the flag reader
        // is turned ON explicitly (Build defaults it OFF per D-05).
        var coordinator = Build(local, prod, prodReader: new FakeDirectPushFlagReader { FlagValue = true }, confirmer: confirmer);

        var result = await coordinator.VerifyAndPublishAsync(publish, CancellationToken.None);

        Assert.Single(result.Confirmed);
        Assert.Empty(result.NotConfirmed);
        Assert.Single(prod.StampCalls);
        Assert.Single(prod.VisibilityKeyCalls);
        Assert.Single(local.StampCalls);
        Assert.Single(local.VisibilityKeyCalls);
        Assert.Single(local.ClearAwaitingConfirmCalls);
    }

    [Fact]
    public async Task VerifyAndPublishAsync_RowWithNoBodyHash_TreatedAsNotConfirmed_ConfirmerNeverCalled()
    {
        // A row with no stored body_sha256 has nothing to match against — never call the confirmer,
        // never publish.
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") with { BodySha256 = null } };
        prod.Rows.Add(Youtube(1, "aaa"));
        var confirmer = new FakeDeployedBodyConfirmer { ConfirmedResult = true };
        // Codex-HIGH fix: VerifyAndPublishAsync only polls the /app deploy-confirm endpoint when
        // sync.directpush-gitbody is ON. These tests exercise that ON poll path, so the flag reader
        // is turned ON explicitly (Build defaults it OFF per D-05).
        var coordinator = Build(local, prod, prodReader: new FakeDirectPushFlagReader { FlagValue = true }, confirmer: confirmer);

        var result = await coordinator.VerifyAndPublishAsync(publish, CancellationToken.None);

        Assert.Empty(result.Confirmed);
        Assert.Single(result.NotConfirmed);
        Assert.Empty(confirmer.Calls);
        Assert.Empty(prod.StampCalls);
        Assert.Empty(prod.VisibilityKeyCalls);
    }

    // Per-key IDeployedBodyConfirmer test double: confirms only the keys explicitly listed as
    // deployed, so a single VerifyAndPublishAsync call can exercise a mixed confirmed/not-confirmed
    // batch (a real HTTP confirmer polls each row's own natural key independently).
    private sealed class SelectiveDeployedBodyConfirmer : IDeployedBodyConfirmer
    {
        public HashSet<string> ConfirmedKeys { get; } = new(StringComparer.Ordinal);

        public Task<bool> IsDeployedBodyConfirmedAsync(
            string naturalKeyType,
            string naturalKeyValue,
            string expectedBodySha256,
            CancellationToken cancellationToken)
            => Task.FromResult(ConfirmedKeys.Contains(naturalKeyValue));
    }

    [Fact]
    public async Task VerifyAndPublishAsync_MixedConfirmedAndNot_OnlyConfirmedRowsPublished()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var confirmedRow = Youtube(1, "confirmed") with { BodySha256 = "hash-confirmed" };
        var notConfirmedRow = Youtube(2, "pending") with { BodySha256 = "hash-pending" };
        prod.Rows.Add(Youtube(1, "confirmed"));
        prod.Rows.Add(Youtube(2, "pending"));
        var confirmer = new SelectiveDeployedBodyConfirmer { ConfirmedKeys = { "confirmed" } };
        // Codex-HIGH fix: VerifyAndPublishAsync only polls the /app deploy-confirm endpoint when
        // sync.directpush-gitbody is ON. These tests exercise that ON poll path, so the flag reader
        // is turned ON explicitly (Build defaults it OFF per D-05).
        var coordinator = Build(local, prod, prodReader: new FakeDirectPushFlagReader { FlagValue = true }, confirmer: confirmer);

        var result = await coordinator.VerifyAndPublishAsync(
            new[] { confirmedRow, notConfirmedRow }, CancellationToken.None);

        var confirmedResult = Assert.Single(result.Confirmed);
        Assert.Equal("confirmed", confirmedResult.YoutubeVideoId);
        var pendingResult = Assert.Single(result.NotConfirmed);
        Assert.Equal("pending", pendingResult.YoutubeVideoId);
        // Only the confirmed row's key was stamped/made visible.
        Assert.Single(prod.StampCalls);
        Assert.Single(prod.StampCalls[0].Keys);
        Assert.Equal("confirmed", prod.StampCalls[0].Keys[0].Value);
    }

    [Fact]
    public async Task VerifyAndPublishAsync_FlagOff_PublishesImmediately_WithoutPollingConfirmer()
    {
        // Codex-HIGH regression: with sync.directpush-gitbody OFF (default), Stage 4 keeps
        // [skip render] so Render never redeploys the body to /app — polling /app would 404 forever
        // and strand the row awaiting-confirm. In the OFF path the body is served live from the
        // /data overlay, so VerifyAndPublishAsync must publish immediately and NEVER poll the
        // confirmer (proven here by a confirmer hard-wired to FALSE that must never be consulted).
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa") with { BodySha256 = "expected-hash" } };
        prod.Rows.Add(Youtube(1, "aaa"));
        var confirmer = new FakeDeployedBodyConfirmer { ConfirmedResult = false };
        var coordinator = Build(local, prod, prodReader: new FakeDirectPushFlagReader { FlagValue = false }, confirmer: confirmer);

        var result = await coordinator.VerifyAndPublishAsync(publish, CancellationToken.None);

        // Published without a poll: confirmer untouched, row treated as confirmed, stamp+visibility ran.
        Assert.Empty(confirmer.Calls);
        Assert.Single(result.Confirmed);
        Assert.Empty(result.NotConfirmed);
        Assert.Single(prod.StampCalls);
        Assert.Single(prod.VisibilityKeyCalls);
        Assert.Single(local.StampCalls);
        Assert.Single(local.VisibilityKeyCalls);
        Assert.Single(local.ClearAwaitingConfirmCalls);
    }

    // Minimal recording logger: captures formatted Warning messages for D-08 skip-log assertions.
    private sealed class RecordingTestLogger : ILogger
    {
        public List<string> Warnings { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
