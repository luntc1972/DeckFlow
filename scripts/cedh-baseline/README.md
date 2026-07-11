# cEDH Baseline Runbook

Monthly pipeline:

1. Refresh the calibration cache:

   ```bash
   python3 scripts/cedh-baseline/fetch.py --since YYYY-MM-DD
   ```

2. Build the monthly baseline artifacts from the cached `_calib` inputs:

   ```bash
   dotnet run --project DeckFlow.CLI -- cedh-land-baseline --data _calib --month YYYY-MM
   ```

3. Commit the new dated artifacts under `DeckFlow.Web/Data/cedh-land-baseline/`:

   - `YYYY-MM.md`
   - `YYYY-MM.json`
   - `latest.json`

Notes:

- `fetch.py` defaults to a **6-month** window (`--since` today−6mo). The wider window gives lower-play
  cEDH commanders a usable N≥10 sample without much recency loss (land counts are fairly stable);
  the size-tiered fetch (winner / top4 / top16) is what feeds `_calib`.
- `fetch.py` is stdlib-only and resumes off `_calib/cards_full.json`, resolving only missing card names.
- `cedh-land-baseline` writes the monthly markdown report, the matching monthly JSON snapshot, and copies the same JSON content to `latest.json` for the app contract.
- Re-run the calibration harness (`DeckFlow.Core.Tests/Manabase/_CalibrationHarness.cs`, untracked) after
  each refresh to confirm the hybrid target still cuts the under-flag rate without over-correcting grindy
  commanders, before flipping `analysis.manabase.cedh-land-target` ON.

### Exception: under-covered commanders (commander search)

Some commanders (e.g. **Plagon, Lord of the Beach**) barely appear in the size-tiered top-cut results, so
they never reach N≥10 through the normal fetch. `fetch.py` now handles these automatically through the
module-level `SUPPLEMENT_COMMANDERS` list:

1. The monthly refresh still uses the normal size-tiered fetch for `_calib`, driven by `--since` (default:
   today−6mo).
2. After that, `fetch.py` runs a commander-specific EDHTop16 search for each name in
   `SUPPLEMENT_COMMANDERS`, paginates to completion, filters to `--supplement-since` (default: today−12mo),
   shapes the results as `_calib` deck records, resolves any new cards into `cards_full.json`, and appends
   the decks to `decks_all.json`.
3. Because the supplement decks are part of `_calib/decks_all.json`, the `cedh-land-baseline` CLI naturally
   reproduces those commanders in `YYYY-MM.json` and `latest.json` on each monthly refresh.

To add another under-covered commander, append the exact commander name to `SUPPLEMENT_COMMANDERS` in
`scripts/cedh-baseline/fetch.py` and re-run the monthly pipeline.
