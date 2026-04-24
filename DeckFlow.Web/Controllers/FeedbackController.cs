using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DeckFlow.Web.Controllers;

public sealed class FeedbackController : Controller
{
    private readonly IFeedbackStore _store;
    private readonly IVersionService _versionService;

    public FeedbackController(IFeedbackStore store, IVersionService versionService)
    {
        _store = store;
        _versionService = versionService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new FeedbackSubmission());
    }

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
