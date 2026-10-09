using DeckFlow.Core.Content;
using DeckFlow.Web.Models;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CreatorStyle;
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
    private readonly PublishStateDeriver _deriver;
    private readonly ILogger<AdminContentKbController> _logger;
    private readonly ICreatorSuppressionStore _suppressionStore;
    private readonly ICreatorIdentityResolver _identityResolver;
    private readonly CreatorPurgeService _purgeService;
    private readonly CreatorWhitelistPoolBuilder _creatorWhitelistPoolBuilder;

    /// <summary>Constructor injecting the index store, seed loader, flag cache, and logger.</summary>
    /// <param name="store">Content site-index store (read all rows + flip visibility).</param>
    /// <param name="seedLoader">Curation-preserving seed loader for the reload action.</param>
    /// <param name="flagCache">Feature-flag cache for the tool.knowledge-base.enabled status display.</param>
    /// <param name="deriver">Shared publish-state deriver.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="suppressionStore">Creator suppression persistence.</param>
    /// <param name="identityResolver">Creator representation resolver.</param>
    /// <param name="purgeService">Creator-scoped multi-store purge service.</param>
    /// <param name="creatorWhitelistPoolBuilder">Existing creator whitelist cache invalidator.</param>
    public AdminContentKbController(
        IContentSiteIndexStore store,
        IContentKbSeedLoader seedLoader,
        IFeatureFlagCache flagCache,
        PublishStateDeriver deriver,
        ILogger<AdminContentKbController> logger,
        ICreatorSuppressionStore suppressionStore,
        ICreatorIdentityResolver identityResolver,
        CreatorPurgeService purgeService,
        CreatorWhitelistPoolBuilder creatorWhitelistPoolBuilder)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(seedLoader);
        ArgumentNullException.ThrowIfNull(flagCache);
        ArgumentNullException.ThrowIfNull(deriver);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(suppressionStore);
        ArgumentNullException.ThrowIfNull(identityResolver);
        ArgumentNullException.ThrowIfNull(purgeService);
        ArgumentNullException.ThrowIfNull(creatorWhitelistPoolBuilder);
        _store = store;
        _seedLoader = seedLoader;
        _flagCache = flagCache;
        _deriver = deriver;
        _logger = logger;
        _suppressionStore = suppressionStore;
        _identityResolver = identityResolver;
        _purgeService = purgeService;
        _creatorWhitelistPoolBuilder = creatorWhitelistPoolBuilder;
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
                PushedToProdUtc = r.PushedToProdUtc,
                IndexedUtc = r.IndexedUtc,
                PublishState = _deriver.Derive(r.PushedToProdUtc, r.IsVisible, r.IndexedUtc),
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
            FlagEnabled = _flagCache.IsEnabled("tool.knowledge-base.enabled"),
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
    /// <param name="visibilityFilter">Current visibility tab to return to.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("SetVisibility")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetVisibility(long entryId, bool visible, string? visibilityFilter, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        await _store.SetVisibilityAsync(entryId, visible, cancellationToken).ConfigureAwait(false);
        TempData[BannerKey] = "Visibility updated.";
        return RedirectToAction(nameof(Index), new { visibilityFilter });
    }

    /// <summary>
    /// Hides a single entry by surrogate id. Double-CSRF-guarded.
    /// </summary>
    /// <param name="entryId">Surrogate row id.</param>
    /// <param name="visibilityFilter">Current visibility tab to return to.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("Hide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hide(long entryId, string? visibilityFilter, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        await _store.SetHiddenAsync(entryId, hidden: true, cancellationToken).ConfigureAwait(false);
        TempData[BannerKey] = "Visibility updated.";
        return RedirectToAction(nameof(Index), new { visibilityFilter });
    }

    /// <summary>
    /// Marks or unmarks an artifact as evergreen. Double-CSRF-guarded.
    /// </summary>
    /// <param name="entryId">Surrogate row id.</param>
    /// <param name="evergreen">Desired evergreen flag value.</param>
    /// <param name="visibilityFilter">Current visibility tab to return to.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("SetEvergreen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEvergreen(long entryId, bool evergreen, string? visibilityFilter, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        await _store.SetEvergreenAsync(entryId, evergreen, cancellationToken).ConfigureAwait(false);
        TempData[BannerKey] = "Evergreen status updated.";
        return RedirectToAction(nameof(Index), new { visibilityFilter });
    }

    /// <summary>
    /// Permanently removes a single site-index row by surrogate id. Double-CSRF-guarded.
    /// </summary>
    /// <param name="entryId">Surrogate row id.</param>
    /// <param name="visibilityFilter">Current visibility tab to return to.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntry(long entryId, string? visibilityFilter, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        var deleted = await _store.DeleteByIdAsync(entryId, cancellationToken).ConfigureAwait(false);
        TempData[BannerKey] = deleted > 0 ? "Entry deleted." : "Entry not found.";
        return RedirectToAction(nameof(Index), new { visibilityFilter });
    }

    /// <summary>
    /// Publishes or hides every entry for a given source. Double-CSRF-guarded.
    /// </summary>
    /// <param name="source">Source key.</param>
    /// <param name="visible">Desired visibility for all of the source's entries.</param>
    /// <param name="visibilityFilter">Current visibility tab to return to.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("BulkSetVisibility")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkSetVisibility(string source, bool visible, string? visibilityFilter, CancellationToken cancellationToken)
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
        return RedirectToAction(nameof(Index), new { visibilityFilter });
    }

    /// <summary>
    /// Hides every entry for a given source. Double-CSRF-guarded.
    /// </summary>
    /// <param name="source">Source key.</param>
    /// <param name="visibilityFilter">Current visibility tab to return to.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("BulkHide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkHide(string source, string? visibilityFilter, CancellationToken cancellationToken)
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
        return RedirectToAction(nameof(Index), new { visibilityFilter });
    }

    /// <summary>
    /// Re-runs the seed loader to upsert the committed curation seed into the live index.
    /// Double-CSRF-guarded.
    /// </summary>
    /// <param name="visibilityFilter">Current visibility tab to return to.</param>
    /// <param name="cancellationToken">Request-aborted token.</param>
    [HttpPost("ReloadSeed")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReloadSeed(string? visibilityFilter, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        var rowCount = await _seedLoader.LoadIfPresentAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Content KB seed reload completed. Rows={RowCount}", rowCount);
        TempData[BannerKey] = $"Reloaded seed ({rowCount} rows).";
        return RedirectToAction(nameof(Index), new { visibilityFilter });
    }

    /// <summary>Suppresses a resolved creator and hides its indexed source rows.</summary>
    [HttpPost("SuppressCreator")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuppressCreator(string creator, string? reason, string? note, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        var utc = DateTimeOffset.UtcNow;
        try
        {
            var identity = await RequireIdentityAsync(creator, cancellationToken).ConfigureAwait(false);
            if (identity is null)
            {
                TempData[BannerKey] = "Creator was not found.";
                _logger.LogInformation("Creator {Action} {Slug} at {Utc} failed: {Reason}", "suppress", creator, utc, "unknown");
                return RedirectToAction(nameof(Index));
            }

            var aliases = identity.DisplayNames.Concat(identity.FolderSlugs)
                .Where(alias => !string.Equals(alias, identity.CanonicalSlug, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            await _suppressionStore.SuppressAsync(identity.CanonicalSlug, aliases, reason ?? string.Empty, utc, note, cancellationToken).ConfigureAwait(false);
            foreach (var displayName in identity.DisplayNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await _store.SetVisibilityBySourceAsync(displayName, visible: false, cancellationToken).ConfigureAwait(false);
            }
            await _store.SetVisibilityByCreatorAsync(identity, visible: false, cancellationToken).ConfigureAwait(false);

            _creatorWhitelistPoolBuilder.Invalidate(identity.CanonicalSlug);
            TempData[BannerKey] = $"Creator {identity.CanonicalSlug} suppressed.";
            _logger.LogInformation("Creator {Action} {Slug} at {Utc}", "suppress", identity.CanonicalSlug, utc);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TempData[BannerKey] = $"Creator suppression failed: {exception.Message}";
            _logger.LogWarning("Creator {Action} {Slug} at {Utc} failed: {Reason}", "suppress", creator, utc, exception.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Purges resolved creator records after exact canonical-slug confirmation.</summary>
    [HttpPost("PurgeCreator")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurgeCreator(string creator, string confirmSlug, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        var utc = DateTimeOffset.UtcNow;
        try
        {
            var identity = await RequireIdentityAsync(creator, cancellationToken).ConfigureAwait(false);
            if (identity is null)
            {
                TempData[BannerKey] = "Creator was not found.";
                _logger.LogInformation("Creator {Action} {Slug} at {Utc} failed: {Reason}", "purge", creator, utc, "unknown");
                return RedirectToAction(nameof(Index));
            }

            if (!string.Equals(confirmSlug, identity.CanonicalSlug, StringComparison.Ordinal))
            {
                TempData[BannerKey] = $"To purge, type the canonical slug {identity.CanonicalSlug}.";
                _logger.LogInformation("Creator {Action} {Slug} at {Utc} failed: {Reason}", "purge", identity.CanonicalSlug, utc, "confirmation mismatch");
                return RedirectToAction(nameof(Index));
            }

            var result = await _purgeService.PurgeAsync(identity, cancellationToken).ConfigureAwait(false);
            _creatorWhitelistPoolBuilder.Invalidate(identity.CanonicalSlug);
            var stores = string.Join(", ", result.Stores.Select(store => store.Succeeded ? $"{store.StoreName}: {store.RowsDeleted}" : $"{store.StoreName}: failed ({store.Error})"));
            TempData[BannerKey] = $"Purge {identity.CanonicalSlug}: {stores}. Delete artifact folders by hand: {string.Join(", ", result.ArtifactFolders)}.";
            _logger.LogInformation("Creator {Action} {Slug} at {Utc}; {Stores}", "purge", identity.CanonicalSlug, utc, stores);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TempData[BannerKey] = $"Creator purge failed: {exception.Message}";
            _logger.LogWarning("Creator {Action} {Slug} at {Utc} failed: {Reason}", "purge", creator, utc, exception.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<CreatorIdentity?> RequireIdentityAsync(string creator, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(creator) ? null : await _identityResolver.ResolveAsync(creator, cancellationToken).ConfigureAwait(false);
}
