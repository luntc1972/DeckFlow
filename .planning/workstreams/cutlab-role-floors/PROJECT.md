# Project: DeckFlow — Cycle 21 Commander-Aware Role Floors (workstream)

**Full project history:** `.planning/PROJECT.md` on `main`/`feat/personal-tools` — this workstream is
scoped to one milestone and does not duplicate that file. Read the root `PROJECT.md` for prior
milestone context if needed.

## What This Is

DeckFlow is a Magic: The Gathering deck analysis tool for cEDH/Commander players
(deckflow.gg). Core value: every supported workflow produces output the user can paste into
ChatGPT/Claude/Gemini and get a useful answer in one round-trip.

## Current Milestone: Cycle 21 — Commander-Aware Role Floors

**Goal:** Validate whether per-commander divergence in Cut Lab's 5 role floors (interaction,
protection, engines, payoffs, win conditions) is real signal or corpus noise and, if the signal
holds up, ship commander-aware floor defaults using the same priority-chain pattern already
proven for lands/ramp/draw.

**Target features:**
- Phase 1 (research): re-run per-commander role classification using the real production
  classifiers (`DeckStatClassifier`, `PlanRoleClassifier` in `DeckFlow.Core`), not a throwaway
  Python reimplementation; expand the commander sample past the 4 sampled ad hoc last session
  (Sokka, Edgar Markov, Krenko, Atraxa); define and apply a statistical bar for "real" divergence
  vs. sampling noise; produce a written findings doc with an explicit go/no-go recommendation.
- Phase 2 (conditional on Phase 1 go): design and implement commander-aware role-floor defaults
  in `CutLabFloorDefaults.cs`, reconciled with the existing `ManabaseBaseline` three-layer
  architecture (`EdhrecAveragesConverter` / `ManabaseBaselineProvider` / `CedhLandBaselineProvider`)
  where that pattern fits, without breaking the existing bracket+plan fallback.

**Key context:**
- Isolated from the concurrently in-flight Cycle 20 "Personal Tools" milestone
  (`feat/personal-tools`, creator-style port) — different branch (`research/cutlab-role-floors`),
  different worktree, different workstream. No shared files expected to be touched by both.
- Prior shipped, closed work: Phase 102 "Structural Analysis & Role Floors"
  (`.planning/milestones/ws-cut-lab-2026-07-23/phases/102-structural-analysis-role-floors`)
  shipped the *bracket+plan*-derived role floors this milestone extends. Do not re-litigate or
  duplicate that phase's scope — this milestone is additive (commander layer on top).
- Corpus available for research: Postgres `harvest_runs`/`deck_queue`/`card_deck_totals` via
  `CategoryKnowledgeRepository`, ~11k queued decks (~3.6k processed with `commander_name`
  populated at last check).

## Out of Scope (this milestone)

- Any change to the bracket+plan fallback behavior for role floors when no commander-specific
  data exists.
- Land/ramp/draw floor logic — already commander-aware via the existing priority chain; not
  touched here.
- Public-facing changes outside Cut Lab.

## Decisions Log

| Decision | Reasoning | Outcome |
|----------|-----------|---------|
| New standalone milestone/branch/worktree, not folded into Cycle 20 | Unrelated surface area (Cut Lab vs. creator-style port); avoids blocking either on the other | Pending |
| Validate against real C# classifiers before any implementation | Prior night's Python reimplementation may have drifted from `DeckStatClassifier`/`PlanRoleClassifier`; a feature built on unverified signal is a wasted-effort risk | Pending |

---
*Created: 2026-07-26*
