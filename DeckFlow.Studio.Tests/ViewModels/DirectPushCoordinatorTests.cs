using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.ViewModels;
using Microsoft.Extensions.Configuration;

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
        FakeContentKbOrchestrator? orchestrator = null)
        => new(
            local,
            uploader ?? new FakeSshArtifactUploader(),
            new FakeProdStoreFactory(prod),
            new ConfigurationBuilder().Build(),
            new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot },
            git ?? new FakeGitRepository(),
            orchestrator ?? new FakeContentKbOrchestrator());

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

    // ── WritePublishAsync (transactional batch + stamp/visibility) ───────────

    [Fact]
    public async Task WritePublishAsync_HappyPath_UsesContentColumnsOnlyBatch_AndStampsBothStores()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa"), Youtube(2, "bbb") };
        // Seed prod so the stamp/visibility passes have rows to match.
        prod.Rows.Add(Youtube(1, "aaa"));
        prod.Rows.Add(Youtube(2, "bbb"));
        var coordinator = Build(local, prod);

        await coordinator.WritePublishAsync(publish, CancellationToken.None);

        // SC3 / D-08: only the content-columns-only BATCH upsert ran on prod — never a full-row upsert.
        Assert.Equal(new[] { "UpsertContentColumnsOnlyBatchAsync" }, prod.UpsertMethodCalls);
        Assert.Single(prod.BatchUpsertCalls);
        // Prod stamped + made visible, and the local store advanced too.
        Assert.Single(prod.StampCalls);
        Assert.Single(prod.VisibilityKeyCalls);
        Assert.Single(local.StampCalls);
        Assert.Single(local.VisibilityKeyCalls);
    }

    [Fact]
    public async Task WritePublishAsync_BatchRollback_Throws_AndDoesNotStampProd()
    {
        var local = new FakeContentSiteIndexStore();
        var prod = new FakeContentSiteIndexStore();
        var publish = new List<ContentSiteIndexRow> { Youtube(1, "aaa"), Youtube(2, "boom") };
        prod.KeysToFailOnUpsert.Add("boom");
        var coordinator = Build(local, prod);

        await Assert.ThrowsAsync<ContentSiteIndexBatchUpsertException>(
            () => coordinator.WritePublishAsync(publish, CancellationToken.None));

        // PUB-01: nothing was stamped or made visible on EITHER store — the whole batch rolled back.
        Assert.Empty(prod.StampCalls);
        Assert.Empty(prod.VisibilityKeyCalls);
        Assert.Empty(local.StampCalls);
        Assert.Empty(local.VisibilityKeyCalls);
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

        // Committed EXACTLY those body paths — the seed path is never staged (anti-pattern guard).
        var commit = Assert.Single(git.CommitCalls);
        Assert.Equal(copied, commit.Paths);
        Assert.DoesNotContain(commit.Paths, p => p.Contains("index-seed.json", StringComparison.Ordinal));

        // Commit message carries the Render deploy-skip phrase (NOT [skip ci]).
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

        // Anti-pattern guard (Codex NIT): the approved-only seed export/copy path is NEVER invoked —
        // a future refactor that called it would regress the 86-row seed even if the commit pathspec
        // happened to exclude it.
        Assert.Empty(orchestrator.ExportToFilePaths);
        Assert.Equal(0, orchestrator.CopyApprovedCallCount);
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
        Assert.Equal(2, commit.Paths.Count);               // both staged; git commits only the changed 1
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
}
