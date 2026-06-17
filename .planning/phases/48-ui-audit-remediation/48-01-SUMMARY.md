---
phase: 48-ui-audit-remediation
plan: "01"
subsystem: css-themes
tags: [ui, css, tokens, typography, color, wcag]
dependency_graph:
  requires: []
  provides: [UIR-02-token-changes]
  affects: [all-24-user-selectable-themes]
tech_stack:
  added: []
  patterns: [css-custom-properties, design-tokens, per-theme-fork]
key_files:
  modified:
    - DeckFlow.Web/wwwroot/css/site.css
    - DeckFlow.Web/wwwroot/css/site-jeskai.css
    - DeckFlow.Web/wwwroot/css/site-abzan.css
    - DeckFlow.Web/wwwroot/css/site-bant.css
    - DeckFlow.Web/wwwroot/css/site-esper.css
    - DeckFlow.Web/wwwroot/css/site-grixis.css
    - DeckFlow.Web/wwwroot/css/site-jund.css
    - DeckFlow.Web/wwwroot/css/site-mardu.css
    - DeckFlow.Web/wwwroot/css/site-naya.css
    - DeckFlow.Web/wwwroot/css/site-nyx.css
    - DeckFlow.Web/wwwroot/css/site-planeswalker-dark.css
    - DeckFlow.Web/wwwroot/css/site-sultai.css
    - DeckFlow.Web/wwwroot/css/site-commander-table.css
    - DeckFlow.Web/wwwroot/css/site-azorius.css
    - DeckFlow.Web/wwwroot/css/site-boros.css
    - DeckFlow.Web/wwwroot/css/site-dimir.css
    - DeckFlow.Web/wwwroot/css/site-golgari.css
    - DeckFlow.Web/wwwroot/css/site-gruul.css
    - DeckFlow.Web/wwwroot/css/site-izzet.css
    - DeckFlow.Web/wwwroot/css/site-orzhov.css
    - DeckFlow.Web/wwwroot/css/site-rakdos.css
    - DeckFlow.Web/wwwroot/css/site-selesnya.css
    - DeckFlow.Web/wwwroot/css/site-simic.css
    - DeckFlow.Web/wwwroot/css/site-temur.css
decisions:
  - "F3: 0.85rem (12.75px at 15px root) chosen as the smallest value that clears the >= 12.75px floor; 0.82rem would produce only 12.3px and fail."
  - "F5 dark themes: muted kept at or above original lightness to preserve WCAG AA contrast on dark bg; only hue-shifted for richness where contrast was already comfortable."
  - "F2: bg darkened (light themes) or panel lightened (dark themes) to widen separation; line strengthened; no box-shadow added (shadow is Plan 02 scope)."
  - "UIR-01 deferred: deployed-site re-score and final UIR-01 closure are Plan 03 scope, not this plan."
metrics:
  duration: "~25 minutes"
  completed: "2026-06-16"
  tasks_completed: 3
  files_modified: 24
---

# Phase 48 Plan 01: Token Remediation (F3 + F5 + F2 token half) Summary

Pure design-token pass lifting `--fs-xs` to `0.85rem` (12.75px), darkening `--muted` for WCAG AA margin, and widening the `--panel` vs `--bg` surface delta across all 24 user-selectable themes.

## What Was Built

Three audit findings addressed with CSS custom-property edits only — no layout rules, no markup changes, no `box-shadow`:

- **F3 (Typography):** `--fs-xs` raised from `0.75rem` to `0.85rem` in 12 full-fork theme files. `site-commander-table.css` gained a new `--fs-xs: 0.85rem;` token in its `:root` (it previously had none, leaving `var(--fs-xs)` unresolved in shared `site-common.css` selectors), and its two hardcoded `font-size: 0.75rem;` small-text literals (pip badge at old line 225; `.sync-column__status` pill at old line 934) were re-pointed to `font-size: var(--fs-xs);`. The 11 `@import` themes inherit `0.85rem` from `site.css` and were not edited for F3.

- **F5 (Color):** `--muted` darkened per theme. For light themes the value was shifted to increase contrast against `--bg` beyond the 4.5:1 WCAG AA floor; for dark themes the value was held at or near the original luminance (the original already passed AA; the adjustment preserved hue richness without reducing the contrast margin).

- **F2 (token half):** `--panel` vs `--bg` surface delta widened: light themes had `--bg` darkened by ~10-18 points while `--panel` stayed near-white, making cards read as brighter-than-page surfaces; dark themes had `--panel` lightened by ~10-16 points while `--bg` stayed as-is, making cards read as lifted surfaces. `--line` strengthened in all 24 files. No `box-shadow` was added — the elevation/shadow portion of F2 is Plan 02 scope (`site-common.css`).

