---
phase: 72
slug: command-zone-commander-castability
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-27
---

# Phase 72 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `72-RESEARCH.md` § Validation Architecture (verified line-by-line 2026-06-26/27).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`) + Playwright e2e (`DeckFlow.Web/e2e`) |
| **Config file** | none (SDK default); `tsconfig`/playwright config in `DeckFlow.Web` |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests` |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln` |
| **e2e command** | `DECKFLOW_LIVE_E2E=1 npx --no-install playwright test` (start server first via `scripts/run-web-test.sh`, `DECKFLOW_DISABLE_AUTO_BROWSER=true`) |
| **Estimated runtime** | Core ~20s · full sln ~90s · e2e smoke ~60s |

> ⚠ WSL note (CLAUDE.md): VSTest is unreliable in WSL — run via the Windows
> `dotnet.exe` absolute path, or push-and-watch CI. Build-clean is the minimum gate.

---

## Sampling Rate

- **After every task commit:** `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests`
- **After every plan wave:** `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln`
- **Before `/gsd:verify-work`:** Full suite (Core + Web + e2e smoke) must be green
- **Max feedback latency:** ~90s

---

## Per-Task Verification Map

> Task IDs are assigned by the planner; rows below map the SPEC requirements (A–G)
> to their automated proof. The executor attaches the concrete `{N}-NN-NN` task id.

| Req | Behavior | Test Type | Automated Command | File Exists | Status |
|-----|----------|-----------|-------------------|-------------|--------|
| A/B-01 | `ManabaseClassifier` partner pair → commanderCount=2 | unit | `... test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseClassifierTests"` | ✅ add method | ⬜ pending |
| A/B-02 | Background → commander board → commanderCount=2 | unit | same | ✅ add method | ⬜ pending |
| B-03 | Companion NOT counted in commanderCount | unit | same | ✅ add method | ⬜ pending |
| B-04 | Moxfield direct "companions" board imported (companion detected) | unit | `... test DeckFlow.Web.Tests` (importer) | ❌ W0 fixture | ⬜ pending |
| B-05 | Archidekt "Companion" category → inert side metadata (Board stays mainboard) | unit | `... test DeckFlow.Core.Tests` (importer) | ❌ W0 fixture | ⬜ pending |
| B-06 | Spellbook-fallback / paste path → manual designator (no auto-detect) | unit | importer/service | ✅ add method | ⬜ pending |
| C-01 | Companion `SpellRequirement.ManaValue = printed + 3` → CastPercent | unit | `... --filter "FullyQualifiedName~CastabilitySimulatorTests"` | ✅ add method | ⬜ pending |
| C-02 | Companion not in library (99 size unchanged) | unit | same | ✅ add method | ⬜ pending |
| D-01 | `SelectHeadlineSpell` worst-of (MinBy CastPercent) with 2 commanders | unit | `... --filter "FullyQualifiedName~ManabaseAnalyzerTests"` | ✅ add method | ⬜ pending |
| D-02 | `report.Castability` unchanged when flag ON (no mutation; AvgOnCurve/Health byte-identical) | unit | same | ✅ add method | ⬜ pending |
| F-01 | Flag OFF → manabase result byte-identical to prod | unit | `... test DeckFlow.Web.Tests --filter "FullyQualifiedName~ManabaseAnalysisServiceTests"` | ✅ add method | ⬜ pending |
| F-02 | Flag OFF → deck-analysis bytes identical (companion as mainboard) | unit | deck-analysis byte-identity test | ✅ add method | ⬜ pending |
| F-03 | Flag ON → deck-analysis bytes identical (companion side metadata inert) | unit | same | ✅ add method | ⬜ pending |
| G-01 | Commander callout renders above castability table (flag ON, Casual) | e2e | `DECKFLOW_LIVE_E2E=1 npx --no-install playwright test manabase-commander-callout` | ❌ W0 spec | ⬜ pending |
| G-02 | Commander rows absent from castability table (flag ON, display-only) | e2e | same | ❌ W0 spec | ⬜ pending |
| G-03 | Visible table average excludes commanders (flag ON) | e2e | same | ❌ W0 spec | ⬜ pending |
| G-04 | Companion row shown in callout with +3 tax note (flag ON) | e2e | same | ❌ W0 spec | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Moxfield direct-API **companion** board JSON fixture — confirm exact `"companions"`
      board key + entry shape (HARD PREREQ; do NOT rely on auto-detect until captured).
- [ ] Archidekt **Background** + **Companion** category deck JSON fixture(s) — confirm exact
      category strings emitted by the API.
- [ ] `DeckFlow.Web/e2e/manabase-commander-callout.spec.ts` — new live-only e2e spec (G-01…G-04).
- [ ] Four-file flag registration for `manabase.commander-castability` (seed Postgres + SQLite +
      `FeatureFlagCatalog.Descriptions` + `FeatureFlagCatalogTests` InlineData +
      `FeatureFlagStoreSeedTests` InlineData) — CI fails if any one is missed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Callout visual quality across themes | G-01..G-04 | Pixel/theme judgment not assertable in unit tests | Live Playwright callout screenshots: Casual desktop + mobile × 2 themes (CLAUDE.md web-page rule); operator eyeballs callout placement vs Phase 71 verdict block, +3 tax copy, commander star removed from table |
| Companion +3 "to hand" heuristic copy is clear | C-01 | Wording judgment | Operator reads callout + `Help/manabase.md` companion note |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (2 fixtures + e2e spec + flag registration)
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
