# EDHREC Bracket Fetch Runbook

This script fetches the EDHREC per-(commander, bracket) average-deck JSON cells for solo commanders,
trims each payload to the fields needed for downstream research, and writes one resumable cache file per
cell.

Run it from the repo root:

```bash
python3 scripts/edhrec-brackets/fetch.py
```

Useful variants:

```bash
python3 scripts/edhrec-brackets/fetch.py --dry-run
python3 scripts/edhrec-brackets/fetch.py --limit 5 --dry-run
python3 scripts/edhrec-brackets/fetch.py --min-decks 12000 --brackets core,optimized,cedh
```

Notes:

- Default input: `artifacts/edhrec/averages-jul26-m5o50xfj/averages.csv`
- Default threshold: `--min-decks 8000`
- Default brackets: `exhibition,core,upgraded,optimized,cedh`
- Default throttle: `--throttle 1.3`
- At the default threshold this plans 305 commanders x 5 brackets = 1,525 requests, so the throttle floor
  alone is about 33 minutes before any retries or backoff.

## Output Layout

The script writes a local cache under `_edhrec-brackets/` by default:

```text
_edhrec-brackets/
  manifest.json
  unresolved-slugs.txt
  cells/
    atraxa-praetors-voice__core.json
    atraxa-praetors-voice__optimized.json
    ...
```

- `cells/<slug>__<bracket>.json` is the trimmed record for one bracket cell.
- `manifest.json` records fetch start/end UTC, the user-agent used, input CSV provenance, commander
  selection, requested brackets, and complete coverage counts for written / skipped / 404 / failed cells.
- `unresolved-slugs.txt` lists commanders whose requested cells all returned `404`, one per line as
  `<commander>\t<slug>`.

## Resuming

The fetcher is resumable by design:

- If a cell file already exists and parses as valid JSON, the script skips it without re-requesting.
- If a cell file exists but is truncated or corrupt, the script re-fetches that cell.
- Re-running after a full successful fetch should make zero network requests.

## Coverage And Failure Rules

- `404` cells are expected sparse-data misses, are recorded in the manifest, and do not fail the run.
- `429` honors `Retry-After` when present and otherwise backs off before retrying.
- `5xx` retries up to three attempts per cell with linear backoff (`+60s` per successive failure).
- The process exits non-zero only if the input CSV is missing/unreadable, if zero commanders are selected,
  or if more than 25% of attempted cells fail for reasons other than `404`.

## Attribution

This fetcher identifies itself with a DeckFlow user-agent and is intended for noncommercial community use
against EDHREC's published JSON endpoint. Keep the client identification honest if you override
`--user-agent`.

## Cache Warning

`_edhrec-brackets/` is a large local cache. Do not commit it, and do not add it to `.gitignore` without
explicit developer approval.
