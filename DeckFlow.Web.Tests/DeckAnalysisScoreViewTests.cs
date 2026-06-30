using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
/// Render-level guard for the flag-gated multi-axis score block on
/// <c>Views/Deck/DeckAnalysis.cshtml</c> (Phase 77, SCORE-01/04). Renders the real Razor view
/// through <see cref="IRazorViewEngine"/> so the OFF invariant (no <c>chatgpt-score</c> markup) and
/// the scored state are enforced in CI — a source-text scan cannot distinguish the two states
/// because the markup literal always exists in the .cshtml. The excision-equality test proves the
/// OFF path leaks no surrounding-markup drift (the only difference is the contiguous score block
/// between the Analysis Summary heading and the per-category breakdown).
/// </summary>
public sealed class DeckAnalysisScoreViewTests
{
    private const string FixedScoreJson =
        "{\"PowerBand\":4,\"SpeedBand\":3,\"ControlBand\":4,\"ConsistencyBand\":3}";

    [Fact]
    public async Task ScoreNull_RendersNoScoreMarkup()
    {
        // True flag-OFF state: no score, no round-trip field -> byte-identical to baseline.
        string html = await RenderAsync(score: null, scoreJson: string.Empty);

        Assert.DoesNotContain("chatgpt-score", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScoreNull_MarkupEqualsScoredMinusScoreBlock()
    {
        // Both renders share an identical Request (same ScoreJson hidden field, same
        // AnalysisResponse), so the ONLY permitted difference is the contiguous score block
        // between "<h3>Analysis Summary</h3>" and the per-category "<div class=\"stack\">".
        // The antiforgery token is randomized per render; neutralize it so the only real
        // difference between the two pages is the score block (not the CSRF nonce).
        string nullHtml = NeutralizeAntiforgery(await RenderAsync(score: null, scoreJson: FixedScoreJson));
        string scoredHtml = NeutralizeAntiforgery(await RenderAsync(score: BuildScore(), scoreJson: FixedScoreJson));

        var (nullPrefix, nullSuffix, nullMiddle) = SplitAroundScoreBlock(nullHtml);
        var (scoredPrefix, scoredSuffix, scoredMiddle) = SplitAroundScoreBlock(scoredHtml);

        // No upstream drift (the hidden ScoreJson field lives in the prefix and must match).
        Assert.Equal(nullPrefix, scoredPrefix);
        // No downstream drift (the existing Overview/Strengths breakdown must be untouched).
        Assert.Equal(nullSuffix, scoredSuffix);
        // The excised region is whitespace-only when OFF and carries the grid when scored.
        Assert.True(string.IsNullOrWhiteSpace(nullMiddle), "OFF middle should be whitespace-only.");
        Assert.Contains("chatgpt-score-grid", scoredMiddle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScorePresent_RendersGridAllAxesAndCrossCheck()
    {
        string html = await RenderAsync(score: BuildScore(), scoreJson: FixedScoreJson);

        Assert.Contains("chatgpt-score-grid", html, StringComparison.Ordinal);
        Assert.Contains("POWER", html, StringComparison.Ordinal);
        Assert.Contains("SPEED", html, StringComparison.Ordinal);
        Assert.Contains("CONTROL", html, StringComparison.Ordinal);
        Assert.Contains("CONSISTENCY", html, StringComparison.Ordinal);
        Assert.Contains("chatgpt-score-crosscheck", html, StringComparison.Ordinal);
        Assert.Contains("CROSS-CHECK", html, StringComparison.Ordinal);
    }

    private static DeckMultiAxisScore BuildScore() => new(
        PowerBand: 4,
        SpeedBand: 3,
        ControlBand: 4,
        ConsistencyBand: 3,
        PowerRationale: new DeckScoreRationale("4 Game Changers, 2 two-card combos, 9 fast-mana sources"),
        SpeedRationale: new DeckScoreRationale("avg MV 2.6, 9 fast-mana, 7 ramp/draw under 3 MV"),
        ControlRationale: new DeckScoreRationale("11 interaction pieces, 4 board wipes, 3 counters"),
        ConsistencyRationale: new DeckScoreRationale("8 tutors, 2 two-card combos, smooth 2.6 curve"),
        BracketNumber: 4,
        BracketCrossCheckText: "Score aligns with the Bracket 4 classification.",
        ScoreAlignsBracket: true);

    private static DeckAnalysisViewModel BuildModel(DeckMultiAxisScore? score, string scoreJson) => new()
    {
        Request = new DeckAnalysisRequest
        {
            TargetAiPlatform = "ChatGPT",
            WorkflowStep = 3,
            ScoreJson = scoreJson,
        },
        AnalysisResponse = new DeckAnalysisResponse
        {
            Format = "Commander",
            Commander = "Test Commander",
        },
        Score = score,
    };

    /// <summary>
    /// Replaces the per-render antiforgery token value with a stable placeholder so two renders of
    /// the same form compare equal outside the deliberately-different score block.
    /// </summary>
    private static string NeutralizeAntiforgery(string html) => Regex.Replace(
        html,
        "(name=\"__RequestVerificationToken\"[^>]*value=\")[^\"]*\"",
        "${1}TOKEN\"");

    /// <summary>
    /// Splits a rendered page around the multi-axis score block insertion point. The prefix is
    /// everything up to and including the Analysis Summary heading; the suffix is everything from
    /// the per-category breakdown onward; the middle is the excised region that holds the score
    /// block when scored and is whitespace-only when OFF.
    /// </summary>
    private static (string Prefix, string Suffix, string Middle) SplitAroundScoreBlock(string html)
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

    private static async Task<string> RenderAsync(DeckMultiAxisScore? score, string scoreJson)
    {
        var model = BuildModel(score, scoreJson);

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
