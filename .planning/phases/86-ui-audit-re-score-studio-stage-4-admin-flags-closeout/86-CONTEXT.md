# Phase 86: UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout - Context

**Gathered:** 2026-07-05
**Status:** Ready for planning
**Source:** Live-dogfood theme audit (3 read-only agents + Playwright ground-truth) — orchestrator-supplied

<domain>
## Phase Boundary — THIS PASS = THEME UI GAPS ONLY

This planning pass covers ONLY the theme/UI visual-polish gaps surfaced by live dogfooding + the
Phase-82 baseline UI audit. **The ONLY requirement in scope this pass is UIAUDIT-02** ("The enumerated
gaps are fixed and a final re-score confirms the site clears ≥20/24"). It delivers the four theme fixes
A/B/C/D, adds the visual-regression + interaction tests that the current e2e suite structurally cannot
catch, and re-runs the 6-pillar UI audit to ≥20/24.

**Explicitly OUT of scope this pass (DEFERRED to a later pass / separate plan):**
- **UIAUDIT-03** — DirectPush **Stage 4** live verify + `DirectPush.razor:441` no-op success-copy fix (Studio).
- **ADMIN-01** — `/Admin/Flags` on/off sorting.

ALL 5 plans in this pass carry `requirements: [UIAUDIT-02]` ONLY. UIAUDIT-03 and ADMIN-01 remain on Phase 86
in ROADMAP but are NOT planned here and are NOT satisfied by this plan set; the phase will not fully close
until a follow-up pass plans them. This is intentional and must be recorded in ROADMAP/STATE.

Context: Phase 85 (chatgpt→prompt rename) just shipped byte-identical and is PROVEN NOT to have caused
any of these bugs — all four are pre-existing. Unlike Phase 85, this phase is an INTENTIONAL visual
change: byte-identical render is NOT a goal.
</domain>

<decisions>
## Implementation Decisions (LOCKED)

### Bug A — active step-tab: filled-accent-pill (USER-CHOSEN)
- Redesign `.prompt-step-tab.is-active` to a **filled accent pill**: `background: var(--accent)` + contrasting
  text (`var(--accent-contrast, #fff)`), replacing today's weak "same-bg, border+text-tint only" active state.
- Template already exists at `site-mobile.css:353` (mobile circular stepper filled pill) — reuse its pattern.
- MUST fix the 5 dark-theme active-text WCAG fails: jund 2.19, dimir 2.67, sultai 3.00, golgari 3.49,
  planeswalker-dark 4.04. After the fill, verify each theme's `--accent` yields ≥4.5:1 with the chosen
  text color; add a per-theme `--accent-contrast` token where white is insufficient.
- Ground-truth: azorius + gruul already render acceptably (readable, active distinguishable) — the fill is a
  consistency+contrast upgrade, not a rescue, for the light @import themes.

### Bug B — tokenize the 3 hardcoded Jeskai-blue literals
- Replace the non-token `rgba(43,108,176,…)` at `site-common.css:792` & `:797` (`.prompt-layout-segment`
  hover/active), `site.css:308` (`[data-prompt-ui-mode-button].is-active` bg), `site.css:623`
  (`.clear-cache-button:hover` bg) with an `--accent`-derived token/tint so every theme uses its OWN accent.

### Bug C — checklist bucket toggle chevron + a11y
- `DeckAnalysis.cshtml:306` empty `<button class="prompt-question-bucket__toggle">` reads as a stray grey
  pill. Restyle `site.css:533-551` to a plain chevron (drop the standalone border, raise caret contrast/size)
  and ADD an `aria-label` (e.g. "Toggle {bucket} questions") — closes an accessibility gap.

### Bug D — layout picker Full/Compact/Advanced perceptible
- Wiring is intact; the effect is imperceptible on the empty landing. Make `focused`/`expert` produce an
  UNMISTAKABLE difference (collapse whole panels / switch column layout, not just hide sparse hint text) and
  give `guided`/Full a positive style rather than a do-nothing default. CSS/UX only — NO JS change.

### Test-gap closure (REQUIRED — this is why the bugs shipped green)
- Current e2e assert DOM/selectors exist, NOT visual state or interaction OUTCOMES. Add:
  - Per-theme visual-regression: step-tab nav active vs inactive across representative light + dark themes,
    asserting the active tab's computed `background-color` differs from inactive (proves the filled pill).
  - Interaction assertion: toggling Compact/Advanced produces a MEASURABLE layout delta (e.g. an element's
    visibility/box changes), not just a data-attribute flip.
- Harness proven from WSL: cookie `deckflow-theme=site-<theme>.css`, route `/deck-analysis`, `.prompt-step-nav`
  locator, playwright webServer `http-no-browser` profile (never opens a Windows browser).

### Architecture constraint (KEY — drives task sizing)
- 11 `@import` themes inherit base (azorius, boros, dimir, golgari, gruul, izzet, orzhov, rakdos, selesnya,
  simic, temur). 13 STANDALONE forks each DUPLICATE every base rule: site.css (Classic), abzan, bant, esper,
  grixis, jeskai, jund, mardu, naya, nyx, planeswalker-dark, sultai, commander-table.
- **Every base-rule change (A/B#2/B#3/C) MUST be mirrored into the 13 standalone forks.** The 11 @import
  themes pick it up for free. Plan tasks must account for this fan-out explicitly.

### Project guardrails (from CLAUDE.md)
- Layout CSS in `site-common.css`, tokens in `:root` of each theme file. Preserve LF line endings.
  Changed-lines format gate must pass (touch only the lines that need touching; no mass reflow). Never
  re-indent C# raw-string literals. Do NOT commit compiled `wwwroot/js/*.js`.
- Every changed/added page verified desktop + mobile across themes; UI phases need browser screenshot verify.

### Claude's Discretion
- Exact token name for the accent tint (B); precise chevron glyph/sizing (C); which panels collapse per
  mode and the exact column-layout switch (D); the representative theme subset for visual-regression (must
  include ≥1 light @import, ≥1 dark fork from the failing five, Classic).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Audit + root causes (PRIMARY)
- `.planning/phases/86-ui-audit-re-score-studio-stage-4-admin-flags-closeout/86-RESEARCH.md` — full audit:
  file:line root causes, contrast table, 11-@import vs 13-standalone-fork architecture, fix directions,
  validation architecture.

### Baseline UI audit (re-score target)
- `tasks/UI-REVIEW.md` — Phase-82 6-pillar baseline; re-score target ≥20/24 after fixes.

### Key source files
- `DeckFlow.Web/wwwroot/css/site.css` (`:311-325` step-tab, `:308` ui-mode-button, `:388-434` layout modes,
  `:533-551` bucket toggle, `:623` clear-cache), `site-common.css` (`:792/797` layout-segment, load order
  `_Layout.cshtml:73-76`), `site-mobile.css:353` (filled-pill template).
- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` (`:81-91` layout picker, `:300-307` bucket toggle).
- The 13 standalone fork CSS files (mirror targets) + 11 @import theme files.
- `DeckFlow.Web/e2e/` (playwright.config.ts `http-no-browser` webServer; bracket-smoke/print-button specs
  show the `deckflow-theme` cookie + theme-loop pattern to copy for visual-regression tests).
</canonical_refs>

<specifics>
## Specific Ideas
- Contrast fails (active-tab text): jund 2.19, dimir 2.67, sultai 3.00, golgari 3.49, pw-dark 4.04; marginal
  grixis 4.64, nyx 5.13.
- 10 @import themes lack own `.prompt-step-tab.is-active` (rakdos already has a filled pill = reference).
- izzet = barest theme (token-only); rakdos = most complete @import theme.
</specifics>

<deferred>
## Deferred Ideas
- Studio DirectPush Stage 4 verification (Phase 86 remit, not this pass).
- ADMIN-01 `/Admin/Flags` on/off sorting (Phase 86 remit, not this pass).
- Tier-3 polish: bringing the 10 bare @import themes up toward the rakdos template for step-nav / is-complete
  / question-checklist / cache-pill (render acceptably today; polish only).
</deferred>

---

*Phase: 86-ui-audit-re-score-studio-stage-4-admin-flags-closeout*
*Context gathered: 2026-07-05 via live-dogfood theme audit (orchestrator-supplied)*
