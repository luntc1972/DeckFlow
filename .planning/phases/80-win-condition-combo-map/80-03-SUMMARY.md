---
phase: 80-win-condition-combo-map
plan: 03
subsystem: ui
tags: [razor, deck-analysis, feature-flag, json-round-trip, zip-artifact, xunit, playwright]

# Dependency graph
requires:
  - phase: 80-01
    provides: WinConMap Core model + WinConMapAggregator.Compute()
  - phase: 80-02
    provides: analysis.wincon-map flag, BuildWinConMapText, 3-variant prompt threading, ShouldBypassPacketCache
provides:
  - Step-3 on-page win-condition/combo map readout (ranked combos, near-combos, band, closing cards, data-unavailable disclosure)
  - WinConMapJson hidden-field round-trip (Request <-> ViewModel <-> DeckAnalysisPacketResult)
  - 61-wincon-map.json conditional zip entry (BuildZip writer + LoadFromZip restore)
  - Deep untrusted-input hardening (TryDeserializeWinConMap / IsStructurallyValidWinConMap)
  - Dual-layer flag-OFF byte-identity proof (artifact/zip contract test + Razor render excision-equality test)
affects: [81-mulligan-evaluator]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Conditional hidden-field round-trip (rendered only when non-empty) mirrored from Phase-79 InteractionAuditJson"
    - "Conditional zip entry (NormalizeSections drops null/blank) mirrored from 60-interaction-audit.json -> 61-wincon-map.json"
    - "Serialize-fallback at BuildZip call sites: prefer round-tripped hidden field, fall back to serializing the site's own live result so a fresh download never drops the entry"
    - "Deep structural validation of untrusted deserialized input: size cap, per-list count caps, Enum.IsDefined on every enum field, non-blank on every rendered string, AssemblyPathCount integrity check"
    - "Dual-layer flag-OFF byte-identity: artifact/zip layer (entry-map + per-platform content) + page layer (IRazorViewEngine render excision-equality)"

key-files:
  created:
    - DeckFlow.Web.Tests/WinConMapSurfaceContractTests.cs
    - DeckFlow.Web.Tests/DeckAnalysisWinConMapViewTests.cs
  modified:
    - DeckFlow.Web/Models/DeckAnalysisRequest.cs
    - DeckFlow.Web/Models/DeckAnalysisViewModel.cs
    - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
    - DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs
    - DeckFlow.Web/Controllers/DeckPacketController.cs
    - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web.Tests/DeckPacketControllerTests.cs
    - DeckFlow.Web/e2e/deck-analysis-render.spec.ts

key-decisions:
  - "Promoted the already-computed winConMap local (from Plan 80-02) directly into DeckAnalysisPacketResult on the computed path -- no recompute"
  - "Fresh-download serialize-fallback: BuildZip call sites at both the cached and fresh branches prefer request.WinConMapJson but fall back to JsonSerializer.Serialize(result.WinConMap) so a first-time download with an empty posted hidden field still writes the 61-wincon-map.json entry"
  - "Round-trip re-materialization test omits canonicalDeckListText from BuildZip so LoadFromZip leaves DeckSource empty, correctly exercising the Step-3 saved-path short-circuit rather than a full deck rebuild"
  - "Playwright spec extended per plan but execution deferred to the operator's live visual smoke (per project convention -- no browser on the Windows host from this session)"

requirements-completed: [WINCON-01, WINCON-03, WINCON-04]

# Metrics
duration: 55min
completed: 2026-07-02
---

# Phase 80 Plan 3: Win-Condition & Combo Map Web Surface Summary

**Step-3 on-page win-condition/combo map readout with hardened WinConMapJson round-trip through a hidden field and a conditional 61-wincon-map.json zip entry, proven flag-OFF byte-identical at both the artifact/zip layer and the Razor page-render layer**

## Performance

- **Duration:** ~55 min
- **Started:** 2026-07-02T18:47:00Z
- **Completed:** 2026-07-02T19:42:00Z
- **Tasks:** 5 (all executed, no checkpoints)
- **Files modified:** 9 (2 new test files, 7 modified)

## Accomplishments
- Step-3 readout mirrors the paste-artifact win-condition map content exactly: ranked combos with per-combo band hedge, assembly-path count, an always-rendered "One card away (not currently a win line)" near-combos section, overall band, non-combo closing cards, and a distinct data-unavailable disclosure branch
- `WinConMapJson` round-trips through `DeckAnalysisRequest` -> `DeckAnalysisPacketResult` -> `DeckAnalysisViewModel` -> a CONDITIONAL hidden field, mirroring the Phase-79 `InteractionAuditJson` pattern exactly
- `61-wincon-map.json` persists conditionally in the session zip (`BuildZip`/`LoadFromZip`), with a serialize-fallback at both controller download call sites so a fresh download with an empty posted field still emits the entry
- Deep untrusted-input hardening: `TryDeserializeWinConMap` + `IsStructurallyValidWinConMap` size-cap, validate every combo/near-combo/closing-card field (non-blank strings, ranges, `Enum.IsDefined` on both `Band` and `OverallBand`), per-list count caps, and an `AssemblyPathCount == Combos.Count` integrity check -- never throws
- Dual-layer flag-OFF byte-identity proven: `WinConMapSurfaceContractTests` (artifact/zip entry-map + per-platform content + download-upload round trip + hardening) and `DeckAnalysisWinConMapViewTests` (Razor `IRazorViewEngine` render excision-equality)
- New CSS lives entirely in `site-common.css` using existing theme tokens; `:root` token count unchanged; no theme file touched

