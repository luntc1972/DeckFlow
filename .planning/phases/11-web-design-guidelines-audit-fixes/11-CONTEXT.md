# Phase 11: Web Design Guidelines Audit Fixes - Context

**Gathered:** 2026-05-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Land all 10 audit-driven sweep PRs from `.planning/quick/260513-wdg-web-design-guidelines-audit-findings/260513-wdg-FINDINGS.md`. Scope: P1 accessibility bugs (admin focus-visible, df-typeahead keyboard nav, ARIA tablist server-render, CSP inline-handler removal, info-tooltip a11y) + P2 guideline violations (table semantics, autocomplete attrs, Razor `selected=` bool sweep, cross-cutting `site-common.css` foundation, AdminHarvest live region).

10 REQ-IDs (WDG-01..10) — all pre-grounded in `FINDINGS.md` with explicit file:line references. No new capabilities. No URL or class renames (those are Phase 12 + 13).

</domain>

<decisions>
## Implementation Decisions

### Sweep packaging
- **D-01:** Ship 10 separate PRs (one per FINDINGS.md sweep). Maximum review granularity + clean revert points. Heavier PR overhead accepted for traceability — each PR maps to one WDG-* requirement.

### Sweep execution order
- **D-02:** Follow FINDINGS.md leverage-first ordering. Sweep 1 (site-common.css cross-cutting: color-scheme, prefers-reduced-motion, touch-action, tabular-nums utility, scroll-margin-top) ships FIRST as foundation. Sweep 2 (admin.css universal `:focus-visible` block) builds on Sweep 1. View-layer sweeps (3-10) depend on the foundation being in place.

### Verification strategy
- **D-03:** Batch UAT at phase end. Per-sweep verification = `dotnet build DeckFlow.sln --configuration Release` clean only (no warnings). After all 10 sweeps land on `v1.3`, single UAT pass covers: Tab-navigation across admin shell + main shell, screen reader spot-check on autocomplete + ARIA tablist + AdminHarvest live region, mobile + dark-theme smoke check.
- **D-04:** User runs dev server (per CLAUDE.md feedback memory `[[feedback_user_starts_server]]`); Claude does NOT auto-launch web. UAT handoff happens after all 10 sweeps committed to `v1.3`.

### Risk gating — CSP inline-handler removal (WDG-04)
- **D-05:** AdminFeedback Detail Delete button (`Views/AdminFeedback/Detail.cshtml:39-43`) is DEFERRED out of Phase 11. WDG-04 covers all OTHER inline handler removals (`Error.cshtml` inline `style`, `AdminFeedback/Index.cshtml` `onchange="this.form.submit()"`) but leaves the Delete `onsubmit="return confirm(...)"` in place. Reason: removing the inline handler + CSP-blocking `confirm()` = instant delete with no prompt — security/UX regression risk. Defer to v1.4 with a proper modal pattern.
- **D-06:** Add a brief comment in `Detail.cshtml:39` noting the deferral and linking to `260513-wdg-FINDINGS.md` so the gap is documented in code.

### Sub-sweep specifics (locked by FINDINGS.md, restated for downstream agents)
- **D-07:** Cross-cutting CSS rules in Sweep 1 go in `site-common.css` (NOT `site.css`), per CLAUDE.md constraint "layout CSS must go in `site-common.css`, not `site.css`". Affects: `color-scheme: light dark` on `:root`, global `@media (prefers-reduced-motion: reduce)`, `button, a, summary { touch-action: manipulation }`, `.tabular { font-variant-numeric: tabular-nums }`, `h1, h2, h3, [id] { scroll-margin-top: 4rem }`. All 22 guild themes inherit without per-fork edit.
- **D-08:** `df-typeahead.ts` ARIA combobox refactor (Sweep 5) adds full pattern: `role="combobox"` + `aria-autocomplete="list"` + `aria-expanded` + `aria-controls` + `aria-activedescendant` on input; `role="option"` on each suggestion button; ArrowDown/Up/Enter/Escape handlers. Shared module — fix lands once, all 5 typeahead consumers (SuggestCategories, DeckConvert, JudgeQuestions, CommanderCategories, CardLookup) benefit.
- **D-09:** Razor `selected="@(condition)"` sweep (Sweep 3) applies the `selected="@(condition ? "selected" : null)"` pattern from v1.2 commit `32bf620`. Affects: `DeckSync.cshtml:51-54,61-62,68-70,93-94,128-129`, `DeckConvert.cshtml:32-33,38-41,45-48`, `SuggestCategories.cshtml:40-43,88-89`, `AdminHarvest/Index.cshtml:40,90`.
- **D-10:** Info-tooltip a11y (Sweep 8) converts both `SuggestCategories.cshtml:161` and `CommanderCategories.cshtml:67` from `<span class="info-tooltip" title="…">i</span>` to `<details><summary>i</summary><p>…</p></details>` pattern. Simpler than button + aria-describedby; no JS dependency; works keyboard + SR out of the box.

