# Harvest retention rollout

Scope: Web host and CLI retention behavior. Use this runbook during the Legal-Commander harvest rollout.

Known limit: quantity changes alone are not observed; a pruned deck re-enters only after its commander or main-deck changes.

Before any Postgres maintenance run, set `RENDER_API_KEY` and `RENDER_POSTGRES_ID` in the Render dashboard. Do not place their values in this document or a shell command. Render drains at its paced maintenance rate; refresh Admin/Harvest to observe progress.

## Settings

| Setting | Default | Purpose |
| --- | --- | --- |
| `PerCommanderCap` | 100 | Retained decks per commander. |
| `BatchSize` | 25 | Pruning batch size. |
| `PauseMilliseconds` | 500 | Pause between pruning batches. |
| `MinFreeDiskBytes` | 1073741824 | Free-disk floor. |
| `MaxAgeDays` | null | Optional age limit. |
| `PostHarvestMaxDecks` | 500 | Automatic post-harvest removal limit. |
| `MaintenanceChunkDecks` | 1000 | Decks per operator maintenance chunk. |

## Step 1

Deploy the Web host and let schema startup complete. Open Admin/Harvest and use Preview; the rollout estimates are 341,656 decks, about 11,000 purge candidates, and about 16,000 refetch candidates, but Preview counts are authoritative.

## Step 2

Confirm normal harvest health, stop any active maintenance run, and record the three Preview counts for the change window.

## Step 3

Start Purge from the Retention maintenance card after ticking its confirmation. Wait for completion, then Preview again until purge candidates are 0. Purge removes decks outside the legal Commander retention scope.

## Step 4

Start Retention drain after Purge. The server refuses Drain while purge candidates remain. After it completes, Preview again; Refetch is refused until Preview shows purge candidates 0 and decks over the cap 0. Start Refetch only then, with its confirmation ticked.

## Step 5

Review the run log and health strip. A stopped or failed operation says to see the logs; resolve it, Preview again, and repeat only the blocked operation. The CLI uses defaults while Web-host retention overrides apply only to the Web host.
