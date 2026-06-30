using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Core.Bracket;
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
/// Render-level guard for the flag-gated bracket result section on <c>Views/Deck/Bracket.cshtml</c>.
/// Renders the real Razor view through <see cref="IRazorViewEngine"/> so the OFF page invariant
/// (no <c>bracket-badge</c> markup) and the ON state are enforced in CI — a source-text
/// scan cannot distinguish the two states because the markup literal always exists in the .cshtml.
/// </summary>
public sealed class BracketViewRenderTests
{
    [Fact]
    public async Task OffState_ClassificationNull_RendersNoBracketBadge()
    {
        // No Classification → HasResult = false → result section must not render.
        var model = new BracketViewModel();

        string html = await RenderBracketViewAsync(model);

        Assert.DoesNotContain("bracket-badge", html, StringComparison.Ordinal);
        Assert.DoesNotContain("THIS DECK CLASSIFIES AS", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnState_B4Classification_RendersBracketBadgeWithB4Modifier()
    {
        var classification = new BracketClassification(
            BracketNumber: 4,
            DetectedGameChangers: ["Sol Ring", "Demonic Tutor", "Rhystic Study", "Smothering Tithe"],
            DetectedMassLandDenial: [],
            DetectedExtraTurnCards: [],
            TwoCardCombos: null,
            ComboDetectionAvailable: false,
            EffectiveDate: "2025-10-01");

        var model = new BracketViewModel
        {
            Classification = classification,
            Request = new BracketRequest
            {
                DeckInputSource = DeckInputSource.PasteText,
                DeckText = "1 Sol Ring",
            },
        };

        string html = await RenderBracketViewAsync(model);

        Assert.Contains("bracket-badge", html, StringComparison.Ordinal);
        Assert.Contains("bracket-badge--b4", html, StringComparison.Ordinal);
        Assert.Contains("THIS DECK CLASSIFIES AS", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderBracketViewAsync(BracketViewModel model)
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
        services.AddControllersWithViews().AddApplicationPart(typeof(BracketController).Assembly);

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
        var viewResult = viewEngine.FindView(actionContext, "Bracket", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'Bracket' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

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
            ApplicationName = typeof(BracketController).Assembly.GetName().Name ?? "DeckFlow.Web",
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
