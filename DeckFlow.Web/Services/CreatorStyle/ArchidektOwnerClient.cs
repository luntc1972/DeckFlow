using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Registry;
using RestSharp;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>
/// Resolves creator usernames and enumerates public Archidekt deck summaries.
/// </summary>
public interface IArchidektOwnerClient
{
    /// <summary>
    /// Resolves the canonical Archidekt username from a username or profile URL.
    /// </summary>
    /// <param name="usernameOrUrl">Username or Archidekt profile URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved username when found; otherwise <see langword="null"/>.</returns>
    Task<string?> ResolveUsernameAsync(string usernameOrUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists public deck summaries for an Archidekt owner.
    /// </summary>
    /// <param name="ownerUsername">Owner username.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovered deck summaries.</returns>
    Task<IReadOnlyList<ArchidektDeckSummary>> ListDeckSummariesAsync(string ownerUsername, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches Archidekt owner metadata and public deck summaries with bounded JSON parsing.
/// </summary>
public sealed class ArchidektOwnerClient : IArchidektOwnerClient
{
    internal const int MaxPages = 20;
    internal const int MaxDecks = 500;
    internal const int PageSize = 50;
    internal const int MaxResponseBytes = 5 * 1024 * 1024;

    private readonly RestClient _restClient;
    private readonly ResiliencePipeline<RestResponse> _resiliencePipeline;
    private readonly ILogger<ArchidektOwnerClient> _logger;

    /// <summary>
    /// Creates an Archidekt owner client.
    /// </summary>
    /// <param name="httpClientFactory">Named HTTP client factory.</param>
    /// <param name="pipelineProvider">Named resilience pipeline provider.</param>
    /// <param name="logger">Optional logger.</param>
    public ArchidektOwnerClient(
        IHttpClientFactory httpClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<ArchidektOwnerClient>? logger = null)
        : this(
            pipelineProvider,
            new RestClient(httpClientFactory.CreateClient("archidekt-owner")),
            logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
    }

    internal ArchidektOwnerClient(
        ResiliencePipelineProvider<string> pipelineProvider,
        RestClient restClient,
        ILogger<ArchidektOwnerClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(restClient);
        _restClient = restClient;
        _resiliencePipeline = pipelineProvider.GetPipeline<RestResponse>("archidekt") ?? ResiliencePipeline<RestResponse>.Empty;
        _logger = logger ?? NullLogger<ArchidektOwnerClient>.Instance;
    }

    /// <inheritdoc />
    public async Task<string?> ResolveUsernameAsync(string usernameOrUrl, CancellationToken cancellationToken = default)
    {
        if (!ArchidektOwnerUrl.TryGetUsername(usernameOrUrl, out var requestedUsername))
        {
            return null;
        }

        var request = new RestRequest("api/users/", Method.Get);
        request.AddQueryParameter("username", requestedUsername);
        request.AddHeader("Accept", "application/json");

        var response = await _resiliencePipeline.ExecuteAsync(
            async ct => await _restClient.ExecuteAsync(request, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessful)
        {
            _logger.LogWarning(
                "Archidekt owner resolve failed for {Username}: HTTP {StatusCode}.",
                requestedUsername,
                (int)response.StatusCode);
            return null;
        }

        if (!OwnerClientJson.TryGetResponseContent(
                response,
                MaxResponseBytes,
                out var content,
                byteCount => _logger.LogWarning(
                    "Archidekt owner {Operation} payload exceeded the {MaxResponseBytes} byte cap ({ByteCount}).",
                    "resolve",
                    MaxResponseBytes,
                    byteCount)))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array
                || results.GetArrayLength() == 0)
            {
                return null;
            }

            var first = results[0];
            if (!first.TryGetProperty("username", out var usernameElement))
            {
                return null;
            }

            return usernameElement.GetString();
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Archidekt owner resolve returned malformed JSON for {Username}.", requestedUsername);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ArchidektDeckSummary>> ListDeckSummariesAsync(string ownerUsername, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUsername);

        var decks = new List<ArchidektDeckSummary>();
        var page = 1;
        string? next = string.Empty;

        while (page <= MaxPages && decks.Count < MaxDecks && next is not null)
        {
            var request = new RestRequest("api/decks/v3/", Method.Get);
            request.AddQueryParameter("ownerUsername", ownerUsername);
            request.AddQueryParameter("pageSize", PageSize);
            request.AddQueryParameter("page", page);
            request.AddHeader("Accept", "application/json");

            var response = await _resiliencePipeline.ExecuteAsync(
                async ct => await _restClient.ExecuteAsync(request, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessful)
            {
                _logger.LogWarning(
                    "Archidekt owner deck list failed for {Username} page {Page}: HTTP {StatusCode}.",
                    ownerUsername,
                    page,
                    (int)response.StatusCode);
                return Array.Empty<ArchidektDeckSummary>();
            }

            if (!OwnerClientJson.TryGetResponseContent(
                    response,
                    MaxResponseBytes,
                    out var content,
                    byteCount => _logger.LogWarning(
                        "Archidekt owner {Operation} payload exceeded the {MaxResponseBytes} byte cap ({ByteCount}).",
                        "list",
                        MaxResponseBytes,
                        byteCount)))
            {
                return Array.Empty<ArchidektDeckSummary>();
            }

            try
            {
                using var document = JsonDocument.Parse(content);
                next = document.RootElement.TryGetProperty("next", out var nextElement) && nextElement.ValueKind != JsonValueKind.Null
                    ? nextElement.GetString()
                    : null;

                if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                {
                    break;
                }

                foreach (var item in results.EnumerateArray())
                {
                    var summary = new ArchidektDeckSummary
                    {
                        Id = OwnerClientJson.ReadString(item, "id"),
                        Name = OwnerClientJson.ReadString(item, "name"),
                        Size = OwnerClientJson.ReadInt32(item, "size"),
                        ParentFolderId = OwnerClientJson.ReadNullableInt32(item, "parentFolderId"),
                        ParentFolderName = OwnerClientJson.ReadNullableString(item, "parentFolderName")
                    };

                    if (string.IsNullOrWhiteSpace(summary.Id) || string.IsNullOrWhiteSpace(summary.Name))
                    {
                        continue;
                    }

                    decks.Add(summary);
                    if (decks.Count == MaxDecks)
                    {
                        break;
                    }
                }
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Archidekt owner deck list returned malformed JSON for {Username} page {Page}.", ownerUsername, page);
                return Array.Empty<ArchidektDeckSummary>();
            }

            page += 1;
        }

        return decks;
    }
}

/// <summary>
/// Lightweight summary of an owner's public Archidekt deck.
/// </summary>
public sealed record ArchidektDeckSummary
{
    /// <summary>Stable deck identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Deck size reported by Archidekt.</summary>
    public required int Size { get; init; }

    /// <summary>Optional parent-folder identifier.</summary>
    public int? ParentFolderId { get; init; }

    /// <summary>Optional parent-folder display name.</summary>
    public string? ParentFolderName { get; init; }
}
