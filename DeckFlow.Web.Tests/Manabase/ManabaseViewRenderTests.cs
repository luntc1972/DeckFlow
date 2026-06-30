using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DeckFlow.Core.Manabase;
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
/// Render-level guard for the flag-gated tap-analyzer card on <c>Views/Deck/Manabase.cshtml</c>.
/// Renders the real Razor view through <see cref="IRazorViewEngine"/> so the OFF page invariant
/// (no <c>manabase-taplens</c> markup) and the ON card presence are enforced in CI — a source-text
/// scan cannot distinguish the two states because the markup literal always exists in the .cshtml.
/// </summary>
public sealed class ManabaseViewRenderTests
{
    [Fact]
    public async Task OffState_FlagFalse_RendersNoTapAnalyzerMarkup()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: false);

        string html = await RenderManabaseViewAsync(model);

        Assert.DoesNotContain("manabase-taplens", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Untapped sources\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("turn-1 untapped", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnState_FlagTrue_RendersTapAnalyzerCardWithTurn1AndOverallLines()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: true);

        string html = await RenderManabaseViewAsync(model);

        Assert.Contains("manabase-taplens", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Untapped sources\"", html, StringComparison.Ordinal);
        // Turn-1 headline (Turn1UntappedPercent = 76) + its unit microcopy.
        Assert.Contains("turn-1 untapped", html, StringComparison.Ordinal);
        // TAP-02 color-matched pill (overridden 2026-06-28): the explainer must say "of a needed color".
        Assert.Contains(
            "share of games with an untapped source of a needed color on turn 1",
            html,
            StringComparison.Ordinal);
        // Overall row (OverallUntappedPercent = 82) — distinct from the per-color rows (80 / 84).
        Assert.Contains("82% untapped", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OffState_IsByteIdenticalToOnWithTapCardExcised()
    {
        // Codex MED2 — a stronger OFF-path guard than substring-absence. The OFF and ON models are
        // identical except for ShowTapAnalyzer, so the ONLY difference between the two rendered pages
        // must be the contiguous tap-card block. We locate that single differing region via the
        // longest common prefix + suffix of the two outputs:
        //   off = A + offMiddle + B   on = A + onMiddle + B
        // and assert offMiddle is EMPTY (byte-for-byte: OFF must emit nothing — not even a stray space
        // or newline — where the @if lives) while onMiddle is exactly the tap-card <div>…</div>. A
        // whitespace leak when OFF would make offMiddle non-empty and fail this test.
        // The page emits two @Html.AntiForgeryToken() fields whose value is randomized per render;
        // neutralize them so the ONLY remaining difference is the tap card itself.
        string offHtml = NormalizeAntiForgery(await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false)));
        string onHtml = NormalizeAntiForgery(await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: true)));

        int prefix = CommonPrefixLength(offHtml, onHtml);
        int suffix = CommonSuffixLength(offHtml, onHtml, prefix);

        string offMiddle = offHtml[prefix..(offHtml.Length - suffix)];
        string onMiddle = onHtml[prefix..(onHtml.Length - suffix)];

        // OFF emits nothing in the differing region — byte-identical to ON minus the tap card. A
        // stray whitespace/newline leak when the flag is off would make offMiddle non-empty here.
        Assert.Equal(string.Empty, offMiddle);
        // Sanity: the isolated ON region is exactly the tap-analyzer card (modulo its leading indent).
        Assert.StartsWith("<div class=\"manabase-lens manabase-taplens\"", onMiddle.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("</div>", onMiddle.TrimEnd(), StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Untapped sources\"", onMiddle, StringComparison.Ordinal);
    }

    // Replace the randomized __RequestVerificationToken value with a constant so two renders of the
    // same model differ only by intentional content (here: the tap card).
    private static string NormalizeAntiForgery(string html) =>
        Regex.Replace(
            html,
            "(__RequestVerificationToken[^>]*?value=\")[^\"]*(\")",
            "$1NORMALIZED$2");

    private static int CommonPrefixLength(string a, string b)
    {
        int max = Math.Min(a.Length, b.Length);
        int i = 0;
        while (i < max && a[i] == b[i])
        {
            i++;
        }

        return i;
    }

    private static int CommonSuffixLength(string a, string b, int prefix)
    {
        int max = Math.Min(a.Length - prefix, b.Length - prefix);
        int i = 0;
        while (i < max && a[a.Length - 1 - i] == b[b.Length - 1 - i])
        {
            i++;
        }

        return i;
    }

    private static ManabaseViewModel BuildPopulatedModel(bool showTapAnalyzer) => new()
    {
        Request = new ManabaseRequest
        {
            DeckInputSource = DeckInputSource.PasteText,
            Mode = ManabaseMode.Casual,
        },
        InputSummary = "Test deck · 99 cards + 1 commander",
        Report = ReportWithTapAnalysis(),
        ShowTapAnalyzer = showTapAnalyzer,
    };

    /// <summary>A multi-color report carrying populated tap analysis (ColorFindings.Count &gt; 1).</summary>
    private static ManabaseReport ReportWithTapAnalysis() => new()
    {
        ActualLands = 36,
        TargetLands = 37.0,
        ColorFindings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.White,
                ActualSources = 20.0,
                RequiredSources = 18,
                DrivingSpell = "Swords to Plowshares",
                UntappedSources = 16.0,
            },
            new()
            {
                Color = ManaColor.Blue,
                ActualSources = 16.0,
                RequiredSources = 14,
                DrivingSpell = "Counterspell",
                UntappedSources = 13.5,
            },
        },
        Mode = ManabaseMode.Casual,
        Summary = "Mana base looks fine for this test.",
        TapAnalysis = new ManabaseTapAnalysis
        {
            OverallUntappedPercent = 82,
            UntappedSources = 29.5,
            TotalSources = 36.0,
            Turn1UntappedPercent = 76,
            ColorTap = new Dictionary<ManaColor, ColorTapFinding>
            {
                [ManaColor.White] = new() { UntappedSources = 16.0, TotalSources = 20.0, UntappedPercent = 80 },
                [ManaColor.Blue] = new() { UntappedSources = 13.5, TotalSources = 16.0, UntappedPercent = 84 },
            },
        },
    };

    private static async Task<string> RenderManabaseViewAsync(ManabaseViewModel model)
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
        services.AddControllersWithViews().AddApplicationPart(typeof(ManabaseController).Assembly);

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
        var viewResult = viewEngine.FindView(actionContext, "Manabase", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'Manabase' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

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
            ApplicationName = typeof(ManabaseController).Assembly.GetName().Name ?? "DeckFlow.Web",
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
