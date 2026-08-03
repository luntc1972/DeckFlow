---
status: complete
completed: 2026-08-03
---

# Summary

All five 2026-08-03 Codex review outputs are now in the repository.

## What changed

| File | Change |
|---|---|
| `04-REVIEWS.md` | Appended both owed **code** reviews. `04-03` (`b508f27e`): gate **DISCHARGED**, 2 MEDIUM + 1 LOW follow-ups. `04-04` (`8b5d2e8e..908402cd`): **CHANGES REQUIRED**, 2 HIGH + 1 MEDIUM, unfolded. |
| `05-REVIEWS.md` | Added **Round 9** (transcribed from `STATE.md`, never captured when it ran) and **Round 10** (narrow verification: defect CONFIRMED, plus 1 BLOCK and 1 HIGH found *in the prescribed fix*). |
| `07-REVIEWS.md` | **New.** Phase 7's first review of any kind: 10 HIGH · 3 MEDIUM · 2 LOW. |
| `08-REVIEWS.md` | **New.** Phase 8's owed plan review: **2 BLOCK** · 7 HIGH · 4 MEDIUM. |
| `STATE.md`, `ROADMAP.md`, `04-04-SUMMARY.md` | Folded the stale owed-gate markers; added the do-not-execute warning for Phase 8. |

## Findings worth carrying forward

- **Stage 1 vs stage 2 diverged on identical input.** `codex review --base ea3dca2a` reported "no
  actionable defects" on the same range where the stage-2 `codex exec` brief found two CONFIRMED
  HIGHs. Both HIGHs were structurally invisible to a diff-scoped reader: one lives in the
  interaction between new and pre-existing code, the other is an *absent assertion*. Stage 1 also
  could not run the .NET tests (dotnet unreachable from WSL under `workspace-write`), so its clean
  verdict rested on TypeScript alone.
- **Every BLOCK was a census failure.** 70 uncounted constructor sites, 9 e2e specs claimed as 6,
  19 `ResolveDefaults` calls claimed as 12. None is detectable by reading the plan carefully — each
  needs a grep. The same-family checker passed Phase 8 clean; Codex returned 2 BLOCKs.
- **The density fixture measured the wrong population twice.** The repaired 92-card eligible count
  is still not asserted, so the earlier fix is not load-bearing.

## Not done

Nothing was folded. All findings remain open by design — folding Phase 7 and Phase 8 spans multiple
files and a shared contract, which is Codex `terra` work, not a docs task.
