# Phase 32: Expert Context Selection — Pattern Map

**Mapped:** 2026-06-07
**Files analyzed:** 16 new/modified files
**Analogs found:** 16 / 16

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` | service/store | CRUD | self (existing — additive column + method) | exact |
| `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` | model | transform | self (existing `ContentSiteIndexRow` record) | exact |
| `DeckFlow.Web/Services/ContentKbRelevanceService.cs` | service | request-response | self (existing — new interface method) | exact |
| `DeckFlow.Web/Models/DeckAnalysisRequest.cs` | model | request-response | self (existing — additive fields) | exact |
| `DeckFlow.Web/Models/ContentKbExcerpt.cs` | model | transform | self (existing sealed record) | exact |
| `DeckFlow.Web/Services/PacketArtifactStore.cs` | service | file-I/O | self (existing — allowlist + BuildZip + LoadFromZip) | exact |
| `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` | controller | request-response | self (`SetVisibility` action pattern) | exact |
| `DeckFlow.Web/Views/ContentKb/Index.cshtml` | view | request-response | `DeckFlow.Web/Views/ContentKb/Index.cshtml` (existing hub-grid) | exact |
| `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` | view | request-response | self (existing form with hidden fields) | exact |
| `DeckFlow.Web/Views/Deck/_ContentKbPanel.cshtml` | view/partial | request-response | self (existing clip loop) | exact |
| `DeckFlow.Web/Views/Admin/ContentKb/Index.cshtml` | view | request-response | admin action cell with SetVisibility form | role-match |
| `DeckFlow.Web/wwwroot/ts/kb-selection.ts` | utility | event-driven | `content-kb.ts` + `site.ts` (localStorage pattern) | role-match |
| `DeckFlow.Web/wwwroot/css/site-common.css` | config | — | self (existing site-common layout rules) | exact |
| `DeckFlow.Web.Tests/ContentKbMergedClipsTests.cs` | test | — | `ContentKbRelevanceServiceTests.cs` | exact |
| `DeckFlow.Web.Tests/PacketArtifactStoreTests.cs` | test | — | self (extend existing round-trip tests) | exact |
| `DeckFlow.Web.Tests/DeckAnalysisRequestTests.cs` | test | — | `PacketArtifactStoreTests.cs` binding patterns | role-match |

---

## Pattern Assignments

### `DeckFlow.Core/Content/ContentSiteIndexStore.cs` (store, CRUD — additive)

**Analog:** self (lines 44–72, 277–292, 432–474, 583–600)

**IsEvergreen column migration pattern** (lines 56–64 — exact copy for `is_evergreen`):
```csharp
var columns = await GetTableColumnsAsync(connection, "content_site_index", cancellationToken).ConfigureAwait(false);
if (!columns.Contains("is_visible"))
{
    await using var addVisible = connection.CreateCommand();
    addVisible.CommandText = _connectionInfo.IsPostgres
        ? "ALTER TABLE content_site_index ADD COLUMN is_visible BOOLEAN NOT NULL DEFAULT FALSE;"
        : "ALTER TABLE content_site_index ADD COLUMN is_visible INTEGER NOT NULL DEFAULT 0;";
    await addVisible.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
// REPLICATE immediately after, substituting is_evergreen:
```

**SetVisibilityAsync pattern** (lines 277–292 — exact copy for `SetEvergreenAsync`):
```csharp
public async Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
{
    await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

    await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    await using var command = connection.CreateCommand();
    command.CommandText = """
        UPDATE content_site_index
           SET is_visible = @visible
         WHERE id = @id;
        """;
    RelationalDatabaseConnection.AddParameter(command, "@visible", FormatVisibility(visible));
    RelationalDatabaseConnection.AddParameter(command, "@id", id);

    return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```

**ReadVisibility helper** (lines 462–474 — reuse at ordinal 13 for IsEvergreen):
```csharp
private static bool ReadVisibility(DbDataReader reader, int ordinal)
{
    var raw = reader.GetValue(ordinal);
    return raw switch
    {
        bool b => b,
        long l => l != 0,
        int i => i != 0,
        short s => s != 0,
        string text => text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase),
        _ => Convert.ToInt64(raw, CultureInfo.InvariantCulture) != 0
    };
}
```

**ReadRow addition** (line 458 — add after `IsVisible`):
```csharp
IsVisible = ReadVisibility(reader, 12),
IsEvergreen = ReadVisibility(reader, 13)   // ADD — ordinal 13 after is_visible
```

**CREATE TABLE SQL constants** (lines 564–600): Both `PostgresCreateTableSql` and `SqliteCreateTableSql` must gain `is_evergreen` column at the same position as ordinal 13. Add after `is_visible`:
```sql
-- Postgres:
is_evergreen       BOOLEAN NOT NULL DEFAULT FALSE,
-- SQLite:
is_evergreen       INTEGER NOT NULL DEFAULT 0,
```

**SELECT queries** (lines 153–170, 190–211, 219–237, 249–264): All four SELECT statements that currently list 13 columns ending with `is_visible` must add `is_evergreen` to the column list and the `FormatVisibility` call in `GetPublishedRowsAsync` remains unchanged (still queries `is_visible = @visible`).

**IContentSiteIndexStore interface**: Add `Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken ct = default)` mirroring `SetVisibilityAsync` signature exactly.

---

### `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` (model, transform — additive)

**Analog:** `DeckFlow.Web/Models/ContentKbExcerpt.cs` (lines 1–30 — `{ get; init; }` pattern)

**ContentSiteIndexRow addition pattern** (from `ContentKbExcerpt.cs` lines 6–29):
```csharp
// The { get; init; } constraint is mandatory on ALL properties of ContentSiteIndexRow.
// Why: System.Text.Json skips get-only properties for this round-tripped DTO; every member must stay { get; init; }.

