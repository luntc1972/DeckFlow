---
phase: 72
slug: command-zone-commander-castability
status: draft
nyquist_compliant: true
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

> Task IDs assigned by the planner; rows map the SPEC requirements (A–G) to their automated proof
> and the owning plan.

| Req | Behavior | Plan | Test Type | Automated Command | Status |
|-----|----------|------|-----------|-------------------|--------|
| G-flag | Flag `manabase.commander-castability` seeded OFF both dialects (4-file sync) | 72-01 | unit | `... test DeckFlow.Web.Tests --filter "FullyQualifiedName~FeatureFlag"` | ⬜ pending |
| B-04 | Moxfield direct "companions" → name as side metadata; no entry injected | 72-02→72-03 | unit | `... test DeckFlow.Core.Tests --filter "FullyQualifiedName~MoxfieldApiDeckImporterTests"` | ⬜ pending |
| B-05 | Archidekt Background/Companion stay mainboard with category preserved | 72-02→72-03 | unit | `... test DeckFlow.Core.Tests --filter "FullyQualifiedName~ArchidektApiDeckImporterTests"` | ⬜ pending |
| A/B-01 | Partner pair → commanderCount=2 | 72-04 | unit | `... test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseClassifierTests"` | ⬜ pending |
| A/B-02 | Background-style 2 commanders → commanderCount=2 | 72-04/72-05 | unit | same / service | ⬜ pending |
| B-03 | Companion NOT counted in commanderCount | 72-04 | unit | classifier | ⬜ pending |
| C-01 | Companion SpellRequirement ManaValue = clamp(printed)+3 → lower CastPercent | 72-04 | unit | `... --filter "FullyQualifiedName~CastabilitySimulatorTests"` | ⬜ pending |
| C-02 | Companion outside the 99 (library size unchanged) | 72-04 | unit | same | ⬜ pending |
| D-01 | SelectHeadlineSpell worst-of (MinBy CastPercent) with 2 commanders | 72-04 | unit | `... --filter "FullyQualifiedName~ManabaseAnalyzerTests"` | ⬜ pending |
| D-02 | Commander move-out is display-only: report.Castability/AvgOnCurve/Health unchanged when flag ON for a NO-companion deck (partner pair) | 72-05 | unit | `... test DeckFlow.Web.Tests --filter "FullyQualifiedName~ManabaseAnalysisServiceTests"` | ⬜ pending |
| C-03 | Companion deck flag ON → report.Castability has 1 fewer row + recomputed avg vs flag OFF (companion excluded from 99 — intended) | 72-05 | unit | same | ⬜ pending |
| B-06 | Paste / Spellbook-fallback path → manual designator (no auto-detect) | 72-05/72-06 | unit + e2e | service + callout spec | ⬜ pending |
| B-precedence | Companion source precedence: designator > Moxfield-detected > Archidekt category | 72-05 | unit | `... test DeckFlow.Web.Tests --filter "FullyQualifiedName~ManabaseAnalysisServiceTests"` (ResolveCompanionName) | ⬜ pending |
| F-01 | Flag OFF → manabase result + prompt byte-identical | 72-05 | unit | `... --filter "FullyQualifiedName~ManabaseAnalysisServiceTests"` | ⬜ pending |
| F-02 | Flag OFF → deck-analysis bytes identical; companion not injected | 72-07 | unit | `... --filter "FullyQualifiedName~DeckAnalysisPacketServiceTests"` | ⬜ pending |
| F-03 | Flag ON → deck-analysis bytes identical (companion metadata inert) | 72-07 | unit | same | ⬜ pending |
| G-01 | Commander callout renders above castability table (flag ON, Casual) | 72-06 | e2e | `DECKFLOW_LIVE_E2E=1 npx --no-install playwright test manabase-commander-callout` | ⬜ pending |
| G-02 | Commander rows absent from castability table (display-only) | 72-06 | e2e | same | ⬜ pending |
| G-03 | Visible table average excludes commanders | 72-06 | e2e | same | ⬜ pending |
| G-04 | Companion row shown in callout with +3 tax note | 72-06 | e2e | same | ⬜ pending |
| F-pin | Ramp/draw threshold proxy = max command-zone MV (Phase 71/72 agree) | 72-04 | unit | budget calculator test | ⬜ pending |
| BETA-01 | Always-on "analysis in beta" disclaimer renders on manabase results (flag-independent) | 72-06 | e2e/manual | `grep -n "manabase-beta-notice" DeckFlow.Web/Views/Deck/Manabase.cshtml` + cross-theme visual checkpoint | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements (BLOCKING)

- [ ] Four-file flag registration for `manabase.commander-castability` (seed Postgres + SQLite +
      `FeatureFlagCatalog.Descriptions` + `FeatureFlagCatalogTests` InlineData +
      `FeatureFlagStoreSeedTests` InlineData) — plan 72-01. CI fails if any one is missed.
- [ ] Moxfield direct-API **companion** board JSON fixture — confirm exact `"companions"` board key +
      entry shape — plan 72-02 (HARD PREREQ; do NOT rely on auto-detect until captured).
- [ ] Archidekt **Background** + **Companion** category deck JSON fixture(s) — confirm exact category
      strings — plan 72-02.
- [ ] `DeckFlow.Web/e2e/manabase-commander-callout.spec.ts` — new live-only e2e spec (G-01…G-04) — plan 72-06.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Callout visual quality across themes | G-01..G-04 | Pixel/theme judgment not assertable in unit tests | Live Playwright callout screenshots: Casual desktop + mobile × 2 themes (CLAUDE.md web-page rule); operator eyeballs callout placement vs Phase 71 verdict block, +3 tax copy, commander star removed from table — plan 72-06 Task 3 checkpoint |
| Companion +3 "to hand" heuristic copy is clear | C-01 | Wording judgment | Operator reads callout + `Help/manabase.md` companion note — plan 72-06/72-07 |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (2 fixtures + flag registration; e2e spec in 72-06)
- [x] No watch-mode flags
- [x] Feedback latency < 90s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** planned 2026-06-27 (7 plans, 4 waves)
