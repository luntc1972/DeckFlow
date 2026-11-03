using System.Text;
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
    private const string CommanderEntriesQueryId = "124b6abe467f15a68b2a706f3e192fd5";
    private const string TopCommandersQueryId = "86fac9d8c2353944c65306a21cd17574";
    private const string DecklistQueryId = "9e246f3401cf45ec6c7ee849dff18c16";
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

        var payload = await PostPersistedQueryAsync<EdhTop16GraphQlResponse>(
            CommanderEntriesQueryId,
            new
            {
                commander = trimmedCommanderName,
                sortBy = sortBy.ToString(),
                timePeriod = timePeriod.ToString(),
                minEventSize,
                maxStanding
            },
            cancellationToken).ConfigureAwait(false);

        if (payload.Data?.Commander is null)
        {
            throw new InvalidOperationException($"No EDH Top 16 commander record was found for {trimmedCommanderName}.");
        }

        var nodes = payload.Data.Commander.Entries?.Edges?
            .Select(edge => edge.Node)
            .OfType<EdhTop16EntryNode>()
            .Take(count)
            .ToList()
            ?? new List<EdhTop16EntryNode>();

        var entries = new List<EdhTop16Entry>(nodes.Count);
        foreach (var node in nodes)
        {
            var mainDeck = await GetDecklistAsync(node.Id, cancellationToken).ConfigureAwait(false);
            entries.Add(MapEntry(node, mainDeck));
        }

        return entries;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EdhTop16Entry>> GetTopArchetypesAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count < 1)
        {
            throw new InvalidOperationException("At least one EDH Top 16 entry must be requested.");
        }

        var payload = await PostPersistedQueryAsync<EdhTop16TopArchetypesGraphQlResponse>(
            TopCommandersQueryId,
            new { sortBy = "POPULARITY", timePeriod = "SIX_MONTHS" },
            cancellationToken).ConfigureAwait(false);

        return payload.Data?.Commanders?.Edges?
            .Select(edge => edge.Node)
            .OfType<EdhTop16TopArchetypeNode>()
            .Select(MapArchetype)
            .Take(count)
            .ToList()
            ?? new List<EdhTop16Entry>();
    }

    private async Task<IReadOnlyList<EdhTop16Card>> GetDecklistAsync(string? encodedEntryId, CancellationToken cancellationToken)
    {
        if (!TryDecodeEntryId(encodedEntryId, out var entryId))
        {
            return Array.Empty<EdhTop16Card>();
        }

        try
        {
            var payload = await PostPersistedQueryAsync<EdhTop16DecklistGraphQlResponse>(
                DecklistQueryId, new { entry = entryId }, cancellationToken).ConfigureAwait(false);
            if (payload.Data?.Entry is null)
            {
                return Array.Empty<EdhTop16Card>();
            }

            return payload.Data.Entry.DecklistItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Card?.Name))
                .SelectMany(item => Enumerable.Repeat(MapCard(item.Card!), Math.Max(item.Count, 1)))
                .ToList();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A missing decklist must not hide an otherwise useful tournament finish.
            return Array.Empty<EdhTop16Card>();
        }
    }

    private async Task<T> PostPersistedQueryAsync<T>(string queryId, object variables, CancellationToken cancellationToken)
        where T : EdhTop16Response, new()
    {
        var request = new RestRequest(string.Empty, Method.Post);
        request.AddHeader("Content-Type", "application/json");
        request.AddJsonBody(new
        {
            variables,
            extensions = new Dictionary<string, string> { ["pastoria-id"] = queryId }
        });

        // Decklist response failures return empty data even when cancellation has been requested.
        var isDecklist = queryId == DecklistQueryId;
        var response = await _executeAsync(request, cancellationToken).ConfigureAwait(false);
        var statusCode = (int)response.StatusCode;
        if (statusCode is < 200 or >= 300 || string.IsNullOrWhiteSpace(response.Content))
        {
            if (isDecklist)
            {
                return new T();
            }

            throw new HttpRequestException(
                $"EDH Top 16 request failed with HTTP {statusCode}.",
                null,
                response.StatusCode);
        }

        var payload = JsonSerializer.Deserialize<T>(response.Content, JsonOptions);
        if (payload is null)
        {
            return isDecklist
                ? new T()
                : throw new InvalidOperationException("EDH Top 16 returned an unreadable response payload.");
        }

        if (payload.Errors.Count > 0)
        {
            return isDecklist
                ? new T()
                : throw new InvalidOperationException(GetErrorMessage(payload.Errors[0]));
        }

        return payload;
    }

    private static bool TryDecodeEntryId(string? encodedEntryId, out int entryId)
    {
        entryId = 0;
        if (string.IsNullOrWhiteSpace(encodedEntryId))
        {
            return false;
        }

        try
        {
            var decodedId = Encoding.UTF8.GetString(Convert.FromBase64String(encodedEntryId));
            return decodedId.StartsWith("Entry:", StringComparison.Ordinal) && int.TryParse(decodedId["Entry:".Length..], out entryId);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string GetErrorMessage(JsonElement error)
        => error.ValueKind == JsonValueKind.String
            ? error.GetString() ?? "EDH Top 16 returned an unspecified GraphQL error."
            : error.TryGetProperty("message", out var message)
                ? message.GetString() ?? "EDH Top 16 returned an unspecified GraphQL error."
                : "EDH Top 16 returned an unspecified GraphQL error.";

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

    private static EdhTop16Entry MapEntry(EdhTop16EntryNode node, IReadOnlyList<EdhTop16Card> mainDeck)
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
            MainDeck = mainDeck
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

    /// <summary>Holds GraphQL errors shared by persisted-query responses.</summary>
    private abstract class EdhTop16Response
    {
        public List<JsonElement> Errors { get; init; } = new();
    }

    /// <summary>Deserializes EDHTop16 commander-entry data and GraphQL errors from the remote response.</summary>
    private sealed class EdhTop16GraphQlResponse : EdhTop16Response
    {
        public EdhTop16GraphQlData? Data { get; init; }
    }

    /// <summary>Holds the commander node returned by an EDHTop16 deck-entry query.</summary>
    private sealed class EdhTop16GraphQlData
    {
        public EdhTop16CommanderNode? Commander { get; init; }
    }

    /// <summary>Deserializes EDHTop16 archetype data and GraphQL errors from the remote response.</summary>
    private sealed class EdhTop16TopArchetypesGraphQlResponse : EdhTop16Response
    {
        public EdhTop16TopArchetypesData? Data { get; init; }
    }

    /// <summary>Holds the commander connection returned by an EDHTop16 top-archetypes query.</summary>
    private sealed class EdhTop16TopArchetypesData
    {
        public EdhTop16TopArchetypeConnection? Commanders { get; init; }
    }

    /// <summary>Deserializes decklist cards for a single EDHTop16 tournament entry.</summary>
    private sealed class EdhTop16DecklistGraphQlResponse : EdhTop16Response
    {
        public EdhTop16DecklistGraphQlData? Data { get; init; }
    }

    /// <summary>Holds the entry node returned by an EDHTop16 decklist query.</summary>
    private sealed class EdhTop16DecklistGraphQlData
    {
        public EdhTop16DecklistEntryNode? Entry { get; init; }
    }

    /// <summary>Collects card quantities and cards returned by an EDHTop16 decklist query.</summary>
    private sealed class EdhTop16DecklistEntryNode
    {
        public List<EdhTop16DecklistItemNode> DecklistItems { get; init; } = new();
    }

    /// <summary>Holds one EDHTop16 decklist card and its count.</summary>
    private sealed class EdhTop16DecklistItemNode
    {
        public int Count { get; init; }

        public EdhTop16CardNode? Card { get; init; }
    }

    /// <summary>Provides the tournament-entry connection nested under an EDHTop16 commander query.</summary>
    private sealed class EdhTop16CommanderNode
    {
        public EdhTop16EntryConnection? Entries { get; init; }
    }

    /// <summary>Collects EDHTop16 tournament-entry edges for a queried commander.</summary>
    private sealed class EdhTop16EntryConnection
    {
        public List<EdhTop16EntryEdge> Edges { get; init; } = new();
    }

    /// <summary>Wraps an optional EDHTop16 tournament entry returned through a GraphQL connection.</summary>
    private sealed class EdhTop16EntryEdge
    {
        public EdhTop16EntryNode? Node { get; init; }
    }

    /// <summary>Captures placement, record, player, event, and deck cards for one EDHTop16 tournament entry.</summary>
    private sealed class EdhTop16EntryNode
    {
        public string? Id { get; init; }

        public int Standing { get; init; }

        public int Wins { get; init; }

        public int Losses { get; init; }

        public int Draws { get; init; }

        public string? Decklist { get; init; }

        public EdhTop16PlayerNode? Player { get; init; }

        public EdhTop16TournamentNode? Tournament { get; init; }

    }

    /// <summary>Provides the player name attached to an EDHTop16 tournament entry.</summary>
    private sealed class EdhTop16PlayerNode
    {
        public string? Name { get; init; }
    }

    /// <summary>Provides event identity, date, and field size for an EDHTop16 tournament entry.</summary>
    private sealed class EdhTop16TournamentNode
    {
        public string? Name { get; init; }

        public string? TournamentDate { get; init; }

        public int Size { get; init; }

        public string? TID { get; init; }

        public string TournamentId => TID ?? string.Empty;
    }

    /// <summary>Provides card name and board type from an EDHTop16 decklist.</summary>
    private sealed class EdhTop16CardNode
    {
        public string? Name { get; init; }

        public string? Type { get; init; }
    }

    /// <summary>Collects commander edges returned by the EDHTop16 top-archetypes query.</summary>
    private sealed class EdhTop16TopArchetypeConnection
    {
        public List<EdhTop16TopArchetypeEdge> Edges { get; init; } = new();
    }

    /// <summary>Wraps an optional commander archetype returned through an EDHTop16 GraphQL connection.</summary>
    private sealed class EdhTop16TopArchetypeEdge
    {
        public EdhTop16TopArchetypeNode? Node { get; init; }
    }

    /// <summary>Provides commander and color identity for an EDHTop16 archetype result.</summary>
    private sealed class EdhTop16TopArchetypeNode
    {
        public string? Name { get; init; }

        public string? ColorId { get; init; }
    }
}
