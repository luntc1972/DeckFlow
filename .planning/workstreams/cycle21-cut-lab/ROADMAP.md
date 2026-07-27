# Roadmap: Cycle 21 — Discriminating Cut Proposals

**Workstream:** `cycle21-cut-lab` (branch `gsd/cycle21-cut-lab`, isolated worktree at `../deckflow-role-floors`)
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip, without the user reformatting anything.

Five phases. Phase 3 is conditional on Phase 2's findings. Phases 4 and 5 are independent of
everything else and can run in parallel with the 1→2→3 spine.

## Phases

- [ ] **Phase 1: Interaction Taxonomy Split** - Split the merged `interaction` role into `interaction-targeted` and `interaction-mass` from the classifier calls that are already separate, so the taxonomy Phase 2 measures is the taxonomy that ships.
- [ ] **Phase 01.1: Plan-Role Classifier Heuristic Fixes (INSERTED)** - Fix the Counters/counterspell substring collision and the missing protection-card check in `PlanRoleClassifier`'s oracle heuristic (spike 002 findings), before Phase 2 measures floors on top of this same classifier.
- [ ] **Phase 2: Role-Floor Divergence Research** - Repair and actually run the research harness against the real Postgres corpus; delete the synthetic fixture writer; publish an honest go/no-go.
- [ ] **Phase 3: Commander-Aware Floor Defaults (CONDITIONAL on Phase 2 = go)** - Extend `CutLabFloorDefaults` with a commander-specific priority-chain layer for the roles Phase 2 found signal for, showing bracket and commander floors side by side.
- [ ] **Phase 4: Functional-Twins Detector (INDEPENDENT)** - Add the first discriminating structural finding: cards competing for the same slot at the same cost.
- [ ] **Phase 5: Archidekt Bracket Capture (INDEPENDENT, non-gating)** - Capture the bracket already present on the Archidekt deck payload so a future commander × bracket analysis is possible.

## Execution Order

```
Phase 1 ──▶ Phase 2 ──▶ Phase 3 (gated on go/no-go)
Phase 4 ─────────────────────────▶  (parallel, no dependencies)
Phase 5 ─────────────────────────▶  (parallel, no dependencies, backfills over time)
```

Phase 1 **must** precede Phase 2: Phase 2 reports spread per role, and `interaction` is one of the
roles the go/no-go hinges on. Measuring a role that is about to be redefined wastes the run.

## Release Posture — ship phases as they complete

Each phase releases independently. Release order is not phase order: 1, 4, and 5 each ship the
moment they are green; 3 waits only on 2's verdict.

| Phase | User-visible? | Release shape |
|-------|---------------|---------------|
| 1. Interaction split | Yes — role table gains a row, interaction floors change | Own release. CalVer bump + tag. No new flag; rides the existing `tool.cut-lab.enabled`. |
| 2. Research | No — harness is additive and does not ship to the web app | Not a release. Commit findings; no version bump. |
| 3. Commander floors | Yes — role-floor UI gains a commander column | Own release, gated on Phase 2 = go. Consider a dedicated flag so it can deploy dark and flip after UAT. |
| 4. Functional twins | Yes — new structural finding, changes proposal order | Own release. **Recommend a dedicated flag** — this one changes which card is proposed next, the highest-blast-radius behavior change in the cycle. Deploy dark, flip after UAT on a real pool. |
| 5. Bracket capture | No — harvest/schema only | Deploy with the next release; no flag needed. Coverage builds from the deploy date forward. |

Consequence for sequencing: because Phase 4 is independent and separately releasable, it can ship
before the research spine finishes. If Phase 2 returns no-go, Phase 4 is still the cycle's headline.

## Prior Research — read before planning Phase 2

Earlier commander × bracket work already exists in the archive and was not carried into this
workstream. It is directly relevant and partly supersedes the assumed approach.

