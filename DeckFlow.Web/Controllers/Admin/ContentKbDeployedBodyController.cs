using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// D-09 (REVISED) deploy-confirm surface for DirectPush's hash-gated expand-contract ordering
/// (SYNC-09). Resolves a Content KB row's artifact from the git <c>/app</c> tree ONLY (never the
/// <c>/data</c> overlay, never gated on <c>is_visible</c>) and returns the recomputed body hash,
/// or 404 when the <c>/app</c> artifact is missing. Studio's confirmer polls this endpoint until
/// it returns 200 with a matching hash before stamping <c>pushed_to_prod_utc</c> and flipping
/// visibility. Routed under <c>/Admin</c> so the existing <see cref="Infrastructure.BasicAuthMiddleware"/>
/// branch guards it (Program.cs's <c>/Admin</c> <c>UseWhen</c> matches by path prefix - no new
/// wiring needed). Deliberately does NOT call <c>SameOriginRequestValidator</c>: this is a
/// Studio-to-prod server-to-server call with no browser Origin header, so a same-origin check
/// would wrongly reject it. Read-only - no writes, no DDL.
/// </summary>
[ApiController]
[Route("Admin/api/contentkb")]
public sealed class ContentKbDeployedBodyController : ControllerBase
{
    private readonly IContentSiteIndexStore _store;
    private readonly ContentKbArtifactPathResolver _resolver;
    private readonly ILogger<ContentKbDeployedBodyController> _logger;

    /// <summary>Creates the deployed-body-hash controller.</summary>
    /// <param name="store">Content site-index store (unfiltered natural-key lookup).</param>
    /// <param name="resolver">Git-/app-only artifact path resolver.</param>
    /// <param name="logger">Logger.</param>
    public ContentKbDeployedBodyController(
        IContentSiteIndexStore store,
        ContentKbArtifactPathResolver resolver,
        ILogger<ContentKbDeployedBodyController> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>
    /// Returns the deployed git <c>/app</c> body hash for a row located by natural key. 404 when
    /// the natural key is unknown or the <c>/app</c> artifact is missing; 200 with
    /// <c>{ bodySha256 }</c> otherwise - NOT gated on <c>is_visible</c>, so a not-yet-visible
    /// DirectPush'd row is still confirmable (D-09 REVISED).
    /// </summary>
    /// <param name="naturalKeyType">Natural key type, such as <see cref="ContentSourceType.Youtube"/> or <see cref="ContentSourceType.Podcast"/>.</param>
    /// <param name="naturalKeyValue">Natural key value.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpGet("deployed-body-hash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeployedBodyHash(
        [FromQuery] string? naturalKeyType,
        [FromQuery] string? naturalKeyValue,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(naturalKeyType) || string.IsNullOrWhiteSpace(naturalKeyValue))
        {
            return BadRequest();
        }

        // Why: unfiltered natural-key lookup (NOT GetPublishedByIdAsync) so a not-yet-visible
        // DirectPush'd row still yields its /app hash - the whole point of D-09 REVISED is to
        // confirm the deploy independent of is_visible, which flips only AFTER this confirms.
        var row = await _store.GetByNaturalKeyAsync(naturalKeyType, naturalKeyValue, cancellationToken).ConfigureAwait(false);
        if (row is null || !row.ArtifactPath.StartsWith("content-kb/", StringComparison.Ordinal))
        {
            return NotFound();
        }

        var resolution = _resolver.TryResolveGitArtifact(row.ArtifactPath, out var resolvedFullPath);
        if (resolution != ContentKbArtifactResolution.Resolved)
        {
            _logger.LogInformation(
                "Deploy-confirm miss for natural key {NaturalKeyType}/{NaturalKeyValue}: {Resolution}",
                naturalKeyType,
                naturalKeyValue,
                resolution);
            return NotFound();
        }

        var raw = await System.IO.File.ReadAllTextAsync(resolvedFullPath, cancellationToken).ConfigureAwait(false);
        var bodySha256 = ContentSiteIndexContentSignature.ComputeBodySha256(raw);
        return Ok(new { bodySha256 });
    }
}
