using System.Diagnostics;
using System.Text.RegularExpressions;
using DeckFlow.Core.Knowledge.CreatorStyleRubric;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.CreatorStyle;
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
/// Render-level coverage for the creator-style critique Razor view.
/// </summary>
public sealed class CreatorStyleViewRenderTests
{
    [Fact]
    public async Task PopulatedModel_RendersPickerTogglePanelsAndPostTarget()
    {
        string html = await RenderCreatorStyleViewAsync(new CreatorStyleViewModel
        {
            Request = new CreatorStyleRequest
            {
                CreatorSlug = "salubrious-snail",
                DeckInputSource = DeckInputSource.PublicUrl,
                DeckUrl = "https://moxfield.com/decks/test",
            },
            AvailableCreators =
            [
                new CreatorStyleViewModel.CreatorPickerOption
                {
                    Slug = "salubrious-snail",
                    DisplayLabel = "Salubrious Snail — 39 decks · 12 videos",
                },
            ],
            Result = CreatePacketResult(),
        });

        Assert.Contains("action=\"/creator-style\"", html, StringComparison.Ordinal);
        Assert.Contains("<select id=\"creator-style-creator\" name=\"CreatorSlug\" data-df-select>", html, StringComparison.Ordinal);
        Assert.Contains("creator-style-creator", html, StringComparison.Ordinal);
        Assert.Contains("39 decks", html, StringComparison.Ordinal);
        Assert.Contains("12 videos", html, StringComparison.Ordinal);
        Assert.Contains("id=\"creator-style-input-source\"", html, StringComparison.Ordinal);
        Assert.Contains("data-sync-panel=\"creator-style-deck-url\"", html, StringComparison.Ordinal);
        Assert.Contains("data-sync-panel=\"creator-style-deck-text\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyModel_RendersInfoBannerAndNoForm()
    {
        string html = await RenderCreatorStyleViewAsync(new CreatorStyleViewModel
        {
            NoProfilesLoaded = true,
        });

        Assert.Contains("No creator profiles loaded yet.", html, StringComparison.Ordinal);
        Assert.Contains("This tool needs at least one creator profile before it can run. Check back soon.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("form action=\"/creator-style\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PopulatedResult_RendersVerdictStripWithMappedChipModifiers()
    {
        string html = await RenderCreatorStyleViewAsync(new CreatorStyleViewModel
        {
            Request = new CreatorStyleRequest
            {
                CreatorSlug = "salubrious-snail",
            },
            AvailableCreators =
            [
                new CreatorStyleViewModel.CreatorPickerOption
                {
                    Slug = "salubrious-snail",
                    DisplayLabel = "Salubrious Snail — 39 decks · 12 videos",
                },
            ],
            Result = CreatePacketResult(
                exemplars:
                [
                    new CreatorStyleExemplarDeck
                    {
                        DeckId = "deck-1",
                        ConfidenceMarker = "high",
                        CardNames = ["Sol Ring"],
                    },
                ],
                metricScores:
                [
                    MetricScore("category_ratio:ramp", "on-target"),
                    MetricScore("category_ratio:draw", "over"),
                    MetricScore("category_ratio:interaction", "under"),
                    MetricScore("category_ratio:flex", "insufficient"),
                ]),
        });

        Assert.Contains("<div class=\"toolbar\">", html, StringComparison.Ordinal);
        Assert.Contains("manabase-chip manabase-chip--good", html, StringComparison.Ordinal);
        Assert.Contains("manabase-chip manabase-chip--ok", html, StringComparison.Ordinal);
        Assert.Contains("manabase-chip manabase-chip--neutral", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PopulatedResult_UsesPresentableExemplarFallbackInsteadOfDeckId()
    {
        string html = await RenderCreatorStyleViewAsync(new CreatorStyleViewModel
        {
            Request = new CreatorStyleRequest
            {
                CreatorSlug = "salubrious-snail",
            },
            AvailableCreators =
            [
                new CreatorStyleViewModel.CreatorPickerOption
                {
                    Slug = "salubrious-snail",
                    DisplayLabel = "Salubrious Snail — 39 decks · 12 videos",
                },
            ],
            Result = CreatePacketResult(
                exemplars:
                [
                    new CreatorStyleExemplarDeck
                    {
                        DeckId = "internal-deck-id-123",
                        ConfidenceMarker = "high",
                        CardNames = ["Sol Ring"],
                    },
                ]),
        });

        Assert.Contains("Exemplars: Salubrious Snail deck 1", html, StringComparison.Ordinal);
        Assert.DoesNotContain("internal-deck-id-123", html, StringComparison.Ordinal);
    }

    private static CreatorStylePacketResult CreatePacketResult(
        IReadOnlyList<CreatorStyleExemplarDeck>? exemplars = null,
        IReadOnlyList<RubricMetricScore>? metricScores = null) => new()
        {
            ArtifactText = "packet",
            RubricScores = new RubricScoreResult
            {
                CreatorSlug = "salubrious-snail",
                MetricScores = metricScores ?? [MetricScore("category_ratio:ramp", "under")],
            },
            Exemplars = exemplars ??
        [
            new CreatorStyleExemplarDeck
            {
                DeckId = "deck-1",
                ConfidenceMarker = "high",
                CardNames = ["Sol Ring"],
            },
        ],
            ValidatedWhitelist = ["Sol Ring"],
            ValidatedComboCards = ["Dockside Extortionist"],
            GroundingDegraded = false,
        };

    private static RubricMetricScore MetricScore(string metric, string verdict) => new()
    {
        Metric = metric,
        TargetValue = 12,
        SubmittedValue = 10,
        Delta = -2,
        Weight = 1,
        Verdict = verdict,
        Confidence = "high",
    };

    private static string NormalizeAntiForgery(string html) => Regex.Replace(
        html,
        "(name=\"__RequestVerificationToken\"[^>]*value=\")[^\"]*\"",
        "${1}TOKEN\"");

    private static async Task<string> RenderCreatorStyleViewAsync(CreatorStyleViewModel model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(serviceProvider => serviceProvider.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(serviceProvider => serviceProvider.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<DeckFlow.Web.Services.Tools.IToolRegistry, DeckFlow.Web.Services.Tools.ToolRegistry>();
        services.AddSingleton<DeckFlow.Web.Services.FeatureFlags.IFeatureFlagCache>(new FakeFeatureFlagCache());
        services.AddSingleton<IOptions<AiPlatformOptions>>(Options.Create(new AiPlatformOptions()));
        services.AddControllersWithViews().AddApplicationPart(typeof(CreatorStyleController).Assembly);

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
        var viewResult = viewEngine.FindView(actionContext, "CreatorStyle", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'CreatorStyle' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

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
        return NormalizeAntiForgery(writer.ToString());
    }

    private static IWebHostEnvironment CreateHostingEnvironment()
    {
        var contentRoot = AppContext.BaseDirectory;
        var fileProvider = new NullFileProvider();
        return new TestWebHostEnvironment
        {
            ApplicationName = typeof(CreatorStyleController).Assembly.GetName().Name ?? "DeckFlow.Web",
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
