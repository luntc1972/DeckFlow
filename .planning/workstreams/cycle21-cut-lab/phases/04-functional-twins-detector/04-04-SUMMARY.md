---
phase: 04-functional-twins-detector
plan: 04
status: complete
task_3_status: APPROVED 2026-08-03 — see "Task 3 — human checkpoint" below
codex_review: RUN 2026-08-03 — 2 HIGH folded at `1fc48dd6` (see 04-REVIEWS.md)
date: 2026-08-03
---

# 04-04 Summary — merged twins section + density bound

**All three tasks are complete.** Tasks 1 and 2 shipped 2026-08-02; Task 3, the blocking human
checkpoint, was run and **approved 2026-08-03**. Success Criterion 5 is closed and with it Phase 4.

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

## Task 3 — human checkpoint: APPROVED 2026-08-03

**Verdict: approved.** Density and layout accepted on a real cEDH pool. Recorded here per the plan's
Step 7.

**Pool used.** EDHREC's cEDH average decklist for **Kinnan, Bonder Prodigy** (98 cards, from
`_edhrec-brackets/cells/kinnan-bonder-prodigy__cedh.json`) plus 32 real cEDH staples drawn from the
Thrasios and Urza cells as the over-sized intake — **132/100 cards**, Bracket 5 (cEDH), Focused.
A synthetic fixture could not answer this question (D-22); an EDHREC aggregate preserves the real
correlation that matters here, namely genuine mana-rock clustering at mana value 0-2. The pool text,
both raw report JSONs and every screenshot are committed under
`.planning/ui-design/cut-lab/twins-checkpoint-2026-08-03/`.

**Density — the gate itself.**

| Measure | flag OFF | flag ON |
|---|---|---|
| Structural findings | 31 | 40 (+9) |
| "Functional twins" sections rendered | 0 | **1** (merge confirmed) |
| Twin groups (leads) | — | **9** |
| Twin evidence chips | — | **38**, over 32 distinct cards |
| Round banner | Round 2 · Structural choices | Round 1 · Obvious cuts |
| Next proposal | Chrome Mox | Chrome Mox |

Leads descend by mana value — 2, 2, 2, 1, 1, 1, 1, 1, 0 — so the costliest group is listed first.
The flag-OFF pass showed no twins section, no twins help note and no twins contribution; the OFF
check was for *absence of twins output*, not for the pre-Phase-4 panel, per the plan's warning.

**Step 4 — TWIN-02 end to end.** The proposed card did **not** change (Chrome Mox both ways), but the
**card-to-round assignment did**: the pool sits in Round 2 with the flag OFF and Round 1 with it ON.
That is the alternative the plan explicitly accepts, and it is the ranking movement TWIN-02 predicts.

**Step 5 — AJAX parity.** `POST /api/cut-lab/decide` returned 200; the navigation count stayed at 1,
so the patch was in place with no reload. The merged section survived with the same 9 groups, the
same help note, the same section order and its combo badges intact (10 badges before and after).

**Step 6 — layout, two viewports, two guild themes.** Azorius (light) and Nyx (dark), at 1440px and
390x844. Heading and help note wrap cleanly; the `manabase-help` note is legible on the dark theme;
evidence chips wrap without overflowing. Combo badges render on twin evidence chips, which exercises
the `CutLab.cshtml:708` normalized-lookup repair. Locked-chip styling was verified on **Curve
congestion** — a non-twins kind, as required, since twins evidence can never be locked — and renders
with its distinct outline: 16 locked chips there, 9 in Redundant finishers, 19 in Combo-protected,
and **0 inside the twins section**, which independently confirms Wave 2's locked-card exclusion.

**Layout cost attributable to this phase.** Measured on the same pool, both flag states:

| | flag OFF | flag ON | delta |
|---|---|---|---|
| Findings panel, desktop | 3,535px | 4,340px | **+805px** |
| Findings panel, 390px | 7,116px | 8,951px | **+1,835px** |
| Whole page, desktop | 19,610px | 20,415px | +805px |
| Whole page, 390px | 45,177px | 47,011px | +1,834px |

The page is enormous with the flag OFF; that is the pre-existing problem Phase 7 D-1 exists to fix,
not something this phase introduced. The twins section's own contribution is the delta column.

**Observations raised at approval, accepted rather than blocking** — recorded as F-04-09 and F-04-10
below.

## New follow-ups from the Task 3 checkpoint

### F-04-09 — multi-role cards emit duplicate twin groups with identical card sets

Nine groups covered only 32 distinct cards. Faerie Mastermind, Thrasios and Wan Shi Tong appear as
**both** "3 creature cards fill your Card draw slot at mana value 2" and "…your Engines slot at mana
value 2" — the same three cards, twice, differing only in the role label. Mana Vault and Sol Ring
likewise appear under both "Ramp · MV 1" and "Win conditions · MV 1", and Sensei's Divining Top under
two groups.

This is inherent to iterating eight roles (`ComputeFunctionalTwins` groups by role ∩ mana value ∩
primary type, and a card carries multiple roles), so it is by construction, not a defect. It is the
single largest contributor to apparent density. If the section ever needs tightening, **deduplicating
groups whose normalized card set is identical is the cheaper first lever** — it removes repetition
without raising `TwinGroupMinimumCards`, which would also discard legitimate 3-card groups.
Threshold tuning remains a product decision.

### F-04-10 — the "ON THIS PAGE" anchor nav overlays findings content at 390px

At the mobile viewport the sticky in-page anchor bar (Process / Decide / Goals) paints over a chip
row inside the findings panel. It is page-level and pre-existing rather than twins-specific — the
twins section merely happens to sit under it — but it lands on the section this phase added and is
worth folding into the Phase 7 workflow-UX work.

## Still owed

1. **The production `analysis.cut-lab.functional-twins` flag stays OFF.** This phase deploys dark;
   flipping it is a separate post-UAT developer decision.

**Discharged since this summary was first written:** Task 3 (above), and the Codex code review — it
ran 2026-08-03 against the live range `8b5d2e8e..908402cd` and its two HIGH findings were folded at
`1fc48dd6`. The commit-body requirement about the reversed `WeakFloorCase`/`ComboProtected` render
order was satisfied when Tasks 1-2 were committed.
