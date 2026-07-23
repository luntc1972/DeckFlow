# Phase 101: Intake & Protection Foundation - Pattern Map

**Mapped:** 2026-07-18
**Files analyzed:** 13 (new) + 6 (modified)
**Analogs found:** 13 / 13

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `DeckFlow.Web/Controllers/CutLabController.cs` | controller | request-response | `DeckFlow.Web/Controllers/DeckHistoryController.cs` | exact |
| `DeckFlow.Web/Models/CutLabRequest.cs` | model (form-bound request) | request-response | `DeckFlow.Web/Models/DeckHistoryRequest.cs` | exact |
| `DeckFlow.Web/Models/CutLabViewModel.cs` | model (view model) | request-response | `DeckFlow.Web/Models/DeckHistoryViewModel.cs` | exact |
| `DeckFlow.Web/Models/DeckPageTab.cs` (modified — add `CutLab` member) | model (enum) | — | same file, existing entries (e.g. `DeckHistory = 16`) | exact |
| `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` | service (orchestrator) | request-response, CRUD-ish (load→validate→resolve→classify) | `DeckFlow.Web/Services/DeckHistoryPageService.cs` | exact |
| `DeckFlow.Web/Services/CutLab/CutLabPoolValidator.cs` | service (validator, ~10 lines) | transform | `ManabaseAnalysisService.MaxDeckCards` ceiling check (`DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:660-694`) | role-match (ceiling → range) |
| `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` | service (JSON round-trip + size cap) | transform, file-I/O-shaped (serialize/deserialize) | `DeckFlow.Core/History/DeckHistorySerializer.cs` | exact |
| `DeckFlow.Core/Manabase/CardTypeLine.cs` (reused, possibly extended) | utility | transform | itself — `ManabaseClassifier.IsLandType` (`DeckFlow.Core/Manabase/ManabaseClassifier.cs:1393-1397`) is the private wrapper to either promote or duplicate | exact (already exists) |
| `DeckFlow.Web/Views/Deck/CutLab.cshtml` | view (Razor) | request-response | `DeckFlow.Web/Views/Deck/DeckHistory.cshtml` (page shell/hero/form) + `DeckFlow.Web/Views/Deck/Manabase.cshtml` (pills, `CommanderSelectionRequired` panel) | exact (composite of two analogs) |
| `DeckFlow.Web/wwwroot/ts/cut-lab.ts` | frontend module (lock/unlock UI) | event-driven (DOM) | `DeckFlow.Web/wwwroot/ts/deck-sync.ts` (form-state cache pattern) + `deck-input-store.ts` (auto-attach convention) | role-match |
| `DeckFlow.Web/Services/Tools/ToolRegistry.cs` (modified — add `cut-lab` entry) | config (registry) | — | existing `deck-history` entry, line 17 | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (modified) | config | — | `tool.deck-history.enabled` entry, line 43-44 | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (modified — both SQL blocks) | config | — | `tool.deck-history.enabled` rows, lines 244 & 294 | exact |
| `DeckFlow.Web.Tests/CutLabControllerTests.cs` | test | — | `DeckFlow.Web.Tests/DeckHistoryControllerTests.cs` | exact |
| `DeckFlow.Web.Tests/CutLabPageServiceTests.cs` | test | — | `DeckFlow.Web.Tests/DeckHistoryPageServiceTests.cs` | exact |
| `DeckFlow.Web.Tests/CutLabPoolValidatorTests.cs` | test | — | style of `DeckHistoryPageServiceTests.cs` unit tests, no direct analog file | role-match |
| `DeckFlow.Web.Tests/Tools/ToolRegistryTests.cs` (modified — new tool assertions) | test | — | itself, existing `AssertTool(...)` rows | exact |
| `DeckFlow.Web/e2e/cut-lab-smoke.spec.ts` | test (e2e) | event-driven (browser) | `DeckFlow.Web/e2e/deck-history-smoke.spec.ts` | exact |

## Pattern Assignments

### `DeckFlow.Web/Controllers/CutLabController.cs` (controller, request-response)

**Analog:** `DeckFlow.Web/Controllers/DeckHistoryController.cs`

**Imports pattern** (lines 1-9):
```csharp
using System.Text;
using DeckFlow.Core.Content;
using DeckFlow.Core.History;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;
```
For Cut Lab, swap `DeckFlow.Core.History` for `DeckFlow.Web.Services.CutLab` and keep `DeckFlow.Web.Infrastructure` for `FeatureFlagGateAttribute`.

