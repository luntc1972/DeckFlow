---
phase: 99
slug: creator-style-artifact-engine
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-18
---

# Phase 99 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`) |
| **Config file** | None dedicated — standard `dotnet test` via each `.csproj` |
| **Quick run command** | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CreatorStyleRubricScorerTests` (per-class `--filter` on the touched test class) |
| **Full suite command** | `dotnet build` (clean, per project convention — VSTest unreliable in WSL) then `dotnet test DeckFlow.Core.Tests DeckFlow.Web.Tests` |
| **Estimated runtime** | Quick filter runs < 60s; full suite ~3-5 min (1300+ Web tests) |

---

## Sampling Rate

- **After every task commit:** Run the targeted `--filter` command for the touched test class(es)
- **After every plan wave:** Run `dotnet test DeckFlow.Core.Tests DeckFlow.Web.Tests` (full suite)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** < 60 seconds for targeted filter runs

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 99-01-01 | 01 | 1 | CS-27 | T-99-01 | Metric-key join uses `StatedMetricKeyMapper` vocabulary verbatim; unknown keys score as no-match, never throw | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CreatorStyleRubricScorerTests` | ❌ W0 | ⬜ pending |
| 99-01-02 | 01 | 1 | CS-28 | — | Exemplar selection is deterministic (stable ordering) from fixture corpus | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CreatorDeckExemplarSelectorTests` | ❌ W0 | ⬜ pending |
| 99-02-01 | 02 | 2 | CS-27 | — | Category/combo stats built via same `CategoryKnowledgeRepository` path as fused profile (apples-to-apples) | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~SubmittedDeckStatsBuilderTests` | ❌ W0 | ⬜ pending |
| 99-02-02 | 02 | 2 | CS-27, CS-29 | T-99-02 | Karsten stats via `ManabaseClassifier.Classify(isSingleton:true)` + `ManabaseAnalyzer.Analyze(Casual)` parity path; `CardGroundingDeckContext` built in same Scryfall pass | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~SubmittedDeckStatsBuilderTests` | ❌ W0 | ⬜ pending |
| 99-03-01 | 03 | 3 | CS-26, CS-29 | T-99-02 | Single batched `ICardGroundingGuard.ValidateAllAsync` gate; fail-closed — rejected/upstream-unavailable cards excluded and flagged, never emitted | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CreatorStylePacketServiceTests` | ❌ W0 | ⬜ pending |
| 99-03-02 | 03 | 3 | CS-28 | T-99-01 | All 5 artifact elements present; "critique only with the provided cards" instruction constrains LLM to guard-validated names | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CreatorStylePacketServiceTests` | ❌ W0 | ⬜ pending |
| 99-03-03 | 03 | 3 | CS-26 | T-99-03 | DI graph resolves cleanly; tripwire test extended to `CreatorStylePacketService` ctor graph (Phase 98-05 lesson) | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CreatorStyleDiRegistrationTests` | ✅ existing, extend | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*
*Threat refs map to the `<threat_model>` blocks in each plan: T-99-01 = creator-sourced content injected into artifact text (prompt-injection surface), T-99-02 = unverified card names reaching artifact (grounding bypass / upstream failure), T-99-03 = DI misregistration causing runtime cold-start failure.*

---

## Wave 0 Requirements

- [ ] `DeckFlow.Core.Tests/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorerTests.cs` — covers CS-27 (created in 99-01)
- [ ] `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorDeckExemplarSelectorTests.cs` — covers CS-28 selection half (created in 99-01)
- [ ] `DeckFlow.Web.Tests/Services/CreatorStyle/SubmittedDeckStatsBuilderTests.cs` — covers CS-27/CS-29 context half (created in 99-02)
- [ ] `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStylePacketServiceTests.cs` — covers CS-26, CS-28, CS-29, SC #4 (created in 99-03)
- [ ] Extend existing `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStyleDiRegistrationTests.cs` with `CreatorStylePacketService` ctor graph (99-03 Task 3)
- No new test framework/config needed — xUnit already fully configured in both test projects. Tests are created in the same plan/wave as their implementation (test-with-implementation convention), so there is no standalone Wave 0 plan.

---

## Manual-Only Verifications

All phase behaviors have automated verification. (Engine-only phase: no controller, page, or UI surface exists yet — nothing requires browser/manual checks.)

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (each test file created alongside its implementation plan)
- [x] No watch-mode flags
- [x] Feedback latency < 60s for targeted runs
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-07-18
