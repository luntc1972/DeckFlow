using System.Diagnostics;
using System.Reflection;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
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
/// Render-level proof for the /Admin/CreatorProfile page (PTOOL-04). A controller-level fact
/// asserting on the C# model alone cannot detect a missing or misnamed Razor control, an omitted
/// antiforgery token, or markup that unconditionally renders the profile/report regions — these
/// facts inspect the rendered HTML. Mirrors the harness used by
/// <see cref="DeckAnalysisPrintButtonViewTests"/> and <see cref="AdminCreatorStyleViewRenderTests"/>.
/// </summary>
public sealed class AdminCreatorProfileViewRenderTests
{
    [Fact]
    public async Task Index_EmptyModel_RendersFormControlsAndAntiForgeryToken()
    {
        var vm = new AdminCreatorProfileViewModel();

        string html = await RenderAsync(vm);

        Assert.Contains("id=\"creator-profile-slug\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"creator-profile-username\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"creator-profile-platform\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"creator-profile-force-refresh\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_EmptyModel_ArchidektOptionIsSelected()
    {
        var vm = new AdminCreatorProfileViewModel();

        string html = await RenderAsync(vm);

        Assert.Contains(
            "<option value=\"archidekt\" selected=\"selected\">archidekt</option>",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<option value=\"moxfield\" selected=\"selected\">moxfield</option>",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_NoProfileAndNoReport_RendersNeitherProfileNorReportRegions()
    {
        var vm = new AdminCreatorProfileViewModel();

        string html = await RenderAsync(vm);

        Assert.DoesNotContain("id=\"creator-profile-summary\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"creator-profile-metrics\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"creator-profile-decks\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"creator-profile-repeat-cards\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"creator-profile-repeat-commanders\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"creator-profile-category-tendencies\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Index_PopulatedProfileAndReport_RendersMetricsAndAllFourReportCollections()
    {
        var profile = new CreatorStyleProfile
        {
            Slug = "snail",
            Platform = "archidekt",
            MinDecks = 7,
            InsufficientSample = false,
            MeasuredMetrics =
            [
                new MeasuredMetric { Metric = "category_ratio:ramp", Value = 12.3456, NumDecks = 7 },
            ],
            UpdatedUtc = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
        };
        var report = new DeckTendenciesReport
        {
            DeckCount = 2,
            Decks =
            [
                new DeckTendencyDeckRow
                {
                    DeckId = "deck-1",
                    DeckName = "Snail Kicks",
                    CardCount = 100,
                    FolderName = "Brew Box",
                    Commanders = ["Atraxa, Praetors' Voice"],
                },
            ],
            RepeatCards =
            [
                new RepeatCardRow { CardName = "Sol Ring", DeckCount = 2, Frequency = 1.0, IsPersonalStaple = true },
            ],
            RepeatCommanders =
            [
                new RepeatCardRow { CardName = "Atraxa, Praetors' Voice", DeckCount = 2, Frequency = 1.0, IsPersonalStaple = false },
            ],
            CategoryTendencies =
            [
                new CategoryTendencyRow { Category = "Ramp", AverageCountPerDeck = 2.5, PresenceRatio = 1.0 },
            ],
        };
        var vm = new AdminCreatorProfileViewModel { Profile = profile, Report = report };

        string html = await RenderAsync(vm);

        Assert.Contains("category_ratio:ramp", html, StringComparison.Ordinal);
        Assert.Contains("Snail Kicks", html, StringComparison.Ordinal);
        Assert.Contains("Sol Ring", html, StringComparison.Ordinal);
        Assert.Contains("Atraxa, Praetors&#x27; Voice", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ramp", html, StringComparison.Ordinal);
        Assert.Contains("id=\"creator-profile-decks\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"creator-profile-repeat-cards\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"creator-profile-repeat-commanders\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"creator-profile-category-tendencies\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_CarriesValidateAntiForgeryTokenAttribute()
    {
        MethodInfo runMethod = typeof(AdminCreatorProfileController).GetMethod(nameof(AdminCreatorProfileController.Run))!;

        var attributes = runMethod.GetCustomAttributes(inherit: false);

        Assert.Contains(attributes, a => a.GetType().Name == "ValidateAntiForgeryTokenAttribute");
    }

    [Fact]
    public async Task Index_UntrustedUpstreamStringsContainingMarkup_AreHtmlEncoded()
    {
        const string malicious = "<script>alert('x')</script>";
        var profile = new CreatorStyleProfile
        {
            Slug = "snail",
            Platform = "archidekt",
            MinDecks = 7,
            UpdatedUtc = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
        };
        var report = new DeckTendenciesReport
        {
            DeckCount = 1,
            Decks =
            [
                new DeckTendencyDeckRow
                {
                    DeckId = "deck-1",
                    DeckName = malicious,
                    CardCount = 100,
                    FolderName = malicious,
                    Commanders = [malicious],
                },
            ],
            RepeatCards =
            [
                new RepeatCardRow { CardName = malicious, DeckCount = 2, Frequency = 1.0, IsPersonalStaple = false },
            ],
            RepeatCommanders = [],
            CategoryTendencies =
            [
                new CategoryTendencyRow { Category = malicious, AverageCountPerDeck = 1.0, PresenceRatio = 1.0 },
            ],
        };
        var vm = new AdminCreatorProfileViewModel { Profile = profile, Report = report };

        string html = await RenderAsync(vm);

        Assert.DoesNotContain("<script>alert('x')</script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> RenderAsync(AdminCreatorProfileViewModel model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddControllersWithViews().AddApplicationPart(typeof(AdminCreatorProfileController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "AdminCreatorProfile" })),
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
            ApplicationName = typeof(AdminCreatorProfileController).Assembly.GetName().Name ?? "DeckFlow.Web",
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
