using System.Net;
using RestSharp;

namespace DeckFlow.Web.Services.Scryfall;

/// <summary>
/// The resolution band that produced a Scryfall collection result.
/// </summary>
/// <summary>
/// Routes collection protocol requests through the shared Scryfall resolver safeguards.
/// </summary>
public sealed class ScryfallCollectionProtocol : IScryfallCollectionProtocol
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
        restRequest.AddJsonBody(new { identifiers = request.Identifiers });
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
