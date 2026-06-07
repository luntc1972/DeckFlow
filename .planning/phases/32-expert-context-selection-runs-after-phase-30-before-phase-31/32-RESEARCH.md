# Phase 32: Expert Context Selection — Research

**Researched:** 2026-06-07
**Domain:** Brownfield layering — relevance service tier-fill, zip round-trip, localStorage/chip UI, DB column migration, admin toggle, panel markers
**Confidence:** HIGH — all claims verified against live codebase

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**S-01 Placement:** Both surfaces: pin on /content-kb browse cards AND editable chip area on the analysis form (mockup option C, hybrid two-stage picker). /content-kb browse: each video card gets "Pin for next analysis"; each creator heading gets "Follow"; a small tray shows current pins/follows. /deck-analysis form: Expert Context chip area shows carried-over pins + follows; chips removable; typeahead adds more (published KB entries / creators); selection submits via hidden fields.

**S-02 Pinnables:** Videos + creator follows only. NO tag pinning (rejected — duplicates auto archetype matching).

**S-03 Pin lifetime:** Video pins are one-shot: clear after the analysis run. Creator follows are sticky until unfollowed.

**S-04 Storage:** Browser localStorage, per-device. User accounts noted as future feature (out of scope).

**S-05 Merge mechanics:** Layered fill (NOT score-boost, NOT pins-exclusive). Within existing hard caps (K=5 clips, ~4.5KB rendered budget):
- Tier 1: Pinned videos (max 3 pins) — clips injected first, document order; no gate, no score threshold
- Tier 2: Followed creators — their artifacts' clips with >=1 scoring dimension hit (gate relaxed 2->1), score order
- Tier 3: Auto-scored — unchanged Phase 30 behavior (>=2 dims, score >= 2.0)
- Tier 4: Evergreen artifacts — fill remaining slots; max 1 evergreen clip per prompt
Budget trim removes tier 4 first, then 3, then 2; tier 1 last. If a single pinned video busts the budget alone, trim within it but keep at least 1 clip. A pin never silently vanishes below an auto match.

**S-06 Generic-advice videos:** Artifact-level `IsEvergreen` admin flag; evergreen fills leftover slots. Max 1 evergreen clip per prompt.

**S-07 Download persistence:** Selection state saved in packet zip as `33-expert-selection.json` alongside the clip set (`32-expert-context.json`). Re-upload restores both clips AND selection state; the form re-offers the pins (HIGH-2 replay-first pattern).

**Data flow:** localStorage -> hidden form fields -> `DeckAnalysisRequest.PinnedVideoIds` + `FollowedCreators` -> relevance service selection parameter -> ONE merged, trimmed set -> prompt variants + zip + result panel. Phase 30 invariant (prompt == zip == panel by construction) preserved.

**Component changes (locked surface map):**
- `ContentSiteIndexRow` + index store + seed loader: `IsEvergreen` boolean column (additive, migration-safe)
- `ContentKbRelevanceService`: new `GetMergedClipsAsync(selection, ...)` implementing tiers; existing `GetRelevantClipsAsync`/`ScoreAllAsync` untouched
- `DeckAnalysisRequest`: `PinnedVideoIds`, `FollowedCreators` round-tripped fields
- `DeckAnalysisPacketService`: thread selection; extend replay-first logic
- `PacketArtifactStore`: `33-expert-selection.json` allowlist + writer + reader (same-commit rule)
- Views (analysis form, /content-kb browse): chip area, pin/follow buttons, tray
- New TS `kb-selection.ts`: localStorage + chips + tray (progressive enhancement; form works without JS)
- `_ContentKbPanel.cshtml`: origin markers — pinned / followed / auto / evergreen
- `/Admin/ContentKb`: Evergreen toggle per row (POST, SameOrigin-validated like SetVisibility)

**Testing (required by spec):**
- Tier-fill unit tests: pin-first order, follow gate-relax, evergreen filler + 1-clip cap, trim order (4->3->2->1), pin survives trim, pin-cap (3) enforced
- Zip round-trip: selection JSON survives BuildZip -> LoadFromZip; corrupt selection entry degrades to empty selection (no throw)
- Controller: selection fields bind; replay restores selection
- TS/localStorage: manual + human-verify checkpoint at 2 viewports
- All records `{ get; init; }` + serialization round-trip tests (standing constraint)

### Claude's Discretion
- Exact chip/tray markup, CSS class names, and typeahead implementation details (must follow site-common.css layout rule + per-theme token rule)
- Hidden-field encoding format for selection submit
- Internal shape of the selection parameter passed to the relevance service
- Plan/wave decomposition (ROADMAP estimates 4 plans: schema/tiers -> request/packet/zip -> browse+form UI/TS -> admin toggle + markers + UI checkpoint)

