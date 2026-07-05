# P88 / CS-06 — Category fill uses the EXISTING harvested store, not net-new tagging

*Follow-up to the measured-style prototype. The 18%-coverage gap from Archidekt creator labels is closed by DeckFlow's existing harvested category knowledge — no new tagging pipeline needed. Validated against live data 2026-07-04.*

## Finding: harvested store covers 97% of Snail's cards

DeckFlow already ships `CardCategoryRepository` over `artifacts/category-knowledge.db` (`card_category_observations`), harvested from the crawled deck corpus:
- **322,677 observations · 21,405 distinct cards · crowd-labeled categories** (Removal 3910 cards, Draw 3389, Ramp 2298, Tutor 405, Counters 1420, …).

Coverage against Salubrious Snail's **2,726 unique cards**:
```
cards with >=1 harvested category: 2637 / 2726 = 97%
harvested canonical coverage (distinct Snail cards tagged):
  draw     655
  removal  572
  ramp     487
  counter  204
  tutor    109
  wipe      37
```
vs the creator's own Archidekt per-card labels which covered only **18%** of nonland cards.

## CS-06 restated — three-layer tagging, all already in-repo

CS-06 does NOT need a new Scryfall-Tagger-per-card pipeline. It's a **priority merge of sources that already exist**:

1. **Creator's own Archidekt category labels** (18% coverage) — sparse but *authoritative for the creator's intent*; weight highest where present. This is literally how the creator classifies their own cards.
2. **Harvested crowd categories** (`CardCategoryRepository.GetCategoriesAsync`, 97% coverage) — the bulk fill; offline, instant, no upstream call. Query by card name, canonicalize the messy 3,713-label vocabulary down to the functional buckets (reuse `ContentTagVocabulary`).
3. **`ScryfallTaggerLookupService`** (`Services/Scryfall/`) — fallback for the ~3% (≈89 cards) not yet in the harvested store; scrapes tagger.scryfall.com functional tags. Rare path → cache results back into the store.

So CS-06 = a `CategoryResolver` that layers (1) → (2) → (3) and canonicalizes. All three collaborators exist; the phase wires + canonicalizes them, it doesn't build tagging from scratch.

## Implications for the roadmap
- **CS-06 shrinks from "build tagging" to "merge + canonicalize existing sources."** Lower risk, less new code.
- The 3,713-category harvested vocabulary is noisy (synonyms `Draw`/`Card Draw`/`Card Advantage`, plus `Maybeboard`/`mainboard` noise) → the real work is a **canonical-bucket mapping**, not tag acquisition. `ContentTagVocabulary` is the place for it.
- Measured category ratios become trustworthy at ~97% card coverage (up from 18%), so the say-vs-do fusion (P90) has real signal on both sides.
- **wipe = only 37 cards tagged across Snail's catalog** — corroborates the earlier low `massLandDenial`/`gameChanger` signal: Snail genuinely underweights board wipes vs the canonical "6 mass disruption" template. Strong say-vs-do delta candidate for P90.

## Caveats
- Canonicalization quality gates the ratio accuracy — a bad synonym map silently mis-buckets. Unit-test the mapping (CS-06 test).
- Harvested categories are crowd-sourced (same abuse caveat as Scryfall Tagger); creator's own labels should win on conflict for that creator's decks.
- 3% tail still needs the Tagger fallback online; cache to keep it rare.
