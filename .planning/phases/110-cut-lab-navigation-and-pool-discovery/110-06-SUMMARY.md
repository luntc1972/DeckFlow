---
phase: 110
plan: 06
title: Cross-Theme Mobile e2e Verification (CLUP-07/08)
status: complete
completed: 2026-07-24
requirements_addressed: [CLUP-07, CLUP-08]
executor: codex (gpt-5.4 medium)
verifier: claude
checkpoint: approved (2026-07-24)
---

# Plan 110-06 Summary — Cross-Theme Mobile e2e + Human Sign-off

## What was built
A Playwright spec `DeckFlow.Web/e2e/cut-lab-nav-themes.spec.ts` with two tests plus a
blocking human review of the captured screenshots.

- **Task 1 (cross-theme mobile):** iterates Classic (`site.css`), Nyx (`site-nyx.css`), and
  Commander Table (`site-commander-table.css`) at 430×2200, reusing the smoke harness
  (admin-lock, `setToolEnabled('Cut Lab', true)`, oversizedPool). Per theme it asserts the
  anchor nav is sticky at top, pills are not clipped (`scrollWidth <= clientWidth`), the
  `.cutlab-anchor-nav` box does not overlap `.cutlab-sticky-bar` (sticky bar starts at/below the
  nav's bottom), the back-to-top button cannot obscure the nav, the pool filter sits above the
  table header, and a card-text `<details>` toggles open. Captures
  `cut-lab-nav-{classic,nyx,commander-table}-mobile.png`.
- **Task 2 (no-JS fallback, CLUP-07):** a `javaScriptEnabled:false` test asserting anchor
  `#hash` navigation scrolls the target, native `<details>` toggle open/closed, the
  `.cutlab-pool-filter` stays `hidden` while all `tr[data-cut-lab-card]` rows are visible, the
  native card-text `<details>` toggles open, and the server-authored native submit seam (D-04)
  posts and re-renders with no script.
- **Task 3 (checkpoint):** user reviewed all three mobile screenshots and approved
  (2026-07-24) — pills readable/opaque across all themes incl. the dark Commander Table fork, no
  text overlap, no sticky collision.

## Reviewer fixes to Codex's spec (verification caught 3 test defects)
1. **Back-to-top guard:** Codex asserted `#back-to-top-button` `toBeVisible()`, but it is
   `display:none` below 600px (site-mobile.css `@media max-width:600px`), so at 430px it is not
   rendered and cannot obscure the nav. Fixed to assert the real invariant: EITHER the button is
   displayed and non-overlapping, OR it is `display:none` (and thus cannot obscure).
2. **Pool-header locator:** `.conflicts-table thead` matched 6 tables (strict-mode violation);
   scoped to `#cut-lab-section-lock-pool .conflicts-table thead`.
3. **No-JS test structure:** the section-summary click matched 19 nested summaries (fixed to a
   direct-child `> summary`); the submit-fallback originally chained 3 sim-heavy no-JS decide
   POSTs to reach the 100-card Export gate then a full export POST, which blew the 120/240s
   timeout (the Export tab is the only submit-type tab and gates on exactly 100 cards). Reworked
   to prove the same D-04 native-submit seam via the accept decision form (a plain
   `<button type="submit">` posting to /cut-lab/decide with no script — the pattern
   cut-lab-structure's no-JS test already proves), run first on the fresh page; the
   collapse-toggle assertions re-expand collapsibles before the card-text check so the disclosure
   sits in an open section. Full 100-card Export-gate submit stays in manual/checkpoint coverage.

## Verification (claude)
- `npx tsc --noEmit` — clean.
- `npx playwright test e2e/cut-lab-nav-themes.spec.ts --workers=1` — 4/4 pass
  (2 tests × chromium-desktop + chromium-mobile). Run serialized: the two projects contend on the
  `/tmp/deckflow-admin-e2e.lock`, so `--workers=1` is required locally (known admin-e2e serialize
  pattern).
- Screenshots written for all three themes; human checkpoint APPROVED.
- EOL: spec is LF, no leftover diagnostics.

## Files changed
- DeckFlow.Web/e2e/cut-lab-nav-themes.spec.ts (new)
- .planning/ui-design/cut-lab/screenshots/cut-lab-nav-{classic,nyx,commander-table}-mobile.png (new)
