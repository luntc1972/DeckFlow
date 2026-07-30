# Foreman Ledger — Cycle 21 Phase 2, Plan 02-05 (wave 3)

**Run:** 2026-07-27
**Worktree:** /mnt/c/users/chrislunt/source/personal/deckflow-role-floors
**Branch:** gsd/cycle21-cut-lab
**Baseline commit:** ba54ba31 (plan 02-04 complete, 7 commits, all blind-verified PASS)
**Baseline gates:** build 0 errors / 9 pre-existing CS8629 warnings; Core.Tests 1708 / 0 failed;
Web.Tests 2095 passed / 16 skipped / 0 failed.
**Baseline untracked (permanent set, do not touch):** `.foreman/`, `_edhrec-brackets/`,
`_role-floor-research/`

**Mode:** Codex-boosted. **Routing:** WORKHORSE — codex `gpt-5.4`, `model_reasoning_effort=medium`,
`-s danger-full-access`, `approval_policy=never`.

**Plan:** `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/02-05-PLAN.md`

Plan 02-05 is already Core-first — every new type and test lands in `DeckFlow.Core` /
`DeckFlow.Core.Tests` — so the standing user rule ([[feedback_cli_logic_in_core_with_tests]]) needs no
amendment this wave, unlike 02-04.

## Write sets and serialization

| Task | Files |
|---|---|
| 1 | `DeckFlow.Core/Research/RoleFloorFigure.cs` (new), `DeckFlow.Core.Tests/RoleFloorFigureTests.cs` (new) |
| 2 | `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs`, `DeckFlow.Core/Research/RoleFloorDivergenceStats.cs`, `DeckFlow.Core.Tests/RoleFloorDivergenceStatsTests.cs` |
| 3 | `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs` |
| 4 | `DeckFlow.Core.Tests/RoleFloorDivergenceStatsTests.cs` |

Task 1 is disjoint from 2–4. Tasks 2 and 3 both write the runner; 2 and 4 both write
`RoleFloorDivergenceStatsTests.cs`. **Dispatch A = Task 1. Dispatch B = Tasks 2, 3, 4 sequentially,
one commit each.**

## Task 4 — added by LEAD, not in the plan

Closes the two LOW findings the wave-1 blind verification carried forward against `ClearsFloorBar`,
which become permanent the moment Task 2 deletes `ClearsBar`:

1. **The `absoluteFloorGap` boundary (exactly 2.0) is untested.** The comparison is `>=`; a later
   `>=`→`>` flip would stay green while silently dropping every commander sitting exactly at the
   floor.
2. **`ClearsFloorBar` has no `corpusMean <= 0.0` guard, and `ClearsBar` — the only place that guard
   exists — is deleted by Task 2.** On an all-zero corpus row (`corpusP25 = 0`, `corpusMean = 0`,
   `corpusStdDev = 0`) a commander with `P25 >= absoluteFloorGap` now clears: the absolute-gap branch
   fires, and `ComputeZScore` returns `PositiveInfinity` for unequal means against a zero-spread
   baseline, which passes any threshold. The old bar returned false.

**User decision, 2026-07-27: KEEP the behavior, characterize it with tests.** Rationale: a commander
running 2+ of a role the entire corpus runs zero of is a genuine divergence, and handling
`corpusP25 == 0` is precisely what `absoluteFloorGap` was introduced for — porting the old guard
forward would suppress real findings and partly defeat that path. Task 4 therefore adds
characterization tests only. **No behavior change.**

## Tasks

| ID | Task | Seat | Status |
|---|---|---|---|
| T1 | Source-discriminated figure types + `RoleFloorFigureTable` + reflection tests | codex:gpt-5.4/medium | DONE → `f2bd3916` (Core 1708→1716); verification batched to wave end |
| T2 | Switch verdict onto `ClearsFloorBar`; delete `ClearsBar` + all FIVE of its tests | codex:gpt-5.4/medium | **ACCEPTED** → `0971f9d6` |
| T3 | Per-source tables with different column sets; per-source coverage | codex:gpt-5.4/medium | **ACCEPTED** → `e3017d75` |
| T4 | Characterization tests: `absoluteFloorGap` boundary, all-zero-corpus clearance | codex:gpt-5.4/medium | **ACCEPTED** → `58509d69` |
| T4b | Correct D-04's test-deletion count in the plan | claude (planning role) | **DONE** → `3faae0ad` |
| T5 | `02-05-SUMMARY.md` | claude (planning role) | **DONE** → `b0d0f5c0` |

## Outcome — plan 02-05 COMPLETE

Six commits, `f2bd3916`..`b0d0f5c0`. **Not pushed.** Branch is 56 ahead of `main`.
Final gates: build 0 errors / 9 pre-existing warnings (0 new); Core.Tests **1715** / 0 failed;
Web.Tests 2095 / 16 skipped / 0 failed, unchanged. Zero EOL churn; all five touched files LF.

One blind verification covering all four code commits together: **PASS on every item A–K.**