**Feature-flag + GET/POST shape** (lines 12-72):
```csharp
public sealed class DeckHistoryController : Controller
{
    private readonly IDeckHistoryPageService _pageService;
    private readonly ILogger<DeckHistoryController> _logger;

    public DeckHistoryController(IDeckHistoryPageService pageService, ILogger<DeckHistoryController> logger)
    {
        ArgumentNullException.ThrowIfNull(pageService);
        _pageService = pageService;
        _logger = logger;
    }

    [HttpGet("/deck-history")]
    [FeatureFlagGate("tool.deck-history.enabled")]
    public IActionResult Index() => HistoryView(new DeckHistoryRequest(), null);

    [HttpPost("/deck-history")]
    [FeatureFlagGate("tool.deck-history.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Process(IFormFile? historyFile, DeckHistoryRequest request)
    {
        request ??= new DeckHistoryRequest();
        // ... validation, then:
        try
        {
            var result = await _pageService.ProcessAsync(request, uploadedJson, HttpContext.RequestAborted);
            return View("DeckHistory", DeckHistoryViewModel.From(request, result));
        }
        catch (OperationCanceledException)
        {
            return HistoryView(request, error: "The request timed out. Try again.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Deck history processing failed.");
            return HistoryView(request, error: "Something went wrong processing the deck history. Try again.");
        }
    }

    private ViewResult HistoryView(DeckHistoryRequest request, string? error) =>
        View("DeckHistory", new DeckHistoryViewModel
        {
            ActiveTab = DeckPageTab.DeckHistory,
            Request = request,
            ErrorMessage = error,
        });
}
```

**Cut Lab-specific adaptation:** No file upload; POST body is the deck source + intent fields + the `CutLabStateJson` hidden field. Follow the `Process` action's try/catch shape but catch `InvalidOperationException` (thrown by `CutLabPoolValidator` for the ≤100/>150 branches, per RESEARCH.md Pattern 2/3) as a distinct branch that surfaces `exception.Message` directly (mirrors the pattern shown in RESEARCH.md's condensed example, not the generic-`Exception` catch used by `DeckHistoryController`). Route: `[HttpGet("/cut-lab")]` / `[HttpPost("/cut-lab")]`, flag key `tool.cut-lab.enabled`.

---

### `DeckFlow.Web/Models/CutLabRequest.cs` (model, request-response)

**Analog:** `DeckFlow.Web/Models/DeckHistoryRequest.cs` (full file, 47 lines — reproduced pattern below)

```csharp
namespace DeckFlow.Web.Models;

public sealed class DeckHistoryRequest
{
    public DeckInputSource DeckInputSource { get; set; } = DeckInputSource.PublicUrl;
    public string DeckUrl { get; set; } = string.Empty;
    public string DeckText { get; set; } = string.Empty;
    // ... tool-specific fields ...
    public string HistoryJson { get; set; } = string.Empty;   // <-- hidden round-trip field pattern

    public string DeckSource =>
        DeckInputSource == DeckInputSource.PublicUrl ? DeckUrl : DeckText;
}
```

**Cut Lab adaptation:** `CutLabRequest` needs `DeckInputSource`, `DeckUrl`, `DeckText`, `DeckSource` (identical), plus INTAKE-02 intent fields (`PrimaryPlan`, `SecondaryPlan`, `Bracket` (int?), `PlayExperience`), plus `CutLabStateJson` (the hidden round-trip field, replacing `HistoryJson`) carrying `{pool, resolvedFacts, locks, declaredIntent}` per RESEARCH.md Pattern 5. Also needs `SelectedCommander` for the `CommanderSelectionRequired` fallback (see Manabase analog below).

---

### `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` (service, request-response orchestrator)

**Analog:** `DeckFlow.Web/Services/DeckHistoryPageService.cs`

**Interface + result record shape** (lines 20-65):
```csharp
public interface IDeckHistoryPageService
{
    Task<DeckHistoryProcessResult> ProcessAsync(
        DeckHistoryRequest request,
        string? uploadedHistoryJson,
        CancellationToken cancellationToken = default);
}

public sealed record DeckHistoryProcessResult
{
    public DeckHistoryFile? File { get; init; }
    public string? SerializedJson { get; init; }
    public bool Appended { get; init; }
    // ... result fields ...
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string? ErrorMessage { get; init; }
}
```

