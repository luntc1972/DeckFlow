---
phase: 42-orchestrator-extraction
plan: 04
subsystem: api
tags: [studio, blazor, dependency-injection, orchestration, content-kb, sc4]

requires:
  - phase: 42-orchestrator-extraction
    provides: AddContentKbOrchestrator() DI extension + ContentKbOrchestrator (Waves 2-3)
provides:
  - DeckFlow.Studio ContentKbOrchestratorSmokeService resolving the IContentMaintenanceOrchestrator slice
  - Studio composition-root DI for the full orchestrator ctor (local SQLite stores + options)
  - proof Studio -> Core works with NO DeckFlow.CLI reference (SC4)
affects: [43]

tech-stack:
  added: []
  patterns:
    - "Studio composition root constructs local-SQLite stores the same way the CLI does, registers ContentKbOrchestratorOptions (local artifactRoot), then AddContentKbOrchestrator()"
    - "Slice injection: a Studio service depends on IContentMaintenanceOrchestrator (one slice), not the facade"

key-files:
  created:
    - DeckFlow.Studio/Services/ContentKbOrchestratorSmokeService.cs
  modified:
    - DeckFlow.Studio/Program.cs

key-decisions:
  - "All 4 integration deps (LlmDistillationService, YouTubeChannelVideoLister, YouTubeTranscriptSource, FfmpegAudioChunker) are REAL Core impls — none needed a named Noop null-object (none require a secret/network to construct)"
  - "Stores registered as Singleton against a local content-kb.db under MTG_DATA_DIR/studio (or ./artifacts/studio); prod Postgres conn string stays presence-only (StudioConfig) — never constructed/logged"
  - "Startup resolve of the smoke service (in a scope, no ProbeAsync call) proves the full ctor resolves; build-verified only"

patterns-established:
  - "Singleton HttpClient in the Studio composition root for the integration services (real IHttpClientFactory wiring deferred to Phase 43)"

requirements-completed: [ORCH-02]

duration: 6min
completed: 2026-06-13
---

# Phase 42-04: Studio Orchestrator Smoke + DI Summary

**DeckFlow.Studio resolves the IContentMaintenanceOrchestrator slice from DI and calls ListBlockedAsync via a smoke service, with the full orchestrator ctor wired against local-SQLite stores + a local artifactRoot — proving Studio consumes Core orchestration with ZERO DeckFlow.CLI reference (SC4).**

## Performance
- **Duration:** ~6 min (Codex gpt-5.4)
- **Tasks:** 2
- **Files:** 1 created, 1 modified

## Accomplishments
- ContentKbOrchestratorSmokeService injects IContentMaintenanceOrchestrator (the slice, not the facade) and ProbeAsync returns ListBlockedAsync row count — read-only, no writes/network/prod-conn.
- Program.cs registers all 7 stores/ledgers (local SQLite), the 4 real integration services, Func<DateTimeOffset>, ContentKbOrchestratorOptions (local artifactRoot), AddContentKbOrchestrator(), and the scoped smoke service. Startup resolve in a scope proves full ctor resolution.

## Task Commits
1. **Task 1: smoke service** — `ab47bc7` (feat)
2. **Task 2: Studio DI wiring** — `75f7374` (feat)

## Decisions Made
- No Noop null-objects needed — all 4 integration deps construct without a secret/network.
- Local data dir resolved from MTG_DATA_DIR/studio else ./artifacts/studio; prod conn string remains presence-only.

## Reviewer Verification (Claude)
- `grep -rn "DeckFlow.CLI" DeckFlow.Studio/` → nothing (SC4 hard gate passes); csproj untouched.
- Smoke service injects the SLICE (D-01 proof); startup resolve constructs the orchestrator without calling ProbeAsync (no startup DB query).
- No prod connection string constructed or logged (only the presence status line, unchanged).
- `dotnet build DeckFlow.sln -warnaserror` → 0 errors / 0 warnings.

## Deviations from Plan
None beyond taking the plan's OPTIONAL startup-resolve path.

## OPEN — non-blocking operator runtime checkpoint
The startup resolve invokes `TranscriptProviderFactory.Resolve(...)` during `app.Build()`. This is BUILD-verified only — Studio was not started at runtime (server start is the operator's, per project workflow). Recommend a one-time `dotnet run --project DeckFlow.Studio` smoke before relying on Studio startup, mirroring Phase 41's SC1 runtime checkpoint. Non-blocking for this phase (bar = build-clean + container-resolvable).

## Next Phase Readiness
- SC4 proven; Phase 43+ can build the real Studio harvest/distill UI on these registrations (and wire IHttpClientFactory).

---
*Phase: 42-orchestrator-extraction*
*Completed: 2026-06-13*
