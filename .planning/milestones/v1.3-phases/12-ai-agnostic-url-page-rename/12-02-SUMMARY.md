---
phase: 12-ai-agnostic-url-page-rename
plan: 02
subsystem: razor-views
tags: [aspnet-core, razor, view-rename, mvc-routing, git-mv]

# Dependency graph
requires:
  - phase: 12-ai-agnostic-url-page-rename
    plan: 01
    provides: New `/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap` route attributes on DeckController. Existing action methods still call `View("ChatGptPackets", ...)` etc. — those args land in this plan.
provides:
  - "3 Razor view files renamed via `git mv` (history preserved): DeckAnalysis.cshtml, DeckComparison.cshtml, CedhMetaGap.cshtml"
  - "39 `return View(\"ChatGpt…\", ...)` literal strings in DeckController.cs updated to the new view file names — build resolves view lookup successfully on all three routes"
  - "Phase 11 verification note (CedhMetaGap.cshtml filename mismatch) closed"
affects: [12-03-page-labels, 12-04-artifact-sanitizer, 12-05-docs-sweep, 13-class-rename]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Razor view file rename via `git mv` (R100 detected) — preserves blame/log across the rename boundary (threat T-12-05 mitigation)"
    - "`return View(\"explicit-name\", model)` literal-string contract — view file name is the explicit string argument; action method name and view-model class are independent surfaces"

key-files:
  created:
    - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
    - DeckFlow.Web/Views/Deck/DeckComparison.cshtml
    - DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml
  modified:
    - DeckFlow.Web/Controllers/DeckController.cs
  deleted:
    - DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml
    - DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml

key-decisions:
  - "Used `git mv` (not `mv` + `git add` + `git rm`) so rename detection records R100 — preserves blame/log/follow continuity (D-12; T-12-05)"
  - "Razor `@model` directives left on ChatGpt*ViewModel classes — Phase 13 (CLASSRENAME-01) owns C# class rename (D-14)"
  - "Action method names (ChatGptPackets, ChatGptDeckComparison, ChatGptCedhMetaGap) intentionally untouched — Phase 13 surface (D-14)"
  - "Shipped as 2 commits per CLAUDE.md 'one logical change per commit' — Task 1 = file rename, Task 2 = View() arg updates. Build is intentionally broken between the two commits (intra-plan); plan-level gate verifies clean build at completion."

patterns-established:
  - "Pattern: Razor view file rename in this codebase always ships as 2 commits — (1) `git mv` of the .cshtml files alone (R100-detected rename, no content edits), (2) DeckController `View(\"explicit-name\", ...)` literal-string sweep. Build is green at start and end of the pair, intentionally broken in between."

requirements-completed: [RENAME-02]

# Metrics
duration: ~12min
completed: 2026-05-17
---

# Phase 12 Plan 02: Razor View File Rename + View() Arg Sweep Summary

**Renamed the three Razor view files (`ChatGptPackets.cshtml` / `ChatGptDeckComparison.cshtml` / `ChatGptCedhMetaGap.cshtml`) to AI-agnostic names (`DeckAnalysis.cshtml` / `DeckComparison.cshtml` / `CedhMetaGap.cshtml`) using `git mv` for history preservation, and updated all 39 `return View("ChatGpt…", …)` literal strings in DeckController.cs so view lookup resolves to the renamed files. Closes the Phase 11 verification note flagging the CedhMetaGap.cshtml filename mismatch.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-17T01:25:00Z (worktree spawn)
- **Completed:** 2026-05-17T01:37:13Z
- **Tasks:** 2
- **Files modified:** 1 (DeckController.cs)
- **Files renamed:** 3 (Razor views)
- **Literal strings replaced:** 39 (11 + 14 + 14)

## Accomplishments

