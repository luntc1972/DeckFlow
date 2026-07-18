---
phase: 98
slug: card-grounding-guard
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-18
---

# Phase 98 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Guard decision logic (fuzzy-result classification, legality/color-identity/castability
> checks, whitelist assembly) is deterministic and MUST be fully unit-tested, including
> known-hallucination fixtures (CS-25). Scryfall HTTP is exercised only through the
> existing test seams (delegate-injection ctors / MockHttp) — no live network in tests.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x (`DeckFlow.Core.Tests` + `DeckFlow.Web.Tests`) — .NET 10 |
| **Config file** | existing `*.Tests.csproj` (both projects present) |
| **Quick run command** | `dotnet.exe test DeckFlow.Web.Tests --filter FullyQualifiedName~Grounding` (plus `~Grounding` filter on Core.Tests for pure-Core checks) |
| **Full suite command** | `dotnet.exe test DeckFlow.Core.Tests && dotnet.exe test DeckFlow.Web.Tests` |
| **Estimated runtime** | ~60–120 seconds (both suites) |

> **WSL caveat (project rule):** VSTest is unreliable under WSL. Primary gate is
> `dotnet.exe build` clean + targeted `dotnet.exe test` from the Windows host, with
> push-and-watch CI as the authoritative backstop. Use the Windows `dotnet.exe`, not the
> WSL `dotnet`. This phase has **no user-facing UI surface** (reusable service consumed
> by Phase 99) — the web-page tests+themes+mobile rule does not apply.

---

## Sampling Rate

- **After every task commit:** Run quick command (Grounding-filtered tests)
- **After every plan wave:** Run both full suites
- **Before `/gsd:verify-work`:** Both suites green + no regression in existing Scryfall service tests (shared throttle/pipeline untouched or still green)
- **Max feedback latency:** ~120 seconds

---

## Per-Task Verification Map

*Populated by the planner — one row per task with requirement (CS-21..CS-25), threat ref,
test type, and automated command.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| — | — | — | — | — | — | — | — | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements — both test projects, MockHttp,
delegate-injection seams, and `[InternalsVisibleTo]` are already in place. No Wave 0 setup.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Scryfall fuzzy 404/ambiguous shape still matches research capture | CS-21 | External API contract — already live-verified 2026-07-18 (98-RESEARCH.md); re-probe only if Scryfall changes | `curl 'https://api.scryfall.com/cards/named?fuzzy=...'` per 98-RESEARCH.md probes |
