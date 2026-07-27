# Foreman Ledger — Phase 104 execute (cut-lab: goals-what-if-scenarios)
baseline_commit: 167c3ccea8a36881e3287fe49e31d49fbe0f46ed
branch: gsd/cycle18-cut-lab
mode: Codex-boosted (codex exec gpt-5.4 medium, danger-full-access, cross-AI per CLAUDE.md)
run_started: 2026-07-20T15:30-06:00
phase_dir: .planning/workstreams/cut-lab/phases/104-goals-what-if-scenarios
req_ids: GOAL-01, GOAL-02, GOAL-03 (+ SIM-01 no-new-math)

Wave DAG:
- W1: 104-01 (goals domain + serializer + engine threading, backend only)
- W2: 104-02 (goals editor UI), 104-03 (what-if swap backend) — PARALLEL, both dep 104-01
- W3: 104-04 (named scenarios, localStorage, JS-only)
- W4: 104-05 (what-if UI, dep 02+03+04)
- W5: 104-06 (final integration, autonomous:false — HUMAN CHECKPOINT)

| task | seat | status | attempts | notes |
|------|------|--------|----------|-------|
| 104-01 W1 goals backend      | codex gpt-5.4 | DONE | 1 | 4 commits ..73937a07; verifier PASS_WITH_NOTES; build clean, 20/20+17/17; EOL/fence clean; DEVIATION accepted=goal-aware default-iface overloads (concrete uses goals, only fakes drop) |
| 104-02 W2 goals UI           | codex gpt-5.4 | DONE | 1 | 4 commits ..fed3eeb9; verifier PASS; build clean, tsc 0, 8/8+13/13; gates clean; 2 minor->simplify |
| 104-03 W2 whatif backend     | codex gpt-5.4 | DONE | 1 | 4 commits ..c0bb83e7; verifier PASS; build clean, 16/16+15/15; zero-Scryfall non-vacuous; /decide goal-drop FIXED; CS8629 pre-existing; EOL clean |
| 104-04 W3 scenarios          | codex gpt-5.4 | DONE | 1 | 3 commits ..1092bb5c; verifier PASS; vitest 5/5, tsc 0; gates clean; quota-copy note->simplify |
| 104-05 W4 whatif UI          | codex gpt-5.4 | DONE | 2 | att1 FAIL (decide-form regression); att2 fix 56509717 restores main-form-only write; LEAD re-ran: proposal 10/10, whatif 2/2, scenarios 5/5, tsc 0; SUMMARY 66e246be; 4 must-haves PASS(att1 verify) |
| 104-06 W5 integration (gate) | codex gpt-5.4 | E2E_GREEN_AWAIT_HUMAN | 1 | e2e 36/36 all cut-lab specs both viewports; found+fixed 4 REAL bugs (scenario-load restore, whatif-select refresh, goals-carry ProcessAsync, reset button); xUnit 261/261, vitest 25/25; screenshots+sign-off pending |

