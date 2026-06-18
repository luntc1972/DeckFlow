---
phase: 54
slug: feature-debt
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-17
---

# Phase 54 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Web.Tests, DeckFlow.Core.Tests) |
| **Config file** | none — existing test projects |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` (build-clean gate; VSTest unreliable in WSL) |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests` (run from Windows when WSL VSTest flakes) |
| **Estimated runtime** | ~60-120 seconds build; suite varies |

---

## Sampling Rate

- **After every task commit:** Build clean (`dotnet build DeckFlow.sln`)
- **After every plan wave:** Run affected test project (`DeckFlow.Web.Tests`)
- **Before `/gsd:verify-work`:** Full suite green + build 0 errors/0 new warnings + changed-lines format gate clean
- **Max feedback latency:** ~120 seconds (build)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 54-01-xx | 01 | 1 | FEAT-02 | T-54-01 | Untrusted commanderspellbook JSON parsed defensively; absent ranking fields degrade to null (no throw) | unit | `dotnet test DeckFlow.Web.Tests` (CommanderSpellbookServiceTests) | ✅ existing | ⬜ pending |
| 54-01-xx | 01 | 1 | FEAT-02 | — | Combos ranked popularity DESC, manaValueNeeded ASC tiebreak, unknown-cost last | unit | `dotnet test DeckFlow.Web.Tests` (DeckPrimerPacketServiceTests) | ✅ existing | ⬜ pending |
| 54-02-xx | 02 | 1 | FEAT-01 | T-54-02 | Gemini artifact size measured ≤ documented limit across 4 workflows; oversize surfaced as finding not truncated-silently | harness/manual | size-measurement harness or test; record result | ⚠️ W0 (harness) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Repeatable Gemini artifact size-measurement seam for the four workflows (analysis, comparison, meta-gap, primer) — prefer an xUnit test asserting char count of each generated Gemini prompt against the documented ceiling, reusing existing service test seams.

*FEAT-02 needs no new infra — `CommanderSpellbookServiceTests` and `DeckPrimerPacketServiceTests` exist.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Gemini radio appears in selector + artifacts paste into real Gemini within limit | FEAT-01 | Real Gemini paste behavior is external; flag stays default-off | Set `DECKFLOW_GEMINI_ENABLED=true` locally, generate each of the 4 workflow packets, paste Gemini artifact into Gemini, confirm no truncation. Record sizes. |

*The char-count proxy is automated (Wave 0); the live paste is the manual confirmation.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
