using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Configuration;
using DeckFlow.Core.Reporting;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="DeckCategoriesController"/> category suggestion support actions with faked service dependencies.
/// </summary>
public sealed class DeckCategoriesControllerTests
{
    [Fact]
    public void BuildNoSuggestionsMessage_UsesCachedDataNotice_WhenNoDecks()
    {
        var totals = new CardDeckTotals(0, new Dictionary<string, int>());
        var message = CategorySuggestionMessageBuilder.BuildNoSuggestionsMessage("Guardian Project", totals);

        Assert.Equal("No card categories for Guardian Project have been observed in the cached data yet.", message);
    }

    [Fact]
    public void BuildNoSuggestionsMessage_UsesGeneralMessage_WhenDecksExist()
    {
        var totals = new CardDeckTotals(5, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["mainboard"] = 5
        });
        var message = CategorySuggestionMessageBuilder.BuildNoSuggestionsMessage("Guardian Project", totals);

        Assert.Equal("No category suggestions were found for Guardian Project in the selected sources.", message);
    }

    [Fact]
    public async Task CardSearch_ReturnsServiceUnavailable_WhenScryfallFails()
    {
        var controller = new DeckCategoriesController(
            new FakeCategorySuggestionService(),
            new ThrowingCardSearchService(new HttpRequestException("Scryfall search returned HTTP 503.", null, HttpStatusCode.ServiceUnavailable)),
            NullLogger<DeckCategoriesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.CardSearch("bello");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var payload = objectResult.Value!;
        var message = payload.GetType().GetProperty("Message")?.GetValue(payload) as string;
        Assert.Equal("Scryfall returned HTTP 503. Try again shortly.", message);
    }

    [Fact]
    public async Task SuggestCategories_Success_PopulatesMergedAndLegacyCategoryTexts()
    {
        var controller = new DeckCategoriesController(
            new StubCategorySuggestionService(new CategorySuggestionResult(
                "Guardian Project",
                ["Card Draw"],
                ["Ramp", "Draw"],
                ["PUMP✊"],
                ["Draw"],
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["draw"] = 2,
                    ["ramp"] = 3
                },
                new CardDeckTotals(3, new Dictionary<string, int>()),
                ["cached store", "Scryfall Tagger"],
                false)),
            new StubCardSearchService(),
            NullLogger<DeckCategoriesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var response = await controller.SuggestCategories(new CategorySuggestionRequest
        {
            CardName = "Guardian Project"
        });

        var viewResult = Assert.IsType<ViewResult>(response);
        var model = Assert.IsType<DeckDiffViewModel>(viewResult.Model);
        Assert.Equal("- Draw" + Environment.NewLine + "- Ramp", model.MergedCategoriesText);
        Assert.Equal("- Card Draw", model.ExactSuggestedCategoriesText);
        Assert.Equal("- Ramp" + Environment.NewLine + "- Draw", model.InferredCategoriesText);
        Assert.Equal("- PUMP✊", model.EdhrecCategoriesText);
        Assert.Equal("- Draw", model.TaggerCategoriesText);
    }

    [Fact]
    public async Task SuggestCategories_Success_BuildsWeightedCategoriesWithCanonicalLookupSortingAndClamp()
    {
        var controller = new DeckCategoriesController(
            new StubCategorySuggestionService(new CategorySuggestionResult(
                "Guardian Project",
                ["Card Draw", "Ramp"],
                ["Draw", "Protection"],
                ["Protection"],
                ["Protection", "Tutor"],
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["draw"] = 70,
                    ["protection"] = 120,
                    ["ramp"] = 30
                },
                new CardDeckTotals(60, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mainboard"] = 60
                }),
                ["reference deck", "cached store", "EDHREC", "Scryfall Tagger"],
                false)),
            new StubCardSearchService(),
            NullLogger<DeckCategoriesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var response = await controller.SuggestCategories(new CategorySuggestionRequest
        {
            CardName = "Guardian Project"
        });

        var model = Assert.IsType<DeckDiffViewModel>(Assert.IsType<ViewResult>(response).Model);
        Assert.Collection(
            model.WeightedCategories,
            row =>
            {
                Assert.Equal("Protection", row.Category);
                Assert.Equal(120, row.DeckCount);
                Assert.Equal(100, row.Percent);
                Assert.Equal(3, row.SourceCount);
                Assert.Equal(4, row.SourceTotal);
            },
            row =>
            {
                Assert.Equal("Draw", row.Category);
                Assert.Equal(70, row.DeckCount);
                Assert.Equal(100, row.Percent);
                Assert.Equal(2, row.SourceCount);
                Assert.Equal(4, row.SourceTotal);
            },
            row =>
            {
                Assert.Equal("Ramp", row.Category);
                Assert.Equal(30, row.DeckCount);
                Assert.Equal(50, row.Percent);
                Assert.Equal(1, row.SourceCount);
                Assert.Equal(4, row.SourceTotal);
            },
            row =>
            {
                Assert.Equal("Tutor", row.Category);
                Assert.Null(row.DeckCount);
                Assert.Null(row.Percent);
                Assert.Equal(1, row.SourceCount);
                Assert.Equal(4, row.SourceTotal);
            });
    }

    [Fact]
    public async Task SuggestCategories_Success_KeepsMergedCopyTextPlain()
    {
        var controller = new DeckCategoriesController(
            new StubCategorySuggestionService(new CategorySuggestionResult(
                "Guardian Project",
                ["Card Draw"],
                ["Ramp"],
                Array.Empty<string>(),
                Array.Empty<string>(),
                new Dictionary<string, int>(StringComparer.Ordinal),
                new CardDeckTotals(0, new Dictionary<string, int>()),
                ["reference deck", "cached store"],
                false)),
            new StubCardSearchService(),
            NullLogger<DeckCategoriesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var response = await controller.SuggestCategories(new CategorySuggestionRequest
        {
            CardName = "Guardian Project"
        });

        var model = Assert.IsType<DeckDiffViewModel>(Assert.IsType<ViewResult>(response).Model);
        Assert.Equal("- Draw" + Environment.NewLine + "- Ramp", model.MergedCategoriesText);
    }

    [Fact]
    public async Task SuggestCategoriesView_RendersWeightedTableAboveCopyBox_AndCopyTextStaysPlain()
    {
        var model = new DeckDiffViewModel
        {
            ActiveTab = DeckPageTab.SuggestCategories,
            SuggestionRequest = new CategorySuggestionRequest
            {
                CardName = "Guardian Project"
            },
            MergedCategoriesText = "- Draw" + Environment.NewLine + "- Ramp",
            WeightedCategories = new[]
            {
                new CategoryWeightRow("Draw", 12, 34, 2, 3),
                new CategoryWeightRow("Tutor", null, null, 1, 3)
            }
        };

        var html = await RenderAsync(model);
        var tableIndex = html.IndexOf("<table class=\"conflicts-table\"", StringComparison.Ordinal);
        var textareaIndex = html.IndexOf("id=\"merged-categories-output\"", StringComparison.Ordinal);

        Assert.True(tableIndex >= 0);
        Assert.True(textareaIndex > tableIndex);
        Assert.Contains("<th scope=\"col\">Category</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Decks</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">%</th>", html, StringComparison.Ordinal);
        Assert.Contains("title=\"Sources that agreed / sources that contributed\"", html, StringComparison.Ordinal);
        Assert.Contains("<td>Draw</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>12</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>34</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>2/3</td>", html, StringComparison.Ordinal);
        Assert.Contains("<td>Tutor</td>", html, StringComparison.Ordinal);
        Assert.True(
            html.Contains("<td>&#x2014;</td>", StringComparison.Ordinal)
                || html.Contains("<td>—</td>", StringComparison.Ordinal),
            "Expected the weighted table to render an em dash for null deck and percent values.");
        Assert.Contains(">Copy</button>", html, StringComparison.Ordinal);
        Assert.Contains("id=\"merged-categories-output\"", html, StringComparison.Ordinal);
        Assert.Contains("- Draw", html, StringComparison.Ordinal);
        Assert.Contains("- Ramp", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuggestCategoriesView_WeightedTableHeadersHaveColumnScope()
    {
        var model = new DeckDiffViewModel
        {
            ActiveTab = DeckPageTab.SuggestCategories,
            SuggestionRequest = new CategorySuggestionRequest
            {
                CardName = "Guardian Project"
            },
            WeightedCategories = new[]
            {
                new CategoryWeightRow("Draw", 12, 34, 2, 3)
            }
        };

        var html = await RenderAsync(model);
        var tableMatch = System.Text.RegularExpressions.Regex.Match(
            html,
            "<table class=\"conflicts-table\"[\\s\\S]*?</table>",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        Assert.True(tableMatch.Success);
        Assert.DoesNotMatch(
            "<th\\b(?![^>]*\\bscope\\s*=\\s*\"col\")[^>]*>",
            tableMatch.Value);
    }

    [Fact]
    public async Task SuggestCategoriesView_RendersHiddenWeightedTableWrapper_WhenNoWeightedCategoriesExist()
    {
        var html = await RenderAsync(new DeckDiffViewModel
        {
            ActiveTab = DeckPageTab.SuggestCategories
        });

        Assert.Contains("data-api-panel=\"weighted\"", html, StringComparison.Ordinal);
        Assert.Matches("<div class=\"hidden\"\\s+data-api-panel=\"weighted\">", html);
        Assert.DoesNotMatch("<div\\s+class=\"[^\"]*result-panel[^\"]*\"\\s+data-api-panel=\"weighted\">", html);
        Assert.Contains("data-api-field=\"weighted-body\"", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(DeckDiffViewModel model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<DeckFlow.Web.Services.Tools.IToolRegistry, DeckFlow.Web.Services.Tools.ToolRegistry>();
        services.AddSingleton<DeckFlow.Web.Services.FeatureFlags.IFeatureFlagCache>(new FakeFeatureFlagCache());
        services.AddSingleton<IOptions<AiPlatformOptions>>(Options.Create(new AiPlatformOptions()));
        services.AddControllersWithViews().AddApplicationPart(typeof(DeckCategoriesController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "Deck" })),
            new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "SuggestCategories", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'SuggestCategories' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

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
            ApplicationName = typeof(DeckCategoriesController).Assembly.GetName().Name ?? "DeckFlow.Web",
            ContentRootPath = contentRoot,
            ContentRootFileProvider = fileProvider,
            EnvironmentName = Environments.Development,
            WebRootPath = contentRoot,
            WebRootFileProvider = fileProvider,
        };
    }

    private sealed class StubCategorySuggestionService : ICategorySuggestionService
    {
        private readonly CategorySuggestionResult _result;

        public StubCategorySuggestionService(CategorySuggestionResult result)
        {
            _result = result;
        }

        public Task<CategorySuggestionResult> SuggestAsync(CategorySuggestionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class StubCardSearchService : ICardSearchService
    {
        public Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task<IReadOnlyList<string>> SearchCommandersAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
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
