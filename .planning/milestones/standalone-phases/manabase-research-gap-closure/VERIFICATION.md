---
phase: manabase-research-gap-closure
verified: 2026-07-12T22:56:46Z
status: passed
score: 37/37 must-haves verified
overrides_applied: 0
---

# Phase: Manabase Research-Gap Closure Verification Report

**Phase Goal:** Close the research-vs-implementation gaps: MBGAP-01 conditional-restriction
lands (composition-gated, flag OFF), MBGAP-02 all six untapped-land cycles (accuracy bundle
ON), MBGAP-03 ritual land-target credit (cEDH-only, flag OFF, calibration-fit — checkpoint
passed, weight 0.5 accepted), MBGAP-04 threshold research spike (decision doc, closed
doc-only), MBGAP-05a-d verdict polish (live), MBGAP-11 help re-audit, MBGAP-12 lens visual
verify (human signed off).

**Verified:** 2026-07-12
**Status:** passed
**Working tree verified:** `/mnt/c/users/chrislunt/source/personal/deckflow-manabase-gap`, branch `feat/manabase-gap-closure`, HEAD `2fadc1e9` (26 commits ahead of `origin/feat/manabase-gap-closure`, unpushed)

This is goal-backward verification of a 9-plan phase where each plan already went through
per-plan blind verification (01-06 by `foreman-verifier`, incl. one FAIL→fix cycle on plan 02
and a HIGH-fix cycle on plan 06; 07/08 LEAD-reviewed docs; 09 human sign-off). Per the task
brief, this pass re-derives evidence directly from source and focuses on cross-plan
integration rather than re-litigating each plan's own must-haves individually.

## Build & Test Evidence (independently re-run, not taken from SUMMARY claims)

| Command | Result |
| --- | --- |
| `dotnet build DeckFlow.sln` | 0 warnings / 0 errors |
| `dotnet test DeckFlow.Core.Tests --filter Manabase` | 314 passed / 0 failed / 0 skipped |
| `dotnet test DeckFlow.Web.Tests --filter Manabase` | 213 passed / 0 failed / 0 skipped |
| `dotnet test DeckFlow.Core.Tests` (full) | 1399 passed / 0 failed / 0 skipped |
| `dotnet test DeckFlow.Web.Tests` (full) | 1358 passed / 0 failed / 14 skipped (env-gated) |

Full-suite numbers match plan 06's summary exactly (Core 1399/0, Web 1358/14/0) — no
regression introduced by later plans (07-09, docs/e2e only).

## 1. Flag Inventory (cross-plan check)

| Check | Result | Evidence |
| --- | --- | --- |
| Exactly 2 new flag keys added on this branch | ✓ VERIFIED | `git diff main...HEAD -- FeatureFlagCatalog.cs` shows only `analysis.manabase.ritual-land-credit` and `analysis.manabase.restricted-lands` added |
| Both seeded OFF in Postgres branch | ✓ VERIFIED | `FeatureFlagStore.cs`: `('analysis.manabase.ritual-land-credit', FALSE)`, `('analysis.manabase.restricted-lands', FALSE)` |
| Both seeded OFF in SQLite branch | ✓ VERIFIED | `FeatureFlagStore.cs`: `('analysis.manabase.ritual-land-credit', 0)`, `('analysis.manabase.restricted-lands', 0)` |
| Ritual credit NOT folded into `ritual-burst-mana` (D-10) | ✓ VERIFIED | Separate flag key + separate `RitualLandCreditFlagKey` constant in `ManabaseAnalysisService.cs:225`; independent read at `:297` |
| Six-cycle classification gated behind accuracy bundle, not a new flag (D-08) | ✓ VERIFIED | `ManabaseClassifier.cs:414`: `(checkLandUntapped || restrictedLands) ? ClassifySpecialLand(...)`; each of the 6 regex branches individually guarded by `checkLandUntapped &&` (`:469,480,491,504,527,549`) |
| Sim keys six-cycle resolution off classifier-emitted metadata presence only (no separate gate to drift) | ✓ VERIFIED | `CastabilitySimulator.cs:936-937`: `source.CountCondition != CountConditionKind.None ? CardKind.ConditionalCountLand : ...` |
| `FeatureFlagCatalogTests` cover both new keys | ✓ VERIFIED | `FeatureFlagCatalogTests.cs:44-46` `[InlineData]` rows for both |

## 2. Cross-Plan Regression Check

