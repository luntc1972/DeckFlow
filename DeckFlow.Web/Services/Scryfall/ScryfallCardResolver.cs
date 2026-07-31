using System.Net;
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Http;
using Polly;
using Polly.Registry;
using RestSharp;

namespace DeckFlow.Web.Services.Scryfall;

/// <summary>
/// Resolves Scryfall card references for packet services.
/// </summary>
public interface IScryfallCardResolver
{
    /// <summary>
    /// Executes a single Scryfall collection request and returns the raw response.
    /// </summary>
    Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Performs the shared exact-name fallback search used when collection lookup misses a card.
    /// </summary>
    Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken);

    /// <summary>
    /// Performs the analysis-specific printed-name fallback search used when collection lookup misses a card.
    /// </summary>
    Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a single card by name: an exact collection lookup with a normalized-name match, falling
    /// back to the exact-name search when the collection misses. Returns null when nothing matches.
    /// </summary>
    Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken);
}

/// <summary>
/// Executes Scryfall collection and fallback-search requests through the shared throttle and resilience pipeline.
/// </summary>
public sealed class ScryfallCardResolver : IScryfallCardResolver
{
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>> _executeCollectionAsync;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>> _executeSearchAsync;
    private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>> _executeNamedAsync;

    /// <summary>
    /// Creates a resolver using the DI-managed Scryfall client factory and resilience pipeline.
    /// </summary>
    public ScryfallCardResolver(
        IScryfallRestClientFactory scryfallRestClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider)
        : this(
            scryfallRestClientFactory,
            pipelineProvider,
            null,
            null,
            null,
            null)
    {
    }

    internal ScryfallCardResolver(
        IScryfallRestClientFactory scryfallRestClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        RestClient? restClientOverride = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsyncOverride = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsyncOverride = null,
        Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsyncOverride = null)
    {
        ArgumentNullException.ThrowIfNull(scryfallRestClientFactory);
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        var pipeline = pipelineProvider.GetPipeline<RestResponse>("scryfall") ?? ResiliencePipeline<RestResponse>.Empty;
        var client = restClientOverride ?? scryfallRestClientFactory.Create();
        _executeCollectionAsync = executeCollectionAsyncOverride ?? ((request, cancellationToken) =>
            ScryfallThrottle.ExecuteAsync(
                token => pipeline.ExecuteAsync(
                    async pollyCt => await client.ExecuteAsync<ScryfallCollectionResponse>(request, pollyCt).ConfigureAwait(false),
                    token).AsTask(),
                cancellationToken));
        _executeSearchAsync = executeSearchAsyncOverride ?? ((request, cancellationToken) =>
            ScryfallThrottle.ExecuteAsync(
                token => pipeline.ExecuteAsync(
                    async pollyCt => await client.ExecuteAsync<ScryfallSearchResponse>(request, pollyCt).ConfigureAwait(false),
                    token).AsTask(),
                cancellationToken));
        _executeNamedAsync = executeNamedAsyncOverride ?? ((request, cancellationToken) =>
            ScryfallThrottle.ExecuteAsync(
                token => pipeline.ExecuteAsync(
                    async pollyCt => await client.ExecuteAsync<ScryfallCard>(request, pollyCt).ConfigureAwait(false),
                    token).AsTask(),
                cancellationToken));
    }

    /// <inheritdoc/>
    public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _executeCollectionAsync(request, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardName))
        {
            return null;
        }

        var request = new RestRequest("cards/collection", Method.Post);
        request.AddJsonBody(new { identifiers = new object[] { new { name = cardName } } });

        RestResponse<ScryfallCollectionResponse> response =
            await ExecuteCollectionAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices && response.Data?.Data.Count > 0)
        {
            ScryfallCard? hit = response.Data.Data.FirstOrDefault(card =>
                string.Equals(CardNormalizer.Normalize(card.Name), CardNormalizer.Normalize(cardName), StringComparison.Ordinal));
            if (hit is not null)
            {
                return hit;
            }
        }

        return await SearchFallbackCardAsync(cardName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
    {
        var normalizedName = cardName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        var request = new RestRequest("cards/search", Method.Get);
        request.AddQueryParameter("q", $"!\"{normalizedName}\"");
        request.AddQueryParameter("unique", "cards");
        request.AddQueryParameter("order", "name");

        var response = await _executeSearchAsync(request, cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
        {
            return response.Data?.Data.FirstOrDefault();
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        throw new HttpRequestException(
            $"Scryfall fallback lookup failed while resolving {cardName} with HTTP {(int)response.StatusCode}.",
            null,
            response.StatusCode);
    }

    /// <inheritdoc/>
    public async Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardName))
        {
            return null;
        }

        var normalizedCardName = NormalizeLookupName(cardName);
        foreach (var query in new[]
        {
            $"(printed:\"{NormalizeForScryfall(cardName)}\" OR name:\"{NormalizeForScryfall(cardName)}\")",
            NormalizeForScryfall(cardName)
        })
        {
            var request = new RestRequest("cards/search", Method.Get);
            request.AddQueryParameter("q", query);
            request.AddQueryParameter("unique", "prints");
            request.AddQueryParameter("include_multilingual", "true");

            var response = await _executeSearchAsync(request, cancellationToken).ConfigureAwait(false);
            ScryfallThrottle.ThrowIfUpstreamUnavailable(response.StatusCode);
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 || response.Data is null)
            {
                continue;
            }

            var match = response.Data.Data
                .FirstOrDefault(card => NormalizeLookupName(card.Name) == normalizedCardName)
                ?? response.Data.Data.FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        var namedRequest = new RestRequest("cards/named", Method.Get);
        namedRequest.AddQueryParameter("fuzzy", NormalizeForScryfall(cardName));
        var namedResponse = await _executeNamedAsync(namedRequest, cancellationToken).ConfigureAwait(false);
        ScryfallThrottle.ThrowIfUpstreamUnavailable(namedResponse.StatusCode);
        if ((int)namedResponse.StatusCode >= 200 && (int)namedResponse.StatusCode < 300 && namedResponse.Data is not null)
        {
            return namedResponse.Data;
        }

        return null;
    }

    /// <summary>
    /// Normalizes a card name for equality comparisons across quote, apostrophe, and dash variants.
    /// </summary>
    public static string NormalizeLookupName(string cardName)
        => cardName
            .Trim()
            .Replace('\u2019', '\'')
            .Replace('\u2018', '\'')
            .Replace('\u02BC', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .ToLowerInvariant();

    /// <summary>
    /// Normalizes a card name for use in Scryfall API payloads.
    /// Converts the single-slash DFC separator used by Archidekt exports (" / ")
    /// to the double-slash form Scryfall expects (" // "). This changes only the identifier
    /// SUBMITTED to Scryfall and never the key a batch resolver matches returned cards back on.
    /// DeckEntry.Name is NOT modified — normalization happens only at the call site.
    /// </summary>
    /// <remarks>
    /// A live probe (<c>111.1-REVIEWS.md</c> §0, 2026-07-31) showed <c>/cards/collection</c>
    /// resolves NEITHER slash form of a combined multiface name — so no submission spelling makes
    /// a DFC name resolve there; it reaches its card through the search fallback either way. See
    /// <c>docs/decisions/0004-scryfall-batch-match-key-asymmetry.md</c>.
    /// </remarks>
    public static string NormalizeForScryfall(string cardName)
        => cardName.Replace(" / ", " // ");
}
