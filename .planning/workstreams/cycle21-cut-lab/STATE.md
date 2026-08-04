---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
current_phase: Phase 7 — Cut Lab Workflow UX (executing, 3 of 6 plans)
current_plan: `07-04` code complete + SUMMARY written; only Task 5, a HUMAN UAT checkpoint, remains
status: executing
stopped_at: 2026-08-04 — `8ea1e41d` repaired the gate suite; **all four gates now PASS 8/8 on both viewports**. G-3 measures the Decide panel at 731px desktop / 1,099px mobile against 3,000/4,000. Every pre-`8ea1e41d` G-3 number is uninformative — three of four gates were green while incapable of failing. 07-05 does NOT target G-3.
last_updated: "2026-08-04T15:55:00.000Z"
last_activity: 2026-08-04
progress:
  total_phases: 10
  completed_phases: 5
  total_plans: 34
  completed_plans: 28
  percent: 66
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

**Status:** Phases 1, 01.1, 2, 3 **and 4** complete. Phase 4 plans **CONVERGED at round 12** and all
four waves are executed — `04-01` (`518d7d83`), `04-02` (`dbd46e94`), `04-03` (`b508f27e`) and
`04-04` (Tasks 1-2 `8b5d2e8e..908402cd`, HIGH fold `1fc48dd6`). **Those SHAs are post-rebase**; the
pre-rebase `476daf66` / `19fdce26` / `c9b55d1b` appear throughout the older notes.

✅ **Phase 4 CLOSED 2026-08-03.** `04-04` Task 3, the `checkpoint:human-verify` gate that was Success
Criterion 5's only authoritative check, was run headlessly and **approved**. Real pool: EDHREC's cEDH
average list for Kinnan, Bonder Prodigy plus 32 cEDH staples as over-sized intake, 132/100 cards,
Bracket 5. One merged "Functional twins" section with **9 groups / 38 evidence chips over 32 distinct
cards**, ordering descending by mana value; findings 31 → 40. TWIN-02 confirmed through the UI by a
**changed card-to-round assignment** (Round 2 → Round 1) rather than a changed proposal card — the
alternative the plan allows. AJAX decide returned 200 with no reload and the section survived intact.
Layout cost attributable to the phase: **+805px desktop / +1,835px at 390px**, against a page that is
already 19,610px / 45,177px with the flag OFF. Evidence:
`.planning/ui-design/cut-lab/twins-checkpoint-2026-08-03/`. **The production flag stays OFF** — the
phase deployed dark and flipping it is a separate developer decision.
Two follow-ups raised at approval, both deliberately unfixed: **F-04-09** multi-role cards emit
duplicate twin groups with identical card sets (9 groups covered only 32 distinct cards; dedupe by
card set is the cheaper lever if tightening is ever wanted, ahead of raising
`TwinGroupMinimumCards`), and **F-04-10** the mobile "ON THIS PAGE" anchor nav overlays findings
content at 390px (page-level and pre-existing; fold into Phase 7).

Phase 5 is planned and unexecuted; its last open HIGH was folded 2026-08-03. **Phase 7 (Cut Lab Workflow UX) was adopted
2026-08-02** with 6 plans; its `04-04` gate is **discharged**, so Phase 7 is execute-ready.
**Phase 8 (Plan Profile — checkbox plan selection)
was adopted 2026-08-02 and PLANNED 2026-08-02** — 8 plans in 6 waves (`1a44879f`, checker pass
0 BLOCK/0 HIGH, findings folded `f04f0154`). Design spec at
`.planning/specs/2026-08-02-cutlab-plan-profile-design.md`; engine plans (08-01..08-06) parallel
across waves 1-4; plan-panel UI plans (08-07, 08-08) gated on Phase 7.
✅ **Phase 8 plan set is now on `gsd/cycle21-cut-lab`.** It was planned on `feat/ui-audit-batch-a`
(adoption commit `be421acd`), which reached `main` and then this branch via the 2026-08-03 rebase.
The earlier "NOT on `gsd/cycle21-cut-lab` or `main` yet" note was true when written and is now
stale.
✅ **Codex plan review for Phase 8 RUN 2026-08-03 and FOLDED** — see `phases/08-.../08-REVIEWS.md`.
The review returned CHANGES REQUIRED (2 BLOCK · 7 HIGH · 4 MEDIUM) against a plan set the
same-family checker had passed clean. Both BLOCKs are resolved in the fold at `af43a4e4`:
`08-06-PLAN.md` no longer prescribes the constructor change that would have broken 70
`CutLabPageService` construction sites, and `08-07-PLAN.md` now requires the legacy-session
fixture instead of silently destroying restored user sessions. Round 2 converged — 0 BLOCK,
0 HIGH, 5 LOW wording notes. **Phase 8 is execute-ready.**
Phase 8 planning notes: the reorder effect lands in `CutLabCutRoundEngine.BuildQueue`
(`:266-332`, `ComboProtectionRank :483`), NOT `CutLabNextProposalBuilder` as the spec says — that
file is a DTO mapper; and `CutLabIntent` lives at `DeckFlow.Web/Models/CutLab/CutLabState.cs:189`,
not `Services/CutLab/`. Phases 01.2 and 6 have not been started and have no plans.

