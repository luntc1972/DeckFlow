---
phase: 107-cut-lab-tech-debt-cleanup
plan: 02
subsystem: web-ui
tags: [cut-lab, theme-css, accessibility, contrast, mobile, cosmetic]
requires: []
provides:
  - AA-passing --cutlab-delta-up/down :root overrides across the 9 remaining dark themes
  - button-scoped button.manabase-pill.is-selected rule (radio-pill :has() UX untouched)
  - shortened mobile pool-row "Package" data-label (no truncation)
  - decisive Nyx-mobile commander-badge closure (screenshot-backed, no overlap)
affects: [cut-lab, theming, accessibility, mobile]
tech-stack:
  added: []
  patterns: [per-theme :root token override on an existing token seam, button-variant-scoped selected-state rule]
key-files:
  created:
    - .planning/ui-design/cut-lab/screenshots/nyx-mobile-commander-badge-107-02.png
    - .planning/ui-design/cut-lab/screenshots/nyx-mobile-lockpool-107-02.png
  modified:
    - DeckFlow.Web/wwwroot/css/site-abzan.css
    - DeckFlow.Web/wwwroot/css/site-dimir.css
    - DeckFlow.Web/wwwroot/css/site-esper.css
    - DeckFlow.Web/wwwroot/css/site-golgari.css
    - DeckFlow.Web/wwwroot/css/site-grixis.css
    - DeckFlow.Web/wwwroot/css/site-jund.css
    - DeckFlow.Web/wwwroot/css/site-planeswalker-dark.css
    - DeckFlow.Web/wwwroot/css/site-rakdos.css
    - DeckFlow.Web/wwwroot/css/site-sultai.css
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web/Views/Deck/CutLab.cshtml
key-decisions:
  - "Used #4ade80 (up) / #fc8181 (down) — closed-form WCAG worst-case 6.99:1 / 4.99:1 across all 9 dark panels; deliberately did NOT reuse Nyx's #f87171 (fails AA 4.41:1 on sultai + planeswalker-dark)."
  - "Scoped the new selected-state rule to button.manabase-pill.is-selected (button variant only). A bare .manabase-pill.is-selected would regress the radio-pill UX (stale double-highlight) since Bracket/PlayExperience radio labels also carry is-selected; those stay :has(> input:checked)-driven."
  - "Shortened only the mobile data-label (Package assignment -> Package); left the desktop <th> and the packages hint sentence unchanged."
  - "Nyx-mobile commander-badge overlap: CLOSED-WITH-REASON. Live Playwright capture at nyx/mobile shows the 'Commander . Always locked' badge sitting cleanly in grid-column 2 (existing site-common.css:1254-1257 override already resolves it) — no overlap, no truncation. No CSS fix applied."
  - "Item-4 sub-items xmldoc garble (CutLabPoolValidator.cs) and Manabase castability-copy leak: CLOSED-WITH-REASON, confirmed already-fixed earlier this session (grep = 0 hits for the stale strings)."
patterns-established:
  - "Dark-theme Cut Lab delta colors ride the pre-existing --cutlab-delta-up/down token seam; each dark theme carries the pair in :root, layout stays in site-common.css."
  - "Selected-state styling for non-radio pill buttons is button-scoped to avoid colliding with the deliberately-:has()-only radio-pill selection."
requirements-completed: [CLEANUP-3, CLEANUP-4]
---

# 107-02 Summary — AA dark-theme delta tokens + button-scoped selected pill + mobile label + Nyx-badge closure

## What changed
- **Item 3 (CLEANUP-3):** Added `--cutlab-delta-up: #4ade80;` + `--cutlab-delta-down: #fc8181;` to the `:root` of all 9 remaining dark themes (abzan, dimir, esper, golgari, grixis, jund, planeswalker-dark, rakdos, sultai). Nyx + 12 light themes + the token seam untouched.
- **Item 4 (CLEANUP-4):**
  - Added a **button-scoped** `button.manabase-pill.is-selected` rule in site-common.css mirroring the `:has(> input:checked)` selected treatment (accent bg/border, on-accent color, weight 600) so the Lock-all-`<role>` button shows a locked state. Radio pills unchanged.
  - Shortened the mobile pool-row `data-label` to `Package` (desktop header + hint text unchanged).
  - **Nyx-mobile commander-badge overlap → closed-with-reason** (screenshot: `nyx-mobile-commander-badge-107-02.png`): no overlap; the existing grid override handles it.
  - **xmldoc garble + Manabase-copy leak → closed-with-reason**, already fixed earlier this session.

## Verification
- `dotnet build DeckFlow.sln`: 0 errors (9 pre-existing CS8629 warnings in `ManabaseBaselineWeightingTests.cs` — unrelated, not from touched files).
- Acceptance greps: 9 themes × 2 delta tokens; zero `#f87171` in the 9 files; only `button.`-prefixed selected rule (no bare); `:has(> input:checked)` rule byte-unchanged; `data-label="Package assignment"` gone from the mobile row (desktop `<th>` retained).
- EOL: `git diff --stat` == `--ignore-all-space --stat` (27/27) — no churn; all touched files LF (preserved).
- Visual checkpoint: **APPROVED** by user (2026-07-22). Nyx-badge decisive outcome recorded above; delta colors AA-proven closed-form.

## Deviations
None. Codex executed Tasks 1+2; the orchestrator ran the human-verify checkpoint (Playwright headless nyx/mobile capture) and authored this SUMMARY.

## Commit
- `8e92c436` — `fix(107-02): AA dark-theme delta tokens + button-scoped selected pill + mobile label`