| Artifact | What it establishes |
|---|---|
| `.planning/archive/2026-cycles/research/2026-07-16-edhrec-bracket-land-data.md` | EDHREC exposes **commander × bracket average decks** at `https://json.edhrec.com/pages/average-decks/<slug>/<bracket>.json` (slugs exhibition/core/upgraded/optimized/cedh = B1–B5). Proven methodology: ≥400-deck floor per cell, swept 50 then 100 commanders across ~337k decks. |
| `.planning/archive/2026-cycles/research/2026-07-16-edhrec-50commander-B1-B4-rows.json` | 148 qualifying cells, `[slug, bracket, lands, deckCount]` rows. |
| `.planning/archive/2026-cycles/research/2026-07-16-edhrec-100commander-classified-rows.json` | Top-100 commanders with oracle abilities classified and joined to counts. |
| `.planning/archive/2026-cycles/research/2026-07-16-commander-manabase-research.md` | Companion write-up. |

**Three things this changes:**

1. **Commander × bracket data is obtainable now.** Phase 5's Archidekt backfill is not the only
   route to bracket-aware floors — EDHREC already serves per-bracket average decks, and each
   average deck is a decklist that can be run straight through `CutLabRoleAssigner` to produce
   role counts per (commander, bracket). This should be evaluated as the Phase 2 corpus before
   committing to the Postgres path.
2. **But it yields point estimates, not distributions.** EDHREC returns one synthesized average
   deck per cell, so there is no within-commander variance — no SD, z, or Cohen's d per commander.
   Between-commander spread per bracket is computable (the land study did exactly that, SD 1.4–1.6);
   within-commander is not. This collides with the stated methodology's preference for a **25th
   percentile** floor, which a single average deck cannot provide.
3. **The land precedent is a warning, not a template.** That study's verdict was
   *"commander identity barely moves land count; bracket is the only driver"* — every
   commander-ability manabase adjustment was rejected. Cycle 21's entire premise is that the
   non-land roles behave differently. Phase 2 must be prepared to reach the same negative verdict
   and say so plainly.

**Open decision for Phase 2 planning:** Postgres Archidekt corpus (real distributions, no bracket,
known coverage gaps, 240 decks / 6 commanders in the attempted run) versus EDHREC average-decks
(bracket built in, ≥400 decks/cell, 50–100 commanders proven reachable, but point estimates only).
A hybrid — EDHREC for the commander × bracket grid, Postgres for within-commander spread on the
commanders that qualify — is likely the right answer. Resolve before writing plans.

## Phase Details

### Phase 1: Interaction Taxonomy Split
**Goal**: Cut Lab tracks targeted removal and mass removal as separate roles with separate floors, matching the 2025 Command Zone template's split of targeted disruption from mass disruption — and matching what the shipped classifiers already compute separately.
**Depends on**: Nothing
**Requirements**: ISPL-01, ISPL-02, ISPL-03
**Why first**: `CutLabRoleAssigner.cs:141-143` already calls `IsBoardWipeCard` and `IsTargetedRemovalCard` as distinct predicates and then merges both into one role key. The merge is the only thing to undo. Beyond template alignment, the merge is a plausible cause of suppressed effect size in Phase 2 — targeted removal and board wipes have different per-commander distributions, and averaging them flattens the spread the research is trying to detect.
**Success Criteria** (what must be TRUE):
  1. `CutLabRoleAssigner.AssignRoles` returns `interaction-targeted` and/or `interaction-mass`; the key `interaction` is no longer emitted.
  2. All five floor-key consumers listed in ISPL-02 enumerate the new keys, and the 8-role assumption is gone from each (role count becomes 9).
  3. A `CutLabState` persisted with the legacy `interaction` floor key deserializes without exception and without silently dropping the user's override.
  4. Bracket default floors exist for both roles and their per-bracket sum is >= today's merged interaction floor (6/8/10/12 by bracket).
  5. Full suite green; no change to any non-interaction role's floor value.

### Phase 01.1: Plan-Role Classifier Heuristic Fixes (INSERTED)

**Goal:** Fix two concrete defects in `PlanRoleClassifier`'s oracle-text heuristic fallback found by spike 002 (branch `spike/role-classification-accuracy`, `.planning/spikes/002-corpus-category-signal/README.md`): the `Counters`/counterspell substring collision in `IsCounterCategory` (a +1/+1-counters synergy tag wrongly earns Interaction in cEDH), and the missing `DeckStatClassifier.IsProtectionCard` call in `FromHeuristic` (protection cards score `None` from the heuristic and depend entirely on a crowd tag). Must land before Phase 2, which counts roles per card via `CutLabRoleAssigner` — the same classifier — for its floor-divergence corpus analysis.
**Requirements**: TBD
**Depends on:** Phase 1
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd-plan-phase 01.1 to break down)

