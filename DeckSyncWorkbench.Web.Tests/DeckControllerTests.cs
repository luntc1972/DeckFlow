using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DeckSyncWorkbench.Web.Controllers;
using DeckSyncWorkbench.Web.Models;
using DeckSyncWorkbench.Core.Reporting;
using DeckSyncWorkbench.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckSyncWorkbench.Web.Tests;

public sealed class DeckControllerTests
{
    [Fact]
    public void BuildNoSuggestionsMessage_UsesCacheRefreshNotice_WhenNoDecks()
    {
        var totals = new CardDeckTotals(0, new Dictionary<string, int>());
        var message = CategorySuggestionMessageBuilder.BuildNoSuggestionsMessage("Guardian Project", totals);

        Assert.Equal("No card categories for Guardian Project have been observed in the cached data yet. Run Show Categories again to refresh the cache.", message);
    }

    [Fact]
    public void BuildNoSuggestionsMessage_UsesGeneralMessage_WhenDecksExist()
    {
        var totals = new CardDeckTotals(5, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["mainboard"] = 5
        });
        var message = CategorySuggestionMessageBuilder.BuildNoSuggestionsMessage("Guardian Project", totals);

        Assert.Equal("No category suggestions were found for Guardian Project. You can run the lookup again to retry the live Archidekt and EDHREC checks.", message);
    }

    [Fact]
    public async Task CardSearch_ReturnsServiceUnavailable_WhenScryfallFails()
    {
        var controller = new DeckController(
            new FakeDeckSyncService(),
            new ThrowingCardSearchService(new HttpRequestException("Scryfall search returned HTTP 503.", null, HttpStatusCode.ServiceUnavailable)),
            new FakeCategorySuggestionService(),
            NullLogger<DeckController>.Instance)
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

    private sealed class FakeDeckSyncService : IDeckSyncService
    {
        public Task<DeckSyncResult> CompareDecksAsync(DeckDiffRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    private sealed class ThrowingCardSearchService : ICardSearchService
    {
        private readonly Exception _exception;

        public ThrowingCardSearchService(Exception exception)
        {
            _exception = exception;
        }

        public Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<string>>(_exception);
    }

    private sealed class FakeCategorySuggestionService : ICategorySuggestionService
    {
        public Task<CategorySuggestionResult> SuggestAsync(CategorySuggestionRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
