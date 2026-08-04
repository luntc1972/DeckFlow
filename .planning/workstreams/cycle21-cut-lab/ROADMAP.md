# Roadmap: Cycle 21 — Discriminating Cut Proposals

**Workstream:** `cycle21-cut-lab` (branch `gsd/cycle21-cut-lab`, isolated worktree at `../deckflow-role-floors`)
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip, without the user reformatting anything.

Ten phases (two inserted: 01.1 and 01.2, both classifier-defect repairs that gate Phase 2's
measurement validity; Phase 6 inserted 2026-08-01; Phase 7 adopted 2026-08-02; Phase 8 adopted
2026-08-02). Phase 3 is conditional on Phase 2's findings. Phases 4, 5 and 6 are independent of
everything else and can run in parallel with the 1→01.1→01.2→2→3 spine. **Phase 7 is gated on
another phase's plan rather than on a whole phase** — it must follow `04-04`, which rewrites the
same two Cut Lab files (`CutLab.cshtml`, `wwwroot/ts/cut-lab.ts`) that Phase 7 reorders. Phase 8's
engine work is independent; its plan-panel UI plan is gated on Phase 7 for the same two files.

## Phases

- [x] **Phase 1: Interaction Taxonomy Split** - Split the merged `interaction` role into `interaction-targeted` and `interaction-mass` from the classifier calls that are already separate, so the taxonomy Phase 2 measures is the taxonomy that ships. *(shipped 2026-07-26, head `9527dc72`)*
- [x] **Phase 01.1: Plan-Role Classifier Heuristic Fixes (INSERTED)** - Fix the Counters/counterspell substring collision and the missing protection-card check in `PlanRoleClassifier`'s oracle heuristic (spike 002 findings), before Phase 2 measures floors on top of this same classifier. *(completed 2026-07-27, head `b8ec09f3`)*
- [ ] **Phase 01.2: Protection-Vocabulary Widening (INSERTED, from Phase 01.1 D-06)** - Widen `DeckStatClassifier.IsProtectionCard`'s oracle vocabulary, which currently under-detects in both directions because its verb agreement is inconsistent across its own four needles. Same rationale as 01.1: Phase 2 counts roles through this predicate.
- [ ] **Phase 2: Role-Floor Divergence Research** - Run the repaired harness over a **hybrid corpus** (EDHREC for the commander × bracket grid, Postgres for within-commander distributions), report a **25th-percentile** floor per role, and publish an honest go/no-go. Scope now includes **lands and ramp** alongside the non-land roles. Delete the synthetic fixture writer.
- [x] **Phase 3: Commander-Aware Floor Defaults (CONDITIONAL on Phase 2 = go)** - Extend `CutLabFloorDefaults` with a commander-specific priority-chain layer for the roles Phase 2 found signal for, showing bracket and commander floors side by side. (completed 2026-07-29)
- [ ] **Phase 4: Functional-Twins Detector (INDEPENDENT)** - Add the first discriminating structural finding: cards competing for the same slot at the same cost.
- [ ] **Phase 5: Archidekt Bracket Capture (INDEPENDENT, non-gating)** - Capture the bracket already present on the Archidekt deck payload so a future commander × bracket analysis is possible.
- [ ] **Phase 6: Scryfall Throughput (INSERTED 2026-08-01, INDEPENDENT)** - Restore the 200ms pacing floor behind an adaptive degrade to 500ms on observed rate limiting, and batch the per-miss fallback into one `cards/search` OR-query so a miss-heavy import costs one request instead of N.
- [ ] **Phase 7: Cut Lab Workflow UX (ADOPTED 2026-08-02, GATED ON PLAN 04-04)** - Make Cut Lab's primary navigation work, reorder the document into workflow order, and bring the decide loop onto the first screen instead of 87% down the page. Measured defects: all four step tabs inert at import time, Export rendered 1,544px above Decide, 10,453px desktop / 15,896px mobile on a 17-row pool. Excludes the cut engine, the metrics, proposal ordering and every API contract. **Must reserve an empty wizard step slot for Phase 8's plan panel** so Phase 8's UI inserts without restructuring the wizard.
- [ ] **Phase 8: Plan Profile — Checkbox Plan Selection (ADOPTED 2026-08-02)** - Replace the deterministically-inert free-text `PrimaryPlan`/`SecondaryPlan` with a machine-readable plan: fixed generic strategy checkboxes + commander-specific EDHREC themes (`$.panels.taglinks`), driving four engine effects — protect on-plan cards, reorder proposals (off-plan first), plan→floor deltas, and a "stranded off-plan package" finding. Design spec: `.planning/specs/2026-08-02-cutlab-plan-profile-design.md` (research-validated 2026-08-02; no deterministic competitor exists). Engine plans independent/parallel; plan-panel UI plan gated on Phase 7.

## Execution Order

```
Phase 1 ──▶ Phase 01.1 ──▶ Phase 01.2 ──▶ Phase 2 ──▶ Phase 3 (gated on go/no-go)
   ✅            ✅
Phase 4 ────────────────────────────────────────────▶  (parallel, no dependencies)
Phase 5 ────────────────────────────────────────────▶  (parallel, no dependencies, backfills over time)
Phase 6 ────────────────────────────────────────────▶  (parallel, no dependencies; wave 1 ──▶ wave 2)

Phase 4 ──▶ plan 04-04 ──▶ Phase 7   (Phase 7 is gated on 04-04, not on all of Phase 4)
Phase 8 (engine plans) ─────────────────────────────▶  (parallel, no dependencies)
Phase 7 ──▶ Phase 8 (plan-panel UI plan only)          (same two files as Phase 7's reorder)
```

Phase 1 **must** precede Phase 2: Phase 2 reports spread per role, and `interaction` is one of the
roles the go/no-go hinges on. Measuring a role that is about to be redefined wastes the run.