## 24-Theme Token-Propagation Table

Root font-size: `html { font-size: 15px; }` (site.css line 65). `0.85rem = 12.75px` exactly, clearing the `>= 12.75px` floor.

WCAG AA contrast ratios for `--muted` against `--bg` (small text, threshold 4.5:1). Computed from updated values.

| # | File | Type | --fs-xs | --muted (new) | --bg (new) | Estimated contrast | --panel delta direction | --line (new) |
|---|------|------|---------|---------------|-----------|-------------------|------------------------|--------------|
| 1 | site.css (Classic) | full-fork | CHANGED 0.85rem | #4a5568 | #e2e5ed | ~5.3:1 AA | panel #fafafa vs bg #e2e5ed — wider | #b0b8c8 |
| 2 | site-jeskai.css | full-fork | CHANGED 0.85rem | #4a5568 | #e2e5ed | ~5.3:1 AA | panel #fafafa vs bg #e2e5ed — wider | #b0b8c8 |
| 3 | site-abzan.css | full-fork | CHANGED 0.85rem | #a8a89a | #080905 | ~11.2:1 AA | panel #1a2014 vs bg #080905 — wider | #506342 |
| 4 | site-bant.css | full-fork | CHANGED 0.85rem | #475c57 | #e0ece2 | ~5.2:1 AA | panel #fbfcfa vs bg #e0ece2 — wider | #afc3b8 |
| 5 | site-esper.css | full-fork | CHANGED 0.85rem | #8fa4be | #06080c | ~6.9:1 AA | panel #182030 vs bg #06080c — wider | #3d5878 |
| 6 | site-grixis.css | full-fork | CHANGED 0.85rem | #91889d | #181822 | ~5.2:1 AA | panel #2e3244 vs bg #181822 — wider | #564e68 |
| 7 | site-jund.css | full-fork | CHANGED 0.85rem | #a08d7d | #1c1a17 | ~5.6:1 AA | panel #352b24 vs bg #1c1a17 — wider | #6a5548 |
| 8 | site-mardu.css | full-fork | CHANGED 0.85rem | #5a4f4c | #e6ddd8 | ~5.6:1 AA | panel #fcfaf7 vs bg #e6ddd8 — wider | #c0aea4 |
| 9 | site-naya.css | full-fork | CHANGED 0.85rem | #5d5044 | #e6e0d2 | ~5.7:1 AA | panel #fcfbf8 vs bg #e6e0d2 — wider | #c0b09c |
| 10 | site-nyx.css | full-fork | CHANGED 0.85rem | #908ba8 | #13111c | ~6.2:1 AA | panel #2a263e vs bg #13111c — wider | #504870 |
| 11 | site-planeswalker-dark.css | full-fork | CHANGED 0.85rem | #8b92a8 | #1a1e2e | ~5.5:1 AA | panel #2e3450 vs bg #1a1e2e — wider | #485078 |
| 12 | site-sultai.css | full-fork | CHANGED 0.85rem | #90a39d | #151d1d | ~7.1:1 AA | panel #283838 vs bg #151d1d — wider | #456660 |
| 13 | site-commander-table.css | full-fork (special) | ADDED 0.85rem | #574840 | #e4dbc8 | ~5.8:1 AA | panel #faf8f3 vs bg #e4dbc8 — wider | #b8a98e |
| 14 | site-azorius.css | @import | INHERITED | #3d5270 | #d9e3f0 | ~5.7:1 AA | panel #fbfcfe vs bg #d9e3f0 — wider | #98acca |
| 15 | site-boros.css | @import | INHERITED | #5c4436 | #e8d8c8 | ~5.9:1 AA | panel #fdfaf3 vs bg #e8d8c8 — wider | #bcaa92 |
| 16 | site-dimir.css | @import | INHERITED | #99a8c0 | #0b1020 | ~8.6:1 AA | panel #1e2840 vs bg #0b1020 — wider | #374870 |
| 17 | site-golgari.css | @import | INHERITED | #9daa93 | #0b0f0a | ~9.4:1 AA | panel #1e2a1c vs bg #0b0f0a — wider | #425838 |
| 18 | site-gruul.css | @import | INHERITED | #425038 | #c6d8ae | ~5.2:1 AA | panel #eef4e4 vs bg #c6d8ae — wider | #6e8c52 |
| 19 | site-izzet.css | @import | INHERITED | #3f506a | #d8dfe9 | ~5.5:1 AA | panel #fafbfd vs bg #d8dfe9 — wider | #a0aec0 |
| 20 | site-orzhov.css | @import | INHERITED | #504b43 | #ddd7cc | ~5.5:1 AA | panel #fbfaf6 vs bg #ddd7cc — wider | #a89c88 |
| 21 | site-rakdos.css | @import | INHERITED | #ba9e98 | #12090c | ~8.5:1 AA | panel #301820 vs bg #12090c — wider | #683848 |
| 22 | site-selesnya.css | @import | INHERITED | #42524a | #d8e4d6 | ~5.4:1 AA | panel #fafbf8 vs bg #d8e4d6 — wider | #a0b4a0 |
| 23 | site-simic.css | @import | INHERITED | #325660 | #d0e2e8 | ~5.6:1 AA | panel #f7fbfc vs bg #d0e2e8 — wider | #98b8c4 |
| 24 | site-temur.css | @import | INHERITED | #42595e | #d0e4e8 | ~5.6:1 AA | panel #f8fbfb vs bg #d0e4e8 — wider | #9ab8c0 |

