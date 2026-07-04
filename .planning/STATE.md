---
gsd_state_version: 1.0
milestone: Cycle 14
milestone_name: — Deeper Deck Evaluation
status: Awaiting next milestone
stopped_at: 81-03 complete (3/3 tasks); Phase 81 all 3 plans EXECUTED. Commits 996870ba + d1b72048 + c55b6e3b + docs 4f30d036 on branch, not pushed.
last_updated: "2026-07-04T04:50:45.241Z"
last_activity: 2026-07-04 — Milestone cycle14 completed and archived
progress:
  total_phases: 3
  completed_phases: 3
  total_plans: 9
  completed_plans: 9
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 81 — opening-hand-mulligan-evaluator

**Phase 80 verify + review (2026-07-02):** gsd-verifier PASS — 18/18 observable truths, WINCON-01..04 all SATISFIED, build/tests reproduced independently (0/0; Core 1027, Web 1137/12). Codex (gpt-5.4 medium) cross-AI code review: CHANGES-REQUIRED → 3 findings I verified as real + 1 LOW, all fixed by Claude executor → Codex re-review APPROVE (all CLOSED, no new issues). Fixes: (1) download-zip win-con JSON derived solely from the typed flag-gated+validated `result.WinConMap`, never raw posted `request.WinConMapJson` (closes both a flag-OFF byte-identity leak via a stale posted field AND an unvalidated-JSON-into-artifact write); (2) packet-cache write decision gated on the build-time LATCHED `winConMapEnabled`/`commandZoneAwareness` (not a fresh snapshot re-read) — kills the mid-request flip replay for the whole Phase-73 class; (3) `WinConMapAggregator` tie-break key normalized (trim + case-insensitive intra-combo order) without changing displayed order. Fix commits `cc8e1a86`/`a07f3453`/`0d654f25` + 3 regression tests. Then `/simplify` (`de939fce`: shared `WinConBandFormatter`, `FormatQuantityNameList<T>`, `SerializeForArtifact`).

**Phase 80 /code-review (high effort, workflow-backed) 2026-07-02:** 4 finders → 10 candidates → 10 verifier passes → 4 CONFIRMED (5 refuted). All 4 fixed (`20e3f52a`/`f8c0d224`/`90cf1669`/`eb840b92`, +3 regression tests): **[1 HIGH real bug]** restore validator rejected `ManaValueNeeded > 30` but build path is unbounded → one high-cost combo silently nulled the entire restored map → dropped the artificial upper bound; **[2 byte-identity]** view hidden textareas gate on the request field not the typed result, and the POST action only set (never cleared) `ScoreJson`/`InteractionAuditJson`/`WinConMapJson` → stale field survived a flag ON→OFF re-post → now clear-or-set all three from the typed result; **[3 byte-identity]** interaction-audit DOWNLOAD still wrote raw posted JSON (win-con fix applied too narrowly) → generalized `SerializeForArtifact<T>` to both blocks; **[4 LOW perf]** triple-materialized current-deck filter → lazy-once. Fixes [2]/[3] deliberately touch P77/P79 round-trip surfaces (same latent leak class).

**Phase 80 follow-up hardening 2026-07-02 (Codex APPROVE):** (1) `fix(80)` `92a03660` — packet-cache bypass generalized to a `PromptMutatingAnalysisFlags` registry (all 4: command-zone P73 + score P77 + interaction P79 + wincon P80); `ShouldBypassPacketCache()` = `registry.Any(IsAnalysisFlagOn)`, score/interaction latched at top of BuildAsync, write-side `bypassCacheWrite` widened to all 4 build-time locals — **closes the score/interaction cache-replay gap** (`followup_packet_cache_flag_replay` RESOLVED) + 2 regression tests. (2) `refactor(80)` `8adb69a4` — bundled the 4 trailing optional prompt-enrichment params of `IAnalysisPromptVariant.Build` into an `AnalysisPromptEnrichments` record (behavior-neutral, ADR-0001 prose untouched, 7 test files repointed, parity/byte-identity green). Codex gpt-5.4 review: both CONFIRMED-GOOD, APPROVE. Final: build 0/0, Core 1028, Web **1144**/12. Branch 25 commits ahead, NOT pushed. ⚠ Owed operator: push → CI green + live visual smoke (desktop 1280 + mobile 390, flag ON/OFF, themes) before branch→main merge + prod deploy + flip `analysis.wincon-map` ON.