### Phase 2: Role-Floor Divergence Research
**Goal**: We know, from a run that provably touched the corpus, whether any Cut Lab role floor diverges meaningfully by commander — with an artifact that cannot be produced without querying data.
**Depends on**: Phase 1 (taxonomy must be final before measuring)
**Requirements**: RFLR-01, RFLR-02, RFLR-03, RFLR-04, RFLR-09
**Starting state — read before planning**: A substantial harness already exists uncommitted in this worktree: `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs` (985 LOC, real Postgres path at `:83-113`), `DeckFlow.Core/Research/RoleFloorDivergenceStats.cs` (126 LOC), `RoleFloorDivergenceStatsTests.cs` (116 LOC), plus a `boardFilter` parameter added to `CardCategoryRepository.GetCategoryDeckMembershipForCommanderAsync` and passthroughs. **This phase repairs and runs that harness; it does not build one.**
**Known defects in the starting state**:
  1. `WriteSyntheticVerificationOutputs` (`:818`) / `BuildSyntheticCommander` (`:867`) emit hardcoded fixture stats for commanders literally named Alpha/Beta/Gamma/Delta. The writer is currently orphaned (no caller) but its constants match the committed `RESEARCH-FINDINGS.md` exactly — that document is fixture output, not a run.
  2. `RESEARCH-FINDINGS.md` / `.json` are untracked and **must be deleted, not amended**. Their `ClearsBar` column contradicts its own inputs (identical ratio/z/d across commanders yielding different verdicts), so no part of them is salvageable as evidence.
  3. `role-floor-research-run.log` and `.exit` are both 0 bytes — no run was ever recorded.
  4. `boardFilter` is declared after `CancellationToken`, violating the project convention that the token is the last parameter.
**Success Criteria** (what must be TRUE):
  1. The synthetic writer and its helpers are deleted from the harness — grep for `Synthetic` in `DeckFlow.CLI` returns nothing.
  2. The harness emits run provenance into both artifacts: database host, commanders enumerated, raw and deduped deck counts, run timestamp, and the harness commit SHA.
  3. The command exits non-zero, and writes no findings artifact, when zero commanders clear the minimum deck count.
  4. `RESEARCH-FINDINGS.md` reports real commander names with non-identical per-commander statistics, over the post-Phase-1 role taxonomy.
  5. The findings end with an explicit go/no-go naming exactly which roles are in scope for Phase 3.
  6. `boardFilter` moves ahead of `CancellationToken`; no production runtime path changes behavior.
**Plans**: `02-01-PLAN.md` exists from the pre-re-plan pass and is stale — it plans harness construction, not repair. Re-plan before executing.

### Phase 3: Commander-Aware Floor Defaults (CONDITIONAL on Phase 2 = go)
**Goal**: For any role Phase 2 found real signal for, Cut Lab's floor default reflects that commander's own corpus data via a priority chain, while every commander and role without qualifying signal keeps today's bracket+plan floor unchanged.
**Depends on**: Phase 2. If Phase 2 returns no-go for every role, this phase becomes a documented no-op closeout and the cycle ends at Phase 4/5.
**Requirements**: RFLR-05, RFLR-06, RFLR-07, RFLR-08
**Success Criteria** (assuming go):
  1. Floor resolution follows commander data → bracket+plan fallback, mirroring `ResolveLandsDefault`'s existing chain.
  2. Insufficient-data commanders and out-of-scope roles produce byte-identical output to today.
  3. Unit coverage for commander-hit, fallback, and role-not-in-scope paths.
  4. The role-floor UI shows bracket floor **and** commander floor side by side for every role, commander floor shown regardless of bracket, with an explicit empty marker where no commander data exists.
  5. `CutLabCutRoundEngine.LockedOvershootRoleOrder` — currently a hardcoded least-to-most-structural ranking — is reconciled with the commander data, or an explicit decision is recorded for why it stays fixed. A hardcoded order that contradicts commander-aware floors on the same page is a defect.
