---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
current_phase: Phase 4 — Functional-Twins Detector (CONVERGED round 12; 3 of 4 waves executed)
current_plan: `04-04` — next to execute; `autonomous: false`, Task 3 is a human UI checkpoint
status: executing
stopped_at: 2026-08-02 — plan 04-03 executed, verified and committed; 04-04 remains
last_updated: "2026-08-02T17:05:00.000Z"
last_activity: 2026-08-02
progress:
  total_phases: 8
  completed_phases: 4
  total_plans: 28
  completed_plans: 24
  percent: 57
---

> ⚠ **Corrected 2026-08-01. This file had been stale since 2026-07-29** and claimed Phase 3 was
> "planned, not executed" with `03-01-PLAN.md` as the current wave. **Phase 3 completed 2026-07-29**
> (7/7 plans, verified, `469dc9cf`); Phases 1, 01.1 and 2 are also complete. The live session state
> is `.planning/HANDOFF.json` — read it first.
>
> Counters above are derived from the phase directories and the ROADMAP progress table:
> 8 phases (1, 01.1, 01.2, 2, 3, 4, 5, 6), of which 1 / 01.1 / 2 / 3 are complete. 28 plan files
> exist; 01.2 and 6 have none yet. The old `total_plans: 24` predated Phase 5's three plans and
> Phase 4's four.

# Project State

## Current Position

**Status:** Phases 1, 01.1, 2 and 3 complete. Phase 4 is in plan-review convergence with **zero of
its four waves executed**; Phase 5 is planned and unexecuted with an owed review. Phase 01.2 and
Phase 6 have not been started and have no plans.

**Current Phase:** Phase 4 — Functional-Twins Detector (plan review, not execution)
**Last Activity:** 2026-08-01
**Last Activity Description:** Round-4 plan-review findings folded (`5172f6d9`); Phase 4 round 5 and
Phase 5's owed convergence review dispatched to Codex in parallel

**Hard gate:** do not execute Phase 4 or Phase 5 until that phase's own review returns
`VERDICT: CONVERGED`. Rounds 1-4 on Phase 4 all returned CHANGES REQUIRED.

> **Rebase, 2026-08-01.** `gsd/cycle21-cut-lab` rebased onto `main` — 155 commits replayed, two code
> conflicts resolved (`ScryfallCardResolver` using-directives and doc comments; `CutLabPageService`
> where a 91-line block had been refactored to a single flag check), one duplicate commit correctly
> skipped. Build 0 errors / 9 pre-existing CS8629 warnings; Web 2216/0, Core 2011/0. Force-pushed
> with `--force-with-lease` after verifying every remote-only commit's content survived. **Trap for
> next time:** the untracked `.foreman/` ledger directory blocks the rebase and must be moved aside
> first.

