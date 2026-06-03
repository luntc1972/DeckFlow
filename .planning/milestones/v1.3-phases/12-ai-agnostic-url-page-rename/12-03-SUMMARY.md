---
phase: 12-ai-agnostic-url-page-rename
plan: 03
subsystem: razor-views-and-css
tags: [aspnet-core, razor, css, theme-system, page-labels, ai-agnostic]

# Dependency graph
requires:
  - phase: 12-ai-agnostic-url-page-rename
    plan: 01
    provides: New `/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap` route attributes on DeckController. Hrefs in nav and home now target these slugs.
  - phase: 12-ai-agnostic-url-page-rename
    plan: 02
    provides: Razor view files at AI-agnostic file paths (`DeckAnalysis.cshtml`, `DeckComparison.cshtml`, `CedhMetaGap.cshtml`) — Plan 03 edits the H1, lede paragraph, and ViewData[Title] inside those files.
provides:
  - "Mock A explainer line (`<p class=\"page-lede\">`) under H1 on all three AI workflow pages — exact copy per D-07"
  - "Page-1 user-visible label (H1 + browser <title> + nav link + hub-hero title + hub-card title) is now `Deck Analysis` everywhere per D-06 + D-09"
  - "All six chatgpt- hrefs across `_DeckToolTabs.cshtml` (3) and `Home.cshtml` (3 hub-cards + 1 hub-hero = 4) point at the new slugs"
  - "Cross-cutting `.page-lede` CSS rule lives in `site-common.css` only (D-08; CLAUDE.md D-07 invariant satisfied — does NOT appear in any per-theme guild fork)"
affects: [12-04-artifact-sanitizer, 12-05-docs-sweep, 13-class-rename]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Cross-cutting CSS pattern: explainer-line `.page-lede` mirrors the existing `.hub-lede` analog at site-common.css:209-213 (same margin/color/font-size shape) — single source of truth across all 22 guild themes via shared tokens var(--muted) + var(--fs-base)"
    - "Single-task atomic Razor edit: H1 + ViewData[Title] + lede paragraph in a single view file ship in one commit when they form a single user-visible-label change (D-06/D-07/D-09 are cohesive)"

key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
    - DeckFlow.Web/Views/Deck/DeckComparison.cshtml
    - DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml
    - DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml
    - DeckFlow.Web/Views/Deck/Home.cshtml
  deleted: []

key-decisions:
  - "Placed `.page-lede` rule at end of site-common.css (line 1393) rather than co-located with `.hub-lede` (line 209) — keeps Phase 12 additions clearly demarcated by the section comment marker; future Phase 11+ cross-cutting rules append at file end per established pattern"
  - "Rewrote hub-hero description from `paste into ChatGPT, review the structured response` to `paste into ChatGPT, Claude, or Gemini, review the structured response` per D-09 plan instruction (second option) — keeps voice consistent with the new lede copy on all three pages"
  - "Kept existing `<p class=\"lede\">` (the legacy long-form description) AS-IS in all three views — the new `<p class=\"page-lede\">` is an ADDITIONAL short Mock A explainer paragraph that sits BETWEEN the H1 and the existing lede. This means each page now has two paragraphs under the H1: a short page-lede (new) followed by the existing detailed lede. Mock A specifies the short explainer; the longer existing lede remains useful context for new users"
  - "Did NOT touch action method names, `@model` directives, or `DeckPageTab` enum values — Phase 13 owns those (CLASSRENAME-01..03 per D-14). The `is-active` conditionals in _DeckToolTabs.cshtml still reference `DeckPageTab.ChatGptPackets` / `.ChatGptDeckComparison` / `.ChatGptCedhMetaGap` unchanged"

patterns-established:
  - "Pattern: User-visible Razor label changes ship as 2 atomic commits per CLAUDE.md 'one logical change per commit' — (1) supporting CSS + content additions across all affected views in one cohesive change, (2) label rewrites + URL synchronization in one cohesive change"
  - "Pattern: When the plan's grep acceptance check expects an exact count that includes pre-existing occurrences (e.g., `DeckPageTab.ChatGptPackets` expected==1 but file already had 2), trust the D-14 invariant (`enum unchanged`) and verify pre-edit count matches post-edit count via `git show HEAD~N:file`"

requirements-completed: [RENAME-02]

# Metrics
duration: ~4min
completed: 2026-05-17
---

# Phase 12 Plan 03: Page Labels + Explainer Lines Summary

