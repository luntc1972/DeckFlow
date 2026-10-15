using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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
    public async Task StepThreeIntake_RendersEmptyBeforeAnalysis()
    {
        var model = new MetaGapViewModel
        {
            ActiveTab = DeckPageTab.CedhMetaGap,
            Request = new MetaGapRequest { WorkflowStep = 3 },
        };

        string html = await RenderCedhMetaGapViewAsync(model);

        Assert.Contains("<div class=\"cutlab-intake cutlab-intake--empty\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cutlab-intake-summary", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-cut-lab-intake-summary", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StepThreeIntake_IsCollapsedWithChangeActionAfterAnalysis()
    {
        string html = await RenderCedhMetaGapViewAsync(CreateExpandedStepThreeModel());

        Assert.Contains("<details class=\"cutlab-intake\" data-cut-lab-intake-summary>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<details class=\"cutlab-intake\" data-cut-lab-intake-summary open", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"cutlab-intake-summary__commander\">Meta gap analysis ready</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"cutlab-intake-summary__change\">Edit</span>", html, StringComparison.Ordinal);
    }

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
    public async Task GeneratedPromptPanel_CarriesTheResponseSplitTip()
    {
        var model = new MetaGapViewModel
        {
            ActiveTab = DeckPageTab.CedhMetaGap,
            Request = new MetaGapRequest { WorkflowStep = 2 },
            PromptText = "GENERATED META GAP PROMPT",
        };

        string html = await RenderCedhMetaGapViewAsync(model);

        // The tip sits beside the generated prompt box so a user whose AI splits the reply
        // sees the JSON-only recovery move right where they copy the prompt.
        Assert.Contains("data-prompt-split-tip=\"meta_gap\"", html, StringComparison.Ordinal);
        Assert.Contains("Output only the meta_gap JSON in a single response", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SplitTip_AbsentUntilPromptGenerated()
    {
        var model = new MetaGapViewModel
        {
            ActiveTab = DeckPageTab.CedhMetaGap,
            Request = new MetaGapRequest(),
        };

        string html = await RenderCedhMetaGapViewAsync(model);

        Assert.DoesNotContain("data-prompt-split-tip", html, StringComparison.Ordinal);
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

    [Fact]
    public async Task WinLinesDelta_ExcludesBlankMissingLineEntriesFromCount()
    {
        string html = await RenderCedhMetaGapViewAsync(CreatePopulatedStepThreeModel());
        Assert.Contains("&#x2193; 1", html, StringComparison.Ordinal);
        Assert.DoesNotContain("&#x2193; 2", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PopulatedResults_RenderNavigableSectionsAndSingleDownload()
    {
        string html = await RenderCedhMetaGapViewAsync(CreateExpandedStepThreeModel());

        Assert.Single(Regex.Matches(html, "data-prompt-download-submit"));
        Assert.Contains("data-prompt-print", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Meta gap result sections\"", html, StringComparison.Ordinal);
        foreach (string section in new[] { "overview", "comparison", "core-convergence", "recommended-edits" })
        {
            Assert.Contains($"href=\"#meta-gap-{section}\"", html, StringComparison.Ordinal);
            Assert.Contains($"id=\"meta-gap-{section}\"", html, StringComparison.Ordinal);
        }

        Assert.Matches("id=\"meta-gap-comparison\"[^>]*>[\\s\\S]*?Deck vs\\. field", html);
        Assert.Matches("<dl[^>]*>[\\s\\S]*?<dt>Commander</dt>", html);
        Assert.Matches("<details[^>]*open[^>]*>[\\s\\S]*?meta-gap-edit-group__count\">\\(2\\)</span>", html);
        Assert.Matches("<details[^>]*open[^>]*>[\\s\\S]*?meta-gap-edit-group__count\">\\(1\\)</span>", html);
        Assert.Contains("<div class=\"meta-gap-edit-group meta-gap-edit-group--swap\">", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"meta-gap-edit-group meta-gap-edit-group--cut\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<details open class=\"meta-gap-edit-group meta-gap-edit-group--swap\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<details open class=\"meta-gap-edit-group meta-gap-edit-group--cut\">", html, StringComparison.Ordinal);
        Assert.Matches("manabase-chip--good[^>]*>[\\s\\S]*?meta-gap-edit-item__rank\">#1</span> <strong class=\"meta-gap-edit-item__name\">", html);

        Assert.Contains("class=\"meta-gap-edit-item__details\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"meta-gap-edit-item__why\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"meta-gap-edit-item__why-label\">Why:</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PopulatedResults_RenderRecommendedEditsWithSeparateAddAndCutLines()
    {
        string html = await RenderCedhMetaGapViewAsync(CreateExpandedStepThreeModel());

        Assert.Matches(new Regex("meta-gap-edit-item__line\\\">\\s*<span class=\\\"manabase-chip manabase-chip--good\\\">Add</span>\\s*<strong>Rhystic Study</strong>", RegexOptions.Singleline), html);
        Assert.Matches(new Regex("meta-gap-edit-item__line\\\">\\s*<span class=\\\"manabase-chip manabase-chip--low\\\">Cut</span>\\s*Sign in Blood", RegexOptions.Singleline), html);
        Assert.Matches(new Regex("meta-gap-edit-item__why\\\">\\s*<span class=\\\"meta-gap-edit-item__why-label\\\">Why:</span>\\s*Efficient advantage.", RegexOptions.Singleline), html);
        Assert.Contains("meta-gap-edit-item__header", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PopulatedBaseResults_OmitsRecommendedEditSection()
    {
        string html = await RenderCedhMetaGapViewAsync(CreatePopulatedStepThreeModel());

        Assert.DoesNotContain("href=\"#meta-gap-recommended-edits\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"meta-gap-recommended-edits\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#meta-gap-core-convergence\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"meta-gap-core-convergence\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PopulatedStepThreeAnalysis_RendersMirrorSectionsAndDetails()
    {
        string html = await RenderCedhMetaGapViewAsync(CreatePopulatedStepThreeModel());

        Assert.Contains("Core Convergence", html, StringComparison.Ordinal);
        Assert.Contains("Sol Ring", html, StringComparison.Ordinal);
        Assert.Contains("Below average", html, StringComparison.Ordinal);
        Assert.Contains("Needs more interaction", html, StringComparison.Ordinal);
        Assert.Contains("Missing: Thassa&#x27;s Oracle wincon", html, StringComparison.Ordinal);
    }

    private static MetaGapViewModel CreatePopulatedStepThreeModel()
    {
        return new MetaGapViewModel
        {
            ActiveTab = DeckPageTab.CedhMetaGap,
            Request = new MetaGapRequest { WorkflowStep = 3 },
            AnalysisResponse = new MetaGapResponse
            {
                MetaGap = new MetaGapData
                {
                    WinLines = new WinLines
                    {
                        MyDeck = new WinLineSet { Primary = "Dockside loop" },
                        RefConsensus = new WinLineSet { Primary = "Oracle consult" },
                        MissingLines = new[] { "", "Thassa's Oracle wincon" },
                    },
                    Interaction = new Interaction
                    {
                        MyCount = 4,
                        RefAvgCount = 7.0,
                        Verdict = "Below average",
                        Detail = "Needs more interaction",
                    },
                    CoreConvergence = new[]
                    {
                        new CoreConvergenceCard { Card = "Sol Ring", Role = "Ramp" },
                    },
                },
            },
        };
    }

    private static MetaGapViewModel CreateExpandedStepThreeModel()
    {
        return new MetaGapViewModel
        {
            ActiveTab = DeckPageTab.CedhMetaGap,
            Request = new MetaGapRequest { WorkflowStep = 3 },
            AnalysisResponse = new MetaGapResponse
            {
                MetaGap = new MetaGapData
                {
                    Commander = "Tymna the Weaver",
                    RefDeckCount = 12,
                    CoreConvergence = new[] { new CoreConvergenceCard { Card = "Sol Ring", Role = "Ramp" } },
                    Top10Adds = new[]
                    {
                        new TopAdd { Card = "Rhystic Study", Replaces = "Sign in Blood", Role = "Draw", Why = "Efficient advantage." },
                        new TopAdd { Card = "Mystic Remora", Role = "Draw", Why = "Early advantage." },
                    },
                    Top10Cuts = new[] { new TopCut { Card = "Sign in Blood", Role = "Draw", Why = "Too slow." } },
                    MissingStaples = new[]
                    {
                        new MissingStaple { Card = "Force of Will", Role = "Interaction", RefCount = 12, Priority = 3, Why = "Free protection." },
                        new MissingStaple { Card = "Mana Crypt", Role = "Ramp", RefCount = 15, Priority = 1, Why = "Fast mana." },
                    },
                    PotentialCuts = new[] { new PotentialCut { Card = "Commander's Sphere", Role = "Ramp", RefCount = 0, Priority = 1, Why = "Too slow." } },
                },
            },
        };
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
