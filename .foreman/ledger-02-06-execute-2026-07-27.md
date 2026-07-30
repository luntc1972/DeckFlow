# Foreman Ledger — Cycle 21 Phase 2, Plan 02-06 (wave 4)

**Run:** 2026-07-27
**Worktree:** /mnt/c/users/chrislunt/source/personal/deckflow-role-floors
**Branch:** gsd/cycle21-cut-lab
**Baseline commit:** b0d0f5c0 (plan 02-05 complete, 6 commits, blind-verify PASS on all items A–K)
**Baseline gates:** build 0 errors / 9 pre-existing CS8629 warnings; Core.Tests 1715 / 0 failed;
Web.Tests 2095 passed / 16 skipped / 0 failed.
**Baseline untracked (permanent set, do not touch):** `.foreman/`, `_edhrec-brackets/`,
`_role-floor-research/`

**Mode:** Codex-boosted. **Routing:** WORKHORSE — codex `gpt-5.4`, `model_reasoning_effort=medium`,
`-s danger-full-access`, `approval_policy=never`.

**Plan:** `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/02-06-PLAN.md`

This plan connects two halves that waves 1–3 built but deliberately left unconnected: `02-02` fetched
the corpus and committed the fetcher; `02-05` produced the types and the emitter. `02-06` populates
`ResearchComputation.EdhrecPointEstimates`, which `02-05` left as `[]`. **No task makes a network
call — the fetch is done.**

## On-disk facts, verified by LEAD before dispatch

`_edhrec-brackets/` = `cells/` (**1,525** files) + `manifest.json` + `unresolved-slugs.txt`.
Cell top-level keys are **snake_case**, and include `slug`, `bracket`, `bracket_index`, `n_decks`,
`deck` (array of `"<qty> <Name>"`), `land`, `basic`, `nonbasic`, `savedate_summary`.
Sample `adrix-and-nev-twincasters__cedh.json`: `n_decks: 11`, `bracket_index: 5`, 91 deck entries.
`manifest.json` carries `brackets` = the five slugs and **`min_decks: 8000`**.

**The trap, confirmed live:** `min_decks: 8000` is the commander-SELECTION floor (applied against
`averages.csv` `number_decks` when choosing the 305 commanders). The per-cell qualifying floor is
`n_decks >= 400`, read from each cell's OWN `n_decks`. The sample cell shows the two differ by two
orders of magnitude — 11 vs 8000. Conflating them silently changes which cells count.

## Write sets and serialization

| Task | Files |
|---|---|
| 1 | `DeckFlow.Core/Research/EdhrecCellReader.cs` (new), `DeckFlow.Core.Tests/EdhrecCellReaderTests.cs` (new) |
| 2 | `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs` |
| 3 | `DeckFlow.CLI/Program.cs`, `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs` |

Task 1 is Core-only and disjoint from 2–3, which both write the runner.
**Dispatch A = Task 1. Dispatch B = Tasks 2 and 3.** Same split that worked in wave 3.

## Tasks

| ID | Task | Seat | Status |
|---|---|---|---|
| T1 | `EdhrecCellReader` over the real snake_case shape, TDD, hermetic fixtures | codex:gpt-5.4/medium | DONE → `4eb471e4` (Core 1715→1728); verification batched to wave end |
| T2 | Classify cells through `CutLabRoleAssigner.AssignRoles`; populate point estimates + coverage | codex:gpt-5.4/medium | **ACCEPTED** → `610ae71d` |
| T3 | Wire `--edhrec-data` | codex:gpt-5.4/medium | **ACCEPTED** → `4dbdbab6` |
| T3b | Restore the env-var name in `--help` (regression from a defective plan criterion) | codex:gpt-5.4/medium | **DONE** → `168f8ab7` |
| T3c | Correct that criterion in the plan | claude (planning role) | **DONE** → `8092c163` |
| T4 | Move the EDHREC quantity tally into Core and enforce it (verifier finding + user rule) | codex:gpt-5.4/medium | **DONE** → `840e524a` |
| T5 | `02-06-SUMMARY.md` | claude (planning role) | **DONE** → `a38c0133` |

