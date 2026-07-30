# Foreman Ledger — Cut Lab combo-piece cut bias

- **Run started:** 2026-07-29
- **Mode:** Codex-boosted (Agent ✓, real shell ✓, codex-cli 0.146.0 ✓, ChatGPT-account login)
- **Baseline commit:** `1511dd95` (== origin/main), working tree clean
- **Worktree:** `/mnt/c/users/chrislunt/source/personal/deckflow-combo-cuts`
- **Branch:** `fix/cutlab-combo-cut-bias`
- **Scope:** defects D1 + D2 only. D3 (`requires[]` template slots) is a separate later branch, explicitly out of scope.
- **Ship shape:** unflagged hotfix (user decision, 2026-07-29). Merging to main autodeploys.
- **Codex seats:** coding `gpt-5.4` medium; review `gpt-5.5` medium (session default, user-confirmed).

## Problem

Cut Lab promotes combo-dense cards to Round 1 "Obvious cuts". Reported live on
Moxfield deck `7KhI3pGznU2O1SELaVK94g` (Celes, Rune Knight) — Ashnod's Altar
proposed first despite being a live combo piece.

- **D1** — `CutLabCutRoundEngine.cs:111-117`: `ExcludedFindingKindsFromTally`
  contains `ComboProtected` but not `EnablerStarved`. `BuildFindingTallies:363-386`
  skips excluded kinds then does `tally.Count++` per surviving finding. The
  protective finding scores 0; its punitive twin — emitted from the *same*
  near-combo input — scores +1.
- **D2** — `CutLabStructuralFindings.cs:282-299`: `ComputeEnablerStarved`
  iterates `nearCombos` ungrouped, while `ComputeComboProtected:342-378` groups
  near-combo variants by card-set. N Spellbook variants of one near-combo yield
  N findings (and, pre-D1, N tally points).

Upstream root cause is D3 (`CommanderSpellbookService.cs:261` reads `uses` only,
ignoring `requires[]` template slots), which manufactures the phantom
near-combos these two defects then amplify. Out of scope here.

## Pre-dispatch audit (Claude, before any dispatch)

| Existing test | Verdict |
|---|---|
| `CutLabCutRoundEngineTests.cs:11` `BuildQueue_TwoDiscriminatingFindings_PlacesCardInRound1` | Survives. Its `EnablerStarved` fixture at `:26` is incidental — assertions `:30-36` only touch "Round 1 Card". |
| `CutLabCutRoundEngineTests.cs:66` `BuildQueue_OneDiscriminatingFindingGoesToRound2...` | Survives. Uses `CurveCongestion`, not `EnablerStarved`. |
| `CutLabStructuralFindingsTests.cs:210` `Compute_EnablerStarved_UsesInDeckCardsAsPluralSubject...` | Survives. Second fixture combo has `CardsInDeck.Count == 1` (below `NearComboMinPiecesInDeck = 2`) so only one group forms; lead string unchanged. |
| `CutLabStructuralFindingsTests.cs:363` `Compute_ComboProtected_AndEnablerStarved_CoexistForNearComboData` | Survives. Neither defect fix suppresses emission — only tally weight (D1) and per-variant duplication (D2). |

Expectation: **zero existing tests change.** Codex must verify, not assume.

## Foreman design decisions

- **DD-1** — `EnablerStarved` keeps rendering in the UI; only its tally weight is
  removed. Minimal and reversible; "you're one card from a combo" is a shopping-list
  signal, not a cut signal.
- **DD-2** — Grouped `EnablerStarved` lead text preserves the existing string
  byte-for-byte when exactly one distinct missing card exists, and switches to
  "combo partners:" (plural) only when >1. This keeps
  `CutLabStructuralFindingsTests.cs:228` green without a grammar wart.

## Tasks

| ID | Task | Seat | Write set | Status |
|----|------|------|-----------|--------|
| T1 | TDD red tests + D1/D2 fix | Codex `gpt-5.4` medium | `CutLabCutRoundEngine.cs`, `CutLabStructuralFindings.cs`, `CutLabCutRoundEngineTests.cs`, `CutLabStructuralFindingsTests.cs` | **BLOCKED** — Codex out of credits |
| T2 | Blind verification | `foreman-verifier` | none (read-only) | PENDING (gated on T1) |
| T3 | Cross-AI review | Codex `gpt-5.5` medium, read-only | none | PENDING (also credit-gated) |

