---
phase: 95-measured-style-extractor
verified: 2026-07-12T04:59:38Z
status: passed
score: 11/11 must-haves verified
overrides_applied: 0
---

# Phase 95: Measured-Style Extractor Verification Report

**Phase Goal:** Compute a creator's measured style profile from their OWN Archidekt decklists — staple-stripped, lift-weighted (not raw synergy), folder-segmented, every stat carrying `numDecks`. Substrate only (feeds Phase 97); no user-visible surface. Lives in DeckFlow.Web behind a narrow contract so the pure extraction algorithm stays testable independent of the host.
**Verified:** 2026-07-12T04:59:38Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | CS-04a/b: crawler resolves creator → deck list, imports via `ArchidektApiDeckImporter`, caches to `creator_deck_cache`, warm-cache freshness short-circuit makes zero Archidekt calls | ✓ VERIFIED | `DeckFlow.Web/Services/CreatorStyle/CreatorProfileDeckCrawler.cs:69-76` returns entirely from `_deckCacheStore.GetByCreatorAsync` when `LastCrawledUtc` is within `_freshnessWindow` and `forceRefresh` is false — `_ownerClient` is never touched on that path. `ArchidektOwnerClient.cs` resolves username + paginates `api/decks/v3/`. Test `CreatorProfileDeckCrawlerTests.CrawlAsync_WarmCacheWithinWindow_ReturnsFullyPopulatedSamplesWithZeroArchidektCalls` (line 106) asserts a `CountingOwnerClient`/`CountingDeckImporter` see zero calls. |
| 2 | CS-04c: >105-card decks filtered out before ratios | ✓ VERIFIED | `CreatorProfileDeckCrawler.CrawlAsync` line 95-98: `if (summary.Size > 105) continue;` runs before any deck is imported/cached. `StapleStripper.FilterOversized` (line 18-25) is also applied as first pipeline step in `MeasuredStyleProfileBuilder.BuildAsync` (line 107) before staple-strip/ratio computation. |
| 3 | CS-04d: folder-segmented + graded weights (`FolderWeighting`), fractional effective sample | ✓ VERIFIED | `FolderWeighting.ApplyWeights`/`EffectiveSampleSize` (`DeckFlow.Core/Knowledge/MeasuredStyleExtraction/FolderWeighting.cs`) sums per-sample fractional weights; defaults to 1.0 when uncurated (D-04). `CreatorProfileSource.FolderWeights` (curated per-creator map) resolved in `CreatorProfileDeckCrawler.ResolveFolderWeight`. Wired end-to-end in `MeasuredStyleProfileBuilder.BuildAsync` lines 114-123; `MetricDistribution.EffectiveSampleSize` populated on every metric. |
| 4 | CS-05: hybrid staple-strip (`ContentTagVocabulary.Staples` ∪ >60% personal) BEFORE ratios | ✓ VERIFIED | `StapleStripper.StripStaples` (line 102-120) unions `ContentTagVocabulary.Staples` with `ComputePersonalStaples` (>60% frequency threshold, line 66-94) and is invoked in `MeasuredStyleProfileBuilder.BuildAsync` (lines 112-113) before any `CategoryCounter`/`LiftCalculator` call. Confirmed by `SnailSeedCorpusFixture`-driven test asserting stripped samples never contain "Sol Ring"/"Command Tower". |
| 5 | CS-06: multi-bucket category counting (no first-match) + 3-layer resolver | ✓ VERIFIED | `CategoryCounter.CountPerDeck`/`GetIncludedCategories` (lines 101-114) counts every qualifying category per card, no `.First()`/`.Take(1)`. `CreatorDeckCategoryResolver.ResolveAsync` (lines 34-68) tries `CategoryKnowledgeRepository` first, falls back to `IScryfallTaggerLookupService.LookupOracleTagsAsync` only when the repository returns zero categories (harvested-repo → Tagger tail, per D-06). |
| 6 | CS-07: lift = creator Pr(A∩B)/(global Pr(A)·Pr(B)) via `GlobalCategoryBaseline` (processed-only) + `LiftCalculator`, not raw co-occurrence | ✓ VERIFIED | `LiftCalculator.ComputeLift` (lines 15-89) computes `creatorProbability / (globalProbabilityA * globalProbabilityB)`. `CardCategoryRepository.GetGlobalCategoryBaselineAsync` (lines 62-130) restricts its CTE to `q.processed = 1` (`deck_queue` join), confirming a processed-only aggregate, not all queued/raw decks. Test asserts `lift:blink|tokens` (discriminating pair) > `lift:draw|ramp` (staple pair). |
| 7 | CS-08: combo density via `CommanderSpellbookService.FindCombosAsync` (null-graceful) | ✓ VERIFIED | `MeasuredStyleProfileBuilder.ResolveComboCountAsync` (lines 254-263) calls `_commanderSpellbookService.FindCombosAsync` and uses `result?.IncludedCombos.Count ?? 0` — null-safe. Test seeds a `null` result for one deck and asserts no throw plus correct averaged density. |
| 8 | CS-09: Karsten land/curve via `ManabaseAnalyzer.Analyze(ManabaseMode.Casual)` | ✓ VERIFIED | `MeasuredStyleProfileBuilder.AnalyzeDeckAsync` (line 320) calls `ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual)` with an explicit "Why:" comment locking Casual mode for creator-to-creator determinism. `BuildKarstenMetricsAsync` emits `karsten:land_delta`, `karsten:target_lands`, `karsten:health_score`. |
| 9 | CS-10: every `MeasuredMetric` carries `NumDecks` (int) + nested `EffectiveSampleSize`; persisted via `CreatorStyleProfileStore.UpsertAsync`; `InsufficientSample` below `MinDeckFloor=5` | ✓ VERIFIED | `MeasuredMetric.NumDecks` is `required int` (`CreatorStyleProfile.cs:72`); `MetricDistribution.EffectiveSampleSize` is nested `double?` (`CreatorStyleProfile.cs:117`) — no new top-level property added (respects P94 lock). `MeasuredStyleProfileBuilder.BuildAsync` line 141: `InsufficientSample = rawDeckCount < CreatorStyleProfile.MinDeckFloor` (`MinDeckFloor = 5`, `CreatorStyleProfile.cs:9`). Line 148 calls `_profileStore.UpsertAsync(profile, ...)`. Round-trip test confirms every metric's `NumDecks`/`EffectiveSampleSize` persist and reload correctly; thin (4-deck) profile asserts `InsufficientSample == true`. |
| 10 | D-11 layering: pure extraction (`DeckFlow.Core/Knowledge/MeasuredStyleExtraction/*`) has zero `HttpClient`/AspNet; crawler/Spellbook/Tagger live in `DeckFlow.Web` | ✓ VERIFIED | `grep -rn "HttpClient\|AspNet\|Microsoft.AspNetCore" DeckFlow.Core/Knowledge/MeasuredStyleExtraction/*.cs` returns no matches. All HTTP-touching collaborators (`ArchidektOwnerClient`, `CreatorProfileDeckCrawler`, `CreatorDeckCategoryResolver`, `MeasuredStyleProfileBuilder`) live under `DeckFlow.Web/Services/CreatorStyle/`. |
| 11 | Pitfall 3: creator crawl cache never writes `card_category_observations`/`sources`/`deck_queue` | ✓ VERIFIED | `grep` across `DeckFlow.Web/Services/CreatorStyle/*.cs`, `CreatorDeckCacheStore.cs`, `CreatorProfileSourceStore.cs` for those table names returns no matches — only `creator_profile_source` / `creator_deck_cache` tables are written. `ArchidektApiDeckImporter` (reused for import) is a pure parse/fetch importer with no writes to the harvested-corpus schema. |

