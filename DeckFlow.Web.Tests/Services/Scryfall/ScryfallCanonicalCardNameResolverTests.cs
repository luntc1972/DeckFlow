using System.Net;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Scryfall;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests.Services.Scryfall;

public sealed class ScryfallCanonicalCardNameResolverTests
{
    private const string FrontFace = "Valki, God of Lies";
    private const string DoubleFaced = "Valki, God of Lies // Tibalt, Cosmic Impostor";

    [Fact]
    public async Task ResolveCanonicalNamesAsync_FrontFaceName_MapsToDoubleFacedCardFromCache()
    {
        var collectionCalls = 0;
        var searchQueries = new List<string>();
        var resolver = TestServiceFactory.CreateScryfallCanonicalCardNameResolver(
            (request, _) =>
            {
                collectionCalls++;
                return Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
                {
                    StatusCode = HttpStatusCode.OK,
                    Data = new ScryfallCollectionResponse(
                        [Card(DoubleFaced), Card("Sol Ring")],
                        [new ScryfallCollectionNameIdentifier("Notacard Xyzzy")])
                });
            },
            (request, _) =>
            {
                searchQueries.Add(Assert.Single(request.Parameters, parameter => parameter.Name == "q").Value!.ToString()!);
                return Task.FromResult(new RestResponse<ScryfallSearchResponse>(request) { StatusCode = HttpStatusCode.NotFound });
            });

        var result = await resolver.ResolveCanonicalNamesAsync([FrontFace, DoubleFaced, "Sol Ring", "Notacard Xyzzy"]);

        Assert.Equal(DoubleFaced, result[FrontFace]);
        Assert.Equal(DoubleFaced, result[DoubleFaced]);
        Assert.Equal("Sol Ring", result["Sol Ring"]);
        Assert.False(result.ContainsKey("Notacard Xyzzy"));
        Assert.Equal(DoubleFaced, result[FrontFace.ToLowerInvariant()]);
        Assert.Equal(1, collectionCalls);
        Assert.Contains("Notacard Xyzzy", Assert.Single(searchQueries));
    }

    [Fact]
    public async Task ResolveCanonicalNamesAsync_EmptyInput_ReturnsEmptyMapWithoutScryfallCall()
    {
        var resolver = TestServiceFactory.CreateScryfallCanonicalCardNameResolver(
            (_, _) => throw new InvalidOperationException("Unexpected Scryfall collection call"),
            (_, _) => throw new InvalidOperationException("Unexpected Scryfall search call"));

        Assert.Empty(await resolver.ResolveCanonicalNamesAsync([]));
    }

    private static ScryfallCard Card(string name)
        => new(name, null, "Artifact", null, null, null, [], [], null, null, null);
}
