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
/// Render-level guard for the cEDH meta-gap view. Ensures the Step-3 "Render Meta Gap
/// Analysis" submit button carries its own busy-overlay copy so the loading message says it
/// is rendering the analysis rather than the form-level "generating the prompt" text used by
/// the Step-2 submit.
/// </summary>
public sealed class MetaGapViewRenderTests
{
    [Fact]
    public async Task RenderButton_CarriesAnalysisSpecificBusyCopy()
    {
        var model = new MetaGapViewModel
        {
            ActiveTab = DeckPageTab.CedhMetaGap,
            Request = new MetaGapRequest(),
        };

        string html = await RenderCedhMetaGapViewAsync(model);

        Assert.Contains("data-busy-title=\"Rendering cEDH Meta Gap Analysis\"", html, StringComparison.Ordinal);
        Assert.Contains("data-busy-message=\"Reading your pasted analysis and building the report.\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintButton_RendersInResultsToolbar_WhenAnalysisPresent()
    {
        var model = new MetaGapViewModel
        {
            ActiveTab = DeckPageTab.CedhMetaGap,
            Request = new MetaGapRequest { WorkflowStep = 3 },
            AnalysisResponse = new MetaGapResponse(),
        };

        string html = await RenderCedhMetaGapViewAsync(model);

        Assert.Contains("data-prompt-print", html, StringComparison.Ordinal);
        Assert.Contains("Print results", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintButton_AbsentWhenNoAnalysis()
    {
        var model = new MetaGapViewModel
        {
            ActiveTab = DeckPageTab.CedhMetaGap,
            Request = new MetaGapRequest(),
        };

        string html = await RenderCedhMetaGapViewAsync(model);

        Assert.DoesNotContain("data-prompt-print", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Print results", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderCedhMetaGapViewAsync(MetaGapViewModel model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(serviceProvider => serviceProvider.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(serviceProvider => serviceProvider.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        // The shared _DeckToolTabs partial (@inject) needs these two services to activate.
        services.AddSingleton<DeckFlow.Web.Services.Tools.IToolRegistry, DeckFlow.Web.Services.Tools.ToolRegistry>();
        services.AddSingleton<DeckFlow.Web.Services.FeatureFlags.IFeatureFlagCache>(new FakeFeatureFlagCache());
        services.AddControllersWithViews().AddApplicationPart(typeof(DeckPacketController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "Deck" })),
            new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "CedhMetaGap", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'CedhMetaGap' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

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
