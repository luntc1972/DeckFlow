---
phase: 86-ui-audit-re-score-studio-stage-4-admin-flags-closeout
plan: 05
subsystem: ui
tags: [playwright, e2e, wcag, accessibility, theming, ui-audit]

# Dependency graph
requires:
  - phase: 86-01
    provides: "Bug B accent-derived color-mix (Jeskai-blue literal replacement)"
  - phase: 86-02
    provides: "Bug A filled-accent-pill active step-tab + per-theme --accent-contrast"
  - phase: 86-03
    provides: "Bug C bucket-toggle chevron + aria-label"
  - phase: 86-04
    provides: "Bug D layout-picker measurable mode delta + guided positive style"
provides:
  - "theme-active-affordance.spec.ts — visual-regression + WCAG + accent-leak + a11y e2e coverage for Bugs A/B/C"
  - "layout-mode-interaction.spec.ts — interaction-outcome e2e coverage for Bug D"
  - "tasks/UI-REVIEW.md re-scored 18/24 -> 21/24 (UIAUDIT-02 target >=20/24 met, pending human sign-off)"
affects: [86-phase-closeout]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "CSS custom-property probe pattern: create a throwaway DOM element, set style.color = var(--token), read getComputedStyle().color, remove — resolves any token to its final computed color independent of selector-cascade quirks"
    - "WCAG contrast ratio computed inline in the spec (sRGB relative-luminance formula), no new dependency"

key-files:
  created:
    - DeckFlow.Web/e2e/theme-active-affordance.spec.ts
    - DeckFlow.Web/e2e/layout-mode-interaction.spec.ts
  modified:
    - tasks/UI-REVIEW.md

key-decisions:
  - "Scoped .prompt-instructions locator to #prompt-step-panel-2 (not a bare class selector) because Step 4 renders a second .prompt-instructions block in the same DOM — Playwright strict mode caught this immediately."
  - "layout-mode-interaction.spec.ts explicitly clicks [data-prompt-ui-mode-button=\"guided\"] before measuring the guided state, rather than relying on the server-rendered default — client JS (deck-sync.ts getDefaultPromptUiMode) overrides the initial mode to 'focused' on narrow/mobile viewports (a pre-existing, unrelated mobile-default feature), which would otherwise make the guided-positive-style assertion viewport-dependent and flaky."
  - "Color pillar re-scored 2/4 -> 4/4 (not just the plan's suggested 2->3): live-verified Phase 84's --link/--focus/--cta-border/--danger migration is genuinely in effect (theming.spec.ts's two permanent regression guards — danger!=link, and link/focus/cta-border resolve to a real color — both green across all 24 themes) and the ~60 remaining raw --accent-strong references in site-common.css are Phase 84's own audited, dispositioned-KEEP decorative uses (UI-VS-* tags), not unmigrated semantic-role debt. Combined with this phase's Bug B (last hardcoded-blue-literal elimination), the pillar's original core complaint is closed. Documented transparently in tasks/UI-REVIEW.md with full evidence so a reviewer can independently verify or dispute the credit attribution."
  - "Experience Design re-scored 3/4 -> 4/4: Bug C's bucket-toggle aria-label and Bug D's perceptible/positive layout modes close two real, previously-open gaps in this pillar. The unrelated Feedback/Index.cshtml double-submit gap remains open (deferred, needs operator assignment) but does not block the ceiling score, per the same reasoning already applied to Visuals' 4/4 in the 2026-07-04 audit."

requirements-completed: []  # UIAUDIT-02 automated work is DONE and verified >=20/24, but the plan's blocking human-verify checkpoint has NOT yet been approved. Do not mark UIAUDIT-02 complete until that checkpoint is signed off.

# Metrics
duration: ~55min
completed: 2026-07-05
---

# Phase 86 Plan 05: Acceptance Gate — New e2e Tests, Full Suites, 6-Pillar Re-Score Summary

**Two new Playwright specs close the test gap that let Bugs A-D ship green (visual-regression for the filled step-tab pill + WCAG + accent-leak + bucket a11y; interaction-outcome for the layout-picker mode delta), and the 6-pillar UI audit re-scores 18/24 -> 21/24 — clearing the >=20/24 UIAUDIT-02 target with 1 point of margin. The plan's blocking human-verify checkpoint is PENDING, not self-approved.**

## Performance

- **Duration:** ~55 min
- **Completed:** 2026-07-05
- **Tasks:** 2 of 2 automated tasks completed; Task 3 (`checkpoint:human-verify`, gate="blocking") is PENDING
- **Files modified:** 6 (2 new spec files, 1 doc, 4 evidence screenshots)

## Accomplishments

