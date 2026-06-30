---
phase: 76
plan: 05
subsystem: web-ui
tags: [bracket, razor-view, tool-registry, css, help, tests]
dependency_graph:
  requires: [76-02, 76-04]
  provides: [bracket-ui-surface]
  affects: [tool-registry, site-common-css, help-system]
tech_stack:
  added: []
  patterns: [DeckToolControllerBase, FeatureFlagGate, RunGuardedAsync, flag-gated result section]
key_files:
  created:
    - DeckFlow.Web/Controllers/BracketController.cs
    - DeckFlow.Web/Models/BracketViewModel.cs
    - DeckFlow.Web/Models/BracketRequest.cs
    - DeckFlow.Web/Views/Deck/Bracket.cshtml
    - DeckFlow.Web/Help/bracket.md
    - DeckFlow.Web.Tests/Bracket/BracketViewRenderTests.cs
  modified:
    - DeckFlow.Web/Models/DeckPageTab.cs (Bracket=15 added)
    - DeckFlow.Web/Services/Tools/ToolRegistry.cs (bracket entry added)
    - DeckFlow.Web/Views/Shared/_ToolTileIcon.cshtml (bracket SVG added)
    - DeckFlow.Web/wwwroot/css/site-common.css (Phase 76 bracket block)
    - README.md (bracket classifier and balancer section)
    - DeckFlow.Web.Tests/Tools/ToolRegistryTests.cs (14 tools, 19 routes)
    - DeckFlow.Web.Tests/Tools/ToolVisibilityTests.cs (bracket in Analyze section)
    - DeckFlow.Web.Tests/AdminToolsControllerTests.cs (count 14)
decisions:
  - "Bracket badge rendered with bracket-badge--bN modifier (b1..b5) using existing health palette hex values — no new colour tokens"
  - "Static tiers fallback array in Bracket.cshtml for initial GET before any classification; Model.Tiers takes precedence when present"
  - "@{} block wrapped in <div> inside nested @if to avoid Razor RZ1010 parse error (Unexpected { after @)"
  - "Extra-card-draw rendered as static '0 mass extra-card draw' line — no model field per RESEARCH A2"
  - "BracketController placed in Controllers/ (root), view at Views/Deck/Bracket.cshtml following ManabaseController pattern"
metrics:
  duration_minutes: 90
  tasks_completed: 3
  files_created: 6
  files_modified: 8
---

# Phase 76 Plan 05: Bracket UI Surface Summary

Bracket Check web surface implemented: flag-gated controller, view models, Razor view with badge/violations/starter-cuts/prompt collapsible, Phase 76 CSS block, staircase SVG icon, help topic, and all consistency tests updated.

## What Was Built

**Task 1 — View models + controller (commit `48961bab`)**

- `BracketViewModel`: ActiveTab, Request, ErrorMessage, Classification, Tiers, TargetBracketNumber, PromptArtifact, ImportWarning; computed HasResult/HasTarget/IsOverTarget helpers.
- `BracketRequest`: DeckInputSource, DeckUrl, DeckText, DeckName, TargetBracketNumber (nullable int), TargetAiPlatform; DeckSource computed property.
- `BracketController`: sealed, inherits `DeckToolControllerBase`; GET `/bracket` + POST `/bracket` each gated by `[FeatureFlagGate("tool.bracket.enabled")]`; POST validates TargetBracketNumber in 1..5 before classifying; `RunGuardedAsync` mirrors ManabaseController error ladder (timeout / InvalidOp / HttpRequestException / Exception); structured logging with `{Operation}` placeholder.
- `DeckPageTab.Bracket = 15` added (needed by BracketViewModel default; done early to unblock Task 1 build).

**Task 2 — Razor view, CSS, README (commit `e71f0dc8`)**

