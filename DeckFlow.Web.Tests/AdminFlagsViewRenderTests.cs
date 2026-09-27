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
        Assert.Equal(new[] { "All", "analysis", "sync" }, chips.Select(x => x.TextContent.Trim()));
        Assert.Equal(new[] { null, "3 flags", "1 flag" }, chips.Select(x => x.GetAttribute("title")));
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
        Assert.Equal("x\"<b>", chip.TextContent.Trim());
        Assert.Null(document.QuerySelector("b"));
    }

    [Fact]
    public async Task FlagsIndex_WithNoFlags_RendersNoChips()
    {
        var document = new HtmlParser().ParseDocument(await RenderAsync(new AdminFlagsListViewModel()));
        Assert.Contains("No flags loaded yet.", document.Body!.TextContent);
        Assert.Empty(document.QuerySelectorAll("button[data-flag-prefix]"));
    }

    private static AdminFlagsListViewModel Model(params string[] keys) => new() { Flags = keys.Select(key => new FlagRow(key, true, key)).ToArray(), Groups = FlagFilterGroups.Derive(keys) };

    // Why: the test project has no shared Razor render helper, so this harness is duplicated per class.
    private static async Task<string> RenderAsync(object model)
    {
        var services = new ServiceCollection(); services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>(); services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests")); services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>()); services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment()); services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>()); services.AddLogging(); services.AddDataProtection(); services.AddControllersWithViews().AddApplicationPart(typeof(AdminFlagsController).Assembly);
        using var provider = services.BuildServiceProvider(); var context = new DefaultHttpContext { RequestServices = provider }; var routeData = new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "AdminFlags" })); routeData.Routers.Add(new TestRouter()); var action = new ActionContext(context, routeData, new ActionDescriptor()); var result = provider.GetRequiredService<IRazorViewEngine>().FindView(action, "Index", false); Assert.True(result.Success); var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model }; await using var writer = new StringWriter(); await result.View!.RenderAsync(new ViewContext(action, result.View, data, new TempDataDictionary(context, new StubTempDataProvider()), writer, new HtmlHelperOptions())); return writer.ToString();
    }

    private static IWebHostEnvironment CreateHostingEnvironment() => new TestWebHostEnvironment { ApplicationName = typeof(AdminFlagsController).Assembly.GetName().Name ?? "DeckFlow.Web", ContentRootPath = AppContext.BaseDirectory, ContentRootFileProvider = new NullFileProvider(), EnvironmentName = Environments.Development, WebRootPath = AppContext.BaseDirectory, WebRootFileProvider = new NullFileProvider() };
    private sealed class StubTempDataProvider : ITempDataProvider { public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>(); public void SaveTempData(HttpContext context, IDictionary<string, object> values) { } }
    private sealed class TestRouter : IRouter { public Task RouteAsync(RouteContext context) => Task.CompletedTask; public VirtualPathData? GetVirtualPath(VirtualPathContext context) => new(this, $"Admin/Flags/{context.Values["action"]}"); }
    private sealed class TestWebHostEnvironment : IWebHostEnvironment { public string ApplicationName { get; set; } = string.Empty; public IFileProvider ContentRootFileProvider { get; set; } = null!; public string ContentRootPath { get; set; } = string.Empty; public string EnvironmentName { get; set; } = string.Empty; public IFileProvider WebRootFileProvider { get; set; } = null!; public string WebRootPath { get; set; } = string.Empty; }
}
