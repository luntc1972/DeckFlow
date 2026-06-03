using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeckFlow.Web.Controllers;

/// <summary>
/// Collects user feedback through the rate-limited public form and records request context for admin triage.
/// </summary>
public sealed class FeedbackController : Controller
{
    private readonly IFeedbackStore _store;
    private readonly IVersionService _versionService;

    /// <summary>
    /// Creates the feedback controller.
    /// </summary>
    public FeedbackController(IFeedbackStore store, IVersionService versionService)
    {
        _store = store;
        _versionService = versionService;
    }

    /// <summary>
    /// Renders the public feedback submission form.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View(new FeedbackSubmission());
    }

    /// <summary>
    /// Stores a public feedback submission with request context for admin triage.
    /// </summary>
    /// <param name="submission">Feedback form payload.</param>
    /// <param name="cancellationToken">Cancellation token for the store write.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("feedback-submit")]
    public async Task<IActionResult> Index(FeedbackSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (!string.IsNullOrEmpty(submission.Website))
        {
            TempData["FeedbackSuccess"] = true;
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            return View(submission);
        }

        var context = new FeedbackRequestContext(
            Ip: HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent: Request.Headers.UserAgent.ToString(),
            PageUrl: Request.Headers.Referer.ToString(),
            AppVersion: _versionService.GetVersion());

        await _store.AddAsync(submission, context, cancellationToken);

        TempData["FeedbackSuccess"] = true;
        return RedirectToAction(nameof(Index));
    }
}
