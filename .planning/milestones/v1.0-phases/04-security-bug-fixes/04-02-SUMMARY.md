---
phase: 04-security-bug-fixes
plan: 02
subsystem: integration
tags: [scryfall, tagger, http, cache, memory-cache, head-probe]

requires:
  - phase: 03-tech-debt-cleanup
    provides: "Stable Tagger session-cache + cookie-disabled SocketsHttpHandler infrastructure remained intact through TD cleanup; this plan layers iterate-printings on top, not replacing it."
provides:
  - "ScryfallTaggerService.ResolveCardPrintingAsync replaced — `/cards/search?q=!\"<name>\"&unique=prints` (no order=) + up to 5 RestSharp HEAD probes against tagger.scryfall.com; first 200 wins."
  - "IMemoryCache layer over printing resolution — 24hr positive TTL on winning (set, collector_number) tuple; 1hr negative TTL on null sentinel for empty/all-404 results."
  - "Cache key shape `tagger-printing:` + CardNormalizer.Normalize(cardName) — deterministic, lowercase-stripped, DFC-aware."
  - "Bounded probe loop — MaxProbeAttempts = 5, never exceeds even with longer printing lists; on full miss emits LogWarning('Tagger has no indexed printing for {CardName} after {Attempts} probes')."
  - "Program.cs ScryfallTaggerService DI converted from `AddSingleton<I,T>()` shorthand to factory closure resolving IMemoryCache (mirrors CommanderSpellbookService shape)."
  - "ScryfallTaggerServiceTests CreateService helper updated for 5-arg ctor + 5 new tests covering cold lookup with mid-list winner, all-5-probes-404, positive cache hit, negative cache hit, empty Scryfall search."
affects: [phase-05-anything-touching-tagger-flow, /suggest-categories-mode=All-aggregator]

tech-stack:
  added: []
  patterns:
    - "Iterate-printings + HEAD probe pattern — used here to discover Tagger-indexed printings without paying per-printing body weight."
    - "Two-tier IMemoryCache TTL pattern — long positive (24h) + short negative (1h) preserves freshness for upstream recovery while eliminating repeat work for hot keys."
    - "Factory-closure DI registration with IMemoryCache resolution — second example in the codebase after CommanderSpellbookService; pattern is now established."

key-files:
  created: []
  modified:
    - DeckFlow.Web/Services/ScryfallTaggerService.cs
    - DeckFlow.Web/Program.cs
    - DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs
    - .planning/phases/04-security-bug-fixes/04-HUMAN-UAT.md

key-decisions:
  - "D-09: iterate-printings strategy — replace /cards/named with /cards/search + per-printing Tagger probe; first 200 wins."
  - "D-10 (corrected): probe order = Scryfall default (NO `order=` parameter); RESEARCH.md found that explicit `released-desc` actually surfaces Secret Lair Drops first (Tagger-unindexed), while default heuristic prioritizes mainline-set printings."
  - "D-11: probe ceiling = 5 (MaxProbeAttempts); on full miss LogWarning + return []."
  - "D-12: IMemoryCache 24hr positive, 1hr negative; cache key 'tagger-printing:' + CardNormalizer.Normalize(name)."
  - "D-14: BUG-01 verification = mocked-HTTP unit tests + live UAT browser walk on /suggest-categories with Sol Ring."
  - "D-15: SC #3 regression matrix = manual UAT for /sync, /chatgpt-packets, /suggest-categories mode=All."

patterns-established:
  - "HEAD-probe before GET — when probing existence of an upstream page, prefer Method.Head to skip body bytes; HTTP 200 / 404 contract is sufficient."
  - "Generic-tuple cache values — `((string Set, string Number)?)` allows null sentinel for negative cache distinct from cache miss (TryGetValue false)."
  - "`!\"<name>\"` exact-name Scryfall search — required for printings iteration; bare-name search returns oracle-grouped result."

requirements-completed: [BUG-01]

duration: 30min
completed: 2026-05-01
---

# Phase 04 Plan 02: ScryfallTagger Iterate-Printings Fix Summary

**Iterate-printings flow + IMemoryCache cures the silent-empty Tagger bug for cards whose Scryfall default printing is unindexed (Sol Ring, Counterspell, recent staples) — the resolver now sweeps up to 5 printings via HEAD probes and caches the winner, closing BUG-01.**

## What Was Built

`ScryfallTaggerService.ResolveCardPrintingAsync` was rewritten end-to-end. It now:

