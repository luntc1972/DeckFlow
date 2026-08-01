# Phase 4: Functional-Twins Detector - Research

**Researched:** 2026-07-29
**Domain:** Cut Lab structural-finding detector + discriminating-tally engine + dark-launch feature flag (DeckFlow.Web C#)
**Confidence:** MEDIUM-HIGH (all core mechanics traced with `file:line`; two genuine planning decisions flagged, not guessed)

## Summary

The finding-kind, tally, and rendering pipeline is fully generic and requires no new plumbing to
*display* a new finding kind (`DeckFlow.Web/Views/Deck/CutLab.cshtml:683-737` iterates
`Model.FindingGroups` with no per-kind switch except a hardcoded `EnablerStarved` help note). The
tally mechanism that makes a finding "discriminating" is a simple exclusion set
(`CutLabCutRoundEngine.ExcludedFindingKindsFromTally`, `CutLabCutRoundEngine.cs:111-117`) consumed
by `BuildFindingTallies` (`CutLabCutRoundEngine.cs:359-394`) — leaving a new kind off that set is
the entire TWIN-02 mechanism.

The real risk is data plumbing, not display. The detector input type, `CutLabAnalyzedCard`
(`CutLabStructuralFindings.cs:69-81`), carries `Name`, `ManaValue`, `IsLand`, `Roles`, `Categories`,
`Quantity` — and **nothing else**. It has no `TypeLine`, no `IsLocked`, no `IsCommander`. All three
are needed for TWIN-01 (primary type) and TWIN-04 (lock/commander exclusion), and none reach
`CutLabStructuralFindings.Compute()` today. `Compute()` is also called from a 4th site beyond the
three page/AJAX/patch paths (`CutLabSimulationService.cs:524-535`, curve-congestion-only, for
round-3 delta magnitude) which the planner must account for when deciding how the twins detector is
gated. A dedicated dark-launch flag was built earlier today (2026-07-29) as a template; its full
recipe is documented below with citations.

Both `role` (multi-membership) and `primary type` (single, priority-ordered) already have
production helpers with test coverage. **Mana-value bucketing is the one dimension without a
reusable shared helper** — a bucket function exists but is `private` and scoped to
`CurveCongestion` only; its boundaries are `["0-1","2","3","4","5+"]`
(`CutLabStructuralFindings.cs:402-425`). Reusing it (by extraction) vs. inventing new boundaries is
an explicit planning decision — do not assume the planner may pick either without flagging it.

**Primary recommendation:** Thread `TypeLine`, `IsLocked`, and `IsCommander` into the detector's
input (either by extending `CutLabAnalyzedCard` or by having the new detector consume
`CutLabRoundInputCard`-shaped data, the type already built by `CutLabCutRoundEngine.BuildInputs`,
`CutLabCutRoundEngine.cs:299-324`, which already joins pool lock/commander state with analyzed
role/MV data). Reuse `CardTypeLine.PrimaryType` for the type dimension and extract
`ManaValueBucket`/`BucketSortKey` (`CutLabStructuralFindings.cs:402-436`) into a shared location
rather than re-deriving new boundaries. Gate the new kind behind a dedicated
`analysis.cut-lab.*`-namespaced flag using today's `analysis.cut-lab.commander-floors` flag as the
literal template (flag key, seed rows, catalog description, DI, read pattern all cited below).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Twin-group detection (role ∩ MV bucket ∩ type) | API / Backend (`DeckFlow.Web/Services/CutLab`) | — | Pure C# detector over already-resolved card facts; no new I/O |
| Discriminating tally / round ordering | API / Backend (`CutLabCutRoundEngine`) | — | Existing pure engine; new kind participates by omission from an exclusion set |
| Finding rendering | Frontend Server (SSR, Razor `CutLab.cshtml`) | — | Fully generic loop over `FindingGroups`; no new markup required for a bare finding |
| Feature flag state | API / Backend (`FeatureFlagStore`, Postgres/SQLite) | Frontend Server (`/Admin/Flags`) | Existing dual-dialect store + generic admin listing; a new key is just two seed rows + a catalog description |
| AJAX proposal-order patch | API / Backend (`CutLabApiController`, `CutLabUiPatchBuilder`) | — | Must consume the same `CutLabStructuralFindings.Compute()` output as the page path; already unified as of today's `ICutLabFloorResolver` work (see D below) |

## User Constraints

No `CONTEXT.md` was found under
`.planning/workstreams/cycle21-cut-lab/phases/04-functional-twins-detector/` at research time —
there are no locked decisions, discretion notes, or deferred ideas to copy verbatim. The four
requirements TWIN-01..04 (verbatim below) and the ROADMAP's "Release Posture" row for Phase 4
(`.planning/workstreams/cycle21-cut-lab/ROADMAP.md:49`) are the only upstream constraints:

> 4. Functional twins | Yes — new structural finding, changes proposal order | Own release.
> **Recommend a dedicated flag** — this one changes which card is proposed next, the
> highest-blast-radius behavior change in the cycle. Deploy dark, flip after UAT on a real pool.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TWIN-01 | New `CutLabFindingKind` groups unlocked, non-commander pool cards by (role ∩ MV bucket ∩ primary type), fires at >= 3 members | Section A (finding-kind mechanics), Section B (grouping dimensions) — role and type helpers exist; MV bucket helper exists but is private/CurveCongestion-scoped |
| TWIN-02 | Kind is NOT in `ExcludedFindingKindsFromTally`, contributes to round-1/round-2 tally | Section A.3 — exact tally mechanism traced |
| TWIN-03 | Evidence ordered highest MV first within a group | Section A.1 — evidence-ordering precedent (`ComputeCurveCongestion` groups then orders bucket-internally; twins detector needs its own `OrderByDescending(ManaValue)`) |
| TWIN-04 | Locked cards and commander excluded from groups; combo-protected members still listed | Section C — `CutLabAnalyzedCard` has no lock/commander field today; combo-protected has no cross-kind suppression to worry about |
</phase_requirements>

## A. Finding-kind mechanics

### A.1 — Enum, and 3 kinds traced end to end

Full current enum (`DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs:6-25`):

```
CurveCongestion, StrandedSubtheme, RedundantFinishers, WeakFloorCase, ComboProtected, EnablerStarved
```

**Trace 1 — `CurveCongestion`** (a discriminating kind, i.e. NOT excluded from tally):
- Constructed in `ComputeCurveCongestion` (`CutLabStructuralFindings.cs:179-205`). Groups
  non-land pool cards by `ManaValueBucket(card.ManaValue)` (`:402-425`), skips buckets under 30%
  share or under 12 cards (`CongestionShareThreshold`/`CongestionMinimumCards`, `:92-95`).
- Evidence payload: `CutLabFindingEvidence(card.Name, card.ManaValue)` — no badge (`:203`).
- Reaches UI via `CutLabFindingPresenter.BuildFindings` (`DeckFlow.Web/Models/CutLab/CutLabFindingPresenter.cs:9-26`),
  which formats `"{Name} · MV {value}"` when `ManaValue` is non-null, then
  `CutLabViewModel.Findings`/`FindingGroups` (`DeckFlow.Web/Models/CutLabViewModel.cs:134,137,310-311`), rendered generically
  by `CutLab.cshtml:683-737` (no per-kind special case for `CurveCongestion`).
- Also feeds `CutLabSimulationService.CurveCongestionValue` (`CutLabSimulationService.cs:510-536`),
  a 4th, standalone call site of `Compute()` outside the shared `BuildFindingsAndRoundPlan` path,
  used only for round-3 delta-magnitude ordering.

**Trace 2 — `WeakFloorCase`** (a whole-role kind, IS excluded from tally):
- Constructed in `ComputeWeakFloorCases` (`:259-282`), iterating the fixed
  `WeakFloorRoleOrder` array (`:122-133`, 9 role keys). Fires when in-pool count for a role is
  `<= floor + WeakFloorMargin(1)` (`:107,269`).
- Evidence: every card in that role, `ManaValue: null` (`:280`).
- Excluded from tally at `CutLabCutRoundEngine.cs:114` — "attaches uniformly to every member of the
  role" (comment at `:109-111`) is exactly the reasoning TWIN's requirements document repeats
  almost verbatim (`REQUIREMENTS.md`, "Why this matters most").
- UI: `CutLabFindingPresenter.BuildFindingGroups` (`DeckFlow.Web/Models/CutLab/CutLabFindingPresenter.cs:29-92`) specially
  MERGES all `WeakFloorCase` findings into one displayed section (`weakFloorItems`, branch `:39-49`, insert block `:71-79`) —
  this is a per-kind view-model special case a new kind does NOT get unless explicitly added.

**Trace 3 — `ComboProtected`** (excluded from tally, but composes with locked/other findings):
- Constructed in `ComputeComboProtected` (`:303-385`), two passes: complete combos in the pool
  (`:313-342`, badge `ComboBadgeState.CompletePiece`) and near-combo variants grouped by their
  in-deck card set (`:344-380`, badge `ComboBadgeState.NeedsPartner`).
- Excluded from tally at `CutLabCutRoundEngine.cs:116`.
- UI also merges all `ComboProtected` findings into one section
  (`DeckFlow.Web/Models/CutLab/CutLabFindingPresenter.cs`, branch `:51-61`, insert block `:81-89`).
- **No suppression logic exists anywhere that removes a card from another detector's evidence
  because it is combo-protected** — confirmed by reading all six `Compute*` methods; each is
  independent and only reads `pool`/`floors`/combo inputs, never another detector's output. This
  directly satisfies the "compose rather than suppress" half of TWIN-04 for free, PROVIDED the new
  twins detector itself does not add a combo-membership filter (a risk to avoid, not a gap to fix).

### A.2 — Pattern for adding a new finding kind (every file that changes)

1. **Enum** — add a member to `CutLabFindingKind` (`CutLabStructuralFindings.cs:6-25`).
2. **Detector** — add a private `ComputeXxx` method following the `IEnumerable<CutLabFinding>`
   yield pattern (e.g. `:179-205`), and one line adding it to the `findings` list inside
   `Compute()` (`:158-176`). If the detector needs data `Compute()`'s current pool shape lacks
   (see Section C), the `Compute()` signature and all 4 call sites change too (Section D.2 below).
3. **Tally exclusion decision** — TWIN-02 requires NOT adding the new kind to
   `ExcludedFindingKindsFromTally` (`CutLabCutRoundEngine.cs:111-117`). No file change needed to
   "opt in" — omission is the default; this only matters if a future kind needs the opposite.
4. **Round-3 delta magnitude** — `CutLabSimulationService.CurveCongestionValue`
   (`CutLabSimulationService.cs:510-536`) calls `Compute()` filtering to
   `CutLabFindingKind.CurveCongestion` only; a new kind is inert there by construction (the
   `.Where` filter excludes it) unless the planner deliberately wants twins to feed round-3
   ordering too.
5. **View-model grouping (optional)** — `CutLabFindingPresenter.BuildFindingGroups`
   (`DeckFlow.Web/Models/CutLab/CutLabFindingPresenter.cs:29-92`) only special-cases `WeakFloorCase` and `ComboProtected`
   (merge multiple findings of that kind into one section). Absent a special case, each qualifying
   twin group renders as its OWN separate `cutlab-finding` block
   (`CutLab.cshtml:683-737`) — i.e. if a pool has 3 qualifying twin groups, the UI shows 3 separate
   "Functional twins"-headed blocks unless the planner adds a merge case like `WeakFloorItems`.
   This is a real UI-density question tied to Success Criterion 5 ("finding density ... stays
   reviewable") — not resolved by existing code, flagged for the planner.
6. **Heading copy** — the `Heading` string passed to `new CutLabFinding(...)` (e.g. `"Curve
   congestion"`, `:201`) is authored inline per detector; no central copy/strings file exists to
   update (confirmed: no `switch` over `CutLabFindingKind` exists in production code outside
   `CutLabStructuralFindings.cs`, `CutLabCutRoundEngine.cs`, and `CutLabFindingPresenter.cs` —
   `grep` swept `DeckFlow.Web` for `CutLabFindingKind` and found only those three files plus
   `CutLabSimulationService.cs:532` and two DTO property declarations in
   `CutLabViewModel.cs:1097,1113` / `CutLabDecideApiResponse.cs:148,164`, none of which enumerate
   kinds exhaustively).
7. **Tests** — see Section E (Q12) below for exact files to mirror.

### A.3 — `ExcludedFindingKindsFromTally` contents, why, and the exact scoring path

Current set (`CutLabCutRoundEngine.cs:111-117`): `WeakFloorCase`, `RedundantFinishers`,
`ComboProtected`. Comment at `:109-111`: "'Obvious cuts' should reflect findings that discriminate
among cards, not role-wide warnings that attach to every member of a protected or redundant role
uniformly." `RedundantFinishers` (`ComputeRedundantFinishers`, `:240-257`) is role-wide by the same
logic (every wincon gets the same evidence list when the role is over floor+margin) —
`CurveCongestion`, `StrandedSubtheme`, and `EnablerStarved` are NOT excluded because their evidence
lists are genuinely per-card-selective (a curve bucket, a 2-4-card subtheme, or a specific
near-combo's in-deck cards — not "every card holding a role").

**Exact scoring/ordering path** (`BuildQueue`, `CutLabCutRoundEngine.cs:187-297`):
1. `BuildFindingTallies(findings.Findings)` (`:359-394`) iterates every finding NOT in the excluded
   set; for each finding's evidence card names, increments a per-card `Count` and adds the
   `CutLabFindingKind` to a per-card `HashSet<Kind>`.
2. `eligibleCards` = working-list cards that are `!IsLocked && !IsCommander && !accepted &&
   Quantity <= cardsRemainingToTarget` (`:219-226`).
3. `round1` = eligible cards with `Tally.Count >= 2`, ordered
   `OrderByDescending(Tally.Count).ThenBy(ManaValue).ThenBy(Name)` (`:233-239`).
4. `round2` = `Tally.Count == 1`, ordered `OrderBy(ManaValue).ThenBy(Name)` (`:241-246`).
5. `round3` = `Tally.Count == 0`, ordered by a delta-magnitude hint then MV then name (`:248-254`).
6. `NextProposal = queue.FirstOrDefault()` (`:293`) — the single card shown to the user next.

So a card that newly earns a `Twin` finding (not excluded) moves from round 3 into round 2 (if it
had zero other discriminating findings) or from round 2 into round 1 (if it already had exactly
one) — literally reordering `NextProposal`. This is the TWIN-02 mechanism verified precisely, and
it is proven with two existing test names that are the exact mirror to reuse
(`BuildQueue_TwoDiscriminatingFindings_PlacesCardInRound1`,
`BuildQueue_WholeRoleFindingsDoNotInflateDiscriminatingTally`,
`DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs:12-63`).

## B. The three grouping dimensions

### B.4 — Role

Assigned by `CutLabRoleAssigner.AssignRoles` (`DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs:112-197`),
which layers `PlanRoleClassifier.Classify` (`DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs:36,44`,
returns the `[Flags] PlanRole` enum — `DeckFlow.Core/Manabase/ManabaseModels.cs:164-181`: `None`,
`Payoff`, `Engine`, `TutorCombo`, `Interaction`) on top of `DeckStatClassifier` heuristics. The
OUTPUT consumed everywhere in Cut Lab, including by `CutLabAnalyzedCard.Roles`, is NOT `PlanRole`
directly — it is a `List<string>` of up to 10 fixed Cut Lab role keys: `lands, ramp, draw,
interaction-targeted, interaction-mass, protection, engines, payoffs, wincons` (the 9-element
`RoleKeys` array, `CutLabRoleAssigner.cs:29-40`) plus a 10th fallback `other` when nothing else
matched (`:191-194`).

**Role-key → display-label conversion already has a helper — reuse it, do not re-implement it.**
`CutLabRoleAssigner.DisplayLabelFor(string roleKey)` (`CutLabRoleAssigner.cs:89-90`, `internal static`)
is exactly `RoleDisplayLabels.TryGetValue(roleKey, out string? label) ? label : roleKey` — i.e. "display
label, falling back to the raw key". Any new detector that needs a human-readable role name in a lead
should call `CutLabRoleAssigner.DisplayLabelFor(...)` rather than inlining a second
`RoleDisplayLabels.TryGetValue` fallback. (Verified live 2026-07-29; the helper's name is
`DisplayLabelFor`, not `Describe`.)

**Multi-role membership is explicit and by design** — doc comment at `CutLabRoleAssigner.cs:14`:
"Multi-role membership is allowed; cutting a card reduces every role count it currently fills." A
card CAN and DOES hold more than one role key (e.g. a card can be both `engines` and `wincons` if
it hits both `PlanRole.Engine` and `IsClosingPowerCard`). This directly affects TWIN-01 grouping:
existing detectors (`CardsInRole`, `CutLabStructuralFindings.cs:387-388`) test `Roles.Contains(key)`
per role independently, so a multi-role card is counted once per matching role in every existing
detector that iterates roles. **The planner must decide** whether a twins detector groups by EVERY
role a card holds (a multi-role card could appear in 2+ twin groups simultaneously) or by a single
"primary" role — no existing precedent picks one over the other, because no existing detector
groups by role AND type AND MV jointly. Flagged, not resolved.

### B.5 — Mana-value bucket

**No reusable/public helper exists.** `ManaValueBucket` and `BucketSortKey`
(`CutLabStructuralFindings.cs:402-436`) are `private static` methods scoped inside
`CutLabStructuralFindings`, used only by `ComputeCurveCongestion`. Their exact boundaries:

```
<= 1  -> "0-1"
<= 2  -> "2"
<= 3  -> "3"
<= 4  -> "4"
else  -> "5+"
```

No other Cut Lab, manabase, or Core code defines a competing MV-bucket scheme (grep for
`ManaValueBucket`/`ManaValueTier`/similar across `DeckFlow.Web` and `DeckFlow.Core` found no other
hits). **This is the single biggest undecided input to grouping**, per the task brief: either (a)
extract `ManaValueBucket`/`BucketSortKey` to internal/public visibility and reuse verbatim (keeps
TWIN's buckets consistent with the existing Curve Congestion finding a user already sees on the
same page), or (b) define new boundaries specifically for twin-matching (e.g. exact MV equality
instead of buckets, since "same cost" in the community heuristic TWIN cites usually means exact MV,
not a 5-wide bucket). **Do not assume either — this must be an explicit planning/CONTEXT decision.**

### B.6 — Primary card type

`CardTypeLine.PrimaryType(typeLine)` (`DeckFlow.Core/Manabase/CardTypeLine.cs:43-71`) is the
existing, tested, production helper. It splits on `FrontFace` (everything before `//`, `:36-37`,
handling DFC/MDFC/Adventure by construction), strips the subtype half (splits on `—`), strips
supertypes (`Legendary, Basic, Snow, World, Ongoing, Host`, `:13-21`), then matches against a fixed
priority order `Creature, Planeswalker, Battle, Instant, Sorcery, Artifact, Enchantment, Land`
(`:23-33`), falling back to `"Other"`. Multi-type handling is priority-based, not enumerative — an
"Artifact Creature" returns `"Creature"` because `Creature` sorts first in `PrimaryTypePriority`.
Verified by table-driven tests at `DeckFlow.Core.Tests/Manabase/CardTypeLineTests.cs:34-44`,
including the exact cases: `"Artifact Creature — Golem" -> "Creature"` (:36) and
`"Creature — Elf // Instant — Adventure" -> "Creature"` (:40, DFC/Adventure front-face handling).

This same array is re-exposed as `CutLabRoleAssigner.TypeGroupOrder`
(`CutLabRoleAssigner.cs:43-54`, adds `"Other"` as a 9th display bucket) and is already used
elsewhere in Cut Lab for the existing "Type" pool grouping view
(`CutLabViewModel.BuildTypeGroups`, `DeckFlow.Web/Models/CutLabViewModel.cs:472-511`, calling
`CardTypeLine.PrimaryType` at `:482`). **Reusing `CardTypeLine.PrimaryType` directly is
low-risk and has direct precedent inside Cut Lab itself.**

**DFC front-face naming prior art** (as flagged in the task): `CardNormalizer.Normalize`
(`DeckFlow.Core/Normalization/CardNormalizer.cs:16-30`) splits a two-faced *display name* at
`" / "` (after collapsing `" // "` to `" / "`) and keeps only the front-face name for cross-source
name matching (`:22-27`) — this is name-string normalization, a different concern from
`CardTypeLine.FrontFace`'s type-line splitting, but the same "front face is canonical" convention.
`CutLabCardNames.Normalize` (`DeckFlow.Web/Services/CutLab/CutLabCardNames.cs:9-14`) is a thin
wrapper delegating to it, used throughout Cut Lab for name-keyed dictionary lookups.

## C. Exclusions and composition

### C.7 — Lock-state representation and predicate

`CutLabPoolCard.IsLocked` (`DeckFlow.Web/Models/CutLab/CutLabState.cs:149-150`, a plain `bool`
init property). The predicate that consumes it for proposal eligibility is inline in `BuildQueue`:
`!card.IsLocked` (`CutLabCutRoundEngine.cs:221`, on `CutLabRoundInputCard.IsLocked`, itself sourced
from `CutLabPoolCard.IsLocked` via `BuildInputs`, `:299-324`). `CutLabLockRules` provides pure
state-mutation helpers (`LockCard`/`UnlockCard`/`LockPackage`/`UnlockPackage`,
`DeckFlow.Web/Services/CutLab/CutLabLockRules.cs:29-101`) but no separate "is-locked" query beyond
the field itself.

**Critical gap: `CutLabAnalyzedCard` (the detector input type) has NO `IsLocked` field.**
`CutLabAnalysisContextBuilder.BuildAsync` builds one `CutLabAnalyzedCard` per `workingList` entry
UNCONDITIONALLY (`DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs:199-242`) — locked
and commander cards are included in `context.AnalyzedCards` exactly like any other card. This means
**every existing detector today (`CurveCongestion`, `StrandedSubtheme`, etc.) includes locked and
commander cards in its findings/evidence** — lock/commander filtering only happens later, inside
`BuildQueue`'s `eligibleCards` filter (`CutLabCutRoundEngine.cs:219-226`), which governs the
*proposal queue*, not finding computation. TWIN-04's requirement that twin GROUPS THEMSELVES
exclude locked/commander cards is therefore a stricter, novel behavior with no existing precedent
inside `Compute()` — the twins detector needs lock/commander data threaded into (or alongside)
`Compute()`'s pool argument, which it does not receive today.

### C.8 — Commander identification within the pool

Two representations exist and must not be conflated:
- `CutLabPoolCard.IsCommander` (`CutLabState.cs:146-147`), a per-card bool flag on pool state,
  enforced immutable-locked by `CutLabLockRules.EnforceCommanderLock`
  (`CutLabLockRules.cs:13-26`, called after every lock/unlock mutation).
- `CutLabCommanderNames.Resolve(state)` (added today, per the ledger; used at
  `CutLabApiController.cs:84` and `CutLabController.cs:280`) — a name-set derivation, the single
  shared replacement for what were previously three independent copies of the same logic (per
  `.foreman/ledger-cutlab-flag-ajax-2026-07-29.md`, Task 2 round 2). Exact file/line for this
  helper was not directly opened in this research pass — **flagged under Unverified** below; the
  ledger's citations (`CutLabApiController.cs:84/172/230/359`, `CutLabController.cs:280`) are
  call-site references, not a confirmed definition-site read.
- `CutLabAnalysisContextBuilder.BuildAsync` independently computes `commanderNameSet` from the
  `commanderNames` parameter it is given (`CutLabAnalysisContextBuilder.cs:186-188`) and stamps
  `isCommander` per resolved card (`:210`) — but, like lock state, this `isCommander` flag is used
  ONLY to compute `commanderManaValue` (`:226-229`) and is **not** carried onto the emitted
  `CutLabAnalyzedCard` (confirmed: the `CutLabAnalyzedCard` constructor call at `:233-241` does not
  pass an `IsCommander` argument — the record has no such member).

### C.9 — Combo-protection representation and suppression risk

Combo membership is `CutLabCardComboMembership(CompleteCombos, NearCombos)`
(`CutLabAnalysisContextBuilder.cs:75-77`), keyed by normalized card name in
`CutLabClassificationContext.CardComboMembership`
(`CutLabAnalysisContextBuilder.cs:85-90`). `ComputeComboProtected`
(`CutLabStructuralFindings.cs:303-385`) reads this to build the `ComboProtected` finding
independently of every other detector. As established in A.1/Trace 3, **no code path filters a
card OUT of another detector's evidence because it is combo-protected** — there is no suppression
logic to disable for TWIN-04. The only real risk is the twins detector's OWN implementation
accidentally importing combo-membership data as an exclusion filter; nothing in the existing
codebase would force that, so this is a "don't add it" caution, not a "find and disable it" fix.

## D. Feature flag

### D.10 — Full recipe for a new dedicated flag, safe-OFF by default

Template: `analysis.cut-lab.commander-floors`, built earlier today (2026-07-29,
`.foreman/ledger-cutlab-flag-ajax-2026-07-29.md`, Task 1, commits `983ea700`/`f17a612f`/`93f0e3af`).

1. **Naming convention**: dotted namespace `analysis.cut-lab.<feature>` for internal
   computation-level gates (distinct from `tool.*`, which is reserved for whole-page/endpoint
   visibility toggled via the `[FeatureFlagGate("tool.x.enabled")]` attribute, e.g.
   `CutLabApiController.cs:54` on the `/api/cut-lab/decide` endpoint — that attribute pattern is
   NOT what commander-floors or a twins flag uses).
2. **Key constant**: declared as a `public const string` on the primary consuming service, e.g.
   `CutLabPageService.CommanderFloorsFlagKey = "analysis.cut-lab.commander-floors"`
   (`DeckFlow.Web/Services/CutLab/CutLabPageService.cs:122`).
3. **Seed rows — BOTH dialects, same key, opposite literal syntax**:
   - Postgres: `('analysis.cut-lab.commander-floors', FALSE),`
     (`DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs:232`, inside `PostgresSeedSql`,
     `:198-248`).
   - SQLite: `('analysis.cut-lab.commander-floors', 0),`
     (`FeatureFlagStore.cs:284`, inside `SqliteSeedSql`, `:250-...`).
   - Both blocks end `ON CONFLICT (key) DO NOTHING` — an unseeded key added to only one block, or
     with a stale operator-set row already present, is silently skipped, preserving operator state
     (`FeatureFlagStore.cs:196-197` comment).
4. **Catalog description** (drives the `/Admin/Flags` row text): add a dictionary entry keyed by
   the same string in `FeatureFlagCatalog.cs:128-130` — the template text is: *"Cut Lab: enable
   the commander-aware floor defaults layer on the role-floors table and floor resolution. Seeded
   OFF; off = byte-identical to the pre-Phase-3 bracket-only UI and behavior."*
5. **DI**: `IFeatureFlagCache` is registered ONCE, generically, for all flags —
   `AddDeckFlowFeatureFlags()` (`DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs:20-27`),
   called from `Program.cs:113` (per the ledger; not independently re-read this pass). **A new key
   needs NO new DI registration** — only the seed rows and catalog entry. `IFeatureFlagCache` is
   injected as an OPTIONAL constructor parameter (`IFeatureFlagCache? featureFlags = null`,
   `CutLabPageService.cs:161`) — the ledger's "load-bearing fact" #2 warns this must remain
   registered or the flag is silently stuck OFF with no compile error and no failing test.
6. **Admin UI exposure**: automatic. `AdminFlagsController.Index()`
   (`DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs:49-60`) renders every key from
   `_cache.Snapshot()` except keys starting with `"tool."` (those go on `/Admin/Tools` instead,
   `:57`). A new `analysis.cut-lab.*` key appears on `/Admin/Flags` automatically once seeded — no
   separate registration step.
7. **Read pattern — fail-safe OFF, NOT `IFeatureFlagCache`'s own default**:
   ```csharp
   // CutLabPageService.cs:622-626
   private bool IsFlagOn(string key)
       => _featureFlags is { } flags
           && flags.Snapshot().TryGetValue(key, out bool enabled)
           && enabled;
   ```
   This is deliberately inverted from `IFeatureFlagCache`'s documented default (missing key =
   `true`/enabled) per the ledger's design-call #1: a dark-launch flag must default OFF on a
   seeding failure, never silently ship ON. `CutLabFloorResolver.Resolve`
   (`DeckFlow.Web/Services/CutLab/CutLabFloorResolver.cs:38-46`) inlines the identical
   `_featureFlags is { } flags && flags.Snapshot().TryGetValue(...) && enabled` pattern rather than
   calling a shared helper — there is no cross-service shared `IsFlagOn` utility; each consumer
   repeats the 3-line pattern.
8. **No-signature-change constraint (if applicable)**: the commander-floors flag was gated by
   choosing which optional dependency to PASS, not by changing any method's parameter list —
   `commanderFloorsEnabled ? _roleFloorBaseline : null` at the single
   `CutLabFloorDefaults.ResolveDefaults` call site
   (per the ledger's load-bearing fact #1; call site not independently re-verified in this pass at
   `CutLabPageService.cs:298`). A twins flag will likely NOT have this luxury — `Compute()`
   currently has no such "pass null to disable" seam for a brand-new detector; the gate will more
   likely be a new boolean parameter on `Compute()` mirroring `comboDataAvailable`/
   `categoryDataAvailable` (`CutLabStructuralFindings.cs:144-176`), which DOES require touching
   `Compute()`'s signature and all 4 call sites (Section D.11).

### D.11 — Every read path that must be flag-gated, and the dual-path hazard status

**Confirmed unified as of today.** `CutLabStructuralFindings.Compute()` is invoked from exactly 4
production call sites:
1. `CutLabPageService.cs:505`, inside `BuildFindingsAndRoundPlan`'s call at `CutLabCutRoundEngine.cs:338-347` — full page render.
2. `CutLabApiController.cs:100`, same `BuildFindingsAndRoundPlan` — AJAX `decide` endpoint.
3. `CutLabUiPatchBuilder.cs:80`, same `BuildFindingsAndRoundPlan` — the shared UI-patch builder consumed by decide/restart/what-if flows.
4. `CutLabSimulationService.cs:524-535`, standalone, curve-congestion-only, for round-3 delta ordering.

Sites 1-3 all funnel through the SAME `CutLabCutRoundEngine.BuildFindingsAndRoundPlan`
(`CutLabCutRoundEngine.cs:326-357`), which itself calls `CutLabStructuralFindings.Compute` once
(`:338-347`) — i.e. there is exactly ONE production entry point for finding computation across
page render and AJAX, not three independent ones. **This is a materially better starting position
than Phase 3's floor-resolution gap** (WARNING severity, `03-VERIFICATION.md:17,56-57,127`, since
fixed today per the ledger's Task 2). Gating the new twin detector therefore does not have the same
"three divergent floor-map builders" hazard Phase 3 had — as long as the gate is threaded as a
parameter into `Compute()` (or `BuildFindingsAndRoundPlan`) rather than duplicated inline at each of
the 3 call sites. Site 4 (`CutLabSimulationService`) is narrow-scoped by its own `.Where(finding =>
finding.Kind == CurveCongestion)` filter (`:532`) and is inert to a new kind unless the planner
explicitly wants twins to influence round-3 delta ordering too — worth an explicit plan decision,
not an oversight to silently inherit.

**Does the twins detector have the "ICutLabFloorResolver" dual-path hazard?** No — that hazard was
specific to `state.RoleFloors` being persisted as a USER-SET-ONLY subset
(`CutLabPageService.cs:313-320` per `03-VERIFICATION.md:127`) which two independent
`BuildFloorMap`/`StateRoleFloorResolver` re-implementations then rebuilt inconsistently. Finding
computation has no equivalent "persisted subset" step — `Compute()` always receives the FULL
current pool, freshly resolved, on every call. The dual-path risk category does not apply here
structurally; the actual risk for TWIN is the missing `TypeLine`/`IsLocked`/`IsCommander` fields
(Section C), which is a single shared defect (affects all 4 call sites identically), not a
divergence between them.

## E. Testing

### Q12 — Test locations, framework, conventions to mirror

- **Detector unit tests**: `DeckFlow.Web.Tests/CutLabStructuralFindingsTests.cs` (468 lines, xUnit,
  `[Fact]`). Convention: build `List<CutLabAnalyzedCard>` via a local `Card(...)` builder, call
  `CutLabStructuralFindings.Compute(pool, nearCombos, floors, comboDataAvailable, categoryDataAvailable)`
  directly (no DI, no mocking), assert on `result.Findings` (kind, heading, lead string, evidence
  names/order). See e.g. `Compute_CurveCongestion_ReportsBucketLeadAndEvidence`
  (`CutLabStructuralFindingsTests.cs:11-33`) for the bucket-and-evidence assertion shape a twins
  test would mirror. **Caveat**: since `CutLabAnalyzedCard` currently lacks `TypeLine`/
  `IsLocked`/`IsCommander` (Section C), a twins-detector test file will need either an updated
  `Card(...)` builder signature or a different input type, depending on how the planner resolves
  the Section C data-plumbing gap.
- **Engine/ranking test proving a finding changes proposal order**:
  `DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs` (616 lines). Exact mirrors:
  - `BuildQueue_TwoDiscriminatingFindings_PlacesCardInRound1` (`:12-37`) — feeds
    `CutLabCutRoundEngine.BuildQueue` a hand-built `Findings(...)` list (not going through
    `Compute()`), asserts a 2-discriminating-finding card lands in `Round1Key` with
    `FindingCount == 2`.
  - `BuildQueue_WholeRoleFindingsDoNotInflateDiscriminatingTally` (`:39-63`) — proves excluded
    kinds (`WeakFloorCase`, `RedundantFinishers`) do NOT push a card into round 1; the twins
    detector's own test should add a parallel case proving a NEW twins finding DOES push a card
    into round 1/2 the same way `CurveCongestion`/`EnablerStarved` already do in `:12-37`.
  - `BuildQueue_ExcludesLockedCommanderAndAcceptedCards` (`:88-143`) — the existing precedent for
    lock/commander exclusion, but note this tests `BuildQueue`'s `eligibleCards` filter (proposal
    queue), NOT finding computation — TWIN-04's group-membership exclusion is a different,
    currently-unbuilt guarantee (Section C.7).
- **Flag test pattern to mirror**: `DeckFlow.Web.Tests/CutLabCommanderFloorsFlagTests.cs` (374
  lines). Three test shapes, all worth replicating for a twins flag:
  1. `RenderAsync_CommanderFloorsFlagOff_OmitsCommanderColumnsAndMarker` (`:44-58`) — page-render
     off-path assertion.
  2. `RenderAsync_CommanderFloorsFlagOn_RendersCommanderColumnsAndMarker` (`:59-73`) — on-path.
  3. `ProcessAsync_CommanderFloorsFlagOff_IgnoresCommanderRoleFloorBaseline` (`:74-...`) — a
     **counting fake** (`FakeRoleFloorBaselineProvider`, `:356-...`, exposing `QueriedRoles`)
     asserted EMPTY when the flag is off (`Assert.Empty(roleFloorBaseline.QueriedRoles)`,
     `:126`). This is the discriminating-test pattern explicitly called out in the ledger as the
     fix for a prior test-quality finding (MEDIUM severity: tests that "would have passed with the
     entire gate deleted") — a twins-flag test suite should include an equivalent counting-fake
     test proving the twins detector genuinely does not run (not just that its output is filtered)
     when the flag is off.
- **AJAX regression pattern**: `DeckFlow.Web.Tests/CutLabAjaxFloorByRoleRegressionTests.cs` (814
  lines) — the template for "prove the AJAX path produces the same finding/ranking output as the
  page path," relevant if the planner wants an explicit parity test for the twins finding across
  transports (page vs. `/api/cut-lab/decide`).

### Q13 — Density validation against a live ~130-card pool

**No dedicated automated density-validation harness exists.** Two adjacent things were found, both
insufficient on their own:
- `CutLabEngineDeterminismTests.BuildTimingFacts` (`DeckFlow.Web.Tests/CutLabEngineDeterminismTests.cs:141-...`)
  builds a synthetic ~130-card-scale fixture, but it is homogeneous by construction (20 identical
  "Mana Rock NN" artifacts, 20 identical "Interaction NN" instants, 25 identical "Engine NN"
  enchantments, 24 identical "Payoff NN" creatures) — every synthetic group is ALREADY one giant
  twin cluster by design, so it cannot validate realistic density against a real, diverse pool. It
  also measures wall-clock timing (`TimingSpike_DefaultTrialAnalyze_...`, `:61-74`), not finding
  count/density.
- Playwright e2e specs under `DeckFlow.Web/e2e/cut-lab-*.spec.ts` use small (~20-card) synthetic
  pools (e.g. `oversizedPool` at `cut-lab-smoke.spec.ts:22-40`), not a real ~130-card decklist.
- The established precedent for "validate against a live pool" is a **manual** run: paste a real
  Archidekt/Moxfield URL or decklist into the running Cut Lab UI (`scripts/run-web-test.sh`/`.ps1`
  per `CLAUDE.md`'s testing constraint) and visually inspect, exactly as Phase 3's Task 4
  human-verify checkpoint did (`03-VERIFICATION.md`, referenced but not itself detailing an
  automated harness) and as today's flag-off render verification did (temporary
  `DeckFlow.Web/e2e/zz-flagoff-render.spec.ts`, deleted after use, per the ledger's Task 1 "RENDER
  VERIFICATION" section). **There is no committed, reusable "load a real 130-card pool and assert
  finding count" fixture** — Success Criterion 5 ("finding density on a real 130-card pool stays
  reviewable") will need either a new committed fixture (a real decklist checked into
  `DeckFlow.Web.Tests` or `DeckFlow.Web/e2e/`) or a manual/`checkpoint:human-verify` validation
  step, mirroring the Phase 3 precedent.

## Project Constraints (from CLAUDE.md)

- Codex writes implementation code; Claude plans/reviews (delegation rule) — not itself a technical
  constraint on the twins detector's design, but governs how Phase 4's plans will be executed.
- `.editorconfig`/changed-lines format gate applies to any new/changed C# lines
  (`CLAUDE.md` "Formatting" section) — the five carve-outs (get-only properties, inline attributes,
  raw-string literals, switch expressions, xmldoc indent, LF endings) apply to any new
  `CutLabFindingKind` member, detector method, or test file touched.
- Testing: xUnit is the required framework for `DeckFlow.Web.Tests`/`DeckFlow.Core.Tests`
  (`CLAUDE.md` "Test Framework by Project Type" — ".NET Core projects -> xUnit"), matching every
  existing Cut Lab test file cited above.
- No new NuGet packages without explicit user approval — nothing found in this research suggests a
  new package is needed; all required helpers (`CardTypeLine`, `CutLabCardNames`,
  `IFeatureFlagCache`) already exist in-repo.
- UI testing must never open a browser on the Windows host; use
  `scripts/run-web-test.sh`/`.ps1` + headless Playwright, matching the precedent used for both
  Phase 3's and today's flag-off render verification.
- Public repo (`luntc1972/DeckFlow`) — no secrets in commits; not directly relevant to this
  code-only phase.

## Unverified / open questions

1. **`CutLabCommanderNames.Resolve` definition site** (Q8) — cited only via the ledger's
   description and call-site line numbers (`CutLabApiController.cs:84/172/230/359`,
   `CutLabController.cs:280`); its own file/definition was not independently opened and read in
   this research pass. Low risk (well-described in the ledger and its behavior is a name-set
   derivation, not something a twins detector needs to change), but flagged per the provenance
   rule since it was not directly verified.
2. **`Program.cs:113` (`AddDeckFlowFeatureFlags()` call) and `Program.cs:183-184`
   (`CutLabPageService`/`ICutLabFloorResolver` registration order)** — cited from the ledger's own
   verification claims, not independently re-opened and read in this pass. The DI-ordering
   mechanics they describe (optional-ctor-param silently drops to null if a dependency is
   unregistered) are architecturally significant enough that the planner should independently
   re-verify `Program.cs` line numbers before relying on them, since Program.cs is a
   frequently-edited composition root and line numbers drift easily.
3. **MV-bucket boundary decision** — not unverified, but explicitly NOT resolved by this research
   (Section B.5): whether the twins detector reuses `CurveCongestion`'s `["0-1","2","3","4","5+"]`
   buckets (after extracting them to shared visibility) or defines new boundaries (e.g. exact MV
   equality) is a planning/CONTEXT decision this research deliberately does not make.
4. **Multi-role grouping semantics** — not unverified, but explicitly NOT resolved (Section B.4):
   whether a multi-role card contributes to multiple twin groups (once per matching role) or only
   its "primary" role is undecided; no existing Cut Lab detector groups by role AND type AND MV
   jointly, so there is no precedent to cite either way.
5. **Finding-density UI merging** — whether multiple qualifying twin groups should merge into one
   displayed section (like `WeakFloorCase`/`ComboProtected` already do via
   `CutLabFindingPresenter.BuildFindingGroups`) or render as separate blocks is unresolved by
   existing code and tied to Success Criterion 5's "stays reviewable" bar.

## Sources

### Primary (HIGH confidence — all read directly, this session)
- `DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs` (full file)
- `DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs` (lines 1-410)
- `DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs` (full file)
- `DeckFlow.Core/Manabase/CardTypeLine.cs` (full file) + `DeckFlow.Core.Tests/Manabase/CardTypeLineTests.cs` (theory cases)
- `DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs` (full file)
- `DeckFlow.Web/Services/CutLab/CutLabLockRules.cs`, `CutLabCardNames.cs`, `DeckFlow.Core/Normalization/CardNormalizer.cs` (full files)
- `DeckFlow.Web/Models/CutLabViewModel.cs` (lines 66, 310-370, 440-520, 640-680, 1090-1120)
- `DeckFlow.Web/Models/CutLab/CutLabFindingPresenter.cs` (full file)
- `DeckFlow.Web/Views/Deck/CutLab.cshtml` (lines 660-740)
- `DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs` (full file)
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (lines 1-60, 195-300)
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (lines 120-131)
- `DeckFlow.Web/Controllers/Admin/AdminFlagsController.cs` (lines 1-60)
- `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` (lines 100-320, 500-515)
- `DeckFlow.Web/Services/CutLab/CutLabFloorResolver.cs` (full file)
- `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` (lines 1-120)
- `DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs` (lines 480-538)
- `DeckFlow.Web.Tests/CutLabStructuralFindingsTests.cs` (lines 1-90)
- `DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs` (lines 1-145)
- `DeckFlow.Web.Tests/CutLabCommanderFloorsFlagTests.cs` (lines 40-130, grep sweep for class/method names)
- `DeckFlow.Core/Manabase/ManabaseModels.cs` (lines 160-182, `PlanRole` enum)
- `.planning/workstreams/cycle21-cut-lab/ROADMAP.md` (lines 1-70, 320-380)
- `.planning/workstreams/cycle21-cut-lab/REQUIREMENTS.md` (full file)
- `.planning/workstreams/cycle21-cut-lab/phases/03-commander-aware-floor-defaults/03-VERIFICATION.md` (grep sweep, lines 1-260 targeted reads)

### Secondary (MEDIUM confidence)
- `.foreman/ledger-cutlab-flag-ajax-2026-07-29.md` (full file, 224 lines) — today's flag+AJAX work
  narrative; commit SHAs and file/line claims within it were spot-verified against the actual code
  (e.g. seed-row line numbers, `IsFlagOn` body, `ICutLabFloorResolver` shape) but not every claim in
  the ledger was independently re-derived (see Unverified section).

### Tertiary (LOW confidence)
- None used without cross-verification.

## Metadata

**Confidence breakdown:**
- Finding-kind/tally mechanics (Section A): HIGH — every claim traced to specific lines, cross-
  checked against existing tests.
- Grouping dimensions (Section B): HIGH for role and type (tested helpers exist and were read
  directly); the MV-bucket gap is a verified absence (grep-confirmed no alternative helper exists),
  not a guess.
- Exclusions/composition (Section C): HIGH — the `CutLabAnalyzedCard` field gap was verified by
  reading the record definition and its only construction site.
- Feature flag (Section D): HIGH — the template flag's every citation was read directly, not taken
  solely from the ledger's narrative.
- Testing (Section E): MEDIUM-HIGH — file/line citations for test conventions are direct reads;
  the density-validation absence is a negative claim, checked via targeted grep across
  `DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`, and `scripts/`, not an exhaustive audit.

**Research date:** 2026-07-29
**Valid until:** 2026-08-12 (30 days; this is a fast-moving branch — Phase 3's AJAX/flag plumbing
changed twice in the single day before this research ran, so re-verify `Compute()` call sites and
the flag template files before planning if more than ~1-2 weeks elapse).
