using System.Text.Json;
using RestSharp;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services;

/// <summary>
/// Queries EDH Top 16 for commander metagame entries used by cEDH analysis.
/// </summary>
public interface IEdhTop16Client
{
    /// <summary>
    /// Searches EDH Top 16 tournament entries for a commander using the supplied metagame filters.
    /// </summary>
    /// <param name="commanderName">Commander name to search.</param>
    /// <param name="timePeriod">EDH Top 16 time window to query.</param>
    /// <param name="sortBy">Sort order for returned entries.</param>
    /// <param name="minEventSize">Minimum tournament size to include.</param>
    /// <param name="maxStanding">Highest allowed final standing; null leaves standing unbounded.</param>
    /// <param name="count">Maximum number of entries to return.</param>
    /// <param name="cancellationToken">Token used to cancel the EDH Top 16 request.</param>
    /// <returns>A read-only list of EDH Top 16 entry rows for the commander.</returns>
    Task<IReadOnlyList<EdhTop16Entry>> SearchCommanderEntriesAsync(
        string commanderName,
        CedhMetaTimePeriod timePeriod,
        CedhMetaSortBy sortBy,
        int minEventSize,
        int? maxStanding,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the top named cEDH archetypes from the global EDH Top 16 metagame query.
    /// </summary>
    /// <param name="count">Maximum number of archetypes to return.</param>
    /// <param name="cancellationToken">Token used to cancel the EDH Top 16 request.</param>
    /// <returns>A read-only list of named archetype entries.</returns>
    Task<IReadOnlyList<EdhTop16Entry>> GetTopArchetypesAsync(int count, CancellationToken cancellationToken = default);
}

/// <inheritdoc/>
public sealed class EdhTop16Client : IEdhTop16Client
{
    private const string Endpoint = "https://edhtop16.com/api/graphql";
    private const string CommanderEntriesQuery = """
        query($name:String!,$first:Int!,$sortBy:EntriesSortBy!,$timePeriod:TimePeriod!,$minEventSize:Int!,$maxStanding:Int){
          commander(name:$name){
            name
            colorId
            entries(first:$first,sortBy:$sortBy,filters:{timePeriod:$timePeriod,minEventSize:$minEventSize,maxStanding:$maxStanding}){
              edges{
                node{
                  standing
                  wins
                  losses
                  draws
                  decklist
                  player{name}
                  tournament{name tournamentDate size TID}
                  maindeck{name type}
                }
              }
            }
          }
        }
        """;
    private const string TopArchetypesQuery = """
        query($first:Int!,$sortBy:CommandersSortBy!,$timePeriod:TimePeriod!){
          commanders(first:$first,sortBy:$sortBy,timePeriod:$timePeriod){
            edges{ node{ name colorId } }
          }
        }
        """;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<RestRequest, CancellationToken, Task<RestResponse>> _executeAsync;

    /// <summary>
    /// Initializes the EDH Top 16 client with optional HTTP execution overrides for tests.
    /// </summary>
    /// <param name="restClient">Optional RestSharp client used instead of the default endpoint client.</param>
    /// <param name="executeAsync">Optional request executor used by tests to bypass live HTTP.</param>
    public EdhTop16Client(RestClient? restClient = null, Func<RestRequest, CancellationToken, Task<RestResponse>>? executeAsync = null)
    {
        var client = restClient ?? new RestClient(new RestClientOptions(Endpoint));
        _executeAsync = executeAsync ?? ((request, cancellationToken) => client.ExecuteAsync(request, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EdhTop16Entry>> SearchCommanderEntriesAsync(
        string commanderName,
        CedhMetaTimePeriod timePeriod,
        CedhMetaSortBy sortBy,
        int minEventSize,
        int? maxStanding,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commanderName))
        {
            throw new InvalidOperationException("A commander name is required before querying EDH Top 16.");
        }

        if (count < 1)
        {
            throw new InvalidOperationException("At least one EDH Top 16 entry must be requested.");
        }

        var trimmedCommanderName = commanderName.Trim();

        var request = new RestRequest(string.Empty, Method.Post);
        request.AddHeader("Content-Type", "application/json");
        request.AddJsonBody(new
        {
            query = CommanderEntriesQuery,
            variables = new
            {
                name = trimmedCommanderName,
                first = count,
                sortBy = sortBy.ToString(),
                timePeriod = timePeriod.ToString(),
                minEventSize,
                maxStanding
            }
        });

        var response = await _executeAsync(request, cancellationToken).ConfigureAwait(false);
        var statusCode = (int)response.StatusCode;
        if (statusCode is < 200 or >= 300 || string.IsNullOrWhiteSpace(response.Content))
        {
            throw new HttpRequestException(
                $"EDH Top 16 request failed with HTTP {statusCode}.",
                null,
                response.StatusCode);
        }

        var payload = JsonSerializer.Deserialize<EdhTop16GraphQlResponse>(response.Content, JsonOptions)
            ?? throw new InvalidOperationException("EDH Top 16 returned an unreadable response payload.");

        if (payload.Errors.Count > 0)
        {
            throw new InvalidOperationException(payload.Errors[0].Message);
        }

        if (payload.Data?.Commander is null)
        {
            throw new InvalidOperationException($"No EDH Top 16 commander record was found for {trimmedCommanderName}.");
        }

        return payload.Data.Commander.Entries?.Edges?
            .Select(edge => edge.Node)
            .OfType<EdhTop16EntryNode>()
            .Select(MapEntry)
            .ToList()
            ?? new List<EdhTop16Entry>();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EdhTop16Entry>> GetTopArchetypesAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count < 1)
        {
            throw new InvalidOperationException("At least one EDH Top 16 entry must be requested.");
        }

        var request = new RestRequest(string.Empty, Method.Post);
        request.AddHeader("Content-Type", "application/json");
        request.AddJsonBody(new
        {
            query = TopArchetypesQuery,
            variables = new
            {
                first = count,
                sortBy = "POPULARITY",
                timePeriod = "SIX_MONTHS"
            }
        });

        var response = await _executeAsync(request, cancellationToken).ConfigureAwait(false);
        var statusCode = (int)response.StatusCode;
        if (statusCode is < 200 or >= 300 || string.IsNullOrWhiteSpace(response.Content))
        {
            throw new HttpRequestException(
                $"EDH Top 16 request failed with HTTP {statusCode}.",
                null,
                response.StatusCode);
        }

        var payload = JsonSerializer.Deserialize<EdhTop16TopArchetypesGraphQlResponse>(response.Content, JsonOptions)
            ?? throw new InvalidOperationException("EDH Top 16 returned an unreadable response payload.");

        if (payload.Errors.Count > 0)
        {
            throw new InvalidOperationException(payload.Errors[0].Message);
        }

        return payload.Data?.Commanders?.Edges?
            .Select(edge => edge.Node)
            .OfType<EdhTop16TopArchetypeNode>()
            .Select(MapArchetype)
            .ToList()
            ?? new List<EdhTop16Entry>();
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParse(value, out var parsedDateOnly))
        {
            return parsedDateOnly;
        }

