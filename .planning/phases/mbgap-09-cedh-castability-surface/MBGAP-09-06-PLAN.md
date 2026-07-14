---
phase: mbgap-09-cedh-castability-surface
plan: 06
type: execute
wave: 5
depends_on: [05]
files_modified:
  - DeckFlow.Web/Views/Deck/Manabase.cshtml
  - DeckFlow.Web/wwwroot/css/site-common.css
  - DeckFlow.Web.Tests/Playwright/manabase-interaction-lens.spec.ts
autonomous: false
requirements: [D-03, D-09, D-10, D-11, D-12]
must_haves:
  truths:
    - "cEDH mode renders a third 'Early interaction' lens in the header strip; single/dual states still work (D-10)"
    - "The lens shows worst-5 spells + a <details> 'view all' expander disclosing the remainder count (D-11, L2)"
    - "cEDH mode renders the full per-card castability table with a holdable badge column on interaction rows only; the v1 mode-note is removed (D-09, D-12)"
    - "Zero qualifying spells renders a caution-styled empty state, not a hidden lens (D-03)"
    - "The 3-up lens grid is responsive and collapses to one column on mobile; layout CSS lives in site-common.css (D-10)"
  artifacts:
    - path: "DeckFlow.Web/Views/Deck/Manabase.cshtml"
      provides: "third lens section, table holdable column, mode-note removal, formula-panel coverage"
      contains: "manabase-early-interaction"
    - path: "DeckFlow.Web/wwwroot/css/site-common.css"
      provides: "manabase-twolens 3-up responsive rule"
      contains: "manabase-twolens--triple"
  key_links:
    - from: "Manabase.cshtml lens strip"
      to: "report.InteractionLens"
      via: "third <section id=manabase-early-interaction> gated on ShowCedhInteractionLens + cEDH + non-null lens"
      pattern: "manabase-early-interaction"
    - from: "RenderCastabilityTable"
      to: "lens qualifying set"
      via: "holdable <td> rendered only for interaction rows"
      pattern: "data-label"
---

<objective>
Ship the cEDH early-interaction UI: a third "Early interaction" lens in the header strip, the full per-card castability table in cEDH mode (v1 note removed) with a holdable badge column on interaction rows, the worst-5 + `<details>` expander, the caution empty state, the responsive 3-up grid CSS, and the formula-panel coverage. Verify across themes at two viewports (project UI rule).

Purpose: This is the user-facing surface. All prior plans exist to feed it.
Output: Razor view + CSS + Playwright spec + 2-viewport screenshots, gated by a human-verify checkpoint.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/phases/mbgap-09-cedh-castability-surface/MBGAP-09-CONTEXT.md
@.planning/phases/mbgap-09-cedh-castability-surface/MBGAP-09-PATTERNS.md
@./CLAUDE.md

<interfaces>
From Plan 05: Model.ShowCedhInteractionLens (bool), Model.ShowCastability (now cEDH-aware),
ManabaseDisplay.InteractionHoldableMarker(percent, threshold), ManabaseDisplay.CedhInteractionLensGloss,
ManabaseDisplay.DefaultVisibleInteractionCount (5).
From Plan 01/02: report.InteractionLens { QualifyingCount, OnTargetCount, Threshold,
IReadOnlyList<ManabaseInteractionRow> Rows{ Name, HoldablePercent, IsCostOverridden } }.

View anchors (Manabase.cshtml):
- Lens-strip conditionals + resultNavItems anchor list (207-240): showLeftLens/showRightLens.
- RenderCastabilityTable local function (242-281): `<table class="manabase-table castability-table manabase-table--card">`,
  per-cell `data-label` attrs (REQUIRED — the 640px card-stack ::before injects data-label; a missing one silently fails).
- manabase-twolens wrapper + per-lens section structure (430-489): manabase-lens-label -> manabase-lens-big ->
  manabase-lens-row -> manabase-lens-note -> optional manabase-lens-gloss.
- Castability table progressive-disclosure block (800-814): worst-N visible + `<details><summary>Show all N ...</summary>`.
- cEDH mode-note to REMOVE (820-823): "Castability view is available in Casual mode."
- Formula panels (873-938): "How the analysis works" + "This deck's numbers" (per-term <ul class="manabase-formula-terms">).

CSS anchors (site-common.css): .manabase-twolens (2669-2675), .manabase-twolens--single (2992-2994),
640px collapse (3006-3008), .manabase-lens uses var(--panel-soft-bg, var(--panel)) (2678),
print-region selector (3775-3796). Layout CSS goes in site-common.css ONLY; dark themes use --panel not --theme-surface.

