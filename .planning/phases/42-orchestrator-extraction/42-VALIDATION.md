---
phase: 42
slug: orchestrator-extraction
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-06-13
---

# Phase 42 — Validation Strategy

> Per-phase validation contract. Behavior-preserving extraction — validation centers on parity (the extracted Core logic behaves byte-identically to the old CLI) + DI resolvability.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Core.Tests) |
| **Config file** | DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~Orchestration"` |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` |
| **Estimated runtime** | ~7–9 seconds (332 tests) |

*Note: VSTest is unreliable in WSL generally, but Windows `dotnet.exe` from WSL runs the suite cleanly (verified 332/332 this phase).*

---

## Sampling Rate

- **After every task commit:** quick run (`~Orchestration` filter)
- **After every plan wave:** full Core.Tests suite
- **Before `/gsd:verify-work`:** full suite green
- **Max feedback latency:** ~9 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 42-01 | 01 | 1 | ORCH-01 | T-42-01 | Null-safe contracts; required Success; no Console/Serilog | build + transitive | full suite (contracts exercised by orchestrator tests) | ✅ | ✅ green |
| 42-02 | 02 | 2 | ORCH-01 | T-42-03/04/04b | Validators-before-write; spend record-before-next-call; AddSource invalid-type short-circuit | unit | RunDistillAsyncTests, CommandRunnerHarvestTests, CommandRunnerCorpusResetTests, BlockedVideoStoreTests | ✅ | ✅ green |
| 42-03 | 03 | 3 | ORCH-02 | T-42-06/07/08 | Thin adapters; exit-code mapping; sync progress; no conn-string in Core | unit | CommandRunnerValidateClipsTests + 4 re-pointed seam classes | ✅ | ✅ green |
| 42-03 (DI) | 03 | 3 | ORCH-02 | T-42-14 | AddContentKbOrchestrator forwards facade + 5 slices to one scoped instance | unit | `AddContentKbOrchestratorDiTests` (Assert.Same ×6 + cross-scope NotSame) | ✅ | ✅ green |
| 42-04 | 04 | 4 | ORCH-02 | T-42-09/10/11/14 | Studio resolves the maintenance slice; no CLI ref; prod conn presence-only | manual/runtime + grep | Studio launch (`:5271` started, ctor resolved) + `grep DeckFlow.CLI DeckFlow.Studio/` = 0 | ✅ (runtime) | ✅ green (manual) |
| 42-05 | 05 | 4 | ORCH-02 | T-42-12/13 | Exit-code/output parity + byte-identical JSON seed | unit | ContentSource/ContentMaintenance/DistillOrchestratorParityTests + ContentIndexExportJsonGoldenTests | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing xUnit infrastructure covers all phase requirements. One in-solution package added this phase: `Microsoft.Extensions.DependencyInjection` 10.0.0 (to DeckFlow.Core.Tests, for the DI-resolution test) — in-solution family, no approval gate.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Studio composition root boots + resolves the full orchestrator ctor (SC4) | ORCH-02 | Host startup/composition root is not meaningfully unit-testable without a full Blazor host; the DI-forwarding *logic* is now unit-covered (AddContentKbOrchestratorDiTests), but the real Studio wiring (local SQLite stores + integration services) is a runtime concern. | `MTG_DATA_DIR="$(pwd)/artifacts" "/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.Studio` → expect `Now listening on: http://localhost:5271`, `Studio prod connection: not configured`, no startup exception. (Run + verified 2026-06-13.) |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or runtime/manual coverage
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (DI-forwarding gap filled)
- [x] No watch-mode flags
- [x] Feedback latency < 10s
- [x] `nyquist_compliant: true` set in frontmatter

## Validation Audit 2026-06-13
| Metric | Count |
|--------|-------|
| Gaps found | 1 (ORCH-02 DI forwarding, automated) |
| Resolved | 1 (AddContentKbOrchestratorDiTests, +2 tests) |
| Manual-only | 1 (Studio SC4 composition root — runtime-verified) |

**Approval:** approved 2026-06-13