## Task Commits

Each task was committed atomically:

1. **Task 1: Round-trip plumbing (Request/ViewModel/result record/deep hardening/controller mapping)** - `8761e6ca` (feat)
2. **Task 2: On-page readout block + conditional hidden field + site-common.css** - `15d8a330` (feat)
3. **Task 3: Persist WinConMapJson through the zip (61-wincon-map.json writer + loader + controller wiring)** - `235e11fb` (feat)
4. **Task 4: Per-surface flag-OFF byte-identity + zip round-trip contract test + hardening + Playwright spec** - `f5070183` (test)
5. **Task 5: Page-level flag-OFF byte-identity Razor render test** - `feb8abaf` (test)

**Plan metadata:** (this commit, following)

## Files Created/Modified
- `DeckFlow.Web/Models/DeckAnalysisRequest.cs` - `WinConMapJson` hidden-field property (null-guard setter, mirrors `InteractionAuditJson`)
- `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` - `WinConMap? WinConMap { get; init; }` view-model property
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` - `DeckAnalysisPacketResult.WinConMap` param; computed-path + Step-3 saved-path wiring; `TryDeserializeWinConMap` + `IsStructurallyValidWinConMap` + per-shape helpers + size/count-cap constants
- `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` - `61-wincon-map.json` allowed-name, conditional `BuildZip` section, `LoadFromZip` restore
- `DeckFlow.Web/Controllers/DeckPacketController.cs` - hidden-field source write + view-model mapping at both Step-3 sites; serialize-fallback `winConMapJson` at both `DeckAnalysisDownload` `BuildZip` call sites
- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` - conditional hidden `WinConMapJson` textarea + flag-guarded `wincon-map` readout region + `formatWinConBand` Razor helper
- `DeckFlow.Web/wwwroot/css/site-common.css` - `.wincon-map*` layout classes (existing tokens + baked hex, no new `:root` tokens)
- `DeckFlow.Web.Tests/WinConMapSurfaceContractTests.cs` (new) - 3-platform prompt byte-identity, zip entry-map byte-identity, download-upload round trip, 13 hardening cases
- `DeckFlow.Web.Tests/DeckPacketControllerTests.cs` - fresh-download serialize-fallback controller test
- `DeckFlow.Web.Tests/DeckAnalysisWinConMapViewTests.cs` (new) - Razor render excision-equality (page-layer byte-identity gate)
- `DeckFlow.Web/e2e/deck-analysis-render.spec.ts` - ON/OFF `wincon-map` visibility checks across desktop (1280) and mobile (390) Playwright projects (execution deferred to operator)

## Decisions Made
- Promoted the Plan-80-02 `winConMap` local directly into `DeckAnalysisPacketResult` instead of recomputing at any read site.
- Chose a serialize-fallback (prefer hidden field, else serialize the live result) at the `BuildZip` call sites rather than always trusting the posted `WinConMapJson`, so a first-time fresh download never silently drops the zip entry.
- The download-upload round-trip re-materialization test intentionally omits `canonicalDeckListText` from its `BuildZip` call so the restored request's `DeckSource` stays empty, correctly exercising the lightweight Step-3 saved-path short-circuit (matching how a real Step-3 re-post with only `deck_profile` JSON and hidden fields behaves) rather than a full Scryfall/combo rebuild.
- Left the extended Playwright spec as source-only for this session; live execution (desktop/mobile visual confirmation) deferred to the operator's separate live visual smoke pass, consistent with project convention (no browser automation against the live server from this session).

## Deviations from Plan

None - plan executed exactly as written. All five tasks, their acceptance criteria, and the threat-model mitigations (T-80-03-01 through T-80-03-04) were implemented as specified.

## Issues Encountered
- First draft of the download-upload round-trip test passed `canonicalDeckListText: "1 Sol Ring"` into `BuildZip`, which caused `LoadFromZip` to backfill `DeckText` and made `BuildAsync`'s Step-3 short-circuit condition (`IsNullOrWhiteSpace(request.DeckSource)`) false, triggering a full deck rebuild and commander-validation failure instead of the intended lightweight saved-path restore. Fixed by omitting the deck-list argument from that specific test's `BuildZip` call (see Decisions above); all 23 `WinConMapSurfaceContractTests` cases pass.

## User Setup Required

None - no external service configuration required. The `analysis.wincon-map` flag remains seeded OFF in both dialects (from Plan 80-02); this plan makes the ON-state UI fully functional but does not flip the flag in any environment.

## Next Phase Readiness
- Phase 80 (Win-Condition & Combo Map) is now fully implemented across all 3 plans (80-01 Core model, 80-02 Web integration, 80-03 UI surface + round-trip). Ready for push, CI verification, and operator live visual smoke (desktop + mobile, both themes) before flag flip.
- Phase 81 (Opening-Hand / Mulligan Evaluator) can proceed independently; no shared surface with this plan beyond the general deck-analysis page conventions already established.
- Owed: push branch -> CI green; operator live smoke (flag ON: readout matches paste artifact; flag OFF: page/zip byte-identical); eventual branch -> main merge + manual prod deploy; flip `analysis.wincon-map` in prod when ready.

---
*Phase: 80-win-condition-combo-map*
*Completed: 2026-07-02*

## Self-Check: PASSED

All 12 created/modified files confirmed present on disk; all 5 task commit hashes (`8761e6ca`, `15d8a330`, `235e11fb`, `f5070183`, `feb8abaf`) confirmed in git history.