## Outcome — plan 02-06 COMPLETE

Seven commits, `4eb471e4`..`a38c0133`. **Not pushed.** Branch is 63 ahead of `main`.
Final gates: build 0 errors / 9 pre-existing warnings (0 new); Core.Tests **1736** / 0 failed
(1715 at wave start: 1715 → 1728 → 1736); Web.Tests 2095 / 16 skipped / 0 failed, unchanged.
Zero EOL churn. `_edhrec-brackets/` byte-identical throughout — 1,525 cells untouched.

One blind verification over the four code commits: **PASS on every item A–M**, including a third
independent reproduction of 1,525 cells / 805 qualifying / per-bracket 1·284·305·175·40 from a scratch
program against the real corpus. Three independent counts now agree (LEAD, Codex, verifier).

**Both defects this wave were SPECIFICATION defects, not implementation defects.**

1. **A defective grep criterion specified a regression.** Task 3 required
   `grep -c 'DECKFLOW_ROLE_FLOOR_CONNECTION_STRING' Program.cs` == 0, intending "no second READ site" —
   but `02-04` deliberately NAMES the variable in two `--help` strings. The only way to pass was to
   delete the name, degrading `--help` to "the runner's dedicated environment-variable fallback".
   That undercuts D-07: keeping a credential off argv only helps if the operator can discover the
   variable. Restored `168f8ab7`, criterion corrected `8092c163`. **Third defective grep criterion in
   this phase.**
2. **A LEAD stop condition could not be satisfied by any correct implementation** — a live land
   self-check demanded while network calls were forbidden, when the cache lacks basics. Codex stopped
   and correctly diagnosed cache coverage rather than the quantity rule. Requirement replaced.

**The verifier earned its cost on a finding, not a ratification.** It established that a `+= 1`
regression in the quantity tally would have been caught by NOTHING in the repo before the live run —
the reader tests cover the reader, not the runner's tally, and D-03's self-check is a report, not a
gate. Under the standing user rule ([[feedback_cli_logic_in_core_with_tests]]) that is a gap, not a
deferral, so T4 lifted the tally into `DeckFlow.Core/Research/EdhrecRoleTally.cs` with eight tests and
a bite-proof (`Expected: 9   Actual: 1`).

### Carried forward

- **D-03's land self-check still has no real reading.** Offline deltas of −14 to −20 were 100%
  explained by card-cache coverage; `cards_full.json` lacks `island`/`mountain`/`plains`/`swamp`/
  `sol ring`. Plan `02-08`'s live run, after a full resolution pass, is the first real measurement.
- **31 card-count anomalies** on the real corpus — cells not summing to 100. Reported, not judged;
  `02-07` decides whether they belong in a lands comparison.
- **`grep -c 'CutLabRoleAssigner.AssignRoles'` returns 2 while THREE call sites exist** — the probe at
  `:896-897` is split across lines. The count is right for the wrong reason. Verify the Postgres loop
  by reading it, never by counting.
- RFLR-11 (EDHREC ingestion) and RFLR-12 (taxonomy widening) both remain unwritten in
  `REQUIREMENTS.md` — cycle-wide governance no phase plan may edit.

## Attempts (append-only, continued)

## What LEAD added to the T1 ticket beyond the plan

A **reality check against the real cache**, run from a throwaway scratch program outside the repo and
not committed: read at floor 400 and report per-bracket qualifying counts, against the expected
**805 of 1,525** — exhibition 1, core 284, upgraded 305, optimized 175, cedh 40. Instruction is to
STOP and report on a mismatch, never to adjust the reader until it matches. The hermetic unit tests
prove the reader's logic; this proves it against the actual corpus it will run on, which no inline
fixture can.

## Attempts (append-only)

