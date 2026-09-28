using AngleSharp.Html.Parser;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Pins D-06, D-07 and D-10 shared Analytics markup.</summary>
public sealed class AdminSecondaryPagesViewRenderTests
{
    [Fact]
    public async Task YoutubeExportIndex_FormIsSharedCardWithFieldsAndPrimaryButton()
    {
        var document = await YoutubeDocumentAsync(new AdminYoutubeExportViewModel { Channel = "@examplechannel", Limit = 50 });
        var card = document.QuerySelector("section.admin-card[aria-labelledby=yt-export-heading]");
        Assert.NotNull(card?.QuerySelector("h2#yt-export-heading.admin-card__title"));
        Assert.Equal(3, card!.QuerySelectorAll("form[data-yt-export-form] .admin-field").Length);
        Assert.Equal("@examplechannel", card.QuerySelector("input#yt-export-channel")!.GetAttribute("value"));
        Assert.Equal("50", card.QuerySelector("input#yt-export-limit")!.GetAttribute("value"));
        Assert.NotNull(card.QuerySelector("input[name=downloadToken], input[name=__RequestVerificationToken]"));
        Assert.NotNull(card.QuerySelector("button.admin-button.admin-button--primary"));
        Assert.Empty(document.QuerySelectorAll(".admin-banner, .admin-harvest__panel"));
    }

    [Fact]
    public async Task YoutubeExportIndex_ErrorUsesDangerBannerWithAlertRole()
    {
        var document = await YoutubeDocumentAsync(new AdminYoutubeExportViewModel { ErrorMessage = "channel not found" });
        Assert.Equal("channel not found", document.QuerySelector("p.admin-banner.admin-banner--danger[role=alert]")!.TextContent.Trim());
        Assert.Empty(document.QuerySelectorAll(".admin-banner--error"));
    }
    [Fact]
    public async Task AnalyticsIndex_BodyHasNoPageHeadingAndKeepsTableHooks()
    {
        var document = await DocumentAsync(Model("7d", new[] { Row("/x?<img src=y>") }));
        Assert.Empty(document.QuerySelectorAll("h1, header, .admin-range-selector, .admin-analytics"));
        Assert.NotNull(document.QuerySelector("section.admin-card[data-admin-analytics] table.admin-table.admin-analytics-table"));
        Assert.Empty(document.QuerySelectorAll("img"));
        Assert.NotNull(document.QuerySelector("noscript meta[http-equiv='refresh']"));
    }

    [Fact]
    public async Task AnalyticsIndex_RangeChipsRenderInPageHeaderActions()
    {
        var document = await LayoutDocumentAsync(Model("7d", Array.Empty<AdminAnalyticsViewModel.RouteRow>()));
        Assert.Equal("Page-usage analytics", document.QuerySelector("h1.admin-page-header__title")!.TextContent.Trim());
        Assert.Equal("Page-view and traffic analytics over time.", document.QuerySelector(".admin-page-header__lede")!.TextContent.Trim());
        var chips = document.QuerySelectorAll(".admin-page-header__actions nav.admin-filter-chips[aria-label='Time range'] a.admin-filter-chips__chip");
        Assert.Equal(4, chips.Length);
        var activeChip = Assert.Single(chips, chip => chip.ClassList.Contains("is-active"));
        Assert.Equal("Last 7 days", activeChip.TextContent.Trim());
        Assert.Equal("true", activeChip.GetAttribute("aria-current"));
    }

    [Fact]
    public async Task AnalyticsIndex_NoRoutes_RendersSharedEmptyState()
    {
        var document = await DocumentAsync(Model("7d", Array.Empty<AdminAnalyticsViewModel.RouteRow>()));
        Assert.Equal("No request metrics recorded yet for this window.", document.QuerySelector("p.admin-empty")!.TextContent.Trim());
        Assert.Empty(document.QuerySelectorAll("table"));
    }

    [Fact]
    public async Task FeedbackIndex_StatusChipsAndTypeFieldUseSharedComponents()
    {
        var document = await FeedbackDocumentAsync(ListModel(Array.Empty<FeedbackItem>(), FeedbackStatus.New));
        var chips = document.QuerySelectorAll("div.admin-filter nav.admin-filter-chips[aria-label='Status filter'] a.admin-filter-chips__chip");
        Assert.Equal(4, chips.Length);
        Assert.Equal("New (2)", chips[0].TextContent.Trim());
        Assert.Equal("true", chips[0].GetAttribute("aria-current"));
        Assert.NotNull(document.QuerySelector("form.admin-field[method=get] input[name=status]"));
        Assert.NotNull(document.QuerySelector("label[for=typeSelect] + select#typeSelect[data-admin-feedback-submit-on-change]"));
        Assert.Empty(document.QuerySelectorAll("h1, .admin-feedback, .admin-feedback-filters, .admin-feedback-filter, .admin-feedback-type"));
    }

