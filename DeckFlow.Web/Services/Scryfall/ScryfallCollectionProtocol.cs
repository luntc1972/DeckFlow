using System.Net;
using RestSharp;

namespace DeckFlow.Web.Services.Scryfall;

/// <summary>
/// The resolution band that produced a Scryfall collection result.
/// </summary>
internal enum ScryfallCollectionProtocolBand
{
    Identifier,
    ExactName,
    Fallback,
}

/// <summary>
/// Typed cards/collection submission expressed in submitted identifier order.
/// </summary>
internal sealed record ScryfallCollectionProtocolRequest(IReadOnlyList<string> Identifiers);

/// <summary>
/// Typed cards/collection response retaining its HTTP status and payload.
/// </summary>
internal sealed record ScryfallCollectionProtocolResponse(
    HttpStatusCode StatusCode,
    IReadOnlyList<ScryfallCard> Cards,
    IReadOnlyList<ScryfallCollectionIdentifier> NotFound,
    bool HasPayload);

/// <summary>
/// Executes typed Scryfall collection protocol requests.
/// </summary>
internal interface IScryfallCollectionProtocol
{
    Task<ScryfallCollectionProtocolResponse> ExecuteAsync(
        ScryfallCollectionProtocolRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Routes collection protocol requests through the shared Scryfall resolver safeguards.
/// </summary>
internal sealed class ScryfallCollectionProtocol : IScryfallCollectionProtocol
{
    private readonly IScryfallCardResolver _scryfallCardResolver;

    public ScryfallCollectionProtocol(IScryfallCardResolver scryfallCardResolver)
    {
        _scryfallCardResolver = scryfallCardResolver ?? throw new ArgumentNullException(nameof(scryfallCardResolver));
    }

    public async Task<ScryfallCollectionProtocolResponse> ExecuteAsync(
        ScryfallCollectionProtocolRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Identifiers.Count == 0)
        {
            return new ScryfallCollectionProtocolResponse(HttpStatusCode.OK, [], [], HasPayload: true);
        }

        var restRequest = new RestRequest("cards/collection", Method.Post);
        restRequest.AddJsonBody(new
        {
            identifiers = request.Identifiers.Select(name => new { name }).ToArray(),
        });
        RestResponse<ScryfallCollectionResponse> response = await _scryfallCardResolver
            .ExecuteCollectionAsync(restRequest, cancellationToken)
            .ConfigureAwait(false);
        return new ScryfallCollectionProtocolResponse(
            response.StatusCode,
            response.Data?.Data ?? [],
            response.Data?.NotFound ?? [],
            response.Data is not null);
    }
}
