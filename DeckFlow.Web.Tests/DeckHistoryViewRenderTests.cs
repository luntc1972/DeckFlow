using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DeckFlow.Core.History;
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
/// Render-level guard for <c>Views/Deck/DeckHistory.cshtml</c> so compare/download form cleanup
/// stays intact after refactors.
/// </summary>
public sealed class DeckHistoryViewRenderTests
{
    [Fact]
    public async Task ResultView_RendersSuccessHintInsideBanner_AndPlacesDownloadPanelBetweenTimelineAndCompare()
    {
        var model = new DeckHistoryViewModel
        {
            ActiveTab = DeckPageTab.DeckHistory,
            HasResult = true,
            Request = new DeckHistoryRequest
            {
                DeckInputSource = DeckInputSource.PasteText,
                DeckText = "1 Sol Ring",
                DeckName = "Zur Logbook",
                TargetAiPlatform = "ChatGPT",
            },
            HistoryJson = """{"format":"deckflow/history/v1"}""",
            SuccessMessage = "Version 2 added.",
            Warnings = ["Version ids were repaired (renumbered in date order)."],
            PairOlderId = 1,
            PairNewerId = 2,
            PairDiff = new VersionDiff(
                [new SnapshotCard { Name = "Mystic Remora", Qty = 1 }],
                [new SnapshotCard { Name = "Brainstorm", Qty = 1 }],
                []),
            TimelineRows =
            [
                new TimelineRow
                {
                    Id = 2,
                    Date = DateTimeOffset.Parse("2026-07-16T00:00:00Z"),
                    Label = "v2",
                    Notes = "Added Remora",
                    CardCount = 100,
                    AddsCount = 1,
                    CutsCount = 1,
                },
                new TimelineRow
                {
                    Id = 1,
                    Date = DateTimeOffset.Parse("2026-07-09T00:00:00Z"),
                    Label = "v1",
                    Notes = "Initial list",
                    CardCount = 100,
                    AddsCount = 0,
                    CutsCount = 0,
                },
            ],
        };

        string html = NeutralizeAntiforgery(await RenderAsync(model));
        int compareFormStart = html.IndexOf("id=\"deck-history-compare-form\"", StringComparison.Ordinal);
        Assert.True(compareFormStart >= 0, "compare form should render");
        int compareFormEnd = html.IndexOf("</form>", compareFormStart, StringComparison.Ordinal);
        Assert.True(compareFormEnd > compareFormStart, "compare form should close");
        string compareFormHtml = html.Substring(compareFormStart, compareFormEnd - compareFormStart);
        int successBannerStart = html.IndexOf("class=\"success-banner\"", StringComparison.Ordinal);
        Assert.True(successBannerStart >= 0, "success banner should render");
        int successBannerEnd = html.IndexOf("</div>", successBannerStart, StringComparison.Ordinal);
        Assert.True(successBannerEnd > successBannerStart, "success banner should close");
        string successBannerHtml = html.Substring(successBannerStart, successBannerEnd - successBannerStart);
        int timelineStart = html.IndexOf("<h2>Timeline</h2>", StringComparison.Ordinal);
        int saveHistoryStart = html.IndexOf("<h2>Save your history</h2>", StringComparison.Ordinal);
        int compareStart = html.IndexOf("<h2>Compare versions</h2>", StringComparison.Ordinal);

        Assert.Contains("Version 2 added.", html, StringComparison.Ordinal);
        Assert.Contains(
            "To add the next version: update your deck, import it above, and press Update history again — your history carries forward on this page.",
            successBannerHtml,
            StringComparison.Ordinal);
        Assert.Contains("class=\"warning-banner\"", html, StringComparison.Ordinal);
        Assert.Contains("Version ids were repaired (renumbered in date order).", html, StringComparison.Ordinal);
        Assert.DoesNotContain("history-warnings", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<p class=\"history-empty-compare\">To add the next version: update your deck, import it above, and press Update history again — your history carries forward on this page.</p>",
            html,
            StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(html, "name=\"HistoryJson\"", RegexOptions.CultureInvariant).Count);
        Assert.Contains("id=\"deck-history-compare-form\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"history-compare-controls\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"history-compare-controls__arrow\"", html, StringComparison.Ordinal);
        Assert.Contains("form=\"deck-history-compare-form\"", html, StringComparison.Ordinal);
        Assert.Contains("formaction=\"/deck-history/download\"", html, StringComparison.Ordinal);
        Assert.True(timelineStart >= 0, "timeline panel should render");
        Assert.True(saveHistoryStart > timelineStart, "download panel should follow timeline");
        Assert.True(compareStart > saveHistoryStart, "compare panel should follow download");
        Assert.DoesNotContain("name=\"DeckInputSource\"", compareFormHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"DeckUrl\"", compareFormHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"DeckText\"", compareFormHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"DeckName\"", compareFormHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Label\"", compareFormHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Notes\"", compareFormHtml, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Older version\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Newer version\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InputForm_RendersDeckHistoryCacheKey_AndBridgeHintOnUrlField()
    {
        var model = new DeckHistoryViewModel
        {
            ActiveTab = DeckPageTab.DeckHistory,
            Request = new DeckHistoryRequest
            {
                DeckInputSource = DeckInputSource.PublicUrl,
                DeckUrl = "https://www.moxfield.com/decks/example",
                DeckName = "Zur Logbook",
                TargetAiPlatform = "ChatGPT",
            },
        };

        string html = NeutralizeAntiforgery(await RenderAsync(model));
        int formStart = html.IndexOf("<form method=\"post\" action=\"/deck-history\"", StringComparison.Ordinal);
        Assert.True(formStart >= 0, "main deck-history form should render");
        int formEnd = html.IndexOf("</form>", formStart, StringComparison.Ordinal);
        Assert.True(formEnd > formStart, "main deck-history form should close");
        string formHtml = html.Substring(formStart, formEnd - formStart);

        Assert.Contains("data-cache-key=\"deck-history\"", formHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"DeckInputSource\"", formHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"DeckUrl\"", formHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"DeckText\"", formHtml, StringComparison.Ordinal);
        Assert.Contains("details class=\"deckflow-bridge-hint\"", formHtml, StringComparison.Ordinal);
        Assert.Contains("DeckFlow Bridge extension", formHtml, StringComparison.Ordinal);
    }

    private static string NeutralizeAntiforgery(string html) => Regex.Replace(
        html,
        "(name=\"__RequestVerificationToken\"[^>]*value=\")[^\"]*\"",
        "${1}TOKEN\"");

    private static async Task<string> RenderAsync(DeckHistoryViewModel model)
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
        services.AddControllersWithViews().AddApplicationPart(typeof(DeckHistoryController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "Deck" })),
            new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "DeckHistory", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'DeckHistory' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

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
            ApplicationName = typeof(DeckHistoryController).Assembly.GetName().Name ?? "DeckFlow.Web",
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
