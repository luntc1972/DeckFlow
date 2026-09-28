using AngleSharp.Html.Parser;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Services.Harvest;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Pins shared-component markup for the Harvest Commanders grid and category breakdown.
/// </summary>
public sealed class AdminHarvestCommandersViewTests
{
    [Fact]
    public async Task CommandersGrid_SummaryLine_UsesSharedMeta()
    {
        var document = await RenderDocumentAsync(new CommandersGridViewModel
        {
            HarvestedCommanders = [new HarvestedCommanderRow("Commander One", 3, "2026-01-01T00:00:00.0000000Z")],
            DeckTotalCount = 1,
        }, "_CommandersGrid");

        var summary = document.Body!.FirstElementChild;
        Assert.NotNull(summary);
        Assert.Equal("P", summary.LocalName.ToUpperInvariant());
        Assert.Equal("admin-meta", summary.ClassName);
        Assert.Equal("1 commanders — Page 1 of 1", summary.TextContent);
    }

    [Fact]
    public async Task CommandersGrid_Pager_UsesSharedPagination()
    {
        var document = await RenderDocumentAsync(new CommandersGridViewModel
        {
            HarvestedCommanders = [new HarvestedCommanderRow("Commander One", 3, "2026-01-01T00:00:00.0000000Z")],
            DeckPage = 2,
            DeckPageSize = AdminHarvestViewModel.DefaultDeckPageSize,
            DeckTotalCount = 250,
        }, "_CommandersGrid");

        var pagers = document.QuerySelectorAll("nav.admin-pagination");
        var pager = Assert.Single(pagers);
        Assert.Equal("admin-pagination", pager.ClassName);
        Assert.Equal("Commander grid pagination", pager.GetAttribute("aria-label"));
        var currentPage = pager.QuerySelector("strong[aria-current=\"page\"]");
        Assert.NotNull(currentPage);
        Assert.Equal("2", currentPage.TextContent);
        Assert.Null(currentPage.GetAttribute("class"));
        Assert.All(pager.QuerySelectorAll("a"), link =>
        {
            Assert.NotNull(link.GetAttribute("data-page"));
            Assert.NotNull(link.GetAttribute("data-search"));
            Assert.NotNull(link.GetAttribute("data-sort-by"));
            Assert.NotNull(link.GetAttribute("data-sort-dir"));
        });
    }

    [Fact]
    public async Task CategoryBreakdown_Intro_UsesSharedMeta()
    {
        var document = await RenderDocumentAsync(new CommanderCategoryBreakdownViewModel
        {
            CommanderName = "Commander One",
            CommanderDeckCount = 40,
            Summaries = [new CommanderCategorySummary("Ramp", 1, 5, .125)],
        }, "_CommanderCategoryBreakdown");

        var section = document.QuerySelector("section");
        Assert.NotNull(section);
        var intro = section.FirstElementChild;
        Assert.NotNull(intro);
        Assert.Equal("P", intro.LocalName.ToUpperInvariant());
        Assert.Equal("admin-meta", intro.ClassName);
        Assert.Equal("Based on 40 harvested decks.", intro.TextContent);
        Assert.NotNull(section.QuerySelector("table"));
    }

    private static async Task<AngleSharp.Dom.IDocument> RenderDocumentAsync(object model, string viewName)
        => new HtmlParser().ParseDocument(await RazorViewRenderer.RenderAsync(model, typeof(AdminHarvestController), "AdminHarvest", viewName: viewName));
}
