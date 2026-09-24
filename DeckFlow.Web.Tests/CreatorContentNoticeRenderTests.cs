using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Tools;
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

/// <summary>Render-level tests for the creator independence notice on Content KB pages.</summary>
public sealed class CreatorContentNoticeRenderTests
{
    private const string NoticeText = "Independent summaries and short excerpts of publicly available videos, produced by DeckFlow with AI assistance. Not written, reviewed, or endorsed by the creators named. Excerpts belong to their creators - watch the original video at the source link. Are you a creator and want a page changed or removed? Request removal. We aim to act on requests within 7 days.";

    [Fact]
    public async Task Index_NoticeTextAndPlacement_RendersBeforeContentList()
    {
        string html = await RenderViewAsync("Index", CreateBrowseModel(), "/content-kb");

        Assert.Contains(NoticeText, NormalizeText(html), StringComparison.Ordinal);
        Assert.True(html.IndexOf("creator-content-notice", StringComparison.Ordinal) < html.IndexOf("data-kb-grid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Detail_NoticeTextAndCreatorName_Renders()
    {
        string html = await RenderViewAsync("Detail", CreateDetailModel("Commander Cookout"), "/content-kb/123");

        Assert.Contains(NoticeText, NormalizeText(html), StringComparison.Ordinal);
        Assert.Contains("This page summarizes a video by Commander Cookout.", NormalizeText(html), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_RequestRemovalLink_UsesEncodedCurrentPath()
    {
        string html = await RenderViewAsync("Index", CreateBrowseModel(), "/content-kb");

        Assert.Equal("/feedback?type=creator-removal&source=%2Fcontent-kb", RequestRemovalHref(html));
    }

    [Fact]
    public async Task Detail_RequestRemovalLink_UsesEncodedCurrentPath()
    {
        string html = await RenderViewAsync("Detail", CreateDetailModel("Creator"), "/content-kb/123");

        Assert.Equal("/feedback?type=creator-removal&source=%2Fcontent-kb%2F123", RequestRemovalHref(html));
    }

    [Fact]
    public async Task Detail_CreatorNameContainingHtml_IsEncoded()
    {
        string html = await RenderViewAsync("Detail", CreateDetailModel("<script>alert(1)</script>"), "/content-kb/123");

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderViewAsync(string viewName, object model, string path)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(provider => provider.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(provider => provider.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.Configure<AiPlatformOptions>(_ => { });
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<IFeatureFlagCache>(new FakeFeatureFlagCache());
        services.AddControllersWithViews().AddApplicationPart(typeof(ContentKbController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.Path = path;
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "ContentKb" })),
            new ActionDescriptor());
        var viewResult = serviceProvider.GetRequiredService<IRazorViewEngine>().FindView(actionContext, viewName, isMainPage: false);
        Assert.True(viewResult.Success, $"View '{viewName}' was not found.");
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, viewResult.View!, viewData, new TempDataDictionary(httpContext, new StubTempDataProvider()), writer, new HtmlHelperOptions());

        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }

    private static ContentKbBrowseViewModel CreateBrowseModel() => new()
    {
        Entries = [],
        Sources = [],
        Archetypes = [],
        Brackets = [],
        CardCategories = [],
    };

    private static ContentKbDetailViewModel CreateDetailModel(string sourceName) => new()
    {
        Title = "Test artifact",
        SourceName = sourceName,
        SourceUrl = "https://example.test/video",
        PublishedDisplay = "September 24, 2026",
        Bracket = "cEDH",
        Archetype = "Combo",
        RenderedHtml = new Microsoft.AspNetCore.Html.HtmlString("<p>Artifact</p>"),
        CleanBodyText = "Artifact",
    };

    private static string NormalizeText(string html)
    {
        string text = Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim();
        return Regex.Replace(text, @"\s+([.,])", "$1");
    }

    private static string RequestRemovalHref(string html)
    {
        Match match = Regex.Match(html, "<a href=\"([^\"]+)\">Request removal</a>");
        Assert.True(match.Success, "Request removal link was not rendered.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static IWebHostEnvironment CreateHostingEnvironment() => new TestWebHostEnvironment
    {
        ApplicationName = typeof(ContentKbController).Assembly.GetName().Name ?? "DeckFlow.Web",
        ContentRootPath = AppContext.BaseDirectory,
        ContentRootFileProvider = new NullFileProvider(),
        EnvironmentName = Environments.Development,
        WebRootPath = AppContext.BaseDirectory,
        WebRootFileProvider = new NullFileProvider(),
    };

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
