# Foreman Ledger — Phase 2 ManabaseBaselineWeighting

**Run:** Execute committed Phase 2 plan (pure Core helper) via TDD
**Worktree:** /mnt/c/users/chrislunt/source/personal/deckflow-cmdengine
**Branch:** feat/edhrec-bracket-land-target
**Baseline commit:** 2daa8588
**Mode:** Codex-boosted (Codex CLI 0.144.3, consent = session default gpt-5.4 medium)
**Plan:** .planning/plans/2026-07-16-phase2-baseline-weighting.md

## Tasks
| id | task | seat | write set | status |
|----|------|------|-----------|--------|
| T1 | TDD build helper (tests+stub, impl, simplify) per plan | Codex WORKHORSE gpt-5.4/med | DeckFlow.Core/Manabase/ManabaseBaselineWeighting.cs, DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs | PENDING |

## Attempts (append-only)

- **T1 attempt 1** (Codex gpt-5.4/med): BLOCKED — plan test code had CS1503 (double? into xUnit precision overload). Worker correctly refused to deviate. No commits.
- **Foreman action:** corrected plan (unwrap `.Value.Value` on 9 precision asserts), committed a3f1a702. New baseline = a3f1a702.
- **T1 attempt 2** (Codex gpt-5.4/med): re-dispatched against corrected plan.

- **T1 attempt 2**: DONE. Commits c624f1f2 (test+stub, red 8/10) + 623c225b (impl, green 10/10). Task 3 simplify: no change warranted.
- **Verification**: cheap gate — foreman re-ran filtered tests 10/10, inspected diff (2 files +246, LF, scope clean). Blind verifier (foreman-verifier, fresh ctx, cross-family) → **PASS**, 12/12 criteria, 0 findings. Full Core 1557/1557. HEAD=623c225b, tree clean.
- **T1 STATUS: ACCEPTED.**

---
## Phase 1 — Baseline storage
**Baseline commit (pre-build):** (set below)
| id | task | seat | write set | status |
|----|------|------|-----------|--------|
| P1 | Build manabase_baseline table+store per plan (TDD-ish, 6 tasks) | Codex WORKHORSE gpt-5.4/med | Core/Manabase/ManabaseBaselineRow.cs; Web/Services/Persistence/{ManabaseBaselineDialect,IManabaseBaselineStore,ManabaseBaselineStore}.cs; Web/Services/Persistence/DeckFlowDatabaseConnectionFactory.cs; Web/Program.cs; Web.Tests/ManabaseBaselineStoreTests.cs; Web.Tests/Integration/ManabaseBaselineStorePostgresTests.cs | PENDING |

- Plan authored (Claude), Codex plan-review gpt-5.5 → APPROVE_WITH_NITS; MEDIUM(REAL→DOUBLE PRECISION on PG)+LOW(assert Kind=Utc) folded. Plan commit after fold.

- **P1**: DONE. Commits d93c6449 (DTO) + c7d42fe9 (table+store) + 19aa30fa (DI) + dd7a1b9f (tests). Task 6 simplify: no change.
- **Verification**: cheap gate — foreman re-ran store tests 8/8, confirmed DOUBLE PRECISION fold + singleton registration, scope=8 additive files +704, LF no churn. Blind verifier (fresh ctx, cross-family) → **PASS** 9/9, 0 material findings. Full Web 1492p/16skip/0fail, Core 1557/0. HEAD=dd7a1b9f clean.
- **P1 STATUS: ACCEPTED.**
- **CAVEAT (honest):** Postgres path (DOUBLE PRECISION/TIMESTAMPTZ) verified by code-review only; PG [PostgresFact] tests skip without DECKFLOW_POSTGRES_TESTS+container. SQLite path fully executed. No prod consumer until Phase 3/4+flag, so low risk; exercise PG tests before flag-flip.

---
## Increment 1a — Community baseline backend
**Baseline commit (pre-build):** d6baf3a7-successor (see below)
| id | task | seat | write set | status |
|----|------|------|-----------|--------|
| I1a | Build community-baseline backend per plan (6 tasks TDD): Core DTOs+block, data file, provider+DI, flag seed/catalog+tests, analysis wiring | Codex WORKHORSE gpt-5.4/med | Core/Manabase/ManabaseCommunityBaseline.cs; Web/Data/manabase-baseline/latest.json; Web/DeckFlow.Web.csproj; Web/Services/Manabase/ManabaseBaselineProvider.cs; Web/Program.cs; Web/Services/Manabase/ManabaseAnalysisService.cs; Web/Services/FeatureFlags/{FeatureFlagStore,FeatureFlagCatalog}.cs; Core.Tests/Manabase/ManabaseCommunityBaselineTests.cs; Web.Tests/{ManabaseBaselineProviderTests,ManabaseCommunityBaselineWiringTests}.cs; Web.Tests flag tests (FeatureFlagCatalogTests+FeatureFlagStoreSeedTests) | PENDING |

