using System.Diagnostics;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using DeckFlow.Web.Controllers.Admin;
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
/// Proves PTOOL-03 / D-03: the <c>/Admin</c> landing page carries a standalone "Personal Tools"
/// section linking Creator Style, structurally separate from the existing <c>admin-hub-grid</c>
/// (a sibling under <c>section.admin-landing</c>, not folded into it or into the public
/// tool-visibility list). Mirrors the Razor-view render harness used by
/// <see cref="AdminCreatorStyleViewRenderTests"/>.
/// </summary>
public sealed class AdminLandingPersonalToolsViewTests
{
    [Fact]
    public async Task Index_RendersPersonalToolsHeadingAndCreatorStyleLink()
    {
        string html = await RenderAsync();
        var document = new HtmlParser().ParseDocument(html);

        var anchors = document.QuerySelectorAll("a").OfType<IElement>();
        var creatorStyleAnchor = anchors.FirstOrDefault(a =>
            (a.GetAttribute("href") ?? string.Empty).EndsWith("/Admin/CreatorStyle", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(creatorStyleAnchor);

        Assert.Contains("Personal Tools", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_RendersCreatorProfileLinkInPersonalToolsBlock()
    {
        string html = await RenderAsync();
        var document = new HtmlParser().ParseDocument(html);

        var anchors = document.QuerySelectorAll("a").OfType<IElement>();
        var creatorProfileAnchor = anchors.FirstOrDefault(a =>
            (a.GetAttribute("href") ?? string.Empty).EndsWith("/Admin/CreatorProfile", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(creatorProfileAnchor);

        var personalToolsContainer = document.QuerySelector(".admin-hub-personal-tools");
        Assert.NotNull(personalToolsContainer);
        Assert.True(
            personalToolsContainer!.Contains(creatorProfileAnchor),
            "Deck Tendencies link must be inside the Personal Tools container.");
    }

    [Fact]
    public async Task Index_PersonalToolsSectionIsASiblingOfAdminHubGrid_NotNestedInsideIt()
    {
        string html = await RenderAsync();
        var document = new HtmlParser().ParseDocument(html);

        var landingSection = document.QuerySelector("section.admin-landing");
        Assert.NotNull(landingSection);

        var directChildren = landingSection!.Children.ToArray();
        var gridIndex = Array.FindIndex(directChildren, c => c.ClassList.Contains("admin-hub-grid"));
        var personalToolsIndex = Array.FindIndex(directChildren, c => c.ClassList.Contains("admin-hub-personal-tools"));

        Assert.True(gridIndex >= 0, "The existing admin-hub-grid was not found as a direct child of section.admin-landing.");
        Assert.True(personalToolsIndex >= 0, "The Personal Tools container was not found as a direct child of section.admin-landing.");
        Assert.True(
            personalToolsIndex > gridIndex,
            "The Personal Tools container must come after admin-hub-grid closes as a later sibling, not before or nested inside it.");

        // Structural proof, not mere text-order: the Personal Tools container must not itself be a
        // descendant of the existing admin-hub-grid element (which would satisfy plain ordering
        // while still violating D-03's standalone-section requirement).
        var personalToolsContainer = directChildren[personalToolsIndex];
        Assert.Null(personalToolsContainer.Closest(".admin-hub-grid"));

        // The Creator Style card lives inside its own nested grid within the Personal Tools
        // container (D-03: its own block, reusing the admin-hub-grid/card class names) - prove the
        // Creator Style anchor is NOT inside the original (first) admin-hub-grid.
        var originalGrid = directChildren[gridIndex];
        var creatorStyleAnchor = document.QuerySelectorAll("a")
            .FirstOrDefault(a => (a.GetAttribute("href") ?? string.Empty).EndsWith("/Admin/CreatorStyle", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(creatorStyleAnchor);
        Assert.False(originalGrid.Contains(creatorStyleAnchor), "Creator Style link must not be inside the original seven-card admin-hub-grid.");
    }

    private static async Task<string> RenderAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddControllersWithViews().AddApplicationPart(typeof(AdminCreatorStyleController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "AdminLanding" })),
            new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "Index", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'Index' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());

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
            ApplicationName = typeof(AdminCreatorStyleController).Assembly.GetName().Name ?? "DeckFlow.Web",
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
