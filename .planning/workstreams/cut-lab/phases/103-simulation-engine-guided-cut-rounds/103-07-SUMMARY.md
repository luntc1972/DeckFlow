---
phase: 103-simulation-engine-guided-cut-rounds
plan: 07
subsystem: api
tags: [cut-lab, aspnet, api, mvc, simulation, tests]

requires:
  - phase: 103-02
    provides: state envelope, serializer, immutable working-list derivation
  - phase: 103-04
    provides: round engine and proposal queue semantics
  - phase: 103-05
    provides: proposal delta simulation service
  - phase: 103-06
    provides: shared CutLabAnalysisContextBuilder and normalized card-key analysis
provides:
  - shared Cut Lab decision applier for accept/reject/defer/restore
  - POST /api/cut-lab/decide JSON endpoint with same-origin validation
  - POST /cut-lab/decide no-JS fallback wired through ProcessAsync
  - decision DTOs and regression coverage for applier, API, and MVC flows
affects: [cut-lab, api, mvc, simulation, floor-warnings]

tech-stack:
  added: []
  patterns: [shared pure applier, same-origin-first JSON API, no-JS fallback through page service]

key-files:
  created:
    - DeckFlow.Web/Models/Api/CutLabDecideApiRequest.cs
    - DeckFlow.Web/Models/Api/CutLabDecideApiResponse.cs
    - DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs
    - DeckFlow.Web/Controllers/Api/CutLabApiController.cs
    - DeckFlow.Web.Tests/CutLabDecisionApplierTests.cs
    - DeckFlow.Web.Tests/CutLabApiControllerTests.cs
  modified:
    - DeckFlow.Web/Controllers/CutLabController.cs
    - DeckFlow.Web.Tests/CutLabControllerTests.cs

key-decisions:
  - "Decision application stays pure and Pool-immutable; restore removes every decision record for the card."
  - "The JSON endpoint rebuilds post-decision proposal state through CutLabAnalysisContextBuilder instead of duplicating role assignment."
  - "The no-JS form path reuses the shared applier and then re-renders the full page through ICutLabPageService.ProcessAsync."

patterns-established:
  - "Pattern 1: same-origin validation is the first statement in Cut Lab JSON API actions."
  - "Pattern 2: accepted cuts are surfaced for restore by projecting accepted decision records, not by mutating Pool."

requirements-completed: [CUT-01, CUT-02, CUT-03, SIM-01]

duration: 10min
completed: 2026-07-20
---

# Phase 103 Plan 07 Summary

**Shared Cut Lab decision handling now powers both the JSON cut loop and the no-JS form fallback, with rebuilt proposal state, floor warnings, restore support, and regression coverage.**

## Performance

- **Duration:** 10 min
- **Started:** 2026-07-19T19:25:17-06:00
- **Completed:** 2026-07-20T00:00:00-06:00
- **Tasks:** 3
- **Files modified:** 8

## Accomplishments

- Added `CutLabDecisionApplier` plus typed request/response DTOs for accept, reject, defer, and restore.
- Added `POST /api/cut-lab/decide` with same-origin enforcement, shared analysis-context rebuild, next-proposal shaping, floor warnings, and proposal deltas.
- Added `POST /cut-lab/decide` so the non-JS path applies the same decision core and re-renders via `ProcessAsync`.

## Task Commits

1. **Task 1: shared applier and DTO contract** - `7bd3f2bd` (`feat(103-07): add shared cut lab decision applier`)
2. **Task 2: JSON decision endpoint** - `5677c9db` (`feat(103-07): add cut lab decision api endpoint`)
3. **Task 3: no-JS controller fallback** - `8468977b` (`feat(103-07): add cut lab no-js decision fallback`)

**Plan metadata:** pending docs commit

## Files Created/Modified

- `DeckFlow.Web/Models/Api/CutLabDecideApiRequest.cs` - request DTO and decision enum for Cut Lab decide actions.
- `DeckFlow.Web/Models/Api/CutLabDecideApiResponse.cs` - structured response DTOs for next proposal, deltas, warnings, and restore rows.
- `DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs` - shared immutable state-mutation rules for accept/reject/defer/restore.
- `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` - same-origin JSON endpoint that rebuilds context and returns next proposal payloads.
- `DeckFlow.Web/Controllers/CutLabController.cs` - antiforgery-protected no-JS fallback action that reapplies `ProcessAsync`.
- `DeckFlow.Web.Tests/CutLabDecisionApplierTests.cs` - applier regression coverage.
- `DeckFlow.Web.Tests/CutLabApiControllerTests.cs` - JSON endpoint regression coverage.
- `DeckFlow.Web.Tests/CutLabControllerTests.cs` - no-JS fallback regression coverage.

## Decisions Made

- Used accepted decision records, not pool mutation, as the source of truth for cuts and restore.
- Evaluated floor warnings against the accepted cut request using pre-application counts so floor breaks remain visible but non-blocking.
- Kept the form fallback thin by mutating the serialized state first and delegating the full page recomputation back to `ICutLabPageService`.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- The API round-advance test needed real structural-finding inputs instead of synthetic expectations, so the fixture was reshaped around actual combo and curve-congestion signals from `CutLabStructuralFindings.Compute`.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Cut Lab now has a shared decision core, transport endpoint, and no-JS fallback ready for UI wiring and downstream compare/export phases.
- The branch remains on `gsd/cycle18-cut-lab`; no push or integration step was performed.

---
*Phase: 103-simulation-engine-guided-cut-rounds*
*Completed: 2026-07-20*
