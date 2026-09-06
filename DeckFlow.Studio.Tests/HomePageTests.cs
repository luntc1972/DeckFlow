using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Studio.Tests;

public sealed class HomePageTests : BunitContext
{
    private static ContentSiteIndexRow MakeYoutubeRow(
        long id,
        string videoId,
        string approvalStatus = "pending",
        DateTimeOffset? pushedToProdUtc = null,
        bool isVisible = false,
        DateTimeOffset? indexedUtc = null)
        => new ContentSiteIndexRow
        {
            Id = id,
            Source = "test-channel",
            Title = $"Video {id}",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            IndexedUtc = indexedUtc ?? DateTimeOffset.UtcNow,
            ApprovalStatus = approvalStatus,
            PushedToProdUtc = pushedToProdUtc,
            IsVisible = isVisible,
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
        };

    private IRenderedComponent<Home> RenderHome(FakeContentSiteIndexStore store)
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-tests", "content-kb");
        Services.AddSingleton<IContentSiteIndexStore>(store);
        Services.AddSingleton(new ContentKbOrchestratorOptions { ArtifactRoot = artifactRoot });
        Services.AddSingleton<PublishStateDeriver>();
        Services.AddSingleton(new StudioConfig(true, false));

        return Render<Home>();
    }

    [Fact]
    public void Counts_RenderPerVideoStatusBucket()
    {
        var pushedUtc = new DateTimeOffset(2026, 06, 20, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeContentSiteIndexStore();
        store.Rows.AddRange(
        [
            MakeYoutubeRow(1, "vid-1", approvalStatus: "pending"),
            MakeYoutubeRow(2, "vid-2", approvalStatus: "rejected"),
            MakeYoutubeRow(3, "vid-3", approvalStatus: "approved"),
            MakeYoutubeRow(4, "vid-4", approvalStatus: "approved", pushedToProdUtc: pushedUtc, isVisible: false),
            MakeYoutubeRow(5, "vid-5", approvalStatus: "approved", pushedToProdUtc: pushedUtc, isVisible: true, indexedUtc: pushedUtc.AddMinutes(-5)),
        ]);

        var cut = RenderHome(store);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("0", cut.Find("[data-video-status='Harvested'] .studio-count").TextContent.Trim());
            Assert.Equal("2", cut.Find("[data-video-status='Distilled'] .studio-count").TextContent.Trim());
            Assert.Equal("2", cut.Find("[data-video-status='Approved'] .studio-count").TextContent.Trim());
            Assert.Equal("1", cut.Find("[data-video-status='Published'] .studio-count").TextContent.Trim());
        });
    }

    [Fact]
    public void ZeroBucket_RendersZero()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(MakeYoutubeRow(1, "vid-1", approvalStatus: "approved"));

        var cut = RenderHome(store);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("0", cut.Find("[data-video-status='Distilled'] .studio-count").TextContent.Trim());
            Assert.Contains("Distilled", cut.Markup);
        });
    }

    [Fact]
    public void QuickLinks_PresentForHarvestReviewPublish()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(MakeYoutubeRow(1, "vid-1"));

        var cut = RenderHome(store);

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("a[href='/harvest']"));
            Assert.NotNull(cut.Find("a[href='/review']"));
            Assert.NotNull(cut.Find("a[href='/publish']"));
        });
    }

    [Fact]
    public void StoreFailure_ShowsGenericError_NoLeak()
    {
        const string secret = "Server=prod;Password=secret-token";
        var store = new FakeContentSiteIndexStore
        {
            ReadFailureMessage = secret,
        };

        var cut = RenderHome(store);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Could not load pipeline status — check the Studio data directory and retry.", cut.Markup);
            Assert.DoesNotContain(secret, cut.Markup);
        });
    }

    // ── F-07: buckets link into their filtered destination ──────────────────

    [Fact]
    public void HarvestedBucket_LinksToHarvest()
    {
        var cut = RenderHome(CreateLinkBucketRows());
        cut.WaitForAssertion(() => Assert.Equal("/harvest", cut.Find("[data-video-status='Harvested'] a").GetAttribute("href")));
    }

    [Fact]
    public void DistilledBucket_LinksToPendingReviewQueue()
    {
        var cut = RenderHome(CreateLinkBucketRows());
        cut.WaitForAssertion(() => Assert.Equal("/review?tab=pending", cut.Find("[data-video-status='Distilled'] a").GetAttribute("href")));
    }

    [Fact]
    public void ApprovedBucket_LinksToApprovedReviewQueue()
    {
        var cut = RenderHome(CreateLinkBucketRows());
        cut.WaitForAssertion(() => Assert.Equal("/review?tab=approved", cut.Find("[data-video-status='Approved'] a").GetAttribute("href")));
    }

    [Fact]
    public void PublishedBucket_RendersNoLink()
    {
        var cut = RenderHome(CreateLinkBucketRows());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-video-status='Published'] a")));
    }

    [Fact]
    public void NeverPublishedItem_LinksToPublish()
    {
        var cut = RenderHome(CreateLinkBucketRows());
        cut.WaitForAssertion(() => Assert.Equal("/publish", cut.Find("[data-publish-state='NeverPublished'] a").GetAttribute("href")));
    }

    [Fact]
    public void PushedHiddenItem_LinksToDirectPush()
    {
        var cut = RenderHome(CreateLinkBucketRows());
        cut.WaitForAssertion(() => Assert.Equal("/direct-push", cut.Find("[data-publish-state='PushedHidden'] a").GetAttribute("href")));
    }

    [Fact]
    public void LocalNewerItem_LinksToPublish()
    {
        var cut = RenderHome(CreateLinkBucketRows());
        cut.WaitForAssertion(() => Assert.Equal("/publish", cut.Find("[data-publish-state='LocalNewer'] a").GetAttribute("href")));
    }

    [Fact]
    public void PublishedStateItem_RendersNoLink()
    {
        var cut = RenderHome(CreateLinkBucketRows());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-publish-state='Published'] a")));
    }

    private static FakeContentSiteIndexStore CreateLinkBucketRows()
    {
        var pushedUtc = new DateTimeOffset(2026, 06, 20, 12, 0, 0, TimeSpan.Zero);
        var store = new FakeContentSiteIndexStore();
        store.Rows.AddRange(
        [
            MakeYoutubeRow(1, "distilled", approvalStatus: "pending"),
            MakeYoutubeRow(2, "pushed-hidden", approvalStatus: "approved", pushedToProdUtc: pushedUtc),
            MakeYoutubeRow(3, "published", approvalStatus: "approved", pushedToProdUtc: pushedUtc, isVisible: true, indexedUtc: pushedUtc.AddMinutes(-5)),
            MakeYoutubeRow(4, "local-newer", approvalStatus: "approved", pushedToProdUtc: pushedUtc, isVisible: true, indexedUtc: pushedUtc.AddMinutes(5)),
        ]);

        return store;
    }
}
