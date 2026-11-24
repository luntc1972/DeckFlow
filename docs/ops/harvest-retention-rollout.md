# RUNBOOK — Legal-Commander harvest retention rollout

## Purpose

Roll out the legal-Commander gate, compact the per-deck tables, reduce each commander's retained decks, and queue metadata-less decks for refetch without exhausting the production Postgres disk.

**Who runs it:** the operator. An agent must never run these production steps, change the Render environment, or press the Admin/Harvest maintenance controls.

## Before you start

1. Confirm a recent Postgres backup exists in the Render dashboard. The compaction step is one-way.
2. On Admin/Harvest, pause both the Bulk Schedule and Update Schedule. Cancel any active run and wait until no run is active. Keep both schedules paused through Step 4.
3. Confirm the Web service has `RENDER_API_KEY` and `RENDER_POSTGRES_ID` set in the Render dashboard. `render.yaml` declares both with `sync: false`, so repository changes do not update the live service. Never copy either value or the database identifier into this runbook, a log, or a command. Without both variables, every Postgres Purge, Drain, and post-harvest retention run that has removals to make stops at its first disk probe with `DiskProbeUnavailable`. Refetch does not use the disk probe.

Scope: `HarvestRetention__` overrides, including the `HarvestRetention__PostHarvestMaxDecks` 0 kill switch, apply to the Web host only; the CLI always uses the defaults.

## Settings

| Setting | Default | Meaning |
| --- | --- | --- |
| `PerCommanderCap` | 100 | `HarvestRetention__PerCommanderCap`: decks retained per commander. |
| `BatchSize` | 25 | `HarvestRetention__BatchSize`: decks removed per disk check. One 30-second Render sample vouches for at most 10 batches, so up to 10 times BatchSize decks (250 at the default) may be removed before the next sample shows their WAL; raising BatchSize raises this bound in proportion. |
| `PauseMilliseconds` | 500 | `HarvestRetention__PauseMilliseconds`: pause between retention batches. |
| `MinFreeDiskBytes` | 1073741824 | `HarvestRetention__MinFreeDiskBytes`: free-disk floor below which removal stops. |
| `MaxAgeDays` | null | `HarvestRetention__MaxAgeDays`: a null binding value means unset, so the optional age limit is off and excluded from the Preview over-cap count. |
| `PostHarvestMaxDecks` | 500 | `HarvestRetention__PostHarvestMaxDecks`: maximum automatic removals after a harvest; 0 is the post-harvest kill switch. |
| `MaintenanceChunkDecks` | 1000 | `HarvestRetention__MaintenanceChunkDecks`: decks processed per maintenance sweep-gate hold. |
| `MaintenanceMaxDecksPerRun` | 0 | `HarvestRetention__MaintenanceMaxDecksPerRun`: maximum decks removed by one maintenance Drain run; 0 is unbounded. Purge, Backfill, and post-harvest retention are unaffected. |

## Before deploying the processing-time Stats fix

Build the rolling Stats index on production Postgres before deploying the application change. Run the concurrent build outside a transaction to avoid a table write lock during application startup.

```sql
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_deck_queue_processed_last_checked
    ON deck_queue (last_checked_utc) WHERE processed = 1 AND skipped = 0;
```

Verify the index exists and `pg_index.indisvalid` is true before deploying.

```sql
SELECT indisvalid
FROM pg_index
WHERE indexrelid = to_regclass('ix_deck_queue_processed_last_checked');
```

If the index exists but is invalid, run `DROP INDEX CONCURRENTLY IF EXISTS ix_deck_queue_processed_last_checked;` outside a transaction, then rebuild and recheck it. If the check returns no row, build the index and recheck it.

## Step 1 — Deploy gate and filters

Deploy the legality gate and Commander-only discovery filters. Check boot logs for schema errors, then open Admin/Harvest and confirm the Stats card shows skipped decks by reason. The gate keeps only Commander-format decks with a commander and legal Commander deck size; stored decks that become illegal have their category rows removed and remain recorded with a skip reason.

## Step 2 — Deploy compaction and check boot logs

Deploy the per-deck table compaction while both schedules remain paused. Check boot logs for each table: if either reports compaction skipped because a lock was busy, restart once and check again. Complete the rollout's read-only schema and disk-headroom checks before continuing. Stop before Step 3 if compaction, schema verification, or headroom verification fails.

## Marker backfill (before Step 3)

The old maintenance count queries timed out. The stored-live marker replaces those queries, but legacy rows must be backfilled before maintenance reads are ready. Preview shows "Maintenance counts wait for the marker backfill. Run Marker backfill first." and returns no counts while it is pending. Drain and Refetch are refused with `marker_backfill_pending`; Purge, Refetch runs, and Web post-harvest retention stop with `MarkerBackfillPending` without changing decks. Keep both harvest schedules paused and wait for any active run to end.