    [Fact]
    public async Task FeedbackIndex_TableBadgesButtonsAndPagination()
    {
        var items = new[] { Item(1, FeedbackType.Bug, FeedbackStatus.New), Item(2, FeedbackType.Suggestion, FeedbackStatus.Archived) };
        var document = await FeedbackDocumentAsync(ListModel(items, FeedbackStatus.New, totalCount: 120, page: 2));
        Assert.NotNull(document.QuerySelector("section.admin-card table.admin-table.admin-table--card"));
        Assert.Equal(3, document.QuerySelectorAll("span.admin-badge.admin-badge--neutral").Length);
        Assert.NotNull(document.QuerySelector("span.admin-badge.admin-badge--info"));
        Assert.NotNull(document.QuerySelector("form button.admin-button.admin-button--secondary"));
        Assert.NotNull(document.QuerySelector("nav.admin-pagination"));
        Assert.Empty(document.QuerySelectorAll(".type-badge, .admin-feedback-table, .admin-feedback-pagination"));
    }

    [Fact]
    public async Task FeedbackIndex_NoItems_RendersSharedEmptyState()
    {
        var document = await FeedbackDocumentAsync(ListModel(Array.Empty<FeedbackItem>(), FeedbackStatus.New));
        Assert.Equal("No feedback in this view.", document.QuerySelector("section.admin-card[aria-label='Feedback submissions'] p.admin-empty")!.TextContent.Trim());
        Assert.Empty(document.QuerySelectorAll(".admin-feedback-empty"));
    }

    [Fact]
    public async Task FeedbackDetail_UsesCardsArtifactAndDangerDelete()
    {
        var item = Item(9, FeedbackType.Bug, FeedbackStatus.New, "<script>x</script>\nmessage");
        var document = await FeedbackDetailDocumentAsync(item);
        Assert.Empty(document.QuerySelectorAll("h1, script"));
        Assert.NotNull(document.QuerySelector("a.admin-button.admin-button--secondary"));
        Assert.NotNull(document.QuerySelector("section.admin-card dl.detail-grid"));
        Assert.NotNull(document.QuerySelector("section.admin-card[aria-labelledby=feedback-message-heading] pre.admin-artifact"));
        Assert.NotNull(document.QuerySelector("form[data-admin-confirm-delete][data-admin-feedback-id] button.admin-button.admin-button--danger"));
        Assert.NotNull(document.QuerySelector("dialog#admin-confirm-modal"));
        Assert.Single(document.QuerySelectorAll("div.admin-stack"));
        Assert.Empty(document.QuerySelectorAll(".admin-feedback-detail, .detail-message, .detail-actions, button.danger"));
    }

    private static AdminAnalyticsViewModel Model(string range, IReadOnlyList<AdminAnalyticsViewModel.RouteRow> routes) => new() { Range = range, Routes = routes };

    private static AdminAnalyticsViewModel.RouteRow Row(string route) => new(route, 4, 2, 0.1, new[] { 1, 2 });

    private static FeedbackItem Item(long id, FeedbackType type, FeedbackStatus status, string message = "message") =>
        new(id, DateTime.UtcNow, type, message, null, null, null, null, null, status);

    private static AdminFeedbackListViewModel ListModel(IReadOnlyList<FeedbackItem> items, FeedbackStatus status, int totalCount = 3, int page = 1) => new()
    {
        Items = items,
        StatusFilter = status,
        TotalCount = totalCount,
        Page = page,
        CountsByStatus = new Dictionary<FeedbackStatus, int>
        {
            [FeedbackStatus.New] = 2,
            [FeedbackStatus.Read] = 1,
            [FeedbackStatus.Archived] = 0,
        },
    };

    private static async Task<AngleSharp.Html.Dom.IHtmlDocument> DocumentAsync(AdminAnalyticsViewModel model) =>
        new HtmlParser().ParseDocument(await RazorViewRenderer.RenderAsync(model, typeof(AdminAnalyticsController), "AdminAnalytics", new TestRouter()));

    private static async Task<AngleSharp.Html.Dom.IHtmlDocument> FeedbackDocumentAsync(AdminFeedbackListViewModel model) =>
        new HtmlParser().ParseDocument(await RazorViewRenderer.RenderAsync(model, typeof(AdminFeedbackController), "AdminFeedback", new TestRouter()));

    private static async Task<AngleSharp.Html.Dom.IHtmlDocument> FeedbackDetailDocumentAsync(FeedbackItem model) =>
        new HtmlParser().ParseDocument(await RazorViewRenderer.RenderAsync(model, typeof(AdminFeedbackController), "AdminFeedback", new TestRouter(), viewName: "Detail"));

    private static async Task<AngleSharp.Html.Dom.IHtmlDocument> YoutubeDocumentAsync(AdminYoutubeExportViewModel model) =>
        new HtmlParser().ParseDocument(await RazorViewRenderer.RenderAsync(model, typeof(AdminYoutubeExportController), "AdminYoutubeExport", new TestRouter()));

    private static async Task<AngleSharp.Html.Dom.IHtmlDocument> LayoutDocumentAsync(AdminAnalyticsViewModel model)
    {
        var html = await RazorViewRenderer.RenderAsync(model, typeof(Program), "AdminAnalytics", new TestRouter(), isMainPage: true, configureServices: services => services.AddSingleton<IVersionService, FixedVersionService>());
        return new HtmlParser().ParseDocument(html);
    }

    private sealed class TestRouter : IRouter
    {
        public Task RouteAsync(RouteContext context) => Task.CompletedTask;

        public VirtualPathData? GetVirtualPath(VirtualPathContext context) => new(this, context.Values.TryGetValue("range", out var range) ? $"Admin/Analytics?range={range}" : "Admin/Analytics");
    }

    private sealed class FixedVersionService : IVersionService
    {
        public string GetVersion() => "1.2.3";
    }
}