### Deferred Ideas (OUT OF SCOPE)
- User accounts syncing pins/follows across devices (explicit user ask, deferred to future milestone)
- Tag pinning (rejected outright — duplicates automatic archetype matching)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SEL-01 | Pin video / follow creator from browse page + analysis-form chip area | Browse card markup pattern identified; chip area slots into analysis form Step 1; typeahead reuses `DeckFlow.attachTypeahead`; hidden-field binding follows existing `SelectedAnalysisQuestions` list-field pattern |
| SEL-02 | Layered fill: pins -> follows -> auto -> evergreen, within K=5 + budget; pins trimmed last | New `GetMergedClipsAsync` method on `IContentKbRelevanceService`; existing `ScoreArtifact` and `SelectTopClips` remain untouched; `GetPublishedRowsAsync` already supplies the full visible corpus |
| SEL-03 | One-shot video pins, sticky creator follows, localStorage | `site.ts` confirms localStorage usage pattern exists (theme storage); `kb-selection.ts` (new file) handles the per-key persistence; module pattern matches all existing TS files (IIFE, strict, `window.DeckFlow` namespace) |
| SEL-04 | Selection persisted in packet zip `33-expert-selection.json`; re-upload restores | `PacketAllowedNames` HashSet is the exact gating point; `BuildZip` signature accepts new optional param; `LoadFromZip` reads and assigns; corrupt-JSON fallback matches existing `ExpertContextJson` try/catch pattern at lines 539-547 |
| SEL-05 | Artifact-level Evergreen admin flag, max 1 evergreen clip | `IsEvergreen` column added via the existing `GetTableColumnsAsync` + `ALTER TABLE ADD COLUMN` migration pattern (same as `is_visible` was added); `ContentSiteIndexRow` gets new `bool IsEvergreen { get; init; }` property; `ReadRow` reads ordinal 13 |
| SEL-06 | Panel origin markers: pinned/followed/auto/evergreen | `_ContentKbPanel.cshtml` model changes from `IReadOnlyList<ContentKbExcerpt>?` to a view model or decorated excerpt that carries `ClipOrigin`; `ContentKbExcerpt` adds `ClipOrigin` property with `{ get; init; }` constraint |
</phase_requirements>

---

## Summary

Phase 32 is a pure brownfield layering phase. Every component it touches already exists and is well-understood from reading the live code. The research task is therefore an audit of exact call signatures, SQL patterns, and TS conventions — not a technology survey.