**DI ctor + test-seam ctor pattern** (lines 70-126):
```csharp
internal sealed class DeckHistoryPageService : IDeckHistoryPageService
{
    private readonly IDeckEntryLoader _deckEntryLoader;
    // ...
    private readonly ILogger<DeckHistoryPageService> _logger;
    private readonly Func<DateTimeOffset> _nowUtc;

    public DeckHistoryPageService(
        IDeckEntryLoader deckEntryLoader,
        /* ... */
        ILogger<DeckHistoryPageService> logger)
        : this(deckEntryLoader, /* ... */, logger, () => DateTimeOffset.UtcNow)
    {
    }

    internal DeckHistoryPageService(
        IDeckEntryLoader deckEntryLoader,
        /* ... */
        ILogger<DeckHistoryPageService>? logger,
        Func<DateTimeOffset> nowUtc)
    {
        ArgumentNullException.ThrowIfNull(deckEntryLoader);
        // ...
        _logger = logger ?? NullLogger<DeckHistoryPageService>.Instance;
        _nowUtc = nowUtc;
    }
}
```

**Load + error translation pattern** (lines 153-192, condensed):
```csharp
var (_, url, _, deckSource) = DeckInputReconciler.Reconcile(
    request.DeckInputSource, request.DeckUrl, request.DeckText, request.DeckSource);

DeckSourceLoadResult? load = null;
if (!string.IsNullOrWhiteSpace(deckSource))
{
    try
    {
        load = await _deckEntryLoader.LoadFromSourceAsync(deckSource, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is DeckParseException or InvalidOperationException)
    {
        return Error(exception.Message, warnings);
    }
    catch (HttpRequestException exception)
    {
        return Error(UpstreamErrorMessageBuilder.BuildScryfallMessage(exception), warnings);
    }
    // ... FallbackNotice -> warnings ...
}
```

**Error helper pattern** (line 349):
```csharp
private static DeckHistoryProcessResult Error(string message, IReadOnlyList<string>? warnings = null) =>
    new() { ErrorMessage = message, Warnings = warnings ?? [] };
```

**Cut Lab adaptation:** Replace `LoadFromSourceAsync` size-check with `CutLabPoolValidator` (see below, no `ValidateCommanderDeckSize`), then call the Scryfall batch-resolution pattern from `ManabaseAnalysisService` (see next), then run commander auto-lock + land-type detection (`CardTypeLine.FrontFace` check), then serialize state via `CutLabStateSerializer`. Server MUST re-apply "commander always locked" after deserializing any client-submitted `CutLabStateJson`, before rendering (Security Domain requirement in RESEARCH.md — tampered lock state must not be trusted).

---

### `DeckFlow.Web/Services/CutLab/CutLabPoolValidator.cs` (service, transform — NEW, ~10 lines)

**Analog:** `ManabaseAnalysisService.cs` ceiling-check pattern (lines 641-696, relevant excerpt)

```csharp
private const int MaxDeckSourceChars = 100_000;
private const int MaxDeckCards = 500;
// ...
if (deckSource.Length > MaxDeckSourceChars)
{
    throw new InvalidOperationException("That deck input is too large to analyze.");
}

DeckSourceLoadResult load;
try
{
    load = await _deckEntryLoader.LoadFromSourceAsync(deckSource, cancellationToken: cancellationToken)
        .ConfigureAwait(false);
}
catch (DeckParseException exception)
{
    throw new InvalidOperationException(exception.Message, exception);
}

var deckCards = entries.Where(e => AnalyzedBoards.Contains(e.Board)).ToList();

if (deckCards.Count > MaxDeckCards)
{
    throw new InvalidOperationException($"That deck has too many cards to analyze (limit {MaxDeckCards}).");
}
```

**Cut Lab adaptation (the one genuinely new piece of logic):** Replace the single ceiling with a range (101-150 inclusive), throwing `InvalidOperationException` with the two distinct copy strings from the UI-SPEC's Copywriting Contract:
- `count <= 100` → *"This pool already has 100 cards or fewer — Cut Lab is for trimming an oversized pool down to 100. Try Deck Sync or Deck Analysis instead."*
- `count > 150` → *"This pool has too many cards for Cut Lab (limit 150 plus commander). Trim it closer to 150 before importing."*