- **`theme-active-affordance.spec.ts`** (192 lines): proves the active step-tab is a filled `var(--accent)` pill — computed background differs from the inactive tab AND from the resolved `var(--panel-soft-bg)` AND equals the resolved `var(--accent)` — across azorius (light @import), jund (dark fork), dimir (dark @import needing `--accent-contrast`), planeswalker-dark (dark fork needing `--accent-contrast`), and Classic with an EXPLICIT `site.css` cookie (never a null/absent cookie). Separately asserts WCAG >=4.5:1 active-tab text contrast for {dimir, golgari, planeswalker-dark, nyx} (the confirmed fails) plus {jund, sultai} as guards. Adds 3 accent-leak checks on jund (non-Jeskai): layout-segment hover, ui-mode-button `.is-active`, and `.clear-cache-button:hover` all confirmed NOT to render the old hardcoded `rgb(43, 108, 176)`. Adds a bucket-toggle a11y check: non-empty `aria-label` + zero border-width. **26/26 passed** on both `chromium-desktop` and `chromium-mobile`.
- **`layout-mode-interaction.spec.ts`** (101 lines): proves Bug D is a real, measurable OUTCOME, not just a data-attribute flip — guided (Full) carries a positive accent left-border (`border-left` width > 0, color == resolved `var(--accent)`); focused (Compact) measurably shrinks `.prompt-instructions`'s bounding-box height vs guided; expert (Advanced) guarantees a full collapse (`toBeHidden()`). Runs for {Classic, dimir}. **4/4 passed** on both projects.
- Ran the FULL Playwright e2e suite (all 27 spec files, both projects): **286 passed, 14 skipped, 0 failed** (one earlier run under 12-worker parallel load hit 4 transient connection-reset failures against the single dev-server process — a clean re-run confirmed 0 failures; the isolated per-spec runs were consistently green throughout).
- Ran the full build: `dotnet.exe build DeckFlow.sln` — **0 Warnings, 0 Errors**.
- Ran the full xUnit suite: `dotnet.exe test DeckFlow.sln` — **Core 1095/1095, Web 1218/1218 (12 skipped, Postgres-integration with no live PG), Studio 290/290** — all green.
- Confirmed the accent-leak grep gate is clean: `grep -rn "43, *108, *176" DeckFlow.Web/wwwroot/css/*.css | grep -v site-jeskai.css` returns nothing.
- Re-ran the 6-pillar UI audit and updated `tasks/UI-REVIEW.md`: **18/24 -> 21/24** (+3), with a per-pillar delta table, a dedicated "Phase 86 Re-Score" section documenting evidence, and an explicit historical-supersession note on the old "Gap to >=20/24" handoff section. Captured 4 headless screenshots (desktop 1280 + mobile 390, Classic + Dimir themes, Step 2/guided mode) as visual evidence.

## Task Commits

Each task was committed atomically:

1. **Task 1: Visual-regression spec — active pill, WCAG, accent-leak, bucket a11y** - `85a6b029` (test)
2. **Task 2: Interaction spec + accent-leak grep gate + 6-pillar re-score** - `1763f976` (test)

**Plan metadata / SUMMARY:** this commit, follows.

_No TDD RED/GREEN split — these are new permanent e2e specs added directly, not a bug-fix TDD cycle (the specs themselves ARE the deliverable, per plan design)._

## Files Created/Modified

- `DeckFlow.Web/e2e/theme-active-affordance.spec.ts` (new) - visual-regression + WCAG + accent-leak + a11y coverage for Bugs A/B/C
- `DeckFlow.Web/e2e/layout-mode-interaction.spec.ts` (new) - interaction-outcome coverage for Bug D
- `tasks/UI-REVIEW.md` - re-scored 18/24 -> 21/24 with per-pillar deltas and a new "Phase 86 Re-Score" section
- `.planning/phases/86-ui-audit-re-score-studio-stage-4-admin-flags-closeout/evidence/86-05-{classic,dimir}-{chromium-desktop,chromium-mobile}.png` (new) - visual evidence screenshots

## Decisions Made

See `key-decisions` in frontmatter for the two most consequential calls: (1) the `.prompt-instructions` locator scoping fix (Playwright strict-mode caught a genuine two-element ambiguity), and (2) explicitly clicking the guided button rather than trusting the page's initial mode (mobile viewports default to 'focused' via client JS, unrelated to Bug D). The Color and Experience Design pillar re-scoring rationale (going beyond the plan's minimal suggested deltas, with full supporting evidence) is also documented there and in `tasks/UI-REVIEW.md` itself for independent verification.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `.prompt-instructions` locator ambiguity in the interaction spec**
- **Found during:** Task 2, first `layout-mode-interaction.spec.ts` run
- **Issue:** `page.locator('.prompt-instructions')` resolved to 2 elements (Step 2's and Step 4's blocks both exist in the DOM simultaneously; only one is visible via a `.hidden` class on the parent panel). Playwright's strict mode correctly failed rather than silently picking one.
- **Fix:** Scoped the locator to `#prompt-step-panel-2 .prompt-instructions`.
- **Files modified:** `DeckFlow.Web/e2e/layout-mode-interaction.spec.ts`
- **Verification:** Spec passed 4/4 after the fix.

