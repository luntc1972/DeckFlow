---
phase: 04-functional-twins-detector
plan: 04
status: tasks-1-2-complete
task_3_status: NOT STARTED — blocking human checkpoint, awaiting developer
codex_review: RUN 2026-08-03 — CHANGES REQUIRED, 2 HIGH unfolded (see 04-REVIEWS.md)
date: 2026-08-02
---

# 04-04 Summary — merged twins section + density bound

**Tasks 1 and 2 are complete and green. Task 3 (the blocking human checkpoint) has not been run.**
Phase 4 is therefore **not** closed: Success Criterion 5 is a human judgement and Task 3 is its only
authoritative gate.

## What shipped

### Task 1 — merged findings section

`CutLabFindingPresenter.BuildFindingGroups` was rewritten from a capture-index-then-`List.Insert`
scheme to a **single-pass placeholder build**. On first encounter of a merged kind its group is
appended immediately and a reference kept; later encounters append to that group's item list. No
indexes, no deferred inserts, no shifting.

The merged-kind set is exactly `WeakFloorCase`, `ComboProtected`, `FunctionalTwins`. Every other kind
still yields its own single-item group.

**This corrected a live pre-existing defect.** The old algorithm recorded each merged kind's insert
index as `groups.Count` at first occurrence — a count of *non-merged* groups only — so two merged
kinds both captured index `1`, and inserting them in ascending order shifted each previous insert
right. Merged sections rendered in the **reverse** of their first-occurrence order. Reproduced on the
pre-change tree: `[CurveCongestion, WeakFloorCase, ComboProtected, FunctionalTwins]` came out as
`[CurveCongestion, ComboProtected, WeakFloorCase, FunctionalTwins]`.

The fix is **flag-independent** — it stays active with twins OFF, which is why
`BuildFindingGroups_TwoPreExistingMerges_AppearInFirstOccurrenceOrder_WithoutTwins` exists.

Also in Task 1:

- One `<p class="manabase-help">` twins help note in `CutLab.cshtml`, as an **independent** `@if`
  (not chained onto the `EnablerStarved` check), plus a character-identical note emitted by
  `renderStructuralFindings` in `cut-lab.ts`.
- Both Razor readers now normalize at lookup: the evidence-chip badge lookup and the card-text JSON
  reader, whose key set changed to
  `Model.Pool.Select(card => card.Name).Concat(Model.CardTextByCardName.Keys).Distinct(StringComparer.Ordinal)`.
  The old union with `ComboBadgeByCardName.Keys` emitted a normalized name as its own JSON entry and
  attached `comboContext` to that instead of to the raw one.
- `BuildComboBadgeByCardName(pool, cardComboMembership)` now re-keys normalized combo membership
  onto **raw** pool names server-side (D-24), so each raw spelling — including both DFC forms and
  case-distinct spellings — gets its own entry for the case-sensitive JavaScript consumer. No
  TypeScript normalizer was added and neither existing TS badge consumer changed.

### Task 2 — density bound

New `DeckFlow.Web.Tests/CutLabFunctionalTwinsDensityTests.cs`, 6 tests, driving
`CutLabStructuralFindings.Compute` directly at the `CutLabAnalyzedCard` level (never at the
`CardFact` / `CutLabRoleAssigner` level, which would couple the numbers to classifier heuristics).

## Density evidence — the measured numbers

These are the phase's density evidence, measured from a real run.

| Metric | Value |
|---|---|
| Total fixture entries | **130** |
| Lands | **38** (34 basics + Command Tower, Reliquary Tower, Exotic Orchard, Ancient Tomb) |
| Non-land | **92** |
| **Eligible (role-bearing non-land)** | **92** |
| Multi-role cards | **15** |
| Primary types | Artifact 15, Creature 16, Enchantment 15, Instant 15, Planeswalker 14, Sorcery 17 |
| Role assignments | ramp 16, draw 16, payoffs 15, engines 13, interaction-mass 13, protection 12, interaction-targeted 11, wincons 11 (all eight eligible keys) |
| Mana values | MV0 5, MV1 20, MV2 21, MV3 23, MV4 12, MV5 7, MV6 4 — MV1-3 is 64/92 ≈ 70% |

| Detector output | Diverse pool | Homogeneous control |
|---|---|---|
| `FunctionalTwins` findings | **6** | **3** |
| Distinct evidence card names | **20** | **65** |

Bounds asserted: findings `> 0` and `<= 12` (measured 6); distinct evidence names `<= 40`
(measured 20). Lands produce **zero** twin groups.