**The result worth keeping.** The verifier executed `ClearsFloorBar` on a case where the two
statistics disagree — commander P25 neutral (6.0 vs corpus 6.0), mean wildly divergent (18 vs 6,
z≈56) → **False**, where the deleted mean-driven `ClearsBar` returned **True** on the same means.
That is the proof the wave actually moved the verdict onto P25 rather than renaming a call. Until
this wave the harness had been *printing* a P25 column while *deciding* on the mean.

**Codex refused a third time, and was right a third time.** Plan D-04 authorized deleting one test
member; five exercise `ClearsBar`. LEAD verified before authorizing that four have direct
`ClearsFloorBar` counterparts already, so deleting all five loses no coverage — and that the fifth,
`ClearsBar_WhenCorpusMeanIsZero_ReturnsFalse`, is the only executable statement of semantics the user
deliberately changed, now replaced by characterization tests.

### Carried forward into 02-09 (both LOW, from blind verification)

1. The "no distribution column" assertion is hardcoded to the literal `EdhrecColumns` property, while
   the `Source`-column assertion reflects over every declared list. A future `EdhrecColumnsV2`
   carrying both `Source` and `P25` would pass every test in the file.
2. The no-distribution-property guard is specific to `EdhrecRolePointEstimate`, not to "any
   `IRoleFloorFigure` tagged `Edhrec`". A new sibling record tagged `Edhrec` with its own `P25` would
   not be seen.

`02-09` already carries authorization to touch these assertions when it adds `EdhrecBulk = 3`; it
should GENERALIZE both rather than add a third one-off.

### Still owed across the phase

- **RFLR-12 remains unwritten** (carried from 02-04). `REQUIREMENTS.md` is cycle-wide governance no
  phase plan may edit; wording is drafted in `02-04-SUMMARY.md` §9. Needs a developer decision.
- Latent, pre-existing: `RoleFloorResearchCommandRunner.cs:343` writes `exception.Message` to
  `Console.Error`.

## Attempts (append-only, continued)

## Attempts (append-only)

- 2026-07-27 — ledger opened, baseline `ba54ba31` recorded, T1 dispatched.
- 2026-07-27 17:53 — T1 `DONE` → `f2bd3916` (2 new files, +391). Core 1708→1716, Web unchanged,
  build 0/0, both new files LF. Codex demonstrated BOTH reflection assertions actually bite, with
  exact messages: removing `Source` from `EdhrecColumns` →
  `RoleFloorFigureTable declaration 'EdhrecColumns' must include a Source column.`; adding a `P25`
  property to `EdhrecRolePointEstimate` →
  `EdhrecRolePointEstimate must not expose distribution properties; found: P25`. Both throwaway edits
  reverted before commit. LEAD cheap checks: every distribution property (`:78`-`:98`) lies inside
  `PostgresRoleDistribution` (`:48`-`:111`); `EdhrecRolePointEstimate` (`:112`-`:161`) has none;
  `get; }` appears only on the three interface declarations; no `DeckFlow.Web` reference.
  **Verification DEFERRED to the wave end** (one blind pass over all four commits) rather than
  skipped — T1 is additive Core-only with self-proving reflection tests, and a serial verify between
  every task costs a full round-trip each time.
- 2026-07-27 — dispatch B sent: plan Tasks 2 and 3 plus LEAD's Task 4, three commits, sequential.
  Task 4 carries the user's KEEP decision on the all-zero-corpus row explicitly, with an instruction
  NOT to add the `corpusMean <= 0.0` guard and to report disagreement rather than act on it.
- 2026-07-27 17:56 — dispatch B attempt 1 returned **`NEEDS_CONTEXT`, correctly. Zero edits made,
  HEAD unchanged at `f2bd3916`.** Plan D-04 asserts a single test member exercises `ClearsBar`;
  there are **five** (`RoleFloorDivergenceStatsTests.cs` :43, :51, :59, :67, :128). Deleting
  `ClearsBar` breaks compilation of all five, so the plan's "one and only authorized test deletion"
  was unsatisfiable as written. **This is the third time this cycle a fenced Codex executor has
  refused rather than improvised, and the third time it was right** — see the same pattern in wave 1
  (unsatisfiable scope gate; 02-03's "do NOT duplicate" vs "seven members").

  LEAD resolution, verified before authorizing: deleting all five loses **no** coverage.
  `ClearsFloorBar` already carries counterparts at :75 (below-minimum), :85 (divergent+significant),
  :97 (inside neutral band) and :107 (divergent but not significant). The fifth,
  `ClearsBar_WhenCorpusMeanIsZero_ReturnsFalse` (:67), has no counterpart **by the user's decision** —
  it covers exactly the `corpusMean <= 0.0` guard that is deliberately not carried forward, and
  Task 4 Case B pins the new opposite behavior in its place.

  Also confirmed while checking: the existing `ClearsFloorBar_ZeroCorpusP25_UsesAbsoluteGapFallback`
  Theory (:114-125) covers `commanderP25` of 0.0 / 3.0 / 1.0 against a gap of 2.0 — **exactly 2.0 is
  genuinely absent**, independently confirming wave 1's LOW finding.

  Plan corrected in three places (D-04, Task 2 acceptance, verification item 5); dispatch prompt
  patched with the explicit five-member authorization and a table naming each member's counterpart;
  re-dispatched as attempt 2.
