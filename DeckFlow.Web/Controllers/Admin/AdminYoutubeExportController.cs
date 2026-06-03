using System.Text;
using DeckFlow.Core.Integration;
using DeckFlow.Web.Models.Admin;
using DeckFlow.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Operator UI for /Admin/YoutubeExport: enter a YouTube channel (handle/URL/id/slug) and
/// download a plain-text listing of its most recent uploads with title, view count, and
/// upload date. Sits behind the /Admin BasicAuth branch; the export POST carries BOTH
/// <c>[ValidateAntiForgeryToken]</c> and the <see cref="SameOriginRequestValidator"/> guard
/// per the admin mutation convention.
/// </summary>
[Route("Admin/YoutubeExport")]
public sealed class AdminYoutubeExportController : Controller
{
    // Why: each listed video costs one YouTube metadata call (upload date + views are not
    // on the playlist item), so the depth is capped to keep a single export bounded.
    private const int MaxLimit = 500;
    private const int DefaultLimit = 100;
    private static readonly TimeSpan ExportTimeout = TimeSpan.FromMinutes(5);

    private readonly IYouTubeChannelVideoLister _lister;
    private readonly ILogger<AdminYoutubeExportController> _logger;

    /// <summary>Constructor injecting the channel video lister and logger.</summary>
    /// <param name="lister">YouTube channel video lister (full uploads-playlist walk).</param>
    /// <param name="logger">Logger.</param>
    public AdminYoutubeExportController(
        IYouTubeChannelVideoLister lister,
        ILogger<AdminYoutubeExportController> logger)
    {
        ArgumentNullException.ThrowIfNull(lister);
        ArgumentNullException.ThrowIfNull(logger);
        _lister = lister;
        _logger = logger;
    }

    /// <summary>Renders the export form.</summary>
    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index() => View(new AdminYoutubeExportViewModel());

    /// <summary>
    /// Lists the channel's most recent uploads and returns them as a downloadable
    /// plain-text file (title, views, upload date, URL per video).
    /// </summary>
    /// <param name="channel">YouTube channel handle, URL, id, or slug.</param>
    /// <param name="limit">Maximum uploads to include (clamped to 1-500).</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("Export")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(string? channel, int limit = DefaultLimit, CancellationToken cancellationToken = default)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        if (string.IsNullOrWhiteSpace(channel))
        {
            return View("Index", new AdminYoutubeExportViewModel { ErrorMessage = "Enter a YouTube channel handle or URL." });
        }

        var clampedLimit = Math.Clamp(limit, 1, MaxLimit);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(ExportTimeout);

        try
        {
            var videos = await _lister.ListRecentAsync(channel.Trim(), clampedLimit, timeoutSource.Token).ConfigureAwait(false);
            if (videos.Count == 0)
            {
                return View("Index", new AdminYoutubeExportViewModel
                {
                    Channel = channel,
                    Limit = clampedLimit,
                    ErrorMessage = "No videos found for that channel.",
                });
            }

            var text = YouTubeVideoListExport.BuildText(channel, videos, DateTimeOffset.UtcNow);
            var fileName = BuildFileName(channel);
            return File(Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", fileName);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return View("Index", new AdminYoutubeExportViewModel
            {
                Channel = channel,
                Limit = clampedLimit,
                ErrorMessage = "YouTube lookup timed out — try a smaller video count.",
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "YouTube export failed for channel input.");
            return View("Index", new AdminYoutubeExportViewModel
            {
                Channel = channel,
                Limit = clampedLimit,
                ErrorMessage = "YouTube lookup failed: " + exception.Message,
            });
        }
    }

    private static string BuildFileName(string channel)
    {
        var trimmed = channel.Trim();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        var slug = builder.ToString().Trim('-');
        if (string.IsNullOrEmpty(slug))
        {
            slug = "channel";
        }

        return $"{slug}-videos.txt";
    }
}
