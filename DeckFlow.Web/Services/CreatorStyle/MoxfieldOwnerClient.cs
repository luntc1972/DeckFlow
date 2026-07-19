using System.Net.Http;
using System.Text.Json;
using Polly;
using Polly.Registry;
using RestSharp;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>
/// Lists public commander deck summaries for a Moxfield author.
/// </summary>
public interface IMoxfieldOwnerClient
{
    /// <summary>
    /// Lists public commander deck summaries for a Moxfield username.
    /// </summary>
    /// <param name="username">Moxfield username.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The discovered deck summaries.</returns>
    Task<IReadOnlyList<MoxfieldDeckSummary>> ListDeckSummariesAsync(string username, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches Moxfield author deck summaries with bounded pagination and browser-mimicking headers.
/// </summary>
public sealed class MoxfieldOwnerClient : IMoxfieldOwnerClient
{
    internal const int MaxPages = 10;
    internal const int PageSize = 50;
    internal const int MaxResponseBytes = 5 * 1024 * 1024;

    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(500);

    private readonly RestClient _restClient;
    private readonly ResiliencePipeline<RestResponse> _resiliencePipeline;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastRequestUtc = DateTime.MinValue;

    /// <summary>
    /// Creates a Moxfield owner client.
    /// </summary>
    /// <param name="httpClientFactory">Named HTTP client factory.</param>
    /// <param name="pipelineProvider">Named resilience pipeline provider.</param>
    public MoxfieldOwnerClient(
        IHttpClientFactory httpClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider)
        : this(
            pipelineProvider,
            new RestClient(httpClientFactory.CreateClient("moxfield-owner")))
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
    }

    internal MoxfieldOwnerClient(
        ResiliencePipelineProvider<string> pipelineProvider,
        RestClient restClient)
    {
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(restClient);
        _restClient = restClient;
        _resiliencePipeline = pipelineProvider.GetPipeline<RestResponse>("moxfield") ?? ResiliencePipeline<RestResponse>.Empty;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MoxfieldDeckSummary>> ListDeckSummariesAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var decks = new List<MoxfieldDeckSummary>();

        for (var pageNumber = 1; pageNumber <= MaxPages; pageNumber++)
        {
            var request = new RestRequest("v2/decks/search", Method.Get);
            request.AddQueryParameter("authorUserNames", username);
            request.AddQueryParameter("pageNumber", pageNumber);
            request.AddQueryParameter("pageSize", PageSize);
            request.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
            request.AddHeader("Accept", "application/json, text/plain, */*");
            request.AddHeader("Referer", "https://moxfield.com/");
            request.AddHeader("Accept-Language", "en-US,en;q=0.9");

            var response = await ExecuteWithThrottleAsync(
                async ct => await _resiliencePipeline.ExecuteAsync(
                    async innerCt => await _restClient.ExecuteAsync(request, innerCt).ConfigureAwait(false),
                    ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessful)
            {
                var body = response.Content ?? string.Empty;
                throw new HttpRequestException(
                    $"Moxfield owner deck list for {username} page {pageNumber} returned HTTP {(int)response.StatusCode} {response.StatusDescription}: {body[..Math.Min(body.Length, 500)]}",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            if (!OwnerClientJson.TryGetResponseContent(response, MaxResponseBytes, out var content))
            {
                return Array.Empty<MoxfieldDeckSummary>();
            }

            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var totalPages = OwnerClientJson.ReadInt32(root, "totalPages");

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var item in data.EnumerateArray())
            {
                var summary = new MoxfieldDeckSummary(
                    OwnerClientJson.ReadString(item, "publicId"),
                    OwnerClientJson.ReadString(item, "name"),
                    OwnerClientJson.ReadString(item, "format"),
                    OwnerClientJson.ReadNullableString(item, "visibility"));

                if (string.IsNullOrWhiteSpace(summary.PublicId)
                    || string.IsNullOrWhiteSpace(summary.Name)
                    || !string.Equals(summary.Format, "commander", StringComparison.OrdinalIgnoreCase)
                    || (summary.Visibility is not null && !string.Equals(summary.Visibility, "public", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                decks.Add(summary);
            }

            if (totalPages <= pageNumber)
            {
                break;
            }
        }

        return decks;
    }

    private async Task<RestResponse> ExecuteWithThrottleAsync(
        Func<CancellationToken, Task<RestResponse>> execute,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var elapsedSinceLast = DateTime.UtcNow - _lastRequestUtc;
            if (elapsedSinceLast < MinInterval)
            {
                await Task.Delay(MinInterval - elapsedSinceLast, cancellationToken).ConfigureAwait(false);
            }

            var result = await execute(cancellationToken).ConfigureAwait(false);
            _lastRequestUtc = DateTime.UtcNow;
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>
/// Minimal Moxfield deck summary used by creator-style crawls.
/// </summary>
/// <param name="PublicId">Deck public id used for import.</param>
/// <param name="Name">Deck display name.</param>
/// <param name="Format">Deck format.</param>
/// <param name="Visibility">Deck visibility; null means treat as public.</param>
public sealed record MoxfieldDeckSummary(string PublicId, string Name, string Format, string? Visibility);
