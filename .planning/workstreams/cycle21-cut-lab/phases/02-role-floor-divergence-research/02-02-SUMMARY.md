# 02-02 Summary

## Acquisition record

Transcribed from `_edhrec-brackets/manifest.json`:

- `fetch_started_utc`: `2026-07-27T18:38:28Z`
- `fetch_ended_utc`: `2026-07-27T19:11:29Z`
- `user_agent`: `DeckFlow-EDHREC-brackets/1.0 (+https://github.com/luntc1972/DeckFlow/issues)`
- `averages_csv.path`: `/mnt/c/users/chrislunt/source/personal/deckflow/artifacts/edhrec/averages-jul26-m5o50xfj/averages.csv`
- `averages_csv.byte_size`: `791987`
- `averages_csv.sha256`: `52ef25bb72aed5c07d3ba09fa7f826cfe92f653d112a2dcbf0b987616f06c1aa`
- `min_decks`: `8000` for commander selection, not cell qualification
- `brackets`: `exhibition`, `core`, `upgraded`, `optimized`, `cedh`
- `commanders_selected`: `305`
- `cells_planned`: `1525`
- `cells_attempted`: `1525`
- `cells_written`: `1525`
- `cells_skipped_existing`: `0`
- `cells_404`: `0`
- `cells_failed`: `0`
- `request_attempts_total`: `1525`
- `unresolved_slug_count`: `0`

## Measured yield at the >=400 cell floor

Computed from the cached cell files using each cell's `n_decks`:

| Bracket | cells | >=400 | >=100 | >=40 | median N | mean lands (qualifying) |
|---|---:|---:|---:|---:|---:|---:|
| exhibition (B1) | 305 | 1 | 31 | 131 | 36 | n/a (n=1) |
| core (B2) | 305 | 284 | 302 | 303 | 1,138 | 36.0 |
| upgraded (B3) | 305 | 305 | 305 | 305 | 1,048 | 35.4 |
| optimized (B4) | 305 | 175 | 294 | 305 | 458 | 34.3 |
| cedh (B5) | 305 | 40 | 105 | 169 | 51 | 28.7 |

Totals:

- `805` qualifying cells
- `305` commanders with at least one qualifying cell
- `168` commanders with at least three qualifying cells
- `1` commander with all five qualifying cells

Support statements:

- B1/exhibition is not usable at the >=400 floor. It has one qualifying cell out of 305.
- B5/cedh is thin. It has 40 qualifying cells and median cell N 51.
- Independent corroboration already exists in shipped code and data: `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:603-605` restricts EDHREC commander-cell participation to brackets 2-3, and `DeckFlow.Web/Data/manabase-baseline/latest.json` has rows for 2, 3, 4, and 5 only.

## Verified on-disk contract

Cell schema as written to `_edhrec-brackets/cells/<slug>__<bracket>.json`:

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

Manifest schema as written to `_edhrec-brackets/manifest.json`:

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

Absences relative to the originally-specified schema:

- no `source`
- no `estimateKind`
- no `qualifies`
- no pre-parsed `cards` array
- no per-cell `file` entries in the manifest

Binding notes for plan `02-06`:

- Cell qualification must be derived from each cell's own `n_decks`.
- The raw `deck` payload remains EDHREC's `"<qty> <Card Name>"` strings, so quantity parsing moves to C#.
- Field-name changes here are breaking changes for the ingestion contract.

## Reuse justification

This remains a separate script from `edhrec-download` for a concrete reason. `edhrec-download` fetches
and untars EDHREC bulk archives. `scripts/edhrec-brackets/fetch.py` pages 1,525 per-cell JSON documents
from `json.edhrec.com/pages/average-decks/<slug>/<bracket>.json`, writes one cache file per cell, resumes
cell-by-cell, and throttles requests. The reused parts are the `averages.csv` input and the broader
EDHREC baseline pipeline, not the fetch mechanism.

## Requirement traceability gap

This plan still has no requirement ID. `REQUIREMENTS.md` predates the hybrid-corpus decision and names
only the Postgres corpus. Proposed follow-up requirement, not implemented here:

- `RFLR-11`: The commander x bracket half of the hybrid corpus is sourced from EDHREC average-decks at a
  stated per-cell deck-count floor, and every EDHREC-derived figure is labelled as a point estimate that
  cannot be reported as a percentile.

## Execution confirmations

- No request was made to `json.edhrec.com` by this plan.
- `_edhrec-brackets/manifest.json` remained unchanged during this execution.
- `.gitignore` was deliberately not edited.
- `_edhrec-brackets/` remains present and untracked.
- `_role-floor-research/` remains present and untracked.

## Correction — Task 1 stdlib-only gate is a false positive

The plan verifies stdlib-only with `grep -c "requests\|httpx\|aiohttp" scripts/edhrec-brackets/fetch.py`
and expects `0`. It returns `1`, matching the string `total_requests` in a `print` at `fetch.py:293`.

There is no third-party dependency. `fetch.py`'s complete import list is `argparse`, `csv`,
`datetime`, `hashlib`, `json`, `re`, `sys`, `time`, `unicodedata`, `urllib.error`, `urllib.request`,
`email.utils.parsedate_to_datetime`, `pathlib.Path` — all standard library. `fetch.py` was correctly
left unmodified; the gate needs a word-boundary anchor (e.g. `^import requests`), not the script.

This is the same class of defect Phase 01.1 fixed in `IsCounterCategory`: a substring match standing
in for a token match.
