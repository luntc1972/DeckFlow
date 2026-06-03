using DeckFlow.Web.Security;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// View model for the /Admin/Flags index page (Phase 6, FLAG-03). Renders the current
/// IFeatureFlagCache snapshot as a sorted table; each row carries a single toggle button
/// posting to <see cref="AdminFlagsController.Toggle"/>.
/// </summary>
public sealed class AdminFlagsListViewModel
{
    /// <summary>Sorted (Ordinal) list of flag rows from the live cache snapshot.</summary>
    public IReadOnlyList<FlagRow> Flags { get; init; } = Array.Empty<FlagRow>();
}

/// <summary>Single flag row: dotted key + current enabled state.</summary>
/// <param name="Key">Dotted-namespace flag key (e.g. "scryfall.tagger.enabled").</param>
/// <param name="Enabled">Current enabled state from the cache snapshot.</param>
public sealed record FlagRow(string Key, bool Enabled);

/// <summary>
/// Operator UI for runtime feature flags (Phase 6, ADMIN-05 + FLAG-03).
/// GET renders the cache snapshot; POST persists a toggle then synchronously reloads the
/// cache so the new value is visible on the redirect-target GET (D-10 same-round-trip).
/// Sits behind the existing /Admin BasicAuth branch (Program.cs:330-332).
/// </summary>
[Route("Admin/Flags")]
public sealed class AdminFlagsController : Controller
{
    private readonly IFeatureFlagStore _store;
    private readonly IFeatureFlagCache _cache;

    /// <summary>Constructor injecting the persistence store and the in-memory cache.</summary>
    /// <param name="store">Feature-flag persistence (PG/SQLite) — write target.</param>
    /// <param name="cache">In-memory snapshot — read target + sync reload after write.</param>
    public AdminFlagsController(IFeatureFlagStore store, IFeatureFlagCache cache)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(cache);
        _store = store;
        _cache = cache;
    }

    /// <summary>
    /// Renders the flag list sorted by key (Ordinal). Reads
    /// <c>TempData["AdminFlagsAction"]</c> for the post-toggle success banner.
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        var snapshot = _cache.Snapshot();
        var rows = snapshot
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new FlagRow(kv.Key, kv.Value))
            .ToArray();
        var vm = new AdminFlagsListViewModel { Flags = rows };
        return View(vm);
    }

    /// <summary>
    /// Persists a flag toggle and synchronously reloads the in-memory cache (D-10) so the
    /// new value is visible on the redirect-target GET. Validates the key against the
    /// current snapshot before writing — unknown keys cannot be created via this endpoint
    /// (T-06-E2 mitigation).
    /// </summary>
    /// <param name="key">Dotted-namespace flag key (must exist in the current snapshot).</param>
    /// <param name="enabled">Desired new enabled state (the view posts the flipped value).</param>
    /// <param name="cancellationToken">Request-aborted token threaded through to PG + cache reload.</param>
    [HttpPost("{key}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string key, bool enabled, CancellationToken cancellationToken)
    {
        // HIGH-4 / D-22E: same-origin guard alongside the existing anti-forgery token so every
        // mutating admin POST is double-CSRF-guarded (SC4/P11), including this reused toggle.
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest();
        }

        // T-06-E2: only allow toggling keys the cache already knows about. Prevents an
        // attacker (with valid creds + token, e.g. via leaked browser session) from
        // creating arbitrary new flag rows that downstream code never expects to see.
        var snapshot = _cache.Snapshot();
        if (!snapshot.ContainsKey(key))
        {
            return BadRequest("Unknown flag key.");
        }

        await _store.SetEnabledAsync(key, enabled, cancellationToken).ConfigureAwait(false);
        await _cache.ReloadAsync(cancellationToken).ConfigureAwait(false);

        TempData["AdminFlagsAction"] = $"Flag '{key}' is now {(enabled ? "enabled" : "disabled")}.";
        return RedirectToAction(nameof(Index));
    }
}
