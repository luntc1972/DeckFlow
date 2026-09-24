using System.Diagnostics;
using System.Globalization;
using System.IO;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
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

public sealed class CreatorRemovalRequestTests
{
    [Fact]
    public void Index_Get_ValidCreatorRemovalAndSource_PrefillsSubmission()
    {
        var controller = BuildController(out _);

        var result = Assert.IsType<ViewResult>(controller.Index("creator-removal", "/content-kb/123"));
        var model = Assert.IsType<FeedbackSubmission>(result.Model);

        Assert.Equal(FeedbackType.CreatorRemoval, model.Type);
        Assert.Equal("/content-kb/123", model.SourcePageUrl);
    }

    [Fact]
    public void Index_Get_UnknownType_UsesDefaultType()
    {
        var controller = BuildController(out _);

        var result = Assert.IsType<ViewResult>(controller.Index("other", null));

        Assert.Equal(FeedbackType.Comment, Assert.IsType<FeedbackSubmission>(result.Model).Type);
    }

    [Fact]
    public void Index_Get_InvalidSources_DropsSource()
    {
        foreach (var source in new[] { "https://evil.example", "/content-kb/../admin", "/content-kbx", "/content-kb/1?x=y", "/content-kb\n", "/content-kb/1\n" })
        {
            var controller = BuildController(out _);

            var result = Assert.IsType<ViewResult>(controller.Index("creator-removal", source));

            Assert.Null(Assert.IsType<FeedbackSubmission>(result.Model).SourcePageUrl);
        }
    }

    [Fact]
    public async Task Index_Get_RenderedForm_UsesModelTypeAndValidatedSource()
    {
        var creatorRemoval = BuildController(out _);
        creatorRemoval.ModelState.SetModelValue("type", new ValueProviderResult("creator-removal", CultureInfo.InvariantCulture));
        var creatorModel = Assert.IsType<FeedbackSubmission>(Assert.IsType<ViewResult>(creatorRemoval.Index("creator-removal", "/content-kb/123")).Model);

        var creatorHtml = await RenderFeedbackViewAsync(creatorModel, creatorRemoval.ViewData.ModelState);

        Assert.Contains("<option selected=\"selected\" value=\"3\">Creator removal request</option>", creatorHtml, StringComparison.Ordinal);
        Assert.Matches("<input(?=[^>]*name=\"SourcePageUrl\")(?=[^>]*value=\"/content-kb/123\")[^>]*>", creatorHtml);

        var other = BuildController(out _);
        other.ModelState.SetModelValue("type", new ValueProviderResult("other", CultureInfo.InvariantCulture));
        var otherModel = Assert.IsType<FeedbackSubmission>(Assert.IsType<ViewResult>(other.Index("other", "https://evil.example")).Model);

        var otherHtml = await RenderFeedbackViewAsync(otherModel, other.ViewData.ModelState);

        Assert.Contains("<option selected=\"selected\" value=\"2\">Comment</option>", otherHtml, StringComparison.Ordinal);
        Assert.Matches("<input(?=[^>]*name=\"SourcePageUrl\")(?=[^>]*value=\"\")[^>]*>", otherHtml);
    }

    [Fact]
    public async Task Index_Post_ValidSource_StoresSourceAndCreatorRemovalType()
    {
        var controller = BuildController(out var store);
        controller.HttpContext.Request.Headers.Referer = "https://deckflow.gg/content-kb/other";
        var submission = new FeedbackSubmission
        {
            Type = FeedbackType.CreatorRemoval,
            Message = "Please remove the creator content.",
            SourcePageUrl = "/content-kb/123",
        };

        _ = await controller.Index(submission, CancellationToken.None);

        Assert.Equal(FeedbackType.CreatorRemoval, store.LastSubmission!.Type);
        Assert.Equal("/content-kb/123", store.LastContext!.PageUrl);
    }

    [Fact]
    public async Task Index_Post_InvalidSource_UsesReferer()
    {
        var controller = BuildController(out var store);
        controller.HttpContext.Request.Headers.Referer = "https://deckflow.gg/content-kb/other";
        var submission = new FeedbackSubmission
        {
            Type = FeedbackType.CreatorRemoval,
            Message = "Please remove the creator content.",
            SourcePageUrl = "https://evil.example",
        };

        _ = await controller.Index(submission, CancellationToken.None);

        Assert.Equal("https://deckflow.gg/content-kb/other", store.LastContext!.PageUrl);
    }