- Plan authored (Claude, writing-plans) → Codex plan-review gpt-5.5 REQUEST_CHANGES (BLOCK row.Source + MEDIUM flag-tests + LOW sites) → folded → re-review APPROVE_WITH_NITS (converged). Plan converged commit below.

- **I1a attempt 1** (Codex gpt-5.4): TIMEOUT at 10min (SIGTERM). Tasks 1-4 committed (588c72b7,dd243cc9,8e82f479,73b22ba0); Task 5 code+test COMPLETE but uncommitted when killed. Foreman verified: build 0/0, Core 1/1, Web provider+wiring+flags 86/0, EOL LF no churn → committed Task 5. Codex-created nit: 2 doc-comments orphaned from methods (needs Task 6 cleanup).

- **I1a attempt 2** (Codex gpt-5.4, Task 6 only): DONE. Comment placement restored, commit bd59b6aa.
- **Verification**: foreman cheap gate (build 0/0, filters green, scope 13 additive files +668, LF no churn) + blind verifier (fresh ctx, cross-family) → **PASS 11/11, 0 findings**. Full Web 1504p/16skip/0fail, Core 1558/0. Byte-identical-OFF confirmed. HEAD=bd59b6aa clean.
- **I1a STATUS: ACCEPTED.** (Recovered cleanly from a 10-min build timeout via LOST-worker protocol: verified partial state → committed complete Task 5 → short re-dispatch for Task 6.)

- **I1a /simplify**: 4-agent review (reuse/simplification/efficiency/altitude). Reuse+efficiency CLEAN. Simplification: DTO-overlap = deliberate layering tradeoff (SKIPPED). Altitude: 1 real finding (bracket+provenance derived twice on same predicate → lockstep hazard for 1b) → Codex applied single-resolver refactor `f0b46f03` (behavior-preserving; full Web 1504p/0fail confirms). I1a COMPLETE + SIMPLIFIED.

---
## Increment 1b — Community baseline UI
**Baseline (pre-build):** ca5043a5
**UX:** no submit-on-change (radios + Analyze button, zero JS). Plan Codex-reviewed gpt-5.5 (2 HIGH+2 MEDIUM folded→converged APPROVE).
| id | task | seat | status |
|----|------|------|--------|
| I1b-A | Tasks 1-3 backend: options.BracketSource+resolver, ManabaseRequest.Bracket+clamp, controller inject classifier+ResolveEffectiveBracketAsync+ShowCommunityBaseline all paths, FakeBracketClassificationService+update 4 test ctors, ManabaseBracketResolutionTests | Codex WORKHORSE gpt-5.4/med | DISPATCHED |
| I1b-B | Tasks 4-5 UI: display line + B2-B5 selector + download hidden Bracket input | Codex gpt-5.4/med | PENDING |
| I1b-C | Task 6 UI verify (server+Playwright themes/mobile) + full suite | Foreman | PENDING |
| I1b-D | Task 7 simplify | Codex/simplify | PENDING |
- **I1b-A** (Codex gpt-5.4): TIMEOUT 10min (full-suite run ate clock). Tasks 1-2 committed (fd3b4d1a,9b66b88a); Task 3 code COMPLETE+green uncommitted → foreman verified (test-proj build 0/0, resolution+wiring 15/15, full Web 1514p/16skip/0fail, EOL LF) → committed Task 3. I1b-A DONE.
- **I1b-B** (Codex): Tasks 4-5 UI DONE — display line 3acd736d + selector/download-mirror 50621012. Build 0/0, LF.
- **I1b blind-verify**: PASS 11/11, 0 findings. Byte-identical-OFF confirmed (classifier not called flag-off). Full Web 1514p/16skip/0fail.
- **I1b-D simplify**: BracketName helper extracted 08ad657c (behavior-preserving, Core 1/1).
- **I1b-C UI visual sweep (server+Playwright themes/mobile): NOT RUN** — finicky env + deep session. Flag OFF so nothing user-facing until flip; OWED before flag-flip/UAT. Note: `.manabase-baseline-source` span has no explicit CSS (renders default, not muted) — cosmetic, visual sweep would confirm.
- **I1b STATUS: CODE ACCEPTED; visual sweep OWED.**
- **I1b-C UI visual sweep: DONE** — live Playwright (Scryfall 200, flag enabled via admin form) both viewports (desktop 1280 + mobile 390): selector B2-B5+Auto no-Exhibition + baseline line render; 2 tests pass. Spec committed bbc292ed (gated DECKFLOW_LIVE_E2E). Screenshots eyeballed OK. Server stopped.
- **Cosmetic/product polish (owed, user decision):** (1) source label shows raw "edhrec-pilot-aggregate" to users — surfaces EDHREC provenance + dev-ish token; (2) `.manabase-baseline-source` span not muted.
- **I1b FULLY COMPLETE** (code+verified+simplified+visual). Pushed through 08ad657c; spec bbc292ed local ⚠OWED push.

