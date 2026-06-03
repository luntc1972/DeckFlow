# CAT-01 — Production index build + deploy (run once)

**Date:** 2026-05-24
**Branch/deploy:** `v1.4` (Render service `DeckFlow`, db `dpg-d7oj8iugvqtc73fso0g0-a`, Postgres 18)

## Why

`/suggest-categories` and `/commander-categories` were returning HTTP 500.
Root cause: `card_deck_totals` (~4.1M rows) and `card_category_observations`
(~3.9M rows) had **no index** on `normalized_card_name`. The lookup query
(`WHERE normalized_card_name = @n`) full-scanned under harvest write load and
timed out (`Npgsql … Timeout during reading attempt`).

The code now creates these indexes in `EnsureSchemaAsync` (`IF NOT EXISTS`),
but building them at startup on the live ~4M-row tables exceeded the 30s
command timeout and **crashed the deploy**. Startup index creation is now
non-fatal (bounded 15s, logged + skipped), so deploys no longer crash — but
the index still must be built **out-of-band** for the queries to be fast.

## Step 1 — Build the indexes on production (one time)

Connect to the Render Postgres (external connection string from the Render
dashboard → database → "Connect" → External, or the dashboard PSQL console).

Run each statement **separately** and **NOT inside a transaction**
(`CREATE INDEX CONCURRENTLY` is forbidden inside a transaction block):

```sql
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_card_deck_totals_normalized
  ON card_deck_totals (normalized_card_name);

CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_card_category_observations_normalized
  ON card_category_observations (normalized_card_name);
```

`CONCURRENTLY` does not lock out the running harvest's writes; it just takes
a little longer (≈1–2 min each on ~4M rows).

### Verify

```sql
-- Both should be listed and valid (indisvalid = true):
SELECT i.relname AS index_name, idx.indisvalid
FROM pg_index idx
JOIN pg_class i ON i.oid = idx.indexrelid
JOIN pg_class t ON t.oid = idx.indrelid
WHERE t.relname IN ('card_deck_totals', 'card_category_observations')
  AND i.relname LIKE 'ix_%_normalized';
```

If an index shows `indisvalid = false` (a CONCURRENTLY build was interrupted),
drop and rebuild it:

```sql
DROP INDEX CONCURRENTLY IF EXISTS ix_card_deck_totals_normalized;
-- then re-run the CREATE INDEX CONCURRENTLY above
```

## Step 2 — Deploy `v1.4`

Render `autoDeploy` is **off**. Trigger a manual deploy of `v1.4` from the
Render dashboard. Startup `EnsureSchemaAsync` will see the indexes already
exist and no-op; the host boots normally.

## Step 3 — Smoke test

On https://www.deckflow.gg/suggest-categories submit a deck containing
**Sol Ring**. Expect categories to return (e.g. Artifact / Ramp), no 500.
If the DB is briefly slow it now returns a graceful 503 ("temporarily
unavailable"), never a raw 500.

## Notes

- The category pages no longer trigger an on-demand harvest sweep (removed in
  `v1.4`); they read cached data only. The background `ArchidektCacheJobService`
  and admin harvest still populate the cache.
- If indexes are ever missing again (e.g. fresh DB), the app still boots
  (non-fatal) — just rebuild them per Step 1 for full speed.