/// <summary>Whether this artifact fills evergreen advice slots in any deck's analysis prompt.</summary>
public bool IsEvergreen { get; init; }
```

The `sealed record ContentSiteIndexRow` already uses `required ... { get; init; }` on all properties. `IsEvergreen` is not required (it defaults to `false`). Do NOT use `required` on it.

---

### `DeckFlow.Web/Services/ContentKbRelevanceService.cs` (service, request-response — new method)

**Analog:** self — `GetRelevantClipsAsync` (lines 149–179), `SelectTopClips` (lines 317–347), `EstimateRenderedChars` (lines 349–363), `ScoreArtifact` (lines 200–238)

**Interface addition** (after line 44 — mirrors `GetRelevantClipsAsync` signature exactly):
```csharp
/// <summary>
/// Returns a budget-trimmed list of clips merged across tier 1-4 selection,
/// or <see langword="null"/> when the feature is disabled or no clips qualify.
/// </summary>
Task<IReadOnlyList<ContentKbExcerpt>?> GetMergedClipsAsync(
    ExpertSelection selection,
    string? commanderName,
    string? bracket,
    IReadOnlySet<string>? deckArchetypes = null,
    int maxRenderedChars = 4500,
    CancellationToken ct = default);
```

**ExpertSelection parameter type** (new internal record — Claude's discretion, place at bottom of file with other internal records):
```csharp
internal sealed record ExpertSelection(
    IReadOnlyList<string> PinnedVideoIds,    // max 3 enforced at call site
    IReadOnlySet<string> FollowedCreators);  // StringComparer.OrdinalIgnoreCase set
