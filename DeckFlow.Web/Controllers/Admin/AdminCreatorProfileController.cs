using System.Globalization;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Web.Services.CreatorStyle;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Input model for the creator-profile admin form.
/// </summary>
public class AdminCreatorProfileInputModel
{
    /// <summary>Creator slug persisted to the source store.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Platform username used for crawl routing.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Platform identifier; currently archidekt or moxfield.</summary>
    public string Platform { get; init; } = "archidekt";

    /// <summary>When true, bypasses the creator warm-cache freshness window.</summary>
    public bool ForceRefresh { get; init; }
}

/// <summary>
/// Profile summary rendered above the measured metrics and tendency sections.
/// </summary>
public sealed record AdminCreatorProfileSummary
{
    /// <summary>Creator slug.</summary>
    public required string Slug { get; init; }

    /// <summary>Platform identifier.</summary>
    public required string Platform { get; init; }

    /// <summary>Measured profile deck count.</summary>
    public required int MinDecks { get; init; }

    /// <summary>Whether the measured profile fell below the minimum deck floor.</summary>
    public required bool InsufficientSample { get; init; }

    /// <summary>UTC timestamp when the measured profile was updated.</summary>
    public required DateTimeOffset UpdatedUtc { get; init; }
}

/// <summary>
/// View model for the creator-profile admin page.
/// </summary>
public sealed class AdminCreatorProfileViewModel : AdminCreatorProfileInputModel
{
    /// <summary>Action-boundary error surfaced back to the operator.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Optional summary derived from the built measured profile.</summary>
    public AdminCreatorProfileSummary? ProfileSummary { get; init; }

    /// <summary>Persisted measured style profile returned by the builder.</summary>
    public CreatorStyleProfile? Profile { get; init; }

    /// <summary>Deterministic deck-tendencies report for the crawled sample.</summary>
    public DeckTendenciesReport? Report { get; init; }
}

