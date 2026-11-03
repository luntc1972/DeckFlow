using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using DeckFlow.Web.Services;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests EDH Top 16 persisted top-commander queries.</summary>
public sealed class EdhTop16ClientTopArchetypesTests
{
    [Fact]
    public async Task GetTopArchetypesAsync_UsesPersistedTopCommandersQuery()
    {
        RestRequest? capturedRequest = null;
        var client = new EdhTop16Client(executeAsync: (request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new RestResponse
            {
                StatusCode = HttpStatusCode.OK,
                ResponseStatus = ResponseStatus.Completed,
                Content = """{ "data": { "commanders": { "edges": [{ "node": { "name": "Blue Farm", "colorId": "WUBR" } }] } }, "errors": [] }"""
            });
        });

        var result = await client.GetTopArchetypesAsync(1);

        var archetype = Assert.Single(result);
        Assert.Equal("Blue Farm", archetype.PlayerName);
        Assert.Equal("WUBR", archetype.TournamentName);
        var body = capturedRequest!.Parameters.Single(parameter => parameter.Type == ParameterType.RequestBody).Value;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(body));
        Assert.False(document.RootElement.TryGetProperty("query", out _));
        Assert.Equal("86fac9d8c2353944c65306a21cd17574", document.RootElement.GetProperty("extensions").GetProperty("pastoria-id").GetString());
        Assert.Equal("POPULARITY", document.RootElement.GetProperty("variables").GetProperty("sortBy").GetString());
        Assert.Equal("SIX_MONTHS", document.RootElement.GetProperty("variables").GetProperty("timePeriod").GetString());
    }
}
