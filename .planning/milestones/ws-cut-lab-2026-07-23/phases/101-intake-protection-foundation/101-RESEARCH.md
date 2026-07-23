# Phase 101: Intake & Protection Foundation - Research

**Researched:** 2026-07-18
**Domain:** Brownfield ASP.NET MVC feature addition — new "Cut Lab" tool page reusing DeckFlow's existing deck-input, parsing, and role-classification infrastructure
**Confidence:** HIGH (all findings grounded in this codebase; no external library research needed)

## Summary

Phase 101 is a pure brownfield integration phase: every piece of infrastructure it needs (deck parsing, Scryfall resolution, feature-flag gating, tool registration, form-state persistence, land/role classification) already exists in DeckFlow and has a proven pattern from a recent, directly analogous tool (`Deck History`, shipped 2026.07.6). The work is almost entirely "wire a new controller/view into the existing conventions," with one real design decision the planner must make explicitly: **how the multi-page Cut Lab working session (pool + locks + declared intent) persists across Phases 101-105**, because DeckFlow has no ASP.NET Session and two different existing conventions (hidden-field JSON round-trip vs. client `sessionStorage` form-state cache) solve different parts of that problem but neither was built for a 5-phase incremental workflow.

The single most important negative finding: **`IDeckEntryLoader.ValidateCommanderDeckSize` (`DeckFlow.Core/Loading/DeckEntryLoader.cs:159`) hard-rejects any deck that isn't exactly 100 cards** and is NOT the method Cut Lab should call. `ManabaseAnalysisService` already proves the alternative path — `LoadFromSourceAsync` (no size gate) plus its own `MaxDeckCards = 500` ceiling — is the correct precedent for INTAKE-01/02/03's 101-150 card range. Cut Lab needs its own validation (101-150 inclusive; ≤100 and >150 are the two "clear, actionable message" branches from INTAKE-03), not `ValidateCommanderDeckSize`.

