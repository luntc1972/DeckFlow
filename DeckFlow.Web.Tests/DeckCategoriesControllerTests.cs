using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
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
}
