using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
/// Render-level guard for the flag-gated stale-primer banner on
/// <c>Views/Deck/DeckPrimer.cshtml</c>. Renders the real Razor view so flag-OFF output can be
/// compared byte-for-byte after neutralizing the randomized antiforgery token.
/// </summary>
public sealed class DeckPrimerBannerRenderTests
{
    [Fact]
    public async Task FlagOff_RendersNoStaleBannerOrGeneratedPrimerHashField()
    {
        string html = await RenderDeckPrimerViewAsync(BuildModel(
            staleDetectionEnabled: false,
            generatedPrimerHash: "hash-123",
            isStale: true,
            changedCardCount: 3,
            primerPromptText: "Existing primer text."));

        Assert.DoesNotContain("deck-restored-notice--stale", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"GeneratedPrimerHash\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FlagOnFreshWithPrimer_RendersHiddenHashFieldButNoStaleBanner()
    {
        string html = await RenderDeckPrimerViewAsync(BuildModel(
            staleDetectionEnabled: true,
            generatedPrimerHash: "hash-123",
            isStale: false,
            changedCardCount: null,
            primerPromptText: "Existing primer text."));

        Assert.Contains("name=\"GeneratedPrimerHash\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("deck-restored-notice--stale", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FlagOnStaleWithPrimerAndMultipleChangedCards_RendersStatusBanner()
    {
        string html = await RenderDeckPrimerViewAsync(BuildModel(
            staleDetectionEnabled: true,
            generatedPrimerHash: "hash-123",
            isStale: true,
            changedCardCount: 3,
            primerPromptText: "Existing primer text."));

        Assert.Contains("3 cards differ", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
        Assert.Contains("Status:", html, StringComparison.Ordinal);
        Assert.Contains("Regenerate primer", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FlagOnStaleWithPrimerAndOneChangedCard_RendersSingularMessage()
    {
        string html = await RenderDeckPrimerViewAsync(BuildModel(
            staleDetectionEnabled: true,
            generatedPrimerHash: "hash-123",
            isStale: true,
            changedCardCount: 1,
            primerPromptText: "Existing primer text."));

        Assert.Contains("1 card differs", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FlagOnStaleWithPrimerAndUnknownChangedCount_RendersCountSuppressedMessage()
    {
        string html = await RenderDeckPrimerViewAsync(BuildModel(
            staleDetectionEnabled: true,
            generatedPrimerHash: "hash-123",
            isStale: true,
            changedCardCount: null,
            primerPromptText: "Existing primer text."));

        Assert.Contains(
            "Deck changed since this primer was generated. Regenerate to refresh the primer.",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain(" cards differ", html, StringComparison.Ordinal);
        Assert.DoesNotContain(" card differs", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FlagOnStaleWithoutPrimer_RendersNoStaleBanner()
    {
        string html = await RenderDeckPrimerViewAsync(BuildModel(
            staleDetectionEnabled: true,
            generatedPrimerHash: "hash-123",
            isStale: true,
            changedCardCount: 3,
            primerPromptText: string.Empty));

        Assert.DoesNotContain("deck-restored-notice--stale", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FlagOff_IsByteIdenticalToBaselineModelAfterAntiforgeryNeutralization()
    {
        string baselineHtml = NeutralizeAntiforgery(await RenderDeckPrimerViewAsync(BuildModel(
            staleDetectionEnabled: false,
            generatedPrimerHash: null,
            isStale: false,
            changedCardCount: null,
            primerPromptText: "Existing primer text.")));
        string flagOffHtml = NeutralizeAntiforgery(await RenderDeckPrimerViewAsync(BuildModel(
            staleDetectionEnabled: false,
            generatedPrimerHash: "hash-123",
            isStale: true,
            changedCardCount: 3,
            primerPromptText: "Existing primer text.")));

        Assert.Equal(baselineHtml, flagOffHtml);
    }

    [Fact]
    public async Task DownloadButton_RendersPromptDownloadMarker()
    {
        string html = await RenderDeckPrimerViewAsync(BuildModel(
            staleDetectionEnabled: true,
            generatedPrimerHash: "hash-123",
            isStale: false,
            changedCardCount: null,
            primerPromptText: "Existing primer text."));

        Assert.Contains("prompt-sticky-download__button", html, StringComparison.Ordinal);
        Assert.Contains("data-prompt-download-submit", html, StringComparison.Ordinal);
        // coverage note: Card Lookup shares the same markup-only submit-button marker change, but
        // lacks an existing equivalent render harness in this test project.
    }

    private static DeckPrimerViewModel BuildModel(
        bool staleDetectionEnabled,
        string? generatedPrimerHash,
        bool isStale,
        int? changedCardCount,
        string primerPromptText) => new()
        {
            Request = new DeckPrimerRequest
            {
                DeckInputSource = DeckInputSource.PasteText,
                DeckText = "1 Sol Ring",
                TargetAiPlatform = "ChatGPT",
                TargetCommanderBracket = "Optimized",
            },
            InputSummary = "Test deck · 99 cards + 1 commander",
            SuggestedChatTitle = "Test Commander Primer",
            PrimerPromptText = primerPromptText,
            StaleDetectionEnabled = staleDetectionEnabled,
            GeneratedPrimerHash = generatedPrimerHash,
            IsStale = isStale,
            ChangedCardCount = changedCardCount,
        };

    private static string NeutralizeAntiforgery(string html) => Regex.Replace(
        html,
        "(name=\"__RequestVerificationToken\"[^>]*value=\")[^\"]*\"",
        "${1}TOKEN\"");

    private static async Task<string> RenderDeckPrimerViewAsync(DeckPrimerViewModel model)
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
        services.AddControllersWithViews().AddApplicationPart(typeof(DeckPrimerController).Assembly);

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
        var viewResult = viewEngine.FindView(actionContext, "DeckPrimer", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'DeckPrimer' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

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
            ApplicationName = typeof(DeckPrimerController).Assembly.GetName().Name ?? "DeckFlow.Web",
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