**Closed the user-visible label half of RENAME-02 — added the Mock A `<p class="page-lede">` explainer paragraph under the H1 on all three AI workflow pages with exact D-07 copy, rebranded Page 1's H1 + browser title + nav label + hub-card title from `ChatGPT Analysis` to `Deck Analysis` per D-06 + D-09, swung all six remaining `~/chatgpt-` hrefs in `_DeckToolTabs.cshtml` (3) and `Home.cshtml` (4 including the hub-hero) to the new slugs, and added a cross-cutting `.page-lede` CSS rule to `site-common.css` (CLAUDE.md D-07 invariant — single source, NOT forked across any of the 22 guild themes). Pages 2 and 3 H1s left unchanged per D-06 (already AI-agnostic). Build clean against both `DeckFlow.Web.csproj` (Debug) and `DeckFlow.sln` (Release) with 0 warnings, 0 errors.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-05-17T01:40:57Z
- **Completed:** 2026-05-17T01:44:11Z
- **Tasks:** 2
- **Files modified:** 6 (1 CSS + 5 Razor)
- **Files created/deleted:** 0
- **Total diff:** 24 insertions, 12 deletions across 6 files

## Accomplishments

- `.page-lede` CSS rule added at **line 1393** of `DeckFlow.Web/wwwroot/css/site-common.css` with the exact shape specified in `<interfaces>` (margin: 0.25rem 0 1rem / color: var(--muted) / font-size: var(--fs-base)) plus the 3-line section header comment per D-08.
- `.page-lede` does NOT appear in ANY per-theme guild file under `wwwroot/css/` — verified with `grep -rln "\.page-lede" DeckFlow.Web/wwwroot/css/ | grep -v site-common.css | wc -l` → `0`. CLAUDE.md D-07 cross-cutting invariant + threat T-12-07 fully mitigated.
- Three `<p class="page-lede">…</p>` paragraphs inserted (one per view), each immediately after its H1, with the exact Mock A copy per D-07:
  - **DeckAnalysis.cshtml line 30:** `<p class="page-lede">Generate a prompt to paste into ChatGPT, Claude, or Gemini.</p>`
  - **DeckComparison.cshtml line 145:** `<p class="page-lede">Generate a prompt comparing two decks. Paste into ChatGPT, Claude, or Gemini.</p>`
  - **CedhMetaGap.cshtml line 22:** `<p class="page-lede">Generate a prompt analyzing your deck against current cEDH meta. Paste into ChatGPT, Claude, or Gemini.</p>`
- Page-1 label rewritten everywhere per D-06 + D-09:
  - **DeckAnalysis.cshtml line 3:** `ViewData["Title"] = "Deck Analysis";` (renders as `Deck Analysis - DeckFlow` in the browser tab via `_Layout.cshtml:43`)
  - **DeckAnalysis.cshtml line 29:** `<h1>Deck Analysis</h1>`
  - **_DeckToolTabs.cshtml line 18:** nav-link text `Deck Analysis`, href `~/deck-analysis`
  - **Home.cshtml line 11:** hub-hero href `~/deck-analysis`
  - **Home.cshtml line 13:** hub-hero title `Analyze Your Deck` (dropped `with ChatGPT` suffix per D-09)
  - **Home.cshtml line 20-21:** first hub-card href `~/deck-analysis`, title `Deck Analysis`
- All six remaining `chatgpt-` hrefs flipped to new slugs (3 in nav, 1 hub-hero + 3 hub-cards = 4 in home). Grep `Url.Content("~/chatgpt-` against both files returns 0.
- Pages 2 and 3 H1 and ViewData[Title] strings left untouched (already AI-agnostic per D-06).
- Hub-hero description rewritten for vendor parity: `paste into ChatGPT, review the structured response` → `paste into ChatGPT, Claude, or Gemini, review the structured response` per D-09 plan instruction (second option chosen).
- Phase 13 surface (D-14) preserved:
  - `@model DeckFlow.Web.Models.ChatGptDeckViewModel` etc. directives unchanged in all three views.
  - `DeckPageTab.ChatGptPackets` / `.ChatGptDeckComparison` / `.ChatGptCedhMetaGap` enum references in `_DeckToolTabs.cshtml` unchanged (pre/post-edit count = 2 each — `analyzeActive` analyzer line + `is-active` conditional).
- Build verification — both gates pass:
  - `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` → 0 warnings, 0 errors.
  - `dotnet build DeckFlow.sln --configuration Release` → 0 warnings, 0 errors.

## Task Commits

Each task ships as one atomic commit per CLAUDE.md "one logical change per commit":

