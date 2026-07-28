# Phase 3: Commander-Aware Floor Defaults - Context

**Gathered:** 2026-07-28
**Status:** Ready for planning
**Workstream:** `cycle21-cut-lab`

<domain>
## Phase Boundary

For the six roles Phase 2 returned GO on — **ramp, draw, interaction-targeted, engines, payoffs, wincons** — Cut Lab's role-floor default is raised by that commander's own corpus data when the commander cleared Phase 2's statistical bar for that role, and the role-floor table shows the bracket-derived number and the commander-derived number side by side. Every commander and role without qualifying signal keeps today's bracket+plan floor byte-for-byte.

**Locked upstream, not reopened here:**
- **lands is PULLED.** The Postgres arm measures distinct land *names*, not land count; colour count explains 54% of its variance (r = 0.734). See `02-08-SUMMARY.md`.
- **protection and interaction-mass are out of scope.** Signal present, breadth insufficient. `DeckStatClassifier.IsProtectionCard` under-detects in both directions; widening it is deferred Phase 01.2.
- **Commander floors are bracket-agnostic this cycle** (user decision 2026-07-26, restated in the Phase 5 roadmap block).
- **Only `clearsBar` commander-role pairs adopt a commander floor** (RFLR-06). `clearsBar` is per commander *per role*, so one commander can be commander-driven for engines and bracket-driven for ramp in the same table.

</domain>

<decisions>
## Implementation Decisions

### Floor statistic

- **D-01: The commander floor is p25, not the mean.** Roughly 75% of that commander's own decks already sit at or above p25, which is what a floor asserts. A mean-derived floor would put about half of a commander's decks below their own floor. `p25` is already computed per commander per role in `RESEARCH-FINDINGS.json`.
  - Deliberately chosen **standalone**, not pre-clamped to the bracket value — clamping is D-04's job, so the two decisions stay independently revisable.

- **D-02: Fractional p25 truncates down (`Math.Floor`), it does not round.** p25 is interpolated, so values like `7.5` occur, while `CutLabResolvedFloor.Floor` is an `int`.
  - **Do NOT copy `ResolveLandsDefault`'s bare `Math.Round(mean)`.** .NET's default is banker's rounding (`MidpointRounding.ToEven`): `7.5 → 8` but `6.5 → 6`, so two commanders at the same `.5` would round in opposite directions. Truncation is deterministic and never asserts more than the data proves.

- **D-03: `p25 = 0` is treated as no signal and falls back to the bracket value.** A floor of 0 can never be violated, so it silently disables the guardrail while the UI still shows a confident commander-derived number.
  - Measured: 13 commander-role pairs among clearing commanders sit at p25 = 0 — engines 8, ramp 2, draw 2, interaction-targeted 1. payoffs and wincons never drop below 2.
  - This produces exactly the byte-identical-to-today behavior RFLR-06 already requires for no-data commanders, and RFLR-08's empty marker then renders honestly.

### Direction policy

- **D-04: The effective default is `max(bracket-derived, commander-derived)`. Commander data may only RAISE a floor, never lower one.**
  - **This AMENDS RFLR-05.** The requirement's wording — "commander-specific corpus data → existing bracket+plan fallback" — describes a priority chain. The implemented rule is a max. Planning must carry this amendment into REQUIREMENTS.md rather than implementing the literal text.
  - Measured driver — adopting commanders whose p25 falls *below* today's floor:

    | Role | b2 | b3 | b4 | b5 | of |
    |---|---:|---:|---:|---:|---:|
    | interaction-targeted | 82 | 136 | 136 | 136 | 272 |
    | engines | 208 | 220 | 278 | 278 | 367 |
    | payoffs | 105 | 121 | **124** | **124** | 124 |
    | wincons | 0 | 0 | 92 | 92 | 153 |

    At brackets 4–5, **all 124 of 124** adopting payoffs commanders fall below the band (p25 median 2 vs 6) — no exceptions. A literal priority chain would delete the payoffs guardrail outright and gut engines. interaction-targeted meanwhile splits 136 below / 136 above and still tightens under `max()`.
  - Framing that settled it: the bracket bands are **prescriptive** product opinion — `CutLabFloorDefaults.cs:138` marks them `[ASSUMED] ... awaiting product sign-off` — while p25 is **descriptive** of what people actually build. Different quantities, which is why RFLR-08 wants both on screen.