        if (DateTimeOffset.TryParse(value, out var parsedDateTimeOffset))
        {
            return DateOnly.FromDateTime(parsedDateTimeOffset.UtcDateTime);
        }

        if (DateTime.TryParse(value, out var parsedDateTime))
        {
            return DateOnly.FromDateTime(parsedDateTime);
        }

        return null;
    }

    private static EdhTop16Entry MapEntry(EdhTop16EntryNode node)
        => new()
        {
            Standing = node.Standing,
            Wins = node.Wins,
            Losses = node.Losses,
            Draws = node.Draws,
            DecklistUrl = node.Decklist ?? string.Empty,
            PlayerName = node.Player?.Name ?? string.Empty,
            TournamentName = node.Tournament?.Name ?? string.Empty,
            TournamentId = node.Tournament?.TournamentId ?? string.Empty,
            TournamentDate = ParseDate(node.Tournament?.TournamentDate),
            TournamentSize = node.Tournament?.Size ?? 0,
            MainDeck = node.MainDeck
                .Where(card => !string.IsNullOrWhiteSpace(card.Name))
                .Select(MapCard)
                .ToList()
        };

    private static EdhTop16Card MapCard(EdhTop16CardNode card)
        => new()
        {
            Name = card.Name ?? string.Empty,
            Type = card.Type ?? string.Empty
        };

    private static EdhTop16Entry MapArchetype(EdhTop16TopArchetypeNode node)
        => new()
        {
            // Reuse EdhTop16Entry within the 31-03 scope fence: PlayerName carries the named
            // archetype label and TournamentName carries color identity for downstream formatting.
            PlayerName = node.Name ?? string.Empty,
            TournamentName = node.ColorId ?? string.Empty,
            MainDeck = Array.Empty<EdhTop16Card>()
        };

    private sealed class EdhTop16GraphQlResponse
    {
        public EdhTop16GraphQlData? Data { get; init; }

        public List<EdhTop16GraphQlError> Errors { get; init; } = new();
    }

    private sealed class EdhTop16GraphQlData
    {
        public EdhTop16CommanderNode? Commander { get; init; }
    }

    private sealed class EdhTop16TopArchetypesGraphQlResponse
    {
        public EdhTop16TopArchetypesData? Data { get; init; }

        public List<EdhTop16GraphQlError> Errors { get; init; } = new();
    }

    private sealed class EdhTop16TopArchetypesData
    {
        public EdhTop16TopArchetypeConnection? Commanders { get; init; }
    }

    private sealed class EdhTop16GraphQlError
    {
        public string Message { get; init; } = string.Empty;
    }

    private sealed class EdhTop16CommanderNode
    {
        public EdhTop16EntryConnection? Entries { get; init; }
    }

    private sealed class EdhTop16EntryConnection
    {
        public List<EdhTop16EntryEdge> Edges { get; init; } = new();
    }

    private sealed class EdhTop16EntryEdge
    {
        public EdhTop16EntryNode? Node { get; init; }
    }

    private sealed class EdhTop16EntryNode
    {
        public int Standing { get; init; }

        public int Wins { get; init; }

        public int Losses { get; init; }

        public int Draws { get; init; }

        public string? Decklist { get; init; }

        public EdhTop16PlayerNode? Player { get; init; }

        public EdhTop16TournamentNode? Tournament { get; init; }

        public List<EdhTop16CardNode> MainDeck { get; init; } = new();
    }

    private sealed class EdhTop16PlayerNode
    {
        public string? Name { get; init; }
    }

    private sealed class EdhTop16TournamentNode
    {
        public string? Name { get; init; }

        public string? TournamentDate { get; init; }

        public int Size { get; init; }

        public string? TID { get; init; }

        public string TournamentId => TID ?? string.Empty;
    }

    private sealed class EdhTop16CardNode
    {
        public string? Name { get; init; }

        public string? Type { get; init; }
    }

    private sealed class EdhTop16TopArchetypeConnection
    {
        public List<EdhTop16TopArchetypeEdge> Edges { get; init; } = new();
    }

    private sealed class EdhTop16TopArchetypeEdge
    {
        public EdhTop16TopArchetypeNode? Node { get; init; }
    }

    private sealed class EdhTop16TopArchetypeNode
    {
        public string? Name { get; init; }

        public string? ColorId { get; init; }
    }
}
