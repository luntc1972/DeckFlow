using System.Diagnostics;
using AngleSharp.Html.Parser;
using DeckFlow.Web.Controllers.Admin;
using DeckFlow.Web.Services.FeatureFlags;
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

/// <summary>Pins derived FLAGS-01 namespace chip markup read by admin-flags.ts (D-03, D-02).</summary>
public sealed class AdminFlagsViewRenderTests
{
    [Fact]
    public async Task FlagsIndex_RendersAllChipThenDerivedNamespaceChipsInModelOrder()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(Model("analysis.manabase.accuracy", "analysis.manabase.baseline", "analysis.wincon-map", "sync.reconcile")));
        var chips = document.QuerySelectorAll("[aria-label='Namespace filter'] button[data-flag-prefix]").ToArray();
        Assert.Equal(new[] { "", "analysis.", "sync." }, chips.Select(x => x.GetAttribute("data-flag-prefix")));
        Assert.Equal(new[] { "All", "Analysis", "Sync" }, chips.Select(x => x.ChildNodes[0].TextContent.Trim()));
        Assert.Equal(new[] { null, "3", "1" }, chips.Select(x => x.QuerySelector(".admin-filter-chips__count")?.TextContent.Trim()));
        Assert.Null(chips[0].GetAttribute("title"));
        Assert.Equal(new[] { "analysis", "sync" }, chips.Skip(1).Select(x => x.GetAttribute("title")));
        Assert.Equal("true", chips[0].GetAttribute("aria-pressed"));
        Assert.All(chips.Skip(1), x => Assert.Equal("false", x.GetAttribute("aria-pressed")));
        Assert.All(chips, x => Assert.True((x.GetAttribute("data-flag-prefix") ?? string.Empty).Count(c => c == '.') <= 1));
        Assert.Equal(new[] { "", "on", "off" }, document.QuerySelectorAll("[aria-label='Status filter'] button").Select(x => x.GetAttribute("data-flag-status")));
    }

    [Fact]
    public async Task FlagsIndex_WithoutServiceOrAnalysisKeys_RendersNoServiceOrAnalysisChip()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(Model("sync.reconcile")));
        Assert.Equal(new[] { "", "sync." }, document.QuerySelectorAll("button[data-flag-prefix]").Select(x => x.GetAttribute("data-flag-prefix")));
    }

    [Fact]
    public async Task FlagsIndex_EncodesGroupPrefixAndLabel()
    {
        var model = new AdminFlagsListViewModel { Flags = new[] { new FlagRow("safe.row", true, "safe") }, Groups = new[] { new FlagFilterGroup("x\"<b>.", "x\"<b>", 1) } };
        var document = new HtmlParser().ParseDocument(await RenderAsync(model));
        var chip = document.QuerySelectorAll("button[data-flag-prefix]").Skip(1).Single();
        Assert.Equal("x\"<b>.", chip.GetAttribute("data-flag-prefix"));
        Assert.Equal("x\"<b>", chip.GetAttribute("title"));
        Assert.Equal("x\"<b>", chip.ChildNodes[0].TextContent.Trim());
        Assert.Equal("1", chip.QuerySelector(".admin-filter-chips__count")!.TextContent.Trim());
        Assert.Null(document.QuerySelector("b"));
    }

    [Fact]
    public async Task FlagsIndex_DerivedChips_ShowFriendlyLabelAndRawNamespaceTitle()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(Model("analysis.x", "service.y", "widgets.z")));
        var chips = document.QuerySelectorAll("[aria-label='Namespace filter'] button[data-flag-prefix]").Skip(1).ToArray();

        Assert.Equal(new[] { "Analysis", "Background services", "Widgets" }, chips.Select(x => x.ChildNodes[0].TextContent.Trim()));
        Assert.Equal(new[] { "analysis", "service", "widgets" }, chips.Select(x => x.GetAttribute("title")));
        Assert.Equal(new[] { "1", "1", "1" }, chips.Select(x => x.QuerySelector(".admin-filter-chips__count")!.TextContent.Trim()));
    }

    [Fact]
    public async Task FlagsIndex_WithNoFlags_RendersNoChips()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(new AdminFlagsListViewModel()));
        Assert.Equal("No flags loaded yet.", document.QuerySelector("section.admin-card[aria-label='Feature flags'] p.admin-empty")!.TextContent.Trim());
        Assert.Empty(document.QuerySelectorAll("button[data-flag-prefix]"));
    }

    [Fact]
    public async Task FlagsIndex_FilterUsesSharedFilterComponents()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(Model("analysis.a", "analysis.b", "sync.c")));
        Assert.NotNull(document.QuerySelector("div.admin-filter .admin-field.admin-filter-search label[for='flag-filter-search']"));
        Assert.NotNull(document.QuerySelector("div.admin-filter .admin-field.admin-filter-search input#flag-filter-search[aria-controls='flag-table']"));
        Assert.Equal(new[] { "Namespace filter", "Status filter" }, document.QuerySelectorAll(".admin-filter-chips[role='group']").Select(x => x.GetAttribute("aria-label")));
        Assert.All(document.QuerySelectorAll(".admin-filter-chips button"), x => Assert.Contains("admin-filter-chips__chip", x.ClassList));
        Assert.Equal(new[] { "2", "1" }, document.QuerySelectorAll("button[data-flag-prefix]").Skip(1).Select(x => x.QuerySelector(".admin-filter-chips__count")!.TextContent.Trim()));
        Assert.Contains("admin-filter__count", document.QuerySelector("#flag-filter-count")!.ClassList);
        Assert.Contains("admin-filter__empty-row", document.QuerySelector("tr#flag-filter-empty")!.ClassList);
        Assert.Contains("hidden", document.QuerySelector("tr#flag-filter-empty")!.ClassList);
        Assert.Null(document.QuerySelector(".flag-filter, .flag-filter__chips, .flag-filter__chip, .flag-filter__chip-count, .flag-filter__count, .flag-filter__empty-row"));
    }

    [Fact]
    public async Task FlagsIndex_TableInCardWithBadgesAndSecondaryToggles()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(new AdminFlagsListViewModel { Flags = new[] { new FlagRow("enabled.flag", true, "enabled"), new FlagRow("disabled.flag", false, "") } }));
        Assert.NotNull(document.QuerySelector("section.admin-card[aria-label='Feature flags'] table#flag-table"));
        Assert.Equal(new[] { "On", "Off" }, document.QuerySelectorAll("td[data-label='Status']").Select(x => x.TextContent.Trim()));
        Assert.NotNull(document.QuerySelector("span.admin-badge.admin-badge--success"));
        Assert.NotNull(document.QuerySelector("span.admin-badge.admin-badge--neutral"));
        foreach (var form in document.QuerySelectorAll("form.admin-action-form"))
        {
            var button = form.QuerySelector("button[type='submit']")!;
            Assert.Contains("admin-button", button.ClassList);
            Assert.Contains("admin-button--secondary", button.ClassList);
            Assert.NotNull(form.QuerySelector("input[name='enabled']"));
            Assert.NotNull(form.QuerySelector("input[name='__RequestVerificationToken']"));
        }
        Assert.NotNull(document.QuerySelector("td.admin-flags__desc"));
    }

    [Fact]
    public async Task FlagsIndex_NamespaceChipCounts_RenderZeroOneAndMany()
    {
        var model = new AdminFlagsListViewModel { Flags = new[] { new FlagRow("analysis.a", true, "a"), new FlagRow("analysis.b", true, "b"), new FlagRow("sync.c", true, "c") }, Groups = new[] { new FlagFilterGroup("analysis.", "analysis", 2), new FlagFilterGroup("empty.", "empty", 0), new FlagFilterGroup("sync.", "sync", 1) } };
        var document = new HtmlParser().ParseDocument(await RenderAsync(model));
        var chips = document.QuerySelectorAll("button[data-flag-prefix]").Skip(1).ToArray();
        Assert.Equal(new[] { "analysis.", "empty.", "sync." }, chips.Select(x => x.GetAttribute("data-flag-prefix")));
        Assert.All(chips, x => Assert.Contains("admin-filter-chips__chip", x.ClassList));
        Assert.Equal(new[] { "2", "0", "1" }, chips.Select(x => x.QuerySelectorAll("span.admin-filter-chips__count").Single().TextContent.Trim()));
    }

    private static AdminFlagsListViewModel Model(params string[] keys) => new()
    {
        Flags = keys.Select(key => new FlagRow(key, true, key)).ToArray(),
        Groups = FlagFilterGroups.Derive(keys),
    };

    private static Task<string> RenderAsync(object model) => RazorViewRenderer.RenderAsync(
        model,
        typeof(AdminFlagsController),
        "AdminFlags",
        new TestRouter());

    private sealed class TestRouter : IRouter { public Task RouteAsync(RouteContext context) => Task.CompletedTask; public VirtualPathData? GetVirtualPath(VirtualPathContext context) => new(this, $"Admin/Flags/{context.Values["action"]}"); }
}
