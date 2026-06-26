---
phase: 66-admin-tool-visibility-toggles-tool-registry
reviewed: 2026-06-25T00:00:00Z
depth: deep
files_reviewed: 30
files_reviewed_list:
  - DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs
  - DeckFlow.Web/Services/Tools/ToolRegistry.cs
  - DeckFlow.Web/Services/Tools/ToolDefinition.cs
  - DeckFlow.Web/Services/Tools/ToolVisibility.cs
  - DeckFlow.Web/Services/Tools/ToolNavSection.cs
  - DeckFlow.Web/Services/Tools/ToolSection.cs
  - DeckFlow.Web/Services/Tools/IToolRegistry.cs
  - DeckFlow.Web/Controllers/Admin/AdminToolsController.cs
  - DeckFlow.Web/Controllers/CommanderController.cs
  - DeckFlow.Web/Controllers/DeckCategoriesController.cs
  - DeckFlow.Web/Controllers/DeckConvertController.cs
  - DeckFlow.Web/Controllers/DeckLookupController.cs
  - DeckFlow.Web/Controllers/DeckPacketController.cs
  - DeckFlow.Web/Controllers/DeckPrimerController.cs
  - DeckFlow.Web/Controllers/DeckSyncController.cs
  - DeckFlow.Web/Controllers/JudgeQuestionsController.cs
  - DeckFlow.Web/Controllers/ManabaseController.cs
  - DeckFlow.Web/Controllers/ContentKbController.cs
  - DeckFlow.Web/Controllers/HelpController.cs
  - DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs
  - DeckFlow.Web/Controllers/Api/AnalysisPromptApiController.cs
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
  - DeckFlow.Web/Extensions/ToolsServiceCollectionExtensions.cs
  - DeckFlow.Web/Views/AdminTools/Index.cshtml
  - DeckFlow.Web/Views/Deck/Home.cshtml
  - DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml
  - DeckFlow.Web/Views/Shared/_ToolTileIcon.cshtml
  - DeckFlow.Web.Tests/Tools/ToolRouteGateCoverageTests.cs
  - DeckFlow.Web.Tests/AdminToolsControllerTests.cs
  - DeckFlow.Web.Tests/FeatureFlagGateAttributeTests.cs
findings:
  critical: 0
  high: 1
  medium: 3
  low: 1
  info: 2
  total: 7
status: issues_found
---

# Phase 66: Code Review Report

**Reviewed:** 2026-06-25
**Depth:** deep (cross-file, route-graph + reflection-test analysis)
**Files Reviewed:** 30
**Status:** issues_found

## Summary

Phase 66 adds a tool registry, per-action `[FeatureFlagGate]` 404 kill-switches, a
registry-driven nav/home/help surface, and an `/Admin/Tools` operator page. The
implementation is largely solid and security-conscious:

- Every MVC action under every tool landing route now carries the matching gate
  (verified by hand against all 10 tool controllers — GET + every POST/download/upload
  sibling, including the previously-ungated `/suggest-categories/card-search`).
- `FeatureFlagGateAttribute` correctly resolves the cache per-invocation and returns a
  bare 404 when off (no information leak, no stale-snapshot capture).
- All 10 new `tool.*` flags are seeded default-ON in **both** the Postgres (`TRUE`) and
  SQLite (`1`) branches, the 3 existing keys (`feature.categories.enabled`,
  `content.kb.enabled`, `feature.manabase.enabled`) are reused not duplicated, and
  `ON CONFLICT (key) DO NOTHING` preserves operator state (no deploy regression).
- `/Admin/Tools` POST mirrors `AdminFlagsController`: BasicAuth branch + anti-forgery +
  `SameOriginRequestValidator` + registry-key allow-list (no arbitrary-key write) +
  synchronous `ReloadAsync`. Tests cover cross-origin 403, unknown/blank key rejection,
  write+reload+redirect, and core-tool warning.
- Help topics are hidden/404'd via the pre-existing `requires_flag` enforcement in
  `HelpController`; all 12 tool help files carry the correct flag header.