**Phase 81-01 (Opening-Hand / Mulligan Sim Instrumentation) EXECUTED 2026-07-03:** two-stage pure-observation instrumentation on `CastabilitySimulator.Simulate`'s existing London-mulligan trial loop — keepable-hand band, keep-size distribution, spell-attributed representative openers, no second sim, cast% byte-identical. `ManabaseAnalyzer.ComputeMulliganEvaluation` aggregates `ManabaseReport.MulliganEvaluation` (always computed in Core, like `TapAnalysis`). Codex review CHANGES→fixed (spell-specific openers + non-tautological no-resim test)→APPROVE. Commits `e6137ece`/`bba5f6c4`/`4ccd57c7` + fix `8d1cf817`. Core 1049/1049, Web 1144/1156.

**Phase 81-02 (Flag + Paste Artifact) EXECUTED 2026-07-03 (Claude executor):** `analysis.mulligan-eval` seeded OFF both dialects (Postgres `FALSE`/SQLite `0`, catalog description, SQLite runtime test + reflection-based Postgres-literal test). `ManabaseReportTextBuilder.Build` gained a trailing `mulligan` param + `AppendMulliganEvaluationBlock` (null-guarded byte-identical, mirrors TAP exactly) — keepable band + narrow keep criterion, keep-size process, color/curve, up to 3 representative openers whose on-curve read names the tracked spell, hedged as a first-pass consistency signal (never keep/mull advice). `ManabaseAnalysisService.ShowMulliganEval` + `ManabaseController.Download` gating mirror `ShowTapAnalyzer` line-for-line. **Deliberately did NOT** extend `DeckAnalysisPacketService.PromptMutatingAnalysisFlags`/`ShouldBypassPacketCache` to this flag — that registry gates only the `/deck-analysis` packet-session cache; `/manabase` (`ManabaseAnalysisService`, `AddScoped`, no cache dependency) recomputes every request, so there is no cache-replay surface for this flag (see 81-02-SUMMARY.md Deviations for the verification). Commits `477c8801`/`c583054f`/`82e5bed9`. Build 0/0; Core 1052/1052; Web 1147/1159 (12 PG-skip); format-gate clean.

