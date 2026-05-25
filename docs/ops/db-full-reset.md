# RUNBOOK — Full database reset ("start over")

**Date:** 2026-05-24
**DB:** Render `dpg-d7oj8iugvqtc73fso0g0-a` (Postgres 18, `basic_256mb`).
**Authorized by:** user, 2026-05-24 (full reset — ALL tables, including `feedback`).
**Who runs it:** the user. Claude/Codex must NOT run this (production + destructive;
the Render MCP query tool is read-only anyway).

> ⚠️ **DESTRUCTIVE — irreversible.** This deletes ALL application data: harvested
> category cache, deck queue, harvest history, request metrics, feature flags, and
> **user feedback**. Take a backup first (Render dashboard → database → Backups, or
> `pg_dump`) before running anything here.

## Recommended sequencing (do it WITH the schema-redesign deploy)

Per the optimization plan, the schema redesign lands **first**. To avoid harvesting
twice, do the wipe as part of that rollout, not before it:

1. Ship the normalized-schema code to `v1.4` (new `EnsureSchema*` creates the new tables).
2. Take a backup.
3. Reset the DB (Step A below).
4. Deploy; the app recreates the (new) empty schema on startup.
5. Re-harvest from scratch (admin harvest) straight into the optimized schema.

Doing the wipe **now**, before the redesign, means you'd re-harvest into the current
wide schema and then re-harvest again after the redesign — wasted work. Prefer the
sequence above.

## Step A — Full reset

The app recreates all required tables on startup via each store's `EnsureSchema*`
(category knowledge, feedback, feature flags, harvest, metrics). So the simplest,
most complete reset is to drop and recreate the `public` schema, then redeploy/restart
so the app rebuilds empty tables.

```sql
-- Nuclear full reset. Run against the Render Postgres (psql / dashboard console).
-- Confirm you have a backup first.
DROP SCHEMA public CASCADE;
CREATE SCHEMA public;
GRANT ALL ON SCHEMA public TO deckflow_admin;
GRANT ALL ON SCHEMA public TO public;
```

Then **redeploy / restart** the Render service so startup re-runs every `EnsureSchema*`
and recreates empty tables. Verify the app boots (startup DB validation passes) and
`\dt` shows the expected tables, all empty.

### Alternative — truncate only (keeps schema/objects)

If you'd rather keep the schema objects and just empty the data (current schema only):

```sql
TRUNCATE TABLE
  card_category_observations,
  card_deck_totals,
  deck_queue,
  crawl_state,
  harvest_runs,
  harvest_schedule,
  request_metrics,
  request_metric_ip_seen,
  admin_brute_force_buckets,
  feedback,
  feedback_meta,
  feature_flags
RESTART IDENTITY CASCADE;
```

(Adjust the table list to whatever `\dt` shows at run time — table set changes as
features ship. After the schema redesign the category table names will differ.)

## Step B — Re-harvest

Trigger a fresh Archidekt harvest from the admin UI (`/Admin/Harvest`) to repopulate
the category cache. Note that with the click-sweep removed (v1.4), the category pages
read cache only — they will be empty until the harvest has run.

## Notes

- After reset, rebuild any indexes the app does not create automatically per
  `docs/ops/cat-01-suggestion-indexes.md` (or confirm `EnsureSchemaAsync` created them
  — with the new normalized schema, indexes are part of the schema and rebuild on a
  clean DB).
- `feature_flags` will be recreated empty — re-set any flags (e.g. `content_kb_enabled`)
  after reset if you rely on non-default values.