No CRITICAL defects. The one HIGH is a **completeness gap in the kill-switch**: the JSON
API endpoints that back several tools are not gated and remain fully reachable when the
tool is "off", and the reflection coverage test structurally cannot see them — so CI is
green while the security goal (TOGGLE-04) is only partially met. Confirm whether this is
intended scope.

## High

### HI-01: Tool kill-switch does not cover the backing `/api/*` endpoints — disabled tools remain invokable

**File:** `DeckFlow.Web/Controllers/Api/SuggestionsApiController.cs:48,123,181`; `DeckFlow.Web/Controllers/Api/DeckSyncApiController.cs:42`
**Issue:**
The phase gates the MVC landing routes but leaves the parallel JSON API surface that
performs the *same* work entirely ungated. These are not dead endpoints — the tool pages
call them directly:

- `Views/Deck/SuggestCategories.cshtml:34` → `data-suggestion-api="~/api/suggestions/card"`
- `Views/Commander/CommanderCategories.cshtml:30` → `data-suggestion-api="~/api/suggestions/commander"`
- `Views/Deck/DeckSync.cshtml:44` → `data-deck-sync-api="~/api/deck/diff"`

Result: when an operator disables a tool, the page 404s but the capability keeps running:
| Disabled flag | 404'd page | Still-open API |
|---|---|---|
| `feature.categories.enabled` | `/suggest-categories` | `POST /api/suggestions/card` (full DB+EDHREC+tagger pipeline) |
| `tool.commander-categories.enabled` | `/commander-categories` | `POST /api/suggestions/commander` |
| `tool.mechanic-lookup.enabled` | `/mechanic-lookup` | `POST /api/suggestions/mechanic` |
| `tool.deck-sync.enabled` | `/sync` | `POST /api/deck/diff` |

`SameOriginRequestValidator` does not close this: it deliberately allows header-less
(curl/CLI) requests, so the capability is invokable by direct request even when the tool
is administratively "off." If a tool is disabled to stop upstream cost/abuse or because it
is broken, "off" does not actually stop it. This defeats the core TOGGLE-04 objective for
those tools. The reflection test (`ToolControllerTypes`) excludes the API controllers, so
the gap ships CI-green.

**Fix:** Apply the matching `[FeatureFlagGate("<flag>")]` to each backing API action
(`PostCardSuggestionAsync` → `feature.categories.enabled`, `PostCommanderSuggestionAsync`
→ `tool.commander-categories.enabled`, `PostMechanicLookupAsync` → `tool.mechanic-lookup.enabled`,
`DeckSyncApiController.diff` → `tool.deck-sync.enabled`), and add those controllers to
`ToolRouteGateCoverageTests.ToolControllerTypes` so the coverage test enforces it. If the
team decides the APIs are intentionally out of scope, document that decision in the phase
summary and add an explicit exclusion comment — do not leave it implicit.

## Medium

### ME-01: ToolRouteGateCoverageTests has false-negative holes — silently skips ungated siblings on multi-tool controllers

**File:** `DeckFlow.Web.Tests/Tools/ToolRouteGateCoverageTests.cs:84-105,139-157`
**Issue:**
`MatchTool` returns `null` (and the action is `continue`d / never validated) for any action
whose path does not prefix-match a tool route *unless* its controller resolves to exactly
one tool in `BuildControllerToolRouteMap` (`matchingTools.Length == 1`). Multi-tool
controllers are therefore excluded from the fallback map:
- `DeckPacketController` serves 3 tools (deck-analysis, deck-comparison, cedh-meta-gap)
- `DeckLookupController` serves 2 tools (card-lookup, mechanic-lookup)

Today every action on these controllers happens to prefix-match a tool route, so nothing
escapes. But the test's guarantee is illusory: a future shared sibling (e.g. a
`[HttpPost("/packet/clear")]` helper added to `DeckPacketController`) would be **ungated and
invisible to the test** — it would not prefix-match any tool route, the controller is not in
the map, so `MatchTool` returns null and the action is skipped, not flagged. The test would
stay green while an ungated action shipped. Combined with HI-01, the test gives materially
false confidence about kill-switch coverage.
**Fix:** Make non-matching actions a *failure*, not a skip: assert that every
`HttpMethodAttribute` action on a controller in `ToolControllerTypes` matches some tool
(fail with the action name if it does not), or attribute the controllers themselves so
controller→tool is unambiguous for multi-tool controllers. Also add a guard test that
fails if any controller declaring a tool-prefixed route attribute is missing from
`ToolControllerTypes` (the list is hand-maintained with no enforcement today).

