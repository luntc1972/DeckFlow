using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Phase 80 code-review fix (Codex MED finding): the <c>DeckAnalysis</c> POST action only SET the
/// hidden round-trip JSON fields (ScoreJson/InteractionAuditJson/WinConMapJson) when the typed result
/// was non-null; it never CLEARED them. A client that posts a stale field from a prior flag-ON
/// session (operator flips the flag OFF between requests) would see that field survive on the
/// re-rendered page, because the view gates the hidden textarea on the REQUEST field, not the typed
/// result. These tests drive the real <see cref="DeckPacketController.DeckAnalysis(DeckAnalysisRequest)"/>
/// action end-to-end and render the real <c>DeckAnalysis.cshtml</c> view to prove the stale field is
/// cleared and the resulting page is byte-identical to the true flag-OFF baseline (no field posted at
/// all). One test suffices for WinConMap; Score and InteractionAudit share the identical fix.
/// </summary>
public sealed class DeckAnalysisPostFlagIdentityTests
{
    [Fact]
    public async Task DeckAnalysis_FlagOffWithStalePostedWinConMapJson_ClearsFieldAndRendersBaselinePage()
    {
        var fakeService = new FakeDeckAnalysisPacketService
        {
            Result = new DeckAnalysisPacketResult(
                "summary",
                "Test Deck | AI Deck Analysis",
                "{}",
                "reference",
                "analysis prompt text",
                null,
                null,
                null,
                WinConMap: null)
        };
        var flagCache = new FakeFeatureFlagCache(new Dictionary<string, bool> { ["analysis.wincon-map"] = false });

        // Stale posted WinConMapJson from a prior flag-ON session -- must be cleared, not carried forward.
        var staleRequest = new DeckAnalysisRequest
        {
            DeckSource = "https://www.moxfield.com/decks/test-wincon-flag-off",
            TargetAiPlatform = "ChatGPT",
            WinConMapJson = "{\"combos\":[{\"cardNames\":[\"Kiki-Jiki, Mirror Breaker\",\"Restoration Angel\"],\"results\":[\"Infinite combat steps\"]}]}"
        };
        var staleModel = await PostAsync(fakeService, flagCache, staleRequest);

        Assert.Equal(string.Empty, staleModel.Request.WinConMapJson);
        Assert.Null(staleModel.WinConMap);

        var baselineRequest = new DeckAnalysisRequest
        {
            DeckSource = "https://www.moxfield.com/decks/test-wincon-flag-off",
            TargetAiPlatform = "ChatGPT",
            WinConMapJson = string.Empty
        };
        var baselineModel = await PostAsync(fakeService, flagCache, baselineRequest);

        var staleHtml = NeutralizeAntiforgery(await RenderAsync(staleModel));
        var baselineHtml = NeutralizeAntiforgery(await RenderAsync(baselineModel));

        Assert.DoesNotContain("name=\"WinConMapJson\"", staleHtml, StringComparison.Ordinal);
        Assert.Equal(baselineHtml, staleHtml);
    }

    /// <summary>
    /// Replaces the per-render antiforgery token value with a stable placeholder so two independent
    /// renders (each building their own ephemeral data-protection key ring) compare equal outside the
    /// deliberately-varying antiforgery token.
    /// </summary>
    private static string NeutralizeAntiforgery(string html) => Regex.Replace(
        html,
        "(name=\"__RequestVerificationToken\"[^>]*value=\")[^\"]*\"",
        "${1}TOKEN\"");

    private static async Task<DeckAnalysisViewModel> PostAsync(
        FakeDeckAnalysisPacketService service,
        FakeFeatureFlagCache flagCache,
        DeckAnalysisRequest request)
    {
        var controller = new DeckPacketController(
            service,
            new FakeDeckComparisonService(),
            new StubMetaGapService(),
            new PacketSessionCache(),
            NullLogger<DeckPacketController>.Instance,
            flagCache: flagCache)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.DeckAnalysis(request);
        var view = Assert.IsType<ViewResult>(result);
        return Assert.IsType<DeckAnalysisViewModel>(view.Model);
    }

    private static async Task<string> RenderAsync(DeckAnalysisViewModel model)
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
        // The _AiSelector partial (@inject IOptions<AiPlatformOptions>) needs the options accessor.
        services.AddSingleton<IOptions<AiPlatformOptions>>(Options.Create(new AiPlatformOptions()));
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
