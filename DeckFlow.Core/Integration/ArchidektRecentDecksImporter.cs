using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using RestSharp;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Fetches recent public Archidekt deck IDs for the knowledge-cache harvest job.
/// </summary>
public interface IArchidektRecentDecksImporter
{
    /// <summary>
    /// Imports the requested number of recent public Archidekt deck IDs.
    /// </summary>
    /// <param name="count">Number of deck identifiers to collect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The imported recent deck identifiers.</returns>
    Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default);
    /// <summary>
    /// Imports the requested number of recent public Archidekt deck IDs starting from the supplied page.
    /// </summary>
    /// <param name="count">Number of deck identifiers to collect.</param>
    /// <param name="startPage">Page number to start crawling from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The imported recent deck identifiers.</returns>
    Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default);
    /// <summary>
    /// Imports one page of recent public Archidekt deck IDs.
    /// </summary>
    /// <param name="page">Page index to request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deck identifiers found on the requested page.</returns>
    Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default);
}

/// <summary>
/// Crawls Archidekt's recent-decks endpoint paced through <see cref="ArchidektThrottle"/>.
/// </summary>
public sealed class ArchidektRecentDecksImporter : IArchidektRecentDecksImporter
{
    private readonly RestClient _restClient;
    /// <summary>
    /// Initializes the importer optionally using a provided RestClient.
    /// </summary>
    /// <param name="restClient">Optional REST client used for requests.</param>
    public ArchidektRecentDecksImporter(RestClient? restClient = null)
    {
        _restClient = restClient ?? new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("https://archidekt.com"),
            ThrowOnAnyError = false,
            // RestSharp would otherwise send its own User-Agent as a second value.
            UserAgent = null,
        });
    }

    /// <summary>
    /// Imports the requested number of recent public Archidekt deck IDs.
    /// </summary>
    /// <param name="count">Number of decks to collect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, CancellationToken cancellationToken = default)
    {
        return await ImportRecentDeckIdsAsync(count, 1, cancellationToken);
    }

    /// <summary>
    /// Imports the requested number of recent public Archidekt deck IDs starting from the supplied page.
    /// </summary>
    /// <param name="count">Number of decks to collect.</param>
    /// <param name="startPage">Page number to start crawling from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var deckIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var page = Math.Max(1, startPage);

        while (deckIds.Count < count)
        {
            var pageIds = await ImportRecentDeckIdsPageAsync(page, cancellationToken);
            if (pageIds.Count == 0)
            {
                break;
            }

            foreach (var deckId in pageIds)
            {
                if (seen.Add(deckId))
                {
                    deckIds.Add(deckId);
                    if (deckIds.Count == count)
                    {
                        break;
                    }
                }
            }

            page += 1;
        }

        return deckIds;
    }

    /// <summary>
    /// Fetches a single page of recent public Archidekt deck IDs.
    /// </summary>
    /// <param name="page">Page index to request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<string>> ImportRecentDeckIdsPageAsync(int page, CancellationToken cancellationToken = default)
        => ImportRecentDeckIdsPageCoreAsync(page, cancellationToken);

    /// <summary>
    /// Fetches a page of recent deck IDs from Archidekt.
    /// </summary>
    /// <param name="page">Page index to request.</param>
    /// <param name="cancellationToken">Cancellation token for the HTTP call.</param>
    private async Task<IReadOnlyList<string>> ImportRecentDeckIdsPageCoreAsync(int page, CancellationToken cancellationToken)
    {
        var response = await ArchidektThrottle.ExecuteAsync(_restClient, () => CreatePageRequest(page), cancellationToken);
        if (!response.IsSuccessful)
        {
            throw new HttpRequestException($"Archidekt recent decks page {page} returned {(int)response.StatusCode} {response.StatusDescription}");
        }

        var pageResponse = JsonSerializer.Deserialize<ArchidektRecentDecksResponse>(response.Content ?? string.Empty);
        return (pageResponse?.Results ?? [])
            .Select(deck => deck.Id.ToString(CultureInfo.InvariantCulture))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Builds the request used to fetch a page of public deck listings.
    /// </summary>
    /// <param name="page">Page index to request.</param>
    private static RestRequest CreatePageRequest(int page)
    {
        return new RestRequest($"/api/decks/v3/?orderBy=-updatedAt&page={page}", Method.Get)
            .AddHeader("User-Agent", ArchidektUserAgent.Value);
    }

    /// <summary>Deserializes one Archidekt recent-decks page so its deck IDs can be queued for import.</summary>
    private sealed record ArchidektRecentDecksResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<ArchidektRecentDeck> Results);

    /// <summary>Extracts the deck ID needed to enqueue an Archidekt recent-deck result.</summary>
    private sealed record ArchidektRecentDeck(
        [property: JsonPropertyName("id")] int Id);
}
