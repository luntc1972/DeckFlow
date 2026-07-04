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
/// Render-level guard for the flag-gated interaction-audit block on
/// <c>Views/Deck/DeckAnalysis.cshtml</c> (Phase 79, INTERACT-01/03). Renders the real Razor view
/// through <see cref="IRazorViewEngine"/> so the OFF invariant (no <c>interaction-audit</c> markup and
/// no hidden <c>InteractionAuditJson</c> field) and the populated state are enforced in CI.
/// </summary>
public sealed class DeckAnalysisInteractionAuditViewTests
{
    private static readonly InteractionAudit FixedInteractionAudit = BuildAudit();
    private static readonly string FixedInteractionAuditJson = JsonSerializer.Serialize(FixedInteractionAudit);

    [Fact]
    public async Task InteractionAuditNull_RendersNoInteractionMarkup()
    {
        string html = await RenderAsync(interactionAudit: null, interactionAuditJson: string.Empty);

        Assert.DoesNotContain("interaction-audit", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"InteractionAuditJson\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InteractionAuditNull_MarkupEqualsPopulatedMinusInteractionBlock()
    {
        string nullHtml = NeutralizeAntiforgery(await RenderAsync(interactionAudit: null, interactionAuditJson: FixedInteractionAuditJson));
        string populatedHtml = NeutralizeAntiforgery(await RenderAsync(FixedInteractionAudit, FixedInteractionAuditJson));

        var (nullPrefix, nullSuffix, nullMiddle) = SplitAroundInteractionBlock(nullHtml);
        var (populatedPrefix, populatedSuffix, populatedMiddle) = SplitAroundInteractionBlock(populatedHtml);

        Assert.Equal(nullPrefix, populatedPrefix);
        Assert.Equal(nullSuffix, populatedSuffix);
        Assert.True(string.IsNullOrWhiteSpace(nullMiddle), "OFF middle should be whitespace-only.");
        Assert.Contains("interaction-audit-bucket", populatedMiddle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InteractionAuditPresent_RendersAllFiveBuckets()
    {
        string html = await RenderAsync(FixedInteractionAudit, FixedInteractionAuditJson);

        Assert.Contains("interaction-audit-bucket", html, StringComparison.Ordinal);
        Assert.Contains("Targeted removal", html, StringComparison.Ordinal);
        Assert.Contains("Board wipes", html, StringComparison.Ordinal);
        Assert.Contains("Counterspells", html, StringComparison.Ordinal);
        Assert.Contains("Protection or recursion", html, StringComparison.Ordinal);
        Assert.Contains("Stax or taxation", html, StringComparison.Ordinal);
        Assert.Contains("Swords to Plowshares", html, StringComparison.Ordinal);
    }

    private static InteractionAudit BuildAudit() => new(
        TargetedRemoval: Bucket("Swords to Plowshares", "Beast Within"),
        BoardWipes: Bucket("Farewell", "Toxic Deluge"),
        Counterspells: Bucket("Counterspell", "Mana Drain"),
        ProtectionRecursion: Bucket("Teferi's Protection", "Eternal Witness"),
        StaxTaxation: Bucket("Drannith Magistrate", "Thalia, Guardian of Thraben"),
        CoverageGaps: ["Counterspell count is approximately low; verify against the list."]);

    private static InteractionBucketResult Bucket(string confident, string review) =>
        new(
            Confident: [new InteractionCard(confident, 1)],
            Review: [new InteractionCard(review, 1)]);

    private static DeckAnalysisViewModel BuildModel(InteractionAudit? interactionAudit, string interactionAuditJson) => new()
    {
        Request = new DeckAnalysisRequest
        {
            TargetAiPlatform = "ChatGPT",
            WorkflowStep = 3,
            InteractionAuditJson = interactionAuditJson,
        },
        AnalysisResponse = new DeckAnalysisResponse
        {
            Format = "Commander",
            Commander = "Test Commander",
        },
        InteractionAudit = interactionAudit,
    };

    /// <summary>
    /// Replaces the per-render antiforgery token value with a stable placeholder so two renders of
    /// the same form compare equal outside the deliberately-different interaction-audit block.
    /// </summary>
    private static string NeutralizeAntiforgery(string html) => Regex.Replace(
        html,
        "(name=\"__RequestVerificationToken\"[^>]*value=\")[^\"]*\"",
        "${1}TOKEN\"");

    /// <summary>
    /// Splits a rendered page around the interaction-audit block insertion point. The prefix is
    /// everything up to and including the Analysis Summary heading; the suffix is everything from
    /// the per-category breakdown onward; the middle is the excised region that holds the audit
    /// block when populated and is whitespace-only when OFF.
    /// </summary>
    private static (string Prefix, string Suffix, string Middle) SplitAroundInteractionBlock(string html)
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

    private static async Task<string> RenderAsync(InteractionAudit? interactionAudit, string interactionAuditJson)
    {
        var model = BuildModel(interactionAudit, interactionAuditJson);

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
