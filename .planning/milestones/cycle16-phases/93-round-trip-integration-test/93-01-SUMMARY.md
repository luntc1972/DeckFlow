---
phase: 93-round-trip-integration-test
plan: 01
subsystem: testing
tags: [xunit, testcontainers, postgres, git, content-kb, sync-16]

# Dependency graph
requires:
  - phase: 92-pull-hardening
    provides: seed_managed field-authority + PullFromProd hardening the loop this harness will drive in 93-02
provides:
  - Single new `<ProjectReference>` from DeckFlow.Web.Tests to DeckFlow.Studio (no new NuGet package), acyclic (Studio -> Core only)
  - RoundTripHarness — PG schema pre-create (D-02), real git temp-repo bootstrap (D-03), /app deploy-copy, DECKFLOW_REPO_ROOT lifecycle (CM3), in-memory IConfiguration
  - RoundTripSeams — CannedLlmDistillationService, RecordingSshArtifactUploader, AppTreeDeployedBodyConfirmer, FixtureProdReader/FixtureProdStoreFactory (all deterministic, network-free)
  - RoundTripSmokeTests — [PostgresFact] boot proof: real PG schema + real git bootstrap + one canned distill hop writing a hash-verified LOCAL row
affects: [93-02-round-trip-assertions, 93-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Test-only ArgumentList-only git bootstrap helper (init/config/bare-origin/initial-push) as the SOLE hand-rolled ProcessStartInfo carve-out; every subsequent git op routes through the real GitRepository"
    - "Two-store harness shape: CreateLocalStore (SQLite, distill+publish-export SOURCE) vs CreateProdStore (Postgres, schema-ensure OFF, mirrors ProdStoreFactory) kept as distinct instances"
    - "Fixture doubles wrap the SAME real Postgres store instance (FixtureProdReader/FixtureProdStoreFactory) rather than faking data — only the transport is a double"

key-files:
  created:
    - DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripHarness.cs
    - DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSeams.cs
    - DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSmokeTests.cs
  modified:
    - DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj

key-decisions:
  - "DeployToAppAsync is a plain recursive filesystem copy of content-kb/**, not a second git invocation — keeps the test-only git bootstrap helper as the SOLE hand-rolled ProcessStartInfo in the harness"
  - "ArtifactPathSafety (DeckFlow.Studio.Services, internal to that assembly) is mirrored inline in AppTreeDeployedBodyConfirmer rather than exposed via a new InternalsVisibleTo, preserving zero-production-code-change this phase"
  - "Smoke test wires the real ContentKbOrchestrator via the existing public ContentKbOrchestratorFactory.Create (not 14 raw ctor args) — matches the CLI's own construction path and keeps the test from re-deriving the store graph"

requirements-completed: [SYNC-16]

# Metrics
duration: ~25min
completed: 2026-07-10
---

# Phase 93 Plan 01: Round-Trip Harness Infrastructure Summary

**Built the reusable SYNC-16 round-trip test harness (real Postgres schema, real git temp-repo, deterministic transport doubles) and proved it boots end-to-end with one canned distill hop — zero production-code change.**

## Performance

- **Duration:** ~25 min
- **Tasks:** 3 completed
- **Files modified:** 4 (1 csproj + 3 new test files)

## Accomplishments

- Added the single missing `DeckFlow.Studio` project-reference edge so `DeckFlow.Web.Tests` reaches all three app assemblies (Core + Web + Studio), confirmed acyclic (Studio references only Core)
- Built `RoundTripHarness`: PG schema pre-create over a schema-ensuring store (D-02), a schema-ensure-OFF prod store + distinct local SQLite store (D-02a), a real `git init` temp-repo bootstrap with deterministic identity + local bare origin + initial push so `origin/main` exists before any coordinator push (D-03/CH1/CH2), `DECKFLOW_REPO_ROOT` set/restore (CM3), a plain-filesystem `/app` deploy-copy, and an in-memory `IConfiguration` pointing `ContentKb:ContentBase` at `/app`
- Built four deterministic, network-free seams: `CannedLlmDistillationService` (non-drop verdict + populated `Usage`, D-04), `RecordingSshArtifactUploader` (D-05), `AppTreeDeployedBodyConfirmer` (prod-row `ArtifactPath` → `/app` hash match, fail-closed, D-06), and `FixtureProdReader`/`FixtureProdStoreFactory` wrapping the SAME real Postgres store (D-02a)
- Proved the harness boots with a `[PostgresFact]` smoke test: real PG schema + real git bootstrap + one canned distill run over a seeded source/video/transcript, asserting the LOCAL store's `body_sha256` equals `ComputeBodySha256` recomputed over the written artifact body

## Task Commits

1. **Task 1: Add DeckFlow.Studio ProjectReference + PG schema pre-create path (D-01, D-02)** - `a265eec3` (feat)
2. **Task 2: Real-git temp-repo + /app deploy-copy + deterministic seams (D-03, D-04, D-05, D-06, D-02a)** - `854914b9` (feat)
3. **Task 3: Harness boot smoke [PostgresFact] (D-02, D-03, D-07 gate behavior)** - `e7015cd1` (test)

## Files Created/Modified

- `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` - one new `<ProjectReference>` to `DeckFlow.Studio.csproj`, no new `<PackageReference>`
- `DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripHarness.cs` - PG schema pre-create/prod-store/local-store construction, real git temp-repo bootstrap, `/app` deploy-copy, env lifecycle, in-memory configuration
- `DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSeams.cs` - `CannedLlmDistillationService`, `RecordingSshArtifactUploader`, `AppTreeDeployedBodyConfirmer`, `FixtureProdReader`, `FixtureProdStoreFactory`
- `DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSmokeTests.cs` - `[PostgresFact]` boot proof wiring the real `ContentKbOrchestrator` over the harness's local store

## Decisions Made

- `DeployToAppAsync` copies the committed `content-kb/**` tree with a plain recursive filesystem copy rather than a second git invocation, keeping the test-only bootstrap helper the sole hand-rolled `ProcessStartInfo` in the harness (per the plan's acceptance criterion).
- `AppTreeDeployedBodyConfirmer`'s path-containment check mirrors `ArtifactPathSafety` (an `internal` type in `DeckFlow.Studio.Services` with no `InternalsVisibleTo` to `DeckFlow.Web.Tests`) inline rather than adding a new `InternalsVisibleTo` to production code — preserves the zero-production-code-change constraint for this phase.
- The smoke test wires `ContentKbOrchestrator` via the existing public `ContentKbOrchestratorFactory.Create(connection, artifactRoot, distiller, lister, transcriptSource, chunker)` — the same construction path the CLI uses — rather than hand-assembling all 14 orchestrator constructor dependencies, since the factory already builds every local store from one shared connection.

## Deviations from Plan

None - plan executed exactly as written. All acceptance criteria satisfied:
- `DeckFlow.Web.Tests.csproj` has exactly one new `<ProjectReference>` to `DeckFlow.Studio.csproj`, no new `<PackageReference>`.
- `DeckFlow.Studio.csproj` references only `DeckFlow.Core` — no circular reference.
- `RoundTripHarness` bootstraps the temp repo (init/config/bare-origin/initial-push/tracking) so `origin/main` exists, then would drive the real `GitRepository` for loop ops in 93-02 (no `FakeGitRepository` in the folder; the only hand-rolled `ProcessStartInfo` is the scoped bootstrap helper).
- `RoundTripSeams` contains all four required doubles with the required behaviors (non-drop verdict + populated `Usage`, no process launch; recording no-transfer upload; fail-closed hash-match confirmer; fixture reader/factory over the real PG store).
- No second hashing scheme was introduced (`ComputeBodySha256` is the only body-hash call in the folder).
- No production `.cs` file was modified — `git diff --stat` across all three commits touches only the csproj and the three new `RoundTrip/*.cs` test files.
- `dotnet.exe build DeckFlow.sln` is 0 warnings / 0 errors after every task.
- The `[PostgresFact]` auto-skips both at discovery time (no `DECKFLOW_POSTGRES_TESTS=1`) and at runtime (env var set but Docker unavailable, via the fixture's `SkipException`) — both paths verified locally in this environment (Docker was not running here).

## Known Stubs

None — this is test-only infrastructure; no production UI/data-flow stubs were introduced.

## Threat Flags

None — every threat identified in the plan's `<threat_model>` (T-93-01 git tampering, T-93-02 PG connection-string disclosure) was mitigated exactly as specified (ArgumentList-only git bootstrap scoped to this file; throwaway Testcontainers container credentials). No new surface outside the threat model was introduced.

## Self-Check: PASSED

- FOUND: DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj
- FOUND: DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripHarness.cs
- FOUND: DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSeams.cs
- FOUND: DeckFlow.Web.Tests/Integration/RoundTrip/RoundTripSmokeTests.cs
- FOUND commit: a265eec3
- FOUND commit: 854914b9
- FOUND commit: e7015cd1
