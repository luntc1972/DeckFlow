---
status: testing
phase: 24-card-category-lookup-fix-colorless-staple-cards
source: [.planning/debug/resolved/sol-ring-empty-categories.md]
started: 2026-05-24
updated: 2026-05-24
---

## Current Test

number: 2
name: Sol Ring returns non-empty correct categories (+ regression set)
expected: |
  On /suggest-categories (and /commander-categories), Sol Ring resolves to non-empty
  categories (e.g. Ramp / Artifact); a small set of other colorless/staple cards also
  resolve (SC2).
awaiting: user response

## Tests

### 1. Root cause documented + both harvest states
expected: Debug session documents root cause; running harvest reproduced/ruled-out in BOTH running and stopped states (SC1).
result: pass

### 2. Sol Ring returns non-empty correct categories (+ regression set)
expected: On /suggest-categories (and /commander-categories), Sol Ring resolves to non-empty categories (e.g. Ramp / Artifact); a small set of other colorless/staple cards also resolve (SC2).
result: issue
reported: "Tested Sol Ring and Dark Ritual on /suggest-categories — both return 'Unable to fetch suggestions.' (HTTP 500). Reproduced on production."
severity: blocker
root_cause: |
  SEPARATE pre-existing bug, NOT the category fix. Production stack trace:
  Npgsql.NpgsqlException -> System.TimeoutException 'Timeout during reading attempt'
  in CategoryKnowledgeRepository.GetCardDeckTotalsAsync (line 584) <- CategoryKnowledgeStore.GetCardDeckTotalsAsync:256
  <- CategorySuggestionService.SuggestAsync:141 <- SuggestionsApiController.PostCardSuggestionAsync:73.
  Query: SELECT board, SUM(deck_count) FROM card_deck_totals WHERE normalized_card_name=@n GROUP BY board.
  No index on card_deck_totals(normalized_card_name) -> full scan -> Postgres read timeout under
  concurrent harvest write load (~70s). NpgsqlException is not in the controller catch filter
  (DeckParseException/InvalidOperationException/HttpRequestException) -> uncaught 500 + error page.
  Three defects: (1) missing index on card_deck_totals(normalized_card_name) and likely
  card_category_observations(normalized_card_name) (GetCategoriesAsync queries the same way);
  (2) error-handling gap (DB timeout -> 500 instead of graceful 503); (3) harvest concurrency aggravator.
  Blocks live verification of the (correct, unit-verified) category fix.
resolution: |
  FIXED (commits 686b348 test, fadc9e4 fix): added indexes on
  card_deck_totals(normalized_card_name) and card_category_observations(normalized_card_name);
  PostCardSuggestionAsync now catches DbException -> 503 instead of 500.
  ALSO removed the per-click cache sweep from both category pages (commits 01881f3, d56fe55)
  to cut latency/DB load (the harvest-concurrency aggravator). Build clean, Core 69/69,
  Web 462 pass / 13 pre-existing CSS fails.
  RETEST PENDING: live confirmation that Sol Ring returns non-empty categories on a rebuilt app
  (local rebuild, or production deploy — note Render autoDeploy is OFF, manual deploy needed).

### 3. No regression in existing coverage; affected path has regression test
expected: Previously-working cards still return categories; the fixed path is covered by an automated regression test (SC3).
result: [pending]

### 4. Test suite preserved at Failed:0; touch-only-what-you-touch
expected: No NEW test failures introduced; change is scoped (CLAUDE.md R-6) (SC4).
result: [pending]

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0

## Gaps

[none yet]