1. **Task 1: Add .page-lede CSS rule + lede paragraphs to all three view files** — `6b3dbb8` (feat) — 4 files changed, 12 insertions(+). Cross-cutting CSS + the three exact Mock A explainer paragraphs ship together because the markup has no styling without the rule and the rule has no purpose without the markup.
2. **Task 2: Rebrand Page-1 labels + sync nav/home hrefs** — `208654b` (feat) — 3 files changed, 12 insertions(+), 12 deletions(-). H1 + Title + nav label + hub-hero/card labels + six href flips ship together because partial application would leave a visibly inconsistent user surface (e.g., nav-link `Deck Analysis` pointing at `/chatgpt-packets`).

## Files Created/Modified

- `DeckFlow.Web/wwwroot/css/site-common.css` — added `.page-lede` rule (Task 1, +8 lines including the comment block at lines 1390-1397). Cross-cutting per D-08.
- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` — added page-lede `<p>` after H1 (Task 1); rewrote ViewData[Title] and H1 text (Task 2). Net +1 line vs HEAD baseline.
- `DeckFlow.Web/Views/Deck/DeckComparison.cshtml` — added page-lede `<p>` after H1 (Task 1). H1 and Title unchanged per D-06. Net +1 line.
- `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml` — added page-lede `<p>` after H1 (Task 1). H1 and Title unchanged per D-06. Net +1 line.
- `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` — three nav-link hrefs flipped + Page-1 nav label rewritten (Task 2). Pages 2/3 labels and the `DeckPageTab.*` is-active conditionals untouched per D-09 + D-14.
- `DeckFlow.Web/Views/Deck/Home.cshtml` — hub-hero href + title + description rewritten (Task 2); first hub-card href + title rewritten; second and third hub-card hrefs flipped to new slugs. Hub-card 2 and 3 titles and descriptions unchanged per D-09 scope.

## Decisions Made

- **Placed `.page-lede` rule at the END of `site-common.css`** (line 1393) rather than co-located with `.hub-lede` (line 209). The plan's `<interfaces>` example block specifies a `Phase 12 (RENAME-02)` section header comment, which is the established Phase 11+ marker pattern for new cross-cutting rules — appending preserves blame/history and keeps the file's existing cascade order undisturbed.
- **Kept the existing `<p class="lede">` paragraphs** in all three views in addition to the new `<p class="page-lede">` Mock A paragraphs. The two paragraphs serve different purposes: `page-lede` is the new Mock A one-line explainer (short, muted, immediately under H1), `lede` is the legacy multi-sentence description giving new users full context. Result: each page renders H1 → page-lede (new, short) → lede (existing, long) in a clean cascade. This is the most conservative read of D-07 (which mandates the new paragraph but does not say to remove the existing one), and the project's user is in active production with no signal that the existing lede is unwanted.
- **Hub-hero description copy choice (D-09 plan parenthetical):** Chose option 2 (rewrite to `paste into ChatGPT, Claude, or Gemini`) per the plan's explicit guidance, keeping voice consistent with the new lede paragraphs.
- **Did NOT modify** `@model` directives, `DeckPageTab` enum references, or any controller/view-model class names — those are Phase 13 surface per D-14.

## Deviations from Plan

None. The plan executed exactly as written. All Task 1 and Task 2 acceptance grep checks pass, build is clean, no architectural changes triggered Rule 4.

One minor pre-existing observation worth noting (NOT a deviation): the plan's Task 2 acceptance check expected `grep -c "DeckPageTab.ChatGptPackets" _DeckToolTabs.cshtml` to return `1`, but the actual pre-edit count was `2` (the file references that enum value at both the `analyzeActive` analyzer line and the `is-active` conditional on the link). My edit did NOT change this count — pre and post both = 2 — so the underlying invariant ("enum reference UNCHANGED per Phase 13 invariant") is fully satisfied. Verified via `git show HEAD~2:DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml | grep -c DeckPageTab.ChatGptPackets` → 2.

## Issues Encountered

- `dotnet` CLI not on `PATH` inside the WSL worktree shell — used the Windows-side `/mnt/c/Program Files/dotnet/dotnet.exe` per Plan 01 + Plan 02 SUMMARY guidance. No commits or workspace changes resulted.
- `DeckFlow.Web/node_modules/` missing on worktree spawn — copied from the main repo per Plan 01 + Plan 02 SUMMARY guidance. `node_modules` is gitignored; `git status --short` confirmed no contamination of the commit set (only the 6 expected files showed up).

## Manual Smoke-Test Status

Deferred to user — per the user's `feedback_user_starts_server.md` memory, the executor does not auto-launch the DeckFlow dev server. The plan's `<verification>` block specifies the following spot checks for the user to run after starting the dev server (`/mnt/c/users/chrislunt/source/personal/deckflow/scripts/run-web.sh` or `run-web.ps1`):

- Visit `/deck-analysis` → page renders `<h1>Deck Analysis</h1>` followed by the page-lede paragraph; browser tab title reads `Deck Analysis - DeckFlow`.
- Visit `/deck-comparison` → page renders `<h1>Deck Comparison</h1>` (unchanged) followed by the page-lede paragraph.
- Visit `/cedh-meta-gap` → page renders `<h1>cEDH Meta Gap</h1>` (unchanged) followed by the page-lede paragraph.
- Top-nav strip on every Deck-tool page shows `Deck Analysis` | `Deck Comparison` | `cEDH Meta Gap` links, all targeting the new `/deck-*` slugs (no 301 hop expected — direct routing per Plan 01).
- Home page (`/`) → hub-hero says `Analyze Your Deck` and points at `/deck-analysis`; first hub-card title says `Deck Analysis`.
- DevTools → Computed styles on the `.page-lede` element should show `color` resolved to the theme's `--muted` token and `font-size` resolved to `--fs-base` (varies per theme; spot-check by visiting `?theme=…` for 2-3 themes).
- Mobile viewport (375 px): page-lede wraps cleanly; nav strip does not overflow horizontally.

## Defer Notes

- **Artifact filename sanitizer** (`compare2` → `comparison`, `cedh` → `cedh-meta-gap`, commander fallback `deckflow-packet` → `deck-analysis`): Plan 04 per D-10. Out of Plan 03 scope.
- **README, Help/*.md, and browser-extension URL sweep**: Plan 05 per D-15. Plan 03 leaves docs and the extension untouched.
- **C# class renames** (`ChatGptDeckViewModel`, `ChatGptDeckPacketService`, etc.): Phase 13 (CLASSRENAME-01..03) per D-14. Plan 03 leaves all `@model` directives, controller class names, and view-model class names intact.
- **Action method renames** (`public IActionResult ChatGptPackets()` etc.): Phase 13 per D-14. Plan 03 does not touch these.
- **Visual regression harness across 22 guild themes**: tracked in v1.0 deferred list. The shared-token approach (var(--muted), var(--fs-base)) means each theme picks up its own resolved values without any per-theme fork.

## Threat Surface Scan

No new surface introduced beyond the plan's `<threat_model>`:

- T-12-06 (information disclosure via hardcoded copy) — accept; no user data crosses any boundary.
- T-12-07 (cross-cutting CSS drift across 22 guild themes) — mitigated; final grep gate (`grep -rln "\.page-lede" DeckFlow.Web/wwwroot/css/ | grep -v site-common.css | wc -l` → `0`) verified.
- T-12-08 (open-redirect via `Url.Content`) — mitigated; all six slug strings are hardcoded literals (`deck-analysis`, `deck-comparison`, `cedh-meta-gap`); no user input crosses the boundary.

## Next Phase Readiness

- **Plan 04 (artifact sanitizer)** is unblocked and independent — operates on `ChatGptPacketArtifactStore.cs`. No dependency on Plan 03.
- **Plan 05 (docs + extension sweep)** is unblocked and independent — operates on `*.md` and `browser-extensions/deckflow-bridge/`. No dependency on Plan 03.
- **Phase 13 (CLASSRENAME-01..03)** — the user-visible/URL surface (Phase 12 scope) is now fully AI-agnostic. Phase 13 can proceed with class renames knowing that any string change inside the Razor view template files is now stable.

## Self-Check: PASSED

- File `DeckFlow.Web/wwwroot/css/site-common.css` exists with `.page-lede` rule at line 1393 (verified via `grep -n "^\.page-lede" DeckFlow.Web/wwwroot/css/site-common.css`).
- Commit `6b3dbb8` present in `git log --oneline` (Task 1 — feat: add .page-lede explainer paragraphs to AI workflow pages).
- Commit `208654b` present in `git log --oneline` (Task 2 — feat: rebrand Page-1 to Deck Analysis + sync nav/home hrefs).
- Build `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` → 0 warnings, 0 errors.
- Build `dotnet build DeckFlow.sln --configuration Release` → 0 warnings, 0 errors.
- All 11 plan-acceptance grep checks pass (verified inline above under Task 1 + Task 2 verify sections).
- Plan-level success criteria — all 6 checkboxes pass.
- Project CLAUDE.md compliance:
  - `.page-lede` lives in site-common.css ONLY (not site.css, not any guild fork) — D-07 invariant satisfied.
  - Uses `var(--muted)` + `var(--fs-base)` tokens — no hardcoded colors.
  - Plain default-author commits, no `Co-Authored-By` trailer.
  - One logical change per commit (2 commits for 2 cohesive logical changes).

---
*Phase: 12-ai-agnostic-url-page-rename*
*Plan: 03*
*Completed: 2026-05-17*
