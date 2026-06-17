---
phase: 45-harvest-distill-ui
plan: "02"
subsystem: Studio
tags: [di-composition, provider-decision, spend-ledger, badge-resolution, tdd]
dependency_graph:
  requires: ["45-01"]
  provides: [VideoStatus, VideoStatusResolver, StudioDistillConfig, SessionCapOverride, override-aware-ledger]
  affects: [DeckFlow.Core, DeckFlow.Studio, DeckFlow.Core.Tests]
tech_stack:
  added: []
  patterns: [sealed-record-config, app-scoped-singleton, factory-resolved-service, resolver-closure, in-file-fakes, tdd-red-green]
key_files:
  created:
    - DeckFlow.Core/Content/VideoStatus.cs
    - DeckFlow.Core/Content/VideoStatusResolver.cs
    - DeckFlow.Studio/StudioDistillConfig.cs
    - DeckFlow.Studio/SessionCapOverride.cs
    - DeckFlow.Core.Tests/VideoStatusResolverTests.cs
  modified:
    - DeckFlow.Studio/Program.cs
decisions:
  - "Single providerEnv read (builder.Configuration + Environment.GetEnvironmentVariable fallback) drives both LlmDistillationProviderFactory.Resolve AND isSubscriptionProvider — they cannot disagree (HIGH-1 closed)"
  - "SessionCapOverride is class not record (mutable state); documented as app-scoped (whole process), not per-circuit isolation (D-03)"
  - "Override-aware ledger uses a resolver closure capturing the capOverride singleton; same instance reaches orchestrator WouldExceedCapAsync (Pitfall 6 / T-45-04 mitigated)"
  - "VideoStatusResolver placed in DeckFlow.Core (not Studio) so Core.Tests can reference it without inverting the project dependency (HIGH-2)"
  - "youtube literal in LOW-1 warning comment removed; ContentSourceType.Youtube constant used throughout (LOW-1)"
metrics:
  duration: "~25 minutes"
  completed: "2026-06-15"
  tasks_completed: 2
  files_changed: 6
---

# Phase 45 Plan 02: Studio DI Composition Root + VideoStatusResolver

**One-liner:** Wired a single DECKFLOW_LLM_PROVIDER read to drive both the factory-resolved distiller and StudioDistillConfig.IsSubscriptionProvider (closing HIGH-1), added override-aware LLM spend ledger via a resolver closure, and extracted VideoStatus + VideoStatusResolver to DeckFlow.Core with four passing TDD-driven unit tests.

## What Was Built

### Task 1: Single provider decision + StudioDistillConfig + SessionCapOverride + override-aware ledger

**`DeckFlow.Studio/StudioDistillConfig.cs`** — `public sealed record StudioDistillConfig(bool IsSubscriptionProvider)`. Mirrors the `StudioConfig` sealed-record pattern exactly. Registered as a singleton in Program.cs from the resolved `isSubscriptionProvider` flag.

**`DeckFlow.Studio/SessionCapOverride.cs`** — `public sealed class SessionCapOverride` (class, not record — mutable state) with `public decimal? OverrideUsd { get; set; }`. Documented as app-scoped (whole Studio process, NOT per-circuit isolation). Resets to env/default on restart (D-03). Registered as a singleton before the ledger so the closure captures the reference.

**`DeckFlow.Studio/Program.cs`** changes:
- Added `using System.Globalization;`
- Read `DECKFLOW_LLM_PROVIDER` ONCE via `builder.Configuration` + env fallback; derived `isSubscriptionProvider` from the SAME variable (HIGH-1 — single source of truth)
- Replaced hardcoded `new LlmDistillationService(...)` with `LlmDistillationProviderFactory.Resolve(providerEnv, httpClient)` — factory drives the distiller
- Replaced single-line `ILlmSpendLedger` registration with override-aware resolver closure reading `capOverride.OverrideUsd` when set
- Added `SessionCapOverride` and `StudioDistillConfig` singleton registrations
- Added `VideoStatusResolver` singleton registration (Task 2)

### Task 2: VideoStatus enum + VideoStatusResolver (TDD: RED → GREEN)

**`DeckFlow.Core/Content/VideoStatus.cs`** — `public enum VideoStatus` with five members in UI-SPEC vocabulary order: `NotHarvested`, `Harvested`, `Distilled`, `Blocked`, `Duplicate`.

**`DeckFlow.Core/Content/VideoStatusResolver.cs`** — `public sealed class VideoStatusResolver` in DeckFlow.Core (not Studio — HIGH-2). Constructor takes all four store interfaces with `ArgumentNullException.ThrowIfNull` guards. `ResolveStatusAsync(string youtubeVideoId, CancellationToken ct = default)` implements the four-step resolution:
1. `IsBlockedAsync` → `Blocked` (wins over everything)
2. `GetByNaturalKeyAsync(ContentSourceType.Youtube, ...)` → `Distilled`
3. Iterate `ListEnabledSourcesAsync`, call `GetVideoByYoutubeIdAsync` per source → `Harvested` on first hit
4. → `NotHarvested`

