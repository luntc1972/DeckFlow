---
phase: 95
slug: measured-style-extractor
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-11
---

# Phase 95 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Core.Tests / DeckFlow.Web.Tests) |
| **Config file** | none — existing test projects cover this phase |
| **Quick run command** | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~MeasuredStyle` |
| **Full suite command** | `dotnet test DeckFlow.sln` |
| **Estimated runtime** | ~60 seconds (Core) / longer full-solution |

---

## Sampling Rate

- **After every task commit:** Run the quick filtered command for the touched area.
- **After every plan wave:** Run the full suite command.
- **Before `/gsd:verify-work`:** Full suite must be green.
- **Max feedback latency:** ~60 seconds (Core filtered run).

---

## Per-Task Verification Map

*Planner populates one row per task from the finalized PLAN.md files.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 95-01-01 | 01 | 1 | CS-04a | — | N/A | unit | `dotnet test DeckFlow.Web.Tests` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Test stubs for the pure extraction contract (staple-strip, category counting, lift math, folder weighting) in `DeckFlow.Core.Tests`.
- [ ] Snail 39-deck fixture corpus for round-trip extractor validation (D-12).

*Existing xUnit infrastructure covers the framework; no install needed.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Archidekt `ownerUsername` crawl against a real creator | CS-04a/b | External API, non-deterministic, rate-limited | Run crawler harness against Salubrious Snail; confirm deck-ID list + folder tags returned |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
