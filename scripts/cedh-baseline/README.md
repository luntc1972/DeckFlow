# cEDH Baseline Runbook

Monthly pipeline:

1. Refresh the calibration cache:

   ```bash
   python3 ~/.claude/skills/exe-secrets/scripts/secret.py exec TOPDECK_API_KEY --store deckflow_openai --as TOPDECK_API_KEY -- python3 scripts/cedh-baseline/fetch.py --outdir _calib --since YYYY-MM-DD
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

- Tournament data comes from [TopDeck.gg](https://topdeck.gg/) v2. `TOPDECK_API_KEY` must be set; the broker command above supplies it without exposing it. Authorization uses the raw key, without a Bearer prefix.
- Enumeration uses weekly windows and minimal columns. Final placements come from per-tournament standings, cached under `<outdir>/topdeck_cache/` for resumable reruns. Requests are paced below 60 per minute, with retries and `Retry-After` handling. Cached standings are reused; remove a tournament cache file to refresh corrected results.
- **Attribution follow-up:** TopDeck credit and a link to https://topdeck.gg/ must be visible wherever these data are shown. Update consuming views before displaying this data; this script change does not edit views.

- `fetch.py` defaults to a **6-month** window (`--since` today−6mo). The wider window gives lower-play
  cEDH commanders a usable N≥10 sample without much recency loss (land counts are fairly stable);
  the size-tiered fetch (winner / top4 / top16) is what feeds `_calib`.
- `fetch.py` is stdlib-only and resumes off `_calib/cards_full.json`, resolving only missing card names.
- `cedh-land-baseline` writes the monthly markdown report, the matching monthly JSON snapshot, and copies the same JSON content to `latest.json` for the app contract.
- Re-run the committed calibration command after each refresh to confirm the hybrid target still cuts
  the under-flag rate without over-correcting grindy commanders, before flipping
  `analysis.manabase.cedh-land-target` ON:

  ```bash
  dotnet run --project DeckFlow.CLI -- cedh-land-calibrate --data _calib
  ```

### Exception: under-covered commanders (supplement scan)

Some commanders (e.g. **Plagon, Lord of the Beach**) barely appear in the size-tiered top-cut results, so
they never reach N≥10 through the normal fetch. `fetch.py` now handles these automatically through the
module-level `SUPPLEMENT_COMMANDERS` list:

1. The monthly refresh still uses the normal size-tiered fetch for `_calib`, driven by `--since` (default:
   today−6mo).
2. `fetch.py` scans the same TopDeck.gg tournaments back to `--supplement-since` (default: today−12mo),
   keeping decks whose commander matches `SUPPLEMENT_COMMANDERS`, regardless of placement. Overlapping
   windows reuse each tournament's standings without fetching the same TID twice. Supplements are deduped
   against tier decks and each other using `deck_identity`, labeled `commander-supplement`, and appended
   to `decks_all.json`; new card names are resolved into `cards_full.json`.
3. Because the supplement decks are part of `_calib/decks_all.json`, the `cedh-land-baseline` CLI naturally
   reproduces those commanders in `YYYY-MM.json` and `latest.json` on each monthly refresh.

To add another under-covered commander, append the exact commander name to `SUPPLEMENT_COMMANDERS` in
`scripts/cedh-baseline/fetch.py` and re-run the monthly pipeline.

### Guards

The pipeline guards against unresolved cards and baseline drift.

1. **Unresolved cards (`fetch.py`).** After Scryfall resolution, decks containing an unresolved
   maindeck or commander name are dropped entirely, so land counts are never under-counted.
   The final `decks_all.json` contains only resolved decks, including when the gate fails.
   Stderr reports each dropped deck's commander, tier, and unresolved names, followed by the
   dropped-deck count and distinct unresolved names. Dropping at most 2% of fetched decks succeeds;
   dropping more than 2% returns a non-zero exit with an ERROR. The card cache remains resumable.

2. **Drift check (`cedh-land-baseline`).** Before writing any artifact, the new snapshot is
   compared against the committed `latest.json` using the limits in
   `scripts/cedh-baseline/drift-thresholds.json`. Three rules fire on shapes that indicate corrupt
   input rather than metagame movement: an established commander disappearing, a populous
   commander's sample collapsing, and many commanders drifting the same direction at once. On
   failure nothing is written, so the last-known-good snapshot survives. If no committed
   `latest.json` exists the run is treated as a bootstrap and the comparison is skipped, but the
   thresholds file must still be present and valid.

If a refresh trips the drift check because the metagame genuinely moved, retune and commit
`drift-thresholds.json`, then re-run. Committing the change means the new normal is reviewed in a
diff. `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftRegressionTests.cs` will reject any retune
that would let the July 2026 corruption through.