Attempts log (append-only):
- run init: LEAD=Opus 4.8(1M), Agent+shell+Codex all present. Codex consent standing (CLAUDE.md). EOL preservation + scope fence mandatory each dispatch.
- Execution SEQUENTIAL on single working tree (no worktree isolation; two codex exec would race git index). W2 disjoint but still serialized.
- 104-01 verifier finding (FOLD INTO 104-03): CutLabApiController.cs:117 async /decide calls LEGACY ComputeProposalDeltas w/o goals -> once 104-02 goal UI ships, decide-loop deltas silently drop user goals (revert to 3/2/4) + share null-goals cache bucket. 104-03 owns CutLabApiController.cs -> thread state.Goals there.
- 104-01 cleanup candidate (phase-end /simplify): collapse ICutLabSimulationService goal-aware default-iface overloads to single optional-goals signature + update 2 out-of-scope fakes (CutLabApiControllerTests:514, CutLabPageServiceTests:2123). Deferred to avoid churn on green foundation.
- HAZARD: user dev server PID 17208 on :5173 locks DeckFlow.Web/bin; builds must redirect OutDir. Matters for 104-06 e2e (kill/rebuild).
- 104-05 verifier FAIL (att1): 4 must-haves OK+tested, but shared writeStateToHiddenInput now writes ALL CutLabStateJson inputs w/ DOM rebuild -> decide POST body changed -> cut-lab-proposal.test.ts :246 + :383 break (base 10/10 worktree-proven -> 8/10). Codex "green" never ran cut-lab-proposal. Fix att2: restore writeStateToHiddenInput to main-form-only; scope DOM-rebuild write to whatif form's own input. SUMMARY committed separately 66e246be.
- 104-06 specs (T1+T2) DONE: commits 17a0be22+7dc61a12; playwright --list 8 cases/2 files; tsc 0; no prod/server touched. T3 human-verify = LEAD gate (pending).
- POST-MERGE GATE (LEAD, Task 3): dotnet build sln clean (9 CS8629 pre-existing Core.Tests, 0 err); xUnit CutLab 257/257. BUT vitest full cut-lab* found cross-plan regression: cut-lab-lock-interactions.test.ts 3 FAIL (baseline worktree 167c3cce = 6/6, so 104-introduced). Cause: contract EXPANDED correctly (104-02 goals; 104-05 84f161f0 includeSideboard/includeMaybeboard closing a real round-trip gap) but this pre-104 test never updated. No plan's verify listed this suite -> blind spot. Fix = update stale test only (prod serialization correct). Dispatched test-sync to Codex.
- LIVE E2E (fresh server on HEAD, DLL 17:54): 4 cut-lab* e2e fails, ALL spec-vs-correct-behavior (NO product regression; unit gates 257 xUnit + 23 vitest green):
  1. smoke:115 - input[name=CutLabStateJson] now matches 6 forms (104 added goals+whatif forms each w/ state input); strict-mode violation. Values correct. -> scope locator to main form.
  2. structure:295 - expects "0 cut so far"; app renders "0 cuts so far" (pluralization from pre-104 66d7223c). -> update expected string. PRE-EXISTING.
  3. whatif:127 - asserts cardOut !contains "Plains"; plan excludes only locked/commander, basics are valid swap-out. -> drop that assertion.
  4. scenarios:136 - fresh import shows "1 cut"; Cut Lab restores prior session from server-side cache on GET ("Restored from cache" form seen). -> spec must clear session/localStorage or use fresh context. PRE-EXISTING session-cache behavior.
