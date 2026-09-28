using System.Diagnostics;
using AngleSharp.Html.Parser;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Services.Tools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Pins the /Admin/Tools filter markup used by admin-tools.ts and the e2e helpers (TOOLS-01, TOOLS-02, D-01).
/// </summary>
public sealed class AdminToolsViewRenderTests
{
    [Fact]
    public async Task ToolsIndex_RendersSearchInputStatusChipsCountAndHiddenEmptyState()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(Model()));
        Assert.NotNull(document.QuerySelector("label[for=tools-filter-search]"));
        Assert.Equal(new[] { "", "on", "off" }, document.QuerySelectorAll("button[data-tools-status]").Select(x => x.GetAttribute("data-tools-status")));
        Assert.Equal("true", document.QuerySelector("button[data-tools-status]")!.GetAttribute("aria-pressed"));
        var filter = document.QuerySelector("[data-tools-filter]")!;
        Assert.Contains("hidden", filter.ClassList);
        Assert.Contains("admin-filter", filter.ClassList);
        Assert.Equal("polite", document.QuerySelector("#tools-filter-count")!.GetAttribute("aria-live"));
        Assert.Contains("hidden", document.QuerySelector("#tools-filter-empty")!.ClassList);
    }

    [Fact]
    public async Task ToolsIndex_EveryRowCarriesLabelFlagKeyAndEnabledState()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(Model()));
        Assert.Equal("Deck Analysis", document.QuerySelectorAll("tr[data-tool-label]")[0].GetAttribute("data-tool-label"));
        Assert.Equal("tool.deck-analysis.enabled", document.QuerySelectorAll("tr[data-tool-label]")[0].GetAttribute("data-tool-flag-key"));
        Assert.Equal("true", document.QuerySelectorAll("tr[data-tool-label]")[0].GetAttribute("data-tool-enabled"));
        Assert.Equal("false", document.QuerySelectorAll("tr[data-tool-label]")[1].GetAttribute("data-tool-enabled"));
    }

    [Fact]
    public async Task ToolsIndex_EncodesMarkupCharactersInRowDataAttributes()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(Model("Tom & \"Jerry\" <b>x</b>")));
        Assert.Equal("Tom & \"Jerry\" <b>x</b>", document.QuerySelector("tr[data-tool-label]")!.GetAttribute("data-tool-label"));
        Assert.Empty(document.QuerySelectorAll("b"));
    }

    [Fact]
    public async Task ToolsIndex_WithNoTools_OmitsFilterControls()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(new AdminToolsListViewModel { Sections = new[] { new AdminToolSectionViewModel { Section = ToolNavSection.Analyze, Tools = Array.Empty<AdminToolRowViewModel>() } } }));
        Assert.Null(document.QuerySelector("#tools-filter-search"));
        Assert.Empty(document.QuerySelectorAll("button[data-tools-status]"));
        Assert.Null(document.QuerySelector("#tools-filter-count"));
    }

    [Fact]
    public async Task ToolsIndex_SectionsSitInsideTheSearchControlledWrapper()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(Model()));
        Assert.Equal("tools-sections", document.QuerySelector("#tools-filter-search")!.GetAttribute("aria-controls"));
        Assert.NotEmpty(document.QuerySelector("#tools-sections")!.QuerySelectorAll("section[data-tools-section]"));
        Assert.NotNull(document.QuerySelector("#tools-sections #tools-filter-empty"));
        Assert.Null(document.QuerySelector("tbody #tools-filter-empty"));
    }

    [Fact]
    public async Task ToolsIndex_FilterUsesSharedFilterComponents()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(Model()));
        var filter = document.QuerySelector("[data-tools-filter]")!;
        Assert.NotNull(filter.QuerySelector(".admin-field.admin-filter-search label[for=tools-filter-search]"));
        Assert.NotNull(filter.QuerySelector(".admin-field.admin-filter-search input#tools-filter-search"));
        var chips = filter.QuerySelectorAll(".admin-filter-chips[role=group][aria-label='Status filter'] button");
        Assert.Equal(3, chips.Length);
        Assert.All(chips, chip => Assert.Contains("admin-filter-chips__chip", chip.ClassList));
        Assert.Contains("admin-filter__count", document.QuerySelector("#tools-filter-count")!.ClassList);
        var empty = document.QuerySelector("#tools-filter-empty")!;
        Assert.Contains("admin-filter__empty", empty.ClassList);
        Assert.Contains("hidden", empty.ClassList);
        Assert.Null(document.QuerySelector(".tools-filter, .tools-filter__chips, .tools-filter__chip, .tools-filter__count, .tools-filter__empty"));
    }

    [Fact]
    public async Task ToolsIndex_SectionsAreSharedCards()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(Model()));
        var sections = document.QuerySelectorAll("section[data-tools-section]");
        Assert.NotEmpty(sections);
        Assert.All(sections, section =>
        {
            Assert.Contains("admin-card", section.ClassList);
            var headingId = section.GetAttribute("aria-labelledby");
            Assert.NotNull(headingId);
            Assert.Equal(headingId, section.QuerySelector("h2.admin-card__title")!.Id);
        });
        Assert.Contains("admin-stack", document.QuerySelector("#tools-sections")!.ClassList);
        Assert.Null(document.QuerySelector(".admin-tools__section"));
    }

    [Fact]
    public async Task ToolsIndex_ControllerModel_MissingSnapshotKeyRendersEnabled()
    {
        var controller = new AdminToolsController(
            new FakeFeatureFlagStore(),
            new FakeFeatureFlagCache(new Dictionary<string, bool> { ["tool.cut-lab.enabled"] = false }),
            new ToolRegistry());
        var model = Assert.IsType<ViewResult>(controller.Index()).Model;
        var document = new HtmlParser().ParseDocument(await RenderAsync(model!));
        Assert.Equal(new ToolRegistry().All.Count, document.QuerySelectorAll("tr[data-tool-label]").Length);
        Assert.Equal("false", document.QuerySelector("tr[data-tool-label='Cut Lab']")!.GetAttribute("data-tool-enabled"));
        var deckAnalysis = document.QuerySelector("tr[data-tool-label='Deck Analysis']")!;
        Assert.Equal("true", deckAnalysis.GetAttribute("data-tool-enabled"));
        Assert.Equal("On", deckAnalysis.QuerySelector("[data-label='Status']")!.TextContent.Trim());
    }

    private static AdminToolsListViewModel Model(string firstLabel = "Deck Analysis") => new()
    {
        Sections = new[]
        {
            new AdminToolSectionViewModel
            {
                Section = ToolNavSection.Analyze,
                Tools = new[]
                {
                    new AdminToolRowViewModel("deck", firstLabel, "tool.deck-analysis.enabled", true, true),
                    new AdminToolRowViewModel("mana", "Mana Base", "tool.manabase.enabled", false, false),
                },
            },
        },
    };

    private static Task<string> RenderAsync(object model) => RazorViewRenderer.RenderAsync(
        model,
        typeof(AdminToolsController),
        "AdminTools");
}