| Concern | Result | Evidence |
| --- | --- | --- |
| Plan 06's `ManabaseWording` refactor didn't break plan 03/04 restricted-land wording | ✓ VERIFIED | Restricted-land disclosure table/panel (`Manabase.cshtml:653-676`, `ManabaseAnalyzer.cs:274-289`) use static land names, not `ManabaseWording` counts — no shared surface to break; full Web test suite green (213/213 Manabase-filtered, 1358/1358 full) |
| Plan 04's analyzer threading didn't break plan 02's metadata gate | ✓ VERIFIED | `checkLandUntapped` and `restrictedLands` are independent trailing params through `Classify` (`ManabaseClassifier.cs:95`) → `ClassifySpecialLand` (`:450-455`); six-cycle regex branches still gated solely on `checkLandUntapped`, restricted-land branches solely on `restrictedLands` — no cross-contamination |
| Ritual-land-credit / cedh-land-target coupling documented (not a hidden bug) | ✓ VERIFIED (documented) | `KarstenManabase.CedhLandTarget` only applies the ritual subtraction on the enabled-hybrid path (returns early when `!context.Enabled`, before the ritual-credit branch); `docs/manabase-analysis-rules.md:115` and `README.md:155` both explicitly describe the credit as subtracting from "that enabled cEDH target"; test `ManabaseAnalysisServiceTests.cs:937-975` exercises exactly this combination (`CedhLandTargetFlagKey=true` + `RitualLandCreditFlagKey` on/off) |
| Plan 08 (Help re-audit) reads the fully-updated rules doc, not a half-written one | ✓ VERIFIED | Plan 08 depends on plan 07 (wave 8); commit order confirms `43f5547f` (plan 07 docs) lands before `bf325c83` (plan 08 Help edit) |
| No debt markers (`TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER`) left in phase-touched files | ✓ VERIFIED | grep across all files touched in `main...HEAD` diff (`.cs`/`.cshtml`/`.md`/`.ts`) returned zero matches |
| No stale "backlog"/"unconfirmed" residue in `docs/manabase-analysis-rules.md` | ✓ VERIFIED | grep for `479-501`/`backlog`/`unconfirmed` returns nothing |

## 3. `docs/manabase-analysis-rules.md` Internal Consistency

Read the six-cycle section (lines ~49-52), the per-trial sim section (~188-191), the cEDH
land-target section (~115), and the threshold section (~124) after all 7 doc-editing plans
(01,02,03,04,06,07,08 all touch this file). No contradictions found: the per-trial vs.
static-census split is stated identically in both the classifier-facing section and the
simulator-facing section; the ritual-credit dependency on the enabled cEDH-hybrid path is
stated consistently in the land-target section and the flag table (line 310); the threshold
section correctly reflects the plan-07 decision doc's "confirmed" verdict with no leftover
doubt language.

**Minor, non-blocking note:** `FeatureFlagCatalog.cs`'s `analysis.manabase.accuracy` admin
tooltip (line 53-54) was not extended to mention the new six-cycle untapped-land modeling it
now also bundles (it still lists only mana-quantity/ramp-credit/mulligan/land-ramp-sim/
health-band/pay-life/MDFC). This is an internal admin-panel description, not user-facing help
copy (which was re-audited in plan 08 and is accurate), and is not referenced by any
must_have across the 9 plans. Flagged for awareness, not scored as a gap.

## 4. Must-Have Truths Sampled Across All 9 Plans (source-verified, not summary-trusted)