- Stale-server trap: initial e2e ran against a 13:51 pre-104 DLL server (user's !-launched, started 13:57 predating run); killed + relaunched fresh (server-fresh.log, DLL 17:54). Static JS served from disk was already latest.

## Mid-checkpoint user request: Cut Lab reset button (2026-07-20 ~18:10)
- User: "Add a reset button like all of the other analysis pages." Ties to the session-restore UX gap surfaced in e2e.
- Finding: Cut Lab Start over = plain <a href> (Pattern B, navigates but client deck-input-store re-restores last deck). Other pages (DeckAnalysis/Comparison/CedhMetaGap) = <button data-clear-cache data-clear-href> (Pattern A, clears client cache). GET /cut-lab Index() already fresh server-side. Restore is CLIENT-side (deck-input-store .deck-restored-notice).
- Dispatched Codex to wire Cut Lab Start over to the cache-clearing pattern (anchor+data-clear-cache preferred; cut-lab loads cut-lab.js not deck-sync.js). Then e2e scenarios clean-slate can use it.
| reset-button (user add-on) | codex gpt-5.4 | DONE | 1 | 5f3c5a5a; +data-clear-cache on Start over anchor (cut-lab loads deck-input-store+deck-sync); tsc 0; native nav preserved |
- Server relaunched on HEAD (DLL 18:32, includes reset 5f3c5a5a). e2e-fix Codex dispatched: repair 4 cut-lab* specs (smoke locator scope, structure pluralization, whatif basics-in-cardOut, scenarios clean-slate via new reset), verify live vs :5173.

## e2e-fix BLOCKED -> 2 REAL product bugs found by live e2e (Codex correctly refused to mask)
1. GOAL-02 scenario LOAD broken: loadSavedScenario (cut-lab.ts:980) reposts MAIN intake form w/ saved CutLabStateJson, but form still carries current DeckText -> server NeedsDeckInputRehydration=false (requires empty deck text) -> ignores loaded state's intent/decisions/goals, re-imports stale deck. Loaded scenario does NOT restore. Fix: clear DeckText/DeckUrl before form.requestSubmit() so server rehydrates from loaded state.
2. GOAL-03 whatif selects stale: WhatifCardInOptions server-rendered from cut pile at page load (empty initially). JS decide-accept patches sticky/cuts-made but never refreshes whatif cardIn/cardOut selects -> after accepting cuts, swap-in select empty until full reload. Fix: refresh whatif A/B selects after decisions that change working-list/cut-pile (touches sensitive decide-success JS path).
- Unit gates (257 xUnit + 23 vitest) missed both = round-trip/integration gaps; 104-06 e2e did its job.
- Reverted Codex blocked spec edits to clean HEAD. Legit stale-spec fixes still owed (smoke locator, structure/scenarios pluralization, whatif Plains) once product fixed.
- User chose FIX BOTH. Combined bugfix dispatched: cut-lab.ts scenario-load deck-clear + whatif-select refresh after decisions + 4 stale-spec fixes; caution=proposal must stay 10/10; verify live vs :5173. Will blind-verify product fixes after.
- Scenario-load fix attempt 3 (DIFFERENT approach): guard writeStateToHiddenInput itself on preserve-flag (blocks all rebuild paths, not just submit event). whatif fix confirmed working (25/25 vitest). Dispatched.
- Scenario-load client fix WORKS (smoke/whatif/structure e2e PASS desktop+mobile). Final piece = server: Process action missing deserialize+RehydrateIntakeRequestFromState (Decide/Goals/Whatif have it). Dispatched server fix + controller test + commit-all.
- e2e on rebuilt server: 32/34 PASS (smoke/whatif/structure all green). Last fail: scenarios :166 primary-plan shows fresh not saved -> 3rd client cache (decksync-form-state-cut-lab) overrides server-restored fields. Dispatched loadSavedScenario cache-clear fix.
- ROOT: deck-sync form-state = sessionStorage (not localStorage); Codex cleared wrong store. Fix: sessionStorage + skipPersistence flag. Surgical dispatch.
- ROOT (real GOAL-01 bug, e2e-caught): ProcessAsync preAnalysisState (CutLabPageService.cs:688) omits Goals=priorState.Goals -> goal turns dropped through round-trip (scenario-load shows 3 not 5; goal-editing likely broken e2e too). One-line server fix + test dispatched.
- FINAL E2E 36/36 GREEN. e2e caught 4 real bugs now fixed. HEAD 581ce272.

## UAT bug (user, 2026-07-21): Moxfield URL import drops sideboard
- Root: importer uses v2 api.moxfield.com (403 Cloudflare) -> Commander Spellbook fallback (no sideboard/maybeboard) -> pool=99 mainboard only -> "<=100" error. Deck onu3xt3: main 99 + side 41 = 140 (valid).
- v3 api2.moxfield.com/v3 works (verified, returns all boards, same card shape nested under boards.{name}.cards). authorTags stays root; companions -> boards.companions.cards.
- User chose: migrate importer to v3. Blast radius = all Moxfield-importing tools via DeckEntryLoader (DeckEntry shape unchanged = safe). Codex dispatched (DeckFlow.Core + Core.Tests fixtures v2->v3). Keep Spellbook fallback. On current cycle18 branch as separate commit.

## Moxfield v3 migration (UAT-driven, post-phase)
- Task: fix sideboard-import regression (v2 403 → Spellbook fallback drops sideboard/maybeboard).
- Codex dispatch blxt9vf98 → commit d3601109 (squashed). Write set: MoxfieldApiUrl.cs, MoxfieldApiDeckImporter.cs, MoxfieldApiDeckImporterTests.cs, ParserTests.cs (URL assert), moxfield-companion-direct.json.
- URL → api2.moxfield.com/v3/decks/all/{id}; boards parsed at root.boards.{name}.cards; authorTags@root; companions@boards.companions.cards; Spellbook fallback+notice preserved; DeckEntry unchanged.
- EOL: LF clean (no CRLF). Build clean (9 pre-existing Manabase CS8629 only). Moxfield tests 46/46.
- Blind verifier (foreman-verifier): PASS — all 7 criteria reproduced.
- OWED: running :5173 server is STALE (v2 importer) — rebuild+restart to live-UAT the user's deck onu3xt3 (expect 140-card pool w/ sideboard). Not verified live yet.

## Cut Lab → bridge extension wiring (real fix for sideboard import)
- ROOT CAUSE (found after v3 commit still failed live): Moxfield Cloudflare edge 403s the app's .NET client by TLS/HTTP2 fingerprint on BOTH v2 and v3 (curl w/ identical headers gets 200). Server URL-import → 403 → IsCloudEdgeBlock → Commander Spellbook fallback (no sideboard) → 99 mainboard < 101 floor → "100 or fewer". API-version migration cannot fix it.
- Confirmed via CLI probe-moxfield: "HTTP 403 Forbidden ... Attention Required! | Cloudflare".
- Chrome extension does NOT need code changes; it was simply not wired to Cut Lab (cache-key 'cut-lab' unmapped in moxfield-extension-bridge.ts). Bridge reads deck in-browser (un-blocked) → Archidekt-style text w/ #Sideboard tags.
- FIX (commit 25ed5bce, client TS only): added 'cut-lab' branch to collectMoxfieldImportTasks (DeckUrl/DeckText/DeckInputSource) + auto-checks IncludeSideboard on import (server drops Board=sideboard unless checked — default false was the hidden blocker). No C# needed (ArchidektParser tags #Sideboard→sideboard; CutLabPageService includes it when box checked).
- Verify: EOL LF clean; scope = 2 files (bridge.ts + new vitest); vitest 15/15 (proposal 10/10); tsc 0. Blind verifier: PASS_WITH_NOTES, selectors match real CutLab.cshtml.
- ACCEPTED DEVIATION (low sev): test reaches internal via window.DeckFlowMoxfieldExtensionBridgeTest global that ships in prod bundle (module:"none" blocks clean exports; existing bridge tests only cover mobile-guard/submit, not the desktop import path). Inert dead-surface. FOLLOW-UP (phase-end /simplify): replace with postMessage-mock submit-driven test + drop the global.
- TS recompiled → served wwwroot/js updated; running :5173 serves fresh bridge JS (static-from-disk), NO server restart needed. OWED: user browser UAT w/ extension installed + localhost:5173 allowed in extension Options.

## Phase-end /simplify (2026-07-21) — 4 review agents → 3 fixes
- Review: 4 read-only agents (reuse/simplification/efficiency/altitude) over the 4-commit diff + candidate files. Dedup: 3 actionable, rest stale/risky/out-of-scope.
- Codex gpt-5.4 (batched, 3 commits): F1 `940dc420` remove prod window test-seam + submit-driven bridge test; F2 `d9626190` collapse ICutLabSimulationService goal-aware DIM overloads → single `goals=null` sig (removed silent-drop trap, updated NoOp + doubles); F3 `3a028b65` decide-form selector `action=URL`→`data-cut-lab-decide-form` (path-base-safe; isDecisionForm input-based).
- F3-cleanup attempt (drop the residual scan-stamp loop in setDecisionButtonsBusy) → Codex BLOCKED: scan is LOAD-BEARING (removing it fails 3 cut-lab-proposal tests — test fixtures build decide forms without the attr). Reverted; scan kept as accepted residual. My "provably redundant" call was wrong (only mapped prod creation paths, not test fixtures).
- Blind verifier: PASS all 3. Gates: tsc 0, vitest 30/30 (proposal 10/10), dotnet ~CutLab 261/261 0-warn, scope 8 files, LF clean.
- SKIPPED (documented): PoolStatusText (already deleted — stale); pool-status chip (two sites semantically differ total vs non-commander — behavior risk); _spellbook/_categoryKnowledge dead fields (DI-probe-entangled, same as 103 C5 skip); cacheKey→data-table + route path-base + pluralizer nit (out of /simplify remit).
- Branch HEAD 3a028b65; 7 unpushed commits total. OWED push.
