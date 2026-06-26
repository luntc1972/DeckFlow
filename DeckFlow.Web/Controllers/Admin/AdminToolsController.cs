using DeckFlow.Web.Security;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Tools;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Admin;

internal sealed class AdminToolSectionViewModel
{
    public required ToolNavSection Section { get; init; }

    public required IReadOnlyList<AdminToolRowViewModel> Tools { get; init; }
}

internal sealed record AdminToolRowViewModel(string Key, string Label, string FlagKey, bool Core, bool Enabled);

internal sealed class AdminToolsListViewModel
{
    public IReadOnlyList<AdminToolSectionViewModel> Sections { get; init; } = Array.Empty<AdminToolSectionViewModel>();

    public IReadOnlyList<string> DisabledCoreToolLabels { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Operator UI for runtime public-tool visibility flags. GET renders all registry-backed
/// tools grouped by section; POST persists a toggle then synchronously reloads the cache
/// so the new value is visible on the redirect-target GET in the same round trip.
/// </summary>
[Route("Admin/Tools")]
public sealed class AdminToolsController : Controller
{
    private readonly IFeatureFlagStore _store;
    private readonly IFeatureFlagCache _cache;
    private readonly IToolRegistry _registry;

    /// <summary>
    /// Initializes the controller with the registry-backed tool catalog and feature-flag
    /// services used to read and persist tool visibility state.
    /// </summary>
    /// <param name="store">Feature-flag persistence store used for toggle writes.</param>
    /// <param name="cache">In-memory feature-flag cache used for reads and reloads.</param>
    /// <param name="registry">Canonical public-tool registry used to constrain this page.</param>
    public AdminToolsController(
        IFeatureFlagStore store,
        IFeatureFlagCache cache,
        IToolRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(registry);
        _store = store;
        _cache = cache;
        _registry = registry;
    }

    /// <summary>
    /// Renders every public tool from the registry grouped by section in enum order, with
    /// live enabled state sourced from the current feature-flag cache snapshot.
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        var snapshot = _cache.Snapshot();
        var rows = _registry.All
            .Select(tool => new
            {
                tool.Section,
                Row = new AdminToolRowViewModel(
                    tool.Key,
                    tool.Label,
                    tool.FlagKey,
                    tool.Core,
                    snapshot.TryGetValue(tool.FlagKey, out var enabled) ? enabled : true),
            })
            .ToArray();
        var sections = Enum
            .GetValues<ToolNavSection>()
            .Select(section => new AdminToolSectionViewModel
            {
                Section = section,
                Tools = rows
                    .Where(row => row.Section == section)
                    .Select(row => row.Row)
                    .ToArray(),
            })
            .ToArray();
        var vm = new AdminToolsListViewModel
        {
            Sections = sections,
            DisabledCoreToolLabels = rows
                .Where(row => row.Row.Core && !row.Row.Enabled)
                .Select(row => row.Row.Label)
                .ToArray(),
        };
        return View(vm);
    }

    /// <summary>
    /// Persists a registry-backed tool visibility toggle and reloads the cache immediately
    /// so the redirected GET sees the new value. Requests must be same-origin and the posted
    /// key must match a tool flag from the registry; infra flags are rejected here.
    /// </summary>
    /// <param name="flagKey">Feature-flag key for the tool being toggled.</param>
    /// <param name="enabled">Desired enabled state posted by the toggle form.</param>
    /// <param name="cancellationToken">Request cancellation token for store/cache work.</param>
    [HttpPost("{flagKey}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string flagKey, bool enabled, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
        }

        if (string.IsNullOrWhiteSpace(flagKey))
        {
            return BadRequest();
        }

        var tool = _registry.All.FirstOrDefault(candidate => string.Equals(candidate.FlagKey, flagKey, StringComparison.Ordinal));
        if (tool is null)
        {
            return BadRequest("Unknown tool.");
        }

        await _store.SetEnabledAsync(flagKey, enabled, cancellationToken).ConfigureAwait(false);
        await _cache.ReloadAsync(cancellationToken).ConfigureAwait(false);

        TempData["AdminToolsAction"] = $"Tool '{tool.Label}' is now {(enabled ? "enabled" : "disabled")}.";
        if (tool.Core && !enabled)
        {
            TempData["AdminToolsWarning"] = $"Warning: '{tool.Label}' is a core Analyze workflow and is now hidden.";
        }

        return RedirectToAction(nameof(Index));
    }
}