| # | Plan | Truth | Status | Evidence |
| --- | --- | --- | --- | --- |
| 1 | 01 | Fast lands classified with per-trial count metadata (untapped ≤2 other lands) | ✓ VERIFIED | `ManabaseClassifier.cs:469-477` `CountCondition=FastLand, CountThreshold=2` |
| 2 | 01 | Slow lands classified with per-trial count metadata (untapped ≥2) | ✓ VERIFIED | `ManabaseClassifier.cs:480-488` `CountCondition=SlowLand, CountThreshold=2` |
| 3 | 01 | ELD threshold lands classified with count + basic-type-filter metadata (≥3) | ✓ VERIFIED | `ManabaseClassifier.cs:491-499` `CountThreshold=3, CountTypeFilter` |
| 4 | 01 | Verge lands always-untapped, second color gated by static census (not tapped-state) | ✓ VERIFIED | `ManabaseClassifier.cs:504-521` `EntersUntapped=true`, census via `CountLandsBearingAnyType` |
| 5 | 01 | Training Compound always-untapped colorless + static basic-supertype census | ✓ VERIFIED | `ManabaseClassifier.cs:527-546`, `CountBasicLands(allCards) >= CheckLandMatchTypeThreshold` |
| 6 | 01 | Vivid lands ETB-tapped + reduced-weight conditional any-color source | ✓ VERIFIED | `ManabaseClassifier.cs:549-570`, `Weight=0.25, IsConditional=true` |
| 7 | 01 | Every new oracle-text regex has a canary assertion | ✓ VERIFIED | `ManabaseLiveOracleCanaryTests.cs:48-64` — FastLandRegex, SlowLandRegex, EldThresholdRegex, VergeSecondColorRegex, TrainingCompoundRegex, VividChargeRegex, SpendOnlyCreatureRegex ×2, NykthosDevotionRegex all present |
| 8 | 02 | Fast/slow land resolved per-trial at land-play time, before joining `landsOnBoard` | ✓ VERIFIED | `CastabilitySimulator.cs:1427-1439` — `ResolveLandOnlineTurn` called, THEN `landsOnBoard.Add(...)` |
| 9 | 02 | ELD resolved per-trial via basic-type tag on `landsOnBoard`, no static-census fallback | ✓ VERIFIED | `CastabilitySimulator.cs:1565-1576` counts `land.BasicTypeMask & played.CountTypeMask` from `landsOnBoard` directly |
| 10 | 02 | accuracy bundle OFF still byte-identical (six-cycle metadata absent) | ✓ VERIFIED | Gated at source: `checkLandUntapped` false ⇒ classifier never sets `CountCondition` (`:469,480,491` all require `checkLandUntapped &&`) ⇒ sim never sees `ConditionalCountLand`; parity test `ManabaseAnalysisServiceTests` accuracy-off/on suite green |
| 11 | 03 | Cavern/Unclaimed Territory weighted by dominant-creature-type share, not flat discount | ✓ VERIFIED | `ManabaseClassifier.cs:576-582`, `ClampRestrictedLandWeight(creatureComposition.DominantTypeShare)` |
| 12 | 03 | Ancient Ziggurat weighted by creature share of deck | ✓ VERIFIED | `ManabaseClassifier.cs:581` `creatureComposition.CreatureShare` |
| 13 | 03 | Nykthos modeled as conditional low-weight source (0.25) | ✓ VERIFIED | `ManabaseClassifier.cs:591-606`, `RestrictedLandMinWeight = 0.25` |
| 14 | 03 | Creature-subtype histogram computed via TypeLine em-dash splitting | ✓ VERIFIED | `ManabaseClassifier.cs:1437` `typeLineParts = ....Split('—', 2, ...)` |
| 15 | 03 | Deck-level restricted-source signal + land NAMES (not per-spell-row flag) | ✓ VERIFIED | `ManabaseModels.cs:495,852` `HasRestrictedSourceApproximation => RestrictedSourceLandNames.Count > 0` |
| 16 | 04 | `analysis.manabase.restricted-lands` registered + seeded FALSE/0 both dialects | ✓ VERIFIED | See §1 |
| 17 | 04 | Flag OFF ⇒ byte-identical analyzer output (parity test) | ✓ VERIFIED | `ManabaseAnalysisServiceTests.cs:565-598` `Classify_RestrictedLandsFlagOff_...` + `:600` OFF==baseline/ON-diverges test |
| 18 | 04 | Restricted lands disclosed with a marker naming affected land rows | ✓ VERIFIED | `Manabase.cshtml:653-676` — dedicated disclosure table + `†` marker + footnote (adapted from the alt-cost `1*` pattern since no per-land table existed; documented as a sanctioned deviation in 04-SUMMARY) |
| 19 | 04 | Unsupported-interactions panel entry naming affected lands | ✓ VERIFIED | `ManabaseAnalyzer.cs:274-289` `AppendRestrictedLandUnsupportedInteraction` |
| 20 | 04 | Playwright spec asserts marker at desktop + mobile | ✓ VERIFIED | `manabase-restricted-lands.spec.ts` — real functional assertions (table content, aria-label, footnote text, panel text, no-horizontal-scroll check), runs under both chromium-desktop/mobile projects |
| 21 | 05 | cEDH-only ritual land-target credit from already-classified `OneShots` (no re-classification) | ✓ VERIFIED | `KarstenManabase.cs:128-131` reuses `netPositiveRitualCount` param, no new classification call |
| 22 | 05 | Named calibration constant `RitualLandCreditWeight` (~0.5, capped) | ✓ VERIFIED | `KarstenManabase.cs:43-44` `RitualLandCreditWeight = 0.5`, `RitualLandCreditCap = 3.0` |
| 23 | 05 | New flag registered, seeded OFF both dialects, NOT reusing `ritual-burst-mana` | ✓ VERIFIED | See §1 |
| 24 | 05 | Flag OFF ⇒ byte-identical land targets | ✓ VERIFIED | `ManabaseAnalysisServiceTests.cs:937-975` `baselineResult.Report.TargetLands == offResult...TargetLands` |
| 25 | 05 | `cedh-land-calibrate` CLI reports third RitualCredit column | ✓ VERIFIED | `CedhCalibrateCommandRunner.cs:127-142`, `CedhCalibration.cs` full `NewTargetWithRitualCredit*` field set |
| 26 | 05 | Calibration harness RUN, before/after under-flag% documented (BLOCKING gate) | ✓ VERIFIED | `05-calibration-before.md` / `05-calibration-after.md` committed, 3281-deck corpus, 21.8%→11.1% documented; human checkpoint sign-off recorded in 05-SUMMARY frontmatter |
| 27 | 06 | 05a: both `Math.Ceiling` overstatement sites fixed (land-delta AND color-source-short) | ✓ VERIFIED | `ManabaseVerdictSynthesizer.cs:61,110` both route through `ManabaseWording.ApproximateCount` (Round-AwayFromZero, min 1); zero remaining `Math.Ceiling` in the synthesizer |
| 28 | 06 | 05b: >3 issues appends "…plus N more" on page AND .txt | ✓ VERIFIED | `ManabaseVerdictSynthesizer.cs:95-100`; both page (`Manabase.cshtml:503` renders `verdict.Lines`) and .txt (`ManabaseReportTextBuilder.cs:334-336` renders `verdict.Lines`) consume the same `Lines` collection |
| 29 | 06 | 05c: zero `(s)` plural artifacts in synthesizer/view | ✓ VERIFIED | grep for `land(s)/source(s)/color(s)/spell(s)/piece(s)/card(s)` across Core Manabase `.cs` + `Manabase.cshtml` returns only an oracle-regex comment, no user-facing text |
| 30 | 06 | 05d: per-color deficit labeled heuristic guidance on page, .txt, swap prompt | ✓ VERIFIED | `ManabaseVerdictSynthesizer.cs:113,122`, `ManabaseReportTextBuilder.cs:126`, `ManabaseSwapPromptBuilder.cs:93`, `Manabase.cshtml:645` all say "heuristic guidance" |
| 31 | 07 | Decision doc re-verifies (89+M)% and resolves the manabase-math.md vs EF2-L14 contradiction | ✓ VERIFIED | `MBGAP-04-threshold-decision.md` §2, verdict "confirmed — no code change needed" |
| 32 | 07 | (85+M)% multiplayer relaxation evaluated with explicit implement/do-not-implement verdict | ✓ VERIFIED | `MBGAP-04-threshold-decision.md` §3, verdict "do not implement" with double-count rationale |
| 33 | 07 | `docs/manabase-analysis-rules.md` threshold doubt removed; no engine code shipped | ✓ VERIFIED | `git show --stat 43f5547f` touches only the decision doc + docs file; threshold section (line 124) cites the decision doc |
| 34 | 08 | Every factual claim in `Help/manabase.md` cross-checked against `docs/manabase-analysis-rules.md`; overclaims rewritten | ✓ VERIFIED | Help doc correctly describes: flag-gated restricted-lands (OFF, `†` marker — matches shipped UI, not the earlier-drafted `*` claim caught by LEAD spot-check), heuristic per-color labeling, per-trial fast/slow/ELD resolution "inside the simulation," 20,000-trial Monte-Carlo, "…plus N more" truncation note, both new dark flags described as experimental/OFF |
| 35 | 09 | Tap-analyzer + mulligan-evaluator lenses screenshotted at desktop + mobile | ✓ VERIFIED | `manabase-lens-visual.spec.ts` targets real DOM classes `.manabase-taplens` / `.manabase-mulliganlens`, both present in `Manabase.cshtml:368,408`; spec asserts visibility + no-overflow at 2 viewport projects |
| 36 | 09 | Human confirms both lenses render correctly at both viewports | ✓ VERIFIED (human, not re-litigated) | 09-SUMMARY frontmatter: `verifier: HUMAN SIGN-OFF 2026-07-12 — both lenses approved at desktop + mobile`; this is the phase's designated non-automated checkpoint (`autonomous: false`) |
| 37 | all | No plan-10 (UX-polish) code silently shipped without a summary/verification | ✓ VERIFIED | `10-PLAN.md` exists but has no `10-SUMMARY.md` and no corresponding commits beyond the planning-doc commits (`cc6765fb`, `21a0a0a5`); out of this phase's declared goal scope and correctly left unexecuted |

