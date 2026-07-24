---
phase: 109
plan: 01
title: Rename to ICutLabWhatifService and Add Shared CommitSwapAsync + TryValidateSwap
status: complete
completed: 2026-07-23
requirements_addressed: [CLUP-04, CLUP-05]
executor: codex (gpt-5.4 medium)
verifier: claude
---

# Plan 109-01 Summary — Shared What-If Service Surface

## What was built
One interface now owns Cut Lab what-if validation, preview, and commit. This plan
created the surface and locked its rules with service-level tests; it deliberately
changed NO transport behavior (controllers still used their own helpers until 109-02).

- Renamed `ICutLabWhatifPreviewService` → `ICutLabWhatifService`,
  `CutLabWhatifPreviewService` → `CutLabWhatifService`,
  `ComputeSwapPreviewAsync` → `PreviewSwapAsync` (rename only — preview body untouched).
- Added `bool TryValidateSwap(CutLabState, string cardOut, string cardIn, out string? error)`:
  the single validation source. Adopts the STRICTEST prior variant (the no-JS one) —
  rejects a card-out that is missing / `IsLocked` / `IsCommander`, and a card-in not in
  the accepted cut pile — returning `false` + `CutLabMessages.NoChangeMessage`. Never
  throws for an invalid *pair*; still throws on null/whitespace *arguments* (Try-pattern:
  domain rejection returns, programmer error fails loud).
- Added `Task<CutLabWhatifCommitResult> CommitSwapAsync(...)`: `ThrowIfCancellationRequested()`
  at entry, validates via `TryValidateSwap` (no re-derived rules), applies restore-then-accept
  solely through `CutLabDecisionApplier.Apply` keyed by `CutLabCutRoundEngine.WhatifSwapKey`,
  and returns a result object rather than throwing for expected-invalid pairs.
- Added `CutLabWhatifCommitResult` (`Applied`, required `State`, `CardOut`, `CardIn`,
  `ErrorMessage`).
- DI + every name reference updated, including the injected member rename
  `_whatifPreviewService` → `_whatifService` in both controllers and the test fakes.

## Key decisions preserved from planning
- **Atomicity (T-109-02, guards regression `7cb68348`):** when the accept would overshoot,
  the decision-count check returns the ORIGINAL `state` — never `afterRestore`. A rejected
  swap can therefore never leak a half-applied state where the card-in returned but nothing
  was cut. This is what makes the two-step transform transactional without transactions.
- **Input casing (review LOW-1):** success returns the caller's exact `cardOut`/`cardIn`
  strings, not canonicalized names, so the existing API response casing is preserved.

## Key files
- modified: `DeckFlow.Web/Services/CutLab/CutLabWhatifPreviewService.cs` (+133)
- modified: `DeckFlow.Web/Program.cs` (DI registration)
- modified: `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` (name-only)
- modified: `DeckFlow.Web/Controllers/CutLabController.cs` (name-only)
- modified: `DeckFlow.Web.Tests/CutLabWhatifTests.cs` (13 new service tests)
- modified: `DeckFlow.Web.Tests/CutLabApiControllerTests.cs` (fake surface)
- modified: `DeckFlow.Web.Tests/CutLabControllerTests.cs` (fake surface)

## Commits
- `fb8fdfdc` feat(cut-lab): add shared what-if validate and commit
- `7260458f` refactor(cut-lab): point DI and callers at what-if service
- `ba777962` test(cut-lab): cover shared what-if validate and commit rules

## Verification (Self-Check: PASSED)
Verified independently by Claude, not taken on the executor's word:
- Scope fence: exactly the 7 permitted files, nothing else.
- EOL: `git diff --stat` identical to `--ignore-all-space --stat` (485+/45−); all touched
  files LF with CR=0, matching their committed baseline. No churn.
- Grep gates: `ICutLabWhatifPreviewService` / `ComputeSwapPreviewAsync` /
  `_whatifPreviewService` → 0 source matches. DI registers `ICutLabWhatifService`.
- All 13 required test method names present in `CutLabWhatifTests`.
- 109-01 scope guard confirmed: `ValidateWhatifPair` / `IsValidWhatifPair` still present
  (their deletion is 109-02's job) — the rename plan did not overreach.
- Implementation read-checked at `CutLabWhatifPreviewService.cs:162-240`.
- Build: 9 warnings = baseline (pre-existing CS8629 in Core.Tests), 0 new.
- xUnit full solution green.

## Deviations
- Codex ran task 1's `--filter` verify only after tasks 2–3 landed, because the rename-only
  consumers still referenced the old names mid-task and would not compile. Ordering only;
  the acceptance criteria are unaffected.

## Notes
No Scryfall/browser gate was run — the local Windows dev server cannot reach Scryfall
(TLS-fingerprint block). Unit fakes isolate Scryfall; live UAT is deferred to prod,
consistent with the Phase 108 precedent.