**CRITICAL — do not call `IDeckEntryLoader.ValidateCommanderDeckSize`** (`DeckFlow.Core/Loading/DeckEntryLoader.cs:159`) — it hard-rejects any non-exactly-100 count (RESEARCH.md's primary negative finding / Pitfall 2).

---

### `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` (service, JSON round-trip)

**Analog:** `DeckFlow.Core/History/DeckHistorySerializer.cs` (size-cap pattern; full serializer not reproduced here — read the file directly when implementing)

```csharp
public const int MaxUploadBytes = 1_048_576;  // DeckHistorySerializer's cap

if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxUploadBytes)
{
    // reject with a user-facing message
}
```

**Cut Lab adaptation:** Per RESEARCH.md Pitfall 5, size the Cut Lab state blob (150 cards × `CardFact`-shaped record) — expect ~150 KB if oracle text is stripped, comfortably under both `DeckHistorySerializer.MaxUploadBytes` (1 MB) and the controller's `RequestSizeLimit` (2 MB). Consider a smaller `MaxUploadBytes` constant specific to Cut Lab if oracle text is dropped from the round-trip payload (recommended in RESEARCH.md).

---

### Land/type detection helper (`CardTypeLine.FrontFace` + `Contains("Land")`)

**Analog:** `ManabaseClassifier.cs:1393-1397` (currently `private` — promote or duplicate per RESEARCH.md Assumption A3)

```csharp
private static bool IsLandType(string typeLine)
{
    // Use the front face only (before "//") so MDFC spell-fronts aren't treated as lands.
    return IsType(CardTypeLine.FrontFace(typeLine), "Land");
}
```

`CardTypeLine.FrontFace` itself (`DeckFlow.Core/Manabase/CardTypeLine.cs:14-15`):
```csharp
public static string FrontFace(string? typeLine)
    => (typeLine ?? string.Empty).Split("//")[0].Trim();
```

**Cut Lab adaptation:** For LOCK-03's "lock all lands" bulk action, use `CardTypeLine.FrontFace(typeLine).Contains("Land", StringComparison.OrdinalIgnoreCase)` directly — a 1-line inline check reusing the existing public `CardTypeLine.FrontFace` helper. Do NOT reach for `PlanRoleClassifier` (out of scope, Phase 102's SLOT-01).

---

### `DeckFlow.Web/Views/Deck/CutLab.cshtml` (view, request-response)

**Analog 1 (page shell, hero, split-input form):** `DeckFlow.Web/Views/Deck/DeckHistory.cshtml`

**Hero + tabs + error banner pattern** (lines 42-52):
```cshtml
<section class="hero">
    <h1>Deck History</h1>
    <p class="lede">Track your deck's evolution in a file you own. ...</p>
</section>

@await Html.PartialAsync("_BusyIndicator")
@await Html.PartialAsync("_DeckToolTabs", Model.ActiveTab)

<div class="error-banner @(string.IsNullOrWhiteSpace(Model.ErrorMessage) ? "hidden" : string.Empty)" role="alert">
    @Model.ErrorMessage
</div>
```

**Form shell with cache key + busy indicator + hidden round-trip field** (lines 54-60):
```cshtml
<form method="post" action="@Url.Content("~/deck-history")" enctype="multipart/form-data" class="result-panel deck-history-form"
      data-cache-key="deck-history"
      data-busy-title="Updating history"
      data-busy-message="Loading the deck, reconciling the history file, and rebuilding the comparison."
      data-busy-progress="Loading the deck|Reconciling history|Comparing versions|Building the prompt">
    @Html.AntiForgeryToken()
    <input type="hidden" name="HistoryJson" value="@Model.HistoryJson" />
```
For Cut Lab: `data-cache-key="cut-lab"`, hidden field `name="CutLabStateJson"`, action `~/cut-lab`.

**Split-input URL/paste toggle** (Manabase.cshtml:38-57, cited verbatim in RESEARCH.md Pattern 1):
```cshtml
<div class="field">
    <label for="manabase-input-source">Input method</label>
    <select id="manabase-input-source" name="DeckInputSource" data-df-select>
        <option value="@DeckFlow.Web.Models.DeckInputSource.PublicUrl" selected="@(isUrl ? "selected" : null)">Use public deck URL</option>
        <option value="@DeckFlow.Web.Models.DeckInputSource.PasteText" selected="@(!isUrl ? "selected" : null)">Paste text</option>
    </select>
</div>
<div class="field @(isUrl ? string.Empty : "hidden")" data-sync-panel="manabase-deck-url">
    <label for="manabase-deck-url">Archidekt or Moxfield deck URL</label>
    <input id="manabase-deck-url" type="url" name="DeckUrl" ... value="@Model.Request.DeckUrl" />
    @await Html.PartialAsync("_DeckFlowBridgeHint")
</div>
<div class="field @(isUrl ? "hidden" : string.Empty)" data-sync-panel="manabase-deck-text">
    <label for="manabase-deck-text">Paste a decklist...</label>
    <textarea id="manabase-deck-text" name="DeckText" rows="6" ...>@Model.Request.DeckText</textarea>
</div>
```
Use `cut-lab-input-source` / `cut-lab-deck-url` / `cut-lab-deck-text` ids (scripts query by `name`, not `id`, so this is safe).

