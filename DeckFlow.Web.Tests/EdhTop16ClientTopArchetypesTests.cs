using System.Net;
using DeckFlow.Web.Services;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Covers the meta-wide EDH Top 16 archetype query added for bracket-5 primer routing.
/// </summary>
public sealed class EdhTop16ClientTopArchetypesTests
{
    [Fact]
    public async Task GetTopArchetypesAsync_ParsesNamedArchetypes()
    {
        var client = new EdhTop16Client(executeAsync: (_, _) => Task.FromResult(new RestResponse
        {
            StatusCode = HttpStatusCode.OK,
            ResponseStatus = ResponseStatus.Completed,
            Content = """
                {
                  "data": {
                    "commanders": {
                      "edges": [
                        { "node": { "name": "Kraum / Tymna", "colorId": "WUBR" } },
                        { "node": { "name": "Kinnan", "colorId": "UG" } }
                      ]
                    }
                  },
                  "errors": []
                }
                """
        }));

        var result = await client.GetTopArchetypesAsync(2, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Kraum / Tymna", result[0].PlayerName);
        Assert.Equal("WUBR", result[0].TournamentName);
        Assert.Equal("Kinnan", result[1].PlayerName);
        Assert.Equal("UG", result[1].TournamentName);
    }
}
