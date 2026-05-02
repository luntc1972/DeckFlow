---
phase: 04-security-bug-fixes
plan: 03
status: complete
requirements: [BUG-01]
commits:
  - cd04fa8: feat(04-03) sort Scryfall printings by released_at ASC
  - 33dd916: test(04-03) oldest-first iteration regression test
---

# Plan 04-03 Summary — BUG-01 v2: oldest-first printing iteration

## What Was Wrong With Plan 04-02

Plan 04-02 replaced the single-printing 404-prone Scryfall Tagger lookup with iterate-up-to-5-printings + IMemoryCache. Static verification passed 13/13. Live UAT against deckflow.gg revealed the fix was empirically defective: cEDH staples like Sol Ring and Counterspell still returned `hasTaggerCategories: false` (`noSuggestionsFound: true`) via `/api/suggestions/card` mode=ScryfallTagger.

Direct Scryfall probe with the production query confirmed the cause:

```
$ curl --get https://api.scryfall.com/cards/search \
    --data-urlencode 'q=!"Sol Ring"' --data-urlencode 'unique=prints'
```

First 5 printings under default ordering:

| # | set | num  | released_at | set_type  |
|---|-----|------|-------------|-----------|
| 1 | soc | 128  | 2026-04-24  | commander |
| 2 | tmc | 59   | 2026-03-06  | eternal   |
| 3 | ecc | 57   | 2026-01-23  | commander |
| 4 | ecc | 58   | 2026-01-23  | commander |
| 5 | eoc | 57   | 2025-08-01  | commander |

All 5 are 2024-2026 commander/eternal sets. None are indexed by Scryfall Tagger. Probe loop hit `MaxProbeAttempts=5` with all 404 → returned empty + LogWarning. Same outcome as old single-printing code, just slower.

Plan 04-02's research correction "no `order=` parameter" (avoid `released-desc` because it surfaces Secret Lair drops) was directionally wrong: Scryfall's *default* ordering also empirically yields too-new commander/eternal printings first.

## What Was Fixed

`ScryfallTaggerService.ResolveCardPrintingAsync` now sorts the Scryfall search `data` array by `released_at` ASC (oldest first) before entering the HEAD-probe loop. Older expansion/core/masters printings (e.g., LEA Sol Ring released 1993-08-05) ARE Scryfall-Tagger-indexed.

### Implementation Details

- Sort key: `(SortBucket, DateTimeOffset)` where `SortBucket=0` for valid `released_at`, `SortBucket=1` with `MaxValue` for null/missing/unparseable. Drops malformed rows to end of probe order.
- Sort is a single LINQ `OrderBy` (stable) on the `JsonElement.EnumerateArray()` enumeration, applied inline in the `foreach`.
- No change to MaxProbeAttempts (still 5), cache durations (24h positive / 1h negative), cache key shape, ctor signature, or HEAD-probe logic itself.

### Test Coverage

`LookupOracleTagsAsync_MixedAgePrintings_OldestProbedFirst` — fixture has 5 printings with `released_at` values shuffled (2023, 1993, 2024, 2010, 2018). All HEAD probes return 404 so probe loop runs to completion. Captures URL paths and asserts ASC released_at order: `lea/270 → m10/220 → a25/1 → eoc/11 → dsk/142`. Test fails against pre-sort implementation (real regression check, not tautology).

## Commits

```
33dd916 test(04-03): cover oldest-first printing iteration (BUG-01 v2)
cd04fa8 feat(04-03): sort Scryfall printings by released_at ASC before tagger probe (BUG-01 v2)
```

## Verification

- Local: `dotnet build DeckFlow.sln -m:1 -p:BuildInParallel=false` clean (0 errors, 0 warnings)
- Live: Pending push + Render redeploy; UAT 3-4 (Sol Ring + Counterspell) re-run on production after deploy

## Files Modified

- `DeckFlow.Web/Services/ScryfallTaggerService.cs` (+18, -1)
- `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs` (+53, -0)