**Results section pattern** (DeckHistory.cshtml:114-256): `@if (Model.HasResult) { <section data-scroll-on-load> ... }` wrapping success/warning banners then `<section class="result-panel">` blocks per logical group (Timeline / Save / Compare / Prompt in Deck History → Card count+legality / Intent form / Lock table / Packages in Cut Lab).

**Analog 2 (pills, commander-selection fallback):** `DeckFlow.Web/Views/Deck/Manabase.cshtml`

**Commander-selection fallback panel** (lines 87-122):
```cshtml
@if (Model.CommanderSelectionRequired)
{
    <div class="result-panel nested-panel" data-scroll-on-load>
        <div class="panel-heading">
            <div>
                <h2>Pick your commander</h2>
                <p>We couldn't identify your commander automatically. Choose it from the deck, or search to override with the exact name you want analyzed.</p>
            </div>
        </div>
        <div class="field">
            <label for="manabase-selected-commander">Commander from this deck</label>
            <select id="manabase-selected-commander" name="SelectedCommander" data-df-select autofocus>
                <option value="">Choose a commander</option>
                @foreach (var commanderChoice in Model.CommanderChoices)
                {
                    <option value="@commanderChoice" selected="@(string.Equals(Model.Request.SelectedCommander, commanderChoice, StringComparison.OrdinalIgnoreCase) ? "selected" : null)">
                        @commanderChoice
                    </option>
                }
            </select>
        </div>
        <!-- text-search backstop input, same pattern -->
    </div>
}
```
Cut Lab copy per UI-SPEC: *"We couldn't identify your commander automatically. Choose it below — it will be locked automatically once selected."*

**Segmented pill radiogroup (for Target Bracket + Play Experience, INTAKE-02)** (lines 160-181):
```cshtml
<fieldset class="manabase-segmented" role="radiogroup">
    <legend>Deck type</legend>
    <div class="manabase-pills">
        <label class="manabase-pill @(mode == ManabaseMode.Casual ? "is-selected" : null)">
            <input type="radio" name="Mode" value="Casual" checked="@(mode == ManabaseMode.Casual ? "checked" : null)" />
            <span>Casual</span>
        </label>
        <!-- ... -->
    </div>
    <p class="manabase-help">...</p>
</fieldset>
```
Reuse verbatim for `PlayExperience` (Casual/Focused/cEDH labels+copy from `Manabase.cshtml:160-179`) and `Bracket` (5-way pill 1-5, using `Bracket.cshtml:16-20` tier names Exhibition/Core/Upgraded/Optimized/cEDH per UI-SPEC).

**Commander badge (auto-lock, non-fading permanence signal):** `.manabase-cmd-glyph` (`site-common.css:2554-2559`) — Cut Lab's new `.cutlab-lock-badge--commander` reuses `.kb-chip` shape (`site-common.css:569-581`) plus `border-left: 3px solid var(--commander-gold, #d4af37)` per UI-SPEC Component Contract. No new CSS tokens — only new component classes in `site-common.css`.

**Lock-list table:** follow `table[data-prompt-cedh-reference-table]` responsive pattern (`site-common.css:1204-1230`) — columns: lock checkbox · card name · type/role · package assignment; mobile `data-label` stacked-row fallback.

**Scripts section** (DeckHistory.cshtml:259-264):
```cshtml
@section Scripts {
    <script src="~/js/deck-input-store.js" asp-append-version="true"></script>
    <script src="~/js/busy-indicator.js" asp-append-version="true"></script>
    <script src="~/js/moxfield-extension-bridge.js" asp-append-version="true"></script>
    <script src="~/js/deck-sync.js" asp-append-version="true"></script>
}
```
Cut Lab adds `<script src="~/js/cut-lab.js" asp-append-version="true"></script>` for lock/unlock + indeterminate-checkbox + package-select interactions.

---

### `DeckFlow.Web/wwwroot/ts/cut-lab.ts` (frontend module, event-driven)