- Three view files renamed via `git mv` with R100 (100% similarity) rename detection — `git log --follow` and `git blame` continue to trace history across the rename boundary per D-12.
- 39 `return View("ChatGpt…", …)` call sites updated across `DeckController.cs` (lines 157-1093) — GET handlers, POST handlers, and every error-fallback re-render path covered per D-13.
- Build clean with zero warnings (`dotnet build DeckFlow.Web/DeckFlow.Web.csproj` → 0 warnings, 0 errors).
- D-14 Phase 13 surface invariants preserved:
  - `public IActionResult ChatGptPackets()` action method name unchanged (1 occurrence).
  - `new ChatGptDeckViewModel { ... }` instantiations unchanged (11 occurrences).
  - `new ChatGptDeckComparisonViewModel { ... }` unchanged (14 occurrences).
  - `new ChatGptCedhMetaGapViewModel { ... }` unchanged (14 occurrences).
  - `@model DeckFlow.Web.Models.ChatGpt*ViewModel` directives at top of each renamed view file unchanged.

## Task Commits

Each task was committed atomically per CLAUDE.md "one logical change per commit":

1. **Task 1: Git-mv the three view files to AI-agnostic filenames** — `0126672` (refactor) — 3 R100 renames, no content edits.
2. **Task 2: Update DeckController return View() literal strings to new view names** — `1ccf6f8` (refactor) — 39 insertions, 39 deletions in a single file.

## Files Created/Modified

- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` (renamed from `ChatGptPackets.cshtml`, R100 — content byte-identical)
- `DeckFlow.Web/Views/Deck/DeckComparison.cshtml` (renamed from `ChatGptDeckComparison.cshtml`, R100)
- `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml` (renamed from `ChatGptCedhMetaGap.cshtml`, R100)
- `DeckFlow.Web/Controllers/DeckController.cs` — 39 literal-string replacements on `return View(...)` first arguments. View-model class instantiations and action method names left untouched.

## Decisions Made

- **2-commit split (per CLAUDE.md "one logical change per commit"):** Task 1 ships the rename alone with no content edits so `git log --diff-filter=R` records R100. Task 2 ships the controller string sweep on its own. Between the two commits the build is broken (controller still references `View("ChatGptPackets", …)` against a file that no longer exists by that name); after both commits the build is green again. The plan acceptance gate runs after both commits, so the intermediate broken state is invisible to anything downstream.
- **Build-environment workaround (out of scope of the commit set):** Worktree lacked `DeckFlow.Web/node_modules`, so the MSBuild TypeScript pre-step failed before reaching C# compilation. Copied `node_modules` from the main repo for the local build verification. Not committed (gitignored, `git status` confirmed only `DeckFlow.Web/Controllers/DeckController.cs` staged). Same workaround as Plan 01.

## Deviations from Plan

None — plan executed exactly as written. Both task acceptance grep checks pass:

- File existence (Task 1):
  - `[ -f DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml ]` → exists
  - `[ -f DeckFlow.Web/Views/Deck/DeckComparison.cshtml ]` → exists
  - `[ -f DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml ]` → exists
  - `[ ! -f DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml ]` → gone
  - `[ ! -f DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml ]` → gone
  - `[ ! -f DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml ]` → gone
- Rename detection: `git log --diff-filter=R --name-status HEAD~2..HEAD~1 -- DeckFlow.Web/Views/Deck/` shows three `R100` entries.
- @model directives unchanged: `grep -c "@model DeckFlow.Web.Models.ChatGptDeckViewModel" DeckAnalysis.cshtml` → 1 (similar for the other two views).
- DeckController old strings purged (Task 2):
  - `grep -c 'View("ChatGptPackets"' DeckFlow.Web/Controllers/DeckController.cs` → 0
  - `grep -c 'View("ChatGptDeckComparison"' DeckFlow.Web/Controllers/DeckController.cs` → 0
  - `grep -c 'View("ChatGptCedhMetaGap"' DeckFlow.Web/Controllers/DeckController.cs` → 0
- DeckController new strings present (Task 2):
  - `grep -c 'View("DeckAnalysis"' DeckFlow.Web/Controllers/DeckController.cs` → 11
  - `grep -c 'View("DeckComparison"' DeckFlow.Web/Controllers/DeckController.cs` → 14
  - `grep -c 'View("CedhMetaGap"' DeckFlow.Web/Controllers/DeckController.cs` → 14
- D-14 invariants (Task 2):
  - `grep -c 'public IActionResult ChatGptPackets()' DeckFlow.Web/Controllers/DeckController.cs` → 1 (unchanged)
  - `grep -c 'new ChatGptDeckViewModel' DeckFlow.Web/Controllers/DeckController.cs` → 11 (unchanged from pre-edit)
  - `grep -c 'new ChatGptDeckComparisonViewModel' DeckFlow.Web/Controllers/DeckController.cs` → 14 (unchanged)
  - `grep -c 'new ChatGptCedhMetaGapViewModel' DeckFlow.Web/Controllers/DeckController.cs` → 14 (unchanged)
- Build: `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` → 0 warnings, 0 errors.

## Issues Encountered

- `dotnet` CLI not on `PATH` inside WSL worktree shell — same as Plan 01; used the Windows-side `/mnt/c/Program Files/dotnet/dotnet.exe` for build verification.
- `DeckFlow.Web/node_modules/` missing on worktree spawn — copied from main repo per Plan 01 SUMMARY guidance. `node_modules/` is git-ignored; no contamination of the commit set.

## Manual Smoke-Test Status

Deferred to user — per `feedback_user_starts_server.md` the executor does not auto-launch the dev server. The plan's `<verification>` block specifies a curl-based 200 spot check that the user should run when the dev server is up (`http://localhost:5173`):