> **Rebase, 2026-07-28.** 102 commits replayed onto `main`; 101 remain, one dropped. Five conflicts, all resolved in favour of `main` where both sides had independently fixed the same thing: three e2e specs where both sides disambiguated the sticky-bar locator (main's `.cutlab-sticky-bar[data-cut-lab-sticky-target]` kept as the more specific of the two, which made branch commit `8722a753` wholly redundant — that is the dropped commit), and two `DeckFlow.CLI/Program.cs` seams where main's `--thresholds` option sits directly above the role-floor command block. Verified afterwards: zero CR bytes anywhere in the diff, the whitespace-ignored diff identical to the full diff (so no EOL churn), build 0 errors / 9 pre-existing warnings, and 4,512 tests passing across Studio 426, Web 2098, Core 1988. Recovery point retained at branch `backup/cycle21-cut-lab-pre-rebase-2026-07-28` (`37275a87`).
## Progress

**Total phases:** 8 — 1, 01.1, 01.2, 2, 3, 4, 5, 6
**Phases complete:** 4 — Phase 1 (`9527dc72`), Phase 01.1 (2/2, `b8ec09f3`), Phase 2 (11/11,
841 qualifying commanders, lands PULLED), Phase 3 (7/7 verified, `469dc9cf`)
**Phases planned but unexecuted:** 4 (4 plans), 5 (3 plans)
**Phases with no plans yet:** 01.2 Protection-Vocabulary Widening, 6 Scryfall Throughput
**Current plan:** none executing

> **Counter discrepancy — resolved 2026-08-01, previously left open.** The old note kept
> `completed_phases` at 1 because summary evidence was ambiguous: Phase 01 has a PLAN and no SUMMARY,
> and Phase 2's `02-10` / `02-11` executed without summaries. Resolved in favour of the ROADMAP
> progress table, which records all four phases as Complete with commit SHAs and dates. **The missing
> summaries are an artifact gap, not unexecuted work** — the code shipped and is on `main`. Counting
> plans by summary file undercounts for exactly this reason.

> ⚠ **Phase 01.2 is the easy one to miss.** It was inserted from Phase 01.1's D-06 and sits between
> 01.1 and 2 in the numbering, so a reader scanning "1, 2, 3, 4, 5" skips it. It has never been
> started and has no plans. Whether it is in scope for the current "full milestone" push is an open
> question for the developer — the scope was chosen before 01.2 was noticed and before Phase 6
> existed.

## Decisions Resolved (2026-07-27)

- **D-A — RESOLVED:** Hybrid corpus. See the ROADMAP Phase 2 block for the reasoning.
- **D-B — RESOLVED:** 25th-percentile floor. See the ROADMAP Phase 2 block for the reasoning.
- **D-C — RESOLVED:** Lands and ramp are in scope. See the ROADMAP Phase 2 block for the reasoning.

## Uncommitted Work In This Worktree

**None — resolved 2026-08-01.** Everything the previous revision of this section listed as
uncommitted has since been committed and is tracked: `RoleFloorResearchCommandRunner.cs`,
`RoleFloorDivergenceStats.cs` and its tests, the `boardFilter` passthroughs, the `Program.cs` command
wiring and the schema-parity test additions all verified tracked via `git ls-files`.

Only two untracked directories remain, both intentional research caches that should survive:
`_role-floor-research/` and `_edhrec-brackets/`. Whether to gitignore them is still a developer
follow-up — `.gitignore` is do-not-modify without explicit permission.

**The RESEARCH-FINDINGS deletion warning is obsolete — do NOT act on it.** The previous revision said
`phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.md` and `.json` were synthetic fixture
output that must be deleted. That referred to the discarded fixture. The files now present are the
**real run**, committed deliberately at `ed20e0430` ("commit the role-floor research findings and run
evidence"): 5.6 MB of genuine output covering 841 qualifying commanders, and Phase 3 independently
recomputed its 678-commander / 1463-floor snapshot from this exact JSON. Deleting them would destroy
Phase 3's evidence base. (The Greek-letter strings that once signalled the fixture are ordinary card
names in the real data — do not use them as a fixture tell.) The original incident record stays in
PROJECT.md for history.

## Session Continuity

**Stopped At:** 2026-08-02. Phase 4 plans CONVERGED at round 12 (`5246033e`); plans `04-01`
(`476daf66`), `04-02` (`19fdce26`) and `04-03` executed and committed. `04-04` is next.

`04-03` note: Codex `gpt-5.6-terra` wrote Tasks 1-2 and most of Task 3, then correctly returned
BLOCKED rather than weakening the page-vs-patch parity assertion — but reported one failing test
where there were four. Both defects were in the new test fixture, not production. The round-2 fix
dispatch failed with `ERROR: Your workspace is out of credits`, and the operator authorized Claude
to finish. **A Codex review of 04-03 is owed** and is the first in this phase whose code Codex did
not write. Full run record was in `.foreman/ledger.md` (gitignored, local only).
**Resume File:** `.planning/HANDOFF.json` — the authoritative live handoff. Read it before anything
else; it carries the blockers, the dispatched-review state and the remaining milestone steps.

**Owed on resume:**
- `gsd/cycle21-cut-lab` is 4 ahead of origin (`476daf66`, `50830c6b`, `19fdce26`, `7b1ff22a`).
  User pushes; AI does not. `main` is fully pushed (`4cadf943` landed).
- Phase 5 round-9 HIGH still open: the round-8 TRX regex is too permissive. Fix is fully specified
  in `HANDOFF.json` (exact `FullyQualifiedName=` filters, per-name counters, require 1/1/2).
  Dispatch to Codex — hand-editing is what produced the defect.
- `04-04` is `autonomous:false`; Task 3 is a human UI checkpoint at ~1440px and 390x844.
  Ask permission before opening any browser.

## Accumulated Context

### Roadmap Evolution

- Phase 01.1 edited: shortened auto-generated title/goal to a clean summary