Phases 01.1 and 01.2 sit on the same spine for the same reason, one level down: Phase 2 counts roles
per card through `PlanRoleClassifier` / `DeckStatClassifier`, so a defect in those predicates is
measured as if it were a property of the decks. 01.1 is done. **01.2's placement ahead of Phase 2 is
a judgment call, not a hard dependency** — it is sequenced here for consistency with 01.1, but if it
proves large it can be deferred behind Phase 2 provided the go/no-go explicitly records that
protection was under-detected during the run.

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
| 7. Workflow UX | Yes — the whole Cut Lab page reorders and becomes a wizard | Own release. No new flag; rides `tool.cut-lab.enabled`, which is OFF in prod, so it deploys dark like the rest of the tool. Pure presentation — no engine or API change — so the risk is layout regression, not wrong output. UAT at both viewports before the prod flip. |
| 8. Plan profile | Yes — new plan panel, changes protection/ordering/floors/findings | Phased internally (P1 generic+protect/reorder → P2 EDHREC themes → P3 floors+finding); each ships behind `tool.cut-lab.enabled` (tool still dark). Same blast-radius class as Phase 4 — if it lands after the prod flip, give it a dedicated flag. |

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
| `/mnt/c/users/chrislunt/source/personal/deckflow/artifacts/edhrec/data-jul26-uigloqve/edhrec.csv` | `commander,card,count`, 14,150,220 lines including the header, 3,378 commanders, 31,788 distinct cards, 618 MB. No bracket column, no per-deck rows. `LICENSE.txt` permits community use and forbids commercial use. Lives in the MAIN worktree, not `deckflow-role-floors`. |
| `/mnt/c/users/chrislunt/source/personal/deckflow/artifacts/edhrec/averages-jul26-m5o50xfj/averages.csv` | 6,586 lines including the header: 3,372 solo-commander rows plus 3,213 partner-pair rows carrying `avg_land` and `number_decks`. No bracket column, no per-deck rows. Same licence. Lives in the MAIN worktree, not `deckflow-role-floors`. |

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

**RESOLVED 2026-07-27 — hybrid corpus, P25 floor, lands and ramp in scope.** See "Decisions RESOLVED
by the developer" under Phase 2 below for the full reasoning. In short: EDHREC for the commander ×
bracket grid, Postgres for within-commander distributions, 25th-percentile floors, and lands
re-measured deliberately as a calibration control against this document's own land precedent.

The earlier planning pass also failed to inventory the existing EDHREC tooling that already ships in
the repo: `DeckFlow.CLI/EdhrecDataDownloadCommandRunner.cs` (archive download),
`DeckFlow.CLI/EdhrecAveragesCommandRunner.cs` plus
`DeckFlow.Core/Manabase/EdhrecAveragesConverter.cs` (`averages.csv` ->
`ManabaseBaselineSnapshot`), and `DeckFlow.Core/Integration/EdhrecCardLookup.cs`; see also the
proposed shared substrate in `.planning/captures/edhrec-data-feature-plans-2026-07-24.md` and the
archived investigation at
`.planning/archive/2026-cycles/quick/260718-nip-investigate-usefulness-of-edhrec-dump-fo/`. The
consequence is plain: the lands role already has an EDHREC-derived baseline wired into the shipped
product — `EdhrecAveragesConverter` -> `ManabaseBaselineSnapshot`
(`DeckFlow.Web/Data/manabase-baseline/latest.json`) -> `IManabaseBaselineProvider` ->
`CutLabFloorDefaults.ResolveLandsDefault` (`CutLabFloorDefaults.cs:184-201`) — so Phase 2's lands
calibration must compare against it and not only against the 2026-07-16 write-up. Plan `02-07`
owns that comparison.

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
**Requirements**: TBD -- no ID assigned. `REQUIREMENTS.md` maps 19 IDs across Phases 1-5 and was written before this phase was inserted from spike 002. The plans deliberately carry `requirements: []` rather than inventing an ID; a proposed CLSF-01 / CLSF-02 pair is written up in `01.1-01-PLAN.md` under "Requirement Traceability Gap" for the developer to ratify into `REQUIREMENTS.md`.
**Depends on:** Phase 1
**Plans:** 2 plans (2 waves -- both touch `PlanRoleClassifier.cs` and its test file, so they are sequential by file ownership, not by logic)

Plans:
- [x] 01.1-01-PLAN.md -- Bug 1: word-boundary-safe `IsCounterCategory` so the `Counters` (+1/+1) synergy tag stops matching the counterspell arm *(`773cc458` RED, `44e12c90` GREEN, `d30b3141` doc)*
- [x] 01.1-02-PLAN.md -- Bug 2: wire `DeckStatClassifier.IsProtectionCard` into `GrantsInteraction` so protection permanents earn Interaction from oracle text alone (has a blocking checkpoint for the downstream delta) *(`db4d3359` RED, `34e92cc3` GREEN, `b8ec09f3` checkpoint)*

**Outcome (2026-07-27):** Both bugs fixed. Suite green — build 0/0, Core 1630, Web 2095/0/16.
The feared regression did **not** materialize: `ManabaseHealthBandRegressionTests` held at
`("Solid","Solid")` on `.manabase-arch-7084567-facts.json` (18/18), so counting protection permanents
as interaction did not move a deck's health band, and no golden or expectation value was edited.
One user-visible change ships with this phase and must not be filed as a bug at UAT: a 3-card
`Counters` subtheme is now **eligible** for a `StrandedSubtheme` finding, where the substring
collision previously caused `ComputeStrandedSubthemes` to skip it.
Checkpoint dispositions: delta **accepted** with one authorized test-oracle correction (the plan's own
synthetic enchantment oracle used plural-subject phrasing that fell inside its own D-06 gap);
D-06 → **follow-up phase requested**, now Phase 01.2 below.

### Phase 01.2: Protection-Vocabulary Widening (INSERTED, from Phase 01.1 D-06)

**Goal:** `DeckStatClassifier.IsProtectionCard` detects protection consistently regardless of a
card's grammatical phrasing, so a commander's measured interaction floor stops being a function of
how its cards happen to word an effect.
**Depends on:** Phase 01.1
**Requirements**: TBD -- same traceability gap as 01.1; ratify alongside CLSF-01 / CLSF-02.

**The defect, precisely.** `IsProtectionCard` (`DeckFlow.Core/Analysis/DeckStatClassifier.cs:226-231`)
is a curated-name check OR-ed with four oracle needles, and **its verb agreement is inconsistent
across those four needles**:

