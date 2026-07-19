---
status: partial
phase: 102-structural-analysis-role-floors
source: [102-VERIFICATION.md]
started: 2026-07-19T18:16:23Z
updated: 2026-07-19T18:16:23Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. [ASSUMED] product constants eyeball
expected: FallbackLands=36, per-role floor table (CutLabFloorDefaults.cs — interaction 6/8/10/12, protection 2/3/4/5, engines 4/5/6/6, payoffs 4/5/6/6, wincons 2/2/3/3 across B2-B5), and finding thresholds (CutLabStructuralFindings.cs — 0.30 share, 12 min cards, 2..4 stranded, +3 redundant, +1 weak-floor, 2 near-combo pieces) look right as product defaults. All are [ASSUMED]-flagged and user-adjustable.
result: [pending]

### 2. Live structural-read UAT
expected: flip tool.cut-lab.enabled locally (or via /Admin/Flags), import a familiar pool, sanity-check role assignments, findings, and the floor edit → Recalculate round-trip (Adjusted badge + value persist).
result: [pending]

## Summary

total: 2
passed: 0
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps
