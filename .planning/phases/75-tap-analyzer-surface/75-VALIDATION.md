---
phase: 75
slug: tap-analyzer-surface
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-28
---

# Phase 75 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Core.Tests, DeckFlow.Web.Tests) |
| **Config file** | none — existing test projects |
| **Quick run command** | `dotnet build DeckFlow.sln` (build-clean gate; VSTest unreliable in WSL per CLAUDE.md) |
| **Full suite command** | `dotnet test` via dotnet.exe over WSL, or push-and-watch CI |
| **Estimated runtime** | ~build-dominated |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build DeckFlow.sln` (must be 0 errors / 0 new warnings)
- **After every plan wave:** Run the affected test project
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** build-bound

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 75-01-T1 | 75-01 | 0 | TAP-01, TAP-03 | T-75-01 (get-only drop) | Additive `{get;init;}` fields, safe defaults | unit | `dotnet test DeckFlow.Core.Tests --filter ManabaseTapAnalysis` | ❌ W0 (new `ManabaseTapAnalysisTests.cs`) | ⬜ pending |
| 75-01-T2 | 75-01 | 0 | TAP-02, TAP-04 | — | TapMarker pure helper | unit | `dotnet test DeckFlow.Web.Tests --filter ManabaseDisplay` | ✅ (helper impl in T2) | ⬜ pending |
| 75-01-T3 | 75-01 | 0 | TAP-01..04 | T-75-04-I (byte-identity) | RED suite; OFF=byte-identical fact | unit | `dotnet test DeckFlow.sln --filter "Manabase|FeatureFlag|CarveOut"` | ❌ W0 (RED) | ⬜ pending |
| 75-02-T1 | 75-02 | 1 | TAP-02, TAP-03 | T-75-02 (2nd sim) | No new `Simulate` call; counter in existing loop | unit | `dotnet test DeckFlow.Core.Tests --filter ManabaseTapAnalysis` | ❌→GREEN | ⬜ pending |
| 75-02-T2 | 75-02 | 1 | TAP-01, TAP-02 | — | `ComputeTapAnalysis` uses RAW EffectiveSources totals (not rounded `ActualSources`) | unit | `dotnet test DeckFlow.Core.Tests --filter ManabaseTapAnalysis` | ❌→GREEN | ⬜ pending |
| 75-02-T3 | 75-02 | 1 | TAP-01, TAP-03 | T-75-04-I | TextBuilder block gated `if (tap is not null)` | unit | `dotnet test DeckFlow.Core.Tests --filter TextBuilder` | ❌→GREEN | ⬜ pending |
| 75-03-T1 | 75-03 | 2 | TAP-04 | T-75-03 (default-ON/clobber) | Flag seeded OFF, both dialects + catalog desc | unit | `dotnet test DeckFlow.Web.Tests --filter "FeatureFlagStoreSeed|FeatureFlagCatalog"` | ✅ (InlineData add) | ⬜ pending |
| 75-03-T2 | 75-03 | 2 | TAP-04 | T-75-03 | `IsFlagOn` read (missing key→false) + ShowTapAnalyzer propagation; xUnit covers OFF/absent/ON + viewmodel default | unit | `dotnet test DeckFlow.Web.Tests --filter "ManabaseAnalysisService\|ManabaseViewModel"` | ❌→GREEN (new cases in ManabaseAnalysisServiceTests + ManabaseViewModelTests) | ⬜ pending |
| 75-03-T3 | 75-03 | 2 | TAP-04 | T-75-04-I | Download gate `tap: Show? report : null` | unit | `dotnet test DeckFlow.Web.Tests --filter ManabaseControllerDownload` | ❌→GREEN | ⬜ pending |
| 75-04-T1 | 75-04 | 3 | TAP-04 | — | Layout CSS in `site-common.css` only | build | `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` | n/a (CSS) | ⬜ pending |
| 75-04-T2 | 75-04 | 3 | TAP-01, TAP-02, TAP-04 | T-75-04-I | Card wrapped in `@if`, no markup outside | build | `dotnet build DeckFlow.Web/DeckFlow.Web.csproj` | n/a (Razor) | ⬜ pending |
| 75-04-T3 | 75-04 | 3 | TAP-01, TAP-02, TAP-04 | T-75-04-I (byte-identity) | Render test: OFF ⇒ no `manabase-taplens`, ON ⇒ present (CI, not manual) | unit/render | `dotnet test DeckFlow.Web.Tests --filter ManabaseViewRender` | ❌ (new `ManabaseViewRenderTests.cs`) | ⬜ pending |
| 75-04-T4 | 75-04 | 3 | TAP-01, TAP-02, TAP-04 | T-75-04-I | Theme + mobile + OFF view-source byte check | manual | human-verify checkpoint (blocking) | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky. ❌→GREEN = RED at Wave 0 (75-01-T3), turns green when its implementation wave lands.*

---

## Wave 0 Requirements

- [ ] `ManabaseTapAnalysisTests.cs` (new) — stubs for TAP-01, TAP-02, TAP-03
- [ ] Additions to `ManabaseReportTextBuilderTests`, `ManabaseDisplayTests` — on-page + paste artifact metrics
- [ ] Additions to `FeatureFlagCatalogTests`, `FeatureFlagStoreSeedTests` — flag registered + seeded OFF (SQLite + Postgres)
- [ ] Additions to `ManabaseControllerDownloadTests` — paste artifact OFF=byte-identical, ON=round-trip byte-identical

*Existing xUnit infrastructure covers the framework; only new test files/cases needed.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| On-page metric render across guild themes + mobile | TAP-01, TAP-02, TAP-04 | Visual/theme-fork correctness not assertable in xUnit (OFF/ON markup presence IS now automated via ManabaseViewRenderTests — 75-04-T3) | Playwright/manual screenshots desktop+mobile, 2+ themes (per project UI-verify rule) |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency build-bound
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-06-28