**Open decision to resolve in planning**: the commander floor should be derived from the 25th percentile, not the mean (a mean-derived floor puts roughly half the commander's own decks below it). Confirm before implementing.

### Phase 4: Functional-Twins Detector (INDEPENDENT)
**Goal**: Cut Lab surfaces cards that compete for the same slot at the same cost, and that finding actually moves those cards up the proposal queue.
**Depends on**: Nothing. Deliberately corpus-free — no dependency on Phase 2's outcome, so it ships whether the research says go or no-go.
**Requirements**: TWIN-01, TWIN-02, TWIN-03, TWIN-04
**Why this matters most**: Every floor-derived finding (`WeakFloorCase`, `RedundantFinishers`) sits in `ExcludedFindingKindsFromTally` — correctly, because a role-count finding attaches uniformly to every member of the role and cannot discriminate within it. The consequence is that Phases 2 and 3 improve guardrails without changing which card gets proposed next. This phase is the only one in the cycle that changes the ranking, and pairwise redundancy is the single most-cited heuristic in community cutting guides, currently absent from Cut Lab entirely.
**Success Criteria** (what must be TRUE):
  1. A new finding kind groups unlocked non-commander cards by (role ∩ MV bucket ∩ primary type), firing at >= 3 members.
  2. The kind is absent from `ExcludedFindingKindsFromTally` and demonstrably contributes to round-1 ranking in a test.
  3. Group evidence is ordered highest mana value first.
  4. Locked cards and the commander never appear; combo-protected members still appear.
  5. Finding density on a real 130-card pool stays reviewable — validate against a live pool before considering the phase done.

### Phase 5: Archidekt Bracket Capture (INDEPENDENT, non-gating)
**Goal**: Harvested decks carry the bracket Archidekt already reports, so a future commander × bracket floor analysis is possible without re-crawling.
**Depends on**: Nothing. Does not gate Phase 2 or Phase 3 — the commander floor stays bracket-agnostic this cycle (user decision, 2026-07-26).
**Requirements**: BRKT-01, BRKT-02, BRKT-03
**Why non-gating**: Bracket cannot be derived retroactively for already-harvested decks, so coverage builds only as new decks are crawled. Gating the research on backfill would stall the cycle on latency outside our control. Landing the capture early maximizes how much bracket coverage exists when a joint analysis is eventually run.
**Reprioritized by the prior-research find**: EDHREC already serves commander × bracket average decks, so this phase is no longer the only path to bracket-aware floors and its urgency drops accordingly. It remains worth doing — it is the only way DeckFlow's *own* corpus ever gains bracket, which matters for any analysis needing real per-deck distributions rather than EDHREC's synthesized averages. Treat it as infrastructure with a long payback, not a Cycle 21 blocker. Ship it whenever it is convenient within the cycle.
**Success Criteria** (what must be TRUE):
  1. Bracket is parsed from the deck payload already fetched — request count per deck is unchanged.
  2. Bracket persists in a nullable column; existing rows and existing queries are unaffected.
  3. "Not captured" (harvested before this change) is distinguishable from "captured, absent" (deck declared no bracket).
  4. Harvest continues to succeed when the field is missing or malformed — no new failure mode on a payload shape change.

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Interaction Taxonomy Split | 0/TBD | Not started | - |
| 2. Role-Floor Divergence Research | 0/TBD | Not started (harness exists, unverified) | - |
| 3. Commander-Aware Floor Defaults | 0/TBD | Not started (gated on Phase 2) | - |
| 4. Functional-Twins Detector | 0/TBD | Not started | - |
| 5. Archidekt Bracket Capture | 0/TBD | Not started | - |

---

## Traceability Check

| Requirement | Phase | Status |
|-------------|-------|--------|
| ISPL-01, ISPL-02, ISPL-03 | Phase 1 | Pending |
| RFLR-01, RFLR-02, RFLR-03, RFLR-04, RFLR-09 | Phase 2 | Pending |
| RFLR-05, RFLR-06, RFLR-07, RFLR-08 | Phase 3 (conditional) | Pending |
| TWIN-01, TWIN-02, TWIN-03, TWIN-04 | Phase 4 | Pending |
| BRKT-01, BRKT-02, BRKT-03 | Phase 5 | Pending |

**Coverage:** 19/19 requirements mapped. No orphans, no duplicates.

---
*Roadmap created: 2026-07-26*
*Re-planned: 2026-07-26 — scope widened from 2 phases to 5; see PROJECT.md Decisions Log*
