---
phase: 32-expert-context-selection
verified: 2026-06-09T00:00:00Z
status: passed
score: 6/6 requirements verified
overrides_applied: 0
retroactive: true
evidence_source:
  - ".planning/phases/32-expert-context-selection-runs-after-phase-30-before-phase-31/32-01-SUMMARY.md"
  - ".planning/phases/32-expert-context-selection-runs-after-phase-30-before-phase-31/32-02-SUMMARY.md"
  - ".planning/phases/32-expert-context-selection-runs-after-phase-30-before-phase-31/32-03-SUMMARY.md"
  - ".planning/phases/32-expert-context-selection-runs-after-phase-30-before-phase-31/32-04-SUMMARY.md"
  - ".planning/phases/32-expert-context-selection-runs-after-phase-30-before-phase-31/32-VALIDATION.md"
  - ".planning/phases/32-expert-context-selection-runs-after-phase-30-before-phase-31/32-SECURITY.md"
  - ".planning/ROADMAP.md (lines 116, 206-227)"
  - "DeckFlow.Web.Tests/ContentSiteIndexStoreTests.cs"
  - "DeckFlow.Web.Tests/ContentKbMergedClipsTests.cs"
  - "DeckFlow.Web.Tests/ContentKbRelevanceServiceTests.cs"
  - "DeckFlow.Web.Tests/ContentKbClipParserTests.cs"
  - "DeckFlow.Web.Tests/ContentKbExcerptTests.cs"
  - "DeckFlow.Web.Tests/DeckAnalysisPacketServiceExpertContextTests.cs"
  - "DeckFlow.Web.Tests/AdminContentKbControllerTests.cs"
  - "DeckFlow.Web.Tests/ContentKbControllerTests.cs"
  - "DeckFlow.Web.Tests/AnalysisPromptVariantExpertContextTests.cs"
  - "visual-verify: user-approved 2026-06-08 (3 UAT fix rounds, desktop + mobile)"
  - ".planning/debug/expert-pin-not-injected.md (SEL-02 post-phase fix, commit a106c6a)"
re_verification:
  previous_status: none
  previous_score: n/a
  gaps_closed: []
  gaps_remaining: []
  regressions: []
---

# Phase 32: Expert Context Selection — Verification Report

**Phase Goal:** Layer manual expert-context selection (pin videos, follow creators, evergreen flag) over the Phase 30 auto-relevance engine. User selections persist in the packet zip, are restored on re-upload, and are injected into all prompt variants. Admin can mark clips evergreen. A 4-tier fill merge (pins → follows → auto → evergreen, K=5 budget) drives the "What Experts Say" panel.
**Verified:** 2026-06-09T00:00:00Z (retroactive — phase shipped + visual-verified 2026-06-08; VERIFICATION.md backfilled 2026-06-09)
**Status:** passed
**Re-verification:** No — initial verification (retroactive backfill)

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria — the contract)

