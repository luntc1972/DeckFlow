# Phase 100: Creator-Style Tool Surface - Pattern Map

**Mapped:** 2026-07-19
**Files analyzed:** 20 (new/modified)
**Analogs found:** 20 / 20 (one file — `ICreatorStyleProfileStore` listing method — has no direct analog method, only an interface-shape analog; documented under "No Analog Found")

**Scope correction (binding for this map — USER DECISION 2026-07-19, supersedes RESEARCH.md Pitfall 2's patch recommendation):** Sitemap/SEO wiring is fully DEFERRED to a post-merge follow-up. Do NOT plan/edit `SeoPaths.cs` (does not exist on this branch) and do NOT touch `DeckFlow.Web/Controllers/SitemapController.cs` at all. The current hardcoded `IndexablePaths` array does not list `/creator-style`, so D-100-07's acceptance bar ("must NOT advertise `/creator-style` while it 404s") is already satisfied by leaving the file alone. The deck-input toggle template is `Manabase.cshtml`, NOT `DeckComparison.cshtml` (`DeckComparison.cshtml` has no `data-sync-panel` toggle — see Pitfall 1 in RESEARCH.md).

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Web/Controllers/CreatorStyleController.cs` | controller | request-response | `DeckFlow.Web/Controllers/ManabaseController.cs` | exact |
| `DeckFlow.Web/Models/CreatorStyleViewModel.cs` | model (view model) | transform | `DeckFlow.Web/Models/ManabaseViewModel.cs` | exact |
| `DeckFlow.Web/Models/CreatorStyleRequest.cs` | model (request DTO) | transform | EXISTS (Phase 99) — no new pattern needed; shape already mirrors `ManabaseRequest` | n/a (pre-existing) |
| `DeckFlow.Web/Views/Deck/CreatorStyle.cshtml` | view | request-response | `DeckFlow.Web/Views/Deck/Manabase.cshtml` (form/toggle/result shape) + `DeckFlow.Web/Views/Deck/DeckComparison.cshtml` (copy-button/`.sync-column` idioms only, NOT the toggle) | exact (Manabase) / partial (DeckComparison) |
| `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs` (edit: add cache wiring) | service | CRUD + caching | `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (`PromptMutatingAnalysisFlags`/`ShouldBypassPacketCache`) | exact (pattern to replicate; net-new code in target file) |
| `DeckFlow.Web/Services/Content/CreatorStyleSeedLoader.cs` (new) + `ICreatorStyleSeedLoader.cs` | service (startup hydrator) | batch / file-I/O | `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` + `IContentKbSeedLoader.cs` | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (edit) | config / seed data | batch | same file, existing `tool.bracket.enabled` seed rows | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (edit) | config | transform | same file, `["tool.bracket.enabled"]` entry | exact |
| `DeckFlow.Web/Services/Tools/ToolRegistry.cs` (edit) | config | transform | same file, `manabase`/`bracket` `Create(...)` entries | exact |
| `DeckFlow.Web/Models/DeckPageTab.cs` (edit) | model (enum) | transform | same file, `Bracket = 15` entry | exact |
| ~~`DeckFlow.Web/Controllers/SitemapController.cs`~~ | — | — | REMOVED FROM SCOPE — sitemap wiring deferred post-merge (user decision 2026-07-19); file must not be touched; absence of `/creator-style` from `IndexablePaths` already satisfies D-100-07 | — |
| `DeckFlow.Core/Content/ICreatorStyleProfileStore.cs` (edit: add listing method) + `CreatorStyleProfileStore.cs` (edit) | store interface + impl | CRUD | `DeckFlow.Core/Content/ICreatorDeckCacheStore.GetByCreatorAsync` shape (list-by-key query already on the sibling store) | role-match |
| `DeckFlow.CLI/CreatorStyleCommandRunners.cs` (new, or add to `ContentKbCommandRunners.cs`) | CLI command handler | batch / file-I/O | `DeckFlow.CLI/ContentKbCommandRunners.cs` `RunContentIndexExportAsync` + `SerializeContentIndexExportRows` | exact |
| `DeckFlow.CLI/Program.cs` (edit: add `creator-style-index-export` command) | CLI wiring | request-response (CLI) | same file, `contentIndexExportCommand` registration block (lines ~102-107, ~338-341) | exact |
| `DeckFlow.Web/Program.cs` (edit: DI + startup hydration call) | config / composition root | batch | same file, `ContentKbSeedLoader` DI registration + `LoadIfPresentAsync()` startup call | exact |
| `content-kb/seed/creator-style-profiles.json`, `content-kb/seed/creator-deck-cache.json` (new, git-tracked) | seed data | file-I/O | `content-kb/seed/index-seed.json` (shape/location convention via `ContentKbPaths.cs`) | exact |
| `DeckFlow.Web.Tests/CreatorStyleControllerTests.cs` (new) | test | request-response | `DeckFlow.Web.Tests/ManabaseControllerTests.cs` (if present) or the `RunGuardedAsync` error-ladder test idiom used across tool controller tests | role-match |
| `DeckFlow.Web.Tests/CreatorStyleSeedLoaderTests.cs` (new) | test | batch | `DeckFlow.Web.Tests/ContentKbSeedLoaderTests.cs` | exact |
| `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` (edit: add `InlineData`) | test | batch | same file, existing `InlineData("tool.bracket.enabled", false)` row | exact |
| `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStylePacketServiceTests.cs` (edit/new cases) | test | CRUD | existing file's internal-ctor override seam pattern (already present in `CreatorStylePacketService`'s internal test constructor) | exact |
| `DeckFlow.Web/e2e/creator-style.spec.ts` (new) | test (e2e) | request-response | `DeckFlow.Web/e2e/manabase.spec.ts` (form/toggle idioms) + `DeckFlow.Web/e2e/tool-toggles.spec.ts` (404-when-off idiom) | exact |

