using DeckFlow.Core.Content;
using DeckFlow.Web.Models;
using Microsoft.AspNetCore.Mvc;

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

    /// <summary>
    /// Creates the admin creator-style controller.
    /// </summary>
    /// <param name="profileStore">Store backing the creator picker list.</param>
    public AdminCreatorStyleController(ICreatorStyleProfileStore profileStore)
    {
        ArgumentNullException.ThrowIfNull(profileStore);
        _profileStore = profileStore;
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