## Attempts (append-only)

- 2026-07-29 — ledger created, baseline `1511dd95` clean, T1 not yet dispatched.
- 2026-07-29 — T1 attempt 1 dispatched to Codex `gpt-5.4` medium, `-s danger-full-access`,
  `-C <worktree>`. **BLOCKED.** Codex exited without touching a file
  (`git diff` empty; only the untracked ledger present). Error, verbatim:
  `ERROR: Your workspace is out of credits. Ask your workspace owner to refill in order to continue.`
  Not retried: this is a hard account-state error, not a transient failure, and the
  identical error already aborted a Codex plan review earlier today (~16:56).
  Surfaced to user for authorization per CLAUDE.md "Cross-AI dispatch failures" rule 3.
- 2026-07-29 — Environment defect found and fixed (blocks ANY executor, not just Codex):
  the fresh worktree had no `DeckFlow.Web/node_modules`, so the `CompileTypeScriptAssets`
  MSBuild target failed with
  `Cannot find module '...\DeckFlow.Web\node_modules\typescript\bin\tsc'` / `MSB3073`.
  Fixed with a Windows junction to the main worktree's `node_modules`
  (the pattern `.gitignore:8` already anticipates). Verified `typescript/bin/tsc` resolves.
  NOTE: an earlier baseline build was misread as passing because exit code was taken
  from a pipe, not from `dotnet`. Baseline re-run with the exit code captured directly.
- 2026-07-29 — Baseline confirmed green: `dotnet build DeckFlow.sln` → BUILD_EXIT=0,
  0 errors, 9 warnings (matches the known CS8629 baseline).
- 2026-07-29 — **User authorized Claude to implement T1** after the Codex credit block
  (explicit answer to a direct AskUserQuestion, options included "pause" and "refill").
  Seat changed LEAD-implements. Assurance is therefore REDUCED: the standard
  cross-family split (Codex writes / Claude reviews) is unavailable, so the second
  reader is a blind `foreman-verifier` — fresh context, no edit tools, given the
  original task verbatim. Same model family writes and checks. This must be disclosed
  in the final report and NOT presented as cross-AI verified.
- 2026-07-29 — T1 step A (RED) complete and evidenced. Ran the two affected test classes
  against the unfixed tree: `Failed: 5, Passed: 38, Total: 43`. The five failures are
  exactly the predicted set:
    * `BuildQueue_EnablerStarvedDoesNotCountTowardDiscriminatingTally`
    * `BuildQueue_OnlyEnablerStarvedFindings_LandInRound3`
    * `BuildQueue_ComboProtectedPlusEnablerStarved_IsNotPromotedAboveRound3`
    * `BuildQueue_ComboDenseCardNeverReachesRound1_AshnodsAltarRegression`
    * `Compute_EnablerStarved_GroupsVariantsSharingTheSameInDeckCardSet`
  A7 (`..._KeepsDistinctInDeckCardSetsAsSeparateFindings`) passed before AND after by
  design — it is an over-grouping guard, not a red test. Stated plainly rather than
  counted as TDD evidence.
  Headline red output, verbatim:
    `Assert.Single() Failure: The collection contained 3 matching items`
  with the dumped collection showing ONE grouped `ComboProtected` finding beside THREE
  duplicate `EnablerStarved` findings built from identical input — defect 2 demonstrated
  directly, and the tally asymmetry of defect 1 visible in the same dump.
- 2026-07-29 — T1 step B (FIX) applied to the two production files only.
  B1: `EnablerStarved` added to `ExcludedFindingKindsFromTally`; the stale `// Why:`
  comment rewritten, since its "role-wide warnings" rationale never described the combo
  advisories. B2: `ComputeEnablerStarved` grouped on the same card-set key
  `ComputeComboProtected` uses; single-partner lead string preserved byte-for-byte,
  plural "combo partners" only when >1 distinct missing card (DD-2).
- 2026-07-29 — EOL check PASS. `git diff --stat` and `git diff --ignore-all-space --stat`
  are identical (172 insertions, 10 deletions, 4 files). All four touched files carry
  0 CR bytes in both the working tree and `HEAD` — LF preserved, no churn.
  Write set respected exactly; no file outside the fence was modified.