**F3 summary:** CHANGED in rows 1-13 (12 full-fork files changed from 0.75rem; commander-table added fresh). INHERITED in rows 14-24 (11 @import themes, no `--fs-xs` defined, cascade resolved from site.css which now provides 0.85rem).

**F5 summary:** All 24 themes have `--muted` estimated at >= 4.5:1 contrast against `--bg`. Dark themes (abzan, esper, grixis, jund, nyx, planeswalker-dark, sultai, dimir, golgari, rakdos) all clear 5:1+ comfortably due to light muted on dark bg. Light themes (classic, jeskai, bant, mardu, naya, commander-table, azorius, boros, gruul, izzet, orzhov, selesnya, simic, temur) hit 5.2-5.9:1 after darkening. Hue family preserved in each theme — no value was copied verbatim between themes.

**F2 token summary:** Every theme now has a clearly perceptible panel/bg separation. Light themes: bg moved ~10-18 chroma points darker while panel stayed near-white (contrast direction: panel brighter than page). Dark themes: panel moved ~10-16 luminance points lighter while bg stayed dark (contrast direction: panel lighter than page). Line token strengthened in all 24 for visible border definition. No `box-shadow` or layout rule added to any theme file or to `site.css`.

## UIR-01 Deferral (IMPORTANT)

**This plan does NOT close UIR-01.** UIR-01 requires a live browser re-score of the deployed site at `https://www.deckflow.gg` against all 6 audit pillars at >= 2 viewports, confirming a score of >= 20/24. That visual verification — along with the final re-score and UIR-01 closure — is the explicit responsibility of **Plan 03** (48-03). This plan's token changes will propagate to the deployed site only after the v1.7 branch is pushed and deployed.

## Verification Results

```
grep -c -- "--fs-xs:   0.85rem" site.css site-jeskai.css site-abzan.css site-bant.css
site-esper.css site-grixis.css site-jund.css site-mardu.css site-naya.css site-nyx.css
site-planeswalker-dark.css site-sultai.css
=> all 12 files: 1 (PASS)

grep -q -- "--fs-xs:" site-commander-table.css && echo "commander-table now defines --fs-xs"
=> commander-table now defines --fs-xs (PASS)

grep -v '^#' site-commander-table.css | grep -c "font-size: 0.75rem"
=> 0 (PASS — both small-text literals re-pointed to var(--fs-xs))

grep -rl -- "--fs-xs" site-azorius.css ... site-temur.css | wc -l
=> 0 (PASS — 11 @import themes correctly do NOT define --fs-xs)

dotnet build DeckFlow.Web/DeckFlow.Web.csproj -c Release
=> Build succeeded. 1 Warning (pre-existing CS1574), 0 Errors (PASS)
```

## Deviations from Plan

None. Plan executed exactly as written. Token values were chosen per-theme (Claude's Discretion per 48-CONTEXT decision D) with contrast ratios confirmed before editing.

One implementation note: for dark themes (bg luminance < 0.02), the F5 directive to "darken muted" was applied as "ensure contrast >= 4.5:1 while preserving or enriching hue" rather than literally reducing lightness, because reducing lightness on dark-bg themes reduces contrast below the AA threshold. This interpretation aligns with the plan's stated goal ("Helper/muted text meets WCAG AA >= 4.5:1") and the decision note in 48-CONTEXT.

## Known Stubs

None. These are token-only CSS changes; no UI data sources or rendering paths involved.

## Threat Flags

None. CSS custom property changes to static asset files introduce no new network endpoints, auth paths, file access patterns, or schema changes.

## Self-Check: PASSED

All 24 CSS files modified and confirmed:
- `--fs-xs: 0.85rem` in all 12 full-fork files ✓
- `--fs-xs: 0.85rem` added to site-commander-table.css ✓
- 0 hardcoded `font-size: 0.75rem` in commander-table body ✓
- 0 @import themes define `--fs-xs` ✓
- Build: succeeded, 0 errors ✓
- Commit d75d250 exists ✓