**Primary recommendation:** Build Cut Lab as a new `CutLabController` + `Views/Deck/CutLab.cshtml` following the `DeckHistoryController`/`Views/Deck/DeckHistory.cshtml` template exactly (same feature-flag gate pattern, same `ToolRegistry` entry shape, same `data-cache-key` client persistence, same hidden round-trip field for resolved state), load the pool via `IDeckEntryLoader.LoadFromSourceAsync` (not `ValidateCommanderDeckSize`), classify locked "role groups" using the cheap `CardTypeLine`/type-line check for lands (not the full `PlanRoleClassifier` pipeline, which is Phase 102's job), and persist the declared intent + lock state in a single canonical JSON blob carried in a hidden form field (the `DeckHistoryRequest.HistoryJson` pattern), sized comfortably under the existing `RequestSizeLimit(2 MB)` convention.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Deck pool intake (URL/paste) | API/Backend (`CutLabController`) | Browser (form + `deck-input-store.ts`) | Reuses existing split-field pattern; server does the real parse/validate |
| Card count / legality validation | Core (`DeckFlow.Core`) + Backend service | — | `DeckEntryLoader` parses; a new Cut Lab-specific range check (101-150) lives in the Web service layer, mirroring `ManabaseAnalysisService.MaxDeckCards` |
| Deck intent declaration (plan/bracket/experience) | API/Backend (form POST) | Browser (`sessionStorage` field cache) | No DB row needed yet — same "hidden field + hidden hidden hidden" trick as Deck History; only Phase 104 (saved scenarios) needs real persistence |
| Card/package/role locking | API/Backend (state mutation on POST) | Browser (checkbox UI, optimistic toggle) | Lock state is small (card names + package labels), fits in the same round-trip JSON blob as the pool |
| Role/land classification for bulk-lock | Core (`DeckFlow.Core.Manabase.CardTypeLine`) | Backend (Scryfall resolution) | Cheap type-line check, not the full `PlanRoleClassifier` heuristic (that's Phase 102's SLOT-01 concern) |
| Format-legality / banlist summary | Backend (`ICommanderBanListService`) | — | Existing service, cached 6h, already used by `DeckAnalysisPacketService` |
| Session/working-state persistence | Backend (hidden-field JSON round-trip) | Browser (`sessionStorage` restore convenience only) | No ASP.NET Session registered; DB persistence is deferred to Phase 104 (GOAL-02 saved scenarios) |
| Theme-consistent UI | Browser (CSS) | — | Layout in `site-common.css`; new component tokens in each `:root` per theme file |

## Standard Stack

No new external packages are needed — this phase is 100% internal reuse.

### Core (existing, reused)
| Component | Location | Purpose | Why Standard |
|-----------|----------|---------|--------------|
| `IDeckEntryLoader.LoadFromSourceAsync` | `DeckFlow.Core/Loading/DeckEntryLoader.cs:113` | Parses Moxfield/Archidekt URL or pasted text, auto-detecting platform, no size gate | Same method `ManabaseAnalysisService` uses for its 500-card ceiling; proven pattern for non-100-card decks |
| `MoxfieldParser` / `ArchidektParser` | `DeckFlow.Core/Parsing/*.cs` | Text export parsing | Only parsers in the codebase |
| `DeckEntry` record | `DeckFlow.Core/Models/DeckEntry.cs` | Canonical card-entry model (Name, Quantity, Board, SetCode, CollectorNumber, Category, IsFoil) | Single source of truth consumed by every deck tool |
| `ICommanderBanListService` | `DeckFlow.Web/Services/CommanderBanListService.cs:14` | Official banned-card list (mtgcommander.net, 6h cache) | Existing legality-summary source; reused by `DeckAnalysisPacketService` |
| `CardTypeLine.FrontFace` / land-type check pattern | `DeckFlow.Core/Manabase/CardTypeLine.cs`, `ManabaseClassifier.IsLandType` (private, line 1393) | Front-face type-line parsing, MDFC-aware | `IsLandType` itself is `private` in `ManabaseClassifier` — Cut Lab needs its own thin public wrapper (or the planner should promote it to a shared public helper) rather than duplicating the MDFC-aware split logic |
| `CardFact` | `DeckFlow.Core/Manabase/CardFact.cs` | Minimal Scryfall-shaped per-card fact (TypeLine, OracleText, ManaValue, ProducedMana, ...) | Existing Scryfall-adapter shape; Web layer already knows how to fill it |
| `DeckInputSource` enum + `DeckInputReconciler` | `DeckFlow.Web/Models/DeckInputSource.cs`, `DeckInputReconciler.cs` | URL-vs-paste split field reconciliation | Exact mechanism every existing tool uses |

### Supporting (existing infra to wire into, not build)
| Component | Location | Purpose | When to Use |
|-----------|----------|---------|-------------|
| `FeatureFlagGateAttribute` | `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` | 404s the action when the tool flag is off | Apply to every Cut Lab controller action, same as `[FeatureFlagGate("tool.deck-history.enabled")]` |
| `ToolRegistry` | `DeckFlow.Web/Services/Tools/ToolRegistry.cs` | Single source of truth for nav/home-tile rendering | Add one `Create(...)` entry; a new `ToolNavSection.Build` fits ("oversized pool → 100-card build") |
| `FeatureFlagCatalog` + `FeatureFlagStore` seed SQL | `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs`, `FeatureFlagStore.cs` (Postgres block ~line 244, SQLite block ~line 294) | Human-readable description + seeded-OFF row | Every new tool flag needs a matching Postgres AND SQLite `INSERT` line plus a `Descriptions` entry (guarded by `FeatureFlagCatalogTests`) |
| `deck-input-store.ts` | `DeckFlow.Web/wwwroot/ts/deck-input-store.ts` | Restores last URL/paste input across tool switches via `sessionStorage` (`deckflow.last-deck` key) | Loads automatically if the form has `select[name="DeckInputSource"]` + `input[name="DeckUrl"]`/`textarea[name="DeckText"]` — no extra wiring needed, but see Pitfall 1 |
| `deck-sync.ts` generic form-state cache | `DeckFlow.Web/wwwroot/ts/deck-sync.ts:505-563` | `sessionStorage`-backed cache of ALL named form fields, keyed `decksync-form-state-<data-cache-key>` | Add `data-cache-key="cut-lab"` to the form; this is the "restores on reload" mechanism `deck-history` uses (test asserts `data-cache-key="deck-history"`) |
| `_DeckToolTabs.cshtml` | `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` | Renders the tool nav dropdown grouped by `ToolNavSection`, using `ToolRegistry` + `IFeatureFlagCache` | Include via `@await Html.PartialAsync("_DeckToolTabs", Model.ActiveTab)` |
| `_WorkflowStepTabs.cshtml` | `DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml` | ARIA tablist for multi-step in-page flows | Candidate for Cut Lab's intake→declare→lock steps within Phase 101 itself (all three fit on one page/step sequence) |
| `DeckPageTab` enum | `DeckFlow.Web/Models/DeckPageTab.cs` | Tab identity for tool nav highlighting | Add a `CutLab = 17` member |
| `RequestSizeLimit` attribute convention | `DeckHistoryController.cs:36` (2 MB), `DeckPacketController.cs` (11 MB) | Caps POST body size per action | Use 2 MB (matches Deck History's data shape: card list + locks + intent, no file upload needed for Phase 101) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hidden-field JSON round-trip for working state | ASP.NET `ISession` (distributed session) | Not registered anywhere in `Program.cs`; would be a new architectural pattern requiring a session store (Redis/SQL) DeckFlow doesn't have — rejected unless the planner explicitly wants to introduce it |
| Hidden-field JSON round-trip for working state | New SQLite/Postgres table (like `FeedbackStore`) | Real persistence, needed eventually for Phase 104's saved scenarios, but overkill for Phase 101's single-session intake+lock flow — recommend deferring the DB table to Phase 104 and reusing hidden-field round-trip here |
| `ManabaseClassifier.IsLandType` (private) | Duplicate the MDFC-aware `TypeLine.Contains("Land")` check inline | Duplication risk; recommend the planner either promote `IsLandType` to `public` on `CardTypeLine` or add a small new public helper in Core reused by both Manabase and Cut Lab |
| Full `PlanRoleClassifier` for LOCK-03 role groups | Type-line-only land detection | `PlanRoleClassifier` needs category tags + Commander Spellbook combo data (I/O-heavy); Phase 101 only needs "is this card a land" for the one example role group in the success criteria — full slot/role classification is explicitly Phase 102's SLOT-01 scope |

**Installation:** None — no new packages.

**Version verification:** Not applicable (no new dependencies). Existing stack versions confirmed via root `CLAUDE.md` and `.csproj` files: .NET 10, ASP.NET Core MVC 10.0, RestSharp 114.0.0, Polly 8.x, Microsoft.Data.Sqlite 10.0.0, Npgsql 10.0.0.

## Package Legitimacy Audit

Not applicable — this phase introduces zero new npm/NuGet packages. All functionality is built on existing in-repo services.

## Architecture Patterns

### System Architecture Diagram

```
Browser (Cut Lab form)
  |
  |  GET /cut-lab            -> renders empty intake form (feature-flag gated)
  |  POST /cut-lab           -> submit pool (URL or paste) + intent fields
  v
CutLabController (new, mirrors DeckHistoryController)
  |
  |-- 1. IDeckEntryLoader.LoadFromSourceAsync(deckSource)          [Core, no size gate]
  |        -> throws DeckParseException on bad input -> 400-style user message
  |
  |-- 2. Cut Lab pool-size validation (NEW, Web-layer)
  |        count <= 100            -> INTAKE-03 "already at/below 100" message
  |        count > 150             -> INTAKE-03 "exceeds supported cap" message
  |        101 <= count <= 150     -> proceed
  |
  |-- 3. Scryfall batch resolution (reuse ManabaseAnalysisService's
  |        ResolveCardsAsync pattern: batches of 75 via IScryfallCardLookupService,
  |        MaxDeckCards-style ceiling already proven safe up to 500)
  |
  |-- 4. ICommanderBanListService.GetBannedCardsAsync()             [format-legality summary]
  |
  |-- 5. Commander auto-lock (always-locked, non-removable) +
  |        land role-group detection via CardTypeLine front-face check
  |
  |-- 6. Serialize {pool, resolvedFacts, locks, declaredIntent} to
  |        canonical JSON -> hidden field (CutLabStateJson), mirrors
  |        DeckHistoryRequest.HistoryJson
  v
Views/Deck/CutLab.cshtml
  |-- renders card count + legality summary (INTAKE-01)
  |-- renders intent form: primary plan / secondary plan / bracket / experience (INTAKE-02)
  |-- renders lock UI: per-card checkbox, named-package grouping, bulk "lock all lands" (LOCK-01/02/03)
  |-- form POSTs back to /cut-lab with CutLabStateJson round-tripped + updated locks
```

### Recommended Project Structure
```
DeckFlow.Web/
├── Controllers/
│   └── CutLabController.cs                # mirrors DeckHistoryController.cs shape
├── Models/
│   ├── CutLabRequest.cs                    # mirrors ManabaseRequest.cs (DeckInputSource, DeckUrl, DeckText, ...)
│   ├── CutLabViewModel.cs                  # mirrors DeckHistoryViewModel.cs
│   └── CutLabPageTab addition to DeckPageTab.cs
├── Services/
│   └── CutLab/
│       ├── CutLabPageService.cs            # orchestrates load -> validate -> resolve -> classify -> persist
│       ├── CutLabPoolValidator.cs          # NEW: 101-150 range check (Web-layer, mirrors MaxDeckCards pattern)
│       └── CutLabStateSerializer.cs        # mirrors DeckHistorySerializer.cs (JSON round-trip + size cap)
├── Views/Deck/
│   └── CutLab.cshtml                       # mirrors DeckHistory.cshtml / Manabase.cshtml split-input block
└── wwwroot/ts/
    └── cut-lab.ts                          # lock/unlock checkbox interactions, package grouping UI
DeckFlow.Core/
└── Loading/ (no changes needed — LoadFromSourceAsync already fits)
```

### Pattern 1: Deck-input split-field surface (URL vs. paste toggle)
**What:** A `<select name="DeckInputSource">` (values `PublicUrl`/`PasteText`) paired with a URL `<input>` and a paste `<textarea>`, both wrapped in `.field` divs toggled by `data-df-select`/`data-sync-panel`, restored via `deck-input-store.ts` and cached via `deck-sync.ts`'s generic form-state cache.
**When to use:** Every new deck-intake tool. This is the standard, not a choice.
**Example (from Manabase.cshtml:38-57):**
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
For Cut Lab, use `cut-lab-input-source`, `cut-lab-deck-url`, `cut-lab-deck-text` ids so the generic client scripts (which query by `name`, not `id`) still work unchanged.

### Pattern 2: New-tool controller shape (feature-flag gated, GET+POST, no exact-size validation)
**What:** A controller with a `[HttpGet]` empty-form action and a `[HttpPost][ValidateAntiForgeryToken][RequestSizeLimit(...)]` processing action, both `[FeatureFlagGate("tool.cut-lab.enabled")]`.
**When to use:** Any brand-new tool page.
**Example (from DeckHistoryController.cs:26-72, condensed):**
```csharp
[HttpGet("/cut-lab")]
[FeatureFlagGate("tool.cut-lab.enabled")]
public IActionResult Index() => CutLabView(new CutLabRequest(), null);

[HttpPost("/cut-lab")]
[FeatureFlagGate("tool.cut-lab.enabled")]
[ValidateAntiForgeryToken]
[RequestSizeLimit(2 * 1024 * 1024)]
public async Task<IActionResult> Process(CutLabRequest request)
{
    request ??= new CutLabRequest();
    try
    {
        var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
        return View("CutLab", CutLabViewModel.From(request, result));
    }
    catch (InvalidOperationException exception)  // pool-size / parse errors surface as user messages
    {
        return CutLabView(request, error: exception.Message);
    }
    catch (OperationCanceledException)
    {
        return CutLabView(request, error: "The request timed out. Try again.");
    }
}
```

### Pattern 3: Deck loading WITHOUT the exact-100 gate
**What:** Call `IDeckEntryLoader.LoadFromSourceAsync`, never `ValidateCommanderDeckSize` (which throws unless the count is exactly `requiredDeckSize`, default 100).
**When to use:** Any tool that must accept a non-100-card pool. Cut Lab, like Manabase, is one.
**Example (from ManabaseAnalysisService.cs:665-696):**
```csharp
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

if (deckCards.Count > MaxDeckCards)  // MaxDeckCards = 500 for Manabase
{
    throw new InvalidOperationException($"That deck has too many cards to analyze (limit {MaxDeckCards}).");
}
```
Cut Lab's equivalent check is a *range*, not a ceiling: reject `count <= 100` (INTAKE-03 "already at/below 100") and `count > 150` (INTAKE-03 "exceeds cap") as two distinct, differently-worded messages — do not reuse `ValidateCommanderDeckSize`'s single "must be exactly N" exception text.

### Pattern 4: Cheap land/type-line classification (for LOCK-03, NOT full role classification)
**What:** `CardTypeLine.FrontFace(typeLine)` then a `Contains("Land", StringComparison.OrdinalIgnoreCase)` check, MDFC-aware (front-face only, so a spell//land MDFC is correctly NOT a land while a land front-face is).
**When to use:** LOCK-03's "bulk-lock all lands" example role group. Do NOT reach for `PlanRoleClassifier` here — that requires category tags + Commander Spellbook combo data and is scoped to Phase 102 (SLOT-01 functional-slot competition).
**Example (from ManabaseClassifier.cs:1393-1397, currently `private` — planner should decide whether to promote it or duplicate the one-line check):**
```csharp
private static bool IsLandType(string typeLine)
{
    // Use the front face only (before "//") so MDFC spell-fronts aren't treated as lands.
    return IsType(CardTypeLine.FrontFace(typeLine), "Land");
}
```

### Pattern 5: Working-state round-trip via hidden field (session persistence without ASP.NET Session)
**What:** A serialized JSON blob (pool + resolved facts + locks + intent) stored in `<input type="hidden" name="CutLabStateJson" value="@Model.StateJson" />`, re-parsed on every POST, matching `DeckHistoryRequest.HistoryJson` (`DeckHistory.cshtml:60,196`).
**When to use:** "Declaration persists with the working session" (INTAKE-02) — DeckFlow has no `ISession`/distributed cache registered (verified: no `AddSession`/`ISession` usage anywhere in `Program.cs` or controllers), so this hidden-field round-trip IS the established "session" mechanism.
**Example (from DeckHistorySerializer.cs, pattern only — Cut Lab needs its own serializer):**
```csharp
public const int MaxUploadBytes = 1_048_576;  // DeckHistorySerializer's cap; Cut Lab should set its own similarly

if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxUploadBytes)
{
    // reject with a user-facing message
}
```

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| URL/paste toggle UI + restore-on-reload | A new client-side form-persistence script | `deck-input-store.ts` (auto-attaches to any form with `select[name="DeckInputSource"]`) + `deck-sync.ts`'s `data-cache-key` mechanism | Both already handle every existing tool; adding `data-cache-key="cut-lab"` and matching field `name`s is all that's needed |
| Moxfield/Archidekt parsing | A new parser | `MoxfieldParser` / `ArchidektParser` via `IDeckEntryLoader.LoadFromSourceAsync` | Already handles URL-vs-paste auto-detection, MTG Arena export fallback, maybeboard exclusion |
| Commander banlist lookup | A new scraper | `ICommanderBanListService` (mtgcommander.net, 6h cache) | Already the canonical source per project memory `reference_commander_banned_list.md` |
| Scryfall card data resolution | A new batch client | `IScryfallCardLookupService` batch pattern (`ScryfallBatchSize = 75`) used by `ManabaseAnalysisService.ResolveCardsAsync` | Handles collector-number fallback, throttling via `ScryfallThrottle`, and proven up to 500 cards |
| Land/type detection | A new Oracle-text/type parser | `CardTypeLine.FrontFace` + `Contains("Land")` | MDFC-aware; the exact logic `ManabaseClassifier` already uses for its land-slot accounting |
| Feature flag gating for the new route | A custom middleware check | `[FeatureFlagGate("tool.cut-lab.enabled")]` | Standard 404-on-off pattern used by every tool controller |

**Key insight:** Nothing about intake, parsing, legality-checking, or land detection is new engineering in this phase — the entire job is composing existing DeckFlow primitives behind a new route, plus the two genuinely new pieces: (1) a 101-150 range validator (a ~10-line method, not reusing `ValidateCommanderDeckSize`), and (2) lock/package state modeling (new, small domain model).

## Common Pitfalls

### Pitfall 1: `deck-input-store.ts` restores the `<select>` value without firing a `change` event
**What goes wrong:** On page load, if a prior deck was cached in `sessionStorage`, `restoreSplitFields` (`deck-input-store.ts:121-151`) sets `inputSelect.value = stored.inputSource` directly (line 147) but never dispatches a `change` event on that `<select>`. Any script listening for `change` to toggle the URL/paste panel visibility (`data-sync-panel` show/hide) will not react, leaving the visible panel out of sync with the actual restored value (e.g., text was restored but the URL panel is still shown).
**Why it happens:** `dispatchInputEvent` (line 117) is only called from the "Clear" button handler, never from the restore path itself.
**How to avoid:** This is a KNOWN, PRE-EXISTING bug (see project memory `followup_deck_input_store_restore_desync.md`) affecting ALL tools using the split-input surface — it is NOT specific to Cut Lab and is explicitly OUT OF SCOPE to fix in this phase. Flag it for the plan-checker/QA pass so nobody mistakes Cut Lab's intake form for newly-broken; do not attempt a fix here (a dedicated branch is queued post-ship per memory).
**Warning signs:** On Cut Lab's e2e smoke test, after a page reload with a cached deck, the visible input panel doesn't match the restored value in the `<select>`.

### Pitfall 2: Reusing `IDeckEntryLoader.ValidateCommanderDeckSize` (exact-100 gate) instead of a custom range check
**What goes wrong:** `ValidateCommanderDeckSize` throws `InvalidOperationException` for ANY count that isn't exactly `requiredDeckSize` (default 100). If a task mistakenly calls this method against a 130-card pool, INTAKE-01/02 will always fail.
**Why it happens:** It's the only "deck size validation" method visible on `IDeckEntryLoader`, so it's an easy trap to reach for by pattern-matching `DeckSyncService`'s usage.
**How to avoid:** Never call `ValidateCommanderDeckSize` in Cut Lab. Write a dedicated Cut Lab pool-size check (101-150 inclusive) in the Web service layer, following `ManabaseAnalysisService.MaxDeckCards`'s pattern of a Web-layer ceiling check, not a Core-layer exact-match.
**Warning signs:** Any exception message reading "must contain exactly 100 cards" surfacing on the Cut Lab page.

### Pitfall 3: Hardcoded tool-count assertions in `ToolRegistryTests`
**What goes wrong:** `DeckFlow.Web.Tests/Tools/ToolRegistryTests.cs:43-47` asserts exact counts (`Assert.Equal(15, registry.All.Count)`, unique-route count `21`, etc.). Adding Cut Lab to `ToolRegistry` without updating this test will fail CI's `format-gate`/build, but more importantly the assertion list (`All_ReturnsExpectedToolDefinitions`, line 21-35) enumerates every tool by hand — a new tool must be added there too, in registration order.
**Why it happens:** Easy to miss because the failure is in an unrelated-looking test file, not obviously coupled to `ToolRegistry.cs`.
**How to avoid:** When adding the `cut-lab` entry to `ToolRegistry.Definitions`, update both assertions in `ToolRegistryTests.cs` in the same commit.
**Warning signs:** `dotnet test` failures in `ToolRegistryTests` after adding the new tool.

### Pitfall 4: Feature flag seeded in only one of Postgres/SQLite blocks
**What goes wrong:** `FeatureFlagStore.cs` has two separate seed SQL blocks (`PostgresSeedSql` ~line 196-245, `SqliteSeedSql` ~line 248-296) that must both list the new `tool.cut-lab.enabled` row (seeded `FALSE`/`0`). Missing one means the flag exists on one deployment backend but not the other, and `FeatureFlagCatalogTests` (which cross-checks `Descriptions` against seeded keys) may not catch a Postgres-only or SQLite-only omission depending on which backend the test DB uses.
**Why it happens:** Two near-identical blocks invite a copy-paste miss.
**How to avoid:** Add the row to BOTH blocks and to `FeatureFlagCatalog.Descriptions` in the same edit; run `FeatureFlagCatalogTests`, `FeatureFlagStoreSeedTests`, and `FeatureFlagStoreMigrationTests` before committing.

### Pitfall 5: 512 MB Render web-tier RAM cap with a 101-150 card Scryfall resolution
**What goes wrong:** Resolving 150 cards' worth of full `CardFact` (oracle text, type line, produced mana) per Cut Lab session, kept alive in a hidden-field JSON blob that round-trips on every lock toggle, adds per-request payload and per-request JSON parse/serialize cost. This is well within Manabase's already-proven 500-card ceiling on the same 512 MB tier, but the *shape* differs: Manabase re-resolves via Scryfall on every POST (no round-trip cache), while Cut Lab round-tripping resolved facts avoids re-fetching Scryfall on every lock click at the cost of a larger POST body.
**Why it happens:** Not a new risk exactly, but the tradeoff (fewer Scryfall calls vs. larger POST bodies) hasn't been made in any existing tool at this specific shape (150 cards × repeated small-interaction POSTs like a single lock toggle).
**How to avoid:** Estimate the hidden-field JSON size before committing to full round-trip: a `CardFact`-shaped record with full oracle text per card is roughly 300-700 bytes; 150 cards puts the state blob comfortably under 150 KB, well inside the existing 2 MB `RequestSizeLimit` convention and 1 MB `DeckHistorySerializer.MaxUploadBytes` precedent. If the planner decides to strip oracle text from the round-tripped blob (keeping only name/type-line/mana-value needed for locking and land detection) the size drops further. Flag this as a sizing decision for the plan, not a research-open blocker — the numbers work either way.
**Warning signs:** None expected at 150 cards, but worth a `RequestSizeLimit` value decision in the plan rather than defaulting to the DeckHistory tool's 2 MB blindly if oracle text bloats it.

### Pitfall 6: Public repo — no secrets, no committed compiled TS
**What goes wrong:** Standard project hazard, reiterated because Cut Lab adds a new `wwwroot/ts/cut-lab.ts` source file. Compiled `wwwroot/js/cut-lab.js` output must NOT be committed (gitignored per root `CLAUDE.md`; Docker build recompiles at deploy).
**How to avoid:** Confirm `.gitignore` still covers the new compiled file (it does, via the existing `DeckFlow.Web/wwwroot/js/*.js` glob) — no action needed, just don't force-add it.

## Code Examples

### GET/POST controller pair with feature-flag gate and error handling
```csharp
// Source: DeckFlow.Web/Controllers/DeckHistoryController.cs:26-72
[HttpGet("/deck-history")]
[FeatureFlagGate("tool.deck-history.enabled")]
public IActionResult Index() => HistoryView(new DeckHistoryRequest(), null);

[HttpPost("/deck-history")]
[FeatureFlagGate("tool.deck-history.enabled")]
[ValidateAntiForgeryToken]
[RequestSizeLimit(2 * 1024 * 1024)]
public async Task<IActionResult> Process(IFormFile? historyFile, DeckHistoryRequest request)
{
    // ... load, catch OperationCanceledException + generic Exception, re-render with ErrorMessage
}
```

### Deck loading without a size gate (Manabase's proven up-to-500-card pattern)
```csharp
// Source: DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:641-696
private const int MaxDeckSourceChars = 100_000;
private const int MaxDeckCards = 500;
// ...
load = await _deckEntryLoader.LoadFromSourceAsync(deckSource, cancellationToken: cancellationToken)
    .ConfigureAwait(false);
// ...
if (deckCards.Count > MaxDeckCards)
{
    throw new InvalidOperationException($"That deck has too many cards to analyze (limit {MaxDeckCards}).");
}
```

### Tool registration (single source of truth for nav + home tiles + flag key)
```csharp
// Source: DeckFlow.Web/Services/Tools/ToolRegistry.cs:18 (Deck History entry, as a template)
Create("deck-history", "Deck History", "/deck-history", ToolNavSection.Build,
    "tool.deck-history.enabled", false, "Deck History",
    "Track your deck's evolution in a file you own — snapshot each change with a note, diff any two versions, and generate an AI prompt about how the deck has grown.",
    "deck-history", DeckPageTab.DeckHistory, true, "/deck-history/download"),
```

### Feature-flag seed row (must appear in BOTH SQL blocks + Descriptions)
```csharp
// Source: DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs:244 (Postgres) and :294 (SQLite)
('tool.deck-history.enabled', FALSE)   // Postgres block
('tool.deck-history.enabled', 0)       // SQLite block
```
```csharp
// Source: DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs:43-44
["tool.deck-history.enabled"] =
    "Deck History tool: version a deck into a downloadable snapshot-history JSON file with notes, pair diffs, and an evolution prompt.",
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `DeckSyncService`'s exact-100 `ValidateCommanderDeckSize` gate | `ManabaseAnalysisService`'s `LoadFromSourceAsync` + `MaxDeckCards` ceiling (no exact-match) | Established by Phase ~11 (Manabase) and reconfirmed by every subsequent non-sync tool | Cut Lab is the first tool needing a *range* (101-150) rather than either an exact match or an open ceiling — new but small logic, not a new pattern family |
| Ad-hoc per-tool client persistence | Shared `deck-input-store.ts` (URL/text restore) + `deck-sync.ts` generic `data-cache-key` form cache | Consolidated before Deck History shipped (2026.07.6) | Cut Lab gets restore-on-reload "for free" by following the `name`/`data-cache-key` conventions |
| N/A | Feature-flag-gated tool rollout via `[FeatureFlagGate]` + `ToolRegistry` + seeded-OFF flag | Standard since ~Phase 6 (FLAG-05) | Cut Lab ships dark (`tool.cut-lab.enabled` seeded OFF) exactly like every other tool in-progress |

**Deprecated/outdated:** None relevant — Cut Lab is new, not replacing an existing tool.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | "Declaration persists with the working session" (INTAKE-02) is best satisfied by the hidden-field JSON round-trip pattern (`DeckHistoryRequest.HistoryJson` precedent), not a new DB table or ASP.NET Session | Architecture Patterns (Pattern 5), Architectural Responsibility Map | If the product intent for "session" actually means surviving a browser close/reopen or a multi-day return visit (not just page-to-page within one sitting), a DB-backed draft-save may be needed even in Phase 101, not deferred to Phase 104. This is a product-scope question, not purely technical — flag for `/gsd:discuss-phase` or explicit confirmation in planning. |
| A2 | LOCK-03's "bulk-lock a whole role group (e.g. all lands)" only needs type-line-based land detection in Phase 101, with fuller role-group bulk-locking (ramp, draw, interaction, etc.) deferred to reuse Phase 102's SLOT-01 role/category inference once it exists | Don't Hand-Roll, Pattern 4 | If the phase's actual UI needs "bulk-lock all ramp" or "bulk-lock all interaction" working in Phase 101 (not just "lands" as the literal example), the full `PlanRoleClassifier` I/O (category tags + Commander Spellbook lookup) would need to be pulled forward from Phase 102, adding real scope. The ROADMAP.md phase description says "e.g. all lands" (singular concrete example) and defers "functional slot competition" explicitly to Phase 102, supporting the narrower reading, but the planner should confirm. |
| A3 | `ManabaseClassifier.IsLandType`'s private visibility should be changed (promoted to public on `CardTypeLine` or duplicated as a 3-line Cut Lab-local helper) rather than reflection/duplication of the full MDFC-aware logic | Standard Stack, Alternatives Considered | Low risk either way — it's a ~3 line method: `CardTypeLine.FrontFace(typeLine).Contains("Land", StringComparison.OrdinalIgnoreCase)`. Worth a one-line plan decision, not a blocker. |
| A4 | `ToolNavSection.Build` is the correct section for Cut Lab (grouping with deck-history, deck-primer, deck-sync, convert) rather than `Analyze` | Standard Stack (Supporting) | Cosmetic only — wrong section groups the nav link differently but doesn't affect functionality. Low risk. |

**If this table is empty:** N/A — see above.

## Open Questions (RESOLVED)

> Both open questions were resolved as explicit planning decisions during Phase 101 planning (2026-07-19). See the inline `RESOLVED:` notes below and the referenced plan files.

1. **Exact multi-page state-carry mechanism across Phases 101→105** — **RESOLVED (101-02-PLAN.md):** Option (a) — a single serializable `CutLabState` envelope carried as a hidden-field JSON round-trip (`CutLabStateJson`), no ASP.NET Session, no DB this phase. Phases 102+ consume and re-emit the same envelope; DB-backed saved scenarios are deferred to Phase 104 (GOAL-02).
   - What we know: Phase 101 alone can be satisfied with a single-page hidden-field JSON round-trip (matches `DeckHistoryRequest.HistoryJson`). Phase 104 (GOAL-02) explicitly needs "save and reload named scenarios," which strongly implies real DB persistence (a new table, following the `FeedbackStore`/`CategoryKnowledgeStore` + `IRelationalDialect` pattern) by Phase 104 at the latest.
   - What's unclear: Whether Phase 101's locks/intent need to be *retrievable* by Phase 102/103 pages via a stable identifier (e.g., a session/draft ID passed in the URL) or whether the whole pool+locks blob is expected to keep round-tripping through every subsequent phase's forms as one growing hidden field.
   - Recommendation: The Phase 101 plan should pick ONE of: (a) pure hidden-field round-trip that Phase 102 also consumes and re-emits (matches existing zero-DB convention, but couples every future phase's form to carry an ever-growing blob), or (b) introduce a lightweight server-side draft store now (even just an `IMemoryCache`-backed session envelope keyed by a GUID in the URL, given the existing `IMemoryCache` DI registration) so Phases 102+ don't have to round-trip the same growing state through hidden fields repeatedly. This is squarely a planning decision, not something research can resolve alone — recommend raising it explicitly in the Phase 101 plan's design section.

2. **Where does the commander get identified when the pool is submitted as a plain paste (no explicit `Commander` board header)?** — **RESOLVED (101-03-PLAN.md):** Reuse the `ReflagInferredCommanders` leading-card inference heuristic for the common case, with the `CommanderSelectionRequired` fallback UI (Manabase pattern, `SelectedCommander`) for the ambiguous case; whichever commander is resolved is unconditionally force-locked server-side via `CutLabLockRules.EnforceCommanderLock` on every POST.
   - What we know: `ManabaseAnalysisService.ReflagInferredCommanders` (referenced at `ManabaseAnalysisService.cs:681`) already solves "Moxfield plaintext exports carry no Commander section header — the commander is simply the leading card," reflagging it to the commander board.
   - What's unclear: Whether Cut Lab should reuse this exact heuristic (it's Manabase-specific naming but generically applicable) or whether Cut Lab needs its own commander-selection UI (like Manabase's `CommanderSelectionRequired` picker, `Manabase.cshtml:87-123`) given the auto-lock requirement (LOCK-01: "the commander is always auto-locked and cannot be unlocked" — a wrong auto-detected commander would auto-lock the wrong card).
   - Recommendation: Reuse both: the `ReflagInferredCommanders`-style heuristic for the common case, AND the `CommanderSelectionRequired` fallback UI pattern for the ambiguous case, since LOCK-01's correctness depends on getting the commander right.

## Environment Availability

Not applicable — no new external tools, runtimes, or services. All dependencies (Scryfall API, mtgcommander.net banlist, SQLite/Postgres) are already integrated and available per root `CLAUDE.md`/existing `Program.cs` wiring.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`.NET`), `@playwright/test` ^1.60.0 (e2e), Vitest ^3.2.7 (TS unit, per `DeckFlow.Web/package.json`) |
| Config file | `DeckFlow.Web/playwright.config.ts`; xUnit via `.csproj` (no separate config) |
| Quick run command | `dotnet build` (WSL clean build is the reliability baseline per root CLAUDE.md — VSTest is flagged unreliable in WSL) |
| Full suite command | `dotnet test DeckFlow.Web.Tests` + `dotnet test DeckFlow.Core.Tests` + `npx playwright test <new-spec>.spec.ts` (run via `scripts/run-web-test.sh` per root CLAUDE.md UI-testing constraint — never open a browser on the Windows host) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| INTAKE-01 | Submit 101-150 card pool via URL/paste, see card count + legality summary | unit + e2e | `dotnet test --filter CutLabPageServiceTests` / `npx playwright test cut-lab-smoke.spec.ts` | ❌ Wave 0 |
| INTAKE-02 | Declare primary/secondary plan, bracket, experience; persists with working session | unit + e2e | `dotnet test --filter CutLabRequestTests` / same e2e spec, reload-and-check-restored assertion | ❌ Wave 0 |
| INTAKE-03 | ≤100 or >150 card pool produces clear actionable message | unit | `dotnet test --filter CutLabPoolValidatorTests` | ❌ Wave 0 |
| LOCK-01 | Lock individual cards; commander always auto-locked, cannot unlock | unit + e2e | `dotnet test --filter CutLabLockStateTests` / e2e assertion that commander checkbox is `disabled`/`checked` | ❌ Wave 0 |
| LOCK-02 | Group cards into named packages, lock/unlock as a unit | unit | `dotnet test --filter CutLabPackageTests` | ❌ Wave 0 |
| LOCK-03 | Bulk-lock a role group (e.g. all lands) in one action | unit | `dotnet test --filter CutLabRoleGroupLockTests` (assert land-type detection via `CardTypeLine`) | ❌ Wave 0 |

Existing regression coverage that must also pass (guard against tool-registration drift): `ToolRegistryTests`, `FeatureFlagCatalogTests`, `FeatureFlagStoreSeedTests`, `SeoPathsTests` (only if Cut Lab is added to `SeoPaths.Indexable` in this phase — recommend deferring that addition until the tool is feature-complete, i.e. Phase 105).

### Sampling Rate
- **Per task commit:** `dotnet build` clean (WSL) + targeted `dotnet test --filter CutLab*`
- **Per wave merge:** Full `dotnet test` (both test projects) + new `cut-lab-*.spec.ts` e2e suite via `scripts/run-web-test.sh`
- **Phase gate:** Full suite green (unit + e2e across desktop/mobile viewports and at least 2-3 themes, per root CLAUDE.md UI-change convention) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `DeckFlow.Web.Tests/CutLabPageServiceTests.cs` — covers INTAKE-01, INTAKE-02
- [ ] `DeckFlow.Web.Tests/CutLabPoolValidatorTests.cs` — covers INTAKE-03 (both ≤100 and >150 branches)
- [ ] `DeckFlow.Web.Tests/CutLabLockStateTests.cs` — covers LOCK-01, LOCK-02
- [ ] `DeckFlow.Web.Tests/CutLabRoleGroupLockTests.cs` — covers LOCK-03
- [ ] `DeckFlow.Web/e2e/cut-lab-smoke.spec.ts` — full-flow e2e mirroring `deck-history-smoke.spec.ts` (feature-flag admin lock, theme×viewport screenshots per root CLAUDE.md UI convention)
- [ ] Framework install: none — xUnit/Playwright/Vitest already installed and configured

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Cut Lab is a public, anonymous tool like every other DeckFlow tool page (no user accounts) |
| V3 Session Management | Partial | No server session exists; the "session" is a client-carried hidden-field blob — CSRF protection via `[ValidateAntiForgeryToken]` (existing convention) is the relevant control, not session-fixation concerns |
| V4 Access Control | Yes | `[FeatureFlagGate("tool.cut-lab.enabled")]` gates the whole feature; `/Admin/*` remains behind `BasicAuthMiddleware` (unrelated but must not be weakened) |
| V5 Input Validation | Yes | `RequestSizeLimit` on POST actions; pool-size range validation (101-150) as both a UX and a light DoS-shape guard against absurdly large paste-text bodies; existing `MaxDeckSourceChars`-style cap (Manabase uses 100,000 chars) should be mirrored for Cut Lab's paste textarea |
| V6 Cryptography | No | No secrets or crypto operations in this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| CSRF on the Cut Lab POST endpoints | Spoofing/Tampering | `[ValidateAntiForgeryToken]` (standard on every mutating action in this codebase — `DeckHistoryController`, etc.) |
| Same-origin API abuse if Cut Lab adds any `/api/cut-lab/*` JSON endpoint later | Tampering | `SameOriginRequestValidator` (existing CSRF guard for API endpoints; apply if Phase 101 or later adds an API controller, not needed for a pure Razor form POST) |
| Oversized paste/hidden-field payload (memory pressure on 512 MB tier) | Denial of Service | `RequestSizeLimit` attribute + Cut Lab-specific `MaxDeckSourceChars`/pool-size range check, mirroring Manabase's and Deck History's existing caps |
| Tampered hidden-field `CutLabStateJson` (client could edit the DOM before submit, e.g. to unlock the commander) | Tampering | Server MUST re-derive/re-enforce "commander always locked, cannot be unlocked" on every POST server-side, never trust the client-submitted lock state for the commander card — re-apply the auto-lock rule after deserializing, before rendering or persisting |

## Sources

### Primary (HIGH confidence — direct codebase reads with file:line citations)
- `DeckFlow.Core/Loading/DeckEntryLoader.cs` — `LoadFromSourceAsync`, `ValidateCommanderDeckSize` (the critical negative finding)
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — proven 500-card-ceiling, no-exact-match load pattern; `ReflagInferredCommanders`
- `DeckFlow.Web/Controllers/DeckHistoryController.cs` + `DeckFlow.Web/Views/Deck/DeckHistory.cshtml` — most recently shipped analogous new-tool template
- `DeckFlow.Web/Services/Tools/ToolRegistry.cs` + `DeckFlow.Web.Tests/Tools/ToolRegistryTests.cs` — tool registration contract and its test-coupling pitfall
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` + `FeatureFlagStore.cs` — flag seeding convention (both SQL dialects)
- `DeckFlow.Web/wwwroot/ts/deck-input-store.ts` + `deck-sync.ts` — client persistence mechanisms, including the confirmed pre-existing restore-desync bug
- `DeckFlow.Core/Manabase/CardTypeLine.cs`, `ManabaseClassifier.cs`, `CardFact.cs`, `ManabaseModels.cs` (`PlanRole`) — role/land classification location and scope boundary
- `DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs` — confirms full role classification is Web-layer, I/O-heavy, and out of Phase 101 scope
- `DeckFlow.Web/Services/CommanderBanListService.cs` — legality/banlist source
- `DeckFlow.Web/e2e/deck-history-smoke.spec.ts` — e2e test template (feature-flag admin lock, theme×viewport screenshots)
- Root `CLAUDE.md` and `.planning/workstreams/cut-lab/{PROJECT,REQUIREMENTS,ROADMAP,STATE}.md` — constraints and requirement traceability

### Secondary (MEDIUM confidence)
- None — all findings verified directly against source files in this session.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new dependencies, all reuse verified by direct file reads
- Architecture: HIGH — controller/view/service shape directly modeled on a recently-shipped, structurally identical tool (Deck History)
- Pitfalls: HIGH — all six pitfalls are either confirmed via grep/read of actual source (exact-100 gate, hardcoded test counts, dual SQL seed blocks) or documented in project memory (restore-desync bug)
- Open questions: The multi-page state-carry question (Open Question 1) is a genuine product/architecture decision, not a research gap — flagged explicitly for the plan, not left ambiguous

**Research date:** 2026-07-18
**Valid until:** Stable — this is internal-codebase research with no external API/library version drift risk; re-verify only if `DeckEntryLoader`, `ManabaseAnalysisService`, or `ToolRegistry` are refactored before planning executes.
