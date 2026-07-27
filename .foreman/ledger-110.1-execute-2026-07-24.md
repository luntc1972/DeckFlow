# Foreman Ledger — Phase 110.1 Execute (Cut Lab Combo Intelligence)

**Run start:** 2026-07-24
**Mode:** Codex-boosted (Agent + real shell + consented Codex gpt-5.4 medium, ChatGPT-sub login)
**Baseline commit:** 5e1e763cfdd61623069df6607dac99b1f23054f3
**Branch:** gsd/cycle19-cut-lab-upgrade
**Roles:** Codex codes each wave (gpt-5.4 @ medium); LEAD (Opus 4.8) verifies (build+test gates + blind foreman-verifier). Cross-family verify default.

## Dependency graph (strictly linear)
- Wave 1 = Plan 01 (data foundation: CardComboMembership lookup + SC-4 comment) — depends_on: []
- Wave 2 = Plan 02 (ComboProtected finding kind + view-model/patch maps) — depends_on: [110.1-01]
- Wave 3 = Plan 03 (Razor/CSS/TS rendering) — depends_on: [110.1-02]

No parallelism possible — each wave gates the next.

## Task rows
| ID | Plan | Wave | Write set | Status |
|----|------|------|-----------|--------|
| T1 | 110.1-01 | 1 | CutLabAnalysisContextBuilder.cs, CommanderSpellbookService.cs, CutLabAnalysisContextBuilderTests.cs | PENDING |
| T2 | 110.1-02 | 2 | CutLabStructuralFindings.cs, CutLabCutRoundEngine.cs, CutLabSimulationService.cs, CutLabFindingPresenter.cs, CutLabViewModel.cs, CutLabPageService.cs, CutLabUiPatchDto.cs, CutLabUiPatchBuilder.cs, +2 test files | PENDING |
| T3 | 110.1-03 | 3 | CutLab.cshtml, site-common.css, cut-lab.ts, +2 ts-test files | PENDING |

## Attempts (append-only)

### T1 / Plan 110.1-01 — Wave 1
- Attempt 1: Codex gpt-5.4 medium → STATUS DONE. Build 0-warn, CutLabAnalysisContextBuilderTests 15/15.
- LEAD verify: scope fence OK (3 files + SUMMARY), zero EOL churn (all LF), ComboNames grep=0, independent build+test green.
- Blind foreman-verifier: **PASS** (A–F all confirmed). Non-blocking note: unrequested back-compat ctor overload on CutLabClassificationContext (name-only comboNames → EMPTY membership) added to avoid editing 4 sibling test files. Behavior-preserving; accepted.
- **CARRY-FORWARD to Wave 2:** new tests MUST build classification via the full CardComboMembership path, NOT the name-only compat overload (which yields empty CompleteCombos/NearCombos → false-green risk).
- Status: **DONE**, committed 7fcb3a31.

### T2 / Plan 110.1-02 — Wave 2
- Attempt 1: Codex gpt-5.4 medium (bg bm25ee4ud, ~165k tok) → STATUS DONE. Build 0 new warn, 59/59 (3 filters).
- LEAD verify: scope OK (7 src + 3 test + SUMMARY; CutLabSimulationService correctly untouched), zero EOL churn (627 ins normal==ignore-space, all 0/0), **engine boundary gate PASS** (CutLabCutRoundEngine diff = exclusion-set line + completeCombos: arg only), independent build 0-warn + 59/59.
- Blind foreman-verifier: **PASS, no findings** (A–H). C engine boundary confirmed (round-1 advisory is a string in detector, no rule). G false-green trap AVOIDED (patch-builder CreateAnalysisContext now feeds populated cardComboMembership, not the name-only HashSet; new tests use real SpellbookCombo/AlmostCombo).
- Status: **DONE**, committed d5ba27c7.

### T3 / Plan 110.1-03 — Wave 3
- Attempt 1: Codex gpt-5.4 medium (~110k tok) → STATUS DONE. tsc exit 0, vitest 10/10 (2 files), build 0-warn, site.css empty diff.
- LEAD verify: scope OK (5 files + SUMMARY; wwwroot/js gitignored not staged), zero EOL churn (209 ins normal==ignore-space, all 0/0), site.css UNCHANGED, badge span nested in chip button, near-badge Context="Needs {missing}" (server-authored, LOCKED 2-state correct), independent tsc+vitest+build green.
- Blind foreman-verifier: **PASS, no defects** (A–G). A lock invariant (badge child of button, no lock-logic edit), B W4 guard rewritten (combo-only card renders disclosure), C HIGH-3 stale clone refreshed via textContent + vitest proves context change updates disclosure.
- Status: **DONE**, committed fbd8324a.

### PHASE GATE — full-suite regression found
- Full DeckFlow.Web.Tests: 2009 pass / 0 fail / 16 skip (clean; earlier "1 fail" was my shell MTG_DATA_DIR pollution of ContentKbArtifactPathResolverTests, confirmed passes unset).
- Full vitest: **1 fail** — cut-lab-proposal.test.ts:456 (structural-findings body live-patch). ROOT CAUSE: Wave 2 added REQUIRED `comboBadgeByCardName` to CutLabUiPatch interface; cut-lab-proposal.test.ts buildPatch fixture (outside W3 scope fence) omits it → runtime `undefined[cardName]` in appendComboBadge THROWS, aborting renderStructuralFindings → stale lead. Missed because W3 verify ran only 2 targeted vitest files, not full suite.
- FIX (T4): defensive guard — `patch.comboBadgeByCardName ?? {}` at consumption in cut-lab.ts (missing map = no badges, not crash) + update stale proposal fixture.
  - Codex dispatch FAILED: "Your workspace is out of credits" (not transient). Surfaced to user per CLAUDE.md cross-AI failure rule.
  - User AUTHORIZED Claude to apply the 2-file fix directly (Codex unavailable). Claude authored: hoisted `const comboBadgeByCardName = patch.comboBadgeByCardName ?? {}` in renderStructuralFindings, replaced both use sites; added `comboBadgeByCardName: {}` to buildPatch factory in cut-lab-proposal.test.ts.
  - Verify: EOL 0/0 (LF preserved), tsc exit 0, **FULL vitest 23 files / 93 tests ALL PASS**. Self-reviewed (Claude-authored) + deterministic full-suite = authoritative.
  - Note: tsc did not catch the fixture gap because ts-tests are compiled by vitest/esbuild (no type-check), not by `tsc -p tsconfig.json`.
  - Status: **DONE**, committed 691f304a.

## PHASE GATE — GREEN
- Full DeckFlow.Web.Tests: 2009 pass / 0 fail / 16 skip.
- Full vitest: 93 pass / 0 fail.
- All 4 commits on branch: 7fcb3a31 (W1) → d5ba27c7 (W2) → fbd8324a (W3) → 691f304a (fix).
- Waves 1-3 blind-verified PASS; fix self-authored + full-suite verified.
- OWED: phase verification (gsd-verifier), SUMMARY, state/ROADMAP update. UI/UAT (badge legibility across themes) deferred to Phase 111 per plan. Branch NOT pushed (user pushes).