/// <summary>
/// Admin operator UI for upserting a creator source, running crawl plus measure, and rendering the tendencies report.
/// </summary>
[Route("Admin/CreatorProfile")]
public sealed class AdminCreatorProfileController : Controller
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(10);
    private static readonly StringComparer PlatformComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly HashSet<string> AllowedPlatforms = new(PlatformComparer)
    {
        "archidekt",
        "moxfield",
    };

    private readonly ICreatorProfileSourceStore _sourceStore;
    private readonly Func<string, string, CancellationToken, Task<CreatorStyleProfile>> _buildAsync;
    private readonly Func<string, bool, CancellationToken, Task<IReadOnlyList<CreatorDeckSample>>> _crawlAsync;
    private readonly Func<IReadOnlyList<CreatorDeckSample>, CancellationToken, Task<IReadOnlyDictionary<string, IReadOnlyList<string>>>> _resolveAsync;
    private readonly Func<CancellationToken, Task<GlobalCategoryBaseline>> _getBaselineAsync;
    private readonly Func<DateTimeOffset> _nowUtc;
    private readonly ILogger<AdminCreatorProfileController> _logger;

    /// <summary>
    /// Creates the production creator-profile admin controller.
    /// </summary>
    public AdminCreatorProfileController(
        ICreatorProfileSourceStore sourceStore,
        MeasuredStyleProfileBuilder builder,
        CreatorProfileDeckCrawler crawler,
        CreatorDeckCategoryResolver resolver,
        CategoryKnowledgeRepository categoryKnowledgeRepository,
        ILogger<AdminCreatorProfileController>? logger = null)
        : this(
            sourceStore,
            (slug, platform, cancellationToken) => builder.BuildAsync(slug, platform, cancellationToken),
            (slug, forceRefresh, cancellationToken) => crawler.CrawlAsync(slug, forceRefresh, cancellationToken),
            (samples, cancellationToken) => resolver.ResolveAsync(samples, cancellationToken),
            cancellationToken => categoryKnowledgeRepository.GetGlobalCategoryBaselineAsync(cancellationToken),
            null,
            logger)
    {
    }

    internal AdminCreatorProfileController(
        ICreatorProfileSourceStore sourceStore,
        Func<string, string, CancellationToken, Task<CreatorStyleProfile>> buildAsync,
        Func<string, bool, CancellationToken, Task<IReadOnlyList<CreatorDeckSample>>> crawlAsync,
        Func<IReadOnlyList<CreatorDeckSample>, CancellationToken, Task<IReadOnlyDictionary<string, IReadOnlyList<string>>>> resolveAsync,
        Func<CancellationToken, Task<GlobalCategoryBaseline>> getBaselineAsync,
        Func<DateTimeOffset>? nowUtc,
        ILogger<AdminCreatorProfileController>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(sourceStore);
        ArgumentNullException.ThrowIfNull(buildAsync);
        ArgumentNullException.ThrowIfNull(crawlAsync);
        ArgumentNullException.ThrowIfNull(resolveAsync);
        ArgumentNullException.ThrowIfNull(getBaselineAsync);
        _sourceStore = sourceStore;
        _buildAsync = buildAsync;
        _crawlAsync = crawlAsync;
        _resolveAsync = resolveAsync;
        _getBaselineAsync = getBaselineAsync;
        _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
        _logger = logger ?? NullLogger<AdminCreatorProfileController>.Instance;
    }

    /// <summary>
    /// Renders the creator-profile admin form.
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        return View(new AdminCreatorProfileViewModel());
    }

    /// <summary>
    /// Upserts the creator source, runs crawl plus measure, and renders the resulting profile and tendencies report.
    /// </summary>
    [HttpPost("Run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(AdminCreatorProfileInputModel input)
    {
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
            timeoutCts.CancelAfter(RunTimeout);
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
                LastCrawledUtc = input.ForceRefresh || identityChanged ? null : existing?.LastCrawledUtc,
                UpdatedUtc = nowUtc,
            };

            _logger.LogInformation(
                "Upserting creator profile source for {CreatorSlug} on {Platform} with username {ProfileUsername}.",
                normalizedSlug,
                normalizedPlatform,
                trimmedUsername);

            await _sourceStore.UpsertAsync(upsertedSource, cancellationToken).ConfigureAwait(false);

            if (input.ForceRefresh)
            {
                _logger.LogInformation("Force-refreshing creator crawl cache for {CreatorSlug}.", normalizedSlug);
                await _crawlAsync(normalizedSlug, true, cancellationToken).ConfigureAwait(false);
            }

            CreatorStyleProfile profile = await _buildAsync(normalizedSlug, normalizedPlatform, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<CreatorDeckSample> samples = await _crawlAsync(normalizedSlug, false, cancellationToken).ConfigureAwait(false);
            IReadOnlyDictionary<string, IReadOnlyList<string>> categories = await _resolveAsync(samples, cancellationToken).ConfigureAwait(false);

            GlobalCategoryBaseline? baseline = null;
            try
            {
                baseline = await _getBaselineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to load category baseline for creator {CreatorSlug}. Rendering report without baseline.", normalizedSlug);
            }

            DeckTendenciesReport report = DeckTendenciesReportBuilder.Build(samples, categories, baseline);
            return View("Index", BuildViewModel(normalizedInput, profile, report));
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
    {
        return (slug ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string NormalizePlatform(string? platform)
    {
        return (platform ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static AdminCreatorProfileSummary BuildSummary(CreatorStyleProfile profile)
    {
        return new AdminCreatorProfileSummary
        {
            Slug = profile.Slug,
            Platform = profile.Platform,
            MinDecks = profile.MinDecks,
            InsufficientSample = profile.InsufficientSample,
            UpdatedUtc = profile.UpdatedUtc,
        };
    }

    private static AdminCreatorProfileViewModel BuildViewModel(
        AdminCreatorProfileInputModel input,
        CreatorStyleProfile? profile = null,
        DeckTendenciesReport? report = null,
        string? errorMessage = null)
    {
        return new AdminCreatorProfileViewModel
        {
            Slug = input.Slug,
            Username = input.Username,
            Platform = string.IsNullOrWhiteSpace(input.Platform) ? "archidekt" : input.Platform,
            ForceRefresh = input.ForceRefresh,
            ErrorMessage = errorMessage,
            Profile = profile,
            ProfileSummary = profile is null ? null : BuildSummary(profile),
            Report = report,
        };
    }
}
