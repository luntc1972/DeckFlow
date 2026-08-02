# 04-03 — Engine wiring, flag transports, D-23 tally join — SUMMARY

**Status:** complete
**Date:** 2026-08-02
**Baseline HEAD at start:** `7b1ff22a`
**Requirements:** TWIN-02

## What shipped

`CutLabStructuralFindings`' functional-twins detector is now reachable behind the dedicated
`analysis.cut-lab.functional-twins` flag on all three production transports, and `FunctionalTwins`
contributes to the discriminating tally — the one change in this cycle that moves a card up the
proposal queue. The flag is seeded OFF, so the deploy is dark.

### Task 1 — engine and transports

- `BuildFindingsAndRoundPlan` gained a **non-optional** `bool twinsEnabled`, positioned after
  `decisions` and before the optional `round3DeltaMagnitudes` (D-19). Non-optional is the point: the
  compiler enumerates every call site.
- `CutLabPageService` passes `IsFlagOn(CutLabStructuralFindings.FunctionalTwinsFlagKey)` through its
  existing helper. Its ctor shape is unchanged (Phase 3 code).
- `CutLabUiPatchBuilder` and `CutLabApiController` each gained a **required**, null-guarded
  `IFeatureFlagCache` ctor dependency and a private `IsFlagOn` mirroring the page service's inverted
  read (D-19, D-20). `ICutLabUiPatchBuilder.BuildAsync`'s signature is untouched — the flag is a
  construction-time dependency, so no interface consumer changed.
- `ExcludedFindingKindsFromTally` is unchanged in membership; its `// Why:` comment was extended to
  record that `FunctionalTwins` is deliberately absent. `EnablerStarved` was left in place.
- **D-18 honoured:** `CutLabSimulationService` is unmodified and stays inert via `Compute`'s
  optional-`false` default. `Program.cs` untouched — `AddDeckFlowFeatureFlags()` already registers
  `IFeatureFlagCache` (precondition gate returned exactly 1).

### Task 1 step 6b — the D-23 normalized tally join

`BuildFindingTallies` now takes `findings` and `workingList`. For `FunctionalTwins` **only**, the
finding's evidence names are normalized once through `CutLabCardNames.Normalize` into a set under
`CutLabCardNames.Comparer`, and working-list cards are matched by normalized name, deduplicated by
raw name under `OrdinalIgnoreCase` before incrementing. Every other kind keeps its raw dictionary and
raw lookup byte-for-byte. Re-keying all tallies would "alter round 1-3 assignment for every deck" by
the engine's own `// Why:` block, and must not ride in on a flagged feature.

### Tasks 2 and 3 — proof

- 8 new engine tests in `CutLabCutRoundEngineTests` (round promotion, `NextProposal` change, D-23
  normalized join with a non-twins negative control, duplicate-raw-entry join, D-16 multi-role
  double-count, reflection assertion that the kind is absent from the exclusion set).
- New `CutLabFunctionalTwinsFlagTests.cs`, 12 tests, LF: page/patch/decide OFF-ON-missing coverage,
  page-vs-patch parity on findings and `NextProposal`, and the three controller-owned tests that
  assert the **persisted decision round** rather than `NextProposal`.
- Eight `CutLabApiController` / `CutLabUiPatchBuilder` constructions repaired across four test files.
  Proven structurally: a required ctor parameter makes any missed construction a compile error, so
  the clean solution build is the binding evidence, not a grep.
- No pre-existing assertion or expected value was changed anywhere.

## Verification

| Gate | Result |
|---|---|
| `build DeckFlow.sln -c Release` | **Build succeeded. 0 Warning(s), 0 Error(s)** |
| `CutLabFunctionalTwinsFlagTests` filtered | **Passed! Failed: 0, Passed: 12, Skipped: 0, Total: 12** |
| Task 2 filtered (engine + 4 transport files) | **Passed! Failed: 0, Passed: 127, Skipped: 0, Total: 127** |
| Full solution suite | **all green** — Web `Failed: 0, Passed: 2266, Skipped: 16, Total: 2282`; Core `Failed: 0, Passed: 2011, Total: 2011`; Studio `Failed: 0, Passed: 426, Skipped: 4, Total: 430` |
| Test-count delta | Web 2245 → 2266 = **+21**, exactly the 8 new engine tests + 12 flag tests + 1 null-guard test |
| EOL, all 10 touched files | `CR=0` (LF), identical to the pre-dispatch baseline |
| `git diff --stat` vs `--ignore-all-space --stat` | disagree for `CutLabCutRoundEngine.cs` only (49 vs 43). Cause is the 6 pre-existing lines **re-indented** into the new `else` block at `:443-448`, not EOL churn — `CR=0` on both sides. This means Task 1's literal "must agree for all four files" criterion is NOT met as written; see F-04-03-01b. |
| Forbidden paths | `CutLabSimulationService.cs`, `Program.cs`, all `.csproj`, `.editorconfig`, `.gitattributes`, `.gitignore`, `REQUIREMENTS.md`, `ROADMAP.md` all unmodified |
| `BuildFindingsAndRoundPlan` census | 1 declaration + 3 production + 1 test call site; every production site passes `IsFlagOn(...FunctionalTwinsFlagKey)`, **no literal `true`/`false`** |
| `CutLabApiController` calls to it | **exactly 1** (`:105`), matching the plan |