Testing: start server with scripts/run-web-test.sh (sets DECKFLOW_DISABLE_AUTO_BROWSER=true);
run Playwright headless via `env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test` with headless:true.
Never open a browser on the Windows host. Do NOT commit compiled wwwroot/js.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Third lens section, holdable table column, mode-note removal, formula panels</name>
  <read_first>
    - DeckFlow.Web/Views/Deck/Manabase.cshtml (lens strip 207-240; RenderCastabilityTable 242-281; twolens block 430-489; details expander 800-814; mode-note 820-823; formula panels 873-938)
    - DeckFlow.Web/Models/ManabaseDisplay.cs (InteractionHoldableMarker, CedhInteractionLensGloss, DefaultVisibleInteractionCount)
    - DeckFlow.Web/Models/ManabaseViewModel.cs (ShowCedhInteractionLens, ShowCastability)
  </read_first>
  <action>
    (a) Add `var showInteractionLens = Model.ShowCedhInteractionLens && report.Mode == ManabaseMode.Cedh && report.InteractionLens is not null;` and a third resultNavItems entry ("manabase-early-interaction", "Early interaction", showInteractionLens).
    (b) Inside the existing `<div class="manabase-twolens ...">` wrapper add a third `<section id="manabase-early-interaction" class="manabase-lens">` when showInteractionLens: a manabase-lens-label ("Early interaction"), a manabase-lens-big headline rendering `report.InteractionLens.OnTargetCount / report.InteractionLens.QualifyingCount` + "interaction held up by turn 3", then the worst rows (Rows are worst-first) capped to DefaultVisibleInteractionCount using InteractionHoldableMarker(row.HoldablePercent, report.InteractionLens.Threshold) for the badge, a manabase-lens-note carrying the raw-availability caveat, and manabase-lens-gloss = CedhInteractionLensGloss when plain-language is on. When QualifyingCount == 0 render the D-03 caution (manabase-lens-short styling, ⚠, "no cheap interaction found") — do NOT gate the section off on empty rows.
    (c) When more than DefaultVisibleInteractionCount rows exist, add a `<details>` "view all" expander (copy the 800-814 pattern) whose summary discloses the hidden remainder count (never silent truncation, L2).
    (d) Update the twolens wrapper's modifier-class logic so it applies the new triple modifier when all three lenses show, single when one, default (2-up) when two.
    (e) Remove the mode-note block at 820-823 (D-09).
    (f) In RenderCastabilityTable add a conditional `<td data-label="Held up (T1-3)">` rendered only for interaction rows (look up membership via the lens's qualifying set / a Name->HoldablePercent map derived from report.InteractionLens.Rows), showing a manabase-chip badge via InteractionHoldableMarker; non-interaction rows render an empty cell WITH the same data-label so the mobile card-stack stays aligned. Keep the existing worst-first sort.
    (g) Update both formula panels (873-938) to cover the new metric: "How the analysis works" gains a sentence on the interaction lens; "This deck's numbers" gains a term line with the deck's plugged-in numbers (e.g. "N of M interaction spells qualified (PlanRole.Interaction, effective MV <= 2); X held up by turn 3 at the 88% threshold"), rendered only in cEDH mode when the lens is present.
    Use Razor auto-encoding for all card names (@row.Name — never Html.Raw). All card-name output must be HTML-encoded.
  </action>
  <verify>
    <automated>grep -n "manabase-early-interaction\|Held up\|InteractionHoldableMarker\|Castability view is available" DeckFlow.Web/Views/Deck/Manabase.cshtml; echo "expect: mode-note string ABSENT"</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c "Castability view is available in Casual mode" Manabase.cshtml` returns 0 (mode-note removed).
    - The third section id manabase-early-interaction and the "Held up (T1-3)" data-label are present.
    - Card names are emitted via Razor encoding (no Html.Raw on lens/table card names — grep shows none introduced).
    - `dotnet build DeckFlow.Web` clean (Razor compiles), 0 new warnings.
  </acceptance_criteria>
  <done>cEDH renders the third lens + full table + badge; empty-state caution; formula panels cover the metric; mode-note gone.</done>
</task>

<task type="auto">
  <name>Task 2: 3-up responsive grid CSS in site-common.css</name>
  <read_first>
    - DeckFlow.Web/wwwroot/css/site-common.css (.manabase-twolens 2669-2675; --single 2992-2994; 640px collapse 3006-3008; .manabase-lens 2678; print-region 3775-3796)
    - ./CLAUDE.md (layout CSS in site-common.css only; dark themes use --panel)
  </read_first>
  <action>
    Add `.manabase-twolens--triple { grid-template-columns: repeat(3, 1fr); }` (or convert the base rule to repeat(auto-fit, minmax(...)) if cleaner) so the three-lens state lays out 3-up on desktop. Confirm the existing 640px breakpoint collapses the triple state to a single column (it targets `.manabase-twolens`, so it already applies — verify, add nothing new unless it does not). Use only --panel/--line/--muted fallback tokens (no --theme-surface) so all 24 theme forks inherit correctly. Verify the print-region selector (3775-3796) covers the new section via its class targets; add the class only if a new distinct class was introduced. Put ALL of this in site-common.css, never site.css.
  </action>
  <verify>
    <automated>grep -n "manabase-twolens--triple\|manabase-twolens" DeckFlow.Web/wwwroot/css/site-common.css; grep -c "manabase-twolens" DeckFlow.Web/wwwroot/css/site.css</automated>
  </verify>
  <acceptance_criteria>
    - The triple modifier rule exists in site-common.css; `grep -c manabase-twolens site.css` returns 0 (no layout CSS leaked into site.css).
    - No --theme-surface introduced in the new rule (grep shows --panel usage).
    - 640px collapse confirmed to apply to the triple state.
  </acceptance_criteria>
  <done>The 3-up grid is responsive, themed via site-common.css tokens, and mobile-collapsing.</done>
</task>

<task type="auto">
  <name>Task 3: Playwright spec + cross-theme, two-viewport screenshots</name>
  <read_first>
    - DeckFlow.Web.Tests/Playwright/ (existing manabase or lens specs for the harness pattern, server URL, theme-switch mechanism, admin/env setup)
    - CLAUDE.md testing constraints (run-web-test.sh, DECKFLOW_DISABLE_AUTO_BROWSER, WSL headless env, no Windows browser)
  </read_first>
  <action>
    Add a Playwright spec that, against the headless server (started via scripts/run-web-test.sh), submits a cEDH deck containing cheap interaction and asserts: the third lens (#manabase-early-interaction) renders with the N/M headline; the castability table renders in cEDH mode with the "Held up (T1-3)" column on interaction rows; the worst-5 + `<details>` expander is present; and in Casual mode (or with the flag conceptually off) the third lens is absent. Capture desktop (>=1280px) and mobile (<=390px) screenshots for a representative set of themes (at least one light + one dark fork) into the phase screenshot directory, confirming no horizontal overflow on the 3-up strip or the table. Run with `env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test ... headless`. Do not commit compiled wwwroot/js.
  </action>
  <verify>
    <automated>env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test DeckFlow.Web.Tests/Playwright/manabase-interaction-lens.spec.ts --reporter=line (start the server first per run-web-test.sh; record result + screenshot paths in SUMMARY)</automated>
  </verify>
  <acceptance_criteria>
    - Spec asserts third-lens presence in cEDH and absence in Casual, table column presence, and expander presence.
    - Desktop + mobile screenshots captured for >=1 light and >=1 dark theme; no horizontal overflow.
    - No compiled wwwroot/js staged.
  </acceptance_criteria>
  <done>Automated UI checks pass and screenshots exist for the human-verify checkpoint.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <what-built>The cEDH "Early interaction" third lens, the full cEDH castability table with the holdable badge column, the worst-5 + view-all expander, the caution empty state, and the responsive 3-up grid — verified by Playwright and captured as two-viewport screenshots across a light and a dark theme.</what-built>
  <how-to-verify>
    1. Review the desktop + mobile screenshots produced by Task 3 (paths listed in the SUMMARY).
    2. Confirm the 3-up lens strip reads cleanly on desktop and collapses to one column on mobile with no horizontal overflow, in both the light and dark theme shots.
    3. Confirm the third lens headline reads "N / M interaction held up by turn 3", the worst rows show holdable % + met/short glyphs, and the caveat caption is visible.
    4. Confirm the cEDH castability table renders with the "Held up (T1-3)" badge on interaction rows and the mode-note ("available in Casual mode") is gone.
    5. Optionally run a cEDH deck with zero cheap interaction and confirm the caution empty state renders instead of a hidden lens.
  </how-to-verify>
  <resume-signal>Type "approved" or describe rendering issues to fix.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| deck data (card names) -> rendered HTML | Card names from imported/pasted decklists are rendered into new lens + table markup |
| feature flag -> rendered surface | Flag + cEDH mode gate whether the lens/table appear |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-MBGAP09-12 | Tampering (XSS) | Card names in the third lens + new table cells | mitigate | Razor auto-encoding for all @Name output; no Html.Raw introduced (grep-verified); acceptance criterion enforces it |
| T-MBGAP09-13 | Information Disclosure | Lens/table rendering when flag off | mitigate | Section + table gated on Model.ShowCedhInteractionLens && cEDH; Playwright asserts absence in Casual |
| T-MBGAP09-14 | Denial of Service | Mobile overflow / broken layout across 24 themes | accept-with-verification | site-common.css token-only rule + 640px collapse; two-viewport cross-theme screenshots + human-verify checkpoint gate it |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean (Razor compiles).
- Playwright spec green; desktop+mobile screenshots captured (light+dark).
- `grep -c` confirms mode-note removed and no layout CSS in site.css.
- Human-verify checkpoint approved.
</verification>

<success_criteria>
cEDH shows the third lens + full castability table + holdable badge + worst-5 expander + caution empty state; the 3-up grid is responsive and themed; Casual/flag-off is unchanged; verified across themes at two viewports.
</success_criteria>

<output>
Create `.planning/phases/mbgap-09-cedh-castability-surface/MBGAP-09-06-SUMMARY.md` when done.
</output>