- **D-05: The ramp/draw 24-slot coupling is broken.** Today `drawDefault = 24 - rampDefault`, with the comment *"Mirror `ManabaseRampDrawBudgetCalculator`'s fixed 24-slot split: draw gets whatever ramp does not."* Ramp and draw now each resolve `max()` independently and the pair may sum past 24.
  - Justification: floors are minimums, not a budget. Two minimums summing past 24 is a real statement about that commander, not an arithmetic error.
  - **The stale comment must be corrected, not left in place.**
  - Renormalizing after `max()` was rejected on the same grounds as clamping — the number shown in the Commander column must be the number in use, or RFLR-08's side-by-side premise collapses.

- **D-06: Infeasible aggregate floor sums are detected and warned, never silently clamped.** Because `max()` only raises and nothing pushes back, some commanders end up with a floor sum no 100-card deck can satisfy.
  - Measured, **assuming mutually-exclusive role assignment (see open question O-1)**: commanders whose resolved nonland floor sum exceeds ~63 slots = 3/841 at bracket 2, 16/841 at bracket 4, 23/841 at bracket 5. Worst case `The Watcher in the Water` at **78** against today's 56.
  - **Cut Lab has no aggregate feasibility guard today** — every `Sum(` in `DeckFlow.Web/Services/CutLab/` counts cards, never floors.
  - The advisory must name which floors to relax, matching Cut Lab's existing "warn before breaking a floor, never silently" contract.

### Source arm and data shipping

- **D-07: Commander floors come from the Postgres arm only.** The EDHREC arm serves averages only — its 13,725 cells carry `count`/`deckCount`, no percentile — so it structurally cannot supply p25. Its bracket coverage is also uneven (`exhibition` NOT REPORTED at 1 qualifying cell, `cedh` THIN at 40). `02-08-SUMMARY.md` establishes that singleton-set treatment is correct for all six nonland GO roles — the flaw was lands-specific — so the Postgres measurement is sound here. EDHREC stays as corroborating context in the findings, not as a floor source.

- **D-08: The bundled snapshot carries 678 commanders and adopted floors only.** Only commander-role pairs that cleared the bar *and* survived D-03's `p25 > 0` rule. Minifies to **55.8 KB** (research artifact is 5.6 MB; existing `cedh-land-baseline/latest.json` is 11 KB).
  - Every value in the file is a value the app uses, so the file itself is the contract — nothing in it needs a "do not use this one" flag, and RFLR-08's empty marker follows from absence.
  - **Consequence to honour in the UI:** at runtime this snapshot cannot distinguish "commander absent from corpus" from "commander present, role did not clear". D-11 must not pretend otherwise.

- **D-09: A new `DeckFlow.CLI` converter produces the snapshot, guarded by a fail-closed drift check.** Mirrors `DeckFlow.CLI/CedhBaselineCommandRunner.cs`: reads `RESEARCH-FINDINGS.json`, emits `DeckFlow.Web/Data/role-floor-baseline/latest.json`, and **refuses to write** when the new snapshot diverges from the committed one beyond a threshold.
  - Keeps the research artifact and the shipped artifact decoupled, and inherits the lesson the cEDH baseline incident already paid for.
  - **DEPENDENCY (see O-2):** `git merge-base --is-ancestor 1511dd95 HEAD` reports **main is not an ancestor** of `gsd/cycle21-cut-lab`. `CedhBaselineDriftCheck` and the three fail-closed gates do not exist in this worktree. Rebasing onto main is a precondition for copying the guard pattern.
  - The runtime provider mirrors `CedhLandBaselineProvider`: bundled JSON under `Data/`, `IMemoryCache` with a 24h entry, **fail-open** — a missing or corrupt file degrades to "no commander data", never an error.

