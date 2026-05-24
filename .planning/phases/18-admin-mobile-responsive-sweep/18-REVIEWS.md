---
phase: 18
reviewers: [codex]
reviewed_at: 2026-05-24T18:52:23Z
plans_reviewed: [18-01-PLAN.md, 18-02-PLAN.md]
---

# Cross-AI Plan Review — Phase 18

> Reviewer: Codex (primary per project CLAUDE.md). Claude self-review skipped for independence (running inside Claude Code).

## Codex Review

Using `plan-eng-review` and `plan-design-review` lenses.

## Summary

The plans are unusually thorough and mostly aligned with the phase goal, but they overstate what CSS-only contracts prove and under-specify the hardest parts: actual 320px behavior, complete touch-target coverage, cache busting for imported CSS, and accessible table/card transformations. I would not execute as-is. The main risk is not implementation complexity; it is false confidence from grep-based verification while the real failures are visual, cache, and assistive-tech edge cases.

## Plan 18-01

### Strengths

- Good dependency split: CSS contracts first, Razor activation second.
- Correctly identifies the dead `site-common.css` admin-feedback rules as an architecture bug.
- Keeps scope to CSS and avoids new packages or JavaScript.
- Adds reusable contracts for future ContentSources/ContentHarvest pages.
- Explicit token substitution map reduces theme/admin token confusion.

### Concerns

- **[HIGH] Touch-target coverage is incomplete.** The plan only calls out action-form buttons, sidebar links, analytics range links, and modal buttons. It appears to miss feedback filter links, pagination links, table action links like “View”, `<summary>`, inputs/selects, and any plain admin buttons outside `.admin-action-form`.

- **[HIGH] Scoping requirement is not actually enforced.** AMOB-04 says new admin selectors must be scoped to `.admin-shell` or layered. The migrated selectors are planned as `.admin-feedback`, `.type-badge`, `.detail-grid`, `.admin-table-scroll`, etc., without `.admin-shell`. That contradicts the phase’s own bleed-prevention rule.

- **[MEDIUM] `@import` shim breaks reliable cache busting.** `_AdminLayout.cshtml` gets `asp-append-version` for `admin.css`, but `admin-common.css` and `admin-mobile.css` imported from CSS will not get ASP.NET fingerprint query strings. If static CSS caching is aggressive, a deploy can serve a new shim pointing to stale imported files.

- **[MEDIUM] 18-01 claims behavior that cannot exist until 18-02.** “Sidebar collapses to `<details>/<summary>`” is listed as a Plan 01 truth, but Plan 01 does not touch Razor. It should say “CSS contract exists”; Plan 02 is where AMOB-01 becomes true.

- **[MEDIUM] The CSS verification is too grep-heavy.** Build will not catch malformed CSS, unreachable selectors, overflow, contrast, focus visibility, or assistive-tech behavior.

- **[LOW] WCAG wording is inaccurate.** W3C lists WCAG 2.5.5 Target Size Enhanced as Level AAA with 44x44 CSS pixels, while WCAG 2.5.8 Target Size Minimum is Level AA with 24x24 plus exceptions. The project can still require 44px, but the compliance language should be corrected.

### Suggestions

- Add an explicit admin interactive-element inventory and require selectors for each: links, buttons, summary, inputs, selects, pagination, filters, table actions, modal controls.
- Scope migrated component selectors as `.admin-shell .admin-feedback...`, `.admin-shell .type-badge`, `.admin-shell .detail-grid`, etc.
- Prefer two `<link asp-append-version>` tags over CSS `@import`, or add a deployment/cache note that imported CSS is not independently fingerprinted.
- Add a CSS syntax/static check if available, even a simple browser smoke check, not just `grep`.
- Correct WCAG language: “44px is DeckFlow’s stricter product floor aligned with WCAG 2.5.5 AAA; WCAG 2.5.8 AA minimum is 24px.”

### Risk Assessment

**MEDIUM-HIGH.** The architecture is sensible, but the acceptance criteria can pass while still missing the phase goal: “all admin interactive elements” and “scoped admin CSS” are not proven.

## Plan 18-02

### Strengths

- Correctly wires the CSS contracts into Razor without adding JavaScript.
- Uses native `<details>/<summary>`, which is the right baseline for no-JS disclosure.
- Adds keyboard-focusable scroll regions for dense comparison tables.
- Chooses card-stack only for scanning tables, not numeric comparison tables.
- Includes a human verification gate, which is appropriate for CSS-only work.

### Concerns

- **[HIGH] Mobile sidebar is rendered `open` by default.** The plan says `<details open>` is always rendered, so at 375px the nav starts expanded, not collapsed. That may fail “sidebar collapses to disclosure below 768px” depending on intended default behavior.

