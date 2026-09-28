using AngleSharp.Html.Parser;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Pins D-06, D-07 and D-10 shared Analytics markup.</summary>
public sealed class AdminSecondaryPagesViewRenderTests
{
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

    private static AdminAnalyticsViewModel Model(string range, IReadOnlyList<AdminAnalyticsViewModel.RouteRow> routes) => new() { Range = range, Routes = routes };

    private static AdminAnalyticsViewModel.RouteRow Row(string route) => new(route, 4, 2, 0.1, new[] { 1, 2 });

    private static async Task<AngleSharp.Html.Dom.IHtmlDocument> DocumentAsync(AdminAnalyticsViewModel model) =>
        new HtmlParser().ParseDocument(await RazorViewRenderer.RenderAsync(model, typeof(AdminAnalyticsController), "AdminAnalytics", new TestRouter()));

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