**No direct analog controller-file** — this is Cut Lab's one genuinely new TS module. It composes existing infra rather than copying a single file:
- `deck-input-store.ts` auto-attaches to any form with `select[name="DeckInputSource"]` + `input[name="DeckUrl"]`/`textarea[name="DeckText"]` — no explicit registration needed, but inherits the KNOWN restore-desync bug (Pitfall 1) — do not attempt to fix it in this phase.
- `deck-sync.ts:505-563` generic form-state cache — add `data-cache-key="cut-lab"` to the form to get restore-on-reload "for free"; the mechanism itself needs no new code.
- `tsconfig.json` registration: **automatic** via glob `"include": ["wwwroot/ts/**/*.ts"]` (`DeckFlow.Web/tsconfig.json:14`) — no per-file registration step; any new `.ts` file dropped under `wwwroot/ts/` is picked up by the MSBuild `CompileTypeScriptAssets`/`tsc -p tsconfig.json` step automatically. Output `wwwroot/js/cut-lab.js` is gitignored (`wwwroot/js/*.js` glob) — never commit it.
- New logic needed in `cut-lab.ts`: per-row checkbox lock/unlock, package-level indeterminate-state toggling (`site-theme-overrides.css:100-118` native `:indeterminate` rule — set `element.indeterminate = true` in JS on partial-package-lock render), bulk role-group pill toggle, inline "+ New package…" reveal (mirrors `.card-picker__row` inline-add pattern, `site-common.css:812-862`), `window.confirm()` guard before package deletion (mirrors `moxfield-extension-bridge.ts:135`, the only existing public-tool confirm precedent).

---

### `DeckFlow.Web/Services/Tools/ToolRegistry.cs` (config, modified)

**Analog:** existing `deck-history` entry (`ToolRegistry.cs:17`)

```csharp
Create("deck-history", "Deck History", "/deck-history", ToolNavSection.Build,
    "tool.deck-history.enabled", false, "Deck History",
    "Track your deck's evolution in a file you own — snapshot each change with a note, diff any two versions, and generate an AI prompt about how the deck has grown.",
    "deck-history", DeckPageTab.DeckHistory, true, "/deck-history/download"),
```

**Cut Lab entry (add to `Definitions` array, position per `ToolNavSection.Build` grouping with deck-history/deck-primer/deck-sync/convert per RESEARCH.md A4):**
```csharp
Create("cut-lab", "Cut Lab", "/cut-lab", ToolNavSection.Build,
    "tool.cut-lab.enabled", false, "Cut Lab",
    "<tile description per UI-SPEC empty-state copy>",
    "cut-lab", DeckPageTab.CutLab, false),
```

**PITFALL (confirmed via grep):** `DeckFlow.Web.Tests/Tools/ToolRegistryTests.cs:21-35` (`All_ReturnsExpectedToolDefinitions`) and `:43-47` (`All_HasUniqueKeysRoutesAndTabs_AndExactlyThreeCoreTools`, asserting exact counts `15`/`15`/`15`/`21`) hand-enumerate every tool. Adding Cut Lab requires updating BOTH assertion blocks in the same commit — bump counts to `16` and add the new `AssertTool(...)` row in registration order. Full test excerpt read and confirmed at `ToolRegistryTests.cs:1-47`.

---

### `DeckFlow.Web/Models/DeckPageTab.cs` (modified)

**Analog:** existing enum, add new member after `DeckHistory = 16`:
```csharp
/// <summary>Deck version-history tracking page.</summary>
DeckHistory = 16,
```
Add:
```csharp
/// <summary>Cut Lab intake, protection, and cut-recommendation page.</summary>
CutLab = 17,
```

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` + `FeatureFlagStore.cs` (config, modified — BOTH files, BOTH SQL blocks)

**Analog:** `tool.deck-history.enabled` triplet

`FeatureFlagCatalog.cs:43-44`:
```csharp
["tool.deck-history.enabled"] =
    "Deck History tool: version a deck into a downloadable snapshot-history JSON file with notes, pair diffs, and an evolution prompt.",
