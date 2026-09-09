using DeckFlow.Core.Content;
using DeckFlow.Web.Models;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services.CreatorStyle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// View model for the /Admin/CreatorStyle page: the creator picker, the deck-input form, and the
/// result region that carries either the packet artifact or an operator-facing notice.
/// </summary>
public sealed class AdminCreatorStyleViewModel
{
    /// <summary>Available creator-style profile summaries for the picker.</summary>
    public IReadOnlyList<CreatorStyleProfileSummary> AvailableCreators { get; init; } = Array.Empty<CreatorStyleProfileSummary>();

    /// <summary>Currently selected creator slug, or empty when none was submitted yet.</summary>
    public string CreatorSlug { get; init; } = string.Empty;

    /// <summary>Selects whether the deck is supplied via a public URL or pasted export text.</summary>
    public DeckInputSource DeckInputSource { get; init; } = DeckInputSource.PasteText;

    /// <summary>Public deck URL input value.</summary>
    public string DeckUrl { get; init; } = string.Empty;

    /// <summary>Pasted deck export text input value.</summary>
    public string DeckText { get; init; } = string.Empty;

    /// <summary>Magic: The Gathering format the deck targets.</summary>
    public string Format { get; init; } = "Commander";

    /// <summary>Rendered artifact text from a successful packet build, when one is available.</summary>
    public string? ArtifactText { get; init; }

    /// <summary>
    /// Operator-facing notice occupying the result region in place of a critique — either the
    /// canonical <see cref="AdminCreatorStyleController.NoProfilesSeededMessage"/> when the seed
    /// corpus is empty, or <see cref="Services.CreatorStyle.CreatorStylePacketResult.Notice"/>
    /// verbatim for any other non-null notice.
    /// </summary>
    public string? Notice { get; init; }
}

/// <summary>
/// Admin-only operator surface for generating a creator-style critique packet. Reachable only
/// through the existing <c>/Admin</c> BasicAuth branch (Program.cs) — this controller adds no
/// credential check of its own, and there is no public route for creator-style (PTOOL-01).
/// </summary>
[Route("Admin/CreatorStyle")]
public sealed class AdminCreatorStyleController : Controller
{
    /// <summary>
    /// Operator-facing message shown in the result region when the seed corpus is empty. Defined
    /// exactly once here so the GET empty-state render and the POST empty-corpus branch read the
    /// identical string rather than each inventing separate copy.
    /// </summary>
    internal const string NoProfilesSeededMessage =
        "No creator style profiles are seeded yet. Run the operator pipeline to populate one, then come back and pick a creator here.";

    private readonly ICreatorStyleProfileStore _profileStore;
    private readonly ICreatorStylePacketService _packetService;
    private readonly ILogger<AdminCreatorStyleController> _logger;

    /// <summary>
    /// Creates the admin creator-style controller.
    /// </summary>
    /// <param name="profileStore">Store backing the creator picker list.</param>
    /// <param name="packetService">Service that builds the creator-style critique packet.</param>
    /// <param name="logger">Logger.</param>
    public AdminCreatorStyleController(
        ICreatorStyleProfileStore profileStore,
        ICreatorStylePacketService packetService,
        ILogger<AdminCreatorStyleController> logger)
    {
        ArgumentNullException.ThrowIfNull(profileStore);
        ArgumentNullException.ThrowIfNull(packetService);
        ArgumentNullException.ThrowIfNull(logger);
        _profileStore = profileStore;
        _packetService = packetService;
        _logger = logger;
    }