**2. [Rule 1 - Bug] Guided-mode assertion was viewport-dependent (mobile default is 'focused', not 'guided')**
- **Found during:** Task 2, second `layout-mode-interaction.spec.ts` run (chromium-mobile only failures)
- **Issue:** `deck-sync.ts`'s `getDefaultPromptUiMode()` returns `'focused'` on narrow/mobile viewports (a pre-existing "mobile UI phase 1" feature, unrelated to Bug D) rather than the server-rendered `'guided'` default. The spec's guided-positive-style assertion was measuring the wrong mode on chromium-mobile.
- **Fix:** Explicitly click `[data-prompt-ui-mode-button="guided"]` before measuring the guided state, making the assertion deterministic across both viewports instead of relying on the page's initial state.
- **Files modified:** `DeckFlow.Web/e2e/layout-mode-interaction.spec.ts`
- **Verification:** 4/4 passed on both `chromium-desktop` and `chromium-mobile` after the fix.

---

**Total deviations:** 2 auto-fixed (both Rule 1 bug fixes discovered while writing/running the new specs themselves, not in the CSS/Razor code from prior plans).
**Impact on plan:** Necessary corrections to make the new specs correct and deterministic; no scope creep — same files, same task, same requirement (UIAUDIT-02).

## Known Stubs

None.

## Threat Flags

None — no new network endpoints, auth paths, file-access patterns, or schema changes. Matches the plan's `threat_model` disposition (accept/mitigate, no new surface); no new npm/NuGet package was added (`@playwright/test` was already a devDependency, used via `npx --no-install`).

## Verification Results

- `theme-active-affordance.spec.ts` — 26/26 passed (both projects).
- `layout-mode-interaction.spec.ts` — 4/4 passed (both projects).
- Full Playwright e2e suite — 286 passed, 14 skipped, 0 failed (clean re-run after one transient parallel-load blip).
- `dotnet build DeckFlow.sln` — 0 Warnings, 0 Errors.
- Full xUnit — Core 1095/1095, Web 1218/1218 (12 skipped, Postgres-only), Studio 290/290.
- Accent-leak grep gate — clean.
- `tasks/UI-REVIEW.md` — 21/24, contains the string "21/24" (verify-gate pattern match).
- LF line endings preserved on all created/modified files (`grep -c $'\r'` == 0 on each).
- No compiled `wwwroot/js/*.js` committed; only `.ts`/`.md`/`.png` files touched.
- 4 incidental screenshot files (`bracket-{classic,nyx}-chromium-{desktop,mobile}.png`) that were regenerated as a side effect of running the pre-existing `bracket-smoke.spec.ts` during the full-suite run were reverted via `git checkout --` (out of this plan's scope, not committed).

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

**BLOCKING CHECKPOINT PENDING (Task 3, `checkpoint:human-verify`, `gate="blocking"`).** This plan is `autonomous: false` and intentionally ends here. NOT self-approved. Per the plan's own instructions, the human must:
1. Start the headless server (`scripts/run-web-test.sh`) and visit `/deck-analysis`.
2. Switch themes (Classic, Azorius, Jund, Dimir, Planeswalker-Dark) at desktop AND mobile widths and confirm Bugs A/B/C/D all render correctly.
3. Confirm the full suite (build + xUnit + Playwright e2e) is green — already independently verified in this run, see Verification Results above.

Until that checkpoint is approved:
- `UIAUDIT-02` is NOT marked complete in `REQUIREMENTS.md`.
- `STATE.md`/`ROADMAP.md` plan-advance updates have NOT been run (no `state advance-plan`, no `roadmap update-plan-progress`, no `requirements mark-complete`) — these are deferred to whichever agent processes the Task 3 sign-off.
- Phase 86 remains PARTIALLY OPEN regardless: even after this checkpoint is approved, `UIAUDIT-03` (Studio DirectPush Stage 4 verification) and `ADMIN-01` (`/Admin/Flags` sorting) remain NOT satisfied and require a follow-up planning pass, per `86-CONTEXT.md`'s explicit scope boundary.

---
*Phase: 86-ui-audit-re-score-studio-stage-4-admin-flags-closeout*
*Completed: 2026-07-05 (Tasks 1-2 only; Task 3 checkpoint pending)*

## Self-Check: PASSED
- FOUND: DeckFlow.Web/e2e/theme-active-affordance.spec.ts
- FOUND: DeckFlow.Web/e2e/layout-mode-interaction.spec.ts
- FOUND: tasks/UI-REVIEW.md
- FOUND: .planning/phases/86-ui-audit-re-score-studio-stage-4-admin-flags-closeout/evidence/86-05-classic-chromium-desktop.png
- FOUND: 85a6b029 (Task 1 commit)
- FOUND: 1763f976 (Task 2 commit)