```
Add: `["tool.cut-lab.enabled"] = "Cut Lab tool: intake an oversized (101-150 card) Commander pool, declare deck intent, and protect cards/packages from later cuts."`

`FeatureFlagStore.cs` Postgres block (line 244, inside `PostgresSeedSql`, before the closing `ON CONFLICT`):
```csharp
('tool.deck-history.enabled', FALSE)
```
Add: `('tool.cut-lab.enabled', FALSE),` — remember to fix the now-not-last-row comma.

`FeatureFlagStore.cs` SQLite block (line 294, inside `SqliteSeedSql`):
```csharp
('tool.deck-history.enabled', 0)
```
Add: `('tool.cut-lab.enabled', 0),`

**PITFALL (confirmed, RESEARCH.md Pitfall 4):** Both blocks (`PostgresSeedSql` ~196-246, `SqliteSeedSql` ~248-296) must get the new row in the SAME commit as the `Descriptions` entry, or `FeatureFlagCatalogTests`/`FeatureFlagStoreSeedTests`/`FeatureFlagStoreMigrationTests` may pass on one backend and silently miss the other depending on which DB the test run targets.

---

### Test files

**`DeckFlow.Web.Tests/CutLabControllerTests.cs`** — analog `DeckFlow.Web.Tests/DeckHistoryControllerTests.cs` (full class read, 90+ lines shown above): xUnit `[Fact]`, `sealed class`, one test per branch (`Index_ReturnsViewWithDeckHistoryTabActive`, `Process_*_ReturnsError...`, `Process_HappyPath_...`), uses a `Fake*PageService` test double with `CallCount`/`Result` fields (matches project convention: `Fake*` = stateful behavior fake). Example asserted shape:
```csharp
[Fact]
public void Index_ReturnsViewWithDeckHistoryTabActive()
{
    var controller = CreateController(new FakeDeckHistoryPageService());
    var result = controller.Index();
    var view = Assert.IsType<ViewResult>(result);
    Assert.Equal("DeckHistory", view.ViewName);
    var model = Assert.IsType<DeckHistoryViewModel>(view.Model);
    Assert.Equal(DeckPageTab.DeckHistory, model.ActiveTab);
    Assert.NotNull(model.Request);
}
```

**`DeckFlow.Web.Tests/CutLabPageServiceTests.cs`** — analog `DeckFlow.Web.Tests/DeckHistoryPageServiceTests.cs` (not fully read here — same conventions as the controller test file: xUnit, `sealed class`, internal test-seam constructor with injected `Func<DateTimeOffset>`/delegate per project's HTTP test-seam convention).

**`DeckFlow.Web.Tests/CutLabPoolValidatorTests.cs`** — no direct file analog; follow xUnit `[Theory]`/`[InlineData]` for the two boundary branches (100 → error, 101 → pass, 150 → pass, 151 → error) per project's `[Theory]` convention (root `CLAUDE.md` Testing Standards).

**`DeckFlow.Web/e2e/cut-lab-smoke.spec.ts`** — analog `DeckFlow.Web/e2e/deck-history-smoke.spec.ts` (full file read, reproduced structure below):
```typescript
import { expect, test } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';

const baseUrl = 'http://localhost:5173';
const screenshotDir = resolve(__dirname, '../../.planning/ui-design/cut-lab/screenshots');

const themes = [
  { name: 'classic', cookie: 'site.css' },
  { name: 'azorius', cookie: 'site-azorius.css' },
  { name: 'nyx', cookie: 'site-nyx.css' },
] as const;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Cut Lab', true);
});