| Needle | Subject form it assumes | Misses |
|---|---|---|
| `gains hexproof` | singular | "creatures you control **gain** hexproof" |
| `gains indestructible` | singular | "permanents you control **gain** indestructible" |
| `gain protection from` | **plural** | Mother of Runes — "target creature **gains** protection from…" |
| `phases out` | singular | Teferi's Protection — "permanents you control **phase out**" |

So it under-detects in *both* directions depending on phrasing. Vocabulary is also incomplete —
shroud and regeneration are absent entirely.

Cards confirmed still scoring `PlanRole.None` after Phase 01.1 (measured, `01.1-02-DELTA.md` §d):
Swiftfoot Boots, Lightning Greaves, Hexing Squelcher, Goblin Chirurgeon, Mother of Runes.

**Why it is not a one-line fix.** `IsProtectionCard` is shared: widening it simultaneously moves
`InteractionAuditAggregator.cs:58`, Cut Lab's own `protection` role
(`CutLabRoleAssigner.cs:165`), and `PlanRoleClassifier.cs:236`. That is a materially larger blast
radius than 01.1 had, and it deserves a corpus-backed vocabulary decision rather than needles added
one at a time.

**Success Criteria** (what must be TRUE):
  1. The vocabulary is derived from the corpus — the phrasings chosen are the ones that actually
     occur, with counts, not a guessed list.
  2. Verb agreement is handled uniformly; no needle assumes a subject number the others do not.
  3. The blast radius on `InteractionAuditAggregator` and Cut Lab's `protection` role is measured
     against real fixtures and explicitly accepted, exactly as 01.1 did — no golden regenerated to
     make a test pass.
  4. False positives are bounded: a card that merely mentions hexproof/indestructible without
     granting it does not become protection.

### Phase 2: Role-Floor Divergence Research
**Goal**: We know, from a run that provably touched the corpus, whether any Cut Lab role floor diverges meaningfully by commander — with an artifact that cannot be produced without querying data.
**Depends on**: Phase 1 (taxonomy must be final before measuring), Phase 01.1, Phase 01.2

#### Decisions RESOLVED by the developer, 2026-07-27

The two open decisions that blocked plan-writing are now settled. Do not reopen them in planning.

**D-A — Corpus: HYBRID.** EDHREC average-decks supply the commander × bracket grid
(`https://json.edhrec.com/pages/average-decks/<slug>/<bracket>.json`, slugs
exhibition/core/upgraded/optimized/cedh = B1–B5, ≥400 decks/cell); the Postgres Archidekt corpus
supplies within-commander distributions for commanders that clear a minimum deck count. Neither
source alone is sufficient — EDHREC has bracket but no variance, Postgres has variance but no
bracket.

**D-B — Floor statistic: 25th percentile.** A mean-derived floor puts roughly half of a commander's
own decks below their own floor, which is the wrong shape for a threshold most decks should clear.
P25 requires a real distribution, which is precisely why D-A cannot be EDHREC-only: **the two
decisions are coupled.** Where only an EDHREC point estimate exists for a cell, report it as a point
estimate and say so — do not present it as a percentile.

**D-C — Scope now includes LANDS and RAMP**, alongside the non-land roles.

Lands carry a documented prior that plan-writing must confront head-on rather than ignore: the
2026-07-16 EDHREC study (see Prior Research below) swept ~337k decks and concluded *commander
identity barely moves land count; bracket is the only driver*, and every commander-ability manabase
adjustment was rejected on that evidence. Re-running lands is **deliberate and authorized** on this
reasoning:

1. The prior study used EDHREC average-decks only, so it structurally **could not** compute a
   within-commander percentile. P25 over real per-deck distributions measures something the earlier
   work was not able to measure.
2. Lands therefore double as a **calibration control**. If the new methodology reproduces the known
   no-go on lands, that is evidence the harness is trustworthy on the roles where the answer is not
   already known. If it *contradicts* the prior, that is a finding about the methodology and must be
   reported as such before it is treated as a finding about decks.
3. Ramp was never measured this way at all — the rejected work was commander-**ability**-driven land
   adjustment, which is a different question from whether ramp counts vary by commander.

Reporting requirement: the findings must state the lands verdict against the prior explicitly —
"reproduces", "contradicts", or "insufficient data" — and never quietly present a lands result as
novel.
**Requirements**: RFLR-01, RFLR-02, RFLR-03, RFLR-04, RFLR-09
**Starting state — read before planning**: A substantial harness already exists: `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs` (985 LOC, real Postgres path at `:83-113`), `DeckFlow.Core/Research/RoleFloorDivergenceStats.cs` (126 LOC), `RoleFloorDivergenceStatsTests.cs` (116 LOC), plus a `boardFilter` parameter added to `CardCategoryRepository.GetCategoryDeckMembershipForCommanderAsync` and passthroughs. **This phase repairs and runs that harness; it does not build one.**

