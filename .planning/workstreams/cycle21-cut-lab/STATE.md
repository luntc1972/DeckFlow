---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
current_phase: Phase 1 — Interaction Taxonomy Split (not started)
current_plan: None written. `phases/02-role-floor-divergence-research/02-01-PLAN.md` exists from the pre-re-plan pass and is **stale** — it plans harness construction, but the harness already exists and the phase is now repair-and-run.
status: Re-planned — 5 phases scoped, no phase plans written yet
stopped_at: Re-plan complete; phase plans not yet written
last_updated: "2026-07-27T03:19:02.548Z"
last_activity: 2026-07-26
progress:
  total_phases: 6
  completed_phases: 0
  total_plans: 2
  completed_plans: 0
  percent: 0
---

# Project State

## Current Position

**Status:** Re-planned — 5 phases scoped, no phase plans written yet
**Current Phase:** Phase 1 — Interaction Taxonomy Split (not started)
**Last Activity:** 2026-07-26
**Last Activity Description:** Milestone re-planned from 2 phases to 5 and renamed (`research/cutlab-role-floors` → `gsd/cycle21-cut-lab`, workstream dir `cutlab-role-floors` → `cycle21-cut-lab`). Driven by a Cut Lab review against community cutting methodology: the original scope improved guardrails only, because both floor-derived findings sit in `ExcludedFindingKindsFromTally` and so cannot change proposal ranking. Added the interaction split (Phase 1, unblocks the research), functional twins (Phase 4, the cycle's only ranking change), and Archidekt bracket capture (Phase 5, user requirement). Prior EDHREC commander × bracket research pulled in from the archive. Per-phase release posture defined — phases ship independently as they complete.

## Progress

**Total phases:** 6 (Phase 01.1 inserted urgent, ahead of Phase 2; Phase 3 conditional on Phase 2 go/no-go; Phases 4 and 5 independent)
**Phases Complete:** 0
**Current Plan:** None written. `phases/02-role-floor-divergence-research/02-01-PLAN.md` exists from the pre-re-plan pass and is **stale** — it plans harness construction, but the harness already exists and the phase is now repair-and-run.

## Open Decisions Blocking Plan Writing

1. **Phase 2 corpus choice** — Postgres Archidekt (real distributions, no bracket, known coverage gaps) vs EDHREC average-decks (bracket built in, ≥400 decks/cell, but point estimates only, no within-commander variance). Hybrid likely correct. See ROADMAP "Prior Research".
2. **Phase 3 floor statistic** — 25th percentile vs mean. The prior findings doc argued P25 (a mean-derived floor puts ~half the commander's own decks below it); EDHREC average-decks cannot supply P25. Interacts with decision 1.

## Uncommitted Work In This Worktree

Real and worth keeping, but unreviewed:

- `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs` (985 LOC) — has a working Postgres path; also contains the orphaned synthetic fixture writer that must be deleted (RFLR-09).
- `DeckFlow.Core/Research/RoleFloorDivergenceStats.cs` (126 LOC) + `RoleFloorDivergenceStatsTests.cs` (116 LOC).
- `boardFilter` parameter added to `CardCategoryRepository.GetCategoryDeckMembershipForCommanderAsync` plus passthroughs in `CategoryKnowledgeRepository` / `DeckQueueRepository`; parameter is declared after `CancellationToken`, violating project convention.
- `DeckFlow.CLI/Program.cs` command wiring, `CategoryCacheSchemaParityTests.cs` additions.

**Must be deleted, not amended:** `phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.md` and `.json` are fixture output, not a run (commanders named Alpha/Beta/Gamma/Delta; `ClearsBar` contradicts its own inputs; run log and exit file both 0 bytes). Both are untracked — nothing false reached git history. See PROJECT.md "Incident".

## Session Continuity

**Stopped At:** Re-plan complete; phase plans not yet written
**Resume File:** None

## Accumulated Context

### Roadmap Evolution

- Phase 01.1 edited: shortened auto-generated title/goal to a clean summary
