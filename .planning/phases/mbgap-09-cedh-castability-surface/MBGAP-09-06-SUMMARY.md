---
phase: mbgap-09-cedh-castability-surface
plan: 06
status: pending-human-verify
executor: codex gpt-5.4 (cross-AI), reviewed + committed by Claude
commits:
  - e3b54508 feat(MBGAP-09-06): cEDH early-interaction lens UI
  - 70b7b25a test(MBGAP-09-06): Playwright spec for the interaction lens
  - 32da92c6 fix(MBGAP-09): interaction lens sees instants despite permanent gate
  - 8a9d0efc test(MBGAP-09): regression-lock the pre-gate interaction signal
  - 2c575d18 docs(MBGAP-09-06): two-viewport light+dark screenshots
key-files:
  created:
    - DeckFlow.Web/e2e/manabase-interaction-lens.spec.ts
    - .planning/ui-design/mbgap-09/screenshots/ (4 shots)
  modified:
    - DeckFlow.Web/Views/Deck/Manabase.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Core/Manabase/ManabaseModels.cs (fix)
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs (fix)
    - DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs (fix)
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs (fix)
---

# MBGAP-09-06 Summary — UI + live-pipeline gap fix

## What was built
- Third "Early interaction" lens section (`#manabase-early-interaction`): N/M headline, worst-5 rows with ✓/⚠ holdable badges + override asterisks, `<details>` "View all N (M more)" expander, raw-availability caveat, plain-language gloss, ⚠ caution empty state (D-03). Result-nav entry added.
- Triple/single wrapper modifier + `.manabase-twolens--triple` 3-up grid in site-common.css (640px collapse verified; --panel tokens only; nothing in site.css).
- "Held up (T1-3)" column in the cEDH castability table on interaction rows; empty cells keep the data-label for mobile card-stack alignment. Mode-note flag-off fallback preserved exactly once (D-15). Both formula panels cover the metric (LEAD corrected the deck's-numbers line to "QualifyingCount qualified; OnTargetCount held up").
- Playwright spec (relocated to `DeckFlow.Web/e2e/` — the plan's `DeckFlow.Web.Tests/Playwright/` path does not exist in this repo) asserting cEDH presence/Casual absence/column/expander + capturing classic (light) and nyx (dark) desktop+mobile screenshots. **6/6 pass.**

## Gap found and fixed during LEAD verification
Live e2e run rendered **0 / 0** — the plan-presence permanent gate in `PlanRoleClassifier` strips `PlanRole.Interaction` from all instants/sorceries, so no real interaction spell ever qualified (unit fixtures had used a permanent, Drannith Magistrate, masking it). Fix (Codex, spec by Claude): additive `SpellRequirement.IsInteractionSpell` carries the pre-gate interaction merit via a new `Classify` out-param overload; `ComputeInteractionLens` qualifies on `PlanRoles.Interaction || IsInteractionSpell`. Plan-presence permanent-gate semantics untouched (regression-locked). Post-fix live render: 11/12 held up, Counterspell 74% worst.

## Verification
- Playwright 6/6 (chromium-desktop + chromium-mobile), no horizontal overflow.
- LEAD visual review of all captured shots: 3-up desktop strip, single-column mobile, dark-theme legibility all good.
- Full suites post-fix: Core **1430/0**, Web **1383/0** (14 pre-existing skips). Builds 0 new warnings (1 pre-existing CS8602 in MetaGapServiceTests, untouched).
- EOL: no churn on any touched file.

## Deviations
- Spec path relocated (plan defect: nonexistent directory).
- Three strict-mode locator fixes applied by Claude during verification (trivial test-only edits).
- Mid-plan fix commits 32da92c6/8a9d0efc extend Plans 01/02/04 files — documented above.

## Human-verify checkpoint: PENDING user approval (screenshots under .planning/ui-design/mbgap-09/screenshots/)

## Self-Check: PASSED (pending checkpoint)
