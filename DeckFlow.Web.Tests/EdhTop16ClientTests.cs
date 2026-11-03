using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests persisted-query requests and response handling for <see cref="EdhTop16Client"/>.</summary>
public sealed class EdhTop16ClientTests
{
    [Fact]
    public async Task SearchCommanderEntriesAsync_UsesPersistedQueryAndLoadsDecklist()
    {
        var requests = new List<RestRequest>();
        var client = new EdhTop16Client(executeAsync: (request, _) =>
        {
            requests.Add(request);
            return Task.FromResult(Response(requests.Count == 1
                ? """{ "data": { "commander": { "entries": { "edges": [{ "node": { "id": "RW50cnk6NDI=", "standing": 1, "wins": 4, "losses": 1, "draws": 1, "decklist": "https://edhtop16.com/deck/1", "player": { "name": "Alice" }, "tournament": { "name": "Rocky Mountain Showdown", "tournamentDate": "2026-04-10T13:45:00.000Z", "size": 84, "TID": "abc123" } } }] } } }, "errors": [] }"""
                : """{ "data": { "entry": { "decklistItems": [{ "count": 1, "card": { "name": "Mana Crypt", "type": "Artifact" } }] } }, "errors": [] }"""));
        });

        var result = await client.SearchCommanderEntriesAsync(" Tymna the Weaver ", CedhMetaTimePeriod.ONE_YEAR, CedhMetaSortBy.TOP, 50, 16, 1, CancellationToken.None);

        var entry = Assert.Single(result);
        Assert.Equal("Alice", entry.PlayerName);
        Assert.Equal("abc123", entry.TournamentId);
        var card = Assert.Single(entry.MainDeck);
        Assert.Equal("Mana Crypt", card.Name);
        Assert.Equal("Artifact", card.Type);
        Assert.Equal(2, requests.Count);
        AssertPersistedRequest(requests[0], "124b6abe467f15a68b2a706f3e192fd5", "commander", "Tymna the Weaver");
        AssertPersistedRequest(requests[1], "9e246f3401cf45ec6c7ee849dff18c16", "entry", 42);
    }

    [Fact]
    public async Task SearchCommanderEntriesAsync_ThrowsPlainGraphQlErrorMessage()
    {
        var client = new EdhTop16Client(executeAsync: (_, _) => Task.FromResult(Response("""{ "errors": ["Persisted query required"] }""")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SearchCommanderEntriesAsync("Kinnan", CedhMetaTimePeriod.ONE_YEAR, CedhMetaSortBy.TOP, 50, null, 1));

        Assert.Equal("Persisted query required", exception.Message);
    }

    [Fact]
    public async Task SearchCommanderEntriesAsync_KeepsEntryWhenDecklistRequestFails()
    {
        var requestCount = 0;
        var client = new EdhTop16Client(executeAsync: (_, _) => Task.FromResult(++requestCount == 1
            ? Response("""{ "data": { "commander": { "entries": { "edges": [{ "node": { "id": "RW50cnk6NDI=", "decklist": "https://edhtop16.com/deck/1" } }] } } } }""")
            : new RestResponse { StatusCode = HttpStatusCode.BadGateway, ResponseStatus = ResponseStatus.Completed }));

        var result = await client.SearchCommanderEntriesAsync("Kinnan", CedhMetaTimePeriod.ONE_YEAR, CedhMetaSortBy.TOP, 50, null, 1);

        Assert.Empty(Assert.Single(result).MainDeck);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task SearchCommanderEntriesAsync_RejectsNonPositiveCount()
    {
        var client = new EdhTop16Client();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SearchCommanderEntriesAsync("Kinnan", CedhMetaTimePeriod.ONE_YEAR, CedhMetaSortBy.TOP, 50, null, 0));
    }

    private static RestResponse Response(string content) => new()
    {
        StatusCode = HttpStatusCode.OK,
        ResponseStatus = ResponseStatus.Completed,
        Content = content
    };

    private static void AssertPersistedRequest(RestRequest request, string id, string variableName, object variableValue)
    {
        var body = request.Parameters.Single(parameter => parameter.Type == ParameterType.RequestBody).Value;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(body));
        Assert.False(document.RootElement.TryGetProperty("query", out _));
        Assert.Equal(id, document.RootElement.GetProperty("extensions").GetProperty("pastoria-id").GetString());
        Assert.Equal(JsonSerializer.Serialize(variableValue), document.RootElement.GetProperty("variables").GetProperty(variableName).GetRawText());
    }
}
