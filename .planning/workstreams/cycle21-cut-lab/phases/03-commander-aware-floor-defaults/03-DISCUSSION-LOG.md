# Phase 3: Commander-Aware Floor Defaults - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in `03-CONTEXT.md` — this log preserves the alternatives considered.

**Date:** 2026-07-28
**Phase:** 03-commander-aware-floor-defaults (workstream `cycle21-cut-lab`)
**Areas discussed:** Floor statistic & the zero problem · Direction policy · Which arm feeds it + how the data ships · Side-by-side UI + overshoot-order reconciliation

**Note on targeting:** `gsd-sdk query init.phase-op 3` resolved `phase_dir` to the archived
`.planning/milestones/v1.0-phases/03-tech-debt-cleanup` (shipped 2026-05-02) even after the workstream
argument was supplied — the resolver's glob matched the older `03-*` directory while `roadmap_path`
correctly pointed at the workstream. This discussion deliberately targeted
`.planning/workstreams/cycle21-cut-lab/phases/03-commander-aware-floor-defaults/` instead.

---

## Which areas to discuss

| Option | Description | Selected |
|--------|-------------|----------|
| Floor statistic & the zero problem | p25 vs mean vs blend; what a p25 of 0 or 7.5 becomes | ✓ |
| Direction policy — may commander data LOWER a floor? | commander-wins vs max() vs raise-only | ✓ |
| Which arm feeds it + how the data ships | Postgres vs EDHREC arm; bundled snapshot vs live query | ✓ |
| Side-by-side UI + overshoot-order reconciliation | RFLR-08 columns; `LockedOvershootRoleOrder` criterion 5 | ✓ |

**User's choice:** All four.

---

## Floor statistic & the zero problem

### Q1 — Which statistic becomes the commander floor?

| Option | Description | Selected |
|--------|-------------|----------|
| p25 (Recommended) | ~75% of that commander's own decks already at or above it; already computed per commander per role | ✓ |
| Mean | Matches `ResolveLandsDefault`'s shipped `Math.Round(mean)`, but ~half a commander's decks fall below their own floor | |
| p25, floored at the bracket value | `max(bracket, p25)` — collapses this question into the direction-policy area | |

**User's choice:** p25, chosen **standalone** rather than pre-clamped, so the clamping question stayed separately decidable in the next area.
**Notes:** Data shown — engines clearing commanders (n=379): p25 min 0 / median 2 / max 11 against a bracket band of 4–6, versus mean median 4.67.

### Q2 — How does a fractional p25 become an integer floor?

| Option | Description | Selected |
|--------|-------------|----------|
| Floor / truncate down (Recommended) | 7.5 → 7; deterministic, never asserts more than the data proves | ✓ |
| `Math.Round`, matching `ResolveLandsDefault` | Inherits banker's rounding: 7.5 → 8 but 6.5 → 6 | |
| Round half away from zero | Predictable and symmetric, but rounds the floor up past the measured quartile | |

**Notes:** Surfaced during the question — the shipped lands precedent uses bare `Math.Round`, which is
`MidpointRounding.ToEven`. Copying it verbatim would make two adjacent commanders round in opposite
directions at the same `.5`. The precedent was deliberately not followed.

### Q3 — What happens when p25 is 0?

| Option | Description | Selected |
|--------|-------------|----------|
| Treat p25=0 as no signal → bracket fallback (Recommended) | Yields the byte-identical-to-today behavior RFLR-06 already requires | ✓ |
| Ship the 0 as measured | Most faithful to the data; guardrail becomes inert with no visible indication | |
| Clamp to 1 | Keeps a nominal guardrail, but 1 is invented and sits in a column labelled "commander" | |

**Notes:** 13 commander-role pairs affected among clearing commanders — engines 8, ramp 2, draw 2,
interaction-targeted 1. payoffs and wincons never drop below 2.

**Not asked — already locked by requirement:** RFLR-06 settles adoption gating (only `clearsBar` pairs
adopt), and `clearsBar` is per commander *per role*.

**Continue check:** Next area.

---

## Direction policy — may commander data LOWER a floor?

### Q1 — Which number drives the effective floor when commander p25 is below the bracket floor?

