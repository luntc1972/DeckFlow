using DeckFlow.Core.Content;
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
    private readonly ILogger<AdminContentKbController> _logger;

    /// <summary>Constructor injecting the index store, seed loader, flag cache, and logger.</summary>
    /// <param name="store">Content site-index store (read all rows + flip visibility).</param>
    /// <param name="seedLoader">Curation-preserving seed loader for the reload action.</param>
    /// <param name="flagCache">Feature-flag cache for the content.kb.enabled status display.</param>
    /// <param name="logger">Logger.</param>
    public AdminContentKbController(
        IContentSiteIndexStore store,
        IContentKbSeedLoader seedLoader,
        IFeatureFlagCache flagCache,
        ILogger<AdminContentKbController> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(seedLoader);
        ArgumentNullException.ThrowIfNull(flagCache);
        ArgumentNullException.ThrowIfNull(logger);
        _store = store;
        _seedLoader = seedLoader;
        _flagCache = flagCache;
        _logger = logger;
    }

    /// <summary>
    /// Renders the curation grid over ALL index rows (published + unpublished + hidden) plus the status panel
    /// and per-source bulk groups. The status timestamp is max(indexed_utc) honestly labeled as
    /// the index-generation time (D-22D).
    /// </summary>
    /// <param name="visibilityFilter">Optional entry visibility filter: all, published, unpublished, or hidden.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string? visibilityFilter = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _store.GetAllRowsAsync(cancellationToken).ConfigureAwait(false);
        var normalizedVisibilityFilter = NormalizeVisibilityFilter(visibilityFilter);

        IEnumerable<KbEntryRow> entries = rows
            .Select(r => new KbEntryRow
            {
                Id = r.Id,
                Title = r.Title,
                Source = r.Source,
                Tags = r.ArchetypeTags.Concat(r.BracketTags).ToArray(),
                IsVisible = r.IsVisible,
                IsHidden = r.IsHidden,
                IsEvergreen = r.IsEvergreen,
            });

        entries = normalizedVisibilityFilter switch
        {
            "published" => entries.Where(entry => entry.IsVisible),
            "unpublished" => entries.Where(entry => !entry.IsVisible && !entry.IsHidden),
            "hidden" => entries.Where(entry => entry.IsHidden),
            _ => entries.Where(entry => !entry.IsHidden)
        };

        var entryList = entries.ToArray();

        var sources = rows
            .GroupBy(r => r.Source, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new KbSourceGroup(g.Key, g.Count()))
            .ToArray();

        var status = new KbIndexStatus
        {
            TotalCount = rows.Count,
            PublishedCount = rows.Count(r => r.IsVisible),
            UnpublishedCount = rows.Count(r => !r.IsVisible && !r.IsHidden),
            HiddenCount = rows.Count(r => r.IsHidden),
            SourceCount = sources.Length,
            // D-22D: max(indexed_utc) is the index-GENERATION time, not a reload time.
            IndexGeneratedUtc = rows.Count == 0 ? null : rows.Max(r => r.IndexedUtc),
            FlagEnabled = _flagCache.IsEnabled("content.kb.enabled"),
        };

        var model = new AdminContentKbViewModel
        {
            Status = status,
            Sources = sources,
            Entries = entryList,
            VisibilityFilter = normalizedVisibilityFilter,
            SuccessBanner = TempData[BannerKey] as string,
        };

        return View(model);
    }

    private static string NormalizeVisibilityFilter(string? visibilityFilter)
    {
        if (string.Equals(visibilityFilter, "published", StringComparison.OrdinalIgnoreCase))
        {
            return "published";
        }

        if (string.Equals(visibilityFilter, "hidden", StringComparison.OrdinalIgnoreCase))
        {
            return "hidden";
        }

        if (string.Equals(visibilityFilter, "unpublished", StringComparison.OrdinalIgnoreCase))
        {
            return "unpublished";
        }

        return "all";
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
    /// Hides a single entry by surrogate id. Double-CSRF-guarded.
    /// </summary>
    /// <param name="entryId">Surrogate row id.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("Hide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hide(long entryId, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        await _store.SetHiddenAsync(entryId, hidden: true, cancellationToken).ConfigureAwait(false);
        TempData[BannerKey] = "Visibility updated.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Marks or unmarks an artifact as evergreen. Double-CSRF-guarded.
    /// </summary>
    /// <param name="entryId">Surrogate row id.</param>
    /// <param name="evergreen">Desired evergreen flag value.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("SetEvergreen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEvergreen(long entryId, bool evergreen, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        await _store.SetEvergreenAsync(entryId, evergreen, cancellationToken).ConfigureAwait(false);
        TempData[BannerKey] = "Evergreen status updated.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Permanently removes a single site-index row by surrogate id. Double-CSRF-guarded.
    /// </summary>
    /// <param name="entryId">Surrogate row id.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntry(long entryId, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        var deleted = await _store.DeleteByIdAsync(entryId, cancellationToken).ConfigureAwait(false);
        TempData[BannerKey] = deleted > 0 ? "Entry deleted." : "Entry not found.";
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
    /// Hides every entry for a given source. Double-CSRF-guarded.
    /// </summary>
    /// <param name="source">Source key.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("BulkHide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkHide(string source, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return BadRequest();
        }

        await _store.SetHiddenBySourceAsync(source, hidden: true, cancellationToken).ConfigureAwait(false);
        TempData[BannerKey] = $"Bulk visibility updated for {source}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Re-runs the seed loader to upsert the committed curation seed into the live index.
    /// Double-CSRF-guarded.
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

        var rowCount = await _seedLoader.LoadIfPresentAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Content KB seed reload completed. Rows={RowCount}", rowCount);
        TempData[BannerKey] = $"Reloaded seed ({rowCount} rows).";
        return RedirectToAction(nameof(Index));
    }
}
