---
phase: 77
slug: multi-axis-deck-score
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-29
---

# Phase 77 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (DeckFlow.Core.Tests for scoring logic, DeckFlow.Web.Tests for prompt parity) |
| **Config file** | none — existing test projects |
| **Quick run command** | `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~Score"` |
| **Full suite command** | `dotnet.exe test` |
| **Estimated runtime** | ~120 seconds (full) |

---

## Sampling Rate

- **After every task commit:** Run the quick scoped Score filter
- **After every plan wave:** Run the full suite
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 77-01-T1 | 01 | 0 | SCORE-02 | — | N/A | unit | `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~DeckStatClassifier"` | ❌ W0 | ⬜ pending |
| 77-01-T2 | 01 | 0 | SCORE-02 | — | N/A | unit | `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~DeckStat"` | ❌ W0 | ⬜ pending |
| 77-02-T1 | 02 | 1 | SCORE-01, SCORE-02, SCORE-03 | — | N/A | unit | `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~MultiAxisScorer"` | ❌ W0 | ⬜ pending |
| 77-02-T2 | 02 | 1 | SCORE-01, SCORE-03 | — | golden | unit | `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~MultiAxisScorer"` (cEDH Power/Speed >= casual) | ❌ W0 | ⬜ pending |
| 77-03-T1 | 03 | 0 | SCORE-04 | — | N/A | unit | `dotnet.exe build DeckFlow.Web` | ❌ W0 | ⬜ pending |
| 77-03-T2 | 03 | 0 | SCORE-04 | — | N/A | unit | `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~AnalysisScorePromptParity"` | ❌ W0 | ⬜ pending |
| 77-04-T1 | 04 | 2 | SCORE-04 | — | flag default-OFF | unit | `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~Flag"` | ❌ W0 | ⬜ pending |
| 77-04-T2 | 04 | 2 | SCORE-01..04 | T-77-04-01 | ScoreJson round-trip into typed record | unit | `dotnet.exe test DeckFlow.Web.Tests` | ❌ W0 | ⬜ pending |
| 77-04-T3 | 04 | 2 | SCORE-01..04 | T-77-04-01/02 | combos-always-on; HTML-encode | unit | `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~DeckAnalysisScoreBlockText"` | ❌ W0 | ⬜ pending |
| 77-05-T1 | 05 | 3 | SCORE-01, SCORE-04 | T-77-05-02 | flag-OFF byte-identity | unit | `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~DeckAnalysisScoreView"` | ❌ W0 | ⬜ pending |
| 77-05-T2 | 05 | 3 | SCORE-04 | — | CSS in site-common.css only | unit | `dotnet.exe build DeckFlow.Web` | ❌ W0 | ⬜ pending |
| 77-05-T3 | 05 | 3 | SCORE-01, SCORE-04 | T-77-05-02 | render OFF omits / ON contains | unit | `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~DeckAnalysisScoreView"` | ❌ W0 | ⬜ pending |
| 77-06-T1 | 06 | 4 | SCORE-04 | — | N/A | docs | `dotnet.exe build` (README only) | ❌ W0 | ⬜ pending |
| 77-06-T2 | 06 | 4 | SCORE-04 | — | N/A | checkpoint:human-verify | manual cross-theme/mobile sign-off | ❌ W0 | ⬜ pending |

*Map derived from PLAN.md tasks 77-0{1..6}. Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky · File Exists flips to ✅ as Wave-0 stubs land.*

---

## Wave 0 Requirements

- [ ] Golden-test fixtures: a known cEDH deck and a known battlecruiser deck (SCORE-03 cross-check) — reuse existing test deck fixtures where present
- [ ] Score banding unit-test stubs in DeckFlow.Core.Tests
- [ ] Prompt-variant parity test stub in DeckFlow.Web.Tests (modelled on `BracketPromptVariantParityTests`)

*Existing xUnit infrastructure covers framework needs — no install required.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Score block renders in live `/deck-analysis` paste artifact across themes | SCORE-04 | Visual artifact correctness | Run web app, paste a deck, confirm four-axis block appears with inline rationale |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-06-29 (gsd-plan-checker Dimension 8 PASS)