- `Views/Deck/Bracket.cshtml`: hero section with "How it works" details; busy indicator + tab strip + error banner; form with deck input source select, URL/text panels (data-sync-panel), deck name, manabase-segmented pills for B1-B5 target, `_AiSelector` partial, toolbar; result section (flag-gated by `Model.HasResult`) with bracket-badge (`bracket-badge--bN` modifier), target comparison line, WHY THIS BRACKET verdict list, combo-unavailable disclosure, floor violations (`bracket-violation-list` with tag kinds --gamechanger/--combo/--mld/--extraturns), starter cuts (wrapped in `<div>` to satisfy Razor HTML context requirement for `@{}`), bracket-stamp with effective date, copy-prompt collapsible; always-on methodology details block.
- Extra-card-draw rendered as static "0 mass extra-card draw" line — no model field exists (RESEARCH A2: mass extra-card-draw not a separate signal in the current rubric).
- Phase 76 CSS block in `site-common.css`: `.bracket-badge` + `__eyebrow/__tier/__name/__meta`; modifiers `.bracket-badge--b1..b5` using existing health palette (b1=#166534, b2=#1d4ed8, b3=var(--accent-strong), b4=#f59e0b, b5=#b91c1c); `.bracket-violation-list` + `.bracket-violation` with `__name/__tag` and tag-kind modifiers; `.bracket-stamp` small muted with left rule; mobile @media (max-width:480px) flex-wrap.
- README: "### Bracket classifier and balancer" section before manabase section.

**Task 3 — Registry, icon, help, tests (commit `89414c10`)**

- ToolRegistry: bracket entry after manabase (Analyze section, not core, not primary tile, helpSlug "bracket", tab Bracket); 14 tools, 19 routes.
- `_ToolTileIcon.cshtml`: `case "bracket"` with 5-step ascending staircase polyline SVG.
- `Help/bracket.md`: front-matter (title, summary, order 36, `requires_flag: tool.bracket.enabled`); classification methodology section, signal table (GC thresholds, combo hard-floor, MLD hard-floor, extra-turns informational), combo detection availability caveat, GC list freshness note, AI platform docs.
- `BracketViewRenderTests`: OFF (Classification=null) → no bracket-badge; ON (B4 with 4 GCs) → bracket-badge + bracket-badge--b4. Uses Razor view engine via IRazorViewEngine with `controller = "Deck"` route data.
- ToolRegistryTests: bracket lambda added; counts 13→14, routes 18→19.
- ToolVisibilityTests: bracket in Analyze section; sum 13→14; bracket flag in OmitsSectionsWithNoVisibleTools disabled dict.
- AdminToolsControllerTests: tool count 13→14.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] DeckPageTab.Bracket missing — moved to Task 1**
- **Found during:** Task 1 (BracketViewModel CS0117 compile error)
- **Issue:** `DeckPageTab.Bracket = 15` needed for BracketViewModel default `ActiveTab`
- **Fix:** Added to DeckPageTab.cs as part of Task 1 commit (plan listed it under Task 3)
- **Files modified:** `DeckFlow.Web/Models/DeckPageTab.cs`
- **Commit:** `48961bab`

**2. [Rule 3 - Blocking] Razor RZ1010: @{} inside nested @if without HTML context**
- **Found during:** Task 2 build
- **Issue:** `@{...}` at top-level of `@if` body before any HTML element caused "Unexpected { after @ character" (lines 101 and 232)
- **Fix:** Moved `<section>` before `@{...}` for badge variables; wrapped starterCuts `@{...}` in `<div>` for HTML context
- **Files modified:** `DeckFlow.Web/Views/Deck/Bracket.cshtml`
- **No extra commit:** same Task 2 commit

**3. [Rule 3 - Blocking] BracketClassification uses primary constructor (not init-only)**
- **Found during:** Task 3 test build
- **Issue:** Test used object-initializer syntax `new BracketClassification { BracketNumber = 4 }` but the record has a positional primary constructor
- **Fix:** Changed to `new BracketClassification(BracketNumber: 4, ...)` positional named-arg syntax
- **Files modified:** `DeckFlow.Web.Tests/Bracket/BracketViewRenderTests.cs`
- **No extra commit:** same Task 3 commit

**4. [Rule 1 - Bug] BracketViewRenderTests used wrong controller name in route data**
- **Found during:** Task 3 test run
- **Issue:** `controller = "Bracket"` caused Razor to search `Views/Bracket/Bracket.cshtml`; correct path is `Views/Deck/Bracket.cshtml`
- **Fix:** Changed to `controller = "Deck"` matching ManabaseViewRenderTests pattern
- **Files modified:** `DeckFlow.Web.Tests/Bracket/BracketViewRenderTests.cs`
- **No extra commit:** same Task 3 commit

**5. [Rule 1 - Bug] AdminToolsControllerTests hardcoded tool count 13**
- **Found during:** Full suite run
- **Issue:** `Assert.Equal(13, sections.Sum(...))` failed because ToolRegistry now has 14 tools
- **Fix:** Updated to 14
- **Files modified:** `DeckFlow.Web.Tests/AdminToolsControllerTests.cs`
- **Commit:** `89414c10`

## Known Stubs

None. The bracket tool is fully wired: flag-gated controller, real classification service injection, real view. The `tool.bracket.enabled` flag is seeded OFF so no user-visible change until the operator flips it.

## Threat Flags

None. New routes are flag-gated, CSRF-protected (ValidateAntiForgeryToken on POST), and served through the existing controller base class which applies the same timeout and error isolation as all other deck tools. No new network endpoints, auth paths, or schema changes introduced.

## Test Results

| Suite | Pass | Fail | Skip |
|-------|------|------|------|
| DeckFlow.Web.Tests | 977 | 0 | 12 |
| DeckFlow.Core.Tests | 896 | 0 | 0 |
| DeckFlow.Studio.Tests | 149 | 1* | 0 |

\* `HarvestPageTests.HarvestPage_ConfirmBlock_Success_RecordsBlockAndRefreshesBadge` — pre-existing bUnit event-binding race condition; passes when run in isolation; no relation to this plan's changes.

## Self-Check: PASSED

Files exist:
- DeckFlow.Web/Controllers/BracketController.cs: FOUND
- DeckFlow.Web/Models/BracketViewModel.cs: FOUND
- DeckFlow.Web/Models/BracketRequest.cs: FOUND
- DeckFlow.Web/Views/Deck/Bracket.cshtml: FOUND
- DeckFlow.Web/Help/bracket.md: FOUND
- DeckFlow.Web.Tests/Bracket/BracketViewRenderTests.cs: FOUND

Commits:
- 48961bab: Task 1 (models + controller) — FOUND
- e71f0dc8: Task 2 (view + CSS + README) — FOUND
- 89414c10: Task 3 (registry + icon + help + tests) — FOUND