---
## Increment 2 — Per-commander baseline from EDHREC averages dump
**Baseline (pre-build):** 33700090 (= origin/feat/edhrec-bracket-land-target), tree clean.
**Trigger:** keattz (EDHREC) published sanctioned daily dump averages.tgz 2026-07-17; old Inc2 map (average-decks scrape + live fetch + P1 write-through) DROPPED.
**User decisions:** bundled snapshot; >=100-deck cutoff (3,179 rows); non-commercial license accepted; "Data from EDHREC" attribution.
**Spec:** .planning/specs/2026-07-17-manabase-commander-baseline-inc2-design.md (Claude-authored, supersedes v2 spec Inc2 section only).
| id | task | seat | status |
|----|------|------|--------|
| I2-R | Spec plan-review | Codex FRONTIER gpt-5.5/med read-only | PENDING |
| I2-P | Implementation plan (writing-plans) | Foreman | PENDING |
| I2-A.. | Implementation tickets | Codex WORKHORSE gpt-5.4/med | PENDING |
- **I2-R spec review**: gpt-5.5 REQUEST_CHANGES (1 HIGH keying, 1 MEDIUM cEDH-overlap, 2 LOW) folded -> CONVERGED. Spec committed 69505cca.
- **I2-P plan**: writing-plans 7 tasks; gpt-5.5 plan-review R1 REQUEST_CHANGES (2H/2M/2L: suppression predicate 5-member, runner file convention, signature-vs-spec, view-test host, normalize value, dump-tag field) folded; R2 2 residual (provider dedup rule + stale wording) folded; R3 CONVERGED. Plan+spec-amend committed.
| I2-A | Tasks 1-3: key helper + converter/DTOs + CLI generator + regenerate latest.json | Codex WORKHORSE gpt-5.4/med | PENDING |
| I2-B | Tasks 4-6: provider lookup + weighting integration + UI/CSS | Codex WORKHORSE gpt-5.4/med | PENDING |
| I2-C | Task 7 docs + full-suite gate | Codex + Foreman | PENDING |
| I2-V | Blind verify + simplify | foreman-verifier + /simplify | PENDING |
- **I2-A** (Codex gpt-5.4): TIMEOUT 10min; Tasks 1-2 committed (069ef8d0,490d3d4a); Task 3 staged complete -> foreman LOST-recovery verified (data numbers exact, build 0/0, Core 1572/0) + committed 08f1cbbd. latest.json CRLF in worktree from Windows-dotnet generator run; committed blob LF (.gitattributes normalized); working copy re-checked-out.
- **I2-B** (Codex gpt-5.4): TIMEOUT during final report only — all 3 commits landed (aa12acea provider, 199b1f55 weighting, c356dd4c UI). EOL clean, Web 1534/0/16.
- **I2-V blind verify** (foreman-verifier, cross-family): PASS 8/8 criteria, 3 LOW notes (blended-line copy imprecision = designed; StartsWith prefix match — later fixed in simplify; incremental build). HEAD c356dd4c clean.
- **I2-C docs** (Codex): 91426e75 README + spec IMPLEMENTED.
- **/simplify**: 4-agent (reuse/simplification/efficiency/altitude). Efficiency CLEAN. 7 fixes batched to Codex -> f2712979 (HasCedhMetaRange shared predicate on ManabaseReport consumed by service+view; typed ValueSource attribution + EdhrecAveragesSource const; CLI JSON-lexer deleted; SnapshotFileWriter dedup across 3 runners; CommanderDeckCount dropped; loop-invariant hoist; minDeckCount=LowDeckThreshold). SKIPPED: NullIfWhiteSpace hoist (2 parser files outside diff), test identity copies (minor).
- **Final gate**: Core 1572/0, Web 1536/0/16skip, EOL clean all files.
- **INCREMENT 2 STATUS: COMPLETE (code+verified+simplified).** OWED: push (user), visual sweep before flag-flip (with Inc1), UAT, email edhrec@edhrec.com at flip.

