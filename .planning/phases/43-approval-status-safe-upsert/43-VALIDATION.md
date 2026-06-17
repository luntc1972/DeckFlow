---
phase: 43
slug: approval-status-safe-upsert
status: verified
nyquist_compliant: partial
wave_0_complete: true
created: 2026-06-13
---

# Phase 43 — Validation Strategy

> Per-phase validation contract. Reconstructed from artifacts post-execution (State B).
> All store-level controls have automated coverage and run green. Two orchestrator call-site switches are inspection-verified (manual-only) by operator decision.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x (.NET 10) |
| **Config file** | DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~ContentSiteIndexStoreApprovalTests"` |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` |
| **Estimated runtime** | ~8 s (Core.Tests full); ~0.2 s (approval filter) |

*Note: VSTest is documented as flaky in WSL; this run executed cleanly (Core 342/342, Web 602/6-skip). Push-and-watch CI is the backstop.*

---

## Sampling Rate

- **After every task commit:** quick run command (approval tests)
- **After every plan wave:** full Core.Tests suite
- **Before `/gsd:verify-work`:** full suite green (met — 342/342)
- **Max feedback latency:** ~8 s

---

## Per-Task Verification Map

| Plan | Wave | Requirement | Secure Behavior | Test Type | Automated Command | Test | Status |
|------|------|-------------|-----------------|-----------|-------------------|------|--------|
| 43-01 | 1 | REVQ-01 | approval_status column added via self-healing ALTER to legacy schema | integration (real SQLite) | quick run | `EnsureSchemaAsync_AddsApprovalStatusColumn_ToLegacySchema` | ✅ green |
| 43-01 | 1 | REVQ-01 | grandfather backfill: visible→approved, others→pending | integration | quick run | `EnsureSchemaAsync_Grandfather_SetsApprovedForVisibleRows_PendingForOthers` | ✅ green |
| 43-01 | 1 | REVQ-01 (T-43-02) | backfill idempotent — no re-stamp of operator-changed status across fresh-store re-run | integration | quick run | `EnsureSchemaAsync_Grandfather_DoesNotRestampOperatorChangedStatus` | ✅ green |
| 43-01 | 1 | REVQ-01 | DDL default 'pending' on insert without explicit status | integration | quick run | `ApprovalStatusColumn_DefaultsToPending_WhenInsertedWithoutExplicitStatus` (+ `CreateTableDdl_IncludesApprovalStatusDefault`) | ✅ green |
| 43-01 | 1 | PUB-01 | new row via safe overload lands 'pending' | integration | quick run | `UpsertContentColumnsOnlyAsync_NewRow_LandsAsPending` | ✅ green |
| 43-01 | 1 | PUB-01 | re-upsert preserves approval_status | integration | quick run | `UpsertContentColumnsOnlyAsync_ExistingRow_PreservesApprovalStatus` | ✅ green |
| 43-01 | 1 | PUB-01 | safe overload preserves is_visible + is_evergreen + approval_status (non-default) | integration | quick run | `UpsertContentColumnsOnlyAsync_PreservesVisibleEvergreenApprovedFields` | ✅ green |
| 43-01 | 1 | PUB-01 | safe overload preserves is_hidden=TRUE | integration | quick run | `UpsertContentColumnsOnlyAsync_PreservesHiddenRow` | ✅ green |
| 43-01 | 1 | PUB-02 (T-43-03 HIGH) | `GetApprovedRowsAsync` returns only approved; pending/rejected excluded | integration | quick run | `GetApprovedRowsAsync_ReturnsOnlyApprovedRows` | ✅ green |

---

## Wave 0 Requirements

Existing infrastructure (DeckFlow.Core.Tests xUnit + real-SQLite test pattern) covered all phase requirements. No Wave 0 setup needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Verification Instructions |
|----------|-------------|------------|---------------------------|
| `ContentKbOrchestrator.ExportIndexAsync` calls `GetApprovedRowsAsync` (not `GetAllRowsAsync`) | PUB-02 (D-08) | Wiring switch; orchestrator-level automated assertion deferred (operator decision 2026-06-13). The approved-only FILTER itself is automated (`GetApprovedRowsAsync_ReturnsOnlyApprovedRows`); only the 1-line delegation is inspection-covered. | Inspect `ContentKbOrchestrator.cs:610` → confirms `GetApprovedRowsAsync`. Verifier 4/4 confirmed. Behavioral backstop: pending/rejected rows absent from a produced `index-seed.json`. |
| Distill index-write calls `UpsertContentColumnsOnlyAsync` (not `UpsertRowAsync`) | PUB-01 (D-09) | Wiring switch; orchestrator-level automated assertion deferred. Safe-upsert SEMANTICS are automated (4 preservation facts); only the distill delegation is inspection-covered. RunDistillAsyncTests fake records the call (infra ready if revisited). | Inspect `ContentKbOrchestrator.cs:1052` → confirms `UpsertContentColumnsOnlyAsync`. Verifier 4/4 confirmed. |
| Postgres live-migration adds `approval_status` column | REVQ-01 (SC1, Postgres half) | CI is SQLite-only; no PG test harness. ALTER path structurally identical to 3 production-proven blocks (is_visible/is_evergreen/is_hidden). | Post-Render-deploy: confirm column exists in prod Postgres `content_site_index`. |

---

## Validation Audit 2026-06-13

| Metric | Count |
|--------|-------|
| Requirements | 3 (REVQ-01, PUB-01, PUB-02) |
| Store-level controls automated (green) | 9 facts |
| Gaps found | 2 (orchestrator wiring switches) |
| Resolved (automated) | 0 |
| Escalated to manual-only | 2 + 1 (Postgres deploy) |

---

## Validation Sign-Off

- [x] All requirements have automated verification of the core control (filter, safe-upsert, migration)
- [x] Sampling continuity: every plan task has an automated command
- [x] Wave 0 covers all MISSING references (none needed)
- [x] No watch-mode flags
- [x] Feedback latency < 10 s
- [ ] `nyquist_compliant: true` — set to **partial**: 2 orchestrator wiring switches accepted as manual-only (inspection + verifier 4/4), not automated

**Approval:** verified (partial) 2026-06-13
