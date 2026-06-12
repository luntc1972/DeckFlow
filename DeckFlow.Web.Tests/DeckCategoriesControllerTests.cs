using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Controllers;
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
}
