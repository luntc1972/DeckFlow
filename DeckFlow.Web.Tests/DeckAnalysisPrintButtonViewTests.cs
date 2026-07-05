using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Configuration;
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
using Microsoft.Extensions.Options;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Render-level guard for the "Print results" button on <c>Views/Deck/DeckAnalysis.cshtml</c>.
/// The button is wired to <c>window.print()</c> in deck-sync.ts (CSP blocks inline onclick) and
/// paired with the <c>@media print</c> rules in site-common.css that isolate the result panels.
/// It must render in the Step 3 (analysis) and Step 5 (set-upgrade) result toolbars whenever a
/// result is present, and must be absent before any result exists. Mirrors the Razor-view render
/// harness used by <see cref="DeckAnalysisWinConMapViewTests"/>.
/// </summary>
public sealed class DeckAnalysisPrintButtonViewTests
{
    private const string PrintButtonHook = "data-prompt-print";
    private const string PrintButtonLabel = "Print results";

    [Fact]
    public async Task AnalysisResponsePresent_RendersPrintButton()
    {
        var model = new DeckAnalysisViewModel
        {
            Request = new DeckAnalysisRequest { TargetAiPlatform = "ChatGPT", WorkflowStep = 3 },
            AnalysisResponse = new DeckAnalysisResponse { Format = "Commander", Commander = "Test Commander" },
        };

        string html = await RenderAsync(model);

        Assert.Contains(PrintButtonHook, html, StringComparison.Ordinal);
        Assert.Contains(PrintButtonLabel, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetUpgradeResponsePresent_RendersPrintButton()
    {
        var model = new DeckAnalysisViewModel
        {
            Request = new DeckAnalysisRequest { TargetAiPlatform = "ChatGPT", WorkflowStep = 5 },
            SetUpgradeResponse = new SetUpgradeResponse(),
        };

        string html = await RenderAsync(model);

        Assert.Contains(PrintButtonHook, html, StringComparison.Ordinal);
        Assert.Contains(PrintButtonLabel, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoResults_RendersNoPrintButton()
    {
        var model = new DeckAnalysisViewModel
        {
            Request = new DeckAnalysisRequest { TargetAiPlatform = "ChatGPT", WorkflowStep = 1 },
        };

        string html = await RenderAsync(model);

        Assert.DoesNotContain(PrintButtonHook, html, StringComparison.Ordinal);
        Assert.DoesNotContain(PrintButtonLabel, html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(DeckAnalysisViewModel model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        // The shared _DeckToolTabs partial (@inject) needs these two services to activate.
        services.AddSingleton<DeckFlow.Web.Services.Tools.IToolRegistry, DeckFlow.Web.Services.Tools.ToolRegistry>();
        services.AddSingleton<DeckFlow.Web.Services.FeatureFlags.IFeatureFlagCache>(new FakeFeatureFlagCache());
        // The _AiSelector partial (@inject IOptions<AiPlatformOptions>) needs the options accessor.
        services.AddSingleton<IOptions<AiPlatformOptions>>(Options.Create(new AiPlatformOptions()));
        services.AddControllersWithViews().AddApplicationPart(typeof(DeckPacketController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "Deck" })),
            new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "DeckAnalysis", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'DeckAnalysis' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

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