- **D-10: Commander lookup reuses `CedhLandBaselineProvider.CandidateKeys`' shape** — solo name, then both partner orders (`"A / B"` / `"B / A"`), never splitting a DFC's `" // "` form.
  - Measured: the corpus contains **zero partner-pair keys** and 50 DFC keys in full `A // B` form.
  - Partner and Background decks therefore find nothing and take the bracket floor — correct under RFLR-06. **Record the partner gap explicitly; do not paper over it** by attributing a solo commander's build pattern to a two-commander deck.

### Role-floor UI

- **D-11: The table gains two labelled columns — `Bracket` and `Commander`.** New shape: `Role | In pool | Bracket | Commander | Floor | Source`.
  - This is RFLR-08's literal ask: both numbers, clearly labelled, commander shown at every bracket. Each number gets its own header so neither is ambiguous.
  - Existing cells all carry `data-label` for the stacked mobile layout; new columns must do the same.

    ```
    Role      In pool  Bracket  Commander  Floor  Source
    Engines    5        6         9        [9]   Commander
    Payoffs    3        6         —        [6]   Bracket
    Ramp       9       12        14       [14]   Commander
    Wincons    2        3         —        [3]   Bracket
    ```

- **D-12: Two empty-cell states, not one and not three.**
  - `n/a` — for **lands, interaction-mass, protection**: the column is structurally never populated, because Phase 2 put them out of scope. A bare dash here would imply the tool looked and found nothing, when lands was deliberately pulled at the Phase 2 checkpoint.
  - empty marker — for a **GO role with no match**. Per D-08 the snapshot genuinely cannot separate "commander absent" from "role did not clear", so the UI must not claim to.

- **D-13: `LockedOvershootRoleOrder` is reconciled, not merely justified.** Primary sort becomes headroom — `(in-pool count − effective floor)` descending — with the existing least-to-most-structural array retained as the **deterministic tiebreak** when headroom ties.
  - This satisfies roadmap success criterion 5 by reconciling rather than by writing a rationale for a known contradiction.
  - The contradiction being fixed: the array puts **wincons first** ("cut here first if you must unlock something") on a least-structural theory, but wincons carries the smallest floor in the table (band 2–3, commander p25 median 2) and so usually has the *least* slack. The advisory points at the role most likely to break its own floor on the next cut. `max()` sharpens this, since floors only rise.
  - Retaining the fixed array as tiebreak preserves the stability its own comment says the advisory must have.

### Claude's Discretion

These were surfaced during discussion and left to the planner/implementer:

- **Source column wording** now that Bracket and Commander are their own columns — whether `SourceLabel` becomes "Commander" / "Bracket", and how it coexists with the `Adjusted` badge and the Reset button already in that cell.
- **Reset-to-default target.** `data-cut-lab-floor-default` currently carries `DefaultValue`. Under D-04 the effective default is the `max`, so reset should restore the max — but this must be made explicit in both the Razor attribute and `cut-lab.ts`.
- **Theme handling for two extra columns** across the 24 guild themes; layout CSS belongs in `site-common.css`, never `site.css`.
- Whether the D-06 infeasibility advisory is a new `CutLabFindingKind` or a panel-level notice.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 2 findings — the data this phase consumes
- `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/02-08-SUMMARY.md` — **the authority on the go/no-go disposition.** GO on six roles; lands PULLED with the measured colour-count evidence; the open gaps list (degraded provenance, provisional protection, uneven EDHREC bracket support, corpus hygiene).
- `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.json` — **the authority on what was computed.** Per-commander `roles.{role}.{mean,p25,ratio,z,cohensD,clearsBar}` plus `corpusBaseline`, `goNoGo`, `methodology`. Still lists `lands` in `rolesInScopeForPhase3`; `02-08-SUMMARY.md` overrides that and the artifact must not be hand-edited.
- `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.md` — same run, human-readable, with per-row source attribution.
- `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/EDHREC-ROLE-GRID.json` — the EDHREC arm. Context only per D-07; not a floor source.