| # | Truth | Status | Evidence |
| --- | ----- | ------ | -------- |
| 1 | `is_evergreen` column added to the content-site-index table via self-healing migration (ordinal 13) in both SQLite and Postgres; `SetEvergreenAsync` mutates the flag; seed reload preserves curation (not overwritten on upsert) | ✓ VERIFIED | 32-01-SUMMARY: `ContentSiteIndexStore.cs` — `is_evergreen` at ordinal 13 in CREATE TABLE consts + ALTER migration; `IsEvergreen = ReadVisibility(reader, 13)`; `UpsertPreservingVisibilitySql` lists `is_evergreen` in INSERT cols only, omitted from `DO UPDATE SET`. Round-trip test `ContentSiteIndexStoreTests.StoreRoundTrip_IsEvergreenTrue` passes. |
| 2 | `GetMergedClipsAsync` 4-tier fill merge (pinned → followed → auto → evergreen) with pin cap of 3, K=5 budget, `TrimMergedClipsToBudget` (4500 chars), and `ClipOrigin` marker on every clip; injected into all analysis prompt variants; cache key forks on selection (pins ordinal, follows ignore-case) | ✓ VERIFIED | 32-01-SUMMARY: tier-fill merge implemented; auto path (`GetRelevantClipsAsync`, `ScoreArtifact` gate) byte-unchanged. 32-02-SUMMARY: `BuildDeckAnalysisCacheInputs` folds `NormalizedPinnedVideoIds` (ordinal) + `NormalizedFollowedCreators` (ignore-case); `CacheKey_SameDeckDifferentPins` test covers all branches. 32-VALIDATION.md: `ContentKbRelevanceServiceTests`, `ContentKbMergedClipsTests` green. |
| 3 | Expert selection (PinnedVideoIds, FollowedCreators) persists in the packet zip as `33-expert-selection.json`; re-upload restores selection and short-circuits re-merge | ✓ VERIFIED | 32-02-SUMMARY: `ExpertSelectionState` top-level record + `ExpertSelectionJsonOptions` (camelCase, case-insensitive) at every serialize/deserialize site; both `DeckAnalysisDownload.BuildZip` call sites thread `selectionJson:`; 2× `catch (JsonException)` graceful-degrade (LoadFromZip + replay guard). `PacketArtifactStoreTests` + `DeckAnalysisRequestTests` pass. |
| 4 | Browse page Pin/Follow buttons + selection tray, analysis form chip area + object-aware typeahead backed by `/api/content-kb/entries` and `/creators` (visible-only, `Take(10)`, SameOrigin-gated); localStorage persistence via `kb-selection.ts`; pins clear only after successful analysis render | ✓ VERIFIED | 32-03-SUMMARY: all acceptance greps pass: VideoId VM+projection, ResolvedPinTitles, entries+creators endpoints, localStorage keys, entries fetch, `data-kb-clear-pins-on-load` marker inside success region only, zero `innerHTML`, 26 `kb-*` CSS class hits. `ContentKbControllerTests` + `ContentKbSearchApiController` test coverage green. |
| 5 | Admin `/Admin/ContentKb` per-row Evergreen toggle (`[ValidateAntiForgeryToken]` + `SameOriginRequestValidator` double-guard, mirrors SetVisibility); per-clip origin markers in "What Experts Say" panel via `ClipOriginClass` allowlist mapper (`_ => "auto"`) | ✓ VERIFIED | 32-04-SUMMARY: MED-7 verified: `kb-clip-origin--@ClipOriginClass(clip.ClipOrigin)` only; zero raw `@clip.ClipOrigin` in class. `AdminContentKbControllerTests` 15/15 pass. Banner + `KbEntryRow.IsEvergreen` present. |
| 6 | Full regression green at phase close: Core 270/270 pass / 0 fail; Web 608 pass / 5 PG-skip / 0 fail; visual-verified at desktop + mobile across themes (user-approved 2026-06-08 after 3 UAT fix rounds) | ✓ VERIFIED | 32-04-SUMMARY build/test results: `dotnet test DeckFlow.Core.Tests` → 270/0; `dotnet test DeckFlow.Web.Tests` → 608 pass / 5 skip (PG) / 0 fail. Visual checkpoint: user approved 2026-06-08 after 8 dogfood UX items fixed + re-verified. ROADMAP.md line 116 marks `[x]`. |