| Option | Description | Selected |
|--------|-------------|----------|
| max(bracket, commander) — commander may only raise (Recommended) | Guardrails never weakened; amends RFLR-05 from a chain to a max | ✓ |
| Commander always wins — literal RFLR-05 | Most faithful to the phase premise; drops payoffs 6 → ~2 at b4/b5 for all 124 | |
| Per-role direction policy | Most accurate to what was measured; needs a per-role table someone must own | |

**Notes:** The measured below-floor counts were decisive — payoffs at brackets 4 and 5 is **124 of 124**
adopting commanders below the band, with no exceptions, while interaction-targeted splits 136/136 and
still tightens under `max()`. Framing the user accepted: the bracket bands are prescriptive product
opinion (`[ASSUMED] ... awaiting product sign-off`, `CutLabFloorDefaults.cs:138`); p25 is descriptive.

### Q2 — What happens to the ramp/draw 24-slot coupling?

| Option | Description | Selected |
|--------|-------------|----------|
| Break the coupling, update the comment (Recommended) | Floors are minimums, not a budget | ✓ |
| Preserve 24 — renormalize after max() | Keeps the invariant, but displayed ≠ used | |
| Exclude ramp and draw this cycle | Smallest blast radius; drops 2 of the 6 GO roles | |

**Notes:** Renormalizing was rejected on the same principle that later ruled out clamping — the number
in the Commander column must be the number in use.

### Q3 — What about aggregate floor sums that no 100-card deck can satisfy?

| Option | Description | Selected |
|--------|-------------|----------|
| Detect and warn, never silently clamp (Recommended) | Matches Cut Lab's existing warn-before-breaking contract | ✓ |
| Cap the commander raise so the sum stays feasible | Guarantees a reachable target; displayed ≠ used again | |
| Leave it — out of scope | Small tail, no guard exists today either | |

**Notes:** Measured 3/841 at bracket 2, 16/841 at bracket 4, 23/841 at bracket 5 exceeding ~63 nonland
slots; worst case `The Watcher in the Water` at 78 vs today's 56. Stated as a caveat at the time, and
carried into CONTEXT.md as open question O-1: the arithmetic assumes mutually-exclusive role assignment.

**Continue check:** Next area.

---

## Which arm feeds it + how the data ships

### Q1 — Confirm the source arm

| Option | Description | Selected |
|--------|-------------|----------|
| Postgres arm only (Recommended) | The only arm carrying p25; singleton treatment correct for all six nonland roles | ✓ |
| Postgres for p25, EDHREC as a sanity bound | More defensible per value; needs an unspecified reconciliation rule | |
| EDHREC where available, Postgres elsewhere | Cannot deliver p25, so a third of the corpus would silently revert to a mean | |

**Notes:** Largely forced by Q1 of the first area — the EDHREC arm's 13,725 cells carry `count`/`deckCount`
only, no percentile, and its bracket coverage is uneven (`exhibition` NOT REPORTED at 1 qualifying cell,
`cedh` THIN at 40).

### Q2 — What ships in the bundled snapshot?

| Option | Description | Selected |
|--------|-------------|----------|
| 678 commanders, adopted floors only (Recommended) | 55.8 KB; every value in the file is a value the app uses | ✓ |
| All 841 with a `clearsBar` flag per role | More transparent; puts numbers in front of users the tool declined to trust | |
| 678 adopted, plus n and the bar it cleared | Enables hover provenance and future audit; more to keep in sync | |

**Notes:** Sizing computed live during the discussion — 678 of 841 commanders carry at least one adopted
floor after `clearsBar` and the `p25 > 0` rule; the trimmed file minifies to 55.8 KB against a 5.6 MB
research artifact and an existing 11 KB `cedh-land-baseline/latest.json`.

### Q3 — Who converts the research artifact into the shipped snapshot?

| Option | Description | Selected |
|--------|-------------|----------|
| New CLI converter + fail-closed drift guard (Recommended) | Mirrors `CedhBaselineCommandRunner`; decouples research from shipped data | ✓ |
| Research runner emits the snapshot as a second output | No drift between the two; couples a 27-minute run and a DB credential to every regeneration | |
| Hand-generate once; automate later | Smallest phase; no reproducible artifact→data path | |

