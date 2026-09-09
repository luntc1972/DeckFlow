using DeckFlow.Core.Content;
using DeckFlow.Web.Services.CreatorStyle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Admin-only operator surface for upserting a creator source, running crawl plus measurement,
/// and rendering the resulting measured profile and deck-tendencies report. Reachable only
/// through the existing <c>/Admin</c> BasicAuth branch (Program.cs) — this controller adds no
/// credential check of its own, and there is no public route for Deck Tendencies (PTOOL-04).
/// </summary>
[Route("Admin/CreatorProfile")]
public sealed class AdminCreatorProfileController : Controller
{
    private readonly ICreatorProfileSourceStore _sourceStore;
    private readonly Func<string, string, CancellationToken, Task<MeasuredStyleBuildResult>> _buildDetailedAsync;
    private readonly Func<DateTimeOffset> _nowUtc;
    private readonly ILogger<AdminCreatorProfileController> _logger;

    /// <summary>
    /// Creates the production creator-profile admin controller.
    /// </summary>
    /// <param name="sourceStore">Store persisting the creator profile-source mapping.</param>
    /// <param name="builder">Builder that runs the crawl-plus-measure pipeline.</param>
    /// <param name="logger">Logger.</param>
    public AdminCreatorProfileController(
        ICreatorProfileSourceStore sourceStore,
        MeasuredStyleProfileBuilder builder,
        ILogger<AdminCreatorProfileController>? logger = null)
        : this(
            sourceStore,
            BindBuildDelegate(builder),
            nowUtc: null,
            logger)
    {
    }

    /// <summary>
    /// Test seam constructor: substitutes the build delegate and the clock so facts can drive
    /// deterministic crawl outcomes without a real <see cref="MeasuredStyleProfileBuilder"/>.
    /// </summary>
    /// <param name="sourceStore">Store persisting the creator profile-source mapping.</param>
    /// <param name="buildDetailedAsync">Delegate standing in for <see cref="MeasuredStyleProfileBuilder.BuildDetailedAsync"/>.</param>
    /// <param name="nowUtc">Optional clock override; defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    internal AdminCreatorProfileController(
        ICreatorProfileSourceStore sourceStore,
        Func<string, string, CancellationToken, Task<MeasuredStyleBuildResult>> buildDetailedAsync,
        Func<DateTimeOffset>? nowUtc = null,
        ILogger<AdminCreatorProfileController>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(sourceStore);
        ArgumentNullException.ThrowIfNull(buildDetailedAsync);
        _sourceStore = sourceStore;
        _buildDetailedAsync = buildDetailedAsync;
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
        _logger = logger ?? NullLogger<AdminCreatorProfileController>.Instance;
    }

    /// <summary>
    /// Renders the empty crawl-and-measure form: no profile, no report, no error, and the
    /// platform defaulted to archidekt.
    /// </summary>
    /// <returns>The creator-profile admin view.</returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        return View(new AdminCreatorProfileViewModel());
    }

    private static Func<string, string, CancellationToken, Task<MeasuredStyleBuildResult>> BindBuildDelegate(
        MeasuredStyleProfileBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return (slug, platform, cancellationToken) => builder.BuildDetailedAsync(slug, platform, cancellationToken);
    }
}
