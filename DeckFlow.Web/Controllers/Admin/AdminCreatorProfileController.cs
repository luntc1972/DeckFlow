using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Web.Security;
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
    private static readonly TimeSpan DefaultRunTimeout = TimeSpan.FromMinutes(10);
    private static readonly StringComparer PlatformComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly HashSet<string> AllowedPlatforms = new(PlatformComparer)
    {
        "archidekt",
        "moxfield",
    };

    private readonly ICreatorProfileSourceStore _sourceStore;
    private readonly Func<string, string, CancellationToken, Task<MeasuredStyleBuildResult>> _buildDetailedAsync;
    private readonly Func<DateTimeOffset> _nowUtc;
    private readonly TimeSpan _runTimeout;
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
    /// <param name="runTimeout">
    /// Optional run-timeout override; defaults to the production ten-minute constant. Lets a fact
    /// supply a short deterministic interval instead of waiting out the real timeout.
    /// </param>
    internal AdminCreatorProfileController(
        ICreatorProfileSourceStore sourceStore,
        Func<string, string, CancellationToken, Task<MeasuredStyleBuildResult>> buildDetailedAsync,
        Func<DateTimeOffset>? nowUtc = null,
        ILogger<AdminCreatorProfileController>? logger = null,
        TimeSpan? runTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(sourceStore);
        ArgumentNullException.ThrowIfNull(buildDetailedAsync);
        _sourceStore = sourceStore;
        _buildDetailedAsync = buildDetailedAsync;
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
        _logger = logger ?? NullLogger<AdminCreatorProfileController>.Instance;
        _runTimeout = runTimeout ?? DefaultRunTimeout;
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

    /// <summary>
    /// Upserts the creator source, runs crawl plus measure, and renders the resulting profile and
    /// tendencies report. Same-origin checked (CLAUDE.md anti-pattern: an endpoint skipping
    /// <see cref="SameOriginRequestValidator"/>) and antiforgery-protected alongside it — the
    /// middleware branch at Program.cs is the only auth gate, this action adds no second
    /// credential check. Order matters here and is part of the contract: same-origin check,
    /// normalization, field validation, the linked-and-timed-out cancellation scope, the
    /// read-existing/identity-changed decision, the upsert, the detailed build, the report, then
    /// the render.
    /// </summary>
    /// <param name="input">Posted crawl-and-measure form fields.</param>
    /// <returns>
    /// The creator-profile admin view, re-rendered with either the built profile and report, an
    /// operator-facing error message, or a model-state validation error — or a 403 when the
    /// request fails the same-origin check.
    /// </returns>
    [HttpPost("Run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(AdminCreatorProfileInputModel input)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        input ??= new AdminCreatorProfileInputModel();

        string normalizedSlug = NormalizeSlug(input.Slug);
        string normalizedPlatform = NormalizePlatform(input.Platform);
        string trimmedUsername = input.Username?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedSlug))
        {
            ModelState.AddModelError(nameof(AdminCreatorProfileInputModel.Slug), "Enter a creator slug.");
        }

        if (string.IsNullOrWhiteSpace(trimmedUsername))
        {
            ModelState.AddModelError(nameof(AdminCreatorProfileInputModel.Username), "Enter a profile username.");
        }

        if (!AllowedPlatforms.Contains(normalizedPlatform))
        {
            ModelState.AddModelError(nameof(AdminCreatorProfileInputModel.Platform), "Platform must be archidekt|moxfield.");
        }

        var normalizedInput = new AdminCreatorProfileInputModel
        {
            Slug = normalizedSlug,
            Username = trimmedUsername,
            Platform = normalizedPlatform,
            ForceRefresh = input.ForceRefresh,
        };

        if (!ModelState.IsValid)
        {
            return View("Index", BuildViewModel(normalizedInput));
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext?.RequestAborted ?? CancellationToken.None);
            timeoutCts.CancelAfter(_runTimeout);
            CancellationToken cancellationToken = timeoutCts.Token;
            DateTimeOffset nowUtc = _nowUtc();

            CreatorProfileSource? existing = await _sourceStore.GetBySlugAsync(normalizedSlug, cancellationToken).ConfigureAwait(false);
            bool identityChanged = existing is not null
                && (!PlatformComparer.Equals(existing.Platform, normalizedPlatform)
                    || !string.Equals(existing.ProfileUsername, trimmedUsername, StringComparison.Ordinal));

            var upsertedSource = new CreatorProfileSource
            {
                Slug = normalizedSlug,
                Platform = normalizedPlatform,
                ProfileUsername = trimmedUsername,
                ProfileUrl = existing?.ProfileUrl,
                FolderWeights = existing?.FolderWeights ?? new Dictionary<int, double>(),
                WeightsUncurated = existing?.WeightsUncurated ?? true,
                LastCrawledUtc = normalizedInput.ForceRefresh || identityChanged ? null : existing?.LastCrawledUtc,
                UpdatedUtc = nowUtc,
            };

            _logger.LogInformation(
                "Upserting creator profile source for {CreatorSlug} on {Platform} with username {ProfileUsername}.",
                normalizedSlug,
                normalizedPlatform,
                trimmedUsername);

            await _sourceStore.UpsertAsync(upsertedSource, cancellationToken).ConfigureAwait(false);

            MeasuredStyleBuildResult result = await _buildDetailedAsync(normalizedSlug, normalizedPlatform, cancellationToken).ConfigureAwait(false);
            DeckTendenciesReport report = DeckTendenciesReportBuilder.Build(result.Samples, result.CardCategories, result.Baseline);
            return View("Index", BuildViewModel(normalizedInput, result.Profile, report));
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogWarning(exception, "Creator profile run for {CreatorSlug} timed out or was canceled.", normalizedSlug);
            return View("Index", BuildViewModel(
                normalizedInput,
                errorMessage: "Creator crawl and measurement timed out after 10 minutes. Try again in a moment."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to run creator profile pipeline for {CreatorSlug} on {Platform}.", normalizedSlug, normalizedPlatform);
            return View("Index", BuildViewModel(
                normalizedInput,
                errorMessage: "Creator crawl and measurement failed. Check the logs and try again."));
        }
    }

    private static string NormalizeSlug(string? slug)
        => (slug ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizePlatform(string? platform)
        => (platform ?? string.Empty).Trim().ToLowerInvariant();

    private static AdminCreatorProfileViewModel BuildViewModel(
        AdminCreatorProfileInputModel input,
        CreatorStyleProfile? profile = null,
        DeckTendenciesReport? report = null,
        string? errorMessage = null)
        => new()
        {
            Slug = input.Slug,
            Username = input.Username,
            Platform = string.IsNullOrWhiteSpace(input.Platform) ? "archidekt" : input.Platform,
            ForceRefresh = input.ForceRefresh,
            ErrorMessage = errorMessage,
            Profile = profile,
            Report = report,
        };

    private static Func<string, string, CancellationToken, Task<MeasuredStyleBuildResult>> BindBuildDelegate(
        MeasuredStyleProfileBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return (slug, platform, cancellationToken) => builder.BuildDetailedAsync(slug, platform, cancellationToken);
    }
}
