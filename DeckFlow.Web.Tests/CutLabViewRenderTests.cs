using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
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

public sealed class CutLabViewRenderTests
{
    [Fact]
    public async Task ResultView_RendersReservedPlanStepAsIncompleteAndUnavailable()
    {
        string html = await RenderAsync(BuildTwinBadgeModel(
            cardTextByCardName: new Dictionary<string, CutLabCardTextView>(StringComparer.OrdinalIgnoreCase)));

        int planTabEnd = html.IndexOf("aria-label=\"Plan\"", StringComparison.Ordinal);
        Assert.True(planTabEnd >= 0, "The Plan workflow tab should render.");
        string planTab = html.Substring(Math.Max(0, planTabEnd - 300), 300);

        Assert.Contains("aria-disabled=\"true\"", planTab, StringComparison.Ordinal);
        Assert.DoesNotContain("is-complete", planTab, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResultView_RendersLockPoolStickySummaryWithDistinctTargets()
    {
        var model = new CutLabViewModel
        {
            ActiveTab = DeckPageTab.CutLab,
            HasResult = true,
            IsLegal = true,
            PoolStatusText = "142 cards in pool · 57 locked",
            BoardCounts = new BoardCounts
            {
                MainboardCount = 99,
                SideboardCount = 42,
                MaybeboardCount = 11,
            },
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = "Commander",
                    Quantity = 1,
                    TypeLine = "Legendary Creature",
                    IsCommander = true,
                    IsLocked = true,
                },
            ],
            RoleListByCardName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Commander"] = "Commander",
            },
            RoleKeysByCardName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Commander"] = "commander",
            },
        };

        string html = await RenderAsync(model);

        Assert.Contains("cutlab-sticky-bar cutlab-sticky-bar--pool", html, StringComparison.Ordinal);
        Assert.Contains("data-cut-lab-pool-sticky-count", html, StringComparison.Ordinal);
        Assert.Contains("data-cut-lab-pool-sticky-breakdown", html, StringComparison.Ordinal);
        Assert.Contains("142 cards in pool", html, StringComparison.Ordinal);
        Assert.Contains("57 locked", html, StringComparison.Ordinal);
        Assert.Contains("Main 99", html, StringComparison.Ordinal);
        Assert.Contains("Sideboard 42", html, StringComparison.Ordinal);
        Assert.Contains("Considering/Maybe 11", html, StringComparison.Ordinal);
        Assert.Contains("data-cut-lab-lock-count", html, StringComparison.Ordinal);
        Assert.Contains("Lock a card to protect it from future cuts.", html, StringComparison.Ordinal);
        Assert.Contains("Current (as of your last recalculation)", html, StringComparison.Ordinal);
        Assert.Contains("Re-run rounds 1 &amp; 2 — your accepted cuts are kept", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResultView_RendersLockedTunerRowWithBothStepperButtonsDisabled()
    {
        var model = new CutLabViewModel
        {
            ActiveTab = DeckPageTab.CutLab,
            HasResult = true,
            IsLegal = true,
            Request = new CutLabRequest
            {
                SelectedCommander = "Commander",
                Bracket = 3,
                PlayExperience = "Focused",
            },
            StickyBar = new CutLabStickyBarView
            {
                HasStickyBar = true,
                LockedCount = 2,
                CurrentCount = 99,
                RoundLabel = "Round 1",
                CardsRemainingToCut = 1,
                CutsAcceptedCount = 0,
            },
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = "Commander",
                    Quantity = 1,
                    TypeLine = "Legendary Creature",
                    IsCommander = true,
                    IsLocked = true,
                },
                new CutLabPoolCard
                {
                    Name = "Relentless Rats",
                    Quantity = 2,
                    TypeLine = "Creature — Rat",
                    IsLocked = true,
                },
            ],
            RoleListByCardName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Commander"] = "Commander",
                ["Relentless Rats"] = "Engines",
            },
            RoleKeysByCardName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Commander"] = "commander",
                ["Relentless Rats"] = "engines",
            },
            WorkingListRows =
            [
                new CutLabTunableRowView
                {
                    Name = "Relentless Rats",
                    RoleLabel = "Engines",
                    CurrentQuantity = 2,
                    IsLegalMultiple = true,
                    LegalMax = 10,
                    IsLocked = true,
                },
            ],
        };

        string html = await RenderAsync(model);
        string tunerRowHtml = ExtractRowMarkup(html, "Relentless Rats");

        Assert.Contains("data-cut-lab-tuner-row=\"Relentless Rats\"", tunerRowHtml, StringComparison.Ordinal);
        Assert.Contains("title=\"Relentless Rats is locked - unlock it to adjust quantity\"", tunerRowHtml, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Relentless Rats is locked - unlock it to adjust quantity\"", tunerRowHtml, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(tunerRowHtml, "data-cut-lab-card=\"Relentless Rats\""));
        Assert.Equal(2, CountOccurrences(tunerRowHtml, "title=\"Relentless Rats is locked - unlock it to adjust quantity\""));
    }

    [Fact]
    public async Task RenderAsync_NormalizedComboBadgeKey_RendersComboBadgeForMixedCasePunctuatedTwinName()
    {
        var model = BuildTwinBadgeModel(
            cardTextByCardName: new Dictionary<string, CutLabCardTextView>(StringComparer.OrdinalIgnoreCase));

        string html = await RenderAsync(model);

        Assert.Contains("cutlab-combo-badge", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_NormalizedComboBadgeKey_AttachesComboContextToRawCardTextEntry()
    {
        var model = BuildTwinBadgeModel(
            cardTextByCardName: new Dictionary<string, CutLabCardTextView>(StringComparer.OrdinalIgnoreCase)
            {
                [HeliodRawName] = new CutLabCardTextView { OracleText = "Whenever you gain life, put a counter." },
            });

        string html = await RenderAsync(model);

        Assert.Contains(
            "\"Heliod, Sun-Crowned\":{\"oracleText\":\"Whenever you gain life, put a counter.\",\"comboContext\":\"Infinite damage\"}",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain($"\"{HeliodNormalizedName}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_NormalizedComboBadgeKey_AttachesComboContextToRawPoolCardWithoutCardText()
    {
        var model = BuildTwinBadgeModel(
            cardTextByCardName: new Dictionary<string, CutLabCardTextView>(StringComparer.OrdinalIgnoreCase));

        string html = await RenderAsync(model);

        Assert.Contains(
            "\"Heliod, Sun-Crowned\":{\"comboContext\":\"Infinite damage\"}",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain($"\"{HeliodNormalizedName}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_FunctionalTwinsSection_RendersTheHelpNoteCopyVerbatim()
    {
        var model = BuildTwinBadgeModel(
            cardTextByCardName: new Dictionary<string, CutLabCardTextView>(StringComparer.OrdinalIgnoreCase));

        string html = await RenderAsync(model);

        // Why: the AJAX re-render in wwwroot/ts/cut-lab.ts emits this same sentence, and
        // cut-lab-structural-cardtext.test.ts pins the TypeScript copy. Only the TypeScript side was
        // pinned, so mutating the Razor string alone left every C# and vitest test green and the two
        // copies silently drifted apart. This assertion pins the Razor side, so a one-sided edit to
        // EITHER file now fails. Assert the full sentence, not a prefix: a prefix check would not
        // catch drift in the tail (the "combo-protected" clause).
        Assert.Contains(
            "<p class=\"manabase-help\">These cards fill the same role at the same mana value with the same card type, so they compete for one slot. The costliest group is listed first, and a card here may also be combo-protected.</p>",
            html,
            StringComparison.Ordinal);
    }

    private const string HeliodRawName = "Heliod, Sun-Crowned";

    private static readonly string HeliodNormalizedName = CutLabCardNames.Normalize(HeliodRawName);

    private static CutLabViewModel BuildTwinBadgeModel(IReadOnlyDictionary<string, CutLabCardTextView> cardTextByCardName)
    {
        var twinFinding = new CutLabFindingView
        {
            Kind = CutLabFindingKind.FunctionalTwins,
            Heading = "Functional twins",
            Lead = "Three enchantments share mana value 3 in wincons.",
            Evidence = [HeliodRawName],
        };

        return new CutLabViewModel
        {
            ActiveTab = DeckPageTab.CutLab,
            HasResult = true,
            IsLegal = true,
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = HeliodRawName,
                    Quantity = 1,
                    TypeLine = "Legendary Enchantment Creature — God",
                },
            ],
            Findings = [twinFinding],
            FindingGroups =
            [
                new CutLabFindingGroupView
                {
                    Kind = CutLabFindingKind.FunctionalTwins,
                    Heading = "Functional twins",
                    Items = [twinFinding],
                },
            ],
            CardTextByCardName = cardTextByCardName,
            ComboBadgeByCardName = new Dictionary<string, CutLabComboBadgeView>(StringComparer.Ordinal)
            {
                [HeliodNormalizedName] = new CutLabComboBadgeView
                {
                    BadgeState = ComboBadgeState.CompletePiece,
                    Context = "Infinite damage",
                },
            },
        };
    }

    private static async Task<string> RenderAsync(CutLabViewModel model)
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
        services.AddControllersWithViews().AddApplicationPart(typeof(CutLabController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "Deck" })),
            new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "CutLab", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'CutLab' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

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
            ApplicationName = typeof(CutLabController).Assembly.GetName().Name ?? "DeckFlow.Web",
            ContentRootPath = contentRoot,
            ContentRootFileProvider = fileProvider,
            EnvironmentName = Environments.Development,
            WebRootPath = contentRoot,
            WebRootFileProvider = fileProvider,
        };
    }

    private static int CountOccurrences(string html, string value)
    {
        int count = 0;
        int startIndex = 0;

        while ((startIndex = html.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }

    private static string ExtractRowMarkup(string html, string cardName)
    {
        string marker = $"data-cut-lab-tuner-row=\"{cardName}\"";
        int rowStart = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(rowStart >= 0, $"Could not find tuner row for '{cardName}'.");

        rowStart = html.LastIndexOf("<tr", rowStart, StringComparison.Ordinal);
        Assert.True(rowStart >= 0, $"Could not find opening <tr> for '{cardName}'.");

        int rowEnd = html.IndexOf("</tr>", rowStart, StringComparison.Ordinal);
        Assert.True(rowEnd >= 0, $"Could not find closing </tr> for '{cardName}'.");

        return html.Substring(rowStart, rowEnd + "</tr>".Length - rowStart);
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