Repeat the Before you start checks and Step 2's schema and disk-headroom verification. Before starting, require free space of at least the configured floor (default 1,073,741,824 bytes), plus the rollout safety margin, plus the extra row-version and index growth measured in Task 1. After completion, require the floor plus the safety margin. Use the shared Render disk check (D-DP), not database size alone; do not start if headroom verification fails.

On Admin/Harvest, select the confirmation checkbox and press **Marker backfill**. It runs 500-deck transactions, checking free disk before every batch and pausing between batches. The batch size is a constant, not a `HarvestRetention__` setting. Render disk samples pace the run; allow about 35 minutes or more [estimate]. Handle `DiskFloor` and `DiskProbeUnavailable` as described in Watching a run. Press **Marker backfill** again after a stop or restart to resume from the saved cursor. Once complete, pressing it again completes immediately without a disk probe.

Check boot logs for a Warning about the stored-live marker index. If boot could not create the index, the operator builds it using the concurrent fallback:

```sql
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_deck_queue_stored_live ON deck_queue (LOWER(commander_name)) WHERE has_stored_live_rows = 1;
```

Verify `pg_index.indisvalid` for the index. If it is false, `IF NOT EXISTS` will not repair it: the operator must run `DROP INDEX CONCURRENTLY IF EXISTS ix_deck_queue_stored_live;` and then rebuild it with the statement above. Run concurrent index statements outside a transaction.

To reset the backfill, keep harvest paused and wait until no maintenance run is active. The operator deletes the `crawl_state` keys `stored_live_rows_backfill_complete` and `stored_live_rows_backfill_cursor`, then selects the confirmation checkbox and presses **Marker backfill** to recheck all rows. Proceed to Step 3 only after the backfill completes, the index is valid, and headroom verification passes.

## Step 3 — Preview, Purge, Drain

Deploy the Retention maintenance card, then press Preview. Planning estimates are 341,656 total decks, about 187,000 decks over the cap, about 11,000 purge candidates, and about 16,000 refetch candidates; the live Preview counts are authoritative, including queued for refetch. Stop and report a difference of an order of magnitude.

Tick the confirmation box and run Purge. Wait for completion, then Preview again until purge candidates are 0. Next, tick the confirmation box and run Drain. The server refuses Drain while purge candidates remain. Repeat Preview and Drain after recoverable stops until the status shows completed and Preview shows 0 decks over the cap. With `MaintenanceMaxDecksPerRun=0`, if a Drain stopped with `Completed` and the over-cap count remains above 0, stop the rollout and report that it made no progress. A bounded pilot stops with `PerRunCapReached` (admin status `per-run-cap-reached`) at its configured cap when excess decks remain. This is an intentional bounded stop; review pilot measurements before another run. For `DiskFloor`, `DiskProbeUnavailable`, or `MaxDecksReached`, resolve the displayed stop reason, press Preview, and run Drain again before Refetch.

## Archive pilot and full-drain release

These are operator-run production gates. Record an explicit proceed or hold decision before deploying the archive change. A hold keeps Drain stopped. Keep an operator evidence record with the approved SHA, timestamps, measurements, and each gate result.

1. Pause both harvest schedules and confirm no maintenance run is active. Obtain a fresh Render disk measurement from one series: its newest point must be no older than 180 seconds, its unit must be listed, its base must be decimal or explicitly labelled, and free headroom must be at least 1,573,741,824 bytes. Stop if any check fails. The in-app free-disk floor does not replace this check.
2. Set `HarvestRetention__MaintenanceMaxDecksPerRun=100` and deploy the approved SHA through the normal deployment approval process. Keep schedules paused and perform no URL imports or other admin writes during measurement.
3. Before Drain, record manifest count, database bytes, WAL position, category lookup output for five pilot cards, and live row counts. Measure heap tuple bytes with `SELECT COALESCE(SUM(pg_column_size(t.*)), 0) FROM <table> t` for `card_category_observations`, `card_deck_totals`, and each archive table: `card_category_archive`, `card_category_deck_archive`, `card_deck_totals_archive`, and `deck_category_archive_manifest`. Exclude indexes. Record relation sizes for disk tracking only; they do not shrink on delete and are not O4 inputs.
4. Run the bounded Drain once. Record the same measurements afterward, plus write counters, batch durations, and lock waits. Require exactly 100 new manifests, unchanged category lookups for all five cards, and no lock-wait or batch timeout. A smaller backlog requires a separately approved pilot size and matching manifest delta.
5. Require the amended O4 size gate (user decision 2026-11-17 12:19 MST): the net saving is positive, meaning the incremental archive heap tuple bytes are below `live_freed = (obs_sum_before - obs_sum_after) + (totals_sum_before - totals_sum_after)`; and the projected full-drain archive (pilot archive bytes per deck times the decks over the cap at Preview) fits within fresh Render disk headroom above the free-disk floor. Compare heap tuples on both sides, excluding indexes. No other writes may occur between samples. If any gate fails, keep Drain stopped and report the failing measurement before changing archive representation or continuing.
6. Choose BatchSize and any stop threshold from the measured pilot results. Set `HarvestRetention__MaintenanceMaxDecksPerRun=0`, deploy, and repeat the fresh Render disk check from step 1. Run Drain; require retention excess falling to 0, purge candidates remaining 0, and unchanged pilot-card lookups. End the drain release at `Harvest.Maintenance.Completed operation=Drain` with `stopReason=Completed` and Preview confirming 0 excess, then continue to Step 4. Save final evidence in the operator record.