- 2026-07-29 — T1 step C (GREEN) partial. Core: `Passed: 1843, Failed: 0`. Web:
  `Passed: 2048, Failed: 1, Skipped: 16, Total: 2065`. All 5 previously-red tests pass.
  NOTE: `WEB_EXIT=0` / `CORE_EXIT=0` in the log are the exit codes of `tail`, not of
  `dotnet` — the same piped-exit-code trap as the earlier baseline misread. The counts,
  not the exit codes, are authoritative here.
- 2026-07-29 — **ONE FAILURE, and the pre-dispatch audit missed it.**
  `CutLabApiControllerTests.PostDecideAsync_AutoAdvancesToNextRoundAndSecondPass`
  (`CutLabApiControllerTests.cs:326`):
    `Expected: "Round 2 · Structural choices"  Actual: "Round 3 · Preference calls"`
  Audit gap: I grepped test files for the literal token `EnablerStarved`, which only
  matched the two engine/findings test files. This test reaches the same code path
  through a *real* `SpellbookAlmostCombo` fixture, so the grep never saw it. Lesson for
  the D3 branch: grep the finding-producing fixture types
  (`SpellbookAlmostCombo`, `SpellbookCombo`), not just the enum member names.
  Diagnosis — NOT a regression. `CreateAnalysisContext` (`CutLabApiControllerTests.cs:921-929`)
  gives "Round 2 Card" exactly one findings source:
    `new SpellbookAlmostCombo("Missing Piece B", ["Round 2 Card", "Support Card"], ...)`
  which yields `EnablerStarved` (+ `ComboProtected`). Every other finding touching that
  card was ALREADY tally-excluded. So its Round-2 placement was earned solely by the
  promotion this fix removes: the test was pinning the defect. Observed Round 2 -> Round 3
  is the fix behaving exactly as specified.
  HALTED per the ticket's "if an existing test genuinely must change, STOP and report"
  rule rather than silently widening the write set to a 5th file. Surfaced to user.
  Recommended repair (feasibility checked, not yet applied): give "Round 2 Card" and
  "Support Card" a shared category in the fixture so `ComputeStrandedSubthemes` fires
  (`StrandedThemeMinCards = 2`, `StrandedThemeMaxCards = 4`; the fixture currently passes
  empty categories for every card). That restores a genuine discriminating tally of 1 and
  preserves the test's real intent — walking Round 1 -> Round 2 -> second pass. "Support
  Card" is `isLocked: true` so it cannot enter the queue as a side effect.
  Rejected alternative: flipping the assertion to `Round3Label`. It would go green but
  delete this test's only Round-2 coverage.
- 2026-07-29 — **User authorized extending the write set to a 5th file**,
  `DeckFlow.Web.Tests/CutLabApiControllerTests.cs`, and chose the re-fixture repair over
  weakening the assertion (explicit answer to a direct AskUserQuestion).
  Collision check before editing: `"Round 2 Card"` / `"Support Card"` appear in only ONE
  test's pool (`:298-299`), so no other test in the file can be perturbed by giving those
  two cards a category. Verified by grep before the edit, not assumed.
  Applied: `CreateAnalysisContext` now assigns the shared category `"stranded-theme"` to
  those two cards only, with a `// Why:` comment recording that the card must earn its
  round-2 slot from a discriminating finding rather than from a combo advisory.
  No assertion was changed; the test's Round 1 -> Round 2 -> second-pass intent is intact.

## T2 — blind verification: PASS_WITH_NOTES

Verifier: `foreman-verifier`, fresh context, no edit tools, given the ORIGINAL defect
statement (not my restatement) and told to prove each new test fails at HEAD.

Every criterion PASS: scope fence held (5 files, `CommanderSpellbookService.cs` and all
protected config untouched); D1 fixed at `CutLabCutRoundEngine.cs:125`, consumed at `:364`
before any `tally.Count++` at `:382`; D2 group key at `CutLabStructuralFindings.cs:288-295`
byte-identical to `ComputeComboProtected:360-367`; `EnablerStarved` still emitted (`:171`)
and still rendered (`Views/Deck/CutLab.cshtml:682`); `ComboProtected` untouched; thresholds,
keys, labels and banner copy untouched; pre-existing assertions at
`CutLabApiControllerTests.cs:326/339` untouched; build EXIT=0 with 9 warnings, all CS8629
in an untouched Core.Tests file (matches baseline); Web 2049/0/16, Core 1843/0; no EOL churn
(`--stat` == `--ignore-all-space --stat`, 183 ins / 11 del, all 5 files CR=0 in both trees).

