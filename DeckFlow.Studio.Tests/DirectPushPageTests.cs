using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// bUnit behavioral tests for DirectPush.razor (Direct Prod-DB + SCP publish path).
/// Wave-0 scaffold: the 8 req/SC facts are stubbed and discoverable under the
/// <c>--filter "DirectPush"</c> harness; Plan 03 fills the bodies once DirectPush.razor exists.
/// </summary>
public sealed class DirectPushPageTests : Bunit.BunitContext
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

    private (FakeContentSiteIndexStore LocalStore,
             FakeContentSiteIndexStore ProdStore,
             FakeSshArtifactUploader Uploader,
             FakeProdStoreFactory ProdFactory)
        RenderDirectPush(
            IEnumerable<ContentSiteIndexRow>? localApproved = null,
            IEnumerable<ContentSiteIndexRow>? prodRows = null,
            bool isProdConfigured = true,
            bool isScpConfigured = true)
    {
        var localStore = new FakeContentSiteIndexStore();
        var prodStore = new FakeContentSiteIndexStore();
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

        Services.AddSingleton<IContentSiteIndexStore>(localStore);
        Services.AddSingleton<ISshArtifactUploader>(uploader);
        Services.AddSingleton<IProdStoreFactory>(prodFactory);
        Services.AddSingleton(new StudioConfig(isProdConfigured, isScpConfigured));

        // TODO(47-03): un-stub once DirectPush.razor exists
        // var cut = Render<DirectPush>();
        return (localStore, prodStore, uploader, prodFactory);
    }

    // ── Stubbed facts (Plan 03 fills the bodies) ─────────────────────────────

    [Fact]
    public void DirectPush_DiffPreview_ShowsNewUpdatedCounts()
    {
        // PUB-05/SC1: diff shows New/Updated counts before any write.
        Assert.True(true, "stub — implemented in 47-03");
    }

    [Fact]
    public void DirectPush_CheckboxGates_ScpButton()
    {
        // PUB-04/SC2: confirmation checkbox gates the Stage 2 (SCP) button.
        Assert.True(true, "stub — implemented in 47-03");
    }

    [Fact]
    public void DirectPush_Stage3Locked_UntilScpSuccess()
    {
        // PUB-04/SC2: Stage 3 (DB) button disabled until Stage 2 SCP full success.
        Assert.True(true, "stub — implemented in 47-03");
    }

    [Fact]
    public void DirectPush_UsesContentColumnsOnlyUpsert()
    {
        // PUB-04/SC3: only UpsertContentColumnsOnlyAsync called on prod (never full-row upsert).
        Assert.True(true, "stub — implemented in 47-03");
    }

    [Fact]
    public void DirectPush_ScpPartialFailure_Stage3Locked()
    {
        // PUB-05/SC4: SCP partial failure keeps Stage 3 locked + per-file list shown.
        Assert.True(true, "stub — implemented in 47-03");
    }

    [Fact]
    public void DirectPush_DbPartialFailure_PerRowListShown()
    {
        // PUB-05/SC4: DB partial failure shows per-row list; does not re-lock Stage 2.
        Assert.True(true, "stub — implemented in 47-03");
    }

    [Fact]
    public void DirectPush_Secrets_NeverInMarkup()
    {
        // SC5: secrets (conn string, SSH host/user/key, remote path) never appear in markup.
        Assert.True(true, "stub — implemented in 47-03");
    }

    [Fact]
    public void DirectPush_NotConfigured_ButtonsDisabled()
    {
        // PUB-04/SC2: "not configured" (prod or SCP) disables all action buttons.
        Assert.True(true, "stub — implemented in 47-03");
    }
}
