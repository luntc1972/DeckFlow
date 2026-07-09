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
    public async Task OffState_MulliganFlagFalse_RendersNoMulliganLensMarkup()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: false);

        string html = await RenderManabaseViewAsync(model);

        Assert.DoesNotContain("manabase-mulliganlens", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Opening hand\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("keepable hands", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnState_MulliganFlagTrue_RendersOpeningHandLensCardWithTrackedSpell()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true);

        string html = await RenderManabaseViewAsync(model);

        Assert.Contains("manabase-mulliganlens", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Opening hand\"", html, StringComparison.Ordinal);
        // Keepable-band line (KeepableBand = "high", KeepableHandPercent = 82).
        Assert.Contains("high (~82%)", html, StringComparison.Ordinal);
        Assert.Contains("keepable hands", html, StringComparison.Ordinal);
        // Keep-size process line (Kept7Percent = 55, MulliganTo6Percent = 30, MulliganTo5Percent = 15).
        Assert.Contains("kept 7 ~55%", html, StringComparison.Ordinal);
        // Representative-opener line names the tracked spell, never a generic claim.
        Assert.Contains("Swords to Plowshares castable on curve (turn 1)", html, StringComparison.Ordinal);
        Assert.Contains("workable line", html, StringComparison.Ordinal);
        // Plan-presence line is NOT shown when its own flag is off, even with the opening-hand block on.
        Assert.DoesNotContain("Payoff on curve", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlanPresenceFlagTrue_RendersWithAPlanLineAndRoleBreakdown()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true, showPlanPresence: true);

        string html = await RenderManabaseViewAsync(model);

        // Payoff-led headline (PayoffPercent = 55, PayoffBand = "high") + secondary composite (74%).
        Assert.Contains("Payoff on curve", html, StringComparison.Ordinal);
        Assert.Contains("~55%", html, StringComparison.Ordinal);
        Assert.Contains("~74%", html, StringComparison.Ordinal);
        // Per-role breakdown moves to its own muted sub-line; nonzero roles surfaced, zero-role
        // (Engine) omitted, and the Payoff role is NOT repeated (it is already the headline number).
        Assert.Contains("by role:", html, StringComparison.Ordinal);
        Assert.Contains("tutor/combo ~20%", html, StringComparison.Ordinal);
        Assert.Contains("interaction ~40%", html, StringComparison.Ordinal);
        Assert.DoesNotContain("engine ~", html, StringComparison.Ordinal);
        Assert.DoesNotContain("payoff ~55%", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OffState_IsByteIdenticalToOnWithMulliganCardExcised()
    {
        // Mirrors OffState_IsByteIdenticalToOnWithTapCardExcised for the opening-hand card: the OFF
        // and ON models are identical except for ShowMulliganEval (ShowTapAnalyzer held constant at
        // false so the tap card never appears in either render), so the only difference between the
        // two pages must be the contiguous mulligan-lens block.
        string offHtml = NormalizeAntiForgery(await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: false)));
        string onHtml = NormalizeAntiForgery(await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true)));

        int prefix = CommonPrefixLength(offHtml, onHtml);
        int suffix = CommonSuffixLength(offHtml, onHtml, prefix);

        string offMiddle = offHtml[prefix..(offHtml.Length - suffix)];
        string onMiddle = onHtml[prefix..(onHtml.Length - suffix)];

        // OFF emits nothing in the differing region — byte-identical to ON minus the mulligan card.
        Assert.Equal(string.Empty, offMiddle);
        // Sanity: the isolated ON region is exactly the opening-hand lens card.
        Assert.StartsWith("<div class=\"manabase-lens manabase-mulliganlens\"", onMiddle.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("</div>", onMiddle.TrimEnd(), StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Opening hand\"", onMiddle, StringComparison.Ordinal);
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

    [Fact]
    public async Task Summary_LeadsResultPanel_BeforeTheTwoLensGrid()
    {
        // Verdict-first: the .manabase-summary card (health + lands + biggest fix) must render before
        // the supporting two-lens grid so the answer is read before the evidence.
        string html = await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false));

        int summaryIdx = html.IndexOf("class=\"manabase-summary\"", StringComparison.Ordinal);
        int twoLensIdx = html.IndexOf("manabase-twolens", StringComparison.Ordinal);

        Assert.True(summaryIdx >= 0, "Summary card should render.");
        Assert.True(twoLensIdx >= 0, "Two-lens grid should render for a multi-color report.");
        Assert.True(summaryIdx < twoLensIdx, "Summary must precede the two-lens grid.");
        // The wide color table carries a (mobile-only, CSS-gated) sideways-scroll cue.
        Assert.Contains("manabase-scroll-hint", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BiggestFix_RendersExactlyOnce_InTheSummaryNotBelowTheColorTable()
    {
        // The biggest-fix callout moved into the summary; it must not also render in its old
        // mode-note slot below the color table (no duplication).
        string html = await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false));

        Assert.Single(Regex.Matches(html, "manabase-summary-fix"));
        Assert.DoesNotContain("mode-note\"><strong>Biggest fix", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpeningHandHeadline_UsesSoftHierarchyClass()
    {
        // Hierarchy fix: the opening-hand headline is downweighted vs the cast-rate headline.
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true));

        Assert.Contains("manabase-lens-big--soft", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RampDrawBalancedLine_SuppressedWhenBudgetNotBalanced()
    {
        // Contradiction fix (view side): a draw-light budget reports IsBalanced=false, so the
        // "looks balanced" clause must not render beside a draw-light verdict.
        string balancedHtml = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, rampDrawBudget: Budget(isBalanced: true)));
        Assert.Contains("looks balanced", balancedHtml, StringComparison.Ordinal);

        string unbalancedHtml = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, rampDrawBudget: Budget(isBalanced: false)));
        Assert.DoesNotContain("looks balanced", unbalancedHtml, StringComparison.Ordinal);
        // The count line still renders — the section is never empty.
        Assert.Contains("ramp /", unbalancedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BothLensesShown_RendersReconciliationNoteAndBothTableScrollHints()
    {
        // With a Casual report that carries Castability rows, both the Karsten source-check lens and
        // the simulated cast-rate lens render, so the reconciliation note appears; and both wide
        // tables (color + castability) each carry a scroll hint.
        var model = new ManabaseViewModel
        {
            Request = new ManabaseRequest { DeckInputSource = DeckInputSource.PasteText, Mode = ManabaseMode.Casual },
            InputSummary = "Test deck",
            Report = new ManabaseReport
            {
                ActualLands = 36,
                TargetLands = 37.0,
                Mode = ManabaseMode.Casual,
                Summary = "x",
                ColorFindings = new List<ColorSourceFinding>
                {
                    new() { Color = ManaColor.White, ActualSources = 20.0, RequiredSources = 18, DrivingSpell = "Swords to Plowshares", UntappedSources = 16.0 },
                    new() { Color = ManaColor.Blue, ActualSources = 16.0, RequiredSources = 14, DrivingSpell = "Counterspell", UntappedSources = 13.5 },
                },
                Castability = new List<CardCastability>
                {
                    new() { Name = "Swords to Plowshares", ManaValue = 1, OnCurveTurn = 1, CastPercent = 95, LimitingFactor = "color: White" },
                },
            },
        };

        string html = await RenderManabaseViewAsync(model);

        Assert.Contains("manabase-twolens-note", html, StringComparison.Ordinal);
        Assert.Contains("Read the two together", html, StringComparison.Ordinal);
        int hintCount = Regex.Matches(html, "manabase-scroll-hint").Count;
        Assert.Equal(2, hintCount);
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

    private static ManabaseViewModel BuildPopulatedModel(
        bool showTapAnalyzer,
        bool showMulliganEval = false,
        bool showPlanPresence = false,
        ManabaseRampDrawBudget? rampDrawBudget = null) => new()
        {
            Request = new ManabaseRequest
            {
                DeckInputSource = DeckInputSource.PasteText,
                Mode = ManabaseMode.Casual,
            },
            InputSummary = "Test deck · 99 cards + 1 commander",
            Report = ReportWithTapAnalysis(),
            ShowTapAnalyzer = showTapAnalyzer,
            ShowMulliganEval = showMulliganEval,
            ShowPlanPresence = showPlanPresence,
            RampDrawBudget = rampDrawBudget,
        };

    private static ManabaseRampDrawBudget Budget(bool isBalanced) => new()
    {
        RampCount = 12,
        DrawCount = isBalanced ? 12 : 8,
        OverlapCount = 0,
        Threshold = 4.0,
        ThresholdSource = ManabaseRampDrawThresholdSource.CommanderManaValue,
        TargetRamp = 12,
        TargetDraw = 12,
        IsBalanced = isBalanced,
        IsRampLight = false,
        IsRampHeavy = false,
        RampShort = 0,
        IsDrawLight = !isBalanced,
        DrawShort = isBalanced ? 0 : 4,
    };

    /// <summary>
    /// A multi-color report carrying populated tap analysis (ColorFindings.Count &gt; 1) AND a
    /// populated mulligan evaluation, so the tap and mulligan flags can be toggled independently
    /// against the same fixed report.
    /// </summary>
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
        MulliganEvaluation = new ManabaseMulliganEvaluation
        {
            KeepableHandPercent = 82,
            KeepableBand = "high",
            Kept7Percent = 55,
            MulliganTo6Percent = 30,
            MulliganTo5Percent = 15,
            ColorCount = 2,
            AverageManaValue = 2.8,
            RepresentativeOpeners = new List<OpeningHandSample>
            {
                new()
                {
                    Lands = 3,
                    Colors = 2,
                    RampPieces = 1,
                    OtherCards = 3,
                    KeptCards = 7,
                    Decision = "keep 7",
                    TrackedSpellName = "Swords to Plowshares",
                    TrackedOnCurveTurn = 1,
                    OnCurveCastable = true,
                    HasPlan = true,
                },
            },
            PlanPresence = new ManabasePlanPresence
            {
                PayoffPercent = 55,
                PayoffBand = "high",
                PlanPresencePercent = 74,
                Band = "high",
                RolePercents = new Dictionary<PlanRole, int>
                {
                    [PlanRole.Payoff] = 55,
                    [PlanRole.Engine] = 0,
                    [PlanRole.TutorCombo] = 20,
                    [PlanRole.Interaction] = 40,
                },
                KeepableTrials = 17000,
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