**Score:** 11/11 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Web/Services/CreatorStyle/ArchidektOwnerClient.cs` | Owner resolve + deck-summary crawl | ✓ VERIFIED | 286 lines, paginated `api/decks/v3/`, `MaxPages`/`MaxDecks`/`MaxResponseBytes` caps, uses named Polly pipeline `banlist` |
| `DeckFlow.Web/Services/CreatorStyle/ArchidektOwnerUrl.cs` | Trusted-host username parsing | ✓ VERIFIED | HTTPS + `archidekt.com` (or subdomain) host guard, regex-validated username |
| `DeckFlow.Web/Services/CreatorStyle/CreatorProfileDeckCrawler.cs` | Crawl + cache orchestration | ✓ VERIFIED | Warm-cache short-circuit, >105 filter, content-hash cache read-through, `SetLastCrawledAsync` |
| `DeckFlow.Core/Content/CreatorProfileSourceStore.cs` / `CreatorDeckCacheStore.cs` | New dialect-guarded tables (D-01) | ✓ VERIFIED | `creator_profile_source` / `creator_deck_cache` DDL present for both SQLite and Postgres branches |
| `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/{StapleStripper,FolderWeighting,CategoryCounter,LiftCalculator,CreatorDeckSample,MeasuredStyleInputs}.cs` | Pure extraction contract | ✓ VERIFIED | All exist, zero HTTP/AspNet references, pure static helpers over `CreatorDeckSample` |
| `DeckFlow.Core/Knowledge/GlobalCategoryBaseline.cs` + `CardCategoryRepository.GetGlobalCategoryBaselineAsync` | Server-side processed-only lift denominator | ✓ VERIFIED | SQL restricts to `deck_queue.processed = 1`; aggregated server-side (no raw-row pull) |
| `DeckFlow.Web/Services/CreatorStyle/CreatorDeckCategoryResolver.cs` | 3-layer category resolution | ✓ VERIFIED | Repository-first, Tagger-tail fallback |
| `DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs` | End-to-end orchestration → `MeasuredMetric[]` → persist | ✓ VERIFIED | 444 lines; category/lift/combo/Karsten metrics assembled and persisted via `ICreatorStyleProfileStore` |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `CreatorProfileDeckCrawler` | `IArchidektDeckImporter` | `ImportAsync(summary.Id, ...)` | ✓ WIRED | Called for cache-miss decks only (line 107) |
| `CreatorProfileDeckCrawler` | `ICreatorDeckCacheStore` | `UpsertAsync` / `GetByCreatorAsync` | ✓ WIRED | Cache write on import, read-through on warm path |
| `MeasuredStyleProfileBuilder` | `CreatorProfileDeckCrawler` | `CrawlAsync` | ✓ WIRED | Line 103-105, first pipeline step |
| `MeasuredStyleProfileBuilder` | `StapleStripper`/`FolderWeighting`/`CategoryCounter`/`LiftCalculator` | direct static calls | ✓ WIRED | Lines 107-129, ordered per D-05/D-03/D-06/D-07 |
| `MeasuredStyleProfileBuilder` | `ICommanderSpellbookService.FindCombosAsync` | `ResolveComboCountAsync` | ✓ WIRED | Null-graceful (line 262) |
| `MeasuredStyleProfileBuilder` | `ManabaseAnalyzer.Analyze` | `AnalyzeDeckAsync` | ✓ WIRED | Fixed `ManabaseMode.Casual` |
| `MeasuredStyleProfileBuilder` | `ICreatorStyleProfileStore.UpsertAsync` | persistence | ✓ WIRED | Line 148 |
| `Program.cs` DI | `CreatorProfileDeckCrawler`/`CreatorDeckCategoryResolver`/`MeasuredStyleProfileBuilder`/`IArchidektOwnerClient` | `AddScoped`/`AddSingleton` | ✓ WIRED | `Program.cs:190-194` |

### Data-Flow Trace (Level 4)

Not applicable in the traditional sense — this is a substrate/backend orchestration phase with no rendered UI. Data-flow was instead traced end-to-end via the persistence round-trip test: `MeasuredStyleProfileBuilderTests.BuildAsync_PersistsProfile_RoundTripsMetricsAndHandlesNullComboGracefully` builds a profile from seeded `CreatorDeckSample`/category/baseline fixtures, persists it through the real `CreatorStyleProfileStore` (SQLite), reloads it, and asserts metric-for-metric equality — confirming the full crawl → strip → count → lift → combo → Karsten → persist chain produces non-empty, non-hardcoded output (`Assert.NotEmpty(stored.MeasuredMetrics)`, category/lift/combo metrics individually asserted).

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| CS-04a | 95-01, 95-06 | Crawler + new creator-profile-source table | ✓ SATISFIED | `CreatorProfileDeckCrawler`, `CreatorProfileSourceStore` |
| CS-04b | 95-02, 95-06 | Resilience + cached deck set | ✓ SATISFIED | Polly `banlist` pipeline reuse, `CreatorDeckCacheStore` warm-cache short-circuit |
| CS-04c | 95-04, 95-06 | Dedup/confidence/oversize filter | ✓ SATISFIED | `StapleStripper.FilterOversized`/`FlagNearPrecons`, >105 filter in crawler |
| CS-04d | 95-01, 95-04 | Folder capture + weighting | ✓ SATISFIED | `ParentFolderId`/`ParentFolderName` captured, `FolderWeighting` |
| CS-05 | 95-03, 95-04 | Hybrid staple-strip before ratios | ✓ SATISFIED | `ContentTagVocabulary.Staples` ∪ `ComputePersonalStaples`, applied pre-ratio |
| CS-06 | 95-05, 95-07 | Multi-bucket category tagging | ✓ SATISFIED | `CategoryCounter`, `CreatorDeckCategoryResolver` |
| CS-07 | 95-03, 95-05 | Lift metric (not raw co-occurrence) | ✓ SATISFIED | `GlobalCategoryBaseline` (processed-only) + `LiftCalculator` |
| CS-08 | 95-07 | Combo density | ✓ SATISFIED | `CommanderSpellbookService.FindCombosAsync`, null-graceful |
| CS-09 | 95-07 | Karsten land/curve scoring | ✓ SATISFIED | `ManabaseAnalyzer.Analyze(ManabaseMode.Casual)` |
| CS-10 | 95-01, 95-07 | `numDecks` on every stat | ✓ SATISFIED | `MeasuredMetric.NumDecks` required int + nested `EffectiveSampleSize` |

No orphaned requirements — all 10 CS-04a..CS-10 IDs map to a finalized 95-0X plan per ROADMAP.md.

### Anti-Patterns Found

None. Scanned all 14 phase-95 production files (`DeckFlow.Core/Knowledge/MeasuredStyleExtraction/*.cs`, `DeckFlow.Core/Knowledge/GlobalCategoryBaseline.cs`, `DeckFlow.Core/Content/CreatorDeckCacheStore.cs`, `DeckFlow.Core/Content/CreatorProfileSourceStore.cs`, `DeckFlow.Web/Services/CreatorStyle/*.cs`) for `TODO|FIXME|XXX|TBD|PLACEHOLDER|placeholder|coming soon|not yet implemented` — zero matches. No empty-return stubs (`return null`/`return Array.Empty<>()` calls are legitimate not-found/no-op-branch behavior, not stubbed feature bodies, and are covered by dedicated tests for those branches, e.g. warm-cache-with-no-source-row, malformed-JSON, response-too-large).

### Behavioral Spot-Checks

Not run as live `curl`/process checks — this phase has no runnable HTTP endpoint (substrate only, no controller/route registered). Behavior was instead verified via the unit/integration test suite already confirmed green by the requester (`dotnet test DeckFlow.sln`: Core 1261 pass, Web 1286 pass, Studio 414 pass, 0 failures). Targeted tests directly exercising phase-95 code: `CategoryCounterTests`, `FolderWeightingTests`, `LiftCalculatorTests`, `StapleStripperTests` (Core), `ArchidektOwnerClientTests`, `CreatorProfileDeckCrawlerTests`, `MeasuredStyleProfileBuilderTests`, `CreatorDeckCacheStoreTests`, `CreatorProfileSourceStoreTests` (Web/Core) — 2,732+ lines of test code across these files.

### Probe Execution

Not applicable — no `scripts/*/tests/probe-*.sh` declared or referenced in any 95-0X PLAN/SUMMARY, and this is not a migration/CLI-tooling phase. Step 7c SKIPPED.

### Human Verification Required

None required to pass this phase's automated goal-backward check. Two items are explicitly logged as **Manual-Only** in `95-VALIDATION.md` (live Archidekt crawl against the real Salubrious Snail account, and the full 39-deck live round-trip) — these were intentionally deferred to a manual pre-flip smoke test per D-12 ("the deterministic Snail seed corpus is the automated substitute; the live 39-deck crawl is manual-only"). Since this phase ships no user-visible surface and Phase 97 (fusion, the actual consumer) has not started, there is no urgency to run the live crawl now; flag it for whoever first invokes `MeasuredStyleProfileBuilder.BuildAsync` against a real creator (likely early Phase 97 integration or an ops/backfill task) to confirm the live Archidekt shape still matches `ArchidektDeckSummary`/`ArchidektOwnerClient` parsing assumptions.

### Gaps Summary

No gaps. All 11 derived must-haves (roadmap success criteria 1-5 plus the CS-04a..CS-10 requirement-level and D-11/Pitfall-3 constraint-level truths) are verified directly against the shipped code, not summary claims. All 7 SUMMARY.md files exist for plans 95-01 through 95-07. No debt markers, no stub implementations, no orphaned wiring. Solution build/test state (Core 1261, Web 1286, Studio 414, 0 failures) was independently confirmed present in git history (commits `f63561aa` through `072df205` on `plan/cycle-17-creator-style`) and is consistent with the code inspected here.

---

*Verified: 2026-07-12T04:59:38Z*
*Verifier: Claude (gsd-verifier)*