```

**ScoreArtifact gate for tier 2** (line 237 — critical: do NOT modify this line):
```csharp
return dimensionsHit >= 2 ? score : 0d;
```
For tier 2 (followed creators), call `ScoreArtifact` then separately check whether `dimensionsHit >= 1` by computing dimension hits independently — or add a private helper `CountDimensionsHit(ScoreInput, NormalizedCommander?, string?, IReadOnlySet<string>)` that returns the raw count. Do not change the existing `ScoreArtifact` signature or gate logic.

**GetRelevantClipsAsync structure** (lines 149–179 — template for GetMergedClipsAsync):
```csharp
public async Task<IReadOnlyList<ContentKbExcerpt>?> GetRelevantClipsAsync(
    string? commanderName,
    string? bracket,
    IReadOnlySet<string>? deckArchetypes = null,
    int maxRenderedChars = DefaultMaxRenderedChars,
    CancellationToken ct = default)
{
    if (!_flagCache.IsEnabled("content.kb.enabled")) return null;

    var normalizedCommander = NormalizeCommander(commanderName);
    var normalizedBracket = NormalizeBracket(bracket);
    var effectiveArchetypes = await ResolveDeckArchetypesAsync(deckArchetypes, commanderName, ct).ConfigureAwait(false);
    var rows = await _store.GetPublishedRowsAsync(ct).ConfigureAwait(false);

    var parsedRows = await ParseRowsAsync(rows, normalizedCommander, normalizedBracket, effectiveArchetypes, includeFailedRowsAsZeroScore: false, ct).ConfigureAwait(false);
    var selectedClips = SelectTopClips(parsedRows);
    if (selectedClips.Count == 0) return null;

    while (selectedClips.Count > 0 && EstimateRenderedChars(selectedClips) > maxRenderedChars)
    {
        selectedClips.RemoveAt(selectedClips.Count - 1);
    }

    return selectedClips.Count == 0 ? null : selectedClips;
}
```

**SelectTopClips clip construction** (lines 332–344 — add `ClipOrigin` to each `new ContentKbExcerpt`):
```csharp
selected.Add(new ContentKbExcerpt
{
    Source = artifact.Row.Source,
    Title = artifact.Row.Title,
    VideoUrl = ContentKbClipParser.BuildDeepLink(artifact.ScoreInput.SourceUrl, clip.TimestampLabel),
    TimestampLabel = clip.TimestampLabel,
    Excerpt = clip.Excerpt,
    HarvestDate = artifact.ScoreInput.HarvestDate,
    Score = artifact.Score,
    ClipOrigin = "auto"    // ADD — set per tier in GetMergedClipsAsync
});
```

**EstimateRenderedChars** (lines 349–363 — call unchanged; budget trim loop identical):
```csharp
while (selectedClips.Count > 0 && EstimateRenderedChars(selectedClips) > maxRenderedChars)
{
    selectedClips.RemoveAt(selectedClips.Count - 1);
}
```
For tier-aware trim: track tier boundary indices before building the flat list; trim from tier 4 back toward tier 1, never removing the last tier-1 clip.

---

### `DeckFlow.Web/Models/DeckAnalysisRequest.cs` (model — additive fields)

**Analog:** self (lines 1–276 — existing backing-field + property setter pattern)

**Existing List field pattern** (lines 156–161 — copy exactly for `PinnedVideoIds` and `FollowedCreators`):
```csharp
private List<string> _selectedAnalysisQuestions = [];
// ...
public List<string> SelectedAnalysisQuestions
{
    get => _selectedAnalysisQuestions;
    set => _selectedAnalysisQuestions = value ?? [];
}
```

**New fields to add** (after `ExpertContextJson` property, ~line 131):
```csharp
private List<string> _pinnedVideoIds = [];
private List<string> _followedCreators = [];
private string _expertSelectionJson = string.Empty;

/// <summary>Video IDs pinned for the next analysis run. One-shot: cleared after use.</summary>
public List<string> PinnedVideoIds
{
    get => _pinnedVideoIds;
    set => _pinnedVideoIds = value ?? [];
}

/// <summary>Creator names the user follows; sticky across runs.</summary>
public List<string> FollowedCreators
{
    get => _followedCreators;
    set => _followedCreators = value ?? [];
}

/// <summary>Serialized expert-selection state (33-expert-selection.json) round-tripped through the zip.</summary>
public string ExpertSelectionJson
{
    get => _expertSelectionJson;
    set => _expertSelectionJson = value ?? string.Empty;
}
```

---

### `DeckFlow.Web/Models/ContentKbExcerpt.cs` (model — additive property)

**Analog:** self (lines 1–30)

**Critical constraint** (line 8 comment — never violate):
```csharp
// Why: System.Text.Json skips get-only properties for this round-tripped DTO; every member must stay { get; init; }.
```

**New property to add** (after `Score` at line 29):
```csharp
/// <summary>How this clip entered the selection (pinned / followed / auto / evergreen).</summary>
public string ClipOrigin { get; init; } = "auto";
```

Note: NOT `required` — it has a default value of `"auto"` so existing serialized `32-expert-context.json` entries without the property still deserialize correctly.

---

### `DeckFlow.Web/Services/PacketArtifactStore.cs` (service, file-I/O — allowlist + BuildZip + LoadFromZip)

**Analog:** self (lines 27–42, 94–129, 205–314)

**PacketAllowedNames addition** (line 36 — same-commit rule: must be in same commit as BuildZip/LoadFromZip changes):
```csharp
private static readonly HashSet<string> PacketAllowedNames = new(StringComparer.OrdinalIgnoreCase)
{
    // ...existing entries...
    "32-expert-context.json",
    "33-expert-selection.json",   // ADD HERE
    // ...rest...
};
```

**BuildZip signature extension** (line 94 — add optional parameter last):
```csharp
public static byte[] BuildZip(
    DeckAnalysisRequest request,
    string? commanderName,
    string inputSummary,
    string? requestContextText,
    string? referenceText,
    string? analysisPromptText,
    string deckProfileSchemaJson,
    string? setUpgradePromptText,
    string? canonicalDeckListText = null,
    string? originalDeckText = null,
    string? expertContextJson = null,
    string? selectionJson = null)    // ADD — must be last optional