Independently confirmed the 5 discriminating tests DO fail at HEAD, with HEAD tallies
computed as 2/2/1/5 → Round1/Round1/Round2/Round1 against asserted Round2/Round3/Round3/
Round3, and `NextProposal` at HEAD being Ashnod's Altar (count 5) over Genuine Cut (count 2)
via `OrderByDescending(Tally.Count)` at `CutLabCutRoundEngine.cs:236`.

Findings, all LOW/INFO:
1. LOW — `Compute_EnablerStarved_KeepsDistinctInDeckCardSetsAsSeparateFindings` is
   non-discriminating (passes at HEAD too). Already disclosed by me as an over-grouping
   guard rather than a reproducer; verifier reached the same conclusion independently.
   No action.
2. LOW — user-visible side effect, intended: excluding `EnablerStarved` from the tally also
   drops it from `DiscriminatingFindingKinds`, so no "Enabler-starved cards" chip appears on
   a proposal (`Models/CutLabViewModel.cs:623-629`). Identical to `ComboProtected`'s existing
   treatment; the findings panel still renders it. The header "N structural findings" count
   also drops when variants merge — the intended D2 outcome. **Flag at UAT so it is not
   mistaken for a regression.**
3. LOW — ledger untracked and not gitignored, could be swept by `git add -A`.
   INVESTIGATED AND DISMISSED: `.foreman/` ledgers are already TRACKED in this repo
   (5 prior ledgers committed; same in the role-floors worktree). Committing it is the
   established convention, not an accident. Staged the 5 source files explicitly anyway.
4. INFO — merged missing partners are alphabetically ordered; new deterministic ordering on
   a previously unreachable copy path. Single-partner copy preserved byte-for-byte and
   pinned by the untouched test at `CutLabStructuralFindingsTests.cs:228`. Evidence shape
   unchanged apart from dedupe; no findings dropped.

NOT VERIFIED (carried forward honestly, not silently closed):
- ~~Changed-lines format gate~~ — CLOSED by foreman: staged the 5 source files explicitly
  (not `-A`) and ran `scripts/format-check-changed.sh staged` → FORMAT_EXIT=0. PASS.
- Playwright / e2e UI suite — not run, no browser opened (standing user rule).
- Live end-to-end behavior on a real Celes, Rune Knight deck — regression is proven at the
  engine unit level and through the controller path, not against live Spellbook data.
- Whether defect 3 is genuinely separable — out of scope; only confirmed
  `CommanderSpellbookService.cs` is unmodified.

## ASSURANCE STATEMENT

This change was implemented by Claude (LEAD seat) after Codex ran out of credits, with
explicit user authorization. The second reader was a blind same-family verifier.
It is **self-implemented with same-family blind review — NOT cross-AI verified.**
Re-running a Codex read-only review once credits return is recommended before merge.

## T4 — follow-up defect found at UAT (2026-07-30)

User UAT of `31042a36` PASSED for Ashnod's Altar (no longer round 1), then surfaced a
SECOND card: Agatha's Soul Cauldron proposed first despite sitting in two combos.

Initial foreman diagnosis was WRONG and is recorded as such. I theorised round-3
cheapest-first ordering. The user's screenshot disproved it: the banner read
**Round 2 - Structural choices**, "Flagged by 1 findings", chip = **Curve congestion**.
So the tally fix was working correctly — the card earned round 2 on a legitimate
non-combo finding.

Real defect (D4): `ComboProtected` had NO effect on ordering at all. Excluding it from
the tally (T1) stopped it promoting cards, but it never demoted them either, so a card in
two complete combos could still lead a round on one unrelated finding. The label
"Combo-protected" promised protection the code never delivered.