1. Computes a cache key as `tagger-printing:` + `CardNormalizer.Normalize(cardName)` and short-circuits on positive (tuple) or negative (null sentinel) cache hits.
2. On miss, calls Scryfall `cards/search` with `q=!"<name>"&unique=prints` (NO `order=` parameter — RESEARCH.md correction to D-10; default ordering yields better Tagger coverage than `released-desc`, which surfaces Secret Lair drops first).
3. Iterates the `data` array up to `MaxProbeAttempts = 5`, performing a RestSharp `Method.Head` probe against `tagger.scryfall.com/card/{set}/{number}` for each printing. First 200 wins → cache positive 24hr, return tuple.
4. If all probes 404 (or fewer than 5 valid printings exist), caches negative 1hr, emits `LogWarning("Tagger has no indexed printing for {CardName} after {Attempts} probes")`, returns `("", "")` — the existing CSRF/GraphQL caller treats empty tuple as "no tags found", preserving the graceful-fallback contract.
5. Search-side failures (non-success or empty `data` array) take the same negative-cache + warn-log + empty-tuple path; no exceptions leak.

The constructor takes `IMemoryCache` as the new 5th positional parameter (before the optional `ILogger`), with `ArgumentNullException.ThrowIfNull` guards added consistent with sibling services. All other methods (`LookupOracleTagsAsync`, `FetchTaggerSessionAsync`, `QueryTaggerGraphQlAsync`, `BuildCookieHeader`, `StripCookieAttributes`, `RefreshSessionAndRetryAsync`) remain byte-identical.

`Program.cs` flips the DI registration from `AddSingleton<IScryfallTaggerService, ScryfallTaggerService>()` shorthand to a factory closure resolving `IMemoryCache`, mirroring the `CommanderSpellbookService` pattern that already lives a few lines above.

## Tests

`ScryfallTaggerServiceTests.CreateService` helper extended with optional `IMemoryCache? memoryCache = null` (defaults to a fresh `MemoryCache(new MemoryCacheOptions())`); existing 4 tests continue to pass via the same helper.

5 new tests added (xunit + RichardSzalay.MockHttp):

| # | Method | Asserts |
|---|--------|---------|
| 1 | LookupOracleTagsAsync_ColdLookup_ThirdPrintingHits_ReturnsTaggerData | 3 printings (soc/128, tmc/59, lea/270); HEAD probes 1+2 = 404, probe 3 = 200; subsequent CSRF GET + GraphQL POST resolve real tags; each route hit exactly once |
| 2 | LookupOracleTagsAsync_AllFiveProbes404_ReturnsEmpty | 5 printings, all HEAD = 404; result empty; probe 6 NOT hit (cap honored) |
| 3 | LookupOracleTagsAsync_PositiveCacheHit_SkipsScryfall | Cache pre-populated with `("lea", "270")` at `tagger-printing:sol ring`; result non-empty; Scryfall search hit count = 0 |
| 4 | LookupOracleTagsAsync_NegativeCacheHit_ReturnsEmptyWithNoUpstream | Cache pre-populated with null sentinel; result empty; Scryfall search hit count = 0 |
| 5 | LookupOracleTagsAsync_ScryfallSearchEmptyData_ReturnsEmpty | Search returns `data:[]`; result empty (negative cache populated for follow-up calls) |

## Commits

| SHA | Subject |
|-----|---------|
| 06ea1a7 | feat(04-02): replace ResolveCardPrintingAsync with iterate-printings + IMemoryCache (BUG-01) |
| 4585269 | test(04-02): cover ResolveCardPrintingAsync iterate-printings + cache paths (BUG-01) |

## Build / Verification

`dotnet build DeckFlow.sln -m:1 -p:BuildInParallel=false` — clean, 0 warnings, 0 errors. Same WSL-MSBuild serialization workaround used in plan 04-01 applied here. VSTest in WSL is unreliable per PROJECT.md, so the gate is `Build succeeded` plus push-and-watch CI on Render.

`grep -cE 'order=' DeckFlow.Web/Services/ScryfallTaggerService.cs` returns 0 — RESEARCH.md correction to D-10 honored (no `order=` parameter introduced).

## Outstanding — Live UAT (Plan Task 3)

`04-HUMAN-UAT.md` (combined with plan 04-01's UAT in the same file) carries pending entries for:

- **BUG-01 / SC #2:** Sol Ring browser walk on `/suggest-categories` mode=ScryfallTagger; expect non-empty real tag list within ~6s. Repeat with another cEDH staple to reduce single-card-luck risk.
- **SC #3 regression matrix:** /sync deck reconcile, /chatgpt-packets artifact, /suggest-categories mode=All — each must produce same prompt artifacts as pre-deploy.

UAT closes only after Render auto-deploy completes (~17min from `git push main`) and the user records PASS evidence. SC #2 and SC #3 of Phase 04 hinge on this walk.

## Deviations

- Plan called for one `dotnet build DeckFlow.sln` invocation per task; environment forced `-m:1 -p:BuildInParallel=false` workaround (also applied across all 04-01 tasks). Build correctness identical.
- No other deviations.