Uses `ContentSourceType.Youtube` constant throughout; no raw string literal (LOW-1).

**`DeckFlow.Core.Tests/VideoStatusResolverTests.cs`** — `public sealed class VideoStatusResolverTests` with four `[Fact]` methods using in-file fakes (no mocking library):
- `ResolveStatusAsync_BlockedVideo_ReturnsBlocked` — blocked wins even when index row present
- `ResolveStatusAsync_SiteIndexRowPresent_ReturnsDistilled`
- `ResolveStatusAsync_FoundInSecondEnabledSource_ReturnsHarvested` — asserts resolver called source #1 (miss) then source #2 (hit), proving iteration not a no-op
- `ResolveStatusAsync_NotFoundInAnySources_ReturnsNotHarvested`

## TDD Gate Compliance

| Gate | Commit | Status |
|------|--------|--------|
| RED | 919a18b | Build failed — `VideoStatus` and `VideoStatusResolver` did not exist (CS0246 / CS0103 ×4 each) |
| GREEN | 0818c6a | 4/4 tests pass; full solution build succeeded 0 errors 0 warnings |
| REFACTOR | — | No refactor needed; implementation is minimal and clean |

## Decisions Made

### Single providerEnv read (HIGH-1 closure)
`builder.Configuration[LlmDistillationProviderFactory.EnvironmentVariableName] ?? Environment.GetEnvironmentVariable(...)` is read once. Both `LlmDistillationProviderFactory.Resolve(providerEnv, ...)` and the `isSubscriptionProvider` derivation use this same variable. Replicates the pattern from `DeckFlow.CLI/ContentKbCommandRunners.cs` lines 95-97 exactly.

### Override-aware ledger as single shared singleton (T-45-04)
`var capOverride = new SessionCapOverride()` is constructed before DI registration. The resolver closure `key => { if (key == "DECKFLOW_LLM_MONTHLY_CAP_USD" && capOverride.OverrideUsd.HasValue) return ...; return null; }` captures the same reference. The orchestrator receives this single ledger instance so `WouldExceedCapAsync` sees the override (Pitfall 6).

### VideoStatusResolver in Core not Studio (HIGH-2)
If placed in Studio, `DeckFlow.Core.Tests` would need a project reference to Studio, inverting the dependency graph. Core is the correct home for pure store-query logic with no Blazor dependency.

## Deviations from Plan

None — plan executed exactly as written.

## Test Results

| Test suite | Filter | Result |
|---|---|---|
| `VideoStatusResolverTests` | `FullyQualifiedName~VideoStatusResolverTests` | 4/4 Passed |
| Full Core.Tests suite | (no filter) | 355/355 Passed |
| Full solution build | `dotnet build DeckFlow.sln` | Build succeeded — 0 errors, 0 warnings |

## Known Stubs

None — this plan adds no UI, no placeholder data, and no wired-but-empty paths.

## Threat Flags

No new network endpoints, auth paths, file access patterns, or schema changes introduced.

T-45-03 (SessionCapOverride elevation): XML doc states app-scoped reality; no false per-circuit isolation claim. Mitigated as planned.
T-45-04 (duplicate ledger): Exactly one `new LlmSpendLedger(contentKbDatabasePath, ...)` in Program.cs; acceptance verified.
T-45-17 (distiller/flag mismatch — HIGH-1): `LlmDistillationProviderFactory.Resolve(providerEnv` present; `new LlmDistillationService` absent in Program.cs; single providerEnv drives both. Closed.
T-45-05 (provider/cap in logs): No new log statements emit DECKFLOW_LLM_PROVIDER or cap values.
T-45-SC (no package installs): Confirmed — no NuGet packages added; in-file fakes only.

## Self-Check: PASSED

- `DeckFlow.Core/Content/VideoStatus.cs` — FOUND
- `DeckFlow.Core/Content/VideoStatusResolver.cs` — FOUND
- `DeckFlow.Studio/StudioDistillConfig.cs` — FOUND
- `DeckFlow.Studio/SessionCapOverride.cs` — FOUND
- `DeckFlow.Core.Tests/VideoStatusResolverTests.cs` — FOUND
- `DeckFlow.Studio/Program.cs` — MODIFIED (verified)
- Commit 4573c1f (Task 1) — FOUND
- Commit 919a18b (Task 2 RED) — FOUND
- Commit 0818c6a (Task 2 GREEN) — FOUND