**Score:** 6/6 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | -------- | ------ | ------- |
| `DeckFlow.Core/Content/ContentSiteIndexStore.cs` | `is_evergreen` column (ordinal 13), self-healing ALTER migration, `SetEvergreenAsync`, preserving-upsert excludes from DO UPDATE SET | ✓ VERIFIED | 32-01-SUMMARY Task 1 acceptance greps all pass; T-32-01 + T-32-14 CLOSED in 32-SECURITY.md |
| `DeckFlow.Web/Services/ContentKbRelevanceService.cs` | `GetMergedClipsAsync` 4-tier fill, `ResolvePinTitlesAsync`, `CalculateScoreAndDimensions` ungated mirror | ✓ VERIFIED | 32-01-SUMMARY (merge), 32-02-SUMMARY (ResolvePinTitlesAsync + cache key) |
| `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` | `GetMergedClipsAsync` wired as clip source (non-replay); `33-expert-selection.json` zip round-trip; replay-first guard | ✓ VERIFIED | 32-02-SUMMARY; `DeckAnalysisPacketServiceExpertContextTests` green |
| `DeckFlow.Web/Controllers/Api/ContentKbSearchApiController.cs` | `/api/content-kb/entries` + `/creators`; visible-only; `Take(10)`; SameOrigin double-guard | ✓ VERIFIED | 32-03-SUMMARY reviewer notes; T-32-08 + T-32-09 CLOSED in 32-SECURITY.md |
| `DeckFlow.Web/wwwroot/ts/kb-selection.ts` | localStorage pin/follow management, object-aware typeahead, `data-kb-clear-pins-on-load` post-success clear; compiled `.js` NOT committed (gitignored) | ✓ VERIFIED | 32-03-SUMMARY: Codex force-added js; reviewer untracked via `git rm --cached` + chore commit; convention restored |
| `DeckFlow.Web/wwwroot/css/site-common.css` | All `kb-*` classes incl. `.kb-clip-origin*` badges | ✓ VERIFIED | 32-03-SUMMARY: 26 `kb-*` CSS class hits; layout CSS in `site-common.css` per convention |
| `DeckFlow.Web/Views/Deck/_ContentKbPanel.cshtml` | Per-clip origin markers via `ClipOriginClass` allowlist; zero raw `@clip.ClipOrigin` in class attribute | ✓ VERIFIED | 32-04-SUMMARY MED-7 verified |
| `32-VALIDATION.md` | Nyquist validation strategy; per-requirement map; sign-off | ✓ EXISTS | `.planning/phases/32-.../32-VALIDATION.md`; status `validated`; `nyquist_compliant: partial` (logic automated, UI human-verified) |
| `32-SECURITY.md` | 16 STRIDE threats, all CLOSED; 0 open | ✓ EXISTS | `.planning/phases/32-.../32-SECURITY.md`; `threats_total: 16`, `threats_closed: 16`, `threats_open: 0`; re-audited 2026-06-09 |

### Key Link Verification

| From | To | Via | Status | Details |
| ---- | -- | --- | ------ | ------- |
| `GetMergedClipsAsync` (tier-1 pins) | published rows only | `GetPublishedRowsAsync` + exact-Ordinal match | ✓ WIRED | T-32-03 + T-32-05 CLOSED; unknown ids yield no clip |
| `DeckAnalysisRequest.PinnedVideoIds` / `FollowedCreators` | packet cache key | `BuildDeckAnalysisCacheInputs` (pins ordinal, follows ignore-case) | ✓ WIRED | `CacheKey_SameDeckDifferentPins` test verified in 32-02-SUMMARY |
| `ContentKbSearchApiController` | SameOrigin guard | `SameOriginRequestValidator.IsValid` → 403 | ✓ WIRED | T-32-08 CLOSED; consistent with API CSRF posture |
| Admin SetEvergreen | CSRF + BasicAuth | `[ValidateAntiForgeryToken]` + `SameOriginRequestValidator` + `/Admin` path → `BasicAuthMiddleware` | ✓ WIRED | T-32-11 + T-32-12 CLOSED; mirrors SetVisibility double-guard exactly |
| `ClipOrigin` → CSS class | allowlist mapper | `ClipOriginClass(clip.ClipOrigin)` (`_ => "auto"`) | ✓ WIRED | T-32-13 CLOSED; raw `@clip.ClipOrigin` in class = 0 |

### Behavioral Spot-Checks

