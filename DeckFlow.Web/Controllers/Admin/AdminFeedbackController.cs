using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Operations the admin feedback console allows when triaging a stored submission.
/// </summary>
public enum AdminFeedbackOp
{
    /// <summary>Marks feedback as read while keeping it available for review.</summary>
    MarkRead,
    /// <summary>Moves feedback out of the active triage queue without deleting it.</summary>
    Archive,
    /// <summary>Removes feedback that should no longer remain in the store.</summary>
    Delete,
}

/// <summary>
/// View model for the /Admin/Feedback index page, carrying filters, paging, and status counts for triage.
/// </summary>
public sealed class AdminFeedbackListViewModel
{
    /// <summary>Feedback rows rendered on the current admin list page.</summary>
    public IReadOnlyList<FeedbackItem> Items { get; init; } = Array.Empty<FeedbackItem>();
    /// <summary>Status filter currently applied to the list, or null for all statuses.</summary>
    public FeedbackStatus? StatusFilter { get; init; }
    /// <summary>Feedback type filter currently applied to the list, or null for all types.</summary>
    public FeedbackType? TypeFilter { get; init; }
    /// <summary>One-based page number currently rendered.</summary>
    public int Page { get; init; } = 1;
    /// <summary>Maximum feedback rows shown per page.</summary>
    public int PageSize { get; init; } = 50;
    /// <summary>Total number of feedback rows matching the current filters.</summary>
    public int TotalCount { get; init; }
    /// <summary>Feedback counts grouped by status for the admin triage sidebar.</summary>
    public IReadOnlyDictionary<FeedbackStatus, int> CountsByStatus { get; init; } =
        new Dictionary<FeedbackStatus, int>();
    /// <summary>Total number of pages available for the current filters.</summary>
    public int TotalPages => (int)Math.Ceiling((double)Math.Max(TotalCount, 1) / Math.Max(PageSize, 1));
}

/// <summary>
/// Operator UI for reviewing submitted feedback behind the existing /Admin BasicAuth branch.
/// </summary>
[Route("Admin/Feedback")]
public sealed class AdminFeedbackController : Controller
{
    private readonly IFeedbackStore _store;

    /// <summary>
    /// Creates the admin feedback controller.
    /// </summary>
    public AdminFeedbackController(IFeedbackStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Renders a filtered page of feedback so admins can triage by status and type without loading the whole store.
    /// </summary>
    /// <param name="status">Optional status filter; defaults to new submissions.</param>
    /// <param name="type">Optional feedback-type filter.</param>
    /// <param name="page">One-based result page to render.</param>
    /// <returns>The admin feedback list view for the requested filter page.</returns>
    [HttpGet("")]
    public async Task<IActionResult> Index(FeedbackStatus? status = FeedbackStatus.New, FeedbackType? type = null, int page = 1)
    {
        page = Math.Max(page, 1);
        const int pageSize = 50;
        var query = new FeedbackListQuery { Status = status, Type = type, Page = page, PageSize = pageSize };
        var items = await _store.ListAsync(query);
        var total = await _store.CountAsync(status, type);
        var counts = await _store.CountsByStatusAsync();

        var vm = new AdminFeedbackListViewModel
        {
            Items = items,
            StatusFilter = status,
            TypeFilter = type,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            CountsByStatus = counts,
        };
        return View(vm);
    }

    /// <summary>
    /// Renders one stored feedback submission for admin review.
    /// </summary>
    /// <param name="id">Stored feedback identifier.</param>
    /// <returns>The feedback detail view when found; otherwise, a not-found response.</returns>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var item = await _store.GetAsync(id);
        if (item is null) return NotFound();
        return View(item);
    }

    /// <summary>
    /// Applies the selected triage operation through POST so feedback state changes require an antiforgery token.
    /// </summary>
    /// <param name="id">Stored feedback identifier.</param>
    /// <param name="op">Requested triage operation.</param>
    /// <returns>A redirect back to the admin feedback list after the operation is applied.</returns>
    [HttpPost("{id:long}/{op}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(long id, AdminFeedbackOp op)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        switch (op)
        {
            case AdminFeedbackOp.MarkRead:
                await _store.UpdateStatusAsync(id, FeedbackStatus.Read);
                break;
            case AdminFeedbackOp.Archive:
                await _store.UpdateStatusAsync(id, FeedbackStatus.Archived);
                break;
            case AdminFeedbackOp.Delete:
                await _store.DeleteAsync(id);
                break;
            default:
                return BadRequest();
        }

        TempData["AdminFeedbackAction"] = $"{op.ToString().ToLowerInvariant()} applied to #{id}";
        return RedirectToAction(nameof(Index));
    }
}