### Claude's Discretion
- Specific URL slug for `tabular` utility class name in CSS (`.tabular` vs `.tabular-nums` — pick whichever matches existing naming convention in `site-common.css`).
- Specific timing values for `prefers-reduced-motion` reduction (use `0.01ms` per W3C convention; user-overridable).
- Exact ARIA label text on `df-typeahead` combobox role (researcher/planner can pick context-appropriate strings).
- Final structure of the audit-deferral comment in `Detail.cshtml:39` (single-line `<!-- … -->` is fine).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Audit source (drives all 10 sweeps)
- `.planning/quick/260513-wdg-web-design-guidelines-audit-findings/260513-wdg-FINDINGS.md` — All 10 sweep PRs already grouped with file:line references; sequenced by leverage; P1/P2/P3 classification. This is the source of truth for what each sweep changes and why.

### Web Interface Guidelines (external spec)
- https://raw.githubusercontent.com/vercel-labs/web-interface-guidelines/main/command.md — The guideline rule set that drove the audit. Researcher should pull a fresh copy if planning needs to verify a specific rule.

### v1.3 milestone artifacts
- `.planning/REQUIREMENTS.md` §`Frontend Hardening` — WDG-01..10 requirement text and traceability.
- `.planning/ROADMAP.md` §`Phase 11` — Phase goal, success criteria, dependencies.

### Project constraints (constrains all CSS work)
- `CLAUDE.md` §`Constraints` — "Theme system: Guild themes are full standalone CSS forks; layout CSS must go in `site-common.css`, not `site.css`" (D-07).
- `CLAUDE.md` §`Constraints` — "Testing: VSTest unreliable in WSL; rely on `dotnet build` clean + targeted manual harness or push-and-watch CI" (D-03).
- `CLAUDE.md` §`Constraints` — "Commits: Plain default-author commits, no Co-Authored-By trailer".

### Pattern precedent
- v1.2 commit `32bf620` — Razor `selected="@(x ? "selected" : null)"` pattern fix. Drives D-09. Use `git show 32bf620` to see exact diff.
- `DeckFlow.Web/wwwroot/css/site.css:109-118` — Universal `:focus-visible` outline block. Used as template for admin.css mirror in Sweep 2.
- `DeckFlow.Web/wwwroot/css/site.css:1373-1383` — Existing `@media (prefers-reduced-motion: reduce)` block. Sweep 1 generalizes this into `site-common.css`.

