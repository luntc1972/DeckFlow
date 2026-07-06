using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
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
/// Render-level guard for the "Print results" button on <c>Views/Deck/DeckComparison.cshtml</c>.
/// The button (wired to <c>window.print()</c> in deck-sync.ts) and the shared <c>@media print</c>
/// rules in site-common.css must render in the comparison results toolbar whenever a comparison
/// response is present, and be absent before any result exists. Mirrors the cedh
/// <see cref="MetaGapViewRenderTests"/> / analysis print-button render harness.
/// </summary>
public sealed class DeckComparisonPrintButtonViewTests
{
    private const string PrintButtonHook = "data-prompt-print";
    private const string PrintButtonLabel = "Print results";

    [Fact]
    public async Task ComparisonResponsePresent_RendersPrintButton()
    {
        var model = new DeckComparisonViewModel
        {
            ActiveTab = DeckPageTab.DeckComparison,
            Request = new DeckComparisonRequest { WorkflowStep = 3 },
            ComparisonResponse = new DeckComparisonResponse { DeckAName = "Deck A", DeckBName = "Deck B" },
        };

        string html = await RenderAsync(model);

        Assert.Contains(PrintButtonHook, html, StringComparison.Ordinal);
        Assert.Contains(PrintButtonLabel, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedComparisonPrompt_CarriesResponseSplitTip()
    {
        var model = new DeckComparisonViewModel
        {
            ActiveTab = DeckPageTab.DeckComparison,
            Request = new DeckComparisonRequest { WorkflowStep = 2 },
            ComparisonPromptText = "GENERATED COMPARISON PROMPT",
        };

        string html = await RenderAsync(model);

        Assert.Contains("data-prompt-split-tip=\"deck_comparison\"", html, StringComparison.Ordinal);
        Assert.Contains("Output only the deck_comparison JSON in a single response", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoComparisonResponse_RendersNoPrintButton()
    {
        var model = new DeckComparisonViewModel
        {
            ActiveTab = DeckPageTab.DeckComparison,
            Request = new DeckComparisonRequest(),
        };

        string html = await RenderAsync(model);

        Assert.DoesNotContain(PrintButtonHook, html, StringComparison.Ordinal);
        Assert.DoesNotContain(PrintButtonLabel, html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(DeckComparisonViewModel model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<DeckFlow.Web.Services.Tools.IToolRegistry, DeckFlow.Web.Services.Tools.ToolRegistry>();
        services.AddSingleton<DeckFlow.Web.Services.FeatureFlags.IFeatureFlagCache>(new FakeFeatureFlagCache());
        services.AddControllersWithViews().AddApplicationPart(typeof(DeckPacketController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "Deck" })),
            new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "DeckComparison", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'DeckComparison' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View!,
            viewData,
            new TempDataDictionary(httpContext, new StubTempDataProvider()),
            writer,
            new HtmlHelperOptions());

        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }

    private static IWebHostEnvironment CreateHostingEnvironment()
    {
        var contentRoot = AppContext.BaseDirectory;
        var fileProvider = new NullFileProvider();
        return new TestWebHostEnvironment
        {
            ApplicationName = typeof(DeckPacketController).Assembly.GetName().Name ?? "DeckFlow.Web",
            ContentRootPath = contentRoot,
            ContentRootFileProvider = fileProvider,
            EnvironmentName = Environments.Development,
            WebRootPath = contentRoot,
            WebRootFileProvider = fileProvider,
        };
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
    }
}
