using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DeckSyncWorkbench.Web.Services;
using RestSharp;
using Xunit;

namespace DeckSyncWorkbench.Web.Tests;

public sealed class CardLookupServiceTests
{
    [Fact]
    public async Task LookupAsync_PreservesQuantities_AndCollectsMissingLines()
    {
        var service = new ScryfallCardLookupService(
            executeAsync: (request, _) => Task.FromResult(CreateCollectionResponse(
                new[]
                {
                    new ScryfallCard("Sol Ring", "{T}", "Artifact", "Add {W}", "—", "—"),
                    new ScryfallCard("Arcane Signet", "{1}", "Artifact", "Add {W} or {U}", "—", "—")
                },
                new[] { new ScryfallCollectionIdentifier("Made Up Card") },
                request)));

        var result = await service.LookupAsync("1 Sol Ring\nArcane Signet\nMade Up Card");

        Assert.Contains("Sol Ring", result.VerifiedOutputs[0]);
        Assert.Contains("{T}", result.VerifiedOutputs[0]);
        Assert.Equal(new[] { "ERROR: Made Up Card" }, result.MissingLines);
    }

    [Fact]
    public async Task LookupAsync_SendsCollectionRequestsInBatches()
    {
        var requestCount = 0;
        var service = new ScryfallCardLookupService(
            executeAsync: (request, _) =>
            {
                requestCount++;
                return Task.FromResult(CreateCollectionResponse(
                    Array.Empty<ScryfallCard>(),
                    Enumerable.Range(0, 75).Select(index => new ScryfallCollectionIdentifier($"Card {index + ((requestCount - 1) * 75)}")).ToArray(),
                    request));
            });

        var lines = string.Join('\n', Enumerable.Range(0, 100).Select(index => $"Card {index}"));
        await service.LookupAsync(lines);

        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task LookupAsync_ThrowsInvalidOperationException_WhenTooManyCardsSubmitted()
    {
        var service = new ScryfallCardLookupService(
            executeAsync: (request, _) => Task.FromResult(CreateCollectionResponse(
                new[]
                {
                    new ScryfallCard("Sol Ring", "{T}", "Artifact", "Add {W}", "—", "—")
                },
                Array.Empty<ScryfallCollectionIdentifier>(),
                request)));
        var lines = string.Join('\n', Enumerable.Repeat("Sol Ring", 101));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.LookupAsync(lines));

        Assert.Equal("Please verify 100 non-empty lines or fewer per submission.", exception.Message);
    }

    [Fact]
    public async Task LookupAsync_ThrowsHttpRequestException_WhenScryfallFails()
    {
        var service = new ScryfallCardLookupService(
            executeAsync: (request, _) => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            }));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.LookupAsync("Sol Ring"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    private static RestResponse<ScryfallCollectionResponse> CreateCollectionResponse(
        IReadOnlyList<ScryfallCard> cards,
        IReadOnlyList<ScryfallCollectionIdentifier> notFound,
        RestRequest request)
    {
        return new RestResponse<ScryfallCollectionResponse>(request)
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallCollectionResponse(cards.ToList(), notFound.ToList())
        };
    }
}