### Phase scope and requirements
- `.planning/workstreams/cycle21-cut-lab/ROADMAP.md` §"Phase 3: Commander-Aware Floor Defaults" — goal, five success criteria, and the p25-vs-mean open decision this discussion resolved.
- `.planning/workstreams/cycle21-cut-lab/REQUIREMENTS.md` — RFLR-05 through RFLR-08. **RFLR-05 is amended by D-04** (fallback chain → max).

### Code this phase changes or mirrors
- `DeckFlow.Web/Services/CutLab/CutLabFloorDefaults.cs` — `ResolveDefaults` and `ResolveLandsDefault` (the priority-chain template), `GetBracketBand` (the `[ASSUMED]` bands at line 138), and the `CutLabResolvedFloor` record that must grow to carry both numbers.
- `DeckFlow.Web/Services/Manabase/CedhLandBaselineProvider.cs` — the provider pattern to mirror: bundled JSON, `IMemoryCache`, fail-open load, `CandidateKeys` matching.
- `DeckFlow.CLI/CedhBaselineCommandRunner.cs` — the generator pattern to mirror for D-09.
- `DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs:123` — `LockedOvershootRoleOrder` and its consumer at line 442.
- `DeckFlow.Web/Views/Deck/CutLab.cshtml` §role-floors table (~line 776) — the four-column table becoming six.
- `DeckFlow.Web/wwwroot/ts/cut-lab.ts` — floor input, reset, and adjusted-badge wiring.
- `DeckFlow.Web/Models/CutLabViewModel.cs` — `FloorRows` shape.
- `DeckFlow.Web/Data/cedh-land-baseline/latest.json` — the shipped-snapshot precedent (11 KB).

### Project standards
- `CLAUDE.md` §Constraints — LF line endings, `.editorconfig` changed-lines gate, theme CSS in `site-common.css`, the five formatter carve-outs (notably: never convert `{ get; init; }` to `{ get; }` — System.Text.Json silently skips get-only properties, which has broken snapshot deserialization before).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`CedhLandBaselineProvider`** — near-complete template for the new commander role-floor provider: `IWebHostEnvironment`-rooted `Data/` path, 24h `IMemoryCache` entry, fail-open catch on `IOException`/`UnauthorizedAccessException`/`JsonException`, log-once-on-failure, internal test-seam ctor taking an explicit path, and `CandidateKeys` partner/DFC matching that D-10 adopts wholesale.
- **`CutLabFloorDefaults.ResolveLandsDefault`** — the shipped priority-chain shape (commander lookup → bracket baseline row → constant fallback). Structure reused; **its `Math.Round` is explicitly not reused** (D-02).
- **`CedhBaselineCommandRunner`** — CLI generator shape for D-09: `--data`/`--out` args, snapshot build, markdown summary emission.
- **`CutLabResolvedFloor`** record — already carries `DefaultValue`, `ResolvedBracket`, `BracketWasFallback` provenance fields; extends naturally to carry the bracket and commander values separately.

### Established Patterns
- **Bundled data under `DeckFlow.Web/Data/<name>/latest.json`**, copied to output, read via content root. Both existing baselines follow it.
- **Fail-open on missing bundled data** — a snapshot that fails to load must degrade to "no commander data", never throw. This is what makes RFLR-06's byte-identical guarantee hold in the degraded case for free.
- **Role floors are resolved once in `ResolveDefaults`** and flow to the view via `CutLabViewModel.FloorRows`; consumers are `CutLabPageService`, `CutLabUiPatchBuilder`, and the Razor table.
- **`data-label` on every table cell** drives the stacked mobile layout — new columns must carry it.