> **Location correction (superseded 2026-07-27, re-verified):** the earlier `stash@{0}` claim was
> WRONG. `git stash list` in this worktree shows one unrelated entry belonging to
> `feat/manabase-source-list`, not the Phase 2 harness. The harness was on disk as untracked and
> modified working-tree files against `b741b56a` and was then committed unrepaired as the baseline at
> `27e25459` on 2026-07-27 so the phase's repair work diffs against a known starting point:
>
> | Now at `27e25459` | Path |
> |---|---|
> | added | `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs` (985 LOC) |
> | added | `DeckFlow.Core/Research/RoleFloorDivergenceStats.cs` (126 LOC) |
> | added | `DeckFlow.Core.Tests/RoleFloorDivergenceStatsTests.cs` (116 LOC) |
> | modified | `DeckFlow.CLI/Program.cs`, `DeckFlow.CLI/DeckFlow.CLI.csproj` |
> | modified | `DeckFlow.Core/Knowledge/{CardCategoryRepository,CategoryKnowledgeRepository,DeckQueueRepository}.cs` |
> | modified | `DeckFlow.Core.Tests/CategoryCacheSchemaParityTests.cs` |
> | still untracked | `_role-floor-research/cards_full.json` (8.2 MB resumable Scryfall cache — must survive) |
>
> No work in this worktree may run `git stash`, because stash is repo-global across this repo's four
> worktrees. The harness also predates D-A/D-B/D-C: it has a Postgres path only and no EDHREC
> ingestion at all, while `RoleFloorDivergenceStats.ComputePercentile` already exists and is already
> called with `0.25` at `RoleFloorResearchCommandRunner.cs:236-238` and `:560`; the actual defect is
> that `ClearsBar` (`RoleFloorDivergenceStats.cs:47-69`) never reads P25, so P25 is a cosmetic
> column that does not drive the verdict.
**Known defects in the starting state**:
  1. `WriteSyntheticVerificationOutputs` (`:818`) / `BuildSyntheticCommander` (`:867`) emit hardcoded fixture stats for commanders literally named Alpha/Beta/Gamma/Delta. The writer is currently orphaned (no caller) but its constants match the committed `RESEARCH-FINDINGS.md` exactly — that document is fixture output, not a run.
  2. `RESEARCH-FINDINGS.md` / `.json` are untracked and **must be deleted, not amended**. Their `ClearsBar` column contradicts its own inputs (identical ratio/z/d across commanders yielding different verdicts), so no part of them is salvageable as evidence.
  3. `role-floor-research-run.log` and `.exit` are both 0 bytes — no run was ever recorded.
  4. `boardFilter` is declared after `CancellationToken`, violating the project convention that the token is the last parameter.
  5. The role taxonomy in `RoleFloorResearchCommandRunner.TargetRoles` (`:36-43`) is the pre-Phase-1
     five-role set (`interaction`, `protection`, `engines`, `payoffs`, `wincons`) with no `lands`
     and no `ramp`, while the shipped `CutLabRoleAssigner.RoleKeys` (`:29-40`) is nine keys plus an
     `other` fallback assigned when nothing else matches. This is a larger repair than the phase text
     first implied and is what success criteria 4 and 9 actually require.
  6. The `--out` and `--out-json` defaults in `DeckFlow.CLI/Program.cs` (`:83-84`) point at the old
     workstream folder
     `.planning/workstreams/cutlab-role-floors/phases/01-role-floor-divergence-research/`.
**Success Criteria** (what must be TRUE):
  1. The synthetic writer and its helpers are deleted from the harness — a case-insensitive grep for `synthetic` across `DeckFlow.CLI` `.cs` sources (`grep -rni "synthetic" DeckFlow.CLI --include=*.cs`) returns nothing; generated build output under `bin/`/`obj/` is out of scope.
  2. The harness emits run provenance into both artifacts: database host, commanders enumerated, raw and deduped deck counts, run timestamp, and the harness commit SHA.
  3. The command exits non-zero, and writes no findings artifact, when zero commanders clear the minimum deck count.
  4. `RESEARCH-FINDINGS.md` reports real commander names with non-identical per-commander statistics, over the post-Phase-1 role taxonomy.
  5. The findings end with an explicit go/no-go naming exactly which roles are in scope for Phase 3.
  6. `boardFilter` moves ahead of `CancellationToken`; no production runtime path changes behavior.
  7. Floors are reported as **25th percentiles** over real per-deck distributions (D-B). Any cell backed only by an EDHREC point estimate is labelled as such and is never presented as a percentile.
  8. Both corpora are used and distinguishable in the output (D-A): every reported figure states which source it came from, and coverage is reported per source — commanders reached, cells qualifying, decks deduped.
  9. **Lands and ramp are measured** (D-C), and the lands verdict is stated explicitly against the 2026-07-16 prior as "reproduces" / "contradicts" / "insufficient data". A lands result is never presented as novel without that comparison.
  10. The go/no-go is willing to be negative. If no role clears the bar, the findings say so plainly and Phase 3 becomes a documented no-op — a null result is a valid deliverable for this phase, not a failure of it.
Phase 01.2 is deferred behind Phase 2 by explicit 2026-07-27 developer decision, using the
ROADMAP's own escape hatch at `:34-37`; the price of that deferral is an explicit
protection-under-detection disclosure in the Phase 2 go/no-go artifact, delivered by plan `02-07`.

