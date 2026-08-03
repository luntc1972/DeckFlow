---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
current_phase: Phase 4 — Functional-Twins Detector (CONVERGED round 12; 3 of 4 waves executed)
current_plan: `04-04` — next to execute; `autonomous: false`, Task 3 is a human UI checkpoint
status: executing
stopped_at: 2026-08-02 — plan 04-03 executed and committed; branch rebased onto main and main ff'd to it; 04-04 remains
last_updated: "2026-08-02T18:50:00.000Z"
last_activity: 2026-08-02
progress:
  total_phases: 10
  completed_phases: 4
  total_plans: 34
  completed_plans: 24
  percent: 57
---

> ⚠ **Corrected 2026-08-01. This file had been stale since 2026-07-29** and claimed Phase 3 was
> "planned, not executed" with `03-01-PLAN.md` as the current wave. **Phase 3 completed 2026-07-29**
> (7/7 plans, verified, `469dc9cf`); Phases 1, 01.1 and 2 are also complete. **`HANDOFF.json` no
> longer exists** — it was deleted 2026-08-02 after going stale enough to mislead a resume. This
> file is the live state.
>
> Counters above are derived from the phase directories and the ROADMAP progress table:
> 10 phases (1, 01.1, 01.2, 2, 3, 4, 5, 6, 7, 8), of which 1 / 01.1 / 2 / 3 are complete. 34 plan
> files exist; 01.2, 6 and 8 have none yet. The old `total_plans: 24` predated Phase 5's three plans
> and Phase 4's four.

# Project State

## Current Position

**Status:** Phases 1, 01.1, 2 and 3 complete. Phase 4 plans **CONVERGED at round 12** and **3 of its
4 waves are executed** — `04-01` (`518d7d83`), `04-02` (`dbd46e94`) and `04-03` (`b508f27e`) are
committed and on `main`; only `04-04` remains. **Those SHAs are post-rebase**; the pre-rebase
`476daf66` / `19fdce26` / `c9b55d1b` appear throughout the older notes.

Phase 5 is planned and unexecuted with one open HIGH. **Phase 7 (Cut Lab Workflow UX) was adopted
2026-08-02** with 6 plans, gated on plan `04-04`. **Phase 8 (Plan Profile — checkbox plan selection)
was adopted 2026-08-02 and PLANNED 2026-08-02** — 8 plans in 6 waves (`1a44879f`, checker pass
0 BLOCK/0 HIGH, findings folded `f04f0154`). Design spec at
`.planning/specs/2026-08-02-cutlab-plan-profile-design.md`; engine plans (08-01..08-06) parallel
across waves 1-4; plan-panel UI plans (08-07, 08-08) gated on Phase 7.
✅ **Phase 8 plan set is now on `gsd/cycle21-cut-lab`.** It was planned on `feat/ui-audit-batch-a`
(adoption commit `be421acd`), which reached `main` and then this branch via the 2026-08-03 rebase.
The earlier "NOT on `gsd/cycle21-cut-lab` or `main` yet" note was true when written and is now
stale.
✅ **Codex plan review for Phase 8 RUN 2026-08-03** — see `phases/08-.../08-REVIEWS.md`.
⛔ **Verdict: CHANGES REQUIRED — 2 BLOCK · 7 HIGH · 4 MEDIUM, none folded. Do NOT run
`/gsd-execute-phase 8`** until at least both BLOCKs are resolved: `08-06-PLAN.md:199` will not
compile (70 missed `CutLabPageService` construction sites), and `08-07-PLAN.md:143` silently
destroys restored user sessions. The same-family checker had passed this plan set clean.
Phase 8 planning notes: the reorder effect lands in `CutLabCutRoundEngine.BuildQueue`
(`:266-332`, `ComboProtectionRank :483`), NOT `CutLabNextProposalBuilder` as the spec says — that
file is a DTO mapper; and `CutLabIntent` lives at `DeckFlow.Web/Models/CutLab/CutLabState.cs:189`,
not `Services/CutLab/`. Phases 01.2 and 6 have not been started and have no plans.

**Current Phase:** Phase 4 — Functional-Twins Detector (executing, 3/4)
**Last Activity:** 2026-08-02
**Last Activity Description:** `04-03` executed, verified and committed; branch rebased onto `main`
and `main` fast-forwarded to it at `d0eddea6`

**Hard gate — Phase 5 only.** Do not execute Phase 5 until its review returns `VERDICT: CONVERGED`;
round 9 returned 1 HIGH. **Phase 4's gate is discharged** — it converged at round 12 (`5246033e`,
now rewritten by the rebase), which is why waves 1-3 were executed.