```

Add to the `promptSections` NormalizeSections call (after the `"32-expert-context.json"` entry):
```csharp
("33-expert-selection.json", "EXPERT SELECTION JSON", selectionJson),
```

**LoadFromZip addition** (after line 216 `entries.TryGetValue("32-expert-context.json"...)`):
```csharp
entries.TryGetValue("33-expert-selection.json", out var selectionJson);
```

Then after `request.ExpertContextJson = expertContextJson ?? string.Empty;` (line 226):
```csharp
request.ExpertSelectionJson = selectionJson ?? string.Empty;
// Deserialize PinnedVideoIds + FollowedCreators with graceful degradation:
if (!string.IsNullOrWhiteSpace(selectionJson))
{
    try
    {
        var sel = JsonSerializer.Deserialize<ExpertSelectionState>(selectionJson);
        if (sel?.PinnedVideoIds?.Count > 0) request.PinnedVideoIds = [..sel.PinnedVideoIds];
        if (sel?.FollowedCreators?.Count > 0) request.FollowedCreators = [..sel.FollowedCreators];
    }
    catch (JsonException)
    {
        // Degrade gracefully — empty selection, no throw
    }
}
```

**Corrupt-JSON fallback pattern** (lines 518–533 — `TryDeserializeFetchedEntries` is the canonical model):
```csharp
private static IReadOnlyList<EdhTop16Entry> TryDeserializeFetchedEntries(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return Array.Empty<EdhTop16Entry>();
    try
    {
        var deserialized = JsonSerializer.Deserialize<List<EdhTop16Entry>>(json, FetchedEntriesJsonOptions);
        return deserialized ?? (IReadOnlyList<EdhTop16Entry>)Array.Empty<EdhTop16Entry>();
    }
    catch (JsonException)
    {
        return Array.Empty<EdhTop16Entry>();
    }
}
```

**ExpertSelectionState record** (new internal sealed record — add at bottom of PacketArtifactStore.cs or in a companion file):
```csharp
internal sealed record ExpertSelectionState
{
    // Why: System.Text.Json skips get-only properties; { get; init; } required for zip round-trip.
    public IReadOnlyList<string> PinnedVideoIds { get; init; } = [];
    public IReadOnlyList<string> FollowedCreators { get; init; } = [];
}
```

---

### `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` (controller — additive action)

**Analog:** `SetVisibility` action (lines 201–213 — copy verbatim, substitute Evergreen)

**SetVisibility source** (lines 201–213):
```csharp
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
```

**SetEvergreen to add** (immediately after SetVisibility):
```csharp
/// <summary>
/// Marks or unmarks an artifact as evergreen. Double-CSRF-guarded.
/// </summary>
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
```

**KbEntryRow view model addition** (in `AdminContentKbViewModel` or co-located record): add `bool IsEvergreen { get; init; }` sourced from `r.IsEvergreen` in the `entries` LINQ projection (lines 91–100):
```csharp
IEnumerable<KbEntryRow> entries = rows
    .Select(r => new KbEntryRow
    {
        Id = r.Id,
        Title = r.Title,
        Source = r.Source,
        Tags = r.ArchetypeTags.Concat(r.BracketTags).ToArray(),
        IsVisible = r.IsVisible,
        IsEvergreen = r.IsEvergreen,    // ADD
        RelevanceScore = ...
    });
```

---

### `DeckFlow.Web/Views/ContentKb/Index.cshtml` (view — additive buttons + tray)

**Analog:** self (lines 60–91 — existing hub-card article structure)

**Current card article structure** (lines 63–89 — insertion point for actions row):
```html
<article class="hub-card"
         data-kb-entry
         data-search="@(...)"
         data-source="@entry.Source"
         ...>
    <h2 class="hub-card__title">...</h2>
    <p class="hub-card__description">...</p>
    <div aria-hidden="true">
        <span class="kb-tag" data-source="@entry.Source">@entry.Source</span>
        @* bracket/archetype/category tags *@
    </div>
    @* ADD kb-card-actions row here, after the tag div *@
