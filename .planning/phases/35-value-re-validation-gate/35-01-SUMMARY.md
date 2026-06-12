# 35-01 Summary

## Decks Chosen

- `atraxa` — Atraxa, Praetors' Voice — `Upgraded` — archetypes: `ramp`, `control`, `value-engine`, `midrange`
- `light-paws` — Light-Paws, Emperor's Voice — `Optimized` — archetypes: `aggro`, `voltron`
- `kinnan` — Kinnan, Bonder Prodigy — `cEDH` — archetypes: `combo`, `control`
- `talrand` — Talrand, Sky Summoner — `Upgraded` — archetypes: `control`, `stax`
- `aesi` — Aesi, Tyrant of Gyre Strait — `Core` — archetypes: `lands`, `ramp`

Brackets across the five emitted decks span `Core`, `Upgraded`, `Optimized`, and `cEDH`.

## Implementation

- `gen-artifacts.py` now uses two sqlite connections as planned:
  - `artifacts/content-site-index.db` for visible `content_site_index` rows
  - `artifacts/uat-content-kb.db` for `content_videos`, `content_summaries`, and `content_clips`
- `gen-artifacts.py` now writes `artifacts/spike-rows.json` instead of `/tmp/rows.json`.
- `fetch-deck-cards.py` is parameterized as:
  - `<deck.txt> <cards_cs.txt> <decklist.txt>`
- `fetch-deck-cards.py` now resolves DFC / split-card deck names by front face while still emitting canonical full names, and exits non-zero on any `NOT FOUND` or `UNMATCHED` result.
- `Spike001KbValueAbHarness.cs` keeps the original `CreateService(FakeContentKbRelevanceService)` path intact and adds the required overload:
  - `CreateService(IContentKbRelevanceService relevanceService, IReadOnlyList<ScryfallCard> cards)`
- The new overload uses non-static lambdas closing over per-deck `cards`, with matching overloads for:
  - `CreateCollectionResponse(request, cards)`
  - `CreateSearchResponse(request, cards)`
  - `CreateNamedResponse(request, cards)`
- Added `[Fact] EmitRealRetrievalPromptAllDecks`, which:
  - builds the real `ContentKbRelevanceService` once over `artifacts/spike-rows.json`
  - loops the five deck fixtures
  - emits baseline, with-context, and selected-clips traces per deck

## Selected Clip Counts

- `atraxa` — 5 clips
- `light-paws` — 5 clips
- `kinnan` — 5 clips
- `talrand` — 5 clips
- `aesi` — 5 clips

Cold-start decks: none.

## Emitted Files

- `.planning/spikes/001-kb-value-ab/baseline-atraxa.txt`
- `.planning/spikes/001-kb-value-ab/baseline-light-paws.txt`
- `.planning/spikes/001-kb-value-ab/baseline-kinnan.txt`
- `.planning/spikes/001-kb-value-ab/baseline-talrand.txt`
- `.planning/spikes/001-kb-value-ab/baseline-aesi.txt`
- `.planning/spikes/001-kb-value-ab/with-context-atraxa.txt`
- `.planning/spikes/001-kb-value-ab/with-context-light-paws.txt`
- `.planning/spikes/001-kb-value-ab/with-context-kinnan.txt`
- `.planning/spikes/001-kb-value-ab/with-context-talrand.txt`
- `.planning/spikes/001-kb-value-ab/with-context-aesi.txt`
- `.planning/spikes/001-kb-value-ab/selected-clips-atraxa.txt`
- `.planning/spikes/001-kb-value-ab/selected-clips-light-paws.txt`
- `.planning/spikes/001-kb-value-ab/selected-clips-kinnan.txt`
- `.planning/spikes/001-kb-value-ab/selected-clips-talrand.txt`
- `.planning/spikes/001-kb-value-ab/selected-clips-aesi.txt`

## Verify Results

### Task 1

- Deleted one existing Salubrious Snail artifact under `artifacts/content-kb/salubrious-snail/`.
- Ran `python3 .planning/spikes/001-kb-value-ab/gen-artifacts.py`.
- Result:
  - `visible index rows: 82`
  - `artifacts/spike-rows.json` rewritten with `82` rows
  - deleted snail artifact was regenerated
  - regenerated artifact contained non-empty `## Summary` and `## Key Clips`

### Task 2

- Build:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`
  - passed with `0` warnings / `0` errors
- Targeted fact:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --filter "FullyQualifiedName~Spike001KbValueAbHarness.EmitRealRetrievalPromptAllDecks"`
  - passed: `1` test, `0` failed
- File gate:
  - exactly `10` deck files matched the baseline/with-context count check, excluding legacy `with-context-real.txt`
  - all five `with-context-*.txt` files contained `## Expert Context`

## Deviations

- Lands/ramp deck choice used `Aesi, Tyrant of Gyre Strait` instead of `Lord Windgrace`; it still satisfies the locked slot (`Core`, lands/ramp) with a real 100-card list.
- No cold-start deck occurred, so the cold-start branch remains implemented but unexercised by this run.