| Behavior | Source | Result | Status |
| -------- | ------ | ------ | ------ |
| Tier-fill: pin cap 3, K=5, char budget 4500 enforced server-side regardless of list size | 32-SECURITY.md T-32-02; 32-01-SUMMARY | `Take(3)` pin cap, `MaxClips=5`, `TrimMergedClipsToBudget` enforced | ✓ PASS |
| Auto path unchanged (ScoreArtifact gate `dimensionsHit >= 2`) | 32-01-SUMMARY reviewer notes | `GetRelevantClipsAsync` + `ScoreArtifact` byte-unchanged | ✓ PASS |
| Selection JSON uses camelCase options, no default-options serde leak | 32-02-SUMMARY reviewer notes | `ExpertSelectionJsonOptions` (camelCase + case-insensitive) at every serialize/deserialize site | ✓ PASS |
| localStorage pins never injected via `innerHTML` (XSS) | 32-03-SUMMARY; T-32-10 | `textContent` 9 sites, `innerHTML` count = 0 | ✓ PASS |
| Compiled `kb-selection.js` not committed | 32-03-SUMMARY (reviewer correction) | `git rm --cached` applied; `.gitignore` convention restored | ✓ PASS |
| Latent Core.Tests break from interface growth fixed before phase close | 32-04-SUMMARY deviations | `RunDistillAsyncTests.FakeContentSiteIndexStore` CS0535 fixed in `eb125f9` | ✓ PASS |
| Visual checkpoint: pin/follow/tray/chips/origin markers at desktop + mobile | 32-04-SUMMARY Task 3 | User-approved 2026-06-08; 8 UX items fixed + re-verified | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
| ----------- | -------------- | ----------- | ------ | -------- |
| SEL-01 | 32-03 | Pin video / follow creator from browse page + analysis-form chip area; selection UI wired end-to-end | ✓ SATISFIED | `ContentKbBrowseViewModel.Entry.VideoId`; browse Pin/Follow buttons; analysis chip area; acceptance greps pass in 32-03-SUMMARY; `ContentKbControllerTests` green |
| SEL-02 | 32-01, 32-02 | Layered fill merge: pins → follows → auto → evergreen, within K=5 + char budget; pins trimmed last; auto/follow/evergreen tiers verified in-phase. **Note:** A post-phase defect was found and fixed 2026-06-09 (commit `a106c6a`): pinned rows whose KB artifact failed to parse were silently dropped by `ParseRowsAsync` (flag `includeFailedRowsAsZeroScore: false`) before tier-1 selection, so the pin did not appear in the Expert Context block. Fix: `GetMergedClipsAsync` now parses with `includeFailedRowsAsZeroScore: true` so failed rows remain eligible for explicit tiers while tier-3 auto still excludes zero-score rows. Refactor `bfe16b1` unifies `PinId` into `ContentSiteIndexRow.PinId`. Three regression tests added to `ContentKbMergedClipsTests` (Web 657/0/5, CI green). Live-pin re-confirmation deferred until `content.kb.enabled` is next ON in prod (operator ships KB dark in v1.5 by design). Debug session: `.planning/debug/expert-pin-not-injected.md`. | ✓ SATISFIED | Auto/follow/evergreen tiers: `ContentKbRelevanceServiceTests` + `ContentKbMergedClipsTests` green at phase close. Pin-path defect diagnosed + fixed post-phase with regression coverage. `T-32-03`, `T-32-05`, `T-32-06` CLOSED in 32-SECURITY.md. |
| SEL-03 | 32-03 | One-shot video pins, sticky creator follows, localStorage persistence; pins clear only after successful analysis render | ✓ SATISFIED | `kb-selection.ts` localStorage keys; `data-kb-clear-pins-on-load` marker inside success region only (not submit-time); progressive enhancement: server-rendered hidden fields submit with JS off. 32-03-SUMMARY acceptance greps all pass. |
| SEL-04 | 32-02 | Selection persisted in packet zip as `33-expert-selection.json`; re-upload restores selection and short-circuits re-merge | ✓ SATISFIED | `ExpertSelectionState` record; `ExpertSelectionJsonOptions` (camelCase); `PacketAllowedNames` gate; 2× `catch (JsonException)` degrade. `PacketArtifactStoreTests` + `DeckAnalysisRequestTests` pass. T-32-04 CLOSED. |
| SEL-05 | 32-01, 32-04 | Artifact-level Evergreen admin flag (`is_evergreen` column, `SetEvergreenAsync`, admin toggle with CSRF + BasicAuth); expert-context injected into analysis prompt variants | ✓ SATISFIED | Self-healing migration ordinal 13; preserving-upsert omits from DO UPDATE SET; `AdminContentKbControllerTests` 15/15 pass; `DeckAnalysisPacketServiceExpertContextTests` + `AnalysisPromptVariantExpertContextTests` green. T-32-01, T-32-11, T-32-12, T-32-14 CLOSED. |
| SEL-06 | 32-01, 32-04 | Panel origin markers: pinned/followed/auto/evergreen; typeahead search endpoints (visible-only, `Take(10)`, SameOrigin); admin set-evergreen toggle | ✓ SATISFIED | `ClipOriginClass` allowlist mapper (`_ => "auto"`); `ContentKbSearchApiController` entries+creators endpoints; `ContentKbControllerTests` + `AdminContentKbControllerTests` green. T-32-08, T-32-09, T-32-10, T-32-13 CLOSED. Visual-verified 2026-06-08. |

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
| ---- | ------- | -------- | ------ |
| `DeckFlow.Web/Services/ContentKbRelevanceService.cs` | Scoring sub-expressions duplicated between `ScoreArtifact` (gated) and `CalculateScoreAndDimensions` (ungated mirror) | LOW (tech debt) | None on shipped behavior; two paths must stay in sync if weights change. Plan-mandated to protect the proven auto path. Flagged in 32-01-SUMMARY reviewer notes for future consolidation. |
| `DeckFlow.Web/wwwroot/js/kb-selection.js` | Codex force-added compiled JS to git (stale plan note claimed js is tracked) | LOW (VCS hygiene) | Corrected by reviewer via `git rm --cached` + separate `chore` commit. Convention restored. No runtime impact. |
| `DeckFlow.Core.Tests` fake `RunDistillAsyncTests.FakeContentSiteIndexStore` | Left missing `SetEvergreenAsync` (CS0535) when 32-01 only built `DeckFlow.Core` not `DeckFlow.Core.Tests` after interface change | LOW (latent test break) | Caught by full regression at 32-04 close; fixed in `eb125f9`. Lesson: per-plan verify must build the test project when an interface changes. |

