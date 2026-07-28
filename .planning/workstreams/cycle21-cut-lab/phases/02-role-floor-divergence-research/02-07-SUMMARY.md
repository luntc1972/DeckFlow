---
phase: 02-role-floor-divergence-research
plan: 07
wave: 6
status: complete
executed: 2026-07-28
commits:
  - 52f18df5  # feat(02-07): emit the lands calibration verdict against the 2026-07-16 prior
  - 916e3b9c  # fix(02-07): compare the three land reference sets over a common bracket set
  - a6924006  # feat(02-07): name Phase 3 roles, support a null result, and disclose protection under-detection
  - 3aeea130  # docs(02-07): pre-write the no-go findings template
  - 8d77d2dd  # test(02-07): verify boardFilter mainboard excludes sideboard and maybeboard rows
  - e1b5ca5c  # docs(02-07): use repo-relative paths in the no-go template
  - 53ae0dc0  # refactor(02-07): compute the casual-bias ratio once
  - 09f552c3  # docs(02-07): correct the measured role list in the no-go template
  - 6b82fead  # fix(02-07): stop AppendBlock emitting doubled carriage returns
  - 0d322354  # test(02-07): verify the mainboard filter on a single oversized deck
gates:
  build: "0 errors; 0 warnings"
  core_tests: "1740 passed / 0 failed"
  web_tests: "2095 passed / 16 skipped / 0 failed, after rerunning FeedbackStoreTests.ListAsync_FiltersByType by --filter once"
  eol: "existing file EOLs preserved; new summary is LF"
verification: "blind verification first returned four actionable findings; all four are closed in this worktree"
---

# Phase 2 Plan 07 — Summary

The phase shipped in more than the four planned commits because review found two Task-1 defects, and
the blind verification pass found four actionable follow-ups after the original wave landed. The final
state keeps the phase's original behavior changes, closes the verifier's three code/template findings,
and records the missing summary without widening scope into classifier, repository, harvester, or
Phase-5 work.

## 1. Lands calibration: exact verdict strings and the rule

The harness can emit exactly three lands-calibration verdict strings:

| Harness state | Emitted verdict |
|---|---|
| `no-go` | `reproduces` |
| `signal-present` | `insufficient data` |
| `go` | `contradicts` |

That mapping is deliberate and is the core D-06 protection against overstating a near-miss:

- `signal-present` maps to **`insufficient data`**, never to `contradicts`.
- `contradicts` is gated on **`BreadthMinimum = 3`**, by name, not by a retyped literal.
- The emitted insufficient-breadth text names the same constant and value: *"BreadthMinimum = 3
  distinct qualifying commanders clearing the bar."*
- The other insufficient-data route remains the qualifying-coverage route: too few qualifying
  commanders for `lands`, or a zero corpus baseline.

### Prior constants and archive lines

All prior constants were transcribed from
`.planning/archive/2026-cycles/research/2026-07-16-edhrec-bracket-land-data.md` itself:

| Constant | Value | Archive line |
|---|---:|---:|
| `PriorLandBracketMeans` | B1 `36.3`, B2 `35.7`, B3 `35.2`, B4 `34.5`, B5 `30.0` | 21 |
| `PriorLandBracketStdDevs` | B1 `2.24`, B2 `1.19`, B3 `1.25`, B4 `1.20`, B5 `2.14` | 22 |
| `PriorLandOverallMean` | `34.9` | 21 |
| `PriorLandOverallStdDev` | `1.45` | 22 |
| B1 outlier caveat carried into the B1 comments | Teval B1 `42` is a tiny-sample outlier | 24 |

### The three reference sets and the shipped output shape

The lands comparison now records three EDHREC-derived reference sets separately:

1. The 2026-07-16 archive prior above.
2. The live shipped baseline at `DeckFlow.Web/Data/manabase-baseline/latest.json`:
   B2 `35.9`, B3 `35.5`, B4 `34.5`, B5 `30.5`, generated `2026-07-17T21:38:00Z`
   ([latest.json](../../../../../../../DeckFlow.Web/Data/manabase-baseline/latest.json):3-9).
   There is **no B1 row** in that snapshot.
3. The freshly measured qualifying-cell means from the 2026-07-27 EDHREC bracket corpus.

The markdown emits the agreement line unconditionally in this exact shape:

`**Do the three reference sets agree?** Maximum absolute spread across prior / live / fresh = {spread} lands{divergent clause}.`