### Integration Points
- `ResolveDefaults` signature gains a commander role-floor provider alongside the existing `IManabaseBaselineProvider` / `ICedhLandBaselineProvider`; DI registration lands in `Program.cs` beside the other baseline providers.
- `CutLabCutRoundEngine`'s overshoot ranking (line 442) needs the *effective* floor and in-pool count to compute headroom for D-13 — check whether it currently has both in scope.
- `CutLabUiPatchBuilder` emits server-authored UI patches for Cut Lab (Phase 108 contract); the two new columns must be represented there, not only in the initial Razor render.

</code_context>

<specifics>
## Specific Ideas

- The table mock the user selected, to be matched literally:

  ```
  Role      In pool  Bracket  Commander  Floor  Source
  Engines    5        6         9        [9]   Commander
  Payoffs    3        6         —        [6]   Bracket
  Ramp       9       12        14       [14]   Commander
  Wincons    2        3         —        [3]   Bracket
  ```

- The framing the user accepted for D-04, worth preserving in the code comment: bracket bands are **prescriptive** product opinion; commander p25 is **descriptive** of what people build. `max()` is the reconciliation, and both numbers stay visible precisely because they answer different questions.

- Snapshot row shape validated during discussion (55.8 KB minified over 678 commanders):

  ```json
  {"Fire Lord Azula": {"n": 644, "floors": {"ramp": 9, "draw": 8, "interaction-targeted": 20, "engines": 2}}}
  ```

</specifics>

<open_questions>
## Open Questions for Research

- **O-1: Is Cut Lab role assignment mutually exclusive, or can one card carry multiple roles?** D-06's infeasibility arithmetic (78 vs ~63 slots, 23/841 at bracket 5) assumes exclusivity. If roles overlap, the real constraint is looser and the advisory's threshold changes. Resolve before sizing D-06.
- **O-2: Sequencing of the rebase onto main (`1511dd95`).** D-09's fail-closed drift guard copies `CedhBaselineDriftCheck`, which does not exist in this worktree. Planning must decide whether the rebase precedes Phase 3 implementation or whether the guard is written from scratch here and reconciled later.
- **O-3: Does `CutLabCutRoundEngine`'s overshoot ranking have in-pool counts in scope** at the point it orders roles? D-13 needs count and effective floor together.

</open_questions>

<deferred>
## Deferred Ideas

- **Re-measure lands properly.** `02-08-SUMMARY.md` lists three options (carry quantities in the Postgres arm, restrict lands to nonbasics, or take land floors from the EDHREC arm via the shipped `EdhrecAveragesConverter` → `ManabaseBaselineSnapshot` path). Out of scope — lands was pulled deliberately.
- **Protection floors** — blocked on Phase 01.2's vocabulary widening; `IsProtectionCard` has three consumers, so it is not a one-line fix.
- **Fix harness commit-SHA detection for WSL worktrees** — `DescribeHarnessCommitSha` shells Windows `git rev-parse` against a `.git` pointer holding a `/mnt/c` path. Phase 2 gap 1.
- **Gitignore decision for `_role-floor-research/` and `_edhrec-brackets/` caches**, and the 19 MB generated-artifact-in-a-public-repo question. Both carried forward from Phase 2 D-05; both need explicit permission per CLAUDE.md's "Do Not Modify" list.
- **Dead `normalizeForScryfall` parameter** in `ScryfallReferenceResolver.ResolveBatchAsync` after plan 02-10; four production callers still pass it.
- **Bracket-aware commander floors** — deliberately out of scope this cycle. Phase 5 (Archidekt Bracket Capture) is non-gating, and the per-cell arithmetic shows Archidekt can never fill a bracket cell against EDHREC's 400-deck floor.

</deferred>

---

*Phase: 03-commander-aware-floor-defaults*
*Context gathered: 2026-07-28*