</article>
```

**kb-card-actions row to add** (after the tag `<div aria-hidden="true">` closing tag):
```html
<div class="kb-card-actions">
    <button type="button"
            class="kb-pin-btn"
            aria-label="Pin '@entry.Title' for next analysis"
            aria-pressed="false"
            data-kb-pin
            data-video-id="@entry.VideoId"
            data-video-title="@entry.Title">
        📌 Pin
    </button>
    <button type="button"
            class="kb-follow-btn"
            aria-label="Follow @entry.Source"
            aria-pressed="false"
            data-kb-follow
            data-creator="@entry.Source">
        ★ Follow
    </button>
</div>
```

**Selection tray** (insert between `.kb-filter-bar` fieldset and `.hub-grid` div, i.e. after line 58):
```html
<div class="kb-selection-tray" aria-live="polite" aria-label="Current expert context selection" hidden>
    <div class="kb-selection-tray__section">
        <span class="kb-selection-tray__label">Pinned (<span data-tray-pin-count>0</span>/3)</span>
        <ul class="kb-selection-tray__list" data-tray-pins aria-label="Pinned videos"></ul>
    </div>
    <div class="kb-selection-tray__section">
        <span class="kb-selection-tray__label">Following</span>
        <ul class="kb-selection-tray__list" data-tray-follows aria-label="Followed creators"></ul>
    </div>
    <a href="/deck-analysis" class="kb-selection-tray__cta">Run analysis with this selection →</a>
</div>
```

**Script block addition** (line 111 — add kb-selection.js after content-kb.js):
```html
@section Scripts {
    <script src="~/js/content-kb.js" asp-append-version="true"></script>
    <script src="~/js/kb-selection.js" asp-append-version="true"></script>
}
```

**`entry.VideoId` property**: `ContentKbBrowseViewModel.Entry` needs a `VideoId` property sourced from `ContentSiteIndexRow.YoutubeVideoId ?? RssGuid`. Add to the view model projection in `ContentKbController`.

---

### `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` (view — chip area insertion)

**Analog:** self (lines 83–100 — existing hidden fields pattern; `SelectedAnalysisQuestions` checkbox approach)

**Hidden field pattern for list binding** (existing `SelectedAnalysisQuestions` pattern — copy for PinnedVideoIds/FollowedCreators):
```html
@* Server-render one hidden input per replayed pin/follow *@
@foreach (var id in Model.Request.PinnedVideoIds)
{
    <input type="hidden" name="PinnedVideoIds" value="@id" />
}
@foreach (var creator in Model.Request.FollowedCreators)
{
    <input type="hidden" name="FollowedCreators" value="@creator" />
}
```

**Chip area insertion point**: After the AI selector section and before the Step 1 decklist fields. The form tag is at line 73; the `@Html.AntiForgeryToken()` is at line 83. Insert the chip area section as a new `<section class="kb-chip-area">` block in Step 1.

**DeckAnalysisViewModel extension needed**: Add `IReadOnlyDictionary<string, string> ResolvedPinTitles` to carry title lookups for server-rendered chips (populated from `IContentSiteIndexStore.GetByIdAsync` or equivalent in the controller replay path).

**Script block**: Add `<script src="~/js/kb-selection.js" asp-append-version="true"></script>` to the existing `@section Scripts` block.

---

### `DeckFlow.Web/Views/Deck/_ContentKbPanel.cshtml` (partial view — origin markers)

**Analog:** self (lines 1–28 — entire file; clip loop structure)

**Current clip article** (lines 14–22):
```html
<article class="kb-expert-clip">
    <blockquote>@clip.Excerpt</blockquote>
    <p>
        &mdash; @clip.Source,
        <a href="@clip.VideoUrl" target="_blank" rel="noopener noreferrer">@clip.Title</a>
        [@clip.TimestampLabel],
        Harvested @clip.HarvestDate.ToString("yyyy-MM-dd")
    </p>
</article>
```

**Origin marker badge to add** (inside each `<article>`, after the `<p>` attribution):
```html
<span class="kb-clip-origin kb-clip-origin--@clip.ClipOrigin"
      aria-label="@ClipOriginLabel(clip.ClipOrigin)"
      title="@ClipOriginLabel(clip.ClipOrigin)">@ClipOriginGlyph(clip.ClipOrigin)</span>
