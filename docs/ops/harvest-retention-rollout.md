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

## Step 1 — Deploy gate and filters

Deploy the legality gate and Commander-only discovery filters. Check boot logs for schema errors, then open Admin/Harvest and confirm the Stats card shows skipped decks by reason. The gate keeps only Commander-format decks with a commander and legal Commander deck size; stored decks that become illegal have their category rows removed and remain recorded with a skip reason.

## Step 2 — Deploy compaction and check boot logs

Deploy the per-deck table compaction while both schedules remain paused. Check boot logs for each table: if either reports compaction skipped because a lock was busy, restart once and check again. Complete the rollout's read-only schema and disk-headroom checks before continuing. Stop before Step 3 if compaction, schema verification, or headroom verification fails.

## Step 3 — Preview, Purge, Drain

Deploy the Retention maintenance card, then press Preview. Planning estimates are 341,656 total decks, about 187,000 decks over the cap, about 11,000 purge candidates, and about 16,000 refetch candidates; the live Preview counts are authoritative. Stop and report a difference of an order of magnitude.

Tick the confirmation box and run Purge. Wait for completion, then Preview again until purge candidates are 0. Next, tick the confirmation box and run Drain. The server refuses Drain while purge candidates remain. Repeat Preview and Drain after recoverable stops until the status shows completed and Preview shows 0 decks over the cap. If a Drain stopped with `Completed` and the over-cap count remains above 0, stop the rollout and report that it made no progress. For `DiskFloor`, `DiskProbeUnavailable`, or `MaxDecksReached`, resolve the displayed stop reason, press Preview, and run Drain again before Refetch.

## Step 4 — Refetch

Tick the confirmation box and run Refetch. The server refuses Refetch until Preview shows purge candidates 0 and decks over the cap 0. When Refetch completes, Preview must show 0 refetch candidates. Refetch only queues those decks; update harvests fetch them after Step 5 and apply the legality gate.

A deck removed by retention stays pruned. It returns only when its owner changes its commander or main deck on Archidekt; sideboard or maybeboard edits leave it pruned.

Known limit: changing only the quantity of a commander or main-deck card that has no category tag is not detected, so that deck stays pruned; adding, removing, or swapping a card is detected.

## Step 5 — Resume schedules

Resume both the Bulk Schedule and Update Schedule. Confirm both cards show `Paused: No` and both next-scheduled-run lines show a time rather than Paused or Off. After the first harvest run, confirm logs contain `Harvest.Retention.Completed` and the skipped-decks counts begin updating.

## Watching a run

- Refresh Admin/Harvest and watch the Retention maintenance status line for operation, start time, decks processed, and the final stop reason.
- Press Stop if disk usage approaches full or the operator needs to interrupt the run.
- Watch the Render disk-usage graph as well as the status line. The graph includes WAL generated by removals.
- `DiskFloor` means free space reached the configured floor. Wait for a Postgres checkpoint to recycle WAL, check the graph, press Preview, and start the operation again.
- `DiskProbeUnavailable` means a Render variable is missing or wrong, the Render API failed, or Render's newest disk sample is older than 180 seconds. Check both variables and the graph, then start again after correcting the problem.
- On Postgres, a run advances about 10 batches per 30-second Render sample. A production backlog drain therefore takes hours by design.
- A deploy or restart ends the in-memory run. Press the same maintenance button again to resume; completed batches are not repeated.