### ME-02: Dead `Title`/`Message`/`PrimaryAction*` properties on FeatureFlagGateAttribute — set at call sites, never rendered

**File:** `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs:25-35,52-62`
**Issue:**
The 503-maintenance-page behavior was replaced by a bare `NotFoundResult`, and
`MaintenanceViewModel` / `_MaintenancePage.cshtml` were deleted. But `Title`, `Message`,
`PrimaryActionLabel`, and `PrimaryActionUrl` remain on the attribute and are still populated
with elaborate copy at several call sites that nothing ever reads:
- `DeckCategoriesController.cs:41-45,60-64,87-91` (full Title/Message/PrimaryAction set)
- `ManabaseController.cs:34,49,120`
- `HelpController.cs:31-33`

`OnActionExecutionAsync` only consumes `Key`. These are now dead properties that actively
mislead a maintainer into believing a custom branded 404/maintenance page is shown (it is
not — the user gets a generic 404). This is also a `/simplify` / Definition-of-Done item
(no dead code left behind).
**Fix:** Remove the four display properties from the attribute and delete the now-dead
named-argument blocks at every call site (reduces three multi-line attributes in
`DeckCategoriesController` to `[FeatureFlagGate("feature.categories.enabled")]`). If a
branded 404 body is actually wanted, instead render it in `OnActionExecutionAsync` so the
properties are live again.

### ME-03: Home hero "Analyze Your Deck" CTA is hardcoded, not registry-driven — becomes a dead 404 link when deck-analysis is disabled

**File:** `DeckFlow.Web/Views/Deck/Home.cshtml:14-18`
**Issue:**
The hero anchor links unconditionally to `~/deck-analysis`, outside the
`ToolVisibility.VisibleBySection` loop. `deck-analysis` is a Core tool whose disablement is
an explicitly supported (and admin-warned) operation. When `tool.deck-analysis.enabled` is
off, the Analyze tile correctly disappears from the grid, but the prominent hero CTA still
renders and now points at a route that returns 404 — a broken primary call-to-action on the
landing page. This contradicts TOGGLE-05 ("home derives correctly from the registry").
**Fix:** Gate the hero on the deck-analysis flag, e.g.
`@if (sections.Any(s => s.Tools.Any(t => t.Key == "deck-analysis")))` wrap the hero, or
derive the hero from the registry's `IsPrimaryTile`/Core metadata so it can never outlive
its tool.

## Low

### LO-01: ManabaseControllerFlagGateTests count assertion is self-referential

**File:** `DeckFlow.Web.Tests/Manabase/ManabaseControllerFlagGateTests.cs:25,38-40`
**Issue:**
`GetManabaseActions()` now filters to methods that *have* a `FeatureFlagGateAttribute`, then
asserts `actions.Length == 3`. Because the filter selects only gated methods, a newly added
**ungated** Manabase action would not be counted and the `== 3` assertion would still pass —
the test can no longer detect a dropped gate on a new sibling (the prior `m.Name == "Manabase"`
filter would have). The broader `ToolRouteGateCoverageTests` still covers ManabaseController,
so this is a weakened guard rather than an open hole.
**Fix:** Enumerate all `HttpMethodAttribute` actions on `ManabaseController` (not just gated
ones) and assert each carries the gate, mirroring the structure of the coverage test.

## Info

### IN-01: Home section icons duplicated — Analyze and Reference use the identical magnifying-glass SVG

**File:** `DeckFlow.Web/Views/Deck/Home.cshtml:31,37`
**Issue:** The `ToolNavSection.Analyze` and `ToolNavSection.Reference` cases emit byte-identical
`<svg>` markup (circle + line magnifying glass). Reference presumably wanted a distinct glyph
(e.g. a book). Purely cosmetic.
**Fix:** Give Reference its own icon, or confirm the duplication is intentional.