---
## Resume run 2026-07-18 — ship Inc1+Inc2
**Baseline:** f2712979 (= origin), tree clean; main at 3fbcdd43 (51 ahead / 36 behind).
| id | task | seat | status |
|----|------|------|--------|
| S1 | Eyeball commander numbers (stats + live EDHREC spot-checks) | Foreman | IN_PROGRESS |
| S2 | Merge main into branch, build+full suite | Foreman | PENDING |
| S3 | Visual sweep flag-on (DECKFLOW_LIVE_E2E bbc292ed + screenshots 2 viewports) | Foreman | PENDING |
| S4 | Merge branch->main, suite, push | Foreman | PENDING |
| S5 | Prod flag-flip UAT prep + edhrec email reminder | Foreman->user | PENDING |
- **S1 DONE**: 3,179 rows, min deckCount 100, median 35, range 23-83; partners carry partnerName (482 dup display names = pairs, OK); Sanar 83 verified against live EDHREC json (land:83, Lands Matter); Ur-Dragon/Atraxa/Kenrith/Godo spot-checks plausible. PASS.
- **S2 DONE**: merged main (3fbcdd43) into branch -> ec1b430a, zero conflicts. Build 0 errors. Core 1593/0, Web 1579/0/16skip on merged tree.
- **S3 DONE**: visual sweep flag-on, live e2e manabase-community-baseline.spec.ts 2/2 (desktop 1280 + mobile 390). Eyeballed: commander-keyed line "Community baseline · Core (auto-detected): EDHREC decks for Brago, King Eternal average ~34.0 lands (14,435 decks)" + muted "Data from EDHREC"; selector B2-B5+Auto, no Exhibition; mobile stacks clean. Values match latest.json exactly. Windows server PID killed via taskkill.
- **S4 DONE**: main ff -> ec1b430a (tested tree byte-identical), pushed origin. CI watch dispatched. No new code this run -> no fresh blind-verify needed (Inc1/Inc2 verdicts stand).
- **S5 -> USER**: flip `analysis.manabase.baseline` via prod /Admin/Flags + UAT; email edhrec@edhrec.com (attribution live); license = non-commercial.

---
## Run 2026-07-18b — command-zone early-turn castability (user quick change)
**Repo:** main worktree /mnt/c/users/chrislunt/source/personal/deckflow, branch feat/cmdzone-early-cast off a30c996b (release commit; tag 2026.07.7 will be MOVED to final head per user).
| id | task | seat | status |
|----|------|------|--------|
| E1 | Sim early-turn observation + model percents + UI small line + CSS + Core tests | Codex WORKHORSE gpt-5.4/med | DISPATCHED |
| E2 | Cheap gate + blind verify + merge main + retag 2026.07.7 + push --follow-tags | Foreman | PENDING |
- **E1 DONE** (Codex a78bdc90+2d9629b2): early-cast observation+UI. Foreman inline fix 62da5850 (redundant TotalMana guards — ColorsCoverable gates internally; byTurn3 line restored byte-identical). /simplify 4-agent -> 6 fixes batched to Codex d069d8c0 (lens-note reuse, ManabaseDisplay.EarlyCastSummary+tests, SimulateGame overload, single gate local, byTurn3 piggyback perf, histogram+prefix-sum); skipped: test literal dedup, .txt artifact parity (follow-up). Docs 0d516c86 (Claude, trivial prose).
- **E2 DONE**: EOL clean; Core 1597/0, Web 1583/0/16; visual sweep desktop+mobile ("Earlier turns: T2 20% · T3 67%" renders under 100%-by-T4, lens-note muted, mobile wraps); blind verifier PASS_WITH_NOTES (LOW: holdable-family semantics excl. deploy-friction/ritual = designed; NOTE: no direct monotonicity test). main ff -> 0d516c86, tag 2026.07.7 MOVED to release commit a30c996b? NO — tag recreated AT a30c996b? see note: tag deleted (was df2071c0 stale object) and recreated at HEAD=0d516c86; pushed --follow-tags (2026.07.4/5/7 all published). CI watch dispatched.
