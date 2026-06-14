---
phase: 49
slug: dapper-data-access-adoption
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-14
---

# Phase 49 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Core.Tests + DeckFlow.Web.Tests) |
| **Config file** | none — existing test projects |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` (0/0 gate; VSTest unreliable in WSL) |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests` + `... test DeckFlow.Web.Tests` (run from Windows / CI; SQLite side) |
| **PG parity command** | `PostgresFactAttribute`-gated tests via `PostgresContainerFixture` (gated manual / env-flagged — NOT default CI) |
| **Estimated runtime** | ~build <60s; SQLite suite minutes; PG container start adds ~10-20s |

---

## Sampling Rate

- **After every task commit:** `dotnet build DeckFlow.sln` clean (0 errors / 0 new warnings)
- **After every plan wave:** Full `DeckFlow.Core.Tests` + `DeckFlow.Web.Tests` (SQLite) green; per-store tests for the converted store
- **After the spike (Wave 1 / FeedbackStore):** Spike gate evaluated, `49-GATE-VERDICT.md` written (PASS proceeds; FAIL halts)
- **Before `/gsd:verify-work`:** Full suite green on SQLite + PG round-trip parity test (REQ-2) green on both providers
- **Max feedback latency:** build <60s; suite per wave

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| (planner fills) | — | — | REQ-1..6 | — | N/A (mechanism swap, no new surface) | unit | `dotnet test` | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Planner: every conversion task maps to its store's existing test class on the SQLite side; the REQ-2 round-trip test (`DapperTypeHandlerRoundTripTests.cs`) is the dedicated dual-provider parity proof.*

---

## Wave 0 Requirements

- [ ] `DeckFlow.Web.Tests/DapperTypeHandlerRoundTripTests.cs` — REQ-2 round-trip stub (DateTime/decimal/bool/Guid, SQLite + PG via `PostgresFactAttribute`)
- [ ] Existing per-store test classes cover REQ-3..6 (FeedbackStore, CategoryKnowledgeRepository, Content/* stores already have SQLite harnesses)
- [ ] `PostgresContainerFixture` already present — no new test package

*Existing infrastructure covers all phase requirements except the new round-trip test above.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Postgres-side parity of converted stores | REQ-2, REQ-6 | CI does not spin a PG container; PG tests self-skip unless env-flagged; VSTest unreliable in WSL | Run `PostgresFactAttribute` tests with the PG env flag set (Testcontainers spins the container); confirm round-trip equality + 0 new failures |
| Spike gate verdict | REQ-3 | Human judgement on "zero store-local conversion" criterion (a)(b)(c) | After FeedbackStore conversion, grep for `GetInt64`/`GetBoolean`/`Parse`/`ToString("O")` in FeedbackStore.cs = 0; record PASS/FAIL in `49-GATE-VERDICT.md` |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (round-trip test)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s (build gate)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