### Mutation checks — all run and fully reverted

| # | Mutation | Expected | Observed |
|---|---|---|---|
| M1 | Add `FunctionalTwins` to `ExcludedFindingKindsFromTally` | tests 1, 2, 3, 5, 6 all fail | **Failed: 5, Passed: 0** — all five, including `BuildQueue_FunctionalTwinsChangesNextProposal`, so test 3 does prove the ranking change and needed no strengthening |
| M2 | Delete the `FunctionalTwins` normalized tally branch (all kinds fall back to raw) | twins leg fails, non-twins control passes | **Failed: 1, Passed: 1** — `BuildQueue_TwinsEvidenceMatchesNormalizedEquivalentWorkingListEntries` failed; `BuildQueue_TwinsDuplicateRawPoolEntries_IncrementEachRawTallyOnce` passed |
| M3a | `CutLabPageService` read → `IsEnabled(key)` | `PageRender_TwinsFlagMissingFromSnapshot` fails | **Failed: 1** |
| M3b | `CutLabUiPatchBuilder` read → `IsEnabled(key)` | `PatchBuilder_TwinsFlagMissingFromSnapshot` fails | **Failed: 1** |
| M3c | `CutLabApiController` read → `IsEnabled(key)` | `DecideApi_TwinsFlagMissingFromSnapshot_BehavesAsOff` fails | **Failed: 1** |

Every mutation was reverted from a pre-mutation byte copy and re-verified: the exclusion set holds
exactly its four original members, the D-23 branch is present, and no consumer contains a call to
`IsEnabled` (`grep '\.IsEnabled(' → 0` in all three).

## Findings and deviations

**F-04-03-01 — the plan's `IsEnabled` grep criterion contradicts its own instruction.** Task 1 step 4
requires each new consumer to carry an explanatory comment about why `IFeatureFlagCache.IsEnabled` is
deliberately not used; the Task 1 acceptance criteria then require `grep -n 'IsEnabled'` over those
files to return **zero** hits. Both cannot hold. Satisfied in spirit and verified with a call-shaped
grep: `grep '\.IsEnabled('` returns 0 in all three consumers, while the three mandated comments
remain. Future plans should grep for the call, not the identifier.

**F-04-03-02 — display-vs-domain evidence shape.** `CutLabDecideFindingDto.Evidence` is
display-formatted by `CutLabFindingPresenter` (`"Name · MV n"`, separator U+00B7), whereas
`CutLabFinding.Evidence[].CardName` is the plain card name. The two are easy to compare by accident.
The page/patch parity test now renders **both** sides through `CutLabFindingPresenter.BuildFindings`
so it compares what the two surfaces actually emit, and carries a `// Why:` recording the trap.

**F-04-03-03 — a fake that sources the facts it is meant to supply.** The first execution attempt's
`FakeAnalysisContextBuilder` read `TypeLine` off the working list it was handed. That works on the
patch and decide paths, which pass `state.Pool` (already carrying type lines), but silently fails on
the page path: `CutLabPageService.ProcessAsync` does not analyze `state.Pool` — it rebuilds the pool
from the loaded `DeckEntry` list (no type line) and fills each entry from the resolved-card cache
that this same fake populates. Empty propagated forever, disabling primary-type grouping **and**
commander eligibility (`CommanderEligibility.IsEligible("")` is false). Fixed by keying card facts by
name (`TypeLineFor`), mirroring the existing `ManaValueFor`. Worth generalizing: a fake for a
resolver must never derive its answers from the structure the resolver is supposed to populate.