Note the control's shape: it fires **fewer** groups but **3.25×** the evidence cards. That is
precisely why the plan requires test 3 to bound distinct evidence names rather than group count —
one 65-card group is far worse for reviewability than six small ones, and a group-count bound alone
would not see it.

### The bound is live, not decorative

Mutating `TwinGroupMinimumCards` from 3 to 2 drives the diverse fixture to **14 findings** and the
bound fails loudly:

```
Expected at most 12 FunctionalTwins findings (measured baseline on this fixture: 6),
but the detector produced 14.
```

This only works because of a defect the blind verifier caught (see below): the fixture originally
had 64 of its 130 cards carrying `roles: []`, which `ComputeFunctionalTwins` can never select. Only
28 of 130 entries were eligible, so the "130-card" bound was really bounding a 28-card population and
would not have detected this regression.

### Load-bearing fixture properties

- **`TypeGroupOrder` tie** (required, or test 4's permutation proves nothing): Cluster A
  (ramp / Artifact / MV3, 4 cards) and Cluster B (ramp / Creature / MV3, 3 cards) share role and
  mana value but differ in primary type. `TypeGroupOrder` puts Creature before Artifact, so B must
  lead A under every input permutation. Deleting the `ThenBy(TypeGroupOrder)` tiebreak fails the
  test.
- **Named near-misses**: a below-threshold pair (interaction-mass / Sorcery / MV4); a trio split
  across mana values (protection / Instant at MV 1, 2, 3); a trio split across types (payoffs / MV2
  across Artifact, Creature, Enchantment).

## Verification

| Gate | Result |
|---|---|
| `dotnet build DeckFlow.sln -c Release --no-incremental` | **0 errors, 9 warnings** — all pre-existing `CS8629` in `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`. No new warnings. |
| `DeckFlow.Web.Tests` full suite | **2286 passed / 0 failed / 16 skipped** (baseline 2266 → **+20**) |
| `DeckFlow.Core.Tests` | 2011 / 2011 |
| vitest (from `DeckFlow.Web/`) | 3 / 3 |
| EOL | every touched file `CR=0`, matching `git show HEAD:` — no flips, no churn |
| Scope | only `ts/cut-lab.ts` under `wwwroot/`; no CSS; no compiled `.js` staged; no `.csproj`, `.editorconfig`, `.gitattributes`, `.gitignore`, `ROADMAP.md`, `REQUIREMENTS.md` or `STATE.md` touched |

Presenter tests 5 and 6 and all three `CutLabViewRenderTests` combo-badge regressions were confirmed
to **fail on the pre-change tree** for their stated reasons, so they are genuine mutation proofs
rather than decorative assertions.

A blind verifier ran **11 independent mutations**. Ten were caught. The one that was not is recorded
below and has since been fixed.

## Defects found during verification and fixed

**Fixture measured the wrong population (HIGH).** 64 of 130 fixture cards carried `roles: []` and
could never group; only 28 entries were eligible. Fixed — all 92 non-land entries are now
role-bearing across all eight eligible role keys, with 15 multi-role cards. The threshold mutation
above is the proof that the guard is now live.

**Fixture misstated a real card (MEDIUM).** `Bramble Elemental` was given mana value 3; the real card
is `{3}{G}{G}`, cmc 5. Renamed to the synthetic `Thicket Golem Alpha`, keeping the MV-3 Creature
grouping key that Cluster B's tie depends on — correcting the mana value would have destroyed the
load-bearing tie. The file header also wrongly declared four real lands synthetic; corrected.

**Help-note parity was one-directional (MEDIUM).** Mutating the **Razor** copy alone passed all 633
CutLab C# tests and all vitest tests — only the TypeScript side was pinned. Added
`RenderAsync_FunctionalTwinsSection_RendersTheHelpNoteCopyVerbatim`, which asserts the full sentence
verbatim through the `RenderAsync` harness. Proven load-bearing by mutating the sentence's tail.

## Findings recorded, not fixed

- **The plan's Task 1 verify command cannot exit 0 as written.** Its final segment runs vitest from
  the repo root, but the vitest config lives at `DeckFlow.Web/vitest.config.ts`, so a root-cwd run
  resolves a different vitest with no jsdom environment. Proven with a control: `cut-lab-proposal.test.ts`,
  which this plan does not touch, fails identically from the root. The working invocation is
  `cd DeckFlow.Web && npx --no-install vitest run ts-tests/…`. **The plan text needs fixing, not the code.**
- **`StringComparer.Ordinal` is a no-op rename.** `CutLabCardNames.cs:7` is
  `public static StringComparer Comparer { get; } = StringComparer.Ordinal;` — the comparer the plan
  says to replace already *is* the one it says to replace it with. The change is documentational
  (it makes the DTO's raw-key contract explicit and decouples it from the normalized-name comparer),
  not behavioral. Consequence:
  `BuildComboBadgeByCardName_PreservesDistinctRawPoolNamesThatDifferOnlyByCase` proves the
  **pool-iteration re-key**, not a comparer flip.
- **Test 1's upper bound is `<= 12`, where the plan's `<action>` says `<= 8`.** The plan contradicts
  itself in one sentence — it writes `<= 8` and then says to choose the bound as "the fixture's
  actual count plus generous headroom". Measured is 6. The `<acceptance_criteria>` pins no numeral,
  only that both bounds be asserted. `12` was chosen as 2× measured; `8` would also pass today and
  would also catch the 14-finding mutation. **Deliberate, disclosed deviation — developer's call.**
- **`BuildFindings_TwinEvidence_CarriesTheManaValueSuffix` passes both pre- and post-change.** It
  characterizes `BuildFindings`, which this task does not modify. Required by name by the verify
  gate, so it stays; it correctly pins the U+00B7 separator. It is simply not evidence for this
  change.
- **The three `BuildComboBadgeByCardName_*` tests are reflection-driven**, so on the pre-change tree
  they fail with `TargetParameterCountException` — a signature-level proof. The genuine mutation
  proof for the re-key is the six **updated** pre-existing assertions, which fail pre-change with
  `KeyNotFoundException: The given key 'Heliod, Sun-Crowned' was not present in the dictionary`.
- **`BuildComboBadgeByCardName` keys off `state.Pool`, not the working list** (as the plan
  specifies), so a card cut from the working list still receives a badge entry. This matches the
  sibling `CardTextByCardName = BuildPopupCardTextPatch(state.Pool)` and the Razor reader, so the
  transports agree — a deliberate superset, flagged for the owed Codex review.
- **The Razor view now calls the `internal` `CutLabCardNames`.** Compiles because Razor views here
  build into `DeckFlow.Web.dll` with no separate views assembly. Would break under runtime Razor
  compilation or a separate views assembly. First use of an internal member from a `.cshtml` here.
- **The density fixture uses a literal U+001F** as the `string.Join` separator in test 4's
  expected-evidence array. Invisible in most editors; a naive re-type breaks the test.

## New follow-up

### F-04-08 — the AJAX path drops the `EnablerStarved` help note

`renderStructuralFindings` in `DeckFlow.Web/wwwroot/ts/cut-lab.ts` emits the twins help note added by
this plan, but has **no** `EnablerStarved` note, while `CutLab.cshtml:687-690` does. An
`EnablerStarved` section therefore loses its help note the moment an AJAX decide patches the panel.
Pre-existing, outside TWIN-01..04, and another instance of the Phase 3 page-versus-AJAX divergence
class recorded in `03-VERIFICATION.md`.

This joins F-04-01 through F-04-07 already recorded in `04-04-PLAN.md`.

## Still owed

1. **Task 3 — the blocking human checkpoint.** Density and layout on a real ~130-card pool with the
   flag ON, across two viewports and two guild themes, plus the TWIN-02 before/after next-proposal
   observation and the AJAX-survival check. Success Criterion 5 cannot be closed without it.
2. **A Codex code review of this diff.** Codex was out of credits for this entire run
   (`ERROR: Your workspace is out of credits.`), so no cross-family reader saw the change. Per
   `CLAUDE.md` this is an owed gate: `gpt-5.6-sol` at `medium` effort, stage 2 (`codex exec` with a
   written brief), triggered on three of its four conditions — normalization/matching logic changed,
   the claim needs checking against the repo rather than the diff, and it is an owed gate with no
   prior independent review. This is the **second consecutive** Phase 4 plan not written by Codex.
3. **The commit body** must state that the pre-existing `WeakFloorCase`/`ComboProtected` render order
   was reversed by the old ascending-insert algorithm and that this change deliberately fixes it — a
   plan acceptance criterion no worker can satisfy.
4. **The production `analysis.cut-lab.functional-twins` flag stays OFF.** This phase deploys dark;
   flipping it is a separate post-UAT developer decision.
