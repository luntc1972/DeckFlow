using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Models;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Operator UI for /Admin/ContentKb (Phase 22, KB-09). Renders the curation grid over ALL
/// index rows and handles per-entry publish/unpublish, per-source bulk publish/hide, and
/// reload-from-seed. Sits behind the existing /Admin BasicAuth branch. Every mutating POST
/// carries BOTH <c>[ValidateAntiForgeryToken]</c> and a <see cref="SameOriginRequestValidator"/>
/// same-origin guard (SC4/P11).
/// </summary>
[Route("Admin/ContentKb")]
public sealed class AdminContentKbController : Controller
{
    private const string BannerKey = "AdminContentKbBanner";

    private readonly IContentSiteIndexStore _store;
    private readonly IContentKbSeedLoader _seedLoader;
    private readonly IFeatureFlagCache _flagCache;
    private readonly IContentKbRelevanceService _relevanceService;
    private readonly ILogger<AdminContentKbController> _logger;

    /// <summary>Constructor injecting the index store, seed loader, flag cache, relevance service, and logger.</summary>
    /// <param name="store">Content site-index store (read all rows + flip visibility).</param>
    /// <param name="seedLoader">Curation-preserving seed loader for the reload action.</param>
    /// <param name="flagCache">Feature-flag cache for the content.kb.enabled status display.</param>
    /// <param name="relevanceService">Artifact-level relevance scorer used by the admin preview.</param>
    /// <param name="logger">Logger.</param>
    public AdminContentKbController(
        IContentSiteIndexStore store,
        IContentKbSeedLoader seedLoader,
        IFeatureFlagCache flagCache,
        IContentKbRelevanceService relevanceService,
        ILogger<AdminContentKbController> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(seedLoader);
        ArgumentNullException.ThrowIfNull(flagCache);
        ArgumentNullException.ThrowIfNull(relevanceService);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _seedLoader = seedLoader;
        _flagCache = flagCache;
        _relevanceService = relevanceService;
        _logger = logger;
    }

    /// <summary>
    /// Renders the curation grid over ALL index rows (published + hidden) plus the status panel
    /// and per-source bulk groups. The status timestamp is max(indexed_utc) honestly labeled as
    /// the index-generation time (D-22D).
    /// </summary>
    /// <param name="previewCommander">Optional commander text for the live relevance preview.</param>
    /// <param name="previewBracket">Optional bracket filter for the live relevance preview.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string? previewCommander = null,
        string? previewBracket = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _store.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);
        var normalizedPreviewCommander = NormalizePreviewCommander(previewCommander);
        var normalizedPreviewBracket = NormalizePreviewBracket(previewBracket);
        Dictionary<long, double>? previewScores = null;

        if (!string.IsNullOrWhiteSpace(previewCommander) || !string.IsNullOrWhiteSpace(previewBracket))
        {
            previewScores = (await _relevanceService
                    .ScoreAllAsync(normalizedPreviewCommander, normalizedPreviewBracket, cancellationToken)
                    .ConfigureAwait(false))
                .GroupBy(item => item.Row.Id)
                .ToDictionary(group => group.Key, group => group.First().Score);
        }

        var entries = rows
            .Select(r => new KbEntryRow
            {
                Id = r.Id,
                Title = r.Title,
                Source = r.Source,
                Tags = r.ArchetypeTags.Concat(r.BracketTags).ToArray(),
                IsVisible = r.IsVisible,
                RelevanceScore = previewScores is not null && previewScores.TryGetValue(r.Id, out var score) ? score : null,
            })
            .ToArray();

        var sources = rows
            .GroupBy(r => r.Source, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new KbSourceGroup(g.Key, g.Count()))
            .ToArray();

        var status = new KbIndexStatus
        {
            TotalCount = rows.Count,
            PublishedCount = rows.Count(r => r.IsVisible),
            SourceCount = sources.Length,
            // D-22D: max(indexed_utc) is the index-GENERATION time, not a reload time.
            IndexGeneratedUtc = rows.Count == 0 ? null : rows.Max(r => r.IndexedUtc),
            FlagEnabled = _flagCache.IsEnabled("content.kb.enabled"),
        };

        var model = new AdminContentKbViewModel
        {
            Status = status,
            Sources = sources,
            Entries = entries,
            PreviewCommander = normalizedPreviewCommander,
            PreviewBracket = normalizedPreviewBracket,
            BracketOptions = ContentTagVocabulary.Brackets.ToArray(),
            SuccessBanner = TempData[BannerKey] as string,
        };

        return View(model);
    }

    private static string? NormalizePreviewCommander(string? previewCommander)
    {
        if (string.IsNullOrWhiteSpace(previewCommander))
        {
            return null;
        }

        return string.Join(
            ' ',
            previewCommander
                .Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? NormalizePreviewBracket(string? previewBracket)
    {
        if (string.IsNullOrWhiteSpace(previewBracket))
        {
            return null;
        }

        var trimmed = previewBracket.Trim();
        return ContentTagVocabulary.Brackets.Contains(trimmed) ? trimmed : null;
    }

    /// <summary>
    /// Publishes or hides a single entry by surrogate id. Double-CSRF-guarded.
    /// </summary>
    /// <param name="entryId">Surrogate row id.</param>
    /// <param name="visible">Desired visibility.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("SetVisibility")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetVisibility(long entryId, bool visible, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        await _store.SetVisibilityAsync(entryId, visible, cancellationToken).ConfigureAwait(false);
        TempData[BannerKey] = "Visibility updated.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Publishes or hides every entry for a given source. Double-CSRF-guarded.
    /// </summary>
    /// <param name="source">Source key.</param>
    /// <param name="visible">Desired visibility for all of the source's entries.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("BulkSetVisibility")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkSetVisibility(string source, bool visible, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return BadRequest();
        }

        await _store.SetVisibilityBySourceAsync(source, visible, cancellationToken).ConfigureAwait(false);
        TempData[BannerKey] = $"Bulk visibility updated for {source}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Re-runs the curation-preserving seed load (previously-published entries stay published —
    /// Pitfall 1). Double-CSRF-guarded.
    /// </summary>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("ReloadSeed")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReloadSeed(CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        var count = await _seedLoader.LoadIfPresentAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Admin reload-from-seed processed {Count} rows.", count);
        TempData[BannerKey] = "Index reloaded from seed.";
        return RedirectToAction(nameof(Index));
    }
}