**F-04-03-04 — tuple equality over arrays.** The parity assertion compared
`(Lead, string[] Evidence)` tuples. `ValueTuple` equality falls through to `string[]`'s **reference**
equality, so two findings with identical ordered evidence compared unequal and the failure printed
two identical-looking collections. Findings are now flattened to a single delimited string before
comparison. This is the second occurrence of this defect class in Phase 4 (04-02 hit it as "record
equality on reference types") — worth a standing note.

**Non-vacuity guard added.** The parity test now asserts `Assert.NotEmpty(pageViews)` first. Without
it the comparison passes when *both* sides are empty, which is exactly the inert-fixture failure the
test exists to catch — and which the page-path defect above would have hidden.

**F-04-03-01b — Task 1's diff-stat criterion is not met as written.** `git diff --stat` and
`git diff --ignore-all-space --stat` disagree for `CutLabCutRoundEngine.cs` (49 vs 43). The delta is
six pre-existing lines re-indented into the new `else` block at `:443-448` — an unavoidable
consequence of wrapping the existing raw-lookup expression in a conditional, which the plan itself
prescribed. It is not EOL churn: `CR=0` in both the working copy and `git show HEAD:` for all ten
files. A whitespace-agreement criterion cannot survive a step that adds a nesting level.

**F-04-03-05 — the D-23 duplicate-entry mutation proof is not constructible; the `.Distinct` is dead
code.** `CutLabCutRoundEngine.cs:441` calls `.Distinct(StringComparer.OrdinalIgnoreCase)` immediately
before `.ToHashSet(StringComparer.OrdinalIgnoreCase)` at `:442`. The `ToHashSet` already dedupes under
the same comparer, so the `.Distinct` is redundant and unfalsifiable — removing it leaves every test
passing. **Behavior is correct either way** (`FindingCount == 1`), so this is not a production defect,
but plan §4c's claim that `BuildQueue_TwinsDuplicateRawPoolEntries_IncrementEachRawTallyOnce` is a
"duplicate raw-entry mutation proof" is **false**: the dedup it claims to pin comes from `ToHashSet`,
not from the operator the plan prescribed. Left in place deliberately rather than removed, because
the blind verification had already been run against this tree and editing production code would void
it for a no-op simplification. **Follow-up: drop the redundant `.Distinct` in 04-04.**

**F-04-03-06 — a plan premise about the AJAX parity fixture was wrong.** Task 2 requires the
controller, patch builder and page service in `CutLabAjaxFloorByRoleRegressionTests.cs` to share one
`IFeatureFlagCache`, on the stated premise that the page service is constructed at `:352` in the same
factory. It is not — `CreatePageService` (`:377`) is a separate factory with its own cache. The
controller, nested patch builder and floor resolver do share one instance (`:343`, `:356`, `:359`,
`:363`), which is the part that matters. No practical divergence: every call site in that file
resolves twins OFF.

**Pre-existing flake observed, not a regression.** The first full-suite run showed
`Failed: 1, Passed: 2265` on
`DeckPacketControllerTests.DeckAnalysisDownload_FlagOffResultWithStalePostedWinConMapJson...`,
differing at byte 10 — the ZIP DOS modification-time field. It passed 3/3 in isolation and the file
is untouched by this change. Second full run: `Failed: 0, Passed: 2266`.

**Test strengthening applied after blind verification.** The verifier found that
`Assert.All(plan.Queue, ...)` passes vacuously on an empty queue in tests 4b and 4c, neither of which
pinned a count. Both now assert `Queue.Count == 4` first. This is test-only and strictly additive;
the engine + flag suites were re-run after it (`Failed: 0, Passed: 61, Total: 61`). The verifier's
PASS_WITH_NOTES predates this change.

**No production behavior was changed to make a test pass.** Both defects found during execution were
in the new test fixture; the production wiring from the first attempt was correct throughout, as
evidenced by the three controller tests passing before any fixture fix.

## Execution provenance

Plans 04-01 and 04-02 were Codex-executed. This plan was dispatched to Codex `gpt-5.6-terra`, which
completed Tasks 1 and 2 and the bulk of Task 3, then correctly returned `BLOCKED` rather than
weakening the page-vs-patch parity assertion. Its self-report named one failing test where there were
four. The independent re-run diagnosed both defects; the round-2 fix dispatch failed with
`ERROR: Your workspace is out of credits`, and the operator explicitly authorized Claude to complete
the remaining work. Full run record in `.foreman/ledger.md`.

## Owed / follow-ups

- **Codex review of this change is still owed** and could not be run — the Codex workspace is out of
  credits. This is the first plan in the phase whose code was not written by Codex, so the
  cross-family review matters more here, not less.
- Live follow-up count stays at six; F-04-07 (`CardTextByCardName` keyed `OrdinalIgnoreCase` but
  consumed by case-sensitive JavaScript) remains recorded-not-fixed, out of scope.
- D-20's shared `IsFlagOn` helper cleanup remains deferred to 04-04.
- **Drop the redundant `.Distinct(StringComparer.OrdinalIgnoreCase)` at
  `CutLabCutRoundEngine.cs:441`** (F-04-03-05) — dead code ahead of a `ToHashSet` with the same
  comparer. Deferred so the blind verification of this tree stays valid.
- Plan 04-04 is next and is `autonomous: false` — Task 3 is a human UI checkpoint at ~1440px and
  390x844.