    [Fact]
    public async Task Index_Post_BugWithoutSource_UsesReferer()
    {
        var controller = BuildController(out var store);
        controller.HttpContext.Request.Headers.Referer = "https://deckflow.gg/decks/99";
        var submission = new FeedbackSubmission { Type = FeedbackType.Bug, Message = "Bug report with enough text." };

        _ = await controller.Index(submission, CancellationToken.None);

        Assert.Equal("https://deckflow.gg/decks/99", store.LastContext!.PageUrl);
    }

    [Fact]
    public async Task FeedbackStore_CreatorRemoval_PersistsAndReadsBack()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"creator-removal-{Guid.NewGuid():N}.db");
        try
        {
            var store = new FeedbackStore(databasePath);
            var id = await store.AddAsync(
                new FeedbackSubmission { Type = FeedbackType.CreatorRemoval, Message = "Please remove this creator content." },
                new FeedbackRequestContext(null, null, "/content-kb/123", "test"));

            var item = await store.GetAsync(id);

            Assert.NotNull(item);
            Assert.Equal(FeedbackType.CreatorRemoval, item.Type);
            Assert.Equal("/content-kb/123", item.PageUrl);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearPool(new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.GetFullPath(databasePath)}"));
                File.Delete(databasePath);
            }
        }
    }

    private static FeedbackController BuildController(out FakeFeedbackStore store)
    {
        store = new FakeFeedbackStore();
        var httpContext = new DefaultHttpContext();
        return new FeedbackController(store, new FakeVersionService())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider()),
        };
    }

    private static async Task<string> RenderFeedbackViewAsync(FeedbackSubmission model, ModelStateDictionary modelState)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(provider => provider.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(provider => provider.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddControllersWithViews().AddApplicationPart(typeof(FeedbackController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.Path = "/feedback";
        var routeData = new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "Feedback" }));
        routeData.Routers.Add(new FeedbackRouter());
        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        var viewResult = serviceProvider.GetRequiredService<IRazorViewEngine>().FindView(actionContext, "Index", isMainPage: false);
        Assert.True(viewResult.Success, "Feedback view was not found.");
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState) { Model = model };
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, viewResult.View!, viewData, new TempDataDictionary(httpContext, new FakeTempDataProvider()), writer, new HtmlHelperOptions());

        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }

    private static IWebHostEnvironment CreateHostingEnvironment() => new TestWebHostEnvironment
    {
        ApplicationName = typeof(FeedbackController).Assembly.GetName().Name ?? "DeckFlow.Web",
        ContentRootPath = AppContext.BaseDirectory,
        ContentRootFileProvider = new NullFileProvider(),
        EnvironmentName = Environments.Development,
        WebRootPath = AppContext.BaseDirectory,
        WebRootFileProvider = new NullFileProvider(),
    };

    private sealed class FakeFeedbackStore : IFeedbackStore
    {
        public FeedbackSubmission? LastSubmission { get; private set; }
        public FeedbackRequestContext? LastContext { get; private set; }

        public Task<long> AddAsync(FeedbackSubmission submission, FeedbackRequestContext context, CancellationToken cancellationToken = default)
        {
            LastSubmission = submission;
            LastContext = context;
            return Task.FromResult(1L);
        }

        public Task<FeedbackItem?> GetAsync(long id, CancellationToken ct = default) => Task.FromResult<FeedbackItem?>(null);
        public Task<IReadOnlyList<FeedbackItem>> ListAsync(FeedbackListQuery query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<FeedbackItem>>(Array.Empty<FeedbackItem>());
        public Task<int> CountAsync(FeedbackStatus? status, FeedbackType? type, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyDictionary<FeedbackStatus, int>> CountsByStatusAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<FeedbackStatus, int>>(new Dictionary<FeedbackStatus, int>());
        public Task UpdateStatusAsync(long id, FeedbackStatus status, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(long id, CancellationToken ct = default) => Task.CompletedTask;
        public string HashIp(string? ip) => ip ?? string.Empty;
    }

    private sealed class FakeVersionService : IVersionService
    {
        public string GetVersion() => "test-version";
    }

    private sealed class FakeTempDataProvider : ITempDataProvider
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

    private sealed class FeedbackRouter : IRouter
    {
        public VirtualPathData? GetVirtualPath(VirtualPathContext context) => new(this, "/feedback");

        public Task RouteAsync(RouteContext context) => Task.CompletedTask;
    }
}