### IN-02: Section-header SVGs inlined in a Razor switch while tile icons were extracted to a partial

**File:** `DeckFlow.Web/Views/Deck/Home.cshtml:28-42`
**Issue:** Tile icons were correctly factored into `_ToolTileIcon.cshtml`, but the four
section-header icons remain an inline `@switch` in `Home.cshtml`. Minor inconsistency; both
keep SVG in Razor (per the constraint), so this is style-only.
**Fix:** Optionally extract a `_ToolSectionIcon` partial for symmetry.

---

_Reviewed: 2026-06-25_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: deep_

---

## Resolution (follow-up commit 79e1cb1a)

**Verdict: all findings CLOSED. TOGGLE-04 genuinely satisfied.**

- **HI-01 CLOSED.** All four tool-backing JSON endpoints now carry `[FeatureFlagGate]`
  with the correct registry key, verified against `Services/Tools/ToolRegistry.cs`:
  `/api/deck/diff` → `tool.deck-sync.enabled`; `/api/suggestions/card` →
  `feature.categories.enabled`; `/api/suggestions/commander` →
  `tool.commander-categories.enabled`; `/api/suggestions/mechanic` →
  `tool.mechanic-lookup.enabled`. `/resolve` (MVC, already gated) is now registry-tracked
  via `AdditionalRoutes`. The gate is an `IAsyncActionFilter` that sets
  `context.Result = NotFoundResult()` before the action body, so an API caller gets a clean
  404 when OFF regardless of origin. No tool-backing action left ungated. Codex's
  exclusions are correct: `AnalysisPromptApiController` is dev-only (404 in prod) and
  `ArchidektCacheJobsController` is internal job control — neither is a registry tool.
  `/api/set-options` (ShellController) is correctly left ungated: it returns the shared
  Scryfall set catalog used by multiple analysis tools, not a single-tool capability.

- **ME-01 CLOSED (materially strengthened).** `ToolRouteGateCoverageTests` now (a) discovers
  tracked controllers assembly-wide instead of a hand-maintained list, (b) tracks each
  tool's `Route` plus `AdditionalRoutes` (so the API controllers are covered), and (c)
  records a *failure* for any action on a tracked controller that matches no tracked route
  instead of silently skipping it. This closes the multi-tool-controller hole
  (`DeckPacketController`, `DeckLookupController`): every action must match a tracked route
  and be gated, or the build fails. Controller-level `[Route]` prefixes are handled by
  `GetEffectiveRoutePath` (verified for `ContentKbController` and the `api/*` controllers).
  No residual structural false-negative.

- **ME-02 CLOSED.** The `Title`/`Message`/`PrimaryActionLabel`/`PrimaryActionUrl` properties
  were removed from `FeatureFlagGateAttribute`; every call site reduced to bare
  `[FeatureFlagGate("key")]` (DeckCategories ×3, Manabase ×3, ContentKb ×2, Help ×1).
  `FeatureFlagGateAttributeTests` updated to drop the removed-property assertions.

- **ME-03 CLOSED.** The home hero CTA is now derived from the visible registry sections
  (`headlineWorkflow` = deck-analysis when visible) and only renders when present, using
  the tool's own `Route` — no dead 404 CTA when deck-analysis is disabled.

- **LO-01 CLOSED.** `ManabaseControllerFlagGateTests` now enumerates all
  `HttpMethodAttribute` actions and asserts the concrete set `{Load, Manabase, Manabase}`,
  then checks each carries the gate, so a newly added ungated action would fail.

**New issues introduced by the fix:** none. The `Create(..., params string[] additionalRoutes)`
extension defaults to empty for unchanged tools (no regression); `ToolDefinition.AdditionalRoutes`
defaults to `[]`; the gate works on `[ApiController]`/`ControllerBase` (filter short-circuits
before the action). The only behavioral coupling worth noting (not a defect): the stricter
coverage test now forces any new action added to a tracked controller to be either registered
+ gated or it fails the build — which is the intended guard.

_Resolution verified: 2026-06-25 — Claude (gsd-code-reviewer)_