**Score:** 37/37 sampled truths verified.

## 5. D-01 .. D-14 Decision Traceability

| Decision | Traced to shipped code/doc | Status |
| --- | --- | --- |
| D-01 scope cut (Tier1+Tier2+closing tasks, Tier3 backlog) | Only MBGAP-01/02/03/04/05a-d/11/12 plans exist (01-09); plan 10 is separate/unexecuted UX work, not Tier 3 | ✓ VERIFIED |
| D-02 MBGAP-09 deferred | Not touched anywhere in this branch's diff | ✓ VERIFIED |
| D-03 composition-gated per-class modeling | §4 rows 11-13 | ✓ VERIFIED |
| D-04 new flag OFF, byte-identical | §4 rows 16-17 | ✓ VERIFIED |
| D-05 disclosure = marker + panel entry | §4 rows 18-19 | ✓ VERIFIED |
| D-06 all six cycles get real rules | §4 rows 1-6 | ✓ VERIFIED |
| D-07 count-based per-trial, type-based static census | §4 rows 8-9 | ✓ VERIFIED |
| D-08 rides accuracy bundle, no new flag | §1 | ✓ VERIFIED |
| D-09 calibration-fit weight, data decides constant | §4 row 26 | ✓ VERIFIED |
| D-10 new flag, not reusing ritual-burst-mana | §1 | ✓ VERIFIED |
| D-11 research spike, doc fix regardless of verdict | §4 rows 31-33 | ✓ VERIFIED |
| D-12 verdict-polish batch (05a-d) | §4 rows 27-30 | ✓ VERIFIED |
| D-13 Help re-audit | §4 row 34 | ✓ VERIFIED |
| D-14 lens visual verification | §4 rows 35-36 | ✓ VERIFIED |