> **Rebase + ff to main, 2026-08-02.** `gsd/cycle21-cut-lab` rebased onto `main` (`cec908a8`) —
> 196 commits attempted, **195 applied, 1 dropped**: `ae78fd68` ("adopt the canonical GSD model
> routing") was detected as already upstream, exactly as planned when `main`'s `4cadf943` was made
> byte-identical to it. **One conflict**, in `.gitignore`, purely additive — `main` had added a
> `__pycache__/` block and the branch a `.foreman/` block at the same position; both kept verbatim,
> nothing invented. Verified afterwards: `main` is an ancestor so the ff was legal; **0 CR bytes** in
> the whole diff and only 3 whitespace-only lines out of 628,708, so no EOL churn; build 0 errors /
> 9 pre-existing CS8629; **Core 2011/0, Web 2266/0 (16 skipped)**. `main` then fast-forwarded to the
> branch tip `d0eddea6`; the two refs are now identical. Recovery point:
> `backup/cycle21-cut-lab-pre-rebase-2026-08-02` (`23992715`).
>
> **The `.foreman` trap is retired.** It blocked the rebase because commit `804fb13d` *deletes* those
> tracked ledger files and git refuses to clobber an untracked directory in that position. It was
> moved aside for the rebase and restored after; the same commit gitignores `.foreman/`, so it will
> not block a future rebase.
>
> **Landing was safe because prod is dark.** Verified live against the Render Postgres at rebase
> time: `tool.cut-lab.enabled = false`. The whole Cut Lab surface is unreachable in prod, so the
> incomplete Phase 4 (no twins UI until `04-04`) and the flag-dark twins detector ship invisible.
> `analysis.cut-lab.commander-floors = true` in prod but sits inside the disabled tool.

> **Rebase, 2026-08-01.** `gsd/cycle21-cut-lab` rebased onto `main` — 155 commits replayed, two code
> conflicts resolved (`ScryfallCardResolver` using-directives and doc comments; `CutLabPageService`
> where a 91-line block had been refactored to a single flag check), one duplicate commit correctly
> skipped. Build 0 errors / 9 pre-existing CS8629 warnings; Web 2216/0, Core 2011/0. Force-pushed
> with `--force-with-lease` after verifying every remote-only commit's content survived. **Trap for
> next time:** the untracked `.foreman/` ledger directory blocks the rebase and must be moved aside
> first.

> **Rebase, 2026-07-28.** 102 commits replayed onto `main`; 101 remain, one dropped. Five conflicts, all resolved in favour of `main` where both sides had independently fixed the same thing: three e2e specs where both sides disambiguated the sticky-bar locator (main's `.cutlab-sticky-bar[data-cut-lab-sticky-target]` kept as the more specific of the two, which made branch commit `8722a753` wholly redundant — that is the dropped commit), and two `DeckFlow.CLI/Program.cs` seams where main's `--thresholds` option sits directly above the role-floor command block. Verified afterwards: zero CR bytes anywhere in the diff, the whitespace-ignored diff identical to the full diff (so no EOL churn), build 0 errors / 9 pre-existing warnings, and 4,512 tests passing across Studio 426, Web 2098, Core 1988. Recovery point retained at branch `backup/cycle21-cut-lab-pre-rebase-2026-07-28` (`37275a87`).
## Progress

**Total phases:** 9 — 1, 01.1, 01.2, 2, 3, 4, 5, 6, 7
**Phases complete:** 4 — Phase 1 (`9527dc72`), Phase 01.1 (2/2, `b8ec09f3`), Phase 2 (11/11,
841 qualifying commanders, lands PULLED), Phase 3 (7/7 verified, `469dc9cf`)
**Phase 4:** executing, 3 of 4 plans committed — see Current Position for the SHAs
**Phases planned but unexecuted:** 5 (3 plans), 7 (6 plans, gated on `04-04`)
**Phases with no plans yet:** 01.2 Protection-Vocabulary Widening, 6 Scryfall Throughput
**Current plan:** `04-04`, next to execute

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

## Open Decision — Phase 7

- **D-1 Step model — OPEN, blocks `07-04` and gates `07-05`.** Pick option 1 (true wizard, 1,022px
  desktop), 2 (soft fix, 1,596px) or 3 (wizard + pinned proposal, 1,107px) against today's
  10,453px. Mockups rendered against the real site CSS live in
  `.planning/ui-design/cut-lab/proposed/`. `07-05` exists only if option 3 wins; every other plan is
  identical across the three. **The HTML mockups pull site CSS from `http://localhost:5173`** and
  render unstyled without the dev server — the six PNGs are the self-contained artifact.
- **D-4 Branch — RESOLVED 2026-08-02:** Phase 7 runs on `gsd/cycle21-cut-lab`, not a separate
  branch. Rationale in that phase's `README.md`.

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

**Stopped At:** 2026-08-02. Phase 4 waves 1-3 executed and committed, then the branch was rebased
onto `main` and `main` fast-forwarded to it — see the rebase note under Current Position for the
verification gates and the post-rebase SHAs. `04-04` is next.

`04-03` note: Codex `gpt-5.6-terra` wrote Tasks 1-2 and most of Task 3, then correctly returned
BLOCKED rather than weakening the page-vs-patch parity assertion — but reported one failing test
where there were four. Both defects were in the new test fixture, not production. The round-2 fix
dispatch failed with `ERROR: Your workspace is out of credits`, and the operator authorized Claude
to finish. **That Codex review of 04-03 ran 2026-08-03 and DISCHARGED the gate** (no BLOCK, no
HIGH); it was the first in this phase whose code Codex did not write. Full run record was in `.foreman/ledger.md` (gitignored, local only).
**Resume File:** none. `.planning/HANDOFF.json` was deleted 2026-08-02 — it had drifted far enough
to be a hazard, still listing `04-03` as `not_started` after that plan was executed, verified and
committed. Everything it carried that outlives a session was moved here or confirmed present in the
phase docs before deletion. **This file plus the phase `.continue-here.md` are the live state.**

**Owed on resume:**
- **Push, superseded 2026-08-02 by the rebase + ff.** `main` is now **195 ahead of `origin/main`, 0
  behind**, at `d0eddea6`. `gsd/cycle21-cut-lab` points at the same commit but its history was
  rewritten, so origin diverges 190↔204 and the branch needs **`--force-with-lease`**. User pushes;
  AI does not. Pushing `main` triggers a Render autodeploy — safe, because `tool.cut-lab.enabled`
  is `false` in prod.
- ✅ **Codex review of `b508f27e` (plan `04-03`) RUN 2026-08-03 — GATE DISCHARGED.** No BLOCK, no
  HIGH; 2 MEDIUM and 1 LOW recorded as follow-ups in `phases/04-.../04-REVIEWS.md`. Neither MEDIUM
  blocks the phase.
- ⛔ **Codex review of `04-04` Tasks 1-2 RUN 2026-08-03 — CHANGES REQUIRED, 2 HIGH unfolded.** Live
  range is `8b5d2e8e..908402cd`; the ranges recorded in the summaries (`9594b2f6..0a9e7cc9`) are
  STALE after the rebase. Both HIGHs are in `04-REVIEWS.md`: twins tallies can promote a complete
  combo piece into round 1 over its `ComboProtected` finding (`CutLabCutRoundEngine.cs:460`), and
  the repaired 92-card density population is never asserted, so reverting the 64 filler roles leaves
  every test green (`CutLabFunctionalTwinsDensityTests.cs:393`). Stage 1 (`codex review`) found
  nothing on this same range; stage 2 found both.
- **Phase 5 round-9 HIGH still open — and its prescribed fix is itself defective.** Round 10
  (2026-08-03) CONFIRMED the round-9 defect at `05-03-PLAN.md:143` and found two new problems in the
  prescribed fix: the two pinned test methods do not exist yet (Task 1 creates them — a sequencing
  constraint, not a phantom), and exact TRX `testName` equality is incompatible with `[Theory]`, so
  the gate would fail closed. **Do not apply the round-9 fix as written.** Rounds 9 and 10 are now
  both recorded in `05-REVIEWS.md`; the spec below is retained for continuity.

  *Defect:* the round-8 TRX regex, hand-authored rather than dispatched, is too permissive.
  `testName="[^"]*(name1|name2)[^"]*"` combined with **substring** `FullyQualifiedName~` filters lets
  suffix-extended tests satisfy the gate — the reviewer confirmed with a synthetic TRX that
  `RunAsync_MetadataBearingImport_PersistsMetadata_Unrelated` and a second unrelated name together
  produce `failed_count=2`, passing a gate that is supposed to prove two *specific* tests failed.

  *Fix:* use **exact** `FullyQualifiedName=DeckFlow.Core.Tests.ArchidektDeckCacheSessionTests.<method>`
  filters, one exact counter per fully qualified name plus a total, and require
  `first_failed=1`, `second_failed=1`, `total_failed=2`.

  *Dispatch it to Codex — hand-editing is what produced this defect in the first place.*
- `04-04` is `autonomous:false`; Task 3 is a human UI checkpoint at ~1440px and 390x844.
  Ask permission before opening any browser.

## Accumulated Context

### Roadmap Evolution

- Phase 01.1 edited: shortened auto-generated title/goal to a clean summary