- **[HIGH] Card-stack accessibility is shaky.** `thead { display: none; }` removes headers from the accessibility tree in many cases, and CSS `content: attr(data-label)` is not a reliable screen-reader substitute. The visual card labels may work while SR users lose table header context.

- **[HIGH] Verification does not cover the stated ≥320px goal.** The human checkpoint uses 375px. The phase requires viewports ≥320px. Testing only 375px can miss topbar wrapping, long route names, table cards, and full-width forms at 320px.

- **[MEDIUM] Scroll regions may lack an explicit focus style.** The existing focus-visible block lists common controls, but not `.admin-table-scroll:focus-visible`. If browser defaults are suppressed or inconsistent, keyboard users can tab into a scroll region without a clear visible focus indicator.

- **[MEDIUM] “Zero pixel diff” is not supported by the manual process.** Manual screenshot comparison can support “zero visible regression”, not “zero pixel diff”. If the requirement is literal, the plan needs deterministic screenshot capture and image comparison.

- **[MEDIUM] Non-admin regression coverage samples four themes, not all themes.** The roadmap says zero regression on 22 guild themes. The four-theme sample is useful, but the plan should also include a static scan proving removed admin classes are unused by public views.

- **[LOW] The plan introduces literal em dashes in Razor `aria-label`s.** Project editing guidance defaults to ASCII. Use a hyphen or encode intentionally.

### Suggestions

- Decide sidebar default explicitly: if “collapsed on mobile” is required, render without `open` and use desktop CSS to force nav visibility, or document that “collapsible” means “can collapse, initially expanded.”
- Replace `thead { display: none; }` with a visually-hidden header pattern, or add stronger table semantics such as `scope="col"`/`headers` while treating `data-label` as visual-only.
- Add manual checkpoints for 320px, 375px, 768px, and 769px.
- Add `.admin-table-scroll:focus-visible` to the focus-visible selector set.
- Add static public-view scan for removed classes before visual checks.
- Change “zero pixel diff” to “zero visible diff” unless an actual image-diff tool is used.

### Risk Assessment

**MEDIUM-HIGH.** The markup plan is directionally right, but it has real a11y and verification gaps. The biggest ship-blockers are mobile sidebar default state, card-stack SR behavior, and missing 320px validation.

## Sources

- W3C, WCAG 2.5.5 Target Size Enhanced: https://www.w3.org/WAI/WCAG22/Understanding/target-size-enhanced  
- W3C, WCAG 2.5.8 Target Size Minimum: https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum

---

## Consensus Summary

Single external reviewer (Codex). All findings below are Codex's; no cross-reviewer consensus available.

### Agreed Strengths
- CSS-contracts-first / Razor-activation-second wave split is sound.
- Correctly treats stranded `site-common.css` admin-feedback rules as an architecture bug to migrate.
- Native `<details>/<summary>` no-JS disclosure is the right baseline; card-stack reserved for scanning tables only.
- No new packages / no JavaScript.

### Agreed Concerns (HIGH — execution blockers)
1. **Touch-target coverage incomplete (18-01)** — inventory misses feedback filter links, pagination, table "View" actions, `<summary>`, inputs/selects, plain admin buttons outside `.admin-action-form`. AMOB-03 says "all admin interactive elements."
2. **`.admin-shell` scoping not enforced (18-01)** — migrated selectors (`.admin-feedback`, `.type-badge`, `.detail-grid`, `.admin-table-scroll`) planned unscoped; AMOB-04/Pitfall-10 require `.admin-shell` parent or `@layer`.
3. **Sidebar `<details open>` by default (18-02)** — nav starts expanded at 375px; may fail "collapses to disclosure below 768px" depending on intended default.
4. **Card-stack screen-reader behavior (18-02)** — `thead{display:none}` drops header semantics; `content:attr(data-label)` is visual-only, not a reliable SR substitute.
5. **≥320px goal not verified (18-02)** — checkpoint only tests 375px; phase goal is ≥320px. Misses topbar wrap, long route names, full-width forms at 320px.

### MEDIUM
- `@import` shim defeats `asp-append-version` cache-busting on imported CSS (stale-file risk on deploy).
- 18-01 claims sidebar-collapse "truth" that only becomes true in 18-02 — reword to "CSS contract exists."
- grep-heavy verification can't catch malformed CSS / overflow / contrast / focus visibility.
- Missing `.admin-table-scroll:focus-visible` focus style.
- "Zero pixel diff" unachievable via manual screenshots — should read "zero visible diff."
- 4-theme sample vs roadmap's full theme set — add static scan proving removed classes unused by public views.

### LOW
- WCAG wording inaccurate: 2.5.5 (44px) is AAA; 2.5.8 (24px) is AA. 44px is product floor, not the AA bar.
- Literal em dashes in `aria-label`s vs project ASCII default.

### Divergent Views
None — single reviewer.