Then:

- if no bracket carries all three sets, it says so plainly and omits the closest-set claim;
- otherwise it states which of prior / live / fresh this run's Postgres mean sits closest to, over
  the brackets where all three are present;
- in both cases it states that agreement among the three EDHREC-derived baselines says nothing about
  this run's Postgres P25 result, which measures a quantity none of them can measure.

### Authorized deviation

Task 1's criterion asked for **four** source-labelled column groups in the comparison table. The
shipped markdown intentionally has **three** source-labelled groups, with the Postgres figure stated
once beneath the table as "not bracket-resolved; the Postgres corpus has no bracket dimension." This
was a **reviewer-directed** correction under D-08: repeating one corpus-wide Postgres figure down
five bracket rows implied a bracket resolution the Postgres corpus does not have. No cell mixes
sources, so criterion-8 discipline still holds.

### Review defect fixed in `916e3b9c`

Review found two Task-1 defects and both were fixed together in `916e3b9c`:

- the closest-reference-set arithmetic originally compared asymmetric bracket sets;
- the Postgres figure was originally repeated inside the bracket table.

The fix aligned the comparison on a common bracket set and removed the repeated Postgres column.

## 2. Protection disclosure: unconditional, graded, and classifier-untouched

`DeckStatClassifier.cs` was **not modified** in this phase or in the blind-verification cleanup wave.
The protection under-detection disclosure is unconditional on every code path and appears in two
markdown placements:

1. a one-line pointer in the "Known gaps" list;
2. the full `### Protection under-detection disclosure` block inside `## Go/No-Go`, adjacent to the
   role decisions.

The five known missed cards are now recorded with evidence grades, not flattened into an undifferentiated list:

| Card | Evidence grade | Source |
|---|---|---|
| `Swiftfoot Boots` | measured | `01.1-02-DELTA.md:45` |
| `Mother of Runes` | measured | `01.1-02-DELTA.md:45` |
| `Hexing Squelcher` | measured | `01.1-02-DELTA.md:46` |
| `Goblin Chirurgeon` | measured | `01.1-02-DELTA.md:46` |
| `Lightning Greaves` | inferred, not measured | `01.1-02-DELTA.md:47` |

The key line establishing Lightning Greaves as inferred rather than measured is:

- `01.1-02-DELTA.md:47` — it used the plan-provided oracle text and a reasoned `Artifact — Equipment`
  type line because no local facts entry for that card existed in the measured corpus files.

The disclosure still states the consequence in the plan's required terms:

- the `protection` role's measured floors are a **LOWER BOUND**;
- any protection go/no-go verdict is **PROVISIONAL pending Phase 01.2**.

## 3. Casual-bias engagement and corpus hygiene stayed unconditional

The `### The casual-bias objection` block is emitted unconditionally, immediately after the role-scope
lines in `## Go/No-Go`, and it does **not** contain a rebuttal. It records:

- the archived casual-dominance conclusion and its path;
- the two existing code paths that already act on it;
- the stax-versus-swarm objection as it applies to this phase's premise;
- what this run's own numbers do and do not say about within-commander lower-tail spread.

The corpus-hygiene disclosure is likewise unconditional and remained a stated limitation, not a gate.
The emitted figures are:

- corpus scale: `397,063` decks, `151,202` processed, `4,003` commanders with processed decks;
- depth: `847` commanders at `>=40`, `346` at `>=100`, `88` at `>=250`, `17` at `>=500`, deepest `917`;
- random sample `n=300`: `286/287` live decks are `deckFormat` 3, `13/300 (4.3%)` deck ids are dead,
  `7/287 (2.4%)` live decks are `theorycrafted`;
- created-year spread from the live sample: `213x 2026`, `62x 2025`, `10x 2024`, `1x 2023`,
  `1x 2021`;
- there is no recency window in the stored Archidekt corpus;
- commander-ness is still inferred from a card categorized `Commander`, not from `deckFormat`;
- Phase 5 remains independent and non-gating because `917 x 25% / 5 ≈ 46` decks per cell cannot
  satisfy the EDHREC-side `>=400` cell floor.

No filter, purge, harvester change, or new Phase-5 dependency was added. D-08 records the limitation
and leaves harvesting behavior alone.

## 4. `boardFilter: "mainboard"` verification