The commander x bracket corpus D-A calls for was fetched on 2026-07-27 — 305 commanders x 5 brackets
= 1,525 cells, 0 failed — and at the >=400-decks-per-cell floor it yields 805 qualifying cells
(versus the 2026-07-16 study's 148), with B1/exhibition unusable at exactly one qualifying cell and
B5/cedh thin at 40. `ManabaseAnalysisService.cs:603-605` and
`DeckFlow.Web/Data/manabase-baseline/latest.json` independently reached the same conclusion about B1.
Plans `02-02` and `02-06` carry the detail.

**Plans:** 9 plans (7 waves)
- [ ] `02-01-PLAN.md` — correct the Phase 2 starting-state record, delete fixture artifacts, and fix the `boardFilter` parameter order.
- [ ] `02-02-PLAN.md` — commit the existing EDHREC bracket acquisition tooling and record the completed corpus fetch.
- [ ] `02-03-PLAN.md` — make the floor bar verdict P25-driven with deck-count and zero-baseline guards.
- [ ] `02-04-PLAN.md` — delete the synthetic writer, widen the harness taxonomy, and fix the wrong output defaults.
- [ ] `02-05-PLAN.md` — separate percentile figures from point estimates so source semantics are type-enforced.
- [ ] `02-06-PLAN.md` — ingest the fetched EDHREC bracket corpus into the harness as role point estimates.
- [ ] `02-07-PLAN.md` — emit lands calibration and explicit go/no-go reasoning over the hybrid corpus.
- [ ] `02-08-PLAN.md` — run the live research harness and write the real findings artifacts.
- [ ] `02-09-PLAN.md` — add the `edhrec.csv` expected-role-count grid arm for additional EDHREC coverage.

Wave 1 is ordered `02-01` → `02-03` because both run `dotnet build` plus both test suites and then
commit, and concurrent runs contend on `obj`/`bin`, on `.git/index.lock`, and on the pinned test
counts; `02-02` is Python-and-docs only with disjoint files and is the only plan safe to run
concurrently with either. Waves 2-7 hold one plan each, ending with `02-08`'s live run.

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
**Open decision to resolve in planning**: RESOLVED 2026-07-28 — the commander floor is the 25th percentile,
not the mean (`03-CONTEXT.md` D-01). Fractional values truncate down (D-02) and a p25 of 0 falls back to the
bracket value (D-03).
**Amendment carried into REQUIREMENTS.md:** RFLR-05's priority chain becomes `max(bracket, commander)` —
commander data may only raise a floor, never lower one (D-04). Measured driver: at brackets 4-5 all 124 of
124 adopting payoffs commanders sit below the bracket band, so a literal chain would delete that guardrail.

**Plans:** 7/7 plans complete
- [ ] `03-01-PLAN.md` — Core snapshot contract, adoption filter, and fail-closed drift check.
- [ ] `03-02-PLAN.md` — `role-floor-baseline` CLI generator, drift thresholds, and the committed 678-commander snapshot.
- [ ] `03-03-PLAN.md` — shared commander-key helper and the fail-open runtime role-floor provider.
- [ ] `03-04-PLAN.md` — `max(bracket, commander)` floor resolution, the split `CutLabResolvedFloor`, and the RFLR-05 amendment.
- [ ] `03-05-PLAN.md` — the six-column role-floors table with two distinct empty-cell states.
- [ ] `03-06-PLAN.md` — the overlap-corrected aggregate-infeasibility advisory (D-06a).
- [ ] `03-07-PLAN.md` — `LockedOvershootRoleOrder` reconciled to headroom ranking (success criterion 5).

Waves 1 holds `03-01` and `03-07` in parallel — they share no files, and `03-07` threads data that already
exists at its call site, so it depends on nothing else in the phase. Waves 2-6 hold one plan each along the
data spine: generate the snapshot, load it, resolve floors from it, show both numbers, then warn when the
raised floors cannot fit. `CutLabFloorDefaults.cs`, `CutLabViewModel.cs` and `CutLab.cshtml` are each touched
by more than one plan, which is why that spine is sequential rather than raced.

**Execution precondition:** the branch is rebased onto `main` (`1511dd95`) before execution, so
`CedhBaselineDriftCheck` and the cEDH fail-closed CLI gates are present as the pattern plans 03-01 and 03-02
mirror. Branch mutation is the developer's, not a planned task.

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
**Why non-gating**: Bracket cannot be derived retroactively for already-harvested decks, so coverage builds only as new decks are crawled. Gating the research on backfill would stall the cycle on latency outside our control. Landing the capture early maximizes how much bracket coverage exists when a joint analysis is eventually run. The per-cell arithmetic also makes the limit permanent rather than temporary: `edhBracket` is already present on the fetched Archidekt payload at roughly 25% fill, so capture is free, but the deepest commander in the corpus has 917 decks, and 917 x 25% / 5 brackets is roughly 46 decks per cell against EDHREC's 400-decks-per-cell floor. Archidekt bracket capture therefore cannot fill a bracket cell for any commander, now or after a full backfill. This strengthens D-A rather than weakening it.
**Reprioritized by the prior-research find**: EDHREC already serves commander × bracket average decks, so this phase is no longer the only path to bracket-aware floors and its urgency drops accordingly. It remains worth doing — it is the only way DeckFlow's *own* corpus ever gains bracket, which matters for any analysis needing real per-deck distributions rather than EDHREC's synthesized averages. Treat it as infrastructure with a long payback, not a Cycle 21 blocker. Ship it whenever it is convenient within the cycle.
**Success Criteria** (what must be TRUE):
  1. Bracket is parsed from the deck payload already fetched — request count per deck is unchanged.
  2. Bracket persists in a nullable column; existing rows and existing queries are unaffected.
  3. "Not captured" (harvested before this change) is distinguishable from "captured, absent" (deck declared no bracket).
  4. Harvest continues to succeed when the field is missing or malformed — no new failure mode on a payload shape change.

### Phase 6: Scryfall Throughput (INSERTED 2026-08-01, INDEPENDENT)
**Goal**: Get the pacing floor back to 200ms without re-earning the Cloudflare block, and stop paying one throttled request per unresolved card.
**Depends on**: Nothing. Does not gate any other phase.
**Requirements**: SCRY-01, SCRY-02, SCRY-03, SCRY-04 (ratify into `REQUIREMENTS.md` before closeout)
**Origin**: User decision, 2026-08-01, after Phase 111.1 raised `ScryfallThrottle.MinInterval` from 200ms to 500ms to stop a Cloudflare IP block. That fix was correct but blunt, and it applies process-wide to **every** Scryfall consumer (Comparison, Meta-Gap, Analysis, Manabase, Deck History) — not just Cut Lab, which is flag-gated.

**Why this is worth a phase, with the measurement**: `111.1-PACING-MEASUREMENT.md` shows the cost is
not uniform. A normal 0-miss flow makes 2 throttled calls, so it pays **one** extra gap — 200-300ms,
barely perceptible. The damage is concentrated in miss-heavy flows, where the doc models 39 calls
against `39 x 500ms ≈ 19.5s` of serialized throttle time. Aggregate process-wide ceiling also falls
from roughly 5 req/s to 2 req/s across all concurrent users. So the two waves below attack the same
problem from both ends: wave 1 restores the floor for the common case, wave 2 removes the calls that
make the bad case bad.

> ⚠ **WAVE 1 SUPERSEDED 2026-08-01 — do not implement the paragraph below as written.** Research
> during planning found the 200ms figure was itself the defect, not 111.1's over-correction:
> `ScryfallThrottle.cs:14-21` records that Scryfall documents a hard 2 req/s **per-endpoint** limit
> for the four endpoints this throttle covers and that 200ms came from misreading the docs' "all
> other methods" row; `ScryfallThrottleTests.cs:174` pins `>= 450ms` specifically to assert that
> limit; and `111.1-PACING-MEASUREMENT.md` calls the change "a documentation correction, not a defect
> to fix". Restoring 200ms would run above the documented rate and back off only after being caught.
>
> **Replacement, by operator decision:** keep the 500ms floor and make the gate **per-endpoint**
> instead of one process-wide gate, since Scryfall's limit is per-endpoint and a single shared gate is
> stricter than required. That recovers the aggregate throughput this wave was reaching for — the
> measurement's own correction puts the real figures at 2.2 -> 1.33 req/s, not 5 -> 2 — without
> violating anything. The SC-7 test survives unmodified. The adaptive degrade is **deferred, not
> rejected**. Full reasoning and the replacement success criteria live in
> `phases/06-scryfall-throughput/06-CONTEXT.md`, which is canonical for this phase.
>
> The original text is kept below unedited, because the observed-not-thrown rule and the concurrency
> warning in it remain correct and still apply if the degrade is revived.

**Wave 1 — adaptive pacing.** Default `MinInterval` back to 200ms. On an **observed** Scryfall 429,
degrade to 500ms for **5 minutes** since the most recent 429, then revert automatically.
  - ⚠ **The trigger must fire where the 429 is OBSERVED, not where it is thrown.** Phase 111.1's B-1
    design deliberately **swallows** 429s in the Cut Lab fail-open path — they never reach
    `ScryfallThrottle.ThrowIfUpstreamUnavailable`. A degrade hook on the throw path alone would miss
    the exact scenario this exists for. Record at status inspection, before the fail-open branch.
  - ⚠ `ScryfallThrottle` is a process-wide `static`. A mutable `MinInterval` is written from
    concurrent requests — use `Interlocked`/`volatile`, not a plain field.

> ⚠ **WAVE 2 SCOPED 2026-08-01.** "The loop" is two loops. `ResolveAsync` takes the fallback as a
> per-caller delegate, and only one of the two strategies is expressible as an OR query:
> `SearchFallbackCardAsync` (Comparison, Meta-Gap, Cut Lab, Manabase) is one exact-name request and
> **is** the target; `SearchPrintingFallbackCardAsync` (Analysis, Deck History) is a three-stage
> progressive escalation under `unique=prints` with per-name best-match selection, so a batched flat
> list cannot be attributed back to its term. Batching it is a redesign of its matching semantics and
> is **out of scope** — meaning Analysis and Deck History see no request-count change from this wave
> and must not be described as covered. See `06-CONTEXT.md`.

**Wave 2 — batch the fallback.** `ScryfallReferenceResolver` currently does chunk(75) →
`POST cards/collection` → match-back → **one `GET cards/search?q=!"Name"` per miss**. Collapse that
loop into a single OR query: `q=!"A" or !"B" or !"C"`.
  - URL length bounds the batch: `cards/search` is a GET, ~30 chars per term, so chunk at roughly 60
    names — smaller than collection's 75.
  - ⚠ Match-back is the risk. Today it is 1 name → 1 result; a batch returns a flat list. This is the
    same seam that produced BOTH combo-seam MEDs on 2026-08-01 (DFC front-face, curly apostrophe).
    Normalize both sides through `CutLabCardNames.Normalize` from the start, and pin those two
    vectors in tests.
  - One malformed term can 400 the whole chunk. Degrade to the existing per-card path on 400 so nine
    good resolutions are not lost to one bad name.
  - 404 changes meaning: a search 404s only when EVERY term misses. "Which missed" becomes set
    subtraction, which the existing `resolvedRequestNames` step already computes.

**Success Criteria** (what must be TRUE):
  1. Steady state paces at 200ms; an observed 429 moves it to 500ms; it returns to 200ms 5 minutes
     after the last 429, with no manual intervention.
  2. A swallowed (fail-open) 429 triggers the degrade — proven by a test, not by inspection.
  3. N unresolved cards in one chunk cost ONE `cards/search` request, not N.
  4. A 400 on the batch query falls back to per-card resolution and loses no card that the per-card
     path would have resolved.
  5. DFC (`Front // Back`) and curly-apostrophe names match back correctly in the batched path.
  6. No regression for the other five Scryfall consumers — this is shared infrastructure, and Cut
     Lab's flag does **not** gate it.

### Phase 7: Cut Lab Workflow UX (ADOPTED 2026-08-02, GATED ON PLAN 04-04)
**Goal**: Cut Lab's primary navigation works, the document reads in workflow order, and the decide loop is on the first screen instead of 87% down the page.
**Depends on**: **Plan `04-04`, not all of Phase 4.** `04-04` rewrites `CutLab.cshtml` and `wwwroot/ts/cut-lab.ts` for the presenter merge and the D-24 combo-badge repair — the same two files 07-02, 07-03 and 07-04 reorder and re-wire. Running them concurrently is how this milestone earns a third rebase with real conflicts.
**Gates**: Phase 8's plan-panel plans (`08-07`, `08-08`) — they fill the wizard step slot this phase reserves.
**Requirements**: CLUX-01 .. CLUX-08 (declared in the `07-0N-PLAN.md` frontmatter; ratify into `REQUIREMENTS.md` before closeout — see the Traceability Check)
**Origin**: Adopted 2026-08-02 from the unregistered root phase 116, after a 14-issue UX audit.
**Canonical**: `phases/07-cutlab-workflow-ux/07-CONTEXT.md` (measured live 2026-08-02 against `scripts/run-web-test.sh`) and that phase's `README.md`.

**Not in scope (D-3):** the cut engine, the metrics, proposal ordering, any API contract. No file under
`Services/CutLab/` is edited except round-label string constants in `07-06`.

**Measured defects** — all four step tabs are inert at import time; Export renders 1,544px **above**
Decide; the page is 10,453px desktop / 15,896px mobile on a **17-row** pool.

**D-1 Step model — RESOLVED 2026-08-03 as Option 3 (wizard + pinned proposal).** Mockups rendered
against the real site CSS live in `.planning/ui-design/cut-lab/proposed/`; the six PNGs are the
self-contained artifact, because the HTML pulls site CSS from `http://localhost:5173` and renders
unstyled without the dev server.

| Option | Desktop | Mobile | vs today |
|---|---|---|---|
| Today | 10,453px | 15,896px | — |
| 1 true wizard | 1,022px | 1,440px | −90% |
| 2 soft fix | 1,596px | 1,929px | −85% |
| **3 wizard + pinned proposal — SELECTED** | **1,107px** | **1,588px** | **−89%** |

Consequences of Option 3: `07-03` keeps runtime panel-hiding and G-2's exactly-one-visible assertion,
and `07-05` exists and executes. The wizard has **five** slots, with `cut-lab-step-panel-3` at index 3
**reserved empty for Phase 8's plan panel** so Phase 8's UI inserts without restructuring the wizard.

**D-4 Branch — RESOLVED 2026-08-02:** this phase runs on `gsd/cycle21-cut-lab` like every other phase
in the milestone. The original recommendation of a separate `feat/cutlab-workflow-ux` branch off
`main` assumed `main` did not contain Cycle 21 — which stopped being true once the branch was rebased
and `main` fast-forwarded to it.

**Plans:** 6 plans (6 waves, strictly sequential — each rewrites what the last produced)
- [ ] `07-01-PLAN.md` — wave 1 — regression gate spec; **must FAIL on HEAD**.
- [ ] `07-02-PLAN.md` — wave 2 — DOM reorder to Process → Decide → Plan → Goals → Export, with the required selector migration. **Independently shippable**: it fixes the no-JS reading order without touching a line of TypeScript.
- [ ] `07-03-PLAN.md` — wave 3 — step-tab handler, panel visibility, ARIA keyboard support.
- [ ] `07-04-PLAN.md` — wave 4 — intake summary, unified progress strip, collapse defaults.
- [ ] `07-05-PLAN.md` — wave 5 — pinned proposal (exists because Option 3 won).
- [ ] `07-06-PLAN.md` — wave 6 — copy, mobile tab labels, help + README.

**Success Criteria** (what must be TRUE):
  1. Every step tab is operable from import time — none inert.
  2. The document reads Process → Decide → Plan → Goals → Export, and the no-JS order matches the JS order.
  3. The decide loop is reachable on the first screen; Export no longer renders above Decide.
  4. Desktop height on the 17-row measurement pool lands near the Option 3 mockup (~1,107px), not 10,453px.
  5. Exactly one wizard panel is visible at a time, asserted by test, with slot index 3 present and empty.
  6. No file under `Services/CutLab/` changes except round-label string constants.

**Follow-ups deliberately excluded:** auto-recompute Goals/Compare on accept (engine + perf change,
not copy); merging the three pool browse surfaces plus the JS-only lock-table filter into one faceted
explorer; removing the anchor-nav / tablist duplication; deferring the four Export "⚠ pending" rows
until Build export runs.

### Phase 8: Plan Profile — Checkbox Plan Selection (ADOPTED 2026-08-02)
**Goal**: The user's deck plan is machine-readable — fixed generic strategy checkboxes plus commander-specific EDHREC themes — and the deterministic engine acts on it through all four effects: protect on-plan cards, reorder proposals (off-plan first), plan→floor deltas, and a "stranded off-plan package" finding.
**Depends on**: Nothing for the engine plans (parallel-safe). The plan-panel UI plan is gated on **Phase 7** — it fills the wizard step slot Phase 7 reserves and edits the same two files (`CutLab.cshtml`, `wwwroot/ts/cut-lab.ts`).
**Requirements**: PLPR-01..PLPR-06 (ratify into `REQUIREMENTS.md` before closeout)
**Design authority**: `.planning/specs/2026-08-02-cutlab-plan-profile-design.md` (approved, research-validated 2026-08-02) and `phases/08-plan-profile-checkbox-selection/08-CONTEXT.md`, which is canonical for this phase.
**Internal phasing** (from the spec): P1 `CutLabPlanProfile` + generic strategies + role-proxy resolver + protect/reorder (zero new HTTP) → P2 `EdhrecCommanderThemeService` + commander theme UI + pre-check → P3 floor deltas + off-plan finding detector. All behind the existing Cut Lab flag (prod OFF).
**Success Criteria** (what must be TRUE):
  1. `PrimaryPlan`/`SecondaryPlan` free text no longer appears in the intake form; in-flight sessions with the old fields still deserialize.
  2. With zero checkboxes checked, engine output is byte-identical to today — every effect is a no-op, and the panel says so.
  3. Checking a generic strategy protects its role-proxy cards (pushed to back of proposal queue, still cuttable), and off-plan cards surface first in Rounds 1/2/3.
  4. Checking an EDHREC theme resolves membership from that theme's card lists, DFC-aware; a 403/unreachable EDHREC degrades to "commander themes unavailable" while generic strategies keep working.
  5. Overlapping selections compose as union (protection), max (floor deltas per role), additive-with-cap (ordering weights) — each proven by a test that mutates the constant.
  6. The "stranded off-plan package" finding fires at the threshold boundary and phrases its message against the user's selection.

**Plans:** 8 plans (6 waves)
- [ ] `08-01-PLAN.md` — `CutLabPlanProfile` on the serialized intent plus the twelve-strategy catalog and role-proxy table in Core.
- [ ] `08-02-PLAN.md` — `CutLabPlanAffinityResolver`: union membership, capped additive ordering score, DFC-aware matching.
- [ ] `08-03-PLAN.md` — `EdhrecCommanderThemeService`: RestSharp + Polly v8 fetch, 403 fail-open, etag disk cache, theme preselector.
- [ ] `08-04-PLAN.md` — plan→floor-delta table with max-per-role composition, clamped, reported as a separate `PlanDelta`.
- [ ] `08-05-PLAN.md` — off-plan-first proposal ordering in rounds 1-3 and the stranded-off-plan-package detector.
- [ ] `08-06-PLAN.md` — `CutLabPlanAffinityFactory` wiring the page, AJAX patch and decide-API paths to the resolver.
- [ ] `08-07-PLAN.md` — the plan panel: request contract, Razor markup, TypeScript state, `site-common.css` layout, six e2e specs migrated off the removed field. **GATED ON PHASE 7.**
- [ ] `08-08-PLAN.md` — two-viewport Playwright pass over the panel plus the blocking human-verify checkpoint. `autonomous: false`. **GATED ON PHASE 7** via `08-07`.

Wave 1 is `08-01` alone — every other plan reads its two contracts. Wave 2 races `08-02`, `08-03` and `08-04`,
which share no files. Waves 3 and 4 are sequential on `CutLabCutRoundEngine.cs` then `CutLabPageService.cs`.
Waves 5 and 6 are the UI, gated on Phase 7's reserved wizard slot.

⚠ **Source correction found during planning:** the design spec, `08-CONTEXT.md`, `08-RESEARCH.md` and
`08-PATTERNS.md` all say the reorder effect lands in `CutLabNextProposalBuilder`. That file is 40 lines and
contains no ranking — it builds a DTO from an already-selected proposal. Proposal ordering lives in
`CutLabCutRoundEngine.BuildQueue` (`CutLabCutRoundEngine.cs:266-332`), where `ComboProtectionRank` is the
leading sort key. Plan `08-05` carries the correction.

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Interaction Taxonomy Split | done | **Complete** — pushed, `9527dc72` | 2026-07-26 |
| 01.1. Plan-Role Classifier Heuristic Fixes | 2/2 | **Complete** — suite green, `b8ec09f3` | 2026-07-27 |
| 01.2. Protection-Vocabulary Widening | 0/TBD | Not started (inserted from 01.1 D-06) | - |
| 2. Role-Floor Divergence Research | 11/11 | **Complete** — real run exit 0, 841 qualifying commanders. GO on ramp, draw, interaction-targeted, engines, payoffs, wincons; **lands PULLED** at the Task 4 checkpoint (the Postgres arm measures distinct land NAMES, not land count; colour count explains 54% of its variance). See `02-08-SUMMARY.md` | 2026-07-28 |
| 3. Commander-Aware Floor Defaults | 7/7 | **Complete** — all 7 plans verified; `max(bracket, commander)` shipped with both numbers on screen. Snapshot: 678 commanders / 1463 adopted floors, independently recomputed from `RESEARCH-FINDINGS.json`. Task 4 human-verify approved. One WARNING-severity gap on the AJAX patch path (see `03-VERIFICATION.md`) | 2026-07-29 |
| 4. Functional-Twins Detector | 3.5/4 | **Executing** — plans CONVERGED at round 12; `04-01` (`518d7d83`), `04-02` (`dbd46e94`) and `04-03` (`b508f27e`) committed and on `main`. `04-04` Tasks 1-2 are complete and green; **Task 3 — the blocking human UI checkpoint — is NOT STARTED** (`autonomous: false`). Both Codex gates are DISCHARGED: `b508f27e` reviewed 2026-08-03 (no BLOCK/HIGH), and the `04-04` review's 2 HIGH were folded at `1fc48dd6` — see `04-REVIEWS.md` | - |
| 5. Archidekt Bracket Capture | 0/3 | Planned, not started — **Codex review DISCHARGED 2026-08-03 and folded at `af43a4e4`**. The round-10 finding was that `05-03`'s RED-phase gate certified nothing: exact-TRX-name matching cannot see a `[Theory]`'s per-case name suffixes, so both pinned tests are now `[Fact]` | - |
| 6. Scryfall Throughput | 0/TBD | Not started (inserted 2026-08-01; 2 waves — adaptive pacing, then fallback batching) | - |
| 7. Cut Lab Workflow UX | 0/6 | Planned, not started (adopted 2026-08-02 from the unregistered root phase 116). **Gated on plan `04-04`**, which rewrites the same two files. **D-1 RESOLVED 2026-08-03 as Option 3** (wizard + pinned proposal), so `07-05` exists and executes. Codex review folded at `af43a4e4` | - |
| 8. Plan Profile — Checkbox Plan Selection | 0/8 | Planned 2026-08-02, not started. 6 waves. Engine plans `08-01`..`08-06` are independent of Phase 7; `08-07` and `08-08` are **gated on Phase 7**'s reserved wizard slot. `08-08` is `autonomous: false` (human UI checkpoint at 2 viewports). **Codex plan review DISCHARGED 2026-08-03** — 2 BLOCK + 17 HIGH folded at `af43a4e4`, round 2 converged (0 BLOCK / 0 HIGH); execute-ready | - |

---

## Traceability Check

| Requirement | Phase | Status |
|-------------|-------|--------|
| ISPL-01, ISPL-02, ISPL-03 | Phase 1 | Pending |
| RFLR-01, RFLR-02, RFLR-03, RFLR-04, RFLR-09 | Phase 2 | Pending |
| RFLR-05, RFLR-06, RFLR-07, RFLR-08 | Phase 3 (conditional) | Satisfied |
| TWIN-01, TWIN-02, TWIN-03, TWIN-04 | Phase 4 | Pending |
| BRKT-01, BRKT-02, BRKT-03 | Phase 5 | Pending |
| SCRY-01, SCRY-02, SCRY-03, SCRY-04 | Phase 6 | **Gap** -- inserted 2026-08-01 by user decision; ratify into `REQUIREMENTS.md` before closeout |
| CLUX-01 .. CLUX-08 | Phase 7 | **Gap** -- adopted 2026-08-02; the eight IDs are declared in the `07-0N-PLAN.md` frontmatter but do not exist in `REQUIREMENTS.md`. Ratify before closeout, same as SCRY-01..04 |
| (none assigned) | Phase 01.1 | **Gap** -- inserted after REQUIREMENTS.md was written; see the Phase 01.1 block above |
| (none assigned) | Phase 01.2 | **Gap** -- inserted 2026-07-27 from 01.1's D-06; ratify alongside CLSF-01/CLSF-02 |

**Coverage:** 19/19 of the ORIGINAL Cycle 21 requirements mapped. No orphans, no duplicates among
those 19. Phase 6's SCRY-01..04 are **not** in that count — they are proposed IDs, not yet in
`REQUIREMENTS.md`.
**Known gap:** Phases 01.1, 01.2 and 6 have no ratified requirement IDs. 01.1 and 01.2 are
defect-repair phases derived from spike 002 that gate Phase 2's measurement validity. Phase 6 is a
throughput phase inserted from the Phase 111.1 fallout. Ratify CLSF-01 / CLSF-02 (and a third ID for
01.2's vocabulary widening) plus SCRY-01..04 into `REQUIREMENTS.md` before milestone closeout.

---
*Roadmap created: 2026-07-26*
*Re-planned: 2026-07-26 — scope widened from 2 phases to 5; see PROJECT.md Decisions Log*
*Amended 2026-08-01 — Phase 6 (Scryfall Throughput) inserted by user decision after Phase 111.1's
200ms->500ms pacing change; see the Phase 6 block for the measurement that motivates both waves.*
*Amended 2026-08-03 — Phase 7's detail section was missing entirely (the file jumped Phase 6 → Phase 8),
so `roadmap.get-phase 7` returned malformed_roadmap. Section written from `07-CONTEXT.md` and the phase
README; D-1 recorded as RESOLVED = Option 3; Phase 7 added to the Release Posture table; progress rows
for Phases 4, 5, 7 and 8 refreshed against the discharged Codex gates.*
