using AngleSharp.Html.Parser;
using DeckFlow.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Renders the admin shell contracts for D-09, D-10 and D-12.</summary>
public sealed class AdminShellViewRenderTests
{
    [Theory]
    [InlineData("AdminLanding", "/Admin", "Dashboard")]
    [InlineData("AdminHarvest", "/Admin/Harvest", "Harvest")]
    [InlineData("AdminAnalytics", "/Admin/Analytics", "Analytics")]
    [InlineData("AdminContentKb", "/Admin/ContentKb", "Content KB")]
    [InlineData("AdminCreatorProfile", "/Admin/CreatorProfile", "Deck Tendencies")]
    [InlineData("AdminCreatorStyle", "/Admin/CreatorStyle", "Creator Style")]
    [InlineData("AdminYoutubeExport", "/Admin/YoutubeExport", "YouTube Export")]
    [InlineData("AdminTools", "/Admin/Tools", "Tools")]
    [InlineData("AdminFlags", "/Admin/Flags", "Flags")]
    [InlineData("AdminFeedback", "/Admin/Feedback", "Feedback")]
    public async Task Sidebar_MarksOnlyTheCurrentPage(string controllerName, string expectedHref, string expectedSectionLabel)
    {
        var document = await RenderAsync(controllerName);
        var current = Assert.Single(document.QuerySelectorAll("a.admin-sidebar__link[aria-current=\"page\"]"));
        Assert.Equal(expectedHref, current.GetAttribute("href"));
        Assert.EndsWith($"current section: {expectedSectionLabel}", document.QuerySelector(".sr-only")!.TextContent.Trim());
        Assert.All(document.QuerySelectorAll("a.admin-sidebar__link"), link => Assert.Equal("admin-sidebar__link", link.GetAttribute("class")));
    }

    [Fact]
    public async Task Sidebar_ListsAllTenPagesInOrder()
    {
        var document = await RenderAsync("AdminLanding");
        Assert.Equal(10, document.QuerySelectorAll("a.admin-sidebar__link").Length);
    }

    [Fact]
    public async Task Sidebar_GroupsPagesUnderLabelledGroups()
    {
        var document = await RenderAsync("AdminLanding");
        Assert.Equal(3, document.QuerySelectorAll("div.admin-sidebar__group[role=\"group\"]").Length);
    }

    [Fact]
    public async Task PageHeader_RendersHeadingAndLedeFromViewData()
    {
        var document = await RenderAsync("AdminLanding", new Dictionary<string, object?> { ["Heading"] = "Heading text", ["Lede"] = "Lede text" });
        Assert.Equal("Heading text", document.QuerySelector("h1.admin-page-header__title")!.TextContent.Trim());
        Assert.Equal("Lede text", document.QuerySelector("p.admin-page-header__lede")!.TextContent.Trim());
        Assert.Empty(document.QuerySelectorAll("header.admin-topbar h1"));
    }

    [Fact]
    public async Task PageHeader_FallsBackToTitleWithoutLedeOrActions()
    {
        var document = await RenderAsync("AdminLanding");
        Assert.Equal("Admin", document.QuerySelector("h1.admin-page-header__title")!.TextContent.Trim());
        Assert.Null(document.QuerySelector(".admin-page-header__lede"));
    }

    [Fact]
    public async Task PageHeader_EncodesHeadingAndLede()
    {
        var document = await RenderAsync("AdminLanding", new Dictionary<string, object?> { ["Heading"] = "<b>h</b>", ["Lede"] = "<i>l</i>" });
        Assert.Equal("<b>h</b>", document.QuerySelector("h1")!.TextContent.Trim());
        Assert.Empty(document.QuerySelectorAll(".admin-page-header b, .admin-page-header i"));
    }

    private static async Task<AngleSharp.Html.Dom.IHtmlDocument> RenderAsync(string controllerName, IReadOnlyDictionary<string, object?>? viewData = null)
    {
        var html = await RazorViewRenderer.RenderAsync(null, typeof(Program), controllerName, viewName: "~/Views/AdminLanding/Index.cshtml", isMainPage: true, configureServices: services => services.AddSingleton<IVersionService, FixedVersionService>(), viewData: viewData);
        return new HtmlParser().ParseDocument(html);
    }

    private sealed class FixedVersionService : IVersionService
    {
        public string GetVersion() => "1.2.3";
    }
}