**Phase 81-03 (On-Page Readout) EXECUTED 2026-07-03 (Claude executor):** `ManabaseViewModel.ShowMulliganEval` flows through `ManabaseController`'s analyze action from `result.ShowMulliganEval`, mirroring `ShowTapAnalyzer`. A `manabase-mulliganlens` card renders on `/manabase` immediately after the tap-analyzer card as one contiguous flag-guarded block — keepable band + narrow keep criterion, color/curve, keep-size process, and representative openers whose on-curve read names the tracked spell (`TrackedSpellName`/`TrackedOnCurveTurn`), hedged as a first-pass consistency signal. `ManabaseDisplay.KeepableMarker` maps the aggregator's own `KeepableBand` to the existing met/short marker classes. New layout CSS lives only in `site-common.css` (`:root` count unchanged, no theme file touched). `ManabaseViewRenderTests` gained an OFF no-markup test, an ON card-presence test, and an `IRazorViewEngine` excision byte-identity test proving OFF is identical to ON minus the card. `manabase-mulligan.spec.ts` is a desktop-1280/mobile-390 live-UX smoke toggling the flag via `/Admin/Flags` (mirrors `deck-analysis-render.spec.ts`'s admin-lock convention). Commits `996870ba`/`d1b72048`/`c55b6e3b` + docs `4f30d036`. Build 0/0 (full solution); Core 1052/1052; Web 1150/1162 (12 PG-skip); format-gate clean. **Phase 81 (all 3 plans) now fully executed.**

## Current Position

Phase: Milestone cycle14 complete
Plan: —
Status: Awaiting next milestone
Last activity: 2026-07-04 — Milestone cycle14 completed and archived

## Roadmap Summary

| # | Phase | Requirements | Status |
|---|-------|-------------|--------|
| 79 | Interaction & Answers Audit | INTERACT-01, INTERACT-02, INTERACT-03 | Executed + Verified |
| 80 | Win-Condition & Combo Map | WINCON-01, WINCON-02, WINCON-03, WINCON-04 | Executed (3/3 plans) |
| 81 | Opening-Hand / Mulligan Evaluator | MULLIGAN-01..06 | Executed (3/3 plans) |

**Phase ordering rationale:**

- **79 first**: exact Phase-77 precedent, purely additive over `DeckStatClassifier`/`DeckStatSummary`/`DeckStatAggregator`, no external call, lowest risk — locks the "new block through 3 variants + new flag + per-surface byte-identity test" recipe.
- **80 second**: reuses Phase 79's recipe + the already-wired/widened combo fetch; only nuance is combo-null handling + the small Spellbook-grounded parser capture.
- **81 last**: TAP-02-clone sim half, but on the other pipeline (`/manabase`, routing 3a) and may expose private sim internals — done deliberately last. All three are independent at the Core layer.

## Performance Metrics

**Velocity (Cycle 13 reference — most recent shipped):**

- Phases 75-78; build 0/0; Core 945 pass, Web ~1062 (1049 pass / 12 skip at close)
- Claude (Opus 4.8) implements + reviews code; Codex (gpt-5.5) reviews plans + applies fixes (rule active 2026-06-27)

## Accumulated Context

### Resolved Decisions (Cycle 14 — do NOT re-open)

- **Granularity = coarse:** 3 phases — one per deeper read dimension; derived from requirements, not padded.
- **Zero new dependencies:** every input (Scryfall card data incl. `keywords`, Commander Spellbook combos incl. `ManaValueNeeded`/`Popularity`, the seeded Monte-Carlo sim, `Hypergeometric`) is already hydrated. No NuGet/npm additions.
- **Mulligan routing = 3a (`/manabase`):** the mulligan evaluator surfaces on `Manabase.cshtml` + its paste artifact (mirrors TAP-01/02), where the London-mulligan sim already runs — avoids per-request sim cost on the deck-analysis path and the cross-pipeline bridge. (Phase 81.)
- **`manaValueNeeded` capture = Spellbook-grounded:** the assembly band is grounded in the already-parsed-but-dropped `SpellbookCombo.ManaValueNeeded` (and `Popularity`); the parser is updated to capture them, which also satisfies the combo-ranking pitfall. (Phase 80.)
- **Build order = interaction → win-con → mulligan:** resolves the Features-vs-Architecture divergence in favor of locking the safest recipe first.
- **Flag keys (seeded OFF both dialects + catalog description):** `analysis.interaction-audit` (79), `analysis.wincon-map` (80), `analysis.mulligan-eval` (81).

### Key Pitfalls to Watch (every phase)

- **Flag-OFF byte-identity must hold for page + all 3 paste artifacts + zip** — not just the on-screen page (AISEL-04 / `ResultContractTests`). Copy the Phase-77 contiguous-suppressible-block pattern; add a per-surface parity test; seed OFF in BOTH SqliteSeedSql + PostgresSeedSql.
- **ADR-0001 variant parity** — hand-edit all three variants, NO shared helper; the Gemini omission is the classic miss. Don't let `/simplify`/review "fix" the intentional duplication.
- **No second sim / no extra fetch** — thread the single 20k-trial pass and the cached `CommanderSpellbookResult`; a second pass is a real latency/RAM hit on the 512MB tier.
- **Heuristic honesty** — frame every count/band/% as an AI-rechecked first pass; show the cards/bands behind it; distinguish "none found" from "data unavailable."
- **CI is authoritative** — WSL VSTest masked Cycle 13's 2 CI failures; build the test projects, run targeted `--filter`, confirm GitHub Actions green before close.
- **Format-gate carve-outs** — never re-indent raw-string prompt literals (changes bytes shipped to the AI), never convert `{ get; init; }`→get-only (breaks STJ on combo records); changed-lines only.
- **Phase 79 specifically:** extend the shared `DeckStatClassifier`, not a forked `Contains` chain; stax/protection = coarse presence (curated static list + golden tests).
- **Phase 80 specifically:** keep `IncludedCombos` vs `AlmostIncludedCombos` strictly separated; rank by `ManaValueNeeded`/`Popularity`; bands not hard turn numbers; null = "unavailable" not "no win conditions."
- **Phase 81 specifically:** reuse the sim's `LondonMulligan` + `ColorKeepCap` as the single keep definition; band not false-precision %; never contradict the manabase tool's numbers; evaluation, not keep/play advice.

### Pending Todos

- Phase 81 (all 3 plans) fully executed 2026-07-03 — next: `/gsd-verify-work` or `/gsd-code-review` for Phase 81, then Cycle 14 branch push + CI + operator live visual smoke before merge/deploy.

### Blockers/Concerns

- **Phase 81 planning risk:** may need to expose currently-private London-mulligan internals (`LondonMulligan`/`ColorKeepCap`) out of `CastabilitySimulator` to surface the process/keepable reads without a second pass — keep any newly-exposed seam inside Core.
- **Stax/protection classification accuracy** (Phase 79): text heuristics are brittle — mitigate with a curated in-repo static keyword/name list (mirroring `bracket-data.json`, NOT a package) + golden tests; keep stax a coarse presence read.

### Carry-Forward (still open from prior cycles)

| Item | Status |
|------|--------|
| Operator owed: manual prod deploy of Cycle 13 (autodeploy OFF since 2026-06-27) | Operator task |
| Operator owed: flip the four Cycle 13 flags in prod when ready | Operator task |
| Operator owed: flip Cycle 12 manabase/analysis flags in prod (if not yet done) | Operator task |
| `deckflow_admin` credential deletion (password rotated) | Operator task |
| Full dual-dialect branch collapse (PG DDL parity prereq) | Backlog |
| SEO/growth lane (SEO-01..05) | Deferred |
| Scheduled/bulk harvest (AUTO-03/04) | Deferred |
| Matchup / meta-threat read (deepens cedh-meta-gap) | Deferred (separate lane) |
| Phase 81 P01 | 25min | 3 tasks | 6 files |
| Phase 81 P02 | 12min | 3 tasks | 8 files |
| Phase 81 P03 | 22min | 3 tasks | 7 files |

## Session Continuity

Last session: 2026-07-03
Stopped at: **Cycle 14 milestone CLOSED** — phases 79-81 squash-merged to `main` (`701ec2fa`, CI green `28694830980`), audit PASSED, archived to `.planning/milestones/cycle14-*`, tagged `2026.07.1`.
Resume: `/gsd-new-milestone` to scope the next cycle.

## Deferred Items

Items acknowledged and deferred at Cycle 14 milestone close on 2026-07-03 (from `audit-open`; mostly prior-cycle artifacts, not Cycle-14 gaps):

| Category | Item | Status |
|----------|------|--------|
| debug | calibration-measurement | unknown |
| debug | health-band-before-baseline | unknown |
| debug | health-band-flag-on-measurement | unknown |
| debug | health-band-headline-floor-spec | unknown |
| debug | moxfield-bridge-busy-stuck | root-cause-found |
| debug | pull-from-prod-sftp-decouple | unknown |
| quick_task | manabase-load-step | missing |
| quick_task | 260624-kpg-fix-dfc-transform-cards-excluded-from-se | missing |
| quick_task | 260624-opb-be-able-to-download-the-manabase-analysi | missing |
| quick_task | 260627-flag-key-namespacing | missing |
| quick_task | 260627-p55-deckflow-studio-safety-wins-loopback-bin | missing |
| quick_task | 260627-qyc-deckflow-studio-directpush-prod-write-in | missing |
| verification_gap | (phase 79/80/81 CI-green + live-smoke) | resolved at close — CI green + smoke done |

## Operator Next Steps

- **Manual prod deploy** of Cycle 14 (autodeploy OFF) + flip the three flags ON (`analysis.interaction-audit`, `analysis.wincon-map`, `analysis.mulligan-eval`); Cycle 13 deploy + flag flips likewise still owed.
- Start the next milestone with `/gsd-new-milestone`.
