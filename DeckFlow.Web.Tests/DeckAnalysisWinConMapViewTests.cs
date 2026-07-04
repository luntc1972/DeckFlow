using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DeckFlow.Core.Analysis;
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
/// Render-level guard for the flag-gated win-condition/combo map block on
/// <c>Views/Deck/DeckAnalysis.cshtml</c> (Phase 80, WINCON-01/03/04). Renders the real Razor view
/// through <see cref="IRazorViewEngine"/> so the OFF invariant (no <c>wincon-map</c> markup and no
/// hidden <c>WinConMapJson</c> field) and the populated state are enforced in CI. Mirrors
/// <see cref="DeckAnalysisInteractionAuditViewTests"/> exactly.
/// </summary>
public sealed class DeckAnalysisWinConMapViewTests
{
    private static readonly WinConMap FixedWinConMap = BuildWinConMap();
    private static readonly string FixedWinConMapJson = JsonSerializer.Serialize(FixedWinConMap);

    [Fact]
    public async Task WinConMapNull_RendersNoWinConMarkup()
    {
        string html = await RenderAsync(winConMap: null, winConMapJson: string.Empty);

        Assert.DoesNotContain("wincon-map", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"WinConMapJson\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WinConMapNull_MarkupEqualsPopulatedMinusWinConBlock()
    {
        string nullHtml = NeutralizeAntiforgery(await RenderAsync(winConMap: null, winConMapJson: FixedWinConMapJson));
        string populatedHtml = NeutralizeAntiforgery(await RenderAsync(FixedWinConMap, FixedWinConMapJson));

        var (nullPrefix, nullSuffix, nullMiddle) = SplitAroundWinConBlock(nullHtml);
        var (populatedPrefix, populatedSuffix, populatedMiddle) = SplitAroundWinConBlock(populatedHtml);

        Assert.Equal(nullPrefix, populatedPrefix);
        Assert.Equal(nullSuffix, populatedSuffix);
        Assert.True(string.IsNullOrWhiteSpace(nullMiddle), "OFF middle should be whitespace-only.");
        Assert.Contains("wincon-map-combo", populatedMiddle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WinConMapPresent_RendersCombosNearCombosBandAndClosers()
    {
        string html = await RenderAsync(FixedWinConMap, FixedWinConMapJson);

        Assert.Contains("wincon-map-combo", html, StringComparison.Ordinal);
        Assert.Contains("Kiki-Jiki, Mirror Breaker", html, StringComparison.Ordinal);
        Assert.Contains("One card away (not currently a win line)", html, StringComparison.Ordinal);
        Assert.Contains("Splinter Twin", html, StringComparison.Ordinal);
        Assert.Contains("Craterhoof Behemoth", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WinConMapUnavailable_RendersDataUnavailableNote()
    {
        var unavailableMap = new WinConMap(
            Combos: Array.Empty<WinConCombo>(),
            NearCombos: Array.Empty<WinConNearCombo>(),
            AssemblyPathCount: 0,
            ClosingCards: Array.Empty<WinConClosingCard>(),
            ComboDataAvailable: false,
            OverallBand: WinConBand.Unknown);

        string html = await RenderAsync(unavailableMap, JsonSerializer.Serialize(unavailableMap));

        Assert.Contains("wincon-map-unavailable", html, StringComparison.Ordinal);
        Assert.Contains("Combo data unavailable", html, StringComparison.Ordinal);
    }

    private static WinConMap BuildWinConMap() => new(
        Combos: new[]
        {
            new WinConCombo(
                CardNames: new[] { "Kiki-Jiki, Mirror Breaker", "Restoration Angel" },
                Results: new[] { "Infinite combat steps" },
                ManaValueNeeded: 8,
                Popularity: 42,
                Band: WinConBand.Mid)
        },
        NearCombos: new[]
        {
            new WinConNearCombo(
                MissingCard: "Splinter Twin",
                CardsInDeck: new[] { "Deceiver Exarch" },
                Results: new[] { "Infinite hasty tokens" })
        },
        AssemblyPathCount: 1,
        ClosingCards: new[] { new WinConClosingCard("Craterhoof Behemoth", 1) },
        ComboDataAvailable: true,
        OverallBand: WinConBand.Mid);

    private static DeckAnalysisViewModel BuildModel(WinConMap? winConMap, string winConMapJson) => new()
    {
        Request = new DeckAnalysisRequest
        {
            TargetAiPlatform = "ChatGPT",
            WorkflowStep = 3,
            WinConMapJson = winConMapJson,
        },
        AnalysisResponse = new DeckAnalysisResponse
        {
            Format = "Commander",
            Commander = "Test Commander",
        },
        WinConMap = winConMap,
    };

    /// <summary>
    /// Replaces the per-render antiforgery token value with a stable placeholder so two renders of
    /// the same form compare equal outside the deliberately-different win-con map block.
    /// </summary>
    private static string NeutralizeAntiforgery(string html) => Regex.Replace(
        html,
        "(name=\"__RequestVerificationToken\"[^>]*value=\")[^\"]*\"",
        "${1}TOKEN\"");

    /// <summary>
    /// Splits a rendered page around the win-con map block insertion point. The prefix is everything
    /// up to and including the Analysis Summary heading; the suffix is everything from the
    /// per-category breakdown onward; the middle is the excised region that holds the interaction-audit
    /// AND win-con map blocks when populated and is whitespace-only when both are OFF. Both renders in
    /// the excision-equality test leave InteractionAudit null, so the only variable content in the
    /// middle is the win-con map block.
    /// </summary>
    private static (string Prefix, string Suffix, string Middle) SplitAroundWinConBlock(string html)
    {
        const string head = "<h3>Analysis Summary</h3>";
        const string stack = "<div class=\"stack\">";

        int headIndex = html.IndexOf(head, StringComparison.Ordinal);
        Assert.True(headIndex >= 0, "Analysis Summary heading not found in rendered output.");
        int afterHead = headIndex + head.Length;

        int stackIndex = html.IndexOf(stack, afterHead, StringComparison.Ordinal);
        Assert.True(stackIndex >= 0, "Per-category breakdown anchor not found in rendered output.");

        string prefix = html.Substring(0, afterHead);
        string middle = html.Substring(afterHead, stackIndex - afterHead);
        string suffix = html.Substring(stackIndex);
        return (prefix, suffix, middle);
    }

    private static async Task<string> RenderAsync(WinConMap? winConMap, string winConMapJson)
    {
        var model = BuildModel(winConMap, winConMapJson);

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