The repository query remained unchanged and still binds the filter against `o.board` in
`CardCategoryRepository.cs`; there was no repository fix to make here.

The verification now covers both shapes:

| Test shape | Unfiltered result | `boardFilter: "mainboard"` result | Result |
|---|---:|---:|---|
| 220 separate one-card decks | 220 rows | 100 rows | PASS |
| 1 oversized deck with 220 cards across boards | 220 rows | 100 rows | PASS |

Both tests also assert the absence of a named sideboard card and a named maybeboard card from the
filtered result:

- existing multi-deck test: `Sideboard Card 101` and `Maybeboard Card 161`;
- new single-deck test: `Oversized Sideboard Card 101` and `Oversized Maybeboard Card 161`.

So the D-08 oversized single-deck hazard did **not** reproduce. `CardCategoryRepository.cs` stayed
unmodified.

## 5. Blind-verifier findings and disposition

The blind verification pass returned four actionable findings:

1. The no-go template still named stale pre-Phase-1 roles.
   Fixed in `09f552c3` by replacing the list with `lands, ramp, draw, interaction-targeted,
   interaction-mass, protection, engines, payoffs, wincons`.
2. `AppendBlock` emitted doubled carriage returns, and the protection pointer stranded a `\r`.
   Fixed in `6b82fead`, with regression coverage added to `DeckFlow.Core.Tests/RoleFloorTaxonomyGuardTests.cs`.
3. The existing `boardFilter` test covered 220 one-card decks, not one oversized multi-board deck.
   Fixed in `0d322354`; the single-deck shape passed with the same `220` / `100` split.
4. This summary file did not exist.
   Closed here by creating `02-07-SUMMARY.md`.

## 6. Commit history for `a38c0133..HEAD`

Every commit in the landed range:

- `52f18df5 feat(02-07): emit the lands calibration verdict against the 2026-07-16 prior`
- `916e3b9c fix(02-07): compare the three land reference sets over a common bracket set`
  Non-task reason: reviewer-found Task-1 correction for asymmetric bracket comparison and the repeated
  Postgres column.
- `a6924006 feat(02-07): name Phase 3 roles, support a null result, and disclose protection under-detection`
- `3aeea130 docs(02-07): pre-write the no-go findings template`
- `8d77d2dd test(02-07): verify boardFilter mainboard excludes sideboard and maybeboard rows`
- `e1b5ca5c docs(02-07): use repo-relative paths in the no-go template`
  Non-task reason: reviewer cleanup so the negative-path template points at repo-local evidence rather
  than unstable absolute paths.
- `53ae0dc0 refactor(02-07): compute the casual-bias ratio once`
  Non-task reason: mechanical cleanup to compute the same statistic once and reuse it instead of
  rederiving it in multiple emitters.
- `09f552c3 docs(02-07): correct the measured role list in the no-go template`
  Non-task reason: blind-verifier finding 1.
- `6b82fead fix(02-07): stop AppendBlock emitting doubled carriage returns`
  Non-task reason: blind-verifier finding 2.
- `0d322354 test(02-07): verify the mainboard filter on a single oversized deck`
  Non-task reason: blind-verifier finding 3.

## 7. Verification notes

Build and test gates are green in the final state:

| Gate | Result |
|---|---|
| `dotnet build DeckFlow.sln` | `0` errors, `0` warnings |
| `DeckFlow.Core.Tests` | `1740` passed, `0` failed |
| `DeckFlow.Web.Tests` | `2095` passed, `16` skipped, `0` failed |

One spurious `DeckFlow.Web.Tests` failure was observed during this cleanup wave:

- test name: `DeckFlow.Web.Tests.FeedbackStoreTests.ListAsync_FiltersByType`
- first failure: `System.ObjectDisposedException` from `SQLitePCL.sqlite3`
- disposition: **not reproducible**. The named test passed on immediate `--filter` rerun, and the full
  suite passed on rerun. This matches the worktree's known concurrent-testhost contention pattern,
  not a deterministic regression from the 02-07 changes.

## 8. Final state

The plan now has all four required artifacts in place:

- computed lands calibration with the D-06 verdict guard;
- unconditional protection, casual-bias, and corpus-hygiene disclosures;
- a negative-path template whose measured-role list matches the harness taxonomy;
- both the original and oversized single-deck `boardFilter` verification tests;
- and this summary, recording the reviewer-directed deviation and the blind-verifier follow-up wave.