- `curl -i http://localhost:5173/deck-analysis` → expect HTTP 200, body contains the existing `<h1>` text from `DeckAnalysis.cshtml` (Phase-1 H1 text rewrite ships in Plan 03; current H1 still reads "ChatGPT Analysis"). Verifies view file resolution works end-to-end through the renamed file.
- `curl -i http://localhost:5173/deck-comparison` → expect HTTP 200 — H1 already AI-agnostic ("Deck Comparison").
- `curl -i http://localhost:5173/cedh-meta-gap` → expect HTTP 200 — H1 already AI-agnostic ("cEDH Meta Gap").
- All three pages render the full session-bar / step-strip / form UI with no Razor view-not-found exception.

## Defer Notes

- **H1 text + `<p class="page-lede">` explainer lines + `.page-lede` CSS:** ship in Plan 03 (page-labels). Out of Plan 02 scope per D-06/D-07/D-08.
- **Artifact filename sanitizer updates** (`compare2` → `comparison`, `cedh` → `cedh-meta-gap`, commander fallback `deckflow-packet` → `deck-analysis`): ship in Plan 04 (artifact-sanitizer). Out of Plan 02 scope per D-10.
- **README + Help/*.md + browser-extension URL sweep:** ship in Plan 05 (docs-sweep). Out of Plan 02 scope per D-15.
- **C# class renames** (`ChatGptDeckViewModel` → AI-agnostic name etc.): ship in Phase 13. The `@model` directives on the three renamed views still reference the old class names per D-14 invariant.
- **Action method renames** (`public IActionResult ChatGptPackets()` etc.): ship in Phase 13 (CLASSRENAME-01..03). The method names still carry the `ChatGpt` prefix per D-14 invariant.

## Next Phase Readiness

- Plan 03 (H1 + explainer line) is unblocked: the three view files are now at their AI-agnostic file paths, ready for in-place H1/lede edits.
- Plan 04 (artifact sanitizer) is independent — operates on `ChatGptPacketArtifactStore.cs`, no dependency on view filenames.
- Plan 05 (docs sweep) is independent — operates on `*.md` and `browser-extensions/deckflow-bridge/`, no dependency on view filenames.

## Self-Check: PASSED

- File `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` exists.
- File `DeckFlow.Web/Views/Deck/DeckComparison.cshtml` exists.
- File `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml` exists.
- Files `DeckFlow.Web/Views/Deck/ChatGpt{Packets,DeckComparison,CedhMetaGap}.cshtml` do not exist.
- Commit `0126672` present in `git log --oneline` (rename commit, three R100 file moves).
- Commit `1ccf6f8` present in `git log --oneline` (View() arg sweep, 39 insertions / 39 deletions).
- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` returns 0 warnings + 0 errors.
- D-14 invariants (action method name, view-model class instantiations, `@model` directives) preserved with pre/post-edit counts matching.

---
*Phase: 12-ai-agnostic-url-page-rename*
*Plan: 02*
*Completed: 2026-05-17*
