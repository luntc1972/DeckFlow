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

## Acquisition record (2026-07-27)

The recorded sweep is complete. Phase 2 does not require, and does not authorize, re-running it against
`json.edhrec.com`.

| Fact | Value |
|---|---|
| Run date | `2026-07-27T18:38:28Z` to `2026-07-27T19:11:29Z` |
| Wall clock | 33 minutes |
| Commanders selected | 305 |
| Cells planned / attempted / written | 1,525 / 1,525 / 1,525 |
| Cells skipped existing | 0 |
| Failures | 0 failed, 0 404, 0 unresolved slugs |
| Request attempts | 1,525 |
| User-Agent | `DeckFlow-EDHREC-brackets/1.0 (+https://github.com/luntc1972/DeckFlow/issues)` |
| `averages.csv` | `/mnt/c/users/chrislunt/source/personal/deckflow/artifacts/edhrec/averages-jul26-m5o50xfj/averages.csv` |
| `averages.csv` byte size | 791,987 |
| `averages.csv` sha256 | `52ef25bb72aed5c07d3ba09fa7f826cfe92f653d112a2dcbf0b987616f06c1aa` |

Every number above is transcribed from `_edhrec-brackets/manifest.json`.

## The `--averages` path gotcha

`artifacts/edhrec/` exists only in the MAIN worktree at
`/mnt/c/users/chrislunt/source/personal/deckflow/`, not in `deckflow-role-floors`. The script default
is still the relative `artifacts/edhrec/averages-jul26-m5o50xfj/averages.csv`, but in this worktree that
path resolves to nothing, so the recorded run had to pass the absolute MAIN-worktree path explicitly:

```bash
python3 scripts/edhrec-brackets/fetch.py \
  --averages /mnt/c/users/chrislunt/source/personal/deckflow/artifacts/edhrec/averages-jul26-m5o50xfj/averages.csv \
  --outdir _edhrec-brackets
```

## `--min-decks` is the SELECTION floor, not the cell floor

`--min-decks 8000` selects which solo commanders are swept from `averages.csv` by `number_decks`, and the
same meaning is repeated in `manifest.json` as `min_decks: 8000`. It is not the downstream qualifying
floor for individual bracket cells. The separate prior-study rule is `n_decks >= 400` per cell. Conflate
those two floors and you silently change which cells count.

## Measured yield at the >=400 cell floor

Recomputed from the 1,525 cached cells on disk using each cell's own `n_decks`.

| Bracket | cells | >=400 | >=100 | >=40 | median N | mean lands (qualifying cells) |
|---|---:|---:|---:|---:|---:|---:|
| exhibition (B1) | 305 | 1 | 31 | 131 | 36 | n/a (n=1) |
| core (B2) | 305 | 284 | 302 | 303 | 1,138 | 36.0 |
| upgraded (B3) | 305 | 305 | 305 | 305 | 1,048 | 35.4 |
| optimized (B4) | 305 | 175 | 294 | 305 | 458 | 34.3 |
| cedh (B5) | 305 | 40 | 105 | 169 | 51 | 28.7 |

Totals: 805 qualifying cells. All 305 commanders have at least one qualifying cell; 168 have three or
more; exactly one has all five.

Usability:

- B1/exhibition is NOT usable. One qualifying cell out of 305 is not support for a bracket-wide figure.
- B5/cedh is thin. It has 40 qualifying cells and a median cell N of 51.

Independent corroboration reached the same conclusion elsewhere in the product:

- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:603-605` already restricts EDHREC-derived
  land use to brackets 2-3 because the optimized/cEDH signal would be drowned by casual-dominated means.
- `DeckFlow.Web/Data/manabase-baseline/latest.json` carries bracket rows for 2, 3, 4, and 5 only. There
  is no B1 row.

## On-disk contract — plan 02-06 reads this

Changing any field name below is a breaking change for the C# ingestion.

Cell keys, as written to `_edhrec-brackets/cells/<slug>__<bracket>.json`:

```text
artifact
basic
battle
bracket
bracket_index
budget_counts
commander
commander_card
creature
deck
enchantment
fetched_utc
instant
land
mana_curve
n_decks
nonbasic
piechart
planeswalker
savedate_summary
similar
slug
sorcery
tag_counts
```

Manifest keys, as written to `_edhrec-brackets/manifest.json`:

```text
averages_csv
brackets
cells_404
cells_attempted
cells_failed
cells_planned
cells_skipped_existing
cells_written
commanders_selected
failed_cells
fetch_ended_utc
fetch_started_utc
min_decks
missing_cells
request_attempts_total
selected_commanders
unresolved_slug_count
user_agent
```

Absences to state explicitly because the original plan specified them differently:

- There is no `source`.
- There is no `estimateKind`.
- There is no `qualifies`.
- There is no pre-parsed `cards` array; the payload keeps EDHREC's raw `deck` strings.
- The manifest has no per-cell `file` entries.

## The point-estimate warning

One EDHREC cell is one synthesized average deck, so there is no within-cell variance to rank. No EDHREC
figure may ever be reported as a percentile. This is ROADMAP success criterion 7, and plan `02-05`
enforces it in the C# type system rather than by convention.

## Flag asymmetry

The Python fetcher takes `--outdir`. The C# ingestion command introduced by plan `02-06` takes
`--edhrec-data`. Do not transpose them; this runbook is for cache acquisition, not ingestion.

## Why this is a separate script

`edhrec-download` fetches and untars EDHREC bulk archives. This fetcher pages per-cell JSON from
`json.edhrec.com/pages/average-decks/<slug>/<bracket>.json`, writes one cache file per cell, and resumes
cell-by-cell with throttling. The reuse is at the input layer: `averages.csv` and
`EdhrecAveragesConverter` are reused as the commander-selection source, but the crawler itself is a
different job.

## Cache and gitignore

`_edhrec-brackets/` is a local cache like `_calib/`. Whether it should be gitignored is a follow-up
recommendation for the developer, deliberately outside every plan in this phase, because `.gitignore` is
on the do-not-modify list and no Phase 2 permission was granted to change it. No plan in this phase edits
`.gitignore`.

Leave `_edhrec-brackets/` untracked. Do not delete it, do not "clean" it, and do not stage its 1,525
cached cell payloads.
