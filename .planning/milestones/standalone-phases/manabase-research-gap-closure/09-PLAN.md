---
phase: manabase-research-gap-closure
plan: 09
type: execute
wave: 9
depends_on: ["04", "06", "08"]
files_modified:
  - DeckFlow.Web/e2e/manabase-lens-visual.spec.ts
autonomous: false
requirements: [MBGAP-12]
must_haves:
  truths:
    - "The tap-analyzer lens block is rendered in a real headless browser and screenshotted at desktop and mobile viewports"
    - "The mulligan-evaluator lens block is rendered and screenshotted at desktop and mobile viewports"
    - "A human confirms both lenses render correctly (layout, contrast, no overflow) at both viewports — the visual check EF2 never performed (markup was scored statically only)"
  artifacts:
    - path: "DeckFlow.Web/e2e/manabase-lens-visual.spec.ts"
      provides: "Playwright spec that navigates to a manabase result with both lenses on and captures 2-viewport screenshots"
      contains: "tap-analyzer"
  key_links:
    - from: "manabase-lens-visual.spec.ts"
      to: "tap-analyzer + mulligan-eval blocks on Manabase.cshtml"
      via: "screenshot capture at desktop + mobile"
      pattern: "screenshot"
---

<objective>
MBGAP-12 (D-14): the visual verification of the tap-analyzer and mulligan-evaluator lenses
that has never been done — the prior UI review scored markup statically and never rendered
the page in a browser. Both lenses are gated ON by default
(`analysis.manabase.tap-analyzer`, `analysis.manabase.mulligan-eval`). This is the phase's
closing QA gate: render both lenses in a real headless browser, screenshot them at 2
viewports, and have a human confirm they look correct.

This is additive VISUAL QA, not new functional test-writing — the existing 10 manabase e2e
specs already cover functionality (including manabase-mulligan.spec.ts).

Purpose: closes the never-performed lens visual verification.
Output: a screenshot-capturing Playwright spec + a human visual sign-off.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/phases/manabase-research-gap-closure/RESEARCH.md
@.planning/ui-reviews/manabase-UI-REVIEW.md

<interfaces>
<!-- Existing specs + constraints. -->
- DeckFlow.Web/e2e/manabase-mulligan.spec.ts — existing functional mulligan spec (reuse its navigation/deck-submit setup)
- Manabase.cshtml — tap-analyzer block (gated analysis.manabase.tap-analyzer) + mulligan-evaluator block (gated analysis.manabase.mulligan-eval); both default ON
- CLAUDE.md: start server via scripts/run-web-test.sh (sets DECKFLOW_DISABLE_AUTO_BROWSER=true; never opens a Windows browser); drive Playwright headless with `env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test`
- Viewport pattern: reuse the desktop+mobile parameterization used by existing manabase specs
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Capture 2-viewport screenshots of both lenses</name>
  <read_first>
    - DeckFlow.Web/e2e/manabase-mulligan.spec.ts (navigation + deck-submit setup to reuse)
    - DeckFlow.Web/Views/Deck/Manabase.cshtml (locate the tap-analyzer and mulligan-evaluator block selectors/ids)
    - CLAUDE.md constraints (run-web-test.sh; env -u DISPLAY; headless)
  </read_first>
  <action>
    Create DeckFlow.Web/e2e/manabase-lens-visual.spec.ts: submit a deck that triggers both lenses (a deck with enough
    tap-lands and a real curve so tap-analyzer + mulligan-eval render), then capture element screenshots of the tap-analyzer
    block and the mulligan-evaluator block at a desktop viewport and a mobile viewport (4 screenshots total) into the project's
    e2e artifact/output directory. Assert both blocks are visible (functional guard) and not clipped/overflowing (e.g. assert
    boundingBox width <= viewport width). Headless only; no host browser. Save screenshots to a path the SUMMARY can reference.
  </action>
  <verify>
    <automated>scripts/run-web-test.sh &amp; sleep 8; env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test manabase-lens-visual 2>&1 | tail -20</automated>
  </verify>
  <acceptance_criteria>
    - DeckFlow.Web/e2e/manabase-lens-visual.spec.ts exists and captures 4 screenshots (tap-analyzer + mulligan-eval × desktop + mobile)
    - Both blocks assert visible and within-viewport-width at both sizes
    - Spec passes headless; screenshot file paths recorded for the SUMMARY / checkpoint
  </acceptance_criteria>
  <done>Four lens screenshots captured; visibility+overflow guards green.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <what-built>Playwright captured 4 screenshots: the tap-analyzer lens and the mulligan-evaluator lens, each at a desktop and a mobile viewport, from a real headless render of the manabase results page (both lenses default-ON).</what-built>
  <how-to-verify>
    1. Open the 4 screenshots referenced in the plan-09 SUMMARY (tap-analyzer desktop/mobile, mulligan-eval desktop/mobile).
    2. Confirm each lens renders with correct layout, readable contrast, and no clipping/overflow at both viewports.
    3. Spot-check against the current guild theme(s) if a theme-specific concern is suspected (optional).
  </how-to-verify>
  <resume-signal>Type "approved" if both lenses look correct at both viewports, or describe the visual issues to fix.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| rendered lens → user | Visual defects (overflow/contrast) degrade the analysis UX; no runtime/security surface |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap12-01 | Information disclosure | clipped/unreadable lens misleads user | mitigate | 2-viewport screenshot capture + human sign-off (this plan) |
| T-mbgap12-SC | Tampering | NuGet installs | accept | No packages; e2e spec only |
</threat_model>

<verification>
- manabase-lens-visual.spec.ts green headless; 4 screenshots captured.
- Human approves both lenses at both viewports (blocking checkpoint).
</verification>

<success_criteria>
Both lenses are rendered, screenshotted at desktop + mobile, and human-verified as correct — the visual check EF2 never performed. MBGAP-12 complete, closing the phase.
</success_criteria>

<output>
Create `.planning/phases/manabase-research-gap-closure/09-SUMMARY.md` when done.
</output>
