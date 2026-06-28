using DeckFlow.Core.Content;
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
        string artifactRoot = "/data/content-kb")
        => new(
            local,
            uploader ?? new FakeSshArtifactUploader(),
            new FakeProdStoreFactory(prod),
            new ConfigurationBuilder().Build(),
            new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot });

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
}