Fix (user chose "sort combo pieces last within their round" over filtering them out):
combo membership is now the FIRST ordering key in all three first-pass rounds
(`CutLabCutRoundEngine.cs`, round1/round2/round3 comparers) via a new
`ComboProtectionRank` helper. Combo pieces stay cuttable — they just never lead.
Set is derived from `findings.Findings` where `Kind == ComboProtected`, NOT from
`Tally.Kinds`: the tally deliberately drops that kind, so reading it there would have
produced an empty set and a silently no-op fix.
Second-pass (deferred/rejected) ordering deliberately untouched — it preserves the user's
own decision ordinal.

TDD: 3 tests written first, all red (`Failed: 3, Passed: 23`), including
`BuildQueue_ComboProtectedCardSortsAfterEquallyFlaggedNonComboCard_AgathaRegression`
(MV-2 combo card vs MV-5 plain card, one curve-congestion finding each).
Green: Web `Passed: 2052, Failed: 0, Skipped: 16, Total: 2068` (+3), Core `1843/0`.
EOL: no churn (96 ins / 3 del identical under `--ignore-all-space`, 0 CR both trees).
Format gate: FORMAT_EXIT=0.

Environment note: the first full-suite run failed with `MSB3027`/`MSB3021`, NOT a test
failure — the running local UAT server held `DeckFlow.Web.exe` and blocked the test
build's apphost copy. Stopped the server, re-ran clean. Any `dotnet test` of
DeckFlow.Web.Tests requires the local server to be stopped first.

User UAT of the combo-sort change: PASSED (2026-07-30).

## Known-but-unfixed (observed in the UAT screenshot, out of scope)

- "Flagged by **1 findings**" — singular/plural grammar bug in the proposal card.
- "0 of 7 metric families changed meaningfully" is shown on a card the tool is actively
  proposing to cut, which is a weak basis for leading a round.
- Round 3's `round3DeltaMagnitudes` parameter is never supplied by ANY of the three
  production call sites (`CutLabPageService.cs:364`, `CutLabUiPatchBuilder.cs:75`,
  `CutLabApiController.cs:92`), so `Round3DeltaMagnitudeFor` always returns
  `PositiveInfinity` and round 3 degrades to an ascending mana-value sort — cheapest
  first, which is backwards for cEDH. The banner nonetheless claims "ordered by smallest
  measurable tradeoff first". Pre-existing; NOT the cause of the Agatha report.
- Defect 3 (`requires[]` template slots) still unfixed — the upstream root cause.

## Final write set (5 files)

| File | Kind |
|---|---|
| `DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs` | fix (D1) |
| `DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs` | fix (D2) |
| `DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs` | 4 new tests |
| `DeckFlow.Web.Tests/CutLabStructuralFindingsTests.cs` | 2 new tests + 1 guard |
| `DeckFlow.Web.Tests/CutLabApiControllerTests.cs` | fixture repair (user-authorized) |

## T5 — blind-verifier findings on `6cbe4a30` (2026-07-30)

Verifier verdict on the combo-sort commit: **PASS_WITH_NOTES**. Core claims all held
(set not tally-derived, rank leads the OrderBy, round membership filtered before sorting,
second pass untouched, Web 2052/0/16, Core 1843/0, format gate 0, no EOL churn, 3 tests
empirically red at parent with the real "Agatha's Soul Cauldron" string).

Two MEDIUM findings, both independently re-confirmed by the foreman against source:

- **F1 — demotion over-reaches.** `ComputeComboProtected` (`CutLabStructuralFindings.cs:329-395`)
  yields `ComboProtected` from TWO loops: complete combos (evidence `ComboBadgeState.CompletePiece`)
  and near-combos (evidence `ComboBadgeState.NeedsPartner`). The filter at
  `CutLabCutRoundEngine.cs:225-230` keys on `Kind` alone and takes all evidence, so a card
  MISSING its combo partner — a dead piece and prime cut candidate, the same card
  `EnablerStarved` flags — now sorts LAST in its round. Backwards, and D3's phantom
  near-combos amplify it. Net swing for a near-combo card across the two commits:
  promoted -> neutral -> demoted-last.