## Requirements Coverage

| Requirement | Source Plan(s) | Status |
| --- | --- | --- |
| MBGAP-01 | 03, 04 | ✓ SATISFIED |
| MBGAP-02 | 01, 02 | ✓ SATISFIED |
| MBGAP-03 | 05 | ✓ SATISFIED |
| MBGAP-04 | 07 | ✓ SATISFIED |
| MBGAP-05a/b/c/d | 06 | ✓ SATISFIED |
| MBGAP-11 | 08 | ✓ SATISFIED |
| MBGAP-12 | 09 | ✓ SATISFIED |

No orphaned requirements found for this phase in `.planning/ROADMAP.md` (entry is a bounded
backlog line, not a numbered ROADMAP phase with its own success-criteria block; must-haves
were derived from CONTEXT.md D-01..D-14 and the 9 plans' frontmatter per the verification
brief).

## Anti-Patterns Found

None. Zero `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` markers in any file touched by this
branch. No stub returns, no silently-ignored fetch results, no hardcoded-empty props found in
the touched Razor/TS/C# files.

## Human Verification Required

None outstanding. The two items requiring human judgment for this phase (MBGAP-03 calibration
acceptance checkpoint, MBGAP-12 lens visual sign-off) were both already executed as
non-autonomous checkpoint tasks within plans 05 and 09, respectively, with sign-off recorded
in their SUMMARY frontmatter (`checkpoint: task 4 human-verify PASSED`, `verifier: HUMAN
SIGN-OFF`). No new human-verification gaps were introduced by the cross-plan integration
check.

## Gaps Summary

No gaps found. All 37 sampled must-have truths verified directly against source code (not
SUMMARY narrative), full Core+Web test suites pass with 0 failures (1399/1399 Core, 1358/1358
non-skipped Web), the flag inventory is exactly the 2 flags specified by D-04/D-10 seeded OFF
in both dialects, six-cycle classification remains correctly gated behind the existing
`analysis.manabase.accuracy` bundle with no new flag (D-08), and no plan's later commits
regressed an earlier plan's invariant. One non-blocking observation noted in §3 (a stale
admin-panel tooltip for `analysis.manabase.accuracy`) is informational only — it is not
referenced by any must-have and does not affect user-facing behavior, which was independently
re-audited and verified accurate in plan 08.

---

_Verified: 2026-07-12T22:56:46Z_
_Verifier: Claude (gsd-verifier)_
