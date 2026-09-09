using System.Diagnostics;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
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
/// Render- and controller-level proof for the /Admin/CreatorStyle page (PTOOL-01, D-02). Mirrors
/// the Razor-view render harness used by <see cref="DeckAnalysisPrintButtonViewTests"/>.
/// </summary>
public sealed class AdminCreatorStyleViewRenderTests
{
    [Fact]
    public async Task Index_EmptySeedCorpus_ModelReportsNoAvailableCreators()
    {
        var controller = new AdminCreatorStyleController(new FakeCreatorStyleProfileStore());

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<AdminCreatorStyleViewModel>(viewResult.Model);
        Assert.Empty(vm.AvailableCreators);
        Assert.Equal(AdminCreatorStyleController.NoProfilesSeededMessage, vm.Notice);
    }

    [Fact]
    public async Task Index_EmptySeedCorpus_RendersOperatorMessageInResultRegion()
    {
        var controller = new AdminCreatorStyleController(new FakeCreatorStyleProfileStore());
        var vm = Assert.IsType<AdminCreatorStyleViewModel>(Assert.IsType<ViewResult>(await controller.Index()).Model);

        string html = await RenderAsync(vm);

        Assert.Contains(AdminCreatorStyleController.NoProfilesSeededMessage, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_EmptySeedCorpus_StillRendersCreatorPickerAndDeckInputForm()
    {
        var vm = new AdminCreatorStyleViewModel { Notice = AdminCreatorStyleController.NoProfilesSeededMessage };

        string html = await RenderAsync(vm);

        // The empty state replaces the result region, not the page: picker + both deck-input
        // fields (URL and paste-text) must still be present.
        Assert.Contains("id=\"creator-style-slug\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"creator-style-deck-url\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"creator-style-deck-text\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"creator-style-format\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_SeededProfiles_ListsEachSlugAndOmitsOperatorMessage()
    {
        var store = new FakeCreatorStyleProfileStore();
        store.Summaries.Add(NewSummary("alpha-creator"));
        store.Summaries.Add(NewSummary("beta-creator"));
        var controller = new AdminCreatorStyleController(store);

        var result = await controller.Index();
        var vm = Assert.IsType<AdminCreatorStyleViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Null(vm.Notice);

        string html = await RenderAsync(vm);

        Assert.Contains("alpha-creator", html, StringComparison.Ordinal);
        Assert.Contains("beta-creator", html, StringComparison.Ordinal);
        Assert.DoesNotContain(AdminCreatorStyleController.NoProfilesSeededMessage, html, StringComparison.Ordinal);
    }

    private static CreatorStyleProfileSummary NewSummary(string slug)
        => new()
        {
            Slug = slug,
            Platform = "youtube",
            MinDecks = 5,
            UpdatedUtc = DateTimeOffset.Parse("2026-07-18T00:00:00Z"),
        };

    private static async Task<string> RenderAsync(AdminCreatorStyleViewModel model)
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
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "AdminCreatorStyle" })),
            new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "Index", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'Index' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

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

    private sealed class FakeCreatorStyleProfileStore : ICreatorStyleProfileStore
    {
        public List<CreatorStyleProfileSummary> Summaries { get; } = new();

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult<CreatorStyleProfile?>(null);

        public Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<CreatorStyleProfileSummary>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CreatorStyleProfileSummary>>(Summaries);
    }
}
