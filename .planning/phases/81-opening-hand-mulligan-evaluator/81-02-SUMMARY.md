---
phase: 81-opening-hand-mulligan-evaluator
plan: 02
subsystem: analysis
tags: [manabase, feature-flags, paste-artifact, mulligan, dotnet, xunit]

# Dependency graph
requires:
  - phase: 81-01
    provides: "ManabaseMulliganEvaluation deck-level aggregate + OpeningHandSample (TrackedSpellName/TrackedOnCurveTurn/OnCurveCastable/HasPlan), always computed in Core and attached to ManabaseReport.MulliganEvaluation"
provides:
  - "analysis.mulligan-eval flag seeded OFF in both dialects (Postgres FALSE / SQLite 0) with a catalog description"
  - "ManabaseReportTextBuilder.Build trailing ManabaseMulliganEvaluation? mulligan = null param + AppendMulliganEvaluationBlock, null-guarded byte-identical"
  - "ManabaseAnalysisService.MulliganEvalFlagKey + ShowMulliganEval fail-safe-OFF gate on ManabaseAnalysisResult"
  - "ManabaseController Download call-site gating the evaluation on ShowMulliganEval"
affects: [81-03-on-page-readout]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TAP-01/TAP-02 single-artifact recipe reused exactly: trailing optional record param -> null guard appends zero bytes -> private Append*Block renders the record's exact values, no recompute"
    - "Flag const + IsFlagOn(Snapshot) fail-safe-OFF read + ShowX result property + controller download gating, mirrored line-for-line from TapAnalyzerFlagKey/ShowTapAnalyzer"

key-files:
  created:
    - DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderMulliganTests.cs
  modified:
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
    - DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs
    - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs
    - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs
    - DeckFlow.Web/Controllers/ManabaseController.cs

key-decisions:
  - "Did NOT add analysis.mulligan-eval to DeckAnalysisPacketService.PromptMutatingAnalysisFlags/ShouldBypassPacketCache — that registry belongs exclusively to the /deck-analysis packet-session cache pipeline (DeckAnalysisPacketService). ManabaseAnalysisService is a Scoped, per-request service with no IMemoryCache/PacketSessionCache dependency, and ManabaseController has zero cache references — /manabase recomputes on every request, so there is no cache-replay surface for this flag to join. Confirmed via grep (no Cache reference in either file) and Program.cs DI registration (AddScoped, no cache decorator)."
  - "Placed the reflection-based Postgres-literal seed test in FeatureFlagStoreSeedTests.cs (per the plan's files_modified list) rather than ToolFlagSeedConsistencyTests.cs, even though the latter already holds the exact same pattern for analysis.interaction-audit/analysis.wincon-map — the plan explicitly scoped this file, and duplicating the assertion there does no harm."

patterns-established: []

requirements-completed: [MULLIGAN-01, MULLIGAN-04, MULLIGAN-06]

# Metrics
duration: 12min
completed: 2026-07-03
---

# Phase 81 Plan 02: Opening-Hand / Mulligan Flag + Paste Artifact Summary

**analysis.mulligan-eval flag (seeded OFF both dialects) gates a hedged "Opening Hand (mulligan)" block on the /manabase paste artifact — keepable band, keep-size process, and tracked-spell-attributed representative openers — byte-identical to today's output when off.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-07-03T16:33:00Z
- **Completed:** 2026-07-03T16:44:00Z
- **Tasks:** 3
- **Files modified:** 8 (1 new test file, 7 modified)

## Accomplishments

- `analysis.mulligan-eval` is seeded OFF (`FALSE`/`0`) in both the Postgres and SQLite dialect seed rows, inserted between `analysis.wincon-map` and `tool.primer.stale-flag`, with a catalog description ending in the "Off = byte-identical output" guarantee — proven by a SQLite runtime seed-value test and a reflection-based test reading the private `PostgresSeedSql` const (no visibility widening).
- `ManabaseReportTextBuilder.Build` gained a trailing `ManabaseMulliganEvaluation? mulligan = null` param; a null value appends zero bytes (byte-identical to the flag-off artifact), mirroring the existing `tap` param exactly.
- `AppendMulliganEvaluationBlock` renders the evaluation's exact figures: a keepable-hand band with the narrow keep criterion stated inline ("2-5 lands with an early play, on the London mulligan; a heuristic consistency signal, not a strategic keep judgment"), the keep-7/mulligan-to-6/mulligan-to-5 process line, a color/curve line, and up to three representative openers whose on-curve read names the tracked spell (`TrackedSpellName` + `TrackedOnCurveTurn`) rather than a generic claim — closed with a hedge sentence framing the whole block as a first-pass the AI must re-verify.
- `ManabaseAnalysisService` reads the flag fail-safe OFF via `IsFlagOn(MulliganEvalFlagKey)` (Snapshot-based, never `IsEnabled`) and surfaces `ShowMulliganEval` on `ManabaseAnalysisResult`; `ManabaseController.Download` passes `result.Report.MulliganEvaluation` to the builder only when `ShowMulliganEval` is true.
- `ManabaseSwapPromptBuilder` was left untouched, matching the TAP precedent (the swap prompt never gained the tap block either).

## Task Commits

