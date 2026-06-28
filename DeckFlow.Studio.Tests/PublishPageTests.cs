using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// bUnit behavioral tests for Publish.razor.
/// Covers PUB-03: approved-count/branch display, Export+Diff flow, Commit gate, and
/// GitForeignStagedChangesException surfacing. Also asserts IGitRepository has no push verb.
/// </summary>
public sealed class PublishPageTests : BunitContext
{
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

    private static ContentSiteIndexRow MakeApprovedRowWithPublish(long id, string videoId, DateTimeOffset? pushedToProdUtc, bool isVisible)
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
            PushedToProdUtc = pushedToProdUtc,
            IsVisible = isVisible,
        };

    private (IRenderedComponent<Publish> Cut, FakeGitRepository Git, FakeContentKbOrchestrator Orchestrator, FakeContentSiteIndexStore Store)
        RenderPublish(IEnumerable<ContentSiteIndexRow>? approvedRows = null, string branch = "v1.7")
    {
        var git = new FakeGitRepository { CannedBranch = branch, CannedRepoRoot = "/fake/repo" };
        var orchestrator = new FakeContentKbOrchestrator();
        var store = new FakeContentSiteIndexStore();

        if (approvedRows is not null)
        {
            foreach (var r in approvedRows)
            {
                store.Rows.Add(r);
            }
        }

        var artifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-tests-pub", "content-kb");
        Services.AddSingleton<IGitRepository>(git);
        Services.AddSingleton<IContentKbOrchestrator>(orchestrator);
        Services.AddSingleton<IContentSiteIndexStore>(store);
        Services.AddSingleton(new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot });
        Services.AddSingleton<PublishStateDeriver>();
        // Why: the page now resolves its orchestration through PublishCoordinator (H1 split); the
        // coordinator is built from the fakes registered above, so page behavior is unchanged.
        Services.AddScoped<DeckFlow.Studio.ViewModels.PublishCoordinator>();

        var cut = Render<Publish>();
        return (cut, git, orchestrator, store);
    }

    // ── PUB-03: On load, approved-count and branch are displayed ────────────

    [Fact]
    public void OnLoad_DisplaysBranchAndApprovedCount()
    {
        // Arrange: 2 approved rows, branch "v1.7"
        var rows = new[] { MakeApprovedRow(1, "vid1"), MakeApprovedRow(2, "vid2") };
        var (cut, _, _, _) = RenderPublish(rows, "v1.7");

        // Act: wait for OnInitializedAsync to complete
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Resolving repository info", cut.Markup);
        });

        // Assert: branch name shown
        Assert.Contains("v1.7", cut.Markup);

        // Assert: approved-count shown
        Assert.Contains("2", cut.Markup);
        Assert.Contains("entries approved", cut.Markup);
    }

    [Fact]
    public void PublishPage_PublishStateSummary_RendersCountsForApprovedRows()
    {
        var rows = new[]
        {
            MakeApprovedRow(1, "vid1"),
            MakeApprovedRowWithPublish(2, "vid2", DateTimeOffset.UtcNow.AddMinutes(5), isVisible: true),
        };
        var (cut, _, _, _) = RenderPublish(rows);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving repository info", cut.Markup));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Never published", cut.Markup);
            Assert.Contains("Published", cut.Markup);
        });
    }

    // ── PUB-03: "Export & Preview Diff" calls ExportIndexToFileAsync + CopyApprovedArtifactsToRepoAsync + DiffAsync ──

    [Fact]
    public void ExportAndPreviewDiff_CallsOrchestratorAndGit_ThenShowsStage2()
    {
        // Arrange
        var rows = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, git, orchestrator, _) = RenderPublish(rows);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving repository info", cut.Markup));

        // Act: click "Export & Preview Diff" (re-find + dispatch on the renderer dispatcher).
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());

        // Assert: orchestrator's ExportIndexToFileAsync was called
        cut.WaitForAssertion(() =>
        {
            Assert.Single(orchestrator.ExportToFilePaths);
            // Path must contain the seed relative path. The page passes an ABSOLUTE path
            // (Path.GetFullPath(Combine(repoRoot, SeedRelative))), so it carries OS separators
            // (backslashes on Windows). Normalize to forward slashes before the substring check
            // so the assertion is separator-agnostic without weakening intent.
            var exportPath = orchestrator.ExportToFilePaths[0].Replace('\\', '/');
            Assert.Contains("content-kb/seed/index-seed.json", exportPath);
        });

        // Assert: CopyApprovedArtifactsToRepoAsync was called
        cut.WaitForAssertion(() => Assert.Equal(1, orchestrator.CopyApprovedCallCount));

        // Assert: Stage 2 card becomes visible
        cut.WaitForAssertion(() => Assert.Contains("Stage 2 — Commit", cut.Markup));
    }

    [Fact]
    public void ExportAndPreviewDiff_ShowsAddedUpdatedRemovedCounts_FromDiff()
    {
        // Arrange: empty HEAD seed (no prior content) means all rows are "Added"
        var rows = new[] { MakeApprovedRow(1, "vid1") };

        // Set up orchestrator to return a single export row
        var (cut, git, orchestrator, _) = RenderPublish(rows);
        orchestrator.CannedExportResult = new ContentIndexExportResult
        {
            Success = true,
            RowCount = 1,
            Rows = new[]
            {
                new ContentIndexExportRow
                {
                    NaturalKeyType = "youtube_channel",
                    NaturalKeyValue = "vid1",
                    Source = "test-channel",
                    Title = "Video 1",
                    VideoUrl = "https://youtu.be/vid1",
                    ArtifactPath = "content-kb/test-channel/vid1.md",
                    IndexedUtc = DateTimeOffset.UtcNow,
                    ArchetypeTags = Array.Empty<string>(),
                    BracketTags = Array.Empty<string>(),
                    CardCategoryTags = Array.Empty<string>(),
                },
            },
        };
        // HEAD seed is empty = first publish, so this row is "Added"
        git.CannedHeadSeed = string.Empty;

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving repository info", cut.Markup));

        // Act
        cut.Find("button.btn-outline-primary").Click();

        // Assert: Added=1, Updated=0, Removed=0 displayed in diff preview
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Added: 1", cut.Markup);
            Assert.Contains("Updated: 0", cut.Markup);
            Assert.Contains("Removed: 0", cut.Markup);
        });

        // Assert: commit message reports the DELTA (what changed), not the full seed size.
        cut.WaitForAssertion(() =>
        {
            var commitMsg = cut.Find("input#commitMessage").GetAttribute("value");
            Assert.Equal("content: publish KB seed (1 added, 0 updated, 0 removed)", commitMsg);
        });
    }

    // ── PUB-03: "Commit to Branch" disabled until checkbox checked AND rawDiff non-empty ──

    [Fact]
    public void CommitButton_IsDisabled_BeforeCheckboxChecked()
    {
        // Arrange
        var rows = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _) = RenderPublish(rows);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving repository info", cut.Markup));

        // Trigger export to show Stage 2
        cut.Find("button.btn-outline-primary").Click();
        cut.WaitForAssertion(() => Assert.Contains("Stage 2 — Commit", cut.Markup));

        // Assert: commit button is disabled while checkbox not checked
        var commitBtn = cut.Find("button.btn-primary:not(.btn-outline-primary)");
        Assert.True(commitBtn.HasAttribute("disabled"),
            "Commit button must be disabled when diff-reviewed checkbox is unchecked");
    }

    [Fact]
    public void CommitButton_BecomesEnabled_AfterCheckboxChecked()
    {
        // Arrange
        var rows = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, _, _, _) = RenderPublish(rows);

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving repository info", cut.Markup));

        // Trigger export and let the post-Task.Run renders fully settle.
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Stage 2 — Commit"));

        // Act: re-find the checkbox immediately before dispatch and run the onchange on the
        // renderer dispatcher so its handler id is the current (settled) one, not a stale id
        // captured before the InvokeAsync(StateHasChanged) re-renders.
        cut.InvokeAsync(() => cut.Find("input#diffReviewed").Change(true));

        // Assert: commit button is now enabled
        cut.WaitForAssertion(() =>
        {
            var commitBtn = cut.Find("button.btn-primary:not(.btn-outline-primary)");
            Assert.False(commitBtn.HasAttribute("disabled"),
                "Commit button must be enabled after checkbox is checked and rawDiff is non-empty");
        });
    }

    // ── PUB-03: Successful commit shows SHA and push reminder, no push called ──

    [Fact]
    public void SuccessfulCommit_ShowsShaAndPushReminder()
    {
        // Arrange
        var rows = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, git, _, _) = RenderPublish(rows);
        git.CannedCommitSha = "deadbeef";

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving repository info", cut.Markup));

        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Stage 2 — Commit"));

        // Check the diff-reviewed checkbox (re-find + dispatch on renderer dispatcher).
        cut.InvokeAsync(() => cut.Find("input#diffReviewed").Change(true));

        // Act: click commit (re-find the button immediately before dispatch).
        cut.WaitForAssertion(() =>
        {
            var btn = cut.Find("button.btn-primary:not(.btn-outline-primary)");
            Assert.False(btn.HasAttribute("disabled"));
        });
        cut.InvokeAsync(() => cut.Find("button.btn-primary:not(.btn-outline-primary)").Click());

        // Assert: SHA shown
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("deadbeef", cut.Markup);
        });

        // Assert: push reminder shown (without Studio ever calling push)
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("git push origin", cut.Markup);
            Assert.Contains("Studio never pushes", cut.Markup);
        });
    }

    [Fact]
    public void SuccessfulCommit_CallsStageAndCommitAsync_WithSeedAndArtifactPaths()
    {
        // Arrange
        var rows = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, git, orchestrator, _) = RenderPublish(rows);
        orchestrator.CannedCopiedArtifactPaths = new[] { "content-kb/test-channel/vid1.md" };

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving repository info", cut.Markup));

        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Stage 2 — Commit"));
        cut.InvokeAsync(() => cut.Find("input#diffReviewed").Change(true));
        cut.WaitForAssertion(() => Assert.False(
            cut.Find("button.btn-primary:not(.btn-outline-primary)").HasAttribute("disabled")));

        // Act
        cut.InvokeAsync(() => cut.Find("button.btn-primary:not(.btn-outline-primary)").Click());

        // Assert: StageAndCommitAsync was called with the seed + artifact paths
        cut.WaitForAssertion(() =>
        {
            Assert.Single(git.CommitCalls);
            var (repoRoot, paths, msg) = git.CommitCalls[0];
            Assert.Contains("content-kb/seed/index-seed.json", paths);
            Assert.Contains("content-kb/test-channel/vid1.md", paths);
        });
    }

    [Fact]
    public void SuccessfulCommit_StampsApprovedKeys_AfterCommit()
    {
        var rows = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, git, orchestrator, store) = RenderPublish(rows);
        orchestrator.CannedExportResult = new ContentIndexExportResult
        {
            Success = true,
            RowCount = 1,
            Rows =
            [
                new ContentIndexExportRow
                {
                    NaturalKeyType = ContentSourceType.Youtube,
                    NaturalKeyValue = "vid1",
                    Source = "test-channel",
                    Title = "Video 1",
                    VideoUrl = "https://youtu.be/vid1",
                    ArtifactPath = "content-kb/test-channel/vid1.md",
                    IndexedUtc = DateTimeOffset.UtcNow,
                    ArchetypeTags = Array.Empty<string>(),
                    BracketTags = Array.Empty<string>(),
                    CardCategoryTags = Array.Empty<string>(),
                },
            ],
        };

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving repository info", cut.Markup));
        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Stage 2 — Commit"));
        cut.InvokeAsync(() => cut.Find("input#diffReviewed").Change(true));
        cut.WaitForAssertion(() => Assert.False(
            cut.Find("button.btn-primary:not(.btn-outline-primary)").HasAttribute("disabled")));

        cut.InvokeAsync(() => cut.Find("button.btn-primary:not(.btn-outline-primary)").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.Single(git.CommitCalls);
            Assert.Single(store.StampCalls);
            var stamp = store.StampCalls[0];
            var key = Assert.Single(stamp.Keys);
            Assert.Equal(ContentSourceType.Youtube, key.Type);
            Assert.Equal("vid1", key.Value);
            Assert.Equal(stamp.PushedUtc, store.Rows.Single().PushedToProdUtc);
        });
    }

    // ── PUB-03: IGitRepository has no push method (structural contract) ──────

    [Fact]
    public void IGitRepository_HasNoPushMethod()
    {
        // Assert: the interface contract has NO method with "push" in its name (case-insensitive).
        // Studio never pushes — this is a structural/contractual guarantee.
        var methods = typeof(IGitRepository).GetMethods();
        var pushMethods = methods.Where(m => m.Name.Contains("Push", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Empty(pushMethods);
    }

    // ── PUB-03: GitForeignStagedChangesException surfaces specific error ─────

    [Fact]
    public void CommitAsync_GitForeignStagedChangesException_ShowsSpecificErrorMessage()
    {
        // Arrange
        var rows = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, git, _, _) = RenderPublish(rows);
        git.ThrowOnCommit = new GitForeignStagedChangesException(new[] { "some/other/file.cs" });

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving repository info", cut.Markup));

        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Stage 2 — Commit"));
        cut.InvokeAsync(() => cut.Find("input#diffReviewed").Change(true));
        cut.WaitForAssertion(() => Assert.False(
            cut.Find("button.btn-primary:not(.btn-outline-primary)").HasAttribute("disabled")));

        // Act: commit — will throw GitForeignStagedChangesException
        cut.InvokeAsync(() => cut.Find("button.btn-primary:not(.btn-outline-primary)").Click());

        // Assert: the specific "unrelated changes are already staged" error is surfaced
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("unrelated changes are already staged", cut.Markup);
        });

        // Assert: commit success was NOT set — no SHA shown, no push reminder
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Studio never pushes", cut.Markup);
        });
    }

    [Fact]
    public void CommitAsync_GitForeignStagedChangesException_DoesNotSetCommitSuccess()
    {
        // Arrange
        var rows = new[] { MakeApprovedRow(1, "vid1") };
        var (cut, git, _, _) = RenderPublish(rows);
        git.ThrowOnCommit = new GitForeignStagedChangesException(new[] { "unrelated.cs" });

        cut.WaitForAssertion(() => Assert.DoesNotContain("Resolving repository info", cut.Markup));

        cut.InvokeAsync(() => cut.Find("button.btn-outline-primary").Click());
        cut.WaitForState(() => cut.Markup.Contains("Stage 2 — Commit"));
        cut.InvokeAsync(() => cut.Find("input#diffReviewed").Change(true));
        cut.WaitForAssertion(() => Assert.False(
            cut.Find("button.btn-primary:not(.btn-outline-primary)").HasAttribute("disabled")));

        // Act
        cut.InvokeAsync(() => cut.Find("button.btn-primary:not(.btn-outline-primary)").Click());

        // Assert: success alert NOT shown
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("alert-success", cut.Markup);
        });
    }
}