```

**@functions block to add** (after the closing `}` at line 28):
```csharp
@functions {
    private static string ClipOriginGlyph(string origin) => origin switch
    {
        "pinned"    => "📌",
        "followed"  => "★",
        "evergreen" => "☘",
        _           => "auto"
    };
    private static string ClipOriginLabel(string origin) => origin switch
    {
        "pinned"    => "Pinned by you",
        "followed"  => "From followed creator",
        "evergreen" => "Evergreen advice",
        _           => "Auto-selected"
    };
}
```

The model type `@model IReadOnlyList<DeckFlow.Web.Models.ContentKbExcerpt>?` does NOT change — `ClipOrigin` is now a property on the excerpt itself.

---

### `DeckFlow.Web/Views/Admin/ContentKb/Index.cshtml` (view — Evergreen toggle)

**Analog:** Existing SetVisibility form in the admin grid's action `<td>` — exact mirror.

**SetVisibility form structure** (existing pattern to copy):
```html
<form method="post" asp-action="SetVisibility" class="admin-action-form">
    @Html.AntiForgeryToken()
    <input type="hidden" name="entryId" value="@entry.Id" />
    <input type="hidden" name="visible" value="@((!entry.IsVisible).ToString().ToLowerInvariant())" />
    <button type="submit">@(entry.IsVisible ? "Unpublish" : "Publish")</button>
</form>
```

**SetEvergreen form to add** (in same action `<td>`, stacked after SetVisibility form):
```html
<form method="post" asp-action="SetEvergreen" class="admin-action-form">
    @Html.AntiForgeryToken()
    <input type="hidden" name="entryId" value="@entry.Id" />
    <input type="hidden" name="evergreen" value="@((!entry.IsEvergreen).ToString().ToLowerInvariant())" />
    <button type="submit"
            aria-label="@(entry.IsEvergreen ? $"Remove evergreen flag from '{entry.Title}'" : $"Mark '{entry.Title}' as evergreen")">
        @(entry.IsEvergreen ? "Evergreen: On" : "Evergreen: Off")
    </button>
</form>
```

---

### `DeckFlow.Web/wwwroot/ts/kb-selection.ts` (new TypeScript module — localStorage + chips + tray)

**Analog:** `content-kb.ts` (lines 1–116) + `site.ts` (lines 1–224)

**Mandatory IIFE shell** (from `content-kb.ts` line 1 — exact pattern):
```typescript
((): void => {
  'use strict';
  // ...
  document.addEventListener('DOMContentLoaded', () => {
    attachFilters();
    attachCopyButtons();
  });
})();
```

**window.DeckFlow namespace access pattern** (from `df-typeahead.ts` lines 1–23 — exact shape):
```typescript
type DeckFlowNamespace = {
    attachTypeahead?: (
        input: HTMLInputElement,
        panel: HTMLDivElement,
        minChars: number,
        onPick: (name: string) => void,
        options?: { endpoint?: string; debounceMs?: number; onError?: (message?: string) => void; }
    ) => void;
    // ...other existing methods
};

