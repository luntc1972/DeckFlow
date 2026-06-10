using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Api;

/// <summary>
/// Serves same-origin Content KB search endpoints for expert-context selection surfaces.
/// </summary>
[ApiController]
[Route("api/content-kb")]
public sealed class ContentKbSearchApiController : ControllerBase
{
    private readonly IContentSiteIndexStore _store;

    /// <summary>
    /// Creates the Content KB search API controller.
    /// </summary>
    /// <param name="store">Published Content KB index store.</param>
    public ContentKbSearchApiController(IContentSiteIndexStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    /// <summary>
    /// Returns matching published entries as <c>{ id, title }</c> objects for object-aware typeahead.
    /// </summary>
    /// <param name="query">Search text matched against entry titles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("entries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<object>>> Search(string query, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
        }

        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var rows = await _store.GetPublishedRowsAsync(cancellationToken).ConfigureAwait(false);
        var results = rows
            .Where(row => row.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(row => new
            {
                id = row.PinId,
                title = row.Title,
            })
            .Take(10)
            .Cast<object>()
            .ToList();

        return Ok(results);
    }

    /// <summary>
    /// Returns matching published creator names for follow typeahead.
    /// </summary>
    /// <param name="query">Search text matched against creator names.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("creators")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<string>>> Creators(string query, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
        }

        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length == 0)
        {
            return Ok(Array.Empty<string>());
        }

        var rows = await _store.GetPublishedRowsAsync(cancellationToken).ConfigureAwait(false);
        var results = rows
            .Where(row => row.Source.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(row => row.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        return Ok(results);
    }
}