test.afterEach(async ({ page }) => {
  try {
    await setToolEnabled(page, 'Cut Lab', false);
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

test('/cut-lab renders the form when the flag is ON', async ({ page }) => { /* ... */ });

test('imports a 101-150 card pool, shows card count + legality, locks commander, screenshots per theme', async ({ page }) => {
  // fill split-input, submit, assert result panels, assert commander checkbox disabled+checked,
  // screenshot per theme like deck-history-smoke.spec.ts lines 212-240
});

test('with tool.cut-lab.enabled OFF, /cut-lab returns 404 and the Home tile is absent', async ({ page }) => {
  await setToolEnabled(page, 'Cut Lab', false);
  const response = await page.goto('/cut-lab');
  expect(response?.status(), '/cut-lab should be 404 with flag OFF').toBe(404);
  await page.goto('/');
  await expect(page.locator('.hub-card[href$="/cut-lab"]')).toHaveCount(0);
});
```
Key assertions to port: `data-cache-key="cut-lab"` on the form (mirrors line 73 `toHaveAttribute('data-cache-key', 'deck-history')`), ≤100/>150 error-message assertions (mirrors the `.warning-banner`/error-banner text assertions), commander auto-lock `disabled`+`checked` state (new — no direct precedent, but follows the same Playwright locator style as the rest of the file).

---

## Shared Patterns

### Feature-flag gating
**Source:** `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs`, applied as `[FeatureFlagGate("tool.deck-history.enabled")]` on every action (`DeckHistoryController.cs:27,34,77`)
**Apply to:** Every Cut Lab controller action — `[FeatureFlagGate("tool.cut-lab.enabled")]`.

### CSRF protection on mutating POSTs
**Source:** `[ValidateAntiForgeryToken]` + `@Html.AntiForgeryToken()` (`DeckHistoryController.cs:35`, `DeckHistory.cshtml:59`)
**Apply to:** Every Cut Lab POST action and form.

### Request size cap
**Source:** `[RequestSizeLimit(2 * 1024 * 1024)]` (`DeckHistoryController.cs:36`)
**Apply to:** Cut Lab POST actions — 2 MB matches Deck History's data shape (card list + locks + intent, no file upload); revisit only if oracle text bloats the round-trip blob (RESEARCH.md Pitfall 5).

### Deck loading without exact-100 gate
**Source:** `IDeckEntryLoader.LoadFromSourceAsync` (`DeckFlow.Core/Loading/DeckEntryLoader.cs:113`), used by `ManabaseAnalysisService.cs:668`
**Apply to:** `CutLabPageService` — never `ValidateCommanderDeckSize` (`DeckEntryLoader.cs:159`), which is the exact-100 trap documented as Pitfall 2.

### Error translation for controller boundaries
**Source:** `catch (OperationCanceledException)` → timeout copy; `catch (Exception)` → generic copy + `_logger.LogError` (`DeckHistoryController.cs:63-71`); Core-layer `DeckParseException`/`InvalidOperationException` → `Error(exception.Message, warnings)` inside the page service (`DeckHistoryPageService.cs:166-168`)
**Apply to:** `CutLabController.Process` and `CutLabPageService.ProcessAsync`.

### Hidden-field JSON round-trip ("session" without ASP.NET Session)
**Source:** `DeckHistoryRequest.HistoryJson` (`DeckHistoryRequest.cs:32`) + `<input type="hidden" name="HistoryJson" value="@Model.HistoryJson" />` (`DeckHistory.cshtml:60,196`)
**Apply to:** `CutLabRequest.CutLabStateJson` — the working-session mechanism for pool + locks + intent (RESEARCH.md Pattern 5, Open Question 1 flags this as a planning decision the plan must make explicit for how Phases 102-105 consume it).

### Client-side persistence (URL/paste restore + generic form cache)
**Source:** `deck-input-store.ts` (auto-attach) + `deck-sync.ts:505-563` (`data-cache-key`)
**Apply to:** Cut Lab form — add `data-cache-key="cut-lab"`; no new client persistence code needed. NOTE the known restore-desync bug (Pitfall 1) is pre-existing and explicitly out of scope to fix here.

### Themed checkbox/radio rendering
**Source:** `site-theme-overrides.css:53-142` (unchecked/checked/indeterminate/disabled states, all pre-styled)
**Apply to:** Every lock checkbox, package "select-all" checkbox, and pill radio in Cut Lab — zero new checkbox CSS required per UI-SPEC Component Contract.

### `--panel` not `--theme-surface`
**Source:** project memory `reference_theme_surface_light_in_dark.md`, reiterated in UI-SPEC Color section
**Apply to:** Every new Cut Lab CSS rule needing a card/panel background — use `var(--panel)`.

## No Analog Found

None — every file in the phase's inventory has at least a role-match analog; the two genuinely novel pieces of logic (`CutLabPoolValidator`'s 101-150 range, `cut-lab.ts`'s lock/package interaction script) still compose entirely from existing, cited precedents (Manabase's ceiling-check shape; deck-sync.ts's cache mechanism + moxfield-extension-bridge.ts's confirm() precedent) rather than requiring net-new patterns.

## Metadata

**Analog search scope:** `DeckFlow.Web/Controllers/`, `DeckFlow.Web/Models/`, `DeckFlow.Web/Services/`, `DeckFlow.Web/Services/Manabase/`, `DeckFlow.Web/Services/Tools/`, `DeckFlow.Web/Services/FeatureFlags/`, `DeckFlow.Web/Views/Deck/`, `DeckFlow.Web/wwwroot/ts/`, `DeckFlow.Web/e2e/`, `DeckFlow.Web.Tests/`, `DeckFlow.Core/Manabase/`, `DeckFlow.Core/History/`, `DeckFlow.Core/Loading/`
**Files scanned:** 16 read directly (full or targeted sections) — `DeckHistoryController.cs`, `DeckHistoryRequest.cs`, `DeckHistoryPageService.cs`, `DeckHistory.cshtml`, `ToolRegistry.cs`, `DeckPageTab.cs`, `FeatureFlagCatalog.cs`, `FeatureFlagStore.cs` (seed blocks), `Manabase.cshtml` (pills + commander-selection sections), `ManabaseAnalysisService.cs` (ceiling-check section), `ManabaseClassifier.cs` (`IsLandType`), `CardTypeLine.cs`, `deck-history-smoke.spec.ts`, `DeckHistoryControllerTests.cs`, `ToolRegistryTests.cs`, `DeckHistoryViewModel.cs`, `tsconfig.json`
**Pattern extraction date:** 2026-07-18