type DeckFlowWindow = Window & { DeckFlow?: DeckFlowNamespace; };
const win = window as DeckFlowWindow;
```

**localStorage access pattern with guard** (from `site.ts` lines 94–100 — wrap all localStorage calls):
```typescript
const getStoredTheme = (): string | null => {
    try {
        return window.localStorage.getItem(themeStorageKey);
    } catch {
        return null;
    }
};
```
Apply same try/catch guard to ALL localStorage reads and writes in `kb-selection.ts`.

**localStorage keys** (from RESEARCH.md Pattern 6):
```typescript
const PINNED_KEY = 'deckflow.kb.pinned';    // [{id: string, title: string}][], max 3
const FOLLOWED_KEY = 'deckflow.kb.followed'; // [{source: string}][]
```

**Event delegation pattern** (from `content-kb.ts` lines 8–82 — query selectors on data attributes):
```typescript
const cards = Array.from(document.querySelectorAll<HTMLElement>('[data-kb-entry]'));
// ...
document.querySelectorAll<HTMLButtonElement>('[data-kb-pin]').forEach(button => {
    button.addEventListener('click', ...);
});
```

**Progressive enhancement guards** (from `content-kb.ts` line 9):
```typescript
if (cards.length === 0) { return; }
// All DOM queries must be guarded: if (element === null) return;
```

**DOMContentLoaded initialization** (from `content-kb.ts` line 112):
```typescript
document.addEventListener('DOMContentLoaded', () => {
    initKbSelection();
});
```

**Expose on namespace** (from `df-typeahead.ts` lines 276-278 pattern):
```typescript
win.DeckFlow = win.DeckFlow ?? {};
win.DeckFlow.initKbSelection = initKbSelection;
```

---

### `DeckFlow.Web/wwwroot/css/site-common.css` (config — new layout classes)

**Authoring rule** (CLAUDE.md + UI-SPEC): All new classes go in `site-common.css` only. Never touch `site.css` or any guild theme file. Touch only the lines that need touching; no Format Document.

**New classes required** (per UI-SPEC sections C-01 through C-06):
- `.kb-card-actions` — flex row, `gap: 0.5rem; margin-top: 0.5rem; flex-wrap: wrap`
- `.kb-card-actions button` — `flex: 1 1 auto; min-height: 44px; padding: 0.25rem 0.6rem; font-size: var(--fs-xs); font-weight: 600; border: 1px solid var(--line); border-radius: 6px; background: var(--panel-soft-bg); color: var(--ink); cursor: pointer`
- `.kb-pin-btn[aria-pressed="true"]` — `border-color: var(--accent); color: var(--accent-strong)`
- `.kb-follow-btn[aria-pressed="true"]` — same accent pattern
- `.kb-selection-tray` — flex container, `border: 1px solid var(--line); border-radius: 10px; background: var(--panel); margin-bottom: 1rem; padding: 0.75rem 1rem`
- `.kb-selection-tray__item` / `__label` / `__remove` / `__cta` / `__section` / `__list`
- `.kb-chip` — `display: inline-flex; align-items: center; gap: 0.25rem; padding: 0.1rem 0.45rem; border: 1px solid var(--line); border-radius: 999px; font-size: var(--fs-xs); font-weight: 600; background: var(--panel-soft-bg); white-space: nowrap`
- `.kb-chip--pinned`, `.kb-chip--followed` — `border-color: var(--accent); color: var(--accent-strong)`
- `.kb-chip__remove` — `background: transparent; border: none; cursor: pointer; min-height: 44px; min-width: 44px; display: inline-flex; align-items: center; justify-content: center`
- `.kb-chip__remove:hover` — `color: var(--danger)`
- `.kb-chip-area` / `.kb-chip-area__heading` / `.kb-chip-area__chips` / `.kb-chip-area__typeahead` / `.kb-chip-area__search` / `.kb-chip-area__empty-hint`
- `.kb-clip-origin` — badge style per UI-SPEC C-05
- `.kb-clip-origin--pinned`, `--followed`, `--evergreen`

No new CSS custom property tokens are required — all values compose from existing tokens already in `site.css :root`.

---

### `DeckFlow.Web.Tests/ContentKbMergedClipsTests.cs` (new test file)

**Analog:** `ContentKbRelevanceServiceTests.cs` (lines 1–155 — `TrackingContentSiteIndexStore`, `CreateRow` helper, `BuildArtifact` helper, test structure)

**Imports pattern** (lines 1–11):
```csharp
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using Xunit;

namespace DeckFlow.Web.Tests;
```

**Test class declaration** (mirrors existing conventions):
```csharp
public sealed class ContentKbMergedClipsTests
{
    // Test cases per spec:
    // - Tier1_PinsInjectedFirst_InDocumentOrder
    // - Tier2_FollowedCreator_GateRelaxedToOneDimension
    // - Tier4_EvergreenFills_MaxOneClip
    // - TrimOrder_Tier4RemovedBeforeTier3_BeforeTier2_BeforeTier1
    // - PinSurvivestrim_LastTier1ClipKept
    // - PinCap_MaxThreePinnedVideos_Enforced
}
```

**CreateRow helper** (reuse exact pattern from `ContentKbRelevanceServiceTests` — the `TrackingContentSiteIndexStore` fake is already implemented and can be reused by adding `IsEvergreen` parameter):
```csharp
private static ContentSiteIndexRow CreateRow(
    long id,
    string artifactPath,
    string[] archetypeTags,
    string[] bracketTags,
    bool isEvergreen = false)   // ADD parameter
    => new ContentSiteIndexRow { ..., IsEvergreen = isEvergreen };