- **F2 — DFC combo pieces escape demotion.** `ComboProtectionRank` (`:420`) compares
  deck-sourced `entry.Card.Name` against Spellbook-sourced names with plain
  `OrdinalIgnoreCase`, skipping `CutLabCardNames.Normalize`/`.Comparer` that every other
  cross-source comparison in these two files uses. `CardNormalizer.Normalize`
  (`DeckFlow.Core/Normalization/CardNormalizer.cs:24-29`) truncates at `" // "`, so a deck
  listing `Malakir Rebirth // Malakir Mire` misses the Spellbook name `Malakir Rebirth`.

Also noted, not defects: `BuildQueue_ComboProtectedCardSortsLastInRound1DespiteHigherTally`
overstates its name (both cards tally 2; mana value is the real discriminator, since
`RedundantFinishers` is tally-excluded), and the "Final write set (5 files)" section below
is stale — it describes T1, not `6cbe4a30`.

Codex re-probed 2026-07-30: still `ERROR: Your workspace is out of credits`. T3 cross-AI
review remains BLOCKED. User explicitly authorized Claude to implement T5.

| ID | Task | Seat | Write set | Status |
|----|------|------|-----------|--------|
| T5 | TDD red tests + F1/F2 fix | WORKHORSE (Claude `sonnet`) | `CutLabCutRoundEngine.cs`, `CutLabCutRoundEngineTests.cs` | DISPATCHED |
| T6 | Blind verification of T5 | `foreman-verifier` | none (read-only) | PENDING (gated on T5) |
| T3 | Cross-AI review (both commits + T5) | Codex read-only | none | BLOCKED — no credits |

Baseline for T5: `6cbe4a30`, working tree clean.

### T5 result — DONE (2026-07-30)

TDD honoured: red observed BEFORE any source edit —
`Failed: 2, Passed: 26, Total: 28`, with the F2 test returning the deck-side name
`"Malakir Rebirth // Malakir Mire"` as the live proposal (F2 reproduced, not theorised).

Fixes, both in the set builder / rank lookup, none in the detector:

- **F1** — `.Where(evidence => evidence.BadgeState == ComboBadgeState.CompletePiece)` added
  before the name projection. `BadgeState` is nullable, so this excludes `NeedsPartner`
  AND badge-less evidence in one predicate.
- **F2** — set built via `.Select(CutLabCardNames.Normalize).ToHashSet(CutLabCardNames.Comparer)`;
  lookup via `Contains(CutLabCardNames.Normalize(cardName))`. Both sides normalized.
  `CutLabCardNames.Comparer` is `Ordinal` (NOT OrdinalIgnoreCase) and that is correct —
  `Normalize` lowercases first. Do not "fix" the comparer; it would make normalization redundant.

Tests added: `BuildQueue_ComboProtectedNeedsPartnerOnly_DoesNotDemoteCard`,
`BuildQueue_ComboProtectedDfcCardNormalizesAcrossFrontBackName_MalakirRebirthRegression`,
plus a `Finding(kind, ComboBadgeState, params cardNames)` helper overload.

**Fixture change requiring scrutiny:** 5 pre-existing fixtures built `ComboProtected`
evidence through the badge-less `Finding()` helper (`BadgeState = null`), which the F1
filter excludes — so they were switched to the `CompletePiece` overload. Defensible
(production `ComputeComboProtected` ALWAYS sets a badge state, never null, so the old
fixtures were unrealistic) but it is structurally "the fix broke 5 tests so the tests
changed". Flagged to T6 for adversarial review rather than accepted on the worker's word.

Evidence: Web `Passed: 2054, Failed: 0, Skipped: 16` (baseline 2052 + 2 new), Core
`1843/0` unchanged, format gate exit 0, EOL clean (72/72 identical under
`--ignore-all-space`, verified by the foreman independently of the worker's claim).

Known nit, not fixed: `.Where(!IsNullOrWhiteSpace)` runs BEFORE `.Select(Normalize)`, so a
pathological all-punctuation name would normalize to `""` and enter the set. Unreachable
with real card names; recorded so it is not rediscovered as a defect.

### Correction to the stale write-set section below

The `## Final write set (5 files)` table describes T1 (`31042a36`) only. Actual cumulative
write set across the branch: `CutLabCutRoundEngine.cs`, `CutLabStructuralFindings.cs`,
`CutLabCutRoundEngineTests.cs`, `CutLabStructuralFindingsTests.cs`,
`CutLabApiControllerTests.cs`, and this ledger.