    /// <summary>
    /// Renders the creator-style form. Shows the operator empty-state message in the result region
    /// when no creator profiles are seeded, without shrinking the rest of the page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token bound from the current request.</param>
    /// <returns>The creator-style admin view.</returns>
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var summaries = await GetAvailableCreatorsAsync(cancellationToken).ConfigureAwait(false);
        var vm = new AdminCreatorStyleViewModel
        {
            AvailableCreators = summaries,
            Notice = summaries.Count == 0 ? NoProfilesSeededMessage : null,
        };
        return View(vm);
    }

    /// <summary>
    /// Builds a creator-style critique packet for the submitted deck and creator slug. Same-origin
    /// checked (CLAUDE.md anti-pattern: an endpoint skipping <see cref="SameOriginRequestValidator"/>)
    /// and antiforgery-protected alongside it (T-114-03) — the middleware branch at Program.cs is
    /// the only auth gate (T-114-04: no second credential check here).
    /// </summary>
    /// <param name="request">Posted creator-style form fields.</param>
    /// <param name="cancellationToken">Cancellation token bound from the current request.</param>
    /// <returns>
    /// The creator-style admin view, re-rendered with either the built artifact, an operator
    /// notice, or a model-state validation error — or a 403 when the request fails the
    /// same-origin check.
    /// </returns>
    [HttpPost("Run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(CreatorStyleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        if (string.IsNullOrWhiteSpace(request.CreatorSlug))
        {
            ModelState.AddModelError(nameof(CreatorStyleRequest.CreatorSlug), "Select a creator.");
        }

        var deckFieldName = request.DeckInputSource == DeckInputSource.PublicUrl
            ? nameof(CreatorStyleRequest.DeckUrl)
            : nameof(CreatorStyleRequest.DeckText);
        if (string.IsNullOrWhiteSpace(request.DeckSource))
        {
            ModelState.AddModelError(deckFieldName, "Provide a deck URL or pasted decklist.");
        }

        var summaries = await GetAvailableCreatorsAsync(cancellationToken).ConfigureAwait(false);

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), BuildViewModel(request, summaries, artifactText: null, notice: null));
        }

        CreatorStylePacketResult result;
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted, cancellationToken);
            result = await _packetService.BuildAsync(request, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Creator-style packet build canceled for creator {CreatorSlug}.",
                request.CreatorSlug);
            return View(nameof(Index), BuildViewModel(
                request,
                summaries,
                artifactText: null,
                notice: "The request was canceled before a critique could be generated. Try again."));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Creator-style packet build failed for creator {CreatorSlug}.",
                request.CreatorSlug);
            return View(nameof(Index), BuildViewModel(
                request,
                summaries,
                artifactText: null,
                notice: "Something went wrong building the critique packet. Try again in a few minutes."));
        }

        // Why: the canonical empty-seed message is keyed on the repopulated summary list being
        // empty, not on ProfileUnavailable alone — ProfileUnavailable also covers an unmatched
        // slug or an insufficient-sample profile against a non-empty corpus, each of which must
        // render CreatorStylePacketResult.Notice verbatim instead (task 114-01, Task 2).
        string? notice = summaries.Count == 0 ? NoProfilesSeededMessage : result.Notice;
        string? artifactText = string.IsNullOrWhiteSpace(result.ArtifactText) ? null : result.ArtifactText;

        return View(nameof(Index), BuildViewModel(request, summaries, artifactText, notice));
    }

    private static AdminCreatorStyleViewModel BuildViewModel(
        CreatorStyleRequest request,
        IReadOnlyList<CreatorStyleProfileSummary> summaries,
        string? artifactText,
        string? notice)
        => new()
        {
            AvailableCreators = summaries,
            CreatorSlug = request.CreatorSlug,
            DeckInputSource = request.DeckInputSource,
            DeckUrl = request.DeckUrl,
            DeckText = request.DeckText,
            Format = request.Format,
            ArtifactText = artifactText,
            Notice = notice,
        };

    /// <summary>
    /// Loads the creator picker list, degrading to an empty list for a store implementation that
    /// has not overridden <see cref="ICreatorStyleProfileStore.GetAllAsync"/> — the interface
    /// default member throws <see cref="NotSupportedException"/>, while the production SQLite
    /// store does override it.
    /// </summary>
    private async Task<IReadOnlyList<CreatorStyleProfileSummary>> GetAvailableCreatorsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _profileStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            return Array.Empty<CreatorStyleProfileSummary>();
        }
    }
}
