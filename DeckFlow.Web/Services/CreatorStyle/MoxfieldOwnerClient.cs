using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Registry;
using Polly.Timeout;
using RestSharp;

namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>Fetches public Moxfield deck summaries using bounded, paced requests.</summary>
public sealed class MoxfieldOwnerClient : IMoxfieldOwnerClient
{
    internal const int MaxPages = 10;
    internal const int PageSize = 50;
    /// <summary>Maximum response payload accepted from Moxfield.</summary>
    public const int MaxResponseBytes = 5 * 1024 * 1024;
    /// <summary>Browser user agent accepted by Moxfield's Cloudflare front.</summary>
    public const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36";

    private readonly RestClient _restClient;
    private readonly ResiliencePipeline<RestResponse> _pipeline;
    private readonly MoxfieldRequestPacer _pacer;
    private readonly ILogger<MoxfieldOwnerClient> _logger;

    /// <summary>Creates a client using the named Moxfield HTTP client and pipeline.</summary>
    public MoxfieldOwnerClient(IHttpClientFactory httpClientFactory, ResiliencePipelineProvider<string> pipelineProvider, MoxfieldRequestPacer pacer, ILogger<MoxfieldOwnerClient>? logger = null)
        : this(pipelineProvider, new RestClient(httpClientFactory.CreateClient("moxfield-owner"), new RestClientOptions
        {
            BaseUrl = new Uri("https://api2.moxfield.com/"),
            // Why: RestSharp's default agent overrides the named client's browser agent.
            UserAgent = null,
        }), pacer, logger)
    { }

    internal MoxfieldOwnerClient(ResiliencePipelineProvider<string> pipelineProvider, RestClient restClient, MoxfieldRequestPacer pacer, ILogger<MoxfieldOwnerClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(restClient);
        ArgumentNullException.ThrowIfNull(pacer);
        _pipeline = pipelineProvider.GetPipeline<RestResponse>("moxfield");
        _restClient = restClient;
        _pacer = pacer;
        _logger = logger ?? NullLogger<MoxfieldOwnerClient>.Instance;
    }

    /// <inheritdoc />
    public async Task<MoxfieldDeckListResult> ListDeckSummariesAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        var decks = new List<MoxfieldDeckSummary>();
        var publicIds = new HashSet<string>(StringComparer.Ordinal);
        var isTruncated = false;
        for (var page = 1; page <= MaxPages; page++)
        {
            var request = new RestRequest("v2/decks/search", Method.Get);
            request.AddQueryParameter("authorUserNames", username);
            request.AddQueryParameter("pageNumber", page);
            request.AddQueryParameter("pageSize", PageSize);
            RestResponse response;
            try
            {
                response = await _pipeline.ExecuteAsync(
                    async ct => await _pacer.RunAsync(innerCt => _restClient.ExecuteAsync(request, innerCt), ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Moxfield listing JSON failed for {Username} page {Page}.", username, page);
                return Failed(decks);
            }
            catch (TimeoutRejectedException exception)
            {
                _logger.LogWarning(exception, "Moxfield listing timed out for {Username} page {Page}.", username, page);
                return Failed(decks);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Moxfield listing was cancelled by the pipeline for {Username} page {Page}.", username, page);
                return Failed(decks);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!response.IsSuccessful)
            {
                _logger.LogWarning("Moxfield listing failed for {Username} page {Page}: HTTP {StatusCode}.", username, page, (int)response.StatusCode);
                return Failed(decks);
            }
            var bytes = response.RawBytes ?? Encoding.UTF8.GetBytes(response.Content ?? string.Empty);
            if (bytes.Length > MaxResponseBytes)
            {
                _logger.LogWarning("Moxfield listing response for {Username} page {Page} exceeded {MaxResponseBytes} bytes.", username, page, MaxResponseBytes);
                return Failed(decks);
            }
            try
            {
                using var document = JsonDocument.Parse(bytes);
                if (!TryReadPage(document.RootElement, out var totalPages, out var summaries))
                {
                    _logger.LogWarning("Moxfield listing returned an invalid page shape for {Username} page {Page}.", username, page);
                    return Failed(decks);
                }
                foreach (var summary in summaries)
                {
                    if (publicIds.Add(summary.PublicId)) { decks.Add(summary); }
                }
                if (page >= totalPages) { break; }
                isTruncated = page == MaxPages;
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Moxfield listing JSON failed for {Username} page {Page}.", username, page);
                return Failed(decks);
            }
        }
        return new MoxfieldDeckListResult { Decks = decks, HasUpstreamFailure = false, IsTruncated = isTruncated };
    }

    private static MoxfieldDeckListResult Failed(IReadOnlyList<MoxfieldDeckSummary> decks) => new() { Decks = decks, HasUpstreamFailure = true };

    private static bool TryReadPage(JsonElement root, out int totalPages, out List<MoxfieldDeckSummary> decks)
    {
        totalPages = 0;
        decks = [];
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("totalPages", out var pages) || pages.ValueKind != JsonValueKind.Number || !pages.TryGetInt32(out totalPages) || totalPages < 0 || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) { return false; }
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !TryGetString(item, "publicId", out var id) || !TryGetString(item, "name", out var name) || !TryGetString(item, "format", out var format)) { continue; }
            string? visibility = null;
            if (item.TryGetProperty("visibility", out var value))
            {
                if (value.ValueKind != JsonValueKind.String) { continue; }
                visibility = value.GetString();
            }
            if (string.Equals(format, "commander", StringComparison.OrdinalIgnoreCase) && (visibility is null || string.Equals(visibility, "public", StringComparison.OrdinalIgnoreCase))) { decks.Add(new MoxfieldDeckSummary { PublicId = id, Name = name, Format = format, Visibility = visibility }); }
        }
        return true;
    }

    private static bool TryGetString(JsonElement item, string property, out string value)
    {
        value = string.Empty;
        if (!item.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var stringValue = element.GetString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return false;
        }

        value = stringValue;
        return true;
    }
}