1. **Task 1: Seed analysis.mulligan-eval OFF in both dialects + catalog description + seed/catalog consistency entries** - `477c8801` (feat)
2. **Task 2: Report-builder mulligan param + AppendMulliganEvaluationBlock** - `c583054f` (feat)
3. **Task 3: Service flag gate + ShowMulliganEval + download call-site gating + artifact presence/null-byte-identity test** - `82e5bed9` (feat)

_No plan-metadata commit yet — this SUMMARY.md + STATE.md/ROADMAP.md update is the final commit for this plan._

## Files Created/Modified

- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` - `analysis.mulligan-eval` seed row added to both `PostgresSeedSql` (`FALSE`) and `SqliteSeedSql` (`0`)
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` - operator-facing description for `analysis.mulligan-eval`
- `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` - `[InlineData("analysis.mulligan-eval", false)]` SQLite runtime assertion + a reflection-based `[Fact]` proving the Postgres literal is seeded OFF
- `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` - `[InlineData("analysis.mulligan-eval")]` description-presence assertion
- `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` - trailing `mulligan` param on `Build` + null guard + `AppendMulliganEvaluationBlock`
- `DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderMulliganTests.cs` - null-byte-identity test, block-presence-with-tracked-spell test, no-prescriptive-advice test
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` - `MulliganEvalFlagKey` const, `showMulliganEval` read, `ShowMulliganEval` on `ManabaseAnalysisResult`
- `DeckFlow.Web/Controllers/ManabaseController.cs` - `Download` action passes `mulligan: result.ShowMulliganEval ? result.Report.MulliganEvaluation : null`

## Decisions Made

- Reflection-based Postgres seed test placed in `FeatureFlagStoreSeedTests.cs` per the plan's `files_modified` scoping, reusing the exact `BindingFlags.NonPublic | BindingFlags.Static` technique already established in `ToolFlagSeedConsistencyTests.cs` for `analysis.interaction-audit`/`analysis.wincon-map` — no seed-const visibility change.
- Confirmed (and deliberately did NOT touch) the `PromptMutatingAnalysisFlags`/`ShouldBypassPacketCache` registry in `DeckAnalysisPacketService.cs` — see Deviations below.

## Deviations from Plan

### Scoped-out (not a bug, documented per orchestrator instruction)

**1. Packet-cache bypass registry does not apply to `/manabase`**
- **Instruction source:** Orchestrator's `<critical_project_rules>` (not the 81-02-PLAN.md itself, which does not mention this registry anywhere in its tasks/threat-model/verification) asked that `analysis.mulligan-eval` join `PromptMutatingAnalysisFlags`/`ShouldBypassPacketCache` on both read and write side, citing the `followup_packet_cache_flag_replay` pattern from Phases 73/77/79/80.
- **Investigation:** `PromptMutatingAnalysisFlags` and `ShouldBypassPacketCache()` live exclusively in `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — the packet-session cache that serves `/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`, and `/deck-primer`. `ManabaseAnalysisService` is registered `AddScoped` in `Program.cs` (line 177) with no `IMemoryCache`/`PacketSessionCache` dependency, and `grep -n "cache" DeckFlow.Web/Controllers/ManabaseController.cs` returns nothing. `/manabase` recomputes the full analysis on every request (Load/Analyze/Download all call `RunAnalysisAsync` fresh) — there is no cached artifact for a flag flip to stale-replay.
- **Decision:** Left `DeckAnalysisPacketService.cs` untouched. Adding `analysis.mulligan-eval` to a cache-bypass registry for a pipeline that has no cache would be fabricated code with no corresponding bug to fix, and would touch a file entirely outside this plan's declared `files_modified` list and outside the plan's own threat model (which lists only the flag-seed / block-render / flag-read trust boundaries).
- **Impact:** None — no cache-replay surface exists in the manabase pipeline for this flag to leak through. If a future phase adds packet-level caching to `/manabase`, that phase should extend this check at that time.

---

**Total deviations:** 1 scoped-out (registry does not apply — verified, not assumed)
**Impact on plan:** No code changes beyond the plan's own task list. All three tasks executed as specified with zero auto-fixes needed.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. Both dialect seed rows use `ON CONFLICT DO NOTHING` so a fresh deploy seeds OFF and an operator toggle survives re-bootstrap, same as every other Cycle 14 flag.

## Next Phase Readiness

- `ManabaseAnalysisResult.ShowMulliganEval` + `ManabaseReport.MulliganEvaluation` are both available for 81-03's on-page lens to reuse directly — no new sim call, no new flag read (same `IsFlagOn(MulliganEvalFlagKey)` gate can be reused or the `ShowMulliganEval` result property consumed as-is).
- `AppendMulliganEvaluationBlock`'s tracked-spell on-curve wording (`"{TrackedSpellName} castable on curve (turn {TrackedOnCurveTurn})"` / `"... not on curve (slow start)"`) is a reusable prose pattern 81-03 can mirror for the on-page card/table if it wants worded consistency between the page and the paste artifact.
- Build 0/0 (full solution); Core 1052/1052 pass; Web 1147/1159 pass (12 pre-existing Postgres-integration skips); format-gate (`scripts/format-check-changed.sh ci`) clean against `origin/main`.
- No blockers.

---
*Phase: 81-opening-hand-mulligan-evaluator*
*Completed: 2026-07-03*

## Self-Check: PASSED

All 8 files created/modified confirmed present on disk; all 3 task commits (`477c8801`, `c583054f`, `82e5bed9`) confirmed present in git log.
