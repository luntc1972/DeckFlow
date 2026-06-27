---
phase: 56
slug: studio-surfaces
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-06-18
---

# Phase 56 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + bUnit 2.7.2 (DeckFlow.Studio.Tests) / xUnit (DeckFlow.Core.Tests) |
| **Config file** | DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj |
| **Quick run command** | `dotnet build DeckFlow.sln` |
| **Full suite command** | `dotnet test DeckFlow.Studio.Tests && dotnet test DeckFlow.Core.Tests` |
| **Estimated runtime** | ~60 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build DeckFlow.sln`
- **After every plan wave:** Run `dotnet test DeckFlow.Studio.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command |
|---------|------|------|-------------|-----------|-------------------|
| 56-01-01 | 01 | 1 | BROWSE-02 | build | `dotnet build DeckFlow.Core/DeckFlow.Core.csproj` |
| 56-01-02 | 01 | 1 | BROWSE-02 (+ SC5 unblock->NotHarvested loop) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~VideoStatusResolverTests` |
| 56-02-01 | 02 | 1 | PUB-03 | build | `dotnet build DeckFlow.Studio/DeckFlow.Studio.csproj` |
| 56-02-02 | 02 | 1 | PUB-03 | unit bUnit | `dotnet test DeckFlow.Studio.Tests --filter FullyQualifiedName~ReviewPageTests` |
| 56-02-03 | 02 | 1 | PUB-03 | unit bUnit | `dotnet test DeckFlow.Studio.Tests --filter FullyQualifiedName~PublishPageTests` |
| 56-03-01 | 03 | 1 | REM-02 | build | `dotnet build DeckFlow.Studio.Tests/DeckFlow.Studio.Tests.csproj` |
| 56-03-02 | 03 | 1 | REM-02 | build | `dotnet build DeckFlow.Studio/DeckFlow.Studio.csproj` |
| 56-03-03 | 03 | 1 | REM-02 | unit bUnit | `dotnet test DeckFlow.Studio.Tests --filter FullyQualifiedName~BlockedPageTests` |
| 56-04-01 | 04 | 2 | REM-01, BROWSE-02 | build | `dotnet build DeckFlow.Studio/DeckFlow.Studio.csproj` |
| 56-04-02 | 04 | 2 | ADD-01 | build | `dotnet build DeckFlow.Studio/DeckFlow.Studio.csproj` |
| 56-04-03 | 04 | 2 | BROWSE-01, BROWSE-03, REM-01 (+ SC1 browse-Blocked, SC4 block Success==false failure), ADD-01 | unit bUnit | `dotnet test DeckFlow.Studio.Tests --filter FullyQualifiedName~HarvestPageTests` |

*Status legend: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

All test scaffolding is created inside plan tasks (no separate Wave 0 plan is required):

- 56-01-02 creates the new `VideoStatusResolverTests` Approved/Published cases plus the
  `ResolveStatusAsync_UnblockedWithNoIndexOrHarvest_ReturnsNotHarvested` case (SC5 unblock->re-browse loop) (BROWSE-02).
- 56-02-02 / 56-02-03 add the publish-state column bUnit cases to `ReviewPageTests` / `PublishPageTests` (PUB-03).
- 56-03-01 wires the canned `IContentMaintenanceOrchestrator` returns on `FakeContentKbOrchestrator`; 56-03-03 creates `BlockedPageTests` (REM-02).
- 56-04-03 creates `HarvestPageTests` covering REM-01 block confirm SUCCESS, the block `Success==false`
  result-failure path (operator-safe error, badge unchanged, confirm cleared — SC4), the SC1 browse-time
  Blocked badge (`HarvestPage_ChannelBrowse_BlockedVideoRendersBlockedBadge`), ADD-01 zero-resolved, the new
  badge arms, and the BROWSE-03 multi-select harvest regression.

Every requirement maps to at least one `<automated>` verify created within these tasks; there are no MISSING test references, so `wave_0_complete: true`.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real YouTube channel browse + multi-select harvest end-to-end | BROWSE-01 / BROWSE-03 | Needs live YouTube + LLM spend | Operator runs Studio against a real channel; bUnit covers the selection→harvest wiring |
| Real Block hard-delete against a populated local KB | REM-01 | Destructive; needs real `content-kb.db` artifacts | Operator blocks a real harvested video and confirms artifacts + index row removed |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (created within plan tasks)
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** ready
