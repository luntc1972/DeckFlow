---
phase: manabase-research-gap-closure
plan: 06
status: complete
completed: 2026-07-12
commits:
  - 16ed056d fix(manabase): verdict rounding both Ceiling sites + truncation note + plurals (gap-06)
  - 32746e5d fix(manabase): propagate verdict wording to .txt, swap prompt, page (gap-06)
  - 6ec29695 test(manabase): verdict e2e asserts new wording, overflow-conditional (gap-06)
  - 86796704 fix(manabase): shared ManabaseWording helper kills remaining Ceiling/plural sites (gap-06)
executor: codex gpt-5.4 medium (cross-AI); Claude reviewed + committed
verifier: foreman-verifier PASS_WITH_NOTES -> 2 HIGH + 1 MED same-class findings in unplanned sites -> fix 86796704 (greps + pinning tests confirm)
---

# Plan 06 Summary — MBGAP-05a-d verdict polish

## What shipped

- **05a rounding:** both synthesizer `Math.Ceiling` sites → `Math.Round(AwayFromZero)` (1.05 shortfall displays ~1, not ~2). Verification then found the SAME class in 3 unplanned sites, all fixed: `PrimaryFix.Amount` (page headline contradicted the fixed line on the same render), `ManabaseAnalyzer.BuildSummary` (Ceiling + literal `land(s)` echoed verbatim into the .txt paste — core-value surface), `ManabaseDisplay.KarstenMet` deficit.
- **Shared helper:** new `ManabaseWording.{ApproximateCount,Pluralize,ApproximatePhrase}` — all six wording surfaces route through it (kills the copy-paste drift the verifier flagged).
- **05b:** 3-line truncation appends "…plus N more" (N = total−3).
- **05c:** zero `(s)` artifacts across synthesizer/.txt/swap/page (repo-wide grep on display paths clean; remaining Ceiling at ManabaseModels:994 is tolerance-threshold math, not display).
- **05d:** per-color deficit labeled heuristic guidance on page ("Short by (heuristic guidance)" column), .txt, swap prompt.
- Live behavior, no flag (presentation) — ships on deploy.

## Re-baselines (rounded vs Ceiling)

- PrimaryFix 2→1 (1.05 shortfall); KarstenMet deficits 2→1, 2→1, 3→2 (+ new 1.05 case).
- New regression tests: full-.txt-artifact no-`(s)` + Summary==Lands-line consistency; BuildSummary fractional; PrimaryFix rounding.

## Incidents

- e2e assertion defect (unconditional `…plus` on a 3-issue fixture) caught by live run → conditional assertion.
- LEAD staging error bundled `.planning/config.json` into 7470d07f → recommitted clean as 86796704.

## Validation

Build 0/0; Core FULL 1399/0; Web FULL 1358/14skip/0; verdict+manabase e2e LIVE 30/30 both viewports; 2-viewport screenshots reviewed; EOL clean.