- 2026-07-27 — ledger opened, baseline `b0d0f5c0` recorded, T1 dispatched.
- 2026-07-27 19:05 — T1 `DONE` → `4eb471e4` (2 new files, +931). Core 1715→1728 (+13), Web unchanged,
  build 0 errors, both files LF. TDD honored: RED compile error reported verbatim
  (`CS0246: The type or namespace name 'EdhrecReadResult' could not be found`).

  **The reality check matched EXACTLY** — read against the real cache at floor 400: 1,525 cells,
  **805 qualifying**, per bracket exhibition 1 / core 284 / upgraded 305 / optimized 175 / cedh 40,
  matching the lead's independent count digit for digit. Also observed: invalid 0, missing 0,
  unexpected 0, **cardCountAnomalies 31** (31 cells do not sum to 100 cards — reported, not judged;
  a real input for plan `02-07`'s lands calibration).

  LEAD cheap checks: no `DeckFlow.Web`/classifier reference in the reader; `estimateKind` count 0
  (no validation written for a field that does not exist on disk); `get; }` count 0; and `min_decks`
  appears ONLY inside the `// Why:` comment at `EdhrecCellReader.cs:352`, never in qualification
  logic — the selection-floor/cell-floor trap is closed.

  Scratch program used for the reality check was created outside the repo and not committed;
  `_edhrec-brackets/` unchanged.
- 2026-07-27 — dispatch B sent: plan Tasks 2 and 3, two commits, sequential. LEAD added two
  requirements beyond the plan: (a) an ordering proof run WITHOUT a database — missing connection
  string must fail before any EDHREC read, then a fake unreachable credential plus a bad
  `--edhrec-data` must fail on the EDHREC path before attempting Postgres, proving fail-fast;
  (b) a scratch classification run over real cells reporting the D-03 land self-check deltas, with an
  instruction to STOP if most cells diverge by more than ~2 cards, since that would mean the quantity
  handling is wrong.
- 2026-07-27 19:23 — dispatch B attempt 1 returned **`BLOCKED`. Part 1 complete but UNCOMMITTED;
  Part 2 not started; HEAD unchanged at `4eb471e4`.** Two blockers, and **the first was the LEAD's
  specification error, not Codex's**:

  **(1) The land-self-check stop condition was mis-specified.** Codex measured deltas of −18 to −20
  and stopped as instructed, but correctly diagnosed the cause as card-cache coverage rather than the
  `card.Quantity` tally. LEAD confirmed directly against `_role-floor-research/cards_full.json`
  (14,167 entries): `island`, `mountain`, `plains`, `swamp` and `sol ring` are **absent**; `forest`
  and `arcane signet` present. The scratch run could read the cache but was forbidden from calling
  `ResolveCardsAsync`, so every unresolved basic was skipped and the harness land count came in ~18-20
  low. **The check as written could never have produced a meaningful number** — in a real run the
  EDHREC names are folded into `distinctCardNames` BEFORE resolution and get fetched. Requirement
  replaced: report the unresolved-name count and summed quantity per sampled cell and show it explains
  the delta; stop only if a large delta is UNEXPLAINED by coverage. D-03's real reading comes from
  plan `02-08`'s live run, and the emitted block now says so.

  **(2) The test hang was a leaked process, not the code.** A stale `testhost (10924)` held file locks
  on the test output DLLs (`MSB3027 ... Exceeded retry count of 10. Failed. The file is locked by:
  "testhost (10924)"`). It has since exited; LEAD re-ran the suite clean — **Core 1728 passed / 0
  failed**. Recovery guidance added to the ticket: `dotnet build-server shutdown`, check for a stray
  `testhost`, retry — never `--no-build` around it, never block without a shutdown-and-retry first.

  LEAD verified the uncommitted Part 1 before authorizing continuation: constants declared once at
  `:39`/`:40`, no stray `400` literal, `AssignRoles` exactly 2, +318/−15 confined to the runner.
  Re-dispatched as a **continuation** — explicit instruction to keep the uncommitted work rather than
  restart.

  **Fourth fenced-executor refusal this cycle, and the fourth correct one** — but the first where the
  fence itself was wrong. Worth recording: a stop condition that cannot be satisfied by any correct
  implementation is as much a defect as a missing one.