**Current Phase:** Phase 4 — Functional-Twins Detector (COMPLETE, 4/4)
**Last Activity:** 2026-08-03
**Last Activity Description:** `04-04` Task 3 human UI checkpoint run headlessly against a real
132-card Kinnan cEDH pool and **approved**; Phase 4 closed with two new follow-ups (F-04-09, F-04-10)

**Hard gate — Phase 5: DISCHARGED 2026-08-03.** The gate required `VERDICT: CONVERGED`; round 9 had
left 1 HIGH open and round 10 confirmed it plus two defects in its own prescribed fix. The Codex
review owed since round 1 ran 2026-08-03 and its findings are folded at `af43a4e4`. **Phase 5 is
execute-ready.** **Phase 4's gate is discharged** — it converged at round 12 (`5246033e`, now
rewritten by the rebase), which is why waves 1-3 were executed.

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

**Total phases:** 10 — 1, 01.1, 01.2, 2, 3, 4, 5, 6, 7, 8 (the old "9" predated Phase 8's adoption
and disagreed with this file's own `total_phases: 10` frontmatter)
**Phases complete:** 5 — Phase 1 (`9527dc72`), Phase 01.1 (2/2, `b8ec09f3`), Phase 2 (11/11,
841 qualifying commanders, lands PULLED), Phase 3 (7/7 verified, `469dc9cf`), Phase 4 (4/4, human
checkpoint approved 2026-08-03)
**Phases planned but unexecuted:** 5 (3 plans, execute-ready), 7 (6 plans, execute-ready — its
`04-04` gate is discharged), 8 (8 plans, execute-ready; `08-07`/`08-08` gated on Phase 7)
**Phases with no plans yet:** 01.2 Protection-Vocabulary Widening, 6 Scryfall Throughput
**Current plan:** none in flight

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

- **D-1 Step model — RESOLVED 2026-08-03 as Option 3: wizard + pinned proposal (~1,107px desktop
  against today's 10,453px).** Consequences: `07-03` keeps runtime panel-hiding and G-2's
  exactly-one-visible assertion, and **`07-05` exists and executes** (it would not have under
  options 1 or 2). The wizard has five slots, with `cut-lab-step-panel-3` at index 3 reserved empty
  for Phase 8's plan panel. Mockups rendered against the real site CSS live in
  `.planning/ui-design/cut-lab/proposed/`. **The HTML mockups pull site CSS from
  `http://localhost:5173`** and render unstyled without the dev server — the six PNGs are the
  self-contained artifact.
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

**Stopped At:** 2026-08-03. **Phase 4 is closed** — `04-04` Task 3's human UI checkpoint ran and was
approved; see the Phase 4 block under Current Position for the measurements. Nothing is in flight.
Next is a choice between Phase 5 (3 plans, execute-ready), Phase 7 (6 plans, now ungated) and Phase 8
(engine plans `08-01`..`08-06`, execute-ready). The earlier rebase-and-ff note under Current Position
still describes how `main` and this branch were reconciled.

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
- ✅ **Push DONE 2026-08-03.** `main` and `gsd/cycle21-cut-lab` are both pushed and level with
  origin. The force-with-lease the rewritten history needed has been done. User pushes; AI does not.
  Pushing `main` triggers a Render autodeploy — safe, because `tool.cut-lab.enabled` is `false` in
  prod.
- ✅ **Codex review of `b508f27e` (plan `04-03`) RUN 2026-08-03 — GATE DISCHARGED.** No BLOCK, no
  HIGH; 2 MEDIUM and 1 LOW recorded as follow-ups in `phases/04-.../04-REVIEWS.md`. Neither MEDIUM
  blocks the phase.
- ✅ **Codex review of `04-04` Tasks 1-2 — both HIGHs FOLDED at `1fc48dd6` (2026-08-03).** Live range
  was `8b5d2e8e..908402cd`; the ranges recorded in the summaries (`9594b2f6..0a9e7cc9`) are STALE
  after the rebase. HIGH-1: twins tallies could promote a complete combo piece into round 1 over its
  `ComboProtected` finding — combo membership is now resolved **before** `BuildFindingTallies` and
  passed in, so twins evidence is skipped for those cards while non-twins evidence still counts.
  HIGH-2: the repaired density population is now asserted, so reverting the filler roles fails the
  test. A blind verifier over the fix also found a dead `IsFlagOn` helper, removed in the same
  commit. **Stage 1 (`codex review`) found nothing on this same range; stage 2 found both** — the
  standing argument for running stage 2 when a change widens a window rather than altering a path.
- ✅ **Phase 5 round-9 HIGH RESOLVED — folded at `af43a4e4` (2026-08-03).** The history is worth
  keeping because two successive prescribed fixes were themselves defective. Round 8's hand-authored
  TRX regex was too permissive: `testName="[^"]*(name1|name2)[^"]*"` plus **substring**
  `FullyQualifiedName~` filters let suffix-extended tests satisfy the gate, confirmed with a
  synthetic TRX where `RunAsync_MetadataBearingImport_PersistsMetadata_Unrelated` and one other
  unrelated name produced `failed_count=2`. Round 9 prescribed exact-name matching; round 10 found
  *that* broken too — exact TRX `testName` equality cannot match a `[Theory]`, whose TRX names carry
  per-case argument suffixes, so the gate matched zero rows and passed on zero failures. **The landed
  fix pins both tests as `[Fact]`** with byte-exact names, stated in the task body and in `<done>`,
  alongside exact `FullyQualifiedName=` filters and the `first_failed=1 / second_failed=1 /
  total_failed=2` counters. Generalizable: **a gate keyed on an exact test name is silently void if
  the test is parameterized.**
- ✅ **`04-04` Task 3 DONE 2026-08-03 — APPROVED.** The human UI checkpoint ran at 1440px and
  390x844 across Azorius (light) and Nyx (dark), with the flag ON **locally only** via `/Admin/Flags`
  and restored to OFF afterwards; the production row was never touched. Full record in
  `04-04-SUMMARY.md`, evidence in `.planning/ui-design/cut-lab/twins-checkpoint-2026-08-03/`.
  **Phase 4 is closed.** Two follow-ups came out of it: F-04-09 (duplicate twin groups from
  multi-role cards) and F-04-10 (mobile anchor nav overlays findings).
- ⚠ **Trap for any future admin automation: put basic-auth in a HEADER, never in the URL.**
  Chromium sends URL-embedded credentials only after a 401, and `AdminBruteForceTrackerStore` counts
  each 401 (10 per 15 minutes, partitioned on `CF-Connecting-IP`). Ten admin page loads locked the
  entire admin surface out with 429 mid-checkpoint and the run had to be restarted.
  `e2e/support/admin-lock.ts` sets `Authorization` plus a per-test `CF-Connecting-IP` for exactly
  this reason — copy that, do not re-derive it.

## Accumulated Context

> ⚠ **Every `gsd-tools query` in this milestone needs `--ws cycle21-cut-lab`.** Without it the query
> answers from the **root** `.planning/ROADMAP.md` — Cycle 20, phases 111-116 — not this workstream.
> `.planning/active-workstream` does contain `cycle21-cut-lab`, but the stored pointer loses to
> session-scoped resolution, and the accepted flag is `--ws` (not `--workstream`; the env var is
> `GSD_WORKSTREAM`, not `GSD_WS`). Measured 2026-08-03: `roadmap.get-phase 112` returns
> `found: true` while `roadmap.get-phase 7` returns `found: false`, and adding `--ws` flips it.
> **This fails silently** — a wrong-milestone answer has the same shape as a right one, so a bare
> query that "works" is not evidence the pointer was honored.

### Roadmap Evolution

- Phase 01.1 edited: shortened auto-generated title/goal to a clean summary