```

---

### `DeckFlow.Web.Tests/PacketArtifactStoreTests.cs` (extend — new round-trip test)

**Analog:** self (lines 79–132 — `BuildZip_with_expert_context_round_trips_into_request` is the exact template)

**New test to add** (modeled on lines 79–132):
```csharp
[Fact]
public void BuildZip_with_selection_json_round_trips_into_request()
{
    var selectionJson = JsonSerializer.Serialize(new
    {
        pinnedVideoIds = new[] { "abc123" },
        followedCreators = new[] { "EDHRECast" }
    });

    var bytes = PacketArtifactStore.BuildZip(
        new DeckAnalysisRequest { DeckProfileJson = "{\"deck_profile\":{}}" },
        commanderName: "Atraxa",
        inputSummary: "summary",
        requestContextText: "context",
        referenceText: null,
        analysisPromptText: "prompt",
        deckProfileSchemaJson: "{}",
        setUpgradePromptText: null,
        selectionJson: selectionJson);

    var loaded = new DeckAnalysisRequest();
    using var ms = new MemoryStream(bytes);
    PacketArtifactStore.LoadFromZip(ms, loaded);

    Assert.Equal(["abc123"], loaded.PinnedVideoIds);
    Assert.Equal(["EDHRECast"], loaded.FollowedCreators);
    Assert.Contains("abc123", loaded.ExpertSelectionJson);
}

[Fact]
public void LoadFromZip_with_corrupt_selection_json_degrades_to_empty_selection()
{
    // Build zip with valid profile + corrupt selection JSON
    // Assert PinnedVideoIds.Count == 0, FollowedCreators.Count == 0, no throw
}
```

---

## Shared Patterns

### CSRF Double-Guard
**Source:** `AdminContentKbController.cs` lines 203–208
**Apply to:** `SetEvergreen` POST action
```csharp
[ValidateAntiForgeryToken]
// ...
if (!SameOriginRequestValidator.IsValid(Request))
{
    return StatusCode(StatusCodes.Status403Forbidden, SameOriginRequestValidator.GetForbiddenMessage());
}
```

### Graceful JSON Degradation
**Source:** `PacketArtifactStore.cs` lines 518–533 (`TryDeserializeFetchedEntries`)
**Apply to:** `LoadFromZip` selection JSON deserialization, `GetMergedClipsAsync` any JSON parse
```csharp
catch (JsonException)
{
    return Array.Empty<T>();  // or assign empty list — never rethrow
}
```

### `{ get; init; }` on Round-Tripped Records
**Source:** `ContentKbExcerpt.cs` line 8 comment
**Apply to:** `ContentKbExcerpt.ClipOrigin`, `ExpertSelectionState` properties, any new property on `ContentSiteIndexRow`

The compiler will not catch this error. It must be enforced by code review and by serialization round-trip tests.

### Feature Flag Gate
**Source:** `ContentKbRelevanceService.cs` line 157
**Apply to:** `GetMergedClipsAsync` — must also check `_flagCache.IsEnabled("content.kb.enabled")` before doing any store access.
```csharp
if (!_flagCache.IsEnabled("content.kb.enabled")) return null;
```

### ArgumentNullException Constructor Guard
**Source:** `AdminContentKbController.cs` lines 42–51
**Apply to:** Any new constructor with injected dependencies
```csharp
ArgumentNullException.ThrowIfNull(store);
ArgumentNullException.ThrowIfNull(seedLoader);
// ... one per parameter
```

### TempData Banner
**Source:** `AdminContentKbController.cs` line 21 + line 211
**Apply to:** `SetEvergreen` action
```csharp
private const string BannerKey = "AdminContentKbBanner";
// ...
TempData[BannerKey] = "Evergreen status updated.";
return RedirectToAction(nameof(Index));
```

### TypeScript localStorage Guard
**Source:** `site.ts` lines 94–100
**Apply to:** All localStorage access in `kb-selection.ts`
```typescript
try {
    return window.localStorage.getItem(key);
} catch {
    return null;
}
```

---

## No Analog Found

All files in Phase 32 have close analogs in the codebase. No files require pattern invention.

| File | Role | Reason No New Pattern Needed |
|---|---|---|
| `kb-selection.ts` | TS utility | Closest to `content-kb.ts` (IIFE, data-attribute selectors) + `site.ts` (localStorage); IIFE/namespace pattern fully covered |
| `ContentKbMergedClipsTests.cs` | test | New test class but directly extends `ContentKbRelevanceServiceTests.cs` infrastructure (same fakes, same helpers) |

---

## Metadata

**Analog search scope:** `DeckFlow.Core/`, `DeckFlow.Web/`, `DeckFlow.Web.Tests/`, `DeckFlow.Web/wwwroot/ts/`
**Files scanned:** 16 source files read in full; 2 test files read in targeted sections
**Pattern extraction date:** 2026-06-07