### Affected file inventory (for planner dependency graph)
- HTML/Razor: `Views/Deck/*.cshtml` (8 views), `Views/Shared/*.cshtml` (11 partials), `Views/Admin*/*.cshtml` (6 admin views), `Views/Commander/`, `Views/About/`, `Views/Feedback/`, `Views/Help/`.
- CSS: `wwwroot/css/site.css`, `wwwroot/css/site-common.css`, `wwwroot/css/admin.css`, `wwwroot/css/site-mobile.css`, `wwwroot/css/site-theme-overrides.css`.
- TS: `wwwroot/ts/df-typeahead.ts` (Sweep 5 — shared module).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `site.css:109-118` universal `:focus-visible` block — copy/paste pattern into `admin.css` for Sweep 2 (WDG-01).
- `site.css:1373-1383` existing `prefers-reduced-motion` block — generalize and move into `site-common.css` for Sweep 1 (WDG-08).
- `df-typeahead.ts:50-56` `createTypeaheadPanel` + `attachTypeahead` shared module — fix lands once, 5 consumers benefit (WDG-02 Sweep 5).
- `_WorkflowStepTabs.cshtml` — already accepts `currentStep` via `WorkflowStepTabsModel`; pre-select logic just needs to wire `aria-selected`/`tabindex` to `step.Step == currentStep` (WDG-03 Sweep 9).

### Established Patterns
- CLAUDE.md: layout CSS goes in `site-common.css` (not theme forks). Sweep 1 honors this.
- v1.2 commit `32bf620` established `selected="@(x ? "selected" : null)"` pattern. Sweep 3 propagates it.
- ASP.NET 10 + Razor — no framework migration. All renamed handlers/CSP fixes via standard Razor + JS event-listener pattern.
- 22 guild theme forks: cross-cutting a11y changes belong in `site-common.css` only — NEVER edit individual theme files.

### Integration Points
- `admin.css` (loaded by `_AdminLayout.cshtml:14`) does NOT load `site.css` or `site-common.css` — admin is a separate stylesheet. Sweep 2's focus-visible block must be added DIRECTLY to `admin.css`, not inherited.
- `df-typeahead.ts` is loaded as `~/js/df-typeahead.js` (compiled by MSBuild TS task). Sweep 5 ARIA refactor must compile clean under TypeScript 6 + `strict: true`.
- AdminHarvest live region (`#harvest-status-live`) in `Views/AdminHarvest/Index.cshtml:54` updated by `wwwroot/ts/admin-harvest.ts:151 render()` via AJAX poll. Sweep 10's `role="status" aria-live="polite"` addition is HTML-only — no JS changes needed.
- Render auto-deploys `main` to `www.deckflow.gg`. Phase 11 commits to `v1.3` branch only; NO auto-deploy until v1.3 merges to main at end of milestone.

</code_context>

<specifics>
## Specific Ideas

- D-10: prefer `<details><summary>i</summary><p>…</p></details>` over `<button aria-describedby>` for info-tooltip a11y — no JS, keyboard + SR work out of the box.
- D-07: cross-cutting a11y rules land in `site-common.css` once, not 22 times across theme forks.
- Pattern precedent priority: v1.2 commit `32bf620` is the canonical "selected=True" fix; every Razor select sweep in Phase 11 should match it exactly.

</specifics>

<deferred>
## Deferred Ideas

### v1.4+ candidates
- **AdminFeedback Detail Delete CSP cleanup** (per D-05): inline `onsubmit="return confirm(...)"` stays in place until v1.4 ships a proper styled-and-focus-trapped JS confirm modal. WDG-04 covers all OTHER inline-handler removals.
- **P3 polish items from FINDINGS.md** — straight quotes → curly quotes sweep, `&nbsp;` insertion between values/units, hardcoded date formats → `<time datetime>`, "N/A" → "—" em-dash, semver `translate="no"`, `(pointer: coarse)` media queries, `env(safe-area-inset-*)` for iOS notch, mobile back-to-top retention. Roll into a v1.4 polish phase OR per-quick-task as cycles allow.
- **20 unreviewed guild theme stylesheets** (only `site-nyx.css` + `site-planeswalker-dark.css` spot-checked in audit) — assume Sweep 1's `site-common.css` foundation covers them all; if site-common.css inheritance doesn't fix Nyx/Planeswalker rendering, address in v1.4.
- **Full code review of 5 large TS modules** (deck-sync, df-select, site, card-lookup, category-suggestions) — sampled in audit but not full-read. v1.4 candidate.

</deferred>

---

*Phase: 11-Web Design Guidelines Audit Fixes*
*Context gathered: 2026-05-13*
