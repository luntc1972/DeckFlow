---
phase: 02-layout-hierarchy-ux-copy
plan: 01
subsystem: web-css
tags: [css, layout, hub, feedback, admin, busy-state]
requires:
  - phase-01-tokens (--cta-border, --panel, --line, --ink, --muted, --on-accent, --fs-xs, --fs-sm, --fs-xl)
  - existing busy-spin keyframes in site.css:1107
provides:
  - .hub-hero band CSS (UI-LH-01)
  - .hub-card--primary modifier (UI-LH-01)
  - .feedback-panel amended with token-driven background+border (UI-LH-02)
  - .admin-feedback-detail rule absorbing inline-style values (UI-LH-02)
  - .admin-action-form purpose-named class for inline forms (UI-LH-02)
  - .feedback-submit--busy spinner state (UX-02)
affects:
  - DeckFlow.Web/wwwroot/css/site-common.css
tech-stack:
  added: []
  patterns:
    - BEM modifier on existing .hub-card
    - ::before pseudo-element spinner reusing site.css keyframes
    - amend-in-place for existing feedback-panel rule
key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/css/site-common.css
decisions:
  - "Reused existing busy-spin keyframes from site.css:1107 (no duplicate @keyframes in site-common.css)"
  - "Single-signal accent border treatment for both hero and per-group primaries (D-03)"
  - ".admin-action-form is purpose-named, not generalized to .inline-form (D-12)"
metrics:
  duration: ~12 min (mostly the dotnet build)
  completed: 2026-04-30
  tasks: 4
  files: 1
  commits: 3
---

# Phase 02 Plan 01: Phase 02 CSS Foundation Summary

**One-liner:** Lands all seven new/amended CSS rule blocks for Phase 02 (hub hero, primary card modifier, feedback/admin panel amendments, admin inline-form class, feedback submit busy state) in a single file (site-common.css) consuming only Phase 01 tokens — no new :root declarations, no site.css edits, no duplicate keyframes.

## What Shipped

All Phase 02 CSS rules now live in `DeckFlow.Web/wwwroot/css/site-common.css`:

| Selector | site-common.css line | Source decision |
|----------|---------------------|-----------------|
| `.hub-hero` | 194 | UI-LH-01 / D-01, D-04 |
| `.hub-hero:hover, .hub-hero:focus-visible` | 207–208 | UI-SPEC §"Interaction States — Hub hero" |
| `.hub-hero__eyebrow` | 213 | UI-SPEC §1 |
| `.hub-hero__title` | 223 | UI-SPEC §1 |
| `.hub-hero__description` | 231 | UI-SPEC §1 |
| `.hub-card--primary` | 239 | UI-LH-01 / D-02, D-03 |
| `.hub-card--primary:hover, .hub-card--primary:focus-visible` | 243–244 | UI-SPEC §"Interaction States — Hub cards" |
| `.feedback-panel` (amended) | 621 | UI-LH-02 / D-14 |
| `.feedback-submit--busy` | 650 | UX-02 / D-08, D-11 |
| `.feedback-submit--busy::before` | 657 | UX-02 / D-09 |
| `.admin-feedback-detail` | 692 | UI-LH-02 / D-14 |
| `.admin-action-form` | 701 | UI-LH-02 / D-12 |

## Verification Evidence

**Selector location gate (verifier #3):** all four new selector families found in `site-common.css`, zero in `site.css`.

```
selector \.hub-hero[^a-z-]: site-common.css=6 site.css=0
selector \.hub-card--primary: site-common.css=3 site.css=0
selector \.admin-action-form: site-common.css=1 site.css=0
selector \.feedback-submit--busy: site-common.css=2 site.css=0
PASS: all new selectors live in site-common.css only
```

**:root immutability gate (verifier #4):** zero net new token lines in site.css across this plan's three commits.

```
$ git diff HEAD~3 HEAD -- DeckFlow.Web/wwwroot/css/site.css | grep -E '^\+\s*--[a-z]' | wc -l
0
$ git diff HEAD~3 HEAD -- DeckFlow.Web/wwwroot/css/site.css | wc -l
0
PASS: zero new :root tokens (site.css unchanged across 02-01 commits)
```

**Duplicate keyframes gate:** `@keyframes busy-spin` count in site-common.css = 0; `animation: busy-spin` reference present once. Spinner resolves against site.css:1107 keyframes at runtime.

**Build gate:** `dotnet build DeckFlow.sln -c Debug` → `Build succeeded. 0 Warning(s) 0 Error(s)`. Browser-extension zip target ran clean as part of the standard build.

**Per-task automated checks:** all four `<verify>` blocks reported PASS:
- Task 1: 6 hub-hero rule lines, 3 hub-card--primary rule lines, 1 accent stripe.
- Task 2: feedback-panel amendment includes both new declarations; .admin-feedback-detail count=1; .admin-action-form count=1; no duplicate feedback-panel block.
- Task 3: base + ::before rules each count=1; zero @keyframes in site-common.css; ≥1 animation reference.

## Commits

| # | Hash | Subject |
|---|------|---------|
| 1 | `53419ea` | feat(02-01): add hub hero band and primary card modifier CSS |
| 2 | `7dd90f6` | feat(02-01): amend feedback-panel and add admin-feedback-detail / admin-action-form CSS |
| 3 | `fc83aac` | feat(02-01): add feedback-submit--busy spinner CSS |

## Forward Signal to Plan 02

The following classes are now declared and ready to be referenced in markup by Plan 02:

- `.hub-hero` + sub-elements (`.hub-hero__eyebrow`, `.hub-hero__title`, `.hub-hero__description`) → wire on `Views/Deck/Home.cshtml` after `.hub-lede`, before first `.hub-group`.
- `.hub-card--primary` → add to `.hub-card` class list on three cards (Deck Comparison, Deck Sync, Card Lookup).
- `.feedback-panel` → strip inline `style=` from `Views/Feedback/Index.cshtml:8`.
- `.admin-feedback-detail` → strip inline `style=` from `Views/AdminFeedback/Detail.cshtml:6`.
- `.admin-action-form` → replace `style="display:inline"` on 4 admin forms (Index.cshtml:74; Detail.cshtml:27, 34, 39).
- `.feedback-submit--busy` → toggled by Plan 03 TS handler.

Plan 02 markup edits are inert before this plan landed; with this plan landed they take immediate visual effect.

## Deviations from Plan

None — plan executed exactly as written. All four tasks landed verbatim per UI-SPEC values; no Rule 1/2/3 auto-fixes triggered; no Rule 4 architectural questions surfaced.

## Known Stubs

None. All CSS rules ship complete with token-driven values. Markup wiring (Plan 02) and TS handler (Plan 03) are tracked in their own plans within the same phase, so the cross-plan dependency is explicitly documented and not a stub.

## Self-Check: PASSED

- File `DeckFlow.Web/wwwroot/css/site-common.css` exists and contains all 12 listed selector occurrences at the cited line numbers.
- Commits `53419ea`, `7dd90f6`, `fc83aac` are present in `git log --oneline`.
- Build clean: 0 Warning(s), 0 Error(s).
- site.css diff across plan: empty.
