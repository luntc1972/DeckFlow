using System.Diagnostics;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.CreatorStyleRubric;
using DeckFlow.Web.Controllers.Admin;
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
using Microsoft.Extensions.Logging.Abstractions;
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
        var controller = CreateController(new FakeCreatorStyleProfileStore(), new StubCreatorStylePacketService());

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<AdminCreatorStyleViewModel>(viewResult.Model);
        Assert.Empty(vm.AvailableCreators);
        Assert.Equal(AdminCreatorStyleController.NoProfilesSeededMessage, vm.Notice);
    }

    [Fact]
    public async Task Index_EmptySeedCorpus_RendersOperatorMessageInResultRegion()
    {
        var controller = CreateController(new FakeCreatorStyleProfileStore(), new StubCreatorStylePacketService());
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
        var controller = CreateController(store, new StubCreatorStylePacketService());

        var result = await controller.Index();
        var vm = Assert.IsType<AdminCreatorStyleViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Null(vm.Notice);

        string html = await RenderAsync(vm);

        Assert.Contains("alpha-creator", html, StringComparison.Ordinal);
        Assert.Contains("beta-creator", html, StringComparison.Ordinal);
        Assert.DoesNotContain(AdminCreatorStyleController.NoProfilesSeededMessage, html, StringComparison.Ordinal);
    }

    // --- Task 2: POST /Admin/CreatorStyle/Run critique path (T-114-03, T-114-04) ---

    [Fact]
    public async Task Run_BlankCreatorSlug_AddsModelStateErrorAndSkipsPacketService()
    {
        var packetService = new StubCreatorStylePacketService();
        var controller = CreateController(new FakeCreatorStyleProfileStore(), packetService);
        var request = new CreatorStyleRequest { CreatorSlug = string.Empty, DeckText = "1 Sol Ring" };

        var result = await controller.Run(request);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(CreatorStyleRequest.CreatorSlug)));
        Assert.Empty(packetService.Requests);
    }

    [Fact]
    public async Task Run_BlankDeckInput_AddsModelStateErrorAndSkipsPacketService()
    {
        var packetService = new StubCreatorStylePacketService();
        var controller = CreateController(new FakeCreatorStyleProfileStore(), packetService);
        var request = new CreatorStyleRequest { CreatorSlug = "alpha-creator", DeckText = string.Empty };

        var result = await controller.Run(request);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(CreatorStyleRequest.DeckText)));
        Assert.Empty(packetService.Requests);
    }

    [Fact]
    public async Task Run_ValidRequestAgainstEmptyCorpus_RendersCanonicalEmptySeedMessage()
    {
        var packetService = new StubCreatorStylePacketService(UnavailableResult("No creator style profile is available for the supplied creator slug."));
        var controller = CreateController(new FakeCreatorStyleProfileStore(), packetService);
        var request = new CreatorStyleRequest { CreatorSlug = "alpha-creator", DeckText = "1 Sol Ring" };

        var result = await controller.Run(request);

        var viewResult = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<AdminCreatorStyleViewModel>(viewResult.Model);
        Assert.Equal(AdminCreatorStyleController.NoProfilesSeededMessage, vm.Notice);
    }

    [Fact]
    public async Task Run_ValidRequestWithArtifactText_RendersArtifactTextInResultRegion()
    {
        var store = new FakeCreatorStyleProfileStore();
        store.Summaries.Add(NewSummary("alpha-creator"));
        var packetService = new StubCreatorStylePacketService(SuccessResult("Creator Targets\n..."));
        var controller = CreateController(store, packetService);
        var request = new CreatorStyleRequest { CreatorSlug = "alpha-creator", DeckText = "1 Sol Ring" };

        var result = await controller.Run(request);

        var viewResult = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<AdminCreatorStyleViewModel>(viewResult.Model);
        Assert.Equal("Creator Targets\n...", vm.ArtifactText);
    }

    [Fact]
    public async Task Run_FailingSameOriginCheck_Returns403AndSkipsPacketService()
    {
        var packetService = new StubCreatorStylePacketService();
        var controller = CreateController(new FakeCreatorStyleProfileStore(), packetService, crossOrigin: true);
        var request = new CreatorStyleRequest { CreatorSlug = "alpha-creator", DeckText = "1 Sol Ring" };

        var result = await controller.Run(request);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusResult.StatusCode);
        Assert.Empty(packetService.Requests);
    }

    [Fact]
    public async Task Run_ProfileUnavailableForEmptyCorpus_NoticeIsByteIdenticalToGetEmptyStateNotice()
    {
        var packetService = new StubCreatorStylePacketService(UnavailableResult("No creator style profile is available for the supplied creator slug."));
        var runController = CreateController(new FakeCreatorStyleProfileStore(), packetService);
        var request = new CreatorStyleRequest { CreatorSlug = "nonexistent", DeckText = "1 Sol Ring" };

        var runResult = await runController.Run(request);
        var runVm = Assert.IsType<AdminCreatorStyleViewModel>(Assert.IsType<ViewResult>(runResult).Model);

        var getController = CreateController(new FakeCreatorStyleProfileStore(), new StubCreatorStylePacketService());
        var getResult = await getController.Index();
        var getVm = Assert.IsType<AdminCreatorStyleViewModel>(Assert.IsType<ViewResult>(getResult).Model);

        Assert.Equal(getVm.Notice, runVm.Notice);
    }

    [Fact]
    public async Task Run_ProfileUnavailableForUnmatchedSlugAgainstNonEmptyCorpus_RendersNoticeVerbatimNotCanonicalConstant()
    {
        var store = new FakeCreatorStyleProfileStore();
        store.Summaries.Add(NewSummary("alpha-creator"));
        const string unmatchedSlugNotice = "No creator style profile is available for the supplied creator slug.";
        var packetService = new StubCreatorStylePacketService(UnavailableResult(unmatchedSlugNotice));
        var controller = CreateController(store, packetService);
        var request = new CreatorStyleRequest { CreatorSlug = "nonexistent-creator", DeckText = "1 Sol Ring" };

        var result = await controller.Run(request);

        var vm = Assert.IsType<AdminCreatorStyleViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(unmatchedSlugNotice, vm.Notice);
        Assert.NotEqual(AdminCreatorStyleController.NoProfilesSeededMessage, vm.Notice);
    }

    [Fact]
    public async Task Run_ProfileUnavailableForInsufficientSampleAgainstNonEmptyCorpus_RendersNoticeVerbatimNotCanonicalConstant()
    {
        var store = new FakeCreatorStyleProfileStore();
        store.Summaries.Add(NewSummary("alpha-creator"));
        const string insufficientSampleNotice = "The creator style profile sample is insufficient for artifact generation.";
        var packetService = new StubCreatorStylePacketService(UnavailableResult(insufficientSampleNotice));
        var controller = CreateController(store, packetService);
        var request = new CreatorStyleRequest { CreatorSlug = "alpha-creator", DeckText = "1 Sol Ring" };

        var result = await controller.Run(request);

        var vm = Assert.IsType<AdminCreatorStyleViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(insufficientSampleNotice, vm.Notice);
        Assert.NotEqual(AdminCreatorStyleController.NoProfilesSeededMessage, vm.Notice);
    }

    [Fact]
    public async Task Run_PacketServiceThrows_RendersOperatorNoticeInsteadOfPropagating()
    {
        var store = new FakeCreatorStyleProfileStore();
        store.Summaries.Add(NewSummary("alpha-creator"));
        var controller = CreateController(store, new ThrowingCreatorStylePacketService());
        var request = new CreatorStyleRequest { CreatorSlug = "alpha-creator", DeckText = "1 Sol Ring" };

        var result = await controller.Run(request);

        var vm = Assert.IsType<AdminCreatorStyleViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.NotNull(vm.Notice);
        Assert.Null(vm.ArtifactText);
    }

    private static CreatorStyleProfileSummary NewSummary(string slug)
        => new()
        {
            Slug = slug,
            Platform = "youtube",
            MinDecks = 5,
            UpdatedUtc = DateTimeOffset.Parse("2026-07-18T00:00:00Z"),
        };

    private static CreatorStylePacketResult UnavailableResult(string notice)
        => new()
        {
            ArtifactText = string.Empty,
            RubricScores = new RubricScoreResult { CreatorSlug = string.Empty, MetricScores = [] },
            Exemplars = [],
            ValidatedWhitelist = [],
            ValidatedComboCards = [],
            GroundingDegraded = false,
            Notice = notice,
            ProfileUnavailable = true,
        };

    private static CreatorStylePacketResult SuccessResult(string artifactText)
        => new()
        {
            ArtifactText = artifactText,
            RubricScores = new RubricScoreResult { CreatorSlug = "alpha-creator", MetricScores = [] },
            Exemplars = [],
            ValidatedWhitelist = [],
            ValidatedComboCards = [],
            GroundingDegraded = false,
            Notice = null,
            ProfileUnavailable = false,
        };

    private static AdminCreatorStyleController CreateController(
        ICreatorStyleProfileStore profileStore,
        ICreatorStylePacketService packetService,
        bool crossOrigin = false)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("www.deckflow.gg");
        if (crossOrigin)
        {
            httpContext.Request.Headers.Origin = "https://attacker.example";
        }

        return new AdminCreatorStyleController(profileStore, packetService, NullLogger<AdminCreatorStyleController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

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

    private sealed class StubCreatorStylePacketService : ICreatorStylePacketService
    {
        private readonly Queue<CreatorStylePacketResult> _queue;

        public StubCreatorStylePacketService(params CreatorStylePacketResult[] results)
        {
            _queue = new Queue<CreatorStylePacketResult>(results);
        }

        public List<CreatorStyleRequest> Requests { get; } = new();

        public Task<string?> TryComputeCacheKeyAsync(CreatorStyleRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);

        public Task<CreatorStylePacketResult> BuildAsync(CreatorStyleRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (_queue.Count == 0)
            {
                throw new InvalidOperationException("No stubbed CreatorStylePacketResult queued for this call.");
            }

            return Task.FromResult(_queue.Dequeue());
        }
    }

    private sealed class ThrowingCreatorStylePacketService : ICreatorStylePacketService
    {
        public Task<string?> TryComputeCacheKeyAsync(CreatorStyleRequest request, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);

        public Task<CreatorStylePacketResult> BuildAsync(CreatorStyleRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated packet service failure.");
    }
}