The central engineering challenge is the new `GetMergedClipsAsync` method. It must implement four tiers with a budget-trim waterfall while leaving `GetRelevantClipsAsync` and `ScoreArtifact` completely untouched (those are used by admin preview scoring and the existing auto path). The method receives a `ExpertSelection` parameter (name TBD — Claude's discretion) carrying up to 3 pinned video IDs and an arbitrary set of followed creator names; it returns the same `IReadOnlyList<ContentKbExcerpt>?` the existing method returns, so all downstream consumers (prompt builder, zip writer, panel) require only additive changes.

The `IsEvergreen` column follows the identical pattern used to add `is_visible` post-launch: `GetTableColumnsAsync` checks for the column name and `ALTER TABLE ADD COLUMN ... DEFAULT FALSE/0` adds it when absent. No migration script, no deploy ceremony — the schema is self-healing on first request.

The zip side is the most mechanical change: add `33-expert-selection.json` to `PacketAllowedNames`, extend `BuildZip` with an optional `selectionJson` parameter, read it in `LoadFromZip`, and assign it to two new properties on `DeckAnalysisRequest`. The same-commit rule means the entry name must be in the allowlist before any zip containing it is created.

**Primary recommendation:** Implement in four plans exactly as ROADMAP estimates — schema+tiers, request+packet+zip, browse+form UI/TS, admin+panel+checkpoint. The dependency order is strict: plan 1 (store + service) must ship before plan 2 (packet wiring), plan 2 before plan 3 (UI that calls the service), plan 3 and plan 4 can run in parallel if needed.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Pin/Follow persistence | Browser (localStorage) | — | S-04 locked; per-device, no server state |
| Selection submission | Frontend (hidden form fields) | API/Backend (model binding) | Progressive enhancement: works without JS via empty fields |
| Tier-fill merge logic | API/Backend (`ContentKbRelevanceService`) | — | Server owns all clip data; must happen before prompt is built |
| Budget trim | API/Backend (inside `GetMergedClipsAsync`) | — | Same service layer that owns the existing trim loop |
| `IsEvergreen` flag | Database (content_site_index) | Admin UI | Stored in DB; toggled by admin action, read by service |
| Evergreen clip selection | API/Backend (tier 4 in merge method) | — | Max 1 cap enforced server-side |
| Origin tagging | API/Backend (decoration at tier-fill time) | — | Service knows which tier each clip came from |
| Origin display | Frontend Server (Razor `_ContentKbPanel.cshtml`) | — | Renders server-assigned origin string per clip |
| Zip round-trip | API/Backend (`PacketArtifactStore`) | — | Pure CPU/binary; no browser involvement |
| Chip area render | Frontend Server (Razor analysis form) | Browser (TS chip management) | Server renders initial state from replayed zip; TS manages live adds/removes |
| Evergreen admin toggle | API/Backend (`AdminContentKbController`) | — | POST action, mirrors SetVisibility exactly |

---

## Standard Stack

No new packages. All implementation uses existing project dependencies.

### Core (already installed)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | In-box (.NET 10) | Selection JSON serialize/deserialize | Already used for `32-expert-context.json`; `{ get; init; }` constraint applies |
| System.IO.Compression | In-box (.NET 10) | Zip artifact write/read | `PacketArtifactStore` already uses this |
| Microsoft.Data.Sqlite / Npgsql | 10.0.0 | `is_evergreen` column migration | Same dual-dialect pattern as `is_visible` |
| TypeScript 6.0.2 | Pinned in package.json | `kb-selection.ts` module | Matches all existing TS files; tsconfig strict + module none |

### No New Dependencies
This phase installs zero external packages. The Package Legitimacy Audit section is omitted accordingly.

---

## Package Legitimacy Audit

Not applicable — Phase 32 installs no external packages.

---

## Architecture Patterns

### System Architecture Diagram

```
[localStorage (browser)]
        |
        | pin/follow state
        v
[kb-selection.ts] --populate--> [hidden fields: PinnedVideoIds[], FollowedCreators[]]
                                          |
                                          | HTTP POST /deck-analysis
                                          v
                               [DeckAnalysisRequest]
                                 .PinnedVideoIds (List<string>)
                                 .FollowedCreators (List<string>)
                                          |
                                          | passed to
                                          v
                          [IContentKbRelevanceService.GetMergedClipsAsync]
                              |                                  |
                    [IContentSiteIndexStore                  [artifact files
                     .GetPublishedRowsAsync]                  on disk]
                              |
                    Tier 1: pinned videos (no gate, doc order)
                    Tier 2: followed creators (>=1 dim hit, score order)
                    Tier 3: auto-scored (>=2 dims, score >= 2.0)
                    Tier 4: evergreen artifacts (max 1 clip)
                    Budget trim (4 -> 3 -> 2 -> 1)
                              |
                    IReadOnlyList<ContentKbExcerpt> (with ClipOrigin)
                              |
                   +----------+----------+
                   |                     |
          [analysis prompt]    [PacketArtifactStore.BuildZip]
          [variants]              32-expert-context.json (clips)
                                  33-expert-selection.json (selection state)
                                          |
                                  [re-upload -> LoadFromZip]
                                  restores DeckAnalysisRequest
                                  .PinnedVideoIds, .FollowedCreators
                                  + ExpertContextJson
                                          |
                   [_ContentKbPanel.cshtml] -- per-clip ClipOrigin -> markers
```

### Recommended Project Structure

No new folders. Changes are additive to existing file locations:

```
DeckFlow.Core/
  Content/
    ContentSiteIndexStore.cs     # IsEvergreen column migration + SQL
  Knowledge/
    ContentArtifactSpec.cs       # ContentSiteIndexRow adds IsEvergreen property

DeckFlow.Web/
  Models/
    DeckAnalysisRequest.cs       # PinnedVideoIds, FollowedCreators, SelectionJson
    ContentKbExcerpt.cs          # ClipOrigin property (init)
  Services/
    ContentKbRelevanceService.cs # GetMergedClipsAsync + ExpertSelection parameter type
  Controllers/Admin/
    AdminContentKbController.cs  # SetEvergreen POST action
  Views/
    ContentKb/Index.cshtml       # Pin/Follow buttons + tray
    Deck/DeckAnalysis.cshtml     # Expert Context chip area + hidden fields
    Deck/_ContentKbPanel.cshtml  # Origin markers per clip
  wwwroot/ts/
    kb-selection.ts              # NEW: localStorage + chips + tray

DeckFlow.Web.Tests/
  ContentKbMergedClipsTests.cs   # NEW: tier-fill unit tests (SEL-02)
  PacketArtifactStoreTests.cs    # EXTEND: 33-expert-selection.json round-trip
  DeckAnalysisRequestTests.cs    # EXTEND or new: PinnedVideoIds/FollowedCreators binding
```

### Pattern 1: Additive DB Column Migration (IsEvergreen)

The `is_visible` column was added post-launch using this exact pattern. Replicate it for `is_evergreen`.

**What:** `EnsureSchemaAsync` calls `GetTableColumnsAsync`, then conditionally runs `ALTER TABLE ADD COLUMN` if the column is absent. The CREATE TABLE SQL also gains the column for fresh installs.

**Source:** `DeckFlow.Core/Content/ContentSiteIndexStore.cs` lines 56-64

```csharp
// In EnsureSchemaAsync, after creating the table:
var columns = await GetTableColumnsAsync(connection, "content_site_index", cancellationToken).ConfigureAwait(false);
if (!columns.Contains("is_visible"))
{
    await using var addVisible = connection.CreateCommand();
    addVisible.CommandText = _connectionInfo.IsPostgres
        ? "ALTER TABLE content_site_index ADD COLUMN is_visible BOOLEAN NOT NULL DEFAULT FALSE;"
        : "ALTER TABLE content_site_index ADD COLUMN is_visible INTEGER NOT NULL DEFAULT 0;";
    await addVisible.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
// REPLICATE for is_evergreen:
if (!columns.Contains("is_evergreen"))
{
    await using var addEvergreen = connection.CreateCommand();
    addEvergreen.CommandText = _connectionInfo.IsPostgres
        ? "ALTER TABLE content_site_index ADD COLUMN is_evergreen BOOLEAN NOT NULL DEFAULT FALSE;"
        : "ALTER TABLE content_site_index ADD COLUMN is_evergreen INTEGER NOT NULL DEFAULT 0;";
    await addEvergreen.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```

**ReadRow impact:** `is_evergreen` lands at ordinal 13 (after `is_visible` at 12). Add `IsEvergreen = ReadVisibility(reader, 13)` — reuse the existing `ReadVisibility` helper which handles bool/long/int/short/string across both providers.

**ContentSiteIndexRow addition:**
```csharp
/// <summary>Whether this artifact fills evergreen advice slots in any deck's analysis prompt.</summary>
public bool IsEvergreen { get; init; }
```

**The `{ get; init; }` constraint (CLAUDE.md):** `ContentSiteIndexRow` is a `sealed record` with `required` init-only properties. This is already the pattern. System.Text.Json serializes `init` properties correctly in .NET 10. The project comment in `ContentKbExcerpt.cs` line 8 explicitly calls this out: `// Why: System.Text.Json skips get-only properties for this round-tripped DTO; every member must stay { get; init; }.`

### Pattern 2: PacketArtifactStore Allowlist + Writer + Reader

**Source:** `DeckFlow.Web/Services/PacketArtifactStore.cs` (fully read)

The `PacketAllowedNames` HashSet is the same-commit gate. Any zip entry name not in this set causes `ReadEntries` to throw `InvalidOperationException`. The entry must be added to `PacketAllowedNames` in the same commit that adds the write/read logic.

**What to add to `PacketAllowedNames`:**
```csharp
"33-expert-selection.json",
```

**What to add to `BuildZip` signature:**
```csharp
public static byte[] BuildZip(
    DeckAnalysisRequest request,
    // ...existing params...
    string? expertContextJson = null,
    string? selectionJson = null)   // NEW
```

**What to add to `LoadFromZip`:**
```csharp
entries.TryGetValue("33-expert-selection.json", out var selectionJson);
// After existing ExpertContextJson assignment:
// request.ExpertSelectionJson = selectionJson ?? string.Empty;
// Then deserialize PinnedVideoIds + FollowedCreators from selectionJson with try/catch -> empty on JsonException
```

**Corrupt selection degrades gracefully:** Model the try/catch on the existing `ExpertContextJson` pattern (lines 539-547 of `DeckAnalysisPacketService.cs`):
```csharp
// In LoadFromZip or in BuildAsync replay path:
try
{
    var sel = JsonSerializer.Deserialize<ExpertSelectionState>(selectionJson);
    request.PinnedVideoIds = sel?.PinnedVideoIds ?? [];
    request.FollowedCreators = sel?.FollowedCreators ?? [];
}
catch (JsonException)
{
    // Degrade to empty selection — do not throw
}
```

### Pattern 3: DeckAnalysisRequest Field Addition

**Source:** `DeckFlow.Web/Models/DeckAnalysisRequest.cs` (fully read)

The existing pattern: backing field initialized to empty/safe default, property setter assigns `value ?? fallback`. For list fields (`SelectedAnalysisQuestions`, `SelectedSetCodes`) the setter assigns `value ?? []`.

**New fields to add:**
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

**Hidden field encoding:** Use ASP.NET Core's list-field convention — `<input type="hidden" name="PinnedVideoIds" value="@id" />` repeated per item. MVC model binding handles `List<string>` from repeated same-name fields natively. This is the same approach used for `SelectedAnalysisQuestions` hidden checkboxes.

### Pattern 4: IContentKbRelevanceService Extension

**Source:** `DeckFlow.Web/Services/ContentKbRelevanceService.cs` (fully read)

The interface gains one new method. The existing two methods (`GetRelevantClipsAsync`, `ScoreAllAsync`) stay unchanged — the planner must enforce this.

```csharp
// Add to IContentKbRelevanceService:
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

**`ExpertSelection` shape (Claude's discretion):**
```csharp
internal sealed record ExpertSelection(
    IReadOnlyList<string> PinnedVideoIds,   // YoutubeVideoId or RssGuid values, max 3
    IReadOnlySet<string> FollowedCreators); // Source names, case-insensitive
```

**Tier-fill implementation approach:**

`GetPublishedRowsAsync` already returns all visible rows. The merge method partitions them into tiers before scoring:

- **Tier 1:** `rows.Where(r => pinnedIds.Contains(r.YoutubeVideoId ?? r.RssGuid))` — load clips in document order, no scoring gate. Max 3 videos total (cap enforced by taking `pinnedIds.Take(3)`).
- **Tier 2:** `rows.Where(r => followedCreators.Contains(r.Source, OrdinalIgnoreCase))` and NOT in tier 1 — score with `ScoreArtifact`, keep rows where `dimensionsHit >= 1` (relaxed from 2). Order by score descending.
- **Tier 3:** remaining rows scored with existing `ScoreArtifact` logic — keep rows where score >= 2.0. Order by score descending.
- **Tier 4:** `rows.Where(r => r.IsEvergreen)` and NOT already selected — load up to 1 clip regardless of score.

**Budget trim waterfall:** Build the flat selected list by appending tiers in order, then apply the existing `EstimateRenderedChars` loop — but trim from tier 4 first, then 3, then 2, then tier 1 (last resort). If only tier 1 clips remain and they still bust the budget, trim within tier 1 but keep at least 1 clip.

**Reuse of private helpers:** `ParseRowsAsync`, `BuildScoreInputAsync`, `EstimateRenderedChars`, `SelectTopClips` — the merge method calls the same internal helpers. The `ScoreArtifact` internal static method accepts the scoring inputs and returns score+dimensionsHit. For tier 2, check `dimensionsHit >= 1` instead of `dimensionsHit >= 2`.

**`ContentKbExcerpt.ClipOrigin` addition:**
```csharp
/// <summary>How this clip entered the selection (pinned / followed / auto / evergreen).</summary>
public string ClipOrigin { get; init; } = "auto";
```

The merge method sets `ClipOrigin` during clip construction. Existing `GetRelevantClipsAsync` path leaves the default `"auto"` — no change to that method.

**DeckAnalysisPacketService wiring:** The call site at line 664 (`replayedExpertContextJson is null && _contentKbRelevanceService is not null`) changes to call `GetMergedClipsAsync` instead of `GetRelevantClipsAsync`, passing the selection extracted from the request. The replay-first guard (`replayedExpertContextJson is not null`) already short-circuits so replayed clips are returned unchanged.

### Pattern 5: AdminContentKbController — SetEvergreen Toggle

**Source:** `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` (fully read)

Mirror `SetVisibility` exactly:

```csharp
/// <summary>Marks or unmarks an artifact as evergreen. Double-CSRF-guarded.</summary>
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

`IContentSiteIndexStore` gains `Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken ct)` mirroring `SetVisibilityAsync`. The SQL is a single-column UPDATE identical in structure.

### Pattern 6: TypeScript Module (`kb-selection.ts`)

**Source:** Inspected all 17 existing TS files; conventions confirmed.

**Must follow:**
- IIFE pattern: `((): void => { 'use strict'; ... })();`
- `module: "none"` in tsconfig — no imports/exports; functions exposed via `window.DeckFlow` namespace
- Strict TypeScript; every variable typed explicitly
- No external dependencies
- File goes in `wwwroot/ts/kb-selection.ts`; compiled output is `wwwroot/js/kb-selection.js`
- Wire into the view via `<script src="~/js/kb-selection.js" asp-append-version="true"></script>` in the `@section Scripts` block

**localStorage keys (suggestion — Claude's discretion):**
- `"deckflow.kb.pinned"` — JSON array of `{id: string, title: string}` (one-shot pins)
- `"deckflow.kb.followed"` — JSON array of `{source: string}` (sticky follows)

**Typeahead reuse:** The existing `window.DeckFlow.attachTypeahead` (from `df-typeahead.ts`) is already exposed on the namespace. The chip area typeahead for KB entries and creators should call `attachTypeahead` with a new API endpoint (e.g., `/api/content-kb/search`) that returns matching entry titles and creator names. This endpoint is a new `SuggestionsApiController`-style JSON endpoint.

**Progressive enhancement requirement (locked):** The analysis form must submit successfully with empty `PinnedVideoIds` and `FollowedCreators` when JS is absent. The hidden-field area renders zero inputs by default; TS injects them from localStorage on `DOMContentLoaded`.

**Chip tray on browse page:** Each video card article (`hub-card`) gains a "Pin" button that writes to localStorage and updates the tray. Each source heading gains a "Follow" button. The tray is a persistent `<div>` showing current pins/follows with remove buttons. No page navigation required; entirely client-side.

### Pattern 7: _ContentKbPanel.cshtml — Origin Markers

**Current model:** `@model IReadOnlyList<DeckFlow.Web.Models.ContentKbExcerpt>?`

**Change:** The model stays the same type — `ClipOrigin` is now a property on `ContentKbExcerpt`. The panel reads `clip.ClipOrigin` to render the marker badge.

```html
@* In the clip article: *@
<span class="kb-clip-origin kb-clip-origin--@clip.ClipOrigin" aria-label="@ClipOriginLabel(clip.ClipOrigin)">
    @ClipOriginGlyph(clip.ClipOrigin)
</span>
```

```csharp
@functions {
    private static string ClipOriginGlyph(string origin) => origin switch
    {
        "pinned"    => "📌",
        "followed"  => "★",
        "evergreen" => "☘",
        _ => "auto"
    };
    private static string ClipOriginLabel(string origin) => origin switch
    {
        "pinned"    => "Pinned by you",
        "followed"  => "From followed creator",
        "evergreen" => "Evergreen advice",
        _ => "Auto-selected"
    };
}
```

CSS classes follow site-common.css convention (no changes to per-guild theme files required for functional markers; visual polish is Claude's discretion).

### Anti-Patterns to Avoid

- **Adding `is_evergreen` to the CREATE TABLE SQL only without the `ALTER TABLE` guard:** The production DB already has the table. The guard is mandatory.
- **Calling `GetRelevantClipsAsync` from the new service wiring instead of `GetMergedClipsAsync`:** The interface method must be switched at the `DeckAnalysisPacketService` call site.
- **Emitting `33-expert-selection.json` from `BuildZip` before adding it to `PacketAllowedNames`:** The same-commit rule means `ReadEntries` would reject the entry on the very next upload attempt. Both changes belong in one commit.
- **Using `{ get; }` instead of `{ get; init; }` on `ContentKbExcerpt.ClipOrigin` or any new record property:** System.Text.Json silently skips get-only properties in .NET 9+. This has already caused a real bug in `EdhTop16Client`. Every new property on a round-tripped record must be `{ get; init; }`.
- **Storing pin state in a cookie or server session:** S-04 is locked to localStorage.
- **Applying score >= 2.0 gate to tier 2 (followed creators):** The gate is relaxed to `dimensionsHit >= 1` for follows. Using the existing `ScoreArtifact` return value of 0 (which is what happens when `dimensionsHit < 2`) would silently discard followed-creator artifacts.
- **Modifying the prompt variant raw-string literals:** Per CLAUDE.md, the duplicate prose across ChatGPT/Claude/Gemini variants is intentional. If expert context text changes, all three must be edited separately by hand.
- **Running "Format Document" or code cleanup:** CLAUDE.md prohibits formatter sweeps. Touch only the lines that need touching.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| DB column existence check | Custom `PRAGMA` parsing | `GetTableColumnsAsync` (already in `ContentSiteIndexStore`) | Handles both SQLite and Postgres; tested pattern |
| Zip entry gating | Custom entry name validation | `PacketAllowedNames` HashSet + existing `ReadEntries` | One place controls allowlist; throwing on unknown entries is a security invariant |
| Typeahead for chip area | Custom autocomplete | `window.DeckFlow.attachTypeahead` from `df-typeahead.ts` | Already handles ARIA combobox pattern, debounce, keyboard nav |
| Visibility-read across DB providers | Custom switch on provider type | `ReadVisibility(reader, ordinal)` helper in `ContentSiteIndexStore` | Handles bool/long/int/short/string across SQLite and Postgres |
| Clip budget estimation | Ad-hoc char count | `EstimateRenderedChars` (internal static in `ContentKbRelevanceService`) | Already calibrated with header budget + per-clip overhead |
| Score computation | Re-implement scoring | `ScoreArtifact` (internal static, accepts `ScoreInput`) | Already has all weights, archetype specificity table, dimension counting |

**Key insight:** Every non-trivial piece of infrastructure this phase needs already exists in the codebase. The phase is additive composition, not new invention.

---

## Common Pitfalls

### Pitfall 1: `ScoreArtifact` Returns 0 When `dimensionsHit < 2`

**What goes wrong:** Tier 2 (followed creators) calls `ScoreArtifact`. The method returns `dimensionsHit >= 2 ? score : 0d`. A followed artifact with only 1 matching dimension returns score=0. If you filter on `score > 0`, followed clips are silently dropped.

**Why it happens:** `ScoreArtifact` was designed for tier 3 (auto) which gates at >= 2 dimensions. Its return value of 0 for 1-dimension matches conflates "low relevance" with "below gate."

**How to avoid:** For tier 2, after calling `ScoreArtifact`, also check the raw dimension count via a separate helper or by checking if `dimensionsHit >= 1` directly. One approach: the merge method computes `dimensionsHit` separately for tier 2 rows by calling the individual scoring sub-expressions, or adds an overload to `ScoreArtifact` that accepts a `minDimensions` parameter. Do not modify the existing `ScoreArtifact` signature — it is used by `ScoreAllAsync` (admin preview).

**Warning signs:** Tier 2 unit test for a followed creator with 1 dimension hit returns empty list.

### Pitfall 2: ReadRow Ordinal Drift After Adding `is_evergreen`

**What goes wrong:** `ReadRow` reads columns by positional ordinal (0-12). Adding `is_evergreen` as column 13 in `EnsureSchemaAsync` via `ALTER TABLE ADD COLUMN` works on existing DBs. But the `PostgresCreateTableSql` and `SqliteCreateTableSql` constants must also include `is_evergreen` so fresh installs have it at the correct position. If the column ends up at a different ordinal on fresh vs migrated DBs, `ReadRow` reads the wrong column.

**Why it happens:** The CREATE TABLE SQL and the ALTER TABLE migration are independent code paths. They must stay in sync.

**How to avoid:** Update both CREATE TABLE constants AND the `EnsureSchemaAsync` migration block in the same commit. Add `IsEvergreen = ReadVisibility(reader, 13)` to `ReadRow`. Write a test that round-trips `IsEvergreen = true` through `UpsertRowAsync` and `GetByIdAsync`.

### Pitfall 3: Zip Re-upload Clears Selection State

**What goes wrong:** `LoadFromZip` restores `ExpertContextJson` (clips) but does not restore `PinnedVideoIds`/`FollowedCreators`. The form shows the replayed clips in the panel but offers no pre-populated pins in the chip area.

**Why it happens:** The spec says replay "re-offers the pins." This requires the selection state (from `33-expert-selection.json`) to be deserialized into `PinnedVideoIds`/`FollowedCreators` on the request AND the view to render those as initial chip values (so TS can initialize localStorage from them on page load).

**How to avoid:** `LoadFromZip` must deserialize `33-expert-selection.json` and populate `request.PinnedVideoIds` and `request.FollowedCreators`. The analysis form view must emit those as initial chip inputs that `kb-selection.ts` reads on `DOMContentLoaded` and writes back to localStorage.

### Pitfall 4: Budget Trim Removes All Tier 1 Clips

**What goes wrong:** A single pinned video with many clips busts the 4.5KB budget. Naive trim-from-end removes all clips including tier 1.

**Why it happens:** The existing trim loop (`while ... RemoveAt(selectedClips.Count - 1)`) does not know tier boundaries.

**How to avoid:** The merge method must track tier boundaries in the flat list. Budget trim first removes from tier 4 (evergreen slots), then tier 3, then tier 2. Only enters tier 1 if the budget is still blown after tiers 2-4 are exhausted. Within tier 1, keep at least 1 clip (the first clip of the first pinned video). Write a unit test that pins a single 5-clip video against a 200-char budget and asserts >=1 clip remains.

### Pitfall 5: `module: "none"` Means No ES Imports

**What goes wrong:** `kb-selection.ts` tries to import from another TS file (e.g., `import { DeckFlow } from './site';`). TypeScript compiles it but the browser has no module loader, so the import fails silently or throws.

**Why it happens:** `tsconfig.json` sets `"module": "none"` — all modules compile to plain script globals, no import/export syntax allowed.

**How to avoid:** Access the `DeckFlow` namespace via `(window as any).DeckFlow` or a locally declared type alias. All 17 existing TS files use this pattern. Check `df-typeahead.ts` lines 276-278 for the canonical `window.DeckFlow.attachTypeahead` exposure pattern.

### Pitfall 6: `{ get; init; }` Constraint on Round-Tripped Records

**What goes wrong:** A new property is added to `ContentKbExcerpt` (e.g., `ClipOrigin`) as `{ get; }`. Round-trip serialization through `32-expert-context.json` silently drops the value — JSON deserialization skips the property, `ClipOrigin` is always null/default after re-upload.

**Why it happens:** System.Text.Json in .NET 9+ skips get-only properties during serialization. The comment at `ContentKbExcerpt.cs` line 8 explicitly documents this.

**How to avoid:** Every property on every record that participates in zip serialization must be `{ get; init; }`. Add a serialization round-trip test for `ContentKbExcerpt` that includes the new `ClipOrigin` property.

---

## Code Examples

### IsEvergreen Migration Block (verified pattern)
```csharp
// Source: DeckFlow.Core/Content/ContentSiteIndexStore.cs lines 56-64 (is_visible)
// Replicate exactly for is_evergreen:
if (!columns.Contains("is_evergreen"))
{
    await using var addEvergreen = connection.CreateCommand();
    addEvergreen.CommandText = _connectionInfo.IsPostgres
        ? "ALTER TABLE content_site_index ADD COLUMN is_evergreen BOOLEAN NOT NULL DEFAULT FALSE;"
        : "ALTER TABLE content_site_index ADD COLUMN is_evergreen INTEGER NOT NULL DEFAULT 0;";
    await addEvergreen.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```

### SetEvergreen Store Method (verified pattern)
```csharp
// Source: DeckFlow.Core/Content/ContentSiteIndexStore.cs lines 276-292 (SetVisibilityAsync)
public async Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
{
    await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
    await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    await using var command = connection.CreateCommand();
    command.CommandText = """
        UPDATE content_site_index
           SET is_evergreen = @evergreen
         WHERE id = @id;
        """;
    RelationalDatabaseConnection.AddParameter(command, "@evergreen", FormatVisibility(evergreen));
    RelationalDatabaseConnection.AddParameter(command, "@id", id);
    return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```

### Replay-First Guard Extension (verified pattern)
```csharp
// Source: DeckFlow.Web/Services/DeckAnalysisPacketService.cs lines 534-547
// Existing pattern for ExpertContextJson — mirror for selection:
var replayedSelectionJson = string.IsNullOrWhiteSpace(request.ExpertSelectionJson)
    ? null
    : request.ExpertSelectionJson;
if (replayedSelectionJson is not null)
{
    try
    {
        var sel = JsonSerializer.Deserialize<ExpertSelectionState>(replayedSelectionJson);
        // Populate request fields for the chip area render
        if (sel?.PinnedVideoIds?.Count > 0) request.PinnedVideoIds = [..sel.PinnedVideoIds];
        if (sel?.FollowedCreators?.Count > 0) request.FollowedCreators = [..sel.FollowedCreators];
    }
    catch (JsonException)
    {
        // Degrade gracefully — empty selection, no throw
    }
}
```

### PacketAllowedNames Addition (same-commit rule)
```csharp
// Source: DeckFlow.Web/Services/PacketArtifactStore.cs lines 27-42
// Add in the same commit as the BuildZip/LoadFromZip changes:
private static readonly HashSet<string> PacketAllowedNames = new(StringComparer.OrdinalIgnoreCase)
{
    // ...existing entries...
    "32-expert-context.json",
    "33-expert-selection.json",   // ADD
    // ...rest...
};
```

### TS Module Shell (verified pattern)
```typescript
// Source: Pattern from DeckFlow.Web/wwwroot/ts/df-typeahead.ts lines 276-278
// All TS files use this IIFE + window.DeckFlow namespace pattern:
((): void => {
  'use strict';

  type DeckFlowWindow = Window & {
    DeckFlow?: {
      attachTypeahead?: (...) => void;
      // other existing methods
      initKbSelection?: () => void;   // expose for testing
    };
  };

  const win = window as DeckFlowWindow;

  // localStorage keys
  const PINNED_KEY = 'deckflow.kb.pinned';
  const FOLLOWED_KEY = 'deckflow.kb.followed';

  // ... implementation ...

  win.DeckFlow = win.DeckFlow ?? {};
  win.DeckFlow.initKbSelection = initKbSelection;

  document.addEventListener('DOMContentLoaded', initKbSelection);
})();
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Auto-only clip selection | Layered fill with user pins/follows | Phase 32 | Tier-fill replaces single-method call |
| No origin metadata on clips | `ClipOrigin` property on `ContentKbExcerpt` | Phase 32 | Panel can show provenance; additive to record |
| Expert context clips only in zip | Clips + selection state in zip | Phase 32 | Re-upload restores chips as well as clip display |

**No deprecated patterns introduced.** All additions are additive over Phase 30 groundwork.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `is_evergreen` will land at ordinal 13 in `ReadRow` (after `is_visible` at 12) | Pitfall 2, Code Examples | `ReadRow` reads wrong column for some rows if column order differs between providers |
| A2 | The `window.DeckFlow.attachTypeahead` function is already loaded before `kb-selection.ts` runs on pages that include both scripts | Pattern 6 | Typeahead chip area silently fails; scripts need ordering or deferred init |
| A3 | `/content-kb` browse page has no existing creator-heading `<h*>` grouping by source — the source label is inside each card | Browse view inspection | Follow button placement needs a different DOM anchor if source grouping already exists |

**A3 detail:** The current browse view (`ContentKb/Index.cshtml`) renders a flat `hub-grid` of cards with no source-group headings. The "Follow creator" button for a source must therefore appear on each card (or in the tray), not on a heading element. The spec says "each creator heading gets ★ Follow" — this implies a grouped-by-source layout that does not currently exist. The planner should decide: (a) add source-group headings to the browse view and put Follow on the heading, or (b) put Follow on each card alongside Pin. This is Claude's discretion territory.

---

## Open Questions

1. **Where does "Follow creator" button live on browse cards?**
   - What we know: Current browse page is a flat card grid with no source-group headings. Source is displayed as a tag badge inside each card.
   - What's unclear: Spec says "each creator heading gets ★ Follow" — but no heading exists. Is the plan to restructure the grid into source groups, or put Follow on each card?
   - Recommendation: Put Follow on each card next to the source tag (simplest; no layout restructure). Show in tray. If source-group headings are added in UI/TS plan, Follow can migrate to the heading.

2. **`ExpertSelectionState` serialization contract**
   - What we know: The `33-expert-selection.json` payload must survive BuildZip -> LoadFromZip. `{ get; init; }` constraint applies.
   - What's unclear: Exact JSON shape. Suggestion: `{"pinnedVideoIds": ["abc123"], "followedCreators": ["EDHRECast"]}` using `JsonNamingPolicy.CamelCase`.
   - Recommendation: Define as `internal sealed record ExpertSelectionState` with `IReadOnlyList<string>` properties and `{ get; init; }`.

3. **New API endpoint for chip area typeahead**
   - What we know: The chip area needs typeahead to add more pins/follows beyond what localStorage already carries. `attachTypeahead` expects an `endpoint` returning `string[]`.
   - What's unclear: Where to add it. `SuggestionsApiController` already exists for category suggestions.
   - Recommendation: Add to `SuggestionsApiController` as `/api/content-kb/entries` (returns visible entry titles+ids) and `/api/content-kb/creators` (returns distinct source names). Both are simple reads from `IContentSiteIndexStore`.

---

## Environment Availability

This phase has no external dependencies beyond the existing project. Step 2.6 SKIPPED (no new external tools, services, or CLIs required).

---

## Validation Architecture

nyquist_validation is explicitly `false` in `.planning/config.json`. This section is omitted.

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — |
| V3 Session Management | no | localStorage is not a session |
| V4 Access Control | yes (admin toggle) | `BasicAuthMiddleware` (existing) + `SameOriginRequestValidator` (existing) |
| V5 Input Validation | yes (PinnedVideoIds, FollowedCreators) | Normalize + cap at server; treat as untrusted form input |
| V6 Cryptography | no | — |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Spoofed video IDs in PinnedVideoIds | Tampering | Server resolves IDs against `IContentSiteIndexStore.GetPublishedRowsAsync`; unrecognized IDs produce no clips |
| Excessive pins bypassing K=5 cap | Denial of Service | Server enforces pin-cap (3) and K=5 clip cap regardless of form input |
| CSRF on SetEvergreen toggle | Tampering | `[ValidateAntiForgeryToken]` + `SameOriginRequestValidator.IsValid` — same double-guard as SetVisibility |
| localStorage poisoning | Tampering | Server never trusts localStorage directly; only form-submitted values reach the request DTO |
| Selection JSON injection via re-upload | Tampering | `PacketAllowedNames` gate + try/catch JSON deserialization + empty-list fallback |

---

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — column migration pattern, SQL constants, ReadRow ordinals, FormatVisibility helper
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — `ContentSiteIndexRow` current property set
- `DeckFlow.Web/Services/ContentKbRelevanceService.cs` — interface, scoring constants, SelectTopClips, EstimateRenderedChars, ScoreArtifact
- `DeckFlow.Web/Services/PacketArtifactStore.cs` — PacketAllowedNames, BuildZip signature, LoadFromZip body, ReadEntries behavior
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — replay-first guard (lines 534-547), GetMergedClips call site (line 664), DeckAnalysisPacketResult record
- `DeckFlow.Web/Models/DeckAnalysisRequest.cs` — all existing fields, backing-field pattern, list-field convention
- `DeckFlow.Web/Models/ContentKbExcerpt.cs` — `{ get; init; }` constraint comment
- `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` — SetVisibility POST pattern, CSRF guards, TempData banner
- `DeckFlow.Web/Controllers/ContentKbController.cs` — browse Index action
- `DeckFlow.Web/Views/ContentKb/Index.cshtml` — card markup, hub-grid, data attributes, script wiring
- `DeckFlow.Web/Views/Deck/_ContentKbPanel.cshtml` — current model type, clip loop structure
- `DeckFlow.Web/wwwroot/ts/content-kb.ts` — filter module pattern
- `DeckFlow.Web/wwwroot/ts/df-typeahead.ts` — `window.DeckFlow` namespace exposure, IIFE pattern
- `DeckFlow.Web/wwwroot/ts/site.ts` — localStorage usage confirmation
- `DeckFlow.Web/tsconfig.json` — `module: "none"`, strict, target es2017
- `DeckFlow.Web.Tests/PacketArtifactStoreTests.cs` — existing zip round-trip test patterns
- `DeckFlow.Web.Tests/ContentKbRelevanceServiceTests.cs` — TrackingContentSiteIndexStore fake, CreateRow helper, test structure
- `.planning/config.json` — `nyquist_validation: false` confirmed

### Secondary (MEDIUM confidence)
- `DeckFlow.Web/Services/ContentKbSeedLoader.cs` — `BuildRow` helper confirms which `ContentSiteIndexRow` fields the seed populates; `IsEvergreen` defaults to false (not in seed JSON)
- `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` — confirms `ExpertContextClips` property name on the view model
- `DeckFlow.Web/Services/ContentKbSeedLoader.cs` — seed JSON does not include IsEvergreen; new column defaults to false for all seeded rows without seed changes

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all existing libraries confirmed in csproj
- Architecture: HIGH — all component surfaces read from live code, not assumed
- Pitfalls: HIGH — confirmed from code inspection (ordinal numbering, `ScoreArtifact` return-0 behavior, `{ get; init; }` comment in production code)
- Tier-fill design: HIGH — `ScoreArtifact` source read; gate logic confirmed at line 237 (`dimensionsHit >= 2 ? score : 0d`)
- UI patterns: HIGH — all 17 TS files inspected; browse view markup confirmed; tsconfig confirmed
- Zip allowlist: HIGH — `ReadEntries` throws confirmed at lines 601-605

**Research date:** 2026-06-07
**Valid until:** Stable (all claims are code-level, not ecosystem-version-dependent)