**Notes:** Flagged before the question — `git merge-base --is-ancestor 1511dd95 HEAD` reports main is
**not** an ancestor of `gsd/cycle21-cut-lab`, so `CedhBaselineDriftCheck` and the cEDH fail-closed gates
do not exist in this worktree. Carried into CONTEXT.md as open question O-2.

### Q4 — How does commander lookup resolve against snapshot keys?

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse the cEDH `CandidateKeys` shape; partner decks fall back to bracket (Recommended) | One matching rule for both baselines; records the gap rather than papering over it | ✓ |
| Also try each partner separately, take the lower | Extends coverage; attributes one commander's build pattern to a two-commander deck | |
| Normalize aggressively (case-insensitive, DFC front-face) | Matches how the harness grouped; risks matching rows the corpus never keyed that way | |

**Notes:** Measured — zero partner-pair keys in the corpus, 50 DFC keys in full `A // B` form.

**Continue check:** Next area.

---

## Side-by-side UI + overshoot-order reconciliation

### Q1 — How do both numbers get onto the table?

| Option | Description | Selected |
|--------|-------------|----------|
| Two new labelled columns: Bracket \| Commander (Recommended) | RFLR-08's literal ask; each number independently headed | ✓ |
| One "Derived from" column with both inline | Narrower; the two values share a header | |
| Keep four columns; fold into the Source cell | Zero layout change; weakest against RFLR-08 | |

**Notes:** The user selected the option with the concrete mock attached; that mock is reproduced verbatim
in CONTEXT.md `<specifics>` and should be matched literally.

### Q2 — Does the UI distinguish the different reasons a Commander cell is empty?

| Option | Description | Selected |
|--------|-------------|----------|
| Two states: not-applicable vs no-data (Recommended) | `n/a` for out-of-scope roles; empty marker for GO roles with no match | ✓ |
| One marker for all three | Simplest; implies the tool looked for lands data when lands was deliberately pulled | |
| Three states, requiring the 841-with-flags file | Most informative; reopens the snapshot decision just made | |

**Notes:** The two-state answer is the honest one given Q2 of the previous area — the 678-adopted-only
snapshot genuinely cannot separate "commander absent" from "role did not clear" at runtime.

### Q3 — How is `LockedOvershootRoleOrder` reconciled?

| Option | Description | Selected |
|--------|-------------|----------|
| Sort by headroom, keep the fixed order as tiebreak (Recommended) | Reconciles rather than justifying; preserves the stability the comment requires | ✓ |
| Keep it fixed; record why | Cheapest; criterion 5 permits it, but leaves the contradiction shipping | |
| Headroom only; delete the fixed array | Fully commander-aware; order churns between rounds, which the comment forbids | |

**Notes:** The contradiction was made concrete before asking — the array puts wincons first as
"least structural", but wincons carries the smallest floor in the table and therefore usually the least
slack, so the advisory points at the role most likely to break its own floor on the next cut. `max()`
sharpens this because floors only rise.

**Continue check:** Wrap up.

---

## Claude's Discretion

Raised during discussion and explicitly left to planning/implementation:

- Source column wording now that Bracket and Commander are separate columns, and its coexistence with the `Adjusted` badge and Reset button.
- Reset-to-default target — `data-cut-lab-floor-default` must now carry the `max`, not the bracket value, in both Razor and `cut-lab.ts`.
- Theme handling for two extra columns across the 24 guild themes (layout CSS in `site-common.css`).
- Whether the infeasibility advisory is a new `CutLabFindingKind` or a panel-level notice.

## Deferred Ideas

None raised as scope creep during this discussion. The deferred list in CONTEXT.md is carried forward
from Phase 2's open gaps and follow-up recommendations, not generated here:

- Re-measure lands properly (three options in `02-08-SUMMARY.md`).
- Protection floors — blocked on Phase 01.2's vocabulary widening.
- Fix harness commit-SHA detection for WSL worktrees.
- Gitignore decision for the research caches; 19 MB generated artifacts in a public repo.
- Dead `normalizeForScryfall` parameter after plan 02-10.
- Bracket-aware commander floors — deliberately out of scope this cycle.