## Pattern Assignments

### `DeckFlow.Web/Controllers/CreatorStyleController.cs` (controller, request-response)

**Analog:** `DeckFlow.Web/Controllers/ManabaseController.cs` (full file read)

**Imports pattern** (lines 1-9):
```csharp
using System.Text;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Manabase;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;
```
Swap `DeckFlow.Core.Manabase`/`DeckFlow.Web.Services.Manabase` for `DeckFlow.Web.Services.CreatorStyle` (where `ICreatorStylePacketService` lives).

**Class shape + flag gate + CSRF** (lines 16-39):
```csharp
public sealed class ManabaseController : DeckToolControllerBase
{
    private readonly IManabaseAnalysisService _manabaseAnalysisService;
    private readonly ILogger<ManabaseController> _logger;

    public ManabaseController(
        IManabaseAnalysisService manabaseAnalysisService,
        ILogger<ManabaseController> logger)
    {
        ArgumentNullException.ThrowIfNull(manabaseAnalysisService);
        ArgumentNullException.ThrowIfNull(logger);
        _manabaseAnalysisService = manabaseAnalysisService;
        _logger = logger;
    }

    [HttpGet("/manabase")]
    [FeatureFlagGate("tool.manabase.enabled")]
    public IActionResult Manabase()
    {
        return View("Manabase", new ManabaseViewModel());
    }
```
`CreatorStyleController` follows this exactly: `[HttpGet("/creator-style")]` GET action returns an empty `CreatorStyleViewModel` (but see D-100-16 — GET must also branch to the "no creator profiles loaded" state when `ICreatorStyleProfileStore` listing is empty, which Manabase's GET has no equivalent for; add that check in the GET body, not via the flag gate). `[HttpPost("/creator-style")]` mirrors the `Manabase(ManabaseRequest request)` POST action shape (lines 74-106) with `[ValidateAntiForgeryToken]` + `[FeatureFlagGate("tool.creator-style.enabled")]`.

**Error ladder / guarded execution** (lines 174-224, use verbatim structure):
```csharp
private async Task<IActionResult> RunGuardedAsync(
    ManabaseRequest request, string operation, string unexpectedMessage,
    Func<CancellationToken, Task<IActionResult>> body)
{
    using var timeoutScope = CreateTimeoutScope(LookupTimeout);
    try
    {
        return await body(timeoutScope.Token);
    }
    catch (OperationCanceledException) when (timeoutScope.IsCancellationRequested)
    {
        _logger.LogInformation("Mana-base {Operation} timed out.", operation);
        return View("Manabase", new ManabaseViewModel { Request = request, ErrorMessage = "The deck took too long to load. Try again in a moment." });
    }
    catch (InvalidOperationException exception)
    {
        _logger.LogInformation(exception, "Mana-base {Operation} failed validation.", operation);
        return View("Manabase", new ManabaseViewModel { Request = request, ErrorMessage = exception.Message });
    }
    catch (HttpRequestException exception)
    {
        _logger.LogWarning(exception, "Mana-base {Operation} hit an upstream dependency.", operation);
        return View("Manabase", new ManabaseViewModel { Request = request, ErrorMessage = UpstreamErrorMessageBuilder.BuildScryfallMessage(exception) });
    }
    catch (Exception exception)
    {
        _logger.LogError(exception, "Mana-base {Operation} failed unexpectedly.", operation);
        return View("Manabase", new ManabaseViewModel { Request = request, ErrorMessage = unexpectedMessage });
    }
}
```
Reuse verbatim, renaming to `CreatorStyleRequest`/`CreatorStyleViewModel`/`"CreatorStyle"`. Note: `CreatorStylePacketService.BuildAsync` never throws for "profile unavailable" — it returns a `CreatorStylePacketResult` with `GroundingDegraded = true` and a `Notice` (see `CreateUnavailableResult`, lines 277-291 of the service). Per D-100-16, the controller/view must inspect the *result shape*, not an exception, to distinguish "profile unavailable" from "grounding degraded" — `CreatorStylePacketResult.ArtifactText == string.Empty` combined with the two known unavailable-notice strings ("No creator style profile is available...", "...sample is insufficient...") is the only current signal; consider having `BuildAsync` return a more structured discriminator if the plan wants a cleaner IN-04 fix (Claude's Discretion/Wave-0 gap — flagged, not prescribed).

**Timeout budget:** `DeckToolControllerBase.LookupTimeout = TimeSpan.FromSeconds(20)` (`DeckFlow.Web/Controllers/DeckToolControllerBase.cs:14`) — reuse as the starting value; RESEARCH.md Pitfall 6 flags this may be tight given creator-style's extra Spellbook + two grounding-batch round-trips. Verify empirically; do not silently bump without a UAT check.

---

### `DeckFlow.Web/Models/CreatorStyleViewModel.cs` (model, transform)

**Analog:** `DeckFlow.Web/Models/ManabaseViewModel.cs` (full file, 90 lines)

```csharp
public sealed class ManabaseViewModel
{
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.Manabase;
    public ManabaseRequest Request { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public ManabaseReport? Report { get; init; }
    // ... presentation-only computed properties (HasResult, etc.)
    public bool HasResult => Report is not null;
}
```
`CreatorStyleViewModel` mirrors this shape: `ActiveTab = DeckPageTab.CreatorStyle`, `Request` (`CreatorStyleRequest`), `ErrorMessage`, a `CreatorStylePacketResult? Result` (or flattened fields: `ArtifactText`, `RubricScores`, `Exemplars`, `GroundingDegraded`, `Notice`), plus new fields this tool needs that Manabase doesn't: `IReadOnlyList<CreatorPickerOption> AvailableCreators` (for the D-100-09 dropdown) and a `bool NoProfilesLoaded` flag (D-100-16 distinct empty-store state). Use `init`-only properties and `IReadOnlyList<T>` defaults (`Array.Empty<T>()`), matching the codebase-wide convention (see `ManabaseViewModel.Unresolved`/`Suggestions`).

---

### `DeckFlow.Web/Views/Deck/CreatorStyle.cshtml` (view, request-response)

**Analog (form/toggle/page shape):** `DeckFlow.Web/Views/Deck/Manabase.cshtml` (lines 1-120 read; full toggle block at 38-57)

**Page chrome + hero + tabs + error banner** (lines 1-28):
```cshtml
@model DeckFlow.Web.Models.ManabaseViewModel
@{
    ViewData["Title"] = "MTG Commander Mana Base Analyzer";
    ViewData["Description"] = "...";
    var isUrl = Model.Request.DeckInputSource == DeckFlow.Web.Models.DeckInputSource.PublicUrl;
}

<section class="hero">
    <h1>Mana Base Analysis</h1>
    <p class="lede">...</p>
    <details class="hero-detail">
        <summary>How it works</summary>
        <p>...</p>
    </details>
</section>

@await Html.PartialAsync("_BusyIndicator")
@await Html.PartialAsync("_DeckToolTabs", Model.ActiveTab)

<div class="error-banner @(string.IsNullOrWhiteSpace(Model.ErrorMessage) ? "hidden" : string.Empty)" role="alert">
    @Model.ErrorMessage
</div>

<form method="post" action="@Url.Content("~/manabase")" class="result-panel"
      data-busy-title="Analyzing mana base" data-busy-message="..."
      data-busy-progress="..." data-busy-hold-final-step="true" data-busy-min-ms="500">
    @Html.AntiForgeryToken()
```
Do NOT include `_WorkflowStepTabs` (D-100-11) — only `_DeckToolTabs` (the persistent cross-tool nav strip, a different partial — see RESEARCH.md Anti-Patterns).

**URL/paste toggle block** (lines 38-57, clone verbatim, swap ids `manabase-*` → `creator-style-*`):
```cshtml
<div class="field">
    <label for="creator-style-input-source">Input method</label>
    <select id="creator-style-input-source" name="DeckInputSource" data-df-select>
        <option value="@DeckFlow.Web.Models.DeckInputSource.PublicUrl" selected="@(isUrl ? "selected" : null)">Use public deck URL</option>
        <option value="@DeckFlow.Web.Models.DeckInputSource.PasteText" selected="@(!isUrl ? "selected" : null)">Paste text</option>
    </select>
</div>
<div class="field @(isUrl ? string.Empty : "hidden")" data-sync-panel="creator-style-deck-url">
    <label for="creator-style-deck-url">Archidekt or Moxfield deck URL</label>
    <input id="creator-style-deck-url" type="url" name="DeckUrl" spellcheck="false" autocomplete="off"
           placeholder="https://archidekt.com/decks/…" value="@Model.Request.DeckUrl" />
    @await Html.PartialAsync("_DeckFlowBridgeHint")
</div>
<div class="field @(isUrl ? "hidden" : string.Empty)" data-sync-panel="creator-style-deck-text">
    <label for="creator-style-deck-text">Paste a decklist (one card per line, with set/collector if you have it)</label>
    <textarea id="creator-style-deck-text" name="DeckText" rows="6" spellcheck="false" autocomplete="off"
              placeholder="1 Sol Ring&#10;1 Command Tower&#10;…">@Model.Request.DeckText</textarea>
</div>
```
`CreatorStyleRequest.DeckInputSource`/`DeckUrl`/`DeckText` are already shaped identically to `ManabaseRequest` (confirmed in `DeckFlow.Web/Models/CreatorStyleRequest.cs`), so this block requires only id/label swaps, no property-shape changes.

**Copy-button + readonly result textarea (analog for the result panel):** `DeckFlow.Web/Views/Deck/DeckComparison.cshtml` lines 344-346 and 484-486 — `.copy-button` + `data-copy-target` idiom:
```cshtml
<div class="panel-heading">
    <div><h3>30-comparison-prompt.txt</h3><p>...</p></div>
    <button type="button" class="copy-button" data-copy-target="comparison-prompt-output">Copy</button>
</div>
<textarea autocomplete="off" id="comparison-prompt-output" readonly spellcheck="false" data-prompt-comparison-result-anchor>@Model.ComparisonPromptText</textarea>
```
For `CreatorStyle.cshtml`, use one `<textarea readonly>` for `ArtifactText` with a matching `.copy-button data-copy-target="creator-style-packet-output"` — per D-100-13, no multi-file packet decomposition (single copy-ready block).

**Creator `<select>` (native, `data-df-select`, no free-text/`<datalist>`):** same idiom as the input-method `<select>` above, or `CedhMetaGap.cshtml`'s `Mode`/`SortBy` selects (per UI-SPEC Interaction Contract) — do NOT use `CedhMetaGap.cshtml`'s commander-name `<datalist>` pattern (that allows free text; D-100-09 forbids it here).

**Scripts section** (Manabase.cshtml lines 721-725):
```cshtml
@section Scripts {
    <script src="~/js/busy-indicator.js" asp-append-version="true"></script>
    <script src="~/js/moxfield-extension-bridge.js" asp-append-version="true"></script>
    <script src="~/js/deck-sync.js" asp-append-version="true"></script>
}
```
Reuse verbatim — `deck-sync.js` drives the `data-sync-panel`/`data-df-select` toggle; no new script needed.

**Banner classes (D-100-15/D-100-16 states):** reuse `.warning-banner` (grounding-degraded), `.error-banner` (generic failure, already used at line 26-28 of Manabase.cshtml), `.info-banner` (empty-store state — see `DeckComparison.cshtml` line 159 for the exact `.info-banner` markup idiom). Do not invent new banner classes.

---

### `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs` (service, CRUD + caching — edit existing file)

**Analog:** `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` lines 140-212, 329-365 (full pattern read)

**Flag registry pattern to replicate** (lines 148-166):
```csharp
internal const string WinConMapFlag = "analysis.wincon-map";

internal static readonly IReadOnlyList<string> PromptMutatingAnalysisFlags = new[]
{
    CommandZoneAwarenessFlag,
    MultiAxisScoreFlag,
    InteractionAuditFlag,
    WinConMapFlag,
    ReferenceDeckStatsFlag,
};
```
New code for `CreatorStylePacketService`:
```csharp
internal static readonly IReadOnlyList<string> PromptMutatingCreatorStyleFlags = new[]
{
    "tool.creator-style.enabled",
};
```
Per D-100-06/Pitfall 5 (RESEARCH.md): this is the tool's OWN visibility flag, not a separate content-tuning flag — that is intentional (see RESEARCH.md Pitfall 5 rationale). Implement as specified; do not "simplify" it away.

**Constructor: add `PacketSessionCache` + `IFeatureFlagCache?`** (mirrors lines 175-212):
```csharp
internal DeckAnalysisPacketService(
    /* ...existing deps... */
    PacketSessionCache packetCache,
    IFeatureFlagCache? flagCache = null,
    ILogger<DeckAnalysisPacketService>? logger = null)
{
    /* ...ArgumentNullException.ThrowIfNull(...) for each required dep... */
    ArgumentNullException.ThrowIfNull(packetCache);
    _packetCache = packetCache;
    _flagCache = flagCache;
    _logger = logger ?? NullLogger<DeckAnalysisPacketService>.Instance;
}
```
`CreatorStylePacketService`'s production constructor (currently 5 required deps + optional logger, lines 122-142 of the target file) needs `PacketSessionCache packetCache` added as a required param and `IFeatureFlagCache? flagCache = null` added as optional — both stored in new `_packetCache`/`_flagCache` fields. The internal test-seam constructor (lines 144-160) needs matching optional override params if the plan wants cache behavior test-doubled (recommended, matching `DeckAnalysisPacketService`'s override-delegate seam style already used for every other dependency in this file).

**Read-side flag-on check + bypass predicate** (lines 344-364):
```csharp
private bool IsAnalysisFlagOn(string flagKey)
    => _flagCache is not null
        && _flagCache.Snapshot().TryGetValue(flagKey, out var on)
        && on;

private bool ShouldBypassPacketCache()
    => PromptMutatingAnalysisFlags.Any(IsAnalysisFlagOn);
```
Replicate verbatim as `IsCreatorStyleFlagOn`/`ShouldBypassPacketCache` on the new service, iterating `PromptMutatingCreatorStyleFlags`.

**Write-side latched-local pattern (critical — read the full write-side block before implementing):** `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` around line 589 and the "Both BuildAsync and TryComputeCacheKeyAsync route through the SAME two shared helpers" comment block near line 960-976 — the latched-local is read ONCE at the top of `BuildAsync`, then reused at the write site, so a mid-request flag flip cannot desync the enrichment decision from the cache-write decision (see the code comment: "Codex LOW/MED code-review finding #1"). `CreatorStylePacketService.BuildAsync` (lines 163-275 of the current file) must add:
```csharp
bool bypassCacheWrite = ShouldBypassPacketCache(); // latch ONCE at top of BuildAsync
// ... existing pipeline unchanged ...
// at the point the result would be written to PacketSessionCache:
if (!bypassCacheWrite)
{
    await _packetCache.SetAsync(cacheKey, result, cancellationToken);
}
```
And the controller (or a `TryComputeCacheKeyAsync`-equivalent method added to `ICreatorStylePacketService`) must call `ShouldBypassPacketCache()` on the READ side before attempting `_packetCache.TryGet`, mirroring `TryComputeCacheKeyAsync`'s read-side gate (lines 379-428, esp. line 394 `if (ShouldBypassPacketCache()) { ... }`).

---

### `DeckFlow.Web/Services/Content/CreatorStyleSeedLoader.cs` (service, batch/file-I/O — new)

**Analog:** `DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs` (full file, 121 lines)

```csharp
public sealed class ContentKbSeedLoader : IContentKbSeedLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ContentKbArtifactPathResolver _resolver;
    private readonly IContentSiteIndexStore _store;
    private readonly ILogger<ContentKbSeedLoader> _logger;

    public ContentKbSeedLoader(ContentKbArtifactPathResolver resolver, IContentSiteIndexStore store, ILogger<ContentKbSeedLoader> logger)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);
        _resolver = resolver; _store = store; _logger = logger;
    }

    public async Task<int> LoadIfPresentAsync(CancellationToken cancellationToken = default)
    {
        var seedFilePath = _resolver.SeedFilePath;
        if (!File.Exists(seedFilePath))
        {
            _logger.LogInformation("Content KB seed file not found; skipping seed load.");
            return 0;
        }

        await using var stream = File.OpenRead(seedFilePath);
        var entries = await JsonSerializer.DeserializeAsync<ContentKbSeedEntry[]>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? Array.Empty<ContentKbSeedEntry>();

        foreach (var entry in entries)
        {
            var row = BuildRow(entry);
            await _store.UpsertRowPreservingVisibilityAsync(row, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Content KB seed load complete: {RowCount} rows.", entries.Length);
        return entries.Length;
    }

    private sealed record ContentKbSeedEntry { /* required init-only props matching JSON shape */ }
}
```
`CreatorStyleSeedLoader` forks this for TWO files/stores (per RESEARCH.md Architecture Pattern 4): read `content-kb/seed/creator-style-profiles.json` → deserialize `CreatorStyleProfile[]` → `foreach { await _profileStore.UpsertAsync(profile, ct); }`; read `content-kb/seed/creator-deck-cache.json` → deserialize `CreatorDeckCacheEntry[]` → `foreach { await _deckCacheStore.UpsertAsync(entry, ct); }`. Note: unlike `ContentKbSeedLoader`, neither `ICreatorStyleProfileStore.UpsertAsync` nor `ICreatorDeckCacheStore.UpsertAsync` has a "preserving visibility" variant — they are plain upserts (confirmed via full interface reads of `ICreatorStyleProfileStore.cs`/`ICreatorDeckCacheStore.cs`), so no `SeedManaged`-style field mapping is needed. File-not-found → log info + return 0, matching the pattern exactly (both files independently optional/present-checked).

**Path convention analog:** `DeckFlow.Core/Content/ContentKbPaths.cs`:
```csharp
public static class ContentKbPaths
{
    public const string SeedRelativePath = "content-kb/seed/index-seed.json";
}
```
Add sibling consts (`CreatorStyleProfileSeedRelativePath = "content-kb/seed/creator-style-profiles.json"`, `CreatorDeckCacheSeedRelativePath = "content-kb/seed/creator-deck-cache.json"`) to this same static class — it is already the single source of truth shared by Web, Studio (referenced in comments), and CLI.

**Program.cs registration + startup call** (`DeckFlow.Web/Program.cs` lines 113-114, 288):
```csharp
builder.Services.AddSingleton<ContentKbArtifactPathResolver>();
builder.Services.AddSingleton<IContentKbSeedLoader, ContentKbSeedLoader>();
// ...
await app.Services.GetRequiredService<IContentKbSeedLoader>().LoadIfPresentAsync();
```
Add `builder.Services.AddSingleton<ICreatorStyleSeedLoader, CreatorStyleSeedLoader>();` alongside the existing content-kb DI block (lines 108-114, right after the `ICreatorStyleProfileStore`/`ICreatorDeckCacheStore` singleton registrations at lines 98-111), and a matching startup call right after the existing `LoadIfPresentAsync()` call at line 288 (before or after — order relative to `ContentBodyHashBackfill`/`SeedManagedBackfill` doesn't matter since those touch `content_site_index`, a disjoint table).

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (config, batch — edit both seed SQL blocks)

**Analog:** existing `tool.bracket.enabled` seed rows (this same file)

**Postgres block** (lines 198-237, insertion point after line 228's `tool.bracket.enabled` row or in the `tool.*` cluster):
```sql
INSERT INTO feature_flags (key, enabled) VALUES
  ...
  ('tool.bracket.enabled', FALSE),
  ('tool.creator-style.enabled', FALSE),   -- new row
  ...
ON CONFLICT (key) DO NOTHING;
```
**SQLite block** (lines 239-278, same insertion point):
```sql
INSERT INTO feature_flags (key, enabled) VALUES
  ...
  ('tool.bracket.enabled', 0),
  ('tool.creator-style.enabled', 0),   -- new row
  ...
ON CONFLICT (key) DO NOTHING;
```
CS-30 requires BOTH blocks edited in the same commit — this is a documented anti-pattern miss (RESEARCH.md Anti-Patterns: "Seeding the flag in only one dialect's SQL block").

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (config, transform — edit)

**Analog:** `["tool.bracket.enabled"]` entry (lines 74-76):
```csharp
["tool.bracket.enabled"] =
    "Enable the Bracket Check tool — auto-classify a Commander deck into its official 1-5 bracket " +
    "and generate a balancer prompt. Off = byte-identical to pre-Phase-76.",
```
Add:
```csharp
["tool.creator-style.enabled"] =
    "Enable the Creator-Style Critique tool that builds a ChatGPT-ready packet scoring a submitted deck against a chosen creator's measured build style.",
```
`FeatureFlagCatalogTests` fails the build if this entry is missing for any seeded key — non-optional.

---

### `DeckFlow.Web/Services/Tools/ToolRegistry.cs` (config, transform — edit)

**Analog:** the `manabase`/`bracket` `Create(...)` calls (lines 15-16):
```csharp
Create("manabase", "Mana Base", "/manabase", ToolNavSection.Analyze, "tool.manabase.enabled", false,
    "Mana Base", "Score a deck's lands...", "manabase", DeckPageTab.Manabase, false),
Create("bracket", "Bracket Check", "/bracket", ToolNavSection.Analyze, "tool.bracket.enabled", false,
    "Bracket Check", "Classify a Commander deck...", "bracket", DeckPageTab.Bracket, false),
```
Add (per UI-SPEC copywriting contract, D-100-12 craft-first tone):
```csharp
Create("creator-style", "Creator-Style Critique", "/creator-style", ToolNavSection.Analyze,
    "tool.creator-style.enabled", false,
    "Creator-Style Critique",
    "Critique your deck against a creator's measured build style — real exemplars, weighted targets, no vibes.",
    "creator-style", DeckPageTab.CreatorStyle, false),
```
`Create(...)`'s private signature (lines 31-43) takes `params string[] additionalRoutes` last — creator-style needs none (no extra API sub-routes like `deck-sync`'s `/resolve`/`/api/deck/diff`), so omit trailing args.

---

### `DeckFlow.Web/Models/DeckPageTab.cs` (model enum — edit)

**Analog:** existing `Bracket = 15` entry (lines 50-51):
```csharp
/// <summary>Bracket classifier and balancer page.</summary>
Bracket = 15,
```
Add:
```csharp
/// <summary>Creator-style critique artifact generator page.</summary>
CreatorStyle = 16,
```

---

### ~~`DeckFlow.Web/Controllers/SitemapController.cs`~~ — REMOVED FROM SCOPE

**USER DECISION 2026-07-19:** sitemap/SEO wiring deferred to post-merge follow-up. This file must NOT be touched by any Phase 100 plan. Current state kept below for reference only — note `/creator-style` is absent from `IndexablePaths`, so the sitemap already does not advertise the flagged-off route (D-100-07 satisfied by inaction).

**Current state (reference only — DO NOT EDIT):**
```csharp
public sealed class SitemapController : Controller
{
    private static readonly string[] IndexablePaths =
    {
        "/", "/sync", "/convert", "/card-lookup", "/mechanic-lookup",
        "/deck-analysis", "/deck-comparison", "/cedh-meta-gap", "/deck-primer",
        "/suggest-categories", "/commander-categories", "/judge-questions",
        "/content-kb", "/help", "/about", "/feedback",
    };
    // note: /manabase and /bracket are ALSO missing here already — pre-existing staleness, not this phase's bug to fix.

    [HttpGet("/sitemap.xml")]
    public ContentResult SitemapXml()
    {
        var baseUrl = BuildBaseUrl();
        XNamespace ns = "...";
        var document = new XDocument(new XElement(ns + "urlset",
            IndexablePaths.Select(path => new XElement(ns + "url",
                new XElement(ns + "loc", BuildAbsoluteUrl(baseUrl, path))))));
        return Content(document.ToString(SaveOptions.DisableFormatting), "application/xml");
    }
}
```
No patch pattern — deferred. The post-merge follow-up will add `/creator-style` to main's `SeoPaths` registry (which supersedes this controller's array there) once the flag is flipped ON in prod.

---

### `DeckFlow.Core/Content/ICreatorStyleProfileStore.cs` + `CreatorStyleProfileStore.cs` (store interface + impl — edit, add listing method)

**Analog (sibling store's list-by-key query shape):** `DeckFlow.Core/Content/ICreatorDeckCacheStore.cs`:
```csharp
Task<IReadOnlyList<CreatorDeckCacheEntry>> GetByCreatorAsync(string creatorSlug, CancellationToken cancellationToken = default);
```
**Current `ICreatorStyleProfileStore` (full interface, 30 lines) has only:**
```csharp
Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default);
Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
```
No `GetAll*` method exists (confirmed — RESEARCH.md Pitfall 4). Add:
```csharp
Task<IReadOnlyList<CreatorStyleProfileSummary>> GetAllAsync(CancellationToken cancellationToken = default);
```
Implementation in `CreatorStyleProfileStore.cs` follows the existing `GetBySlugAsync` query shape (lines 111-127 of that file — `QuerySingleOrDefaultAsync` → swap for `QueryAsync` over `SELECT slug, min_decks, ... FROM creator_style_profile;` with no WHERE clause), reusing the same `OpenConnectionAsync`/`EnsureSchemaAsync` scaffolding already on every other method in the class (lines 66-137). Per RESEARCH.md Open Question 1 / A2: decide (or default to) deck-count-only for the evidence-depth label (`MinDecks` is already on `CreatorStyleProfile`; a video count needs an extra join to `ContentVideoStore`/`ContentSiteIndexStore` — flagged as a design open point, not prescribed here).

---

### `DeckFlow.CLI/CreatorStyleCommandRunners.cs` (new) + `Program.cs` command registration (CLI)

**Analog:** `DeckFlow.CLI/ContentKbCommandRunners.cs` `RunContentIndexExportAsync` (lines 364-401) + `SerializeContentIndexExportRows` (lines 536-548):
```csharp
public static async Task<int> RunContentIndexExportAsync(FileInfo? db, FileInfo? output)
{
    try
    {
        var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
        var artifactRoot = ContentKbCliPaths.ResolveArtifactRoot(db);
        var orchestrator = CreateSqliteOrchestrator(dbPath, artifactRoot, /* Throwing*-stub deps for export-only path */);
        var result = await orchestrator.ExportIndexAsync().ConfigureAwait(false);
        if (!result.Success)
        {
            Console.Error.WriteLine(result.Message);
            return 1;
        }

        var outputPath = output?.FullName ?? ContentKbPaths.SeedRelativePath;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Directory.GetCurrentDirectory());
        await File.WriteAllTextAsync(outputPath, SerializeContentIndexExportRows(result.Rows)).ConfigureAwait(false);
        Console.WriteLine($"Exported {result.RowCount} rows to {outputPath}");
        return 0;
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

internal static string SerializeContentIndexExportRows(IReadOnlyList<ContentIndexExportRow> rows)
{
    var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    });
    return json + "\n";
}
```
`RunCreatorStyleIndexExportAsync(FileInfo? db, FileInfo? output)` follows this exactly: read local `ICreatorStyleProfileStore`/`ICreatorDeckCacheStore` (constructed directly against the resolved SQLite path, same as other CLI runners), serialize each to its own JSON file (two `File.WriteAllTextAsync` calls, two output paths, or a single `--output-dir`), same `JsonSerializerOptions` (camelCase + indented), same try/catch/exit-code convention, same trailing-newline convention (`json + "\n"`).

**Program.cs command registration** (lines 102-107, 338-341):
```csharp
var contentIndexExportCommand = new Command("content-index-export", "Exports the local content_site_index to a tracked JSON seed file for commit-then-deploy.");
var contentIndexExportDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var contentIndexExportOutputOption = new Option<FileInfo?>("--output", () => new FileInfo(ContentKbPaths.SeedRelativePath)) { Description = "..." };
// ...
contentIndexExportCommand.SetHandler((FileInfo? db, FileInfo? output) =>
{
    Environment.ExitCode = ContentKbCommandRunners.RunContentIndexExportAsync(db, output).GetAwaiter().GetResult();
}, contentIndexExportDbOption, contentIndexExportOutputOption);
```
Add `creator-style-index-export` following this exact three-part shape (Command + Options + `SetHandler`), registered via `rootCommand.AddCommand(...)` alongside the other content-kb-family commands.

---

### `DeckFlow.Web/Program.cs` (composition root — edit, DI wiring)

**Analog block** (lines 95-121):
```csharp
builder.Services.AddSingleton<DeckFlow.Core.Content.ICreatorDeckCacheStore>(_ =>
    new DeckFlow.Core.Content.CreatorDeckCacheStore(
        DeckFlowDatabaseConnectionFactory.CreateCreatorDeckCacheConnection(builder.Environment)));
builder.Services.AddSingleton<DeckFlow.Core.Content.ICreatorStyleProfileStore>(_ =>
    // Why: creator-style profiles live in the local-only content-kb.db per the CLI (ContentKbCommandRunners)
    // and Studio (Program.cs:92) convention (D-14: content-kb never ships to Render).
    new DeckFlow.Core.Content.CreatorStyleProfileStore(
        DeckFlowDatabaseConnectionFactory.CreateLocalContentKbConnection(builder.Environment)));
builder.Services.AddSingleton<ContentKbArtifactPathResolver>();
builder.Services.AddSingleton<IContentKbSeedLoader, ContentKbSeedLoader>();
```
These two store registrations already exist unchanged — only ADD the new `ICreatorStyleSeedLoader` registration alongside them (see the seed-loader section above). No change to the store bindings themselves.

**Startup hydration call** (line 288, in `ValidateDatabaseConnectionsAsync`/schema-ensure sequence):
```csharp
await app.Services.GetRequiredService<DeckFlow.Core.Content.IContentSiteIndexStore>().EnsureSchemaAsync();
await app.Services.GetRequiredService<IContentKbSeedLoader>().LoadIfPresentAsync();
```
Add, immediately after (schema for the two new stores is auto-ensured lazily by their own `EnsureSchemaAsync` gates on first `UpsertAsync`/`GetBySlugAsync` call per `CreatorStyleProfileStore.cs` lines 67-85, so an explicit `EnsureSchemaAsync()` pre-call before the seed loader is optional but matches the existing sequencing convention if the plan wants belt-and-suspenders):
```csharp
await app.Services.GetRequiredService<ICreatorStyleSeedLoader>().LoadIfPresentAsync();
```

---

### Test files

**`CreatorStyleSeedLoaderTests.cs`** — analog: `DeckFlow.Web.Tests/ContentKbSeedLoaderTests.cs` (file-not-found → 0 rows; present file → N upserts against a fake/in-memory store; malformed JSON → throws or logs, matching `ContentKbSeedLoaderTests`'s exact assertions).

**`FeatureFlagStoreSeedTests.cs`** (edit) — analog: existing rows in the same file:
```csharp
[InlineData("tool.bracket.enabled", false)] // BRACKET-05: seeded OFF
```
Add:
```csharp
[InlineData("tool.creator-style.enabled", false)] // CS-30: seeded OFF
```

**`CreatorStylePacketServiceTests.cs`** (edit/new cases) — the service already has an internal test-seam constructor (lines 144-160 of `CreatorStylePacketService.cs`) with override delegates for every collaborator (`getProfileAsync`, `buildSubmittedDeckAsync`, `buildWhitelistAsync`, `validateAdditionalCardsAsync`, `getCreatorDecksAsync`, `scoreRubric`) — this is the exact seam pattern (`[InternalsVisibleTo("DeckFlow.Web.Tests")]`, per project convention) new cache-bypass tests should extend with a `PacketSessionCache`-aware override, mirroring however `DeckAnalysisPacketServiceTests` tests `ShouldBypassPacketCache`/`TryComputeCacheKeyAsync` (grep that test file for the exact assertion idiom before writing).

---

### `DeckFlow.Web/e2e/creator-style.spec.ts` (e2e, request-response)

**Analog 1 (form/toggle idioms):** `DeckFlow.Web/e2e/manabase.spec.ts` lines 9-31:
```typescript
test('manabase page renders the deck-input form', async ({ page }) => {
  const consoleErrors: string[] = [];
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });

  const response = await page.goto('/manabase');
  expect(response?.ok()).toBeTruthy();

  await expect(page.locator('form[action="/manabase"]')).toBeVisible();
  await expect(page.locator('#manabase-input-source')).toBeVisible();
  await expect(page.locator('#manabase-deck-url')).toBeVisible();
  await expect(page.locator('#manabase-deck-text')).toBeAttached();
  await expect(page.locator('#manabase-deck-text')).toBeHidden();

  expect(consoleErrors).toEqual([]);
});
```
Adapt id selectors to `creator-style-input-source`/`creator-style-deck-url`/`creator-style-deck-text`.

**Analog 2 (flag-off 404 idiom):** `DeckFlow.Web/e2e/tool-toggles.spec.ts` lines 80-105 (`'hide flow removes card lookup everywhere and disabled routes return 404'`):
```typescript
const cardLookupRoute = await page.goto('/card-lookup');
expect(cardLookupRoute?.status()).toBe(404);
```
For creator-style, since the flag is seeded OFF (unlike card-lookup's default-ON), the simpler assertion is direct — no admin toggle-off step needed first:
```typescript
test('creator-style route 404s while the flag is off (seeded default)', async ({ page }) => {
  const response = await page.goto('/creator-style');
  expect(response?.status()).toBe(404);
});
```
A second test (after an admin flip via the `/Admin/Tools`/`/Admin/Flags` helper idioms already in `tool-toggles.spec.ts`, e.g. `restoreAllTogglesOn`/toggle-off helpers) should assert 200 + form visible + nav/tile presence once ON, mirroring the `'show flow restores card lookup everywhere and the route returns 200'` test (line 134) shape.

**Playwright projects (desktop+mobile, required by CS-31):** `DeckFlow.Web/playwright.config.ts` already defines `chromium-desktop` (1280x900) and `chromium-mobile` (390x844) — no new config needed; `npx playwright test creator-style` runs both projects automatically.

---

## Shared Patterns

### Feature-flag-gated tool page (four coordinated touch points)
**Sources:** `DeckFlow.Web/Services/Tools/ToolRegistry.cs`, `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (both `PostgresSeedSql`/`SqliteSeedSql`), `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs`, `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs`
**Apply to:** `CreatorStyleController` (both actions), `ToolRegistry`, `FeatureFlagStore` (×2 dialects), `FeatureFlagCatalog`
```csharp
[FeatureFlagGate("tool.creator-style.enabled")]   // controller action attribute
```
```csharp
var cache = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagCache>();
if (cache.IsEnabled(Key)) { await next().ConfigureAwait(false); return; }
context.Result = new NotFoundResult();
```
All four touch points must use the exact string `"tool.creator-style.enabled"` — one commit, checklist discipline (per RESEARCH.md "Key insight").

### Prompt-mutating flag → PacketSessionCache bypass (net-new wiring, replicated pattern)
**Source:** `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` lines 148-166, 344-364
**Apply to:** `CreatorStylePacketService.BuildAsync` (write-side latched local) + a new `TryComputeCacheKeyAsync`-equivalent (read-side gate) on `ICreatorStylePacketService`, called from `CreatorStyleController` before invoking `BuildAsync`.
See full excerpt above under the `CreatorStylePacketService.cs` pattern assignment.

### Error-message translation for upstream HTTP failures
**Source:** `UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)` — used verbatim in `ManabaseController.RunGuardedAsync`'s `catch (HttpRequestException exception)` block.
**Apply to:** `CreatorStyleController`'s guarded execution wrapper — identical catch-block shape.

### CSRF protection on POST
**Source:** `@Html.AntiForgeryToken()` (Manabase.cshtml line 36) + `[ValidateAntiForgeryToken]` (ManabaseController.cs lines 47, 75, 116).
**Apply to:** `CreatorStyleController`'s POST action + `CreatorStyle.cshtml`'s `<form>`.

### Copy-ready textarea + copy button
**Source:** `DeckComparison.cshtml` — `.copy-button` + `data-copy-target="<textarea id>"`, e.g. lines 344-346 / 484-486.
**Apply to:** `CreatorStyle.cshtml`'s single result `<textarea readonly>` (D-100-13 — one copy-ready block, no multi-file decomposition).

### Deck-tool nav tab partial (NOT the multi-step wizard)
**Source:** `@await Html.PartialAsync("_DeckToolTabs", Model.ActiveTab)` (Manabase.cshtml line 24) — distinct from `_WorkflowStepTabs` (DeckComparison.cshtml line 221), which D-100-11 explicitly forbids for this page.
**Apply to:** `CreatorStyle.cshtml`.

### Sqlite/Postgres dialect-guarded store scaffolding
**Source:** `CreatorStyleProfileStore.cs` (full file) — `EnsureSchemaAsync`/`OpenConnectionAsync`/`_connectionInfo.IsPostgres ? Postgres...Sql : Sqlite...Sql` idiom, `ON CONFLICT (key) DO UPDATE`/`DO NOTHING` per Postgres/SQLite EXCLUDED-works-on-both convention.
**Apply to:** the new `GetAllAsync`/`GetSummariesAsync` method added to `ICreatorStyleProfileStore`/`CreatorStyleProfileStore`.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `ICreatorStyleProfileStore.GetAllAsync` (new listing method) | store interface method | CRUD (read, list) | No existing method on this interface enumerates all rows — `GetBySlugAsync` is single-key only. Closest analog is `ICreatorDeckCacheStore.GetByCreatorAsync` (a different key-scoped list, not an unscoped list-all). The query itself is a trivial unparameterized `SELECT ... FROM creator_style_profile;` using the store's existing `EnsureSchemaAsync`/`OpenConnectionAsync` scaffolding — low risk, but the exact return shape (`CreatorStyleProfileSummary` DTO fields) is Claude's Discretion per CONTEXT.md and RESEARCH.md Open Question 1. |
| Evidence-depth video-count join (picker label "N decks · M videos") | data aggregation | transform | `CreatorStyleProfile` has `MinDecks` but no video count; that lives in `ContentVideoStore`/`ContentSiteIndexStore`, a different table keyed by content source, not creator slug directly. No existing code joins these two today. RESEARCH.md flags this as Assumption A2 / Open Question 2 — planner should decide whether v1 ships deck-count-only or invests in the join. |

## Metadata

**Analog search scope:** `DeckFlow.Web/Controllers/`, `DeckFlow.Web/Views/Deck/`, `DeckFlow.Web/Models/`, `DeckFlow.Web/Services/` (incl. `Content/`, `CreatorStyle/`, `FeatureFlags/`, `Tools/`), `DeckFlow.Web/Infrastructure/`, `DeckFlow.Core/Content/`, `DeckFlow.CLI/`, `DeckFlow.Web.Tests/`, `DeckFlow.Web/e2e/`, `README.md`, `DeckFlow.Web/Program.cs`.
**Files scanned:** ~30 (full or targeted reads); all cited paths verified directly against the working tree during this session (no reliance on RESEARCH.md's citations without independent confirmation for load-bearing excerpts).
**Pattern extraction date:** 2026-07-19
