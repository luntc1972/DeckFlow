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
        Assert.Equal(new[] { null, "3", "1" }, chips.Select(x => x.QuerySelector(".flag-filter__chip-count")?.TextContent.Trim()));
        Assert.All(chips, x => Assert.Null(x.GetAttribute("title")));
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
        Assert.Equal("x\"<b>", chip.ChildNodes[0].TextContent.Trim());
        Assert.Equal("1", chip.QuerySelector(".flag-filter__chip-count")!.TextContent.Trim());
        Assert.Null(document.QuerySelector("b"));
    }

    [Fact]
    public async Task FlagsIndex_WithNoFlags_RendersNoChips()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(new AdminFlagsListViewModel()));
        Assert.Contains("No flags loaded yet.", document.Body!.TextContent);
        Assert.Empty(document.QuerySelectorAll("button[data-flag-prefix]"));
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