## Step 4 — Refetch

Tick the confirmation box and run Refetch. The server refuses Refetch until Preview shows purge candidates 0 and decks over the cap 0. Refetch only queues decks; the harvest drains them after Step 5 resumes the schedules, and then read the acceptance sentence below.

Step 4 is complete when Preview shows refetch candidates: 0 and queued for refetch: 0 after the harvest has drained the queue.

- A deck Archidekt reports as "Deck not found" is archived when fetched: card knowledge is kept, it drops out of commander counts, the Stats skip reason is `not_found`, and the log shows `Harvest.DeckNotFound` with disposition `archived` or `marked`.
- Transient and other failures stay `fetch_failure` with their stored rows, and the next Refetch retries them.
- A deck already in the retention archive keeps `retention`.
- A `fallback_fetch_failure` disposition means the archive step failed for that deck. The deck is still marked `not_found`, its card rows stay, it drops out of commander counts, and Refetch does not requeue it; report it.
- After the release containing the harvest changes is live, press Refetch once. This requeues the fetch_failure candidates that remain.
- If refetch candidates remain once queued for refetch reaches 0, press Refetch once more and wait again. A remainder after that second drain is a non-"Deck not found" failure to investigate, and Step 4 is not complete.

- Refetch marks decks in transactions of up to 500 decks, with at most `MaintenanceChunkDecks` decks per sweep-gate hold. The card shows decks, batches, rate, and remaining.
- Stop takes effect before the next batch. The free-disk floor and Render probe are checked before every batch; follow "Watching a run" for stops.
- Committed batches stay committed after a Stop, failure, deploy, or restart. Pressing Refetch again continues, and Preview's refetch count falls by the decks marked.
- A harvest sweep running at the same time makes Refetch wait at the next chunk boundary, so the 2-minute no-progress warning can appear meanwhile.
- Each chunk logs `Harvest.Maintenance.RefetchChunk` with elapsedMs. Stop and report if any chunk exceeds 60000 ms.
- Lowering `HarvestRetention__MaintenanceChunkDecks` below 500 lowers the transaction size. This needs a restart, and the value is restored afterwards.
- Expect about 15-30 minutes for about 92k decks.

A deck removed by retention stays pruned. It returns only when its owner changes its commander or main deck on Archidekt; sideboard or maybeboard edits leave it pruned.

Known limit: changing only the quantity of a commander or main-deck card that has no category tag is not detected, so that deck stays pruned; adding, removing, or swapping a card is detected.

## Step 5 — Resume schedules

Resume both the Bulk Schedule and Update Schedule. Confirm both cards show `Paused: No` and both next-scheduled-run lines show a time rather than Paused or Off. Restore Render autoDeploy only after Refetch has completed on the deployed fix SHA. After the first harvest run, confirm logs contain `Harvest.Retention.Completed` and the skipped-decks counts begin updating.

## Watching a run

- The Retention maintenance card refreshes every 5 seconds while a run is active. It shows the start time, decks and batches done, the last batch time, the rate, and the remaining estimate from the Preview counts the run read at start.
- A warning of no progress for more than 2 minutes can be normal while a chunk ranks candidates or waits for a fresh disk sample. Press Stop if it persists or disk usage climbs.
- After Stop, the card shows the stop-requested headline. The current batch finishes, or 30 seconds pass, and the run ends Stopped and logs `Harvest.Maintenance.Stopped`.
- An out-of-order Drain or Refetch is refused by the run itself; the card shows a refused headline naming the step to run first.
- Watch the Render disk-usage graph as well as the status line. The graph includes WAL generated by removals.
- `MarkerBackfillPending` means maintenance is waiting for the marker backfill. Keep harvest paused, run **Marker backfill** to completion, then retry the operation.
- `DiskFloor` means free space reached the configured floor. Wait for a Postgres checkpoint to recycle WAL, check the graph, press Preview, and start the operation again.
- `DiskProbeUnavailable` means a Render variable is missing or wrong, the Render API failed, or Render's newest disk sample is older than 180 seconds. Check both variables and the graph, then start again after correcting the problem. The probe is retried twice, after 5 and 15 seconds, before the run stops; a retry never lets a batch run without a fresh sample.
- On Postgres, a run advances about 10 batches per 30-second Render sample. A production backlog drain therefore takes hours by design.
- A deploy or restart marks a starting or running run Interrupted, including one started just before the deploy. The card names the time and the decks done. Press the same maintenance button again to resume; completed batches are not repeated.