### Human Verification Required

Visual and UI behaviors were human-verified at the 32-04 checkpoint (2026-06-08, user-approved). The following behaviors are not mechanically verifiable without a running browser session:

| Behavior | Requirement | Status |
| -------- | ----------- | ------ |
| "What Experts Say" panel renders pinned/followed/auto/evergreen clips with correct origin badges; chips add/remove; tray persists across navigation via localStorage | SEL-02, SEL-06 | ✓ VERIFIED 2026-06-08 (user-approved after 3 UAT rounds + 8 UX fixes) |
| Admin `/Admin/ContentKb` per-row Evergreen toggle renders and updates correctly | SEL-05, SEL-06 | ✓ VERIFIED 2026-06-08 (user-approved) |
| Expert Context chip area + typeahead renders correctly in DeckAnalysis form at desktop + mobile | SEL-01, SEL-03 | ✓ VERIFIED 2026-06-08 (user-approved) |
| Live pin appearing in analysis.txt Expert Context block | SEL-02 (pin path) | DEFERRED — `content.kb.enabled` shipped dark in v1.5 by design; live re-confirmation deferred until operator flips the flag in prod. Post-phase fix (`a106c6a`) is regression-tested (3 tests, Web 657/0/5 CI green). |

### Gaps Summary

**One process gap — traceability orphan (no behavioral gap):**

SEL-01..SEL-06 were **orphaned in REQUIREMENTS.md traceability** at phase close. Phase 32 was inserted from Phase 30 UAT (2026-06-07) and the SEL-* requirement IDs were defined in ROADMAP.md but never added to the traceability table in REQUIREMENTS.md. This is being corrected in the same backfill pass as this VERIFICATION.md by adding SEL-01..SEL-06 rows to REQUIREMENTS.md. No behavioral gap; all six requirements were planned, implemented, tested, and ROADMAP-tracked.

**One post-phase defect — now fixed with regression coverage (SEL-02 pin path):**

The manual EXPERT-PIN path in `GetMergedClipsAsync` had a latent defect at phase close: a pinned video whose KB artifact failed to parse was silently dropped before tier-1 selection, so the pin did not appear in the analysis Expert Context block. Diagnosed 2026-06-09 (debug session `.planning/debug/expert-pin-not-injected.md`); fixed in commit `a106c6a` (Codex) with refactor `bfe16b1` unifying `PinId` into `ContentSiteIndexRow.PinId`. Three regression tests added to `ContentKbMergedClipsTests`; Web test count 657/0/5, CI green. The auto-relevance tier (tier-3) is proven unaffected by the fix (zero-score rows still excluded by `MinSelectionScore` gate; sibling test covers this). Live-pin end-to-end confirmation remains deferred until `content.kb.enabled` is next ON in prod — this is a scheduled operator flip, not a gap in the fix.

No other gaps.

---

_Verified: 2026-06-09T00:00:00Z_
_Verifier: Claude (gsd-verifier, retroactive backfill)_
