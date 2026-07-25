---
quick_id: 260718-nip
type: investigation-report
date: 2026-07-18
---

# EDHREC dump follow-on features — usefulness verdicts

Three candidates from the unused `averages.tgz` fields, each investigated against the live codebase by an independent read-only agent.

## 1. Basic/nonbasic split comparison — **DROP** (value LOW, effort M)

- Deck-side basic/nonbasic counts are NOT currently computed on `ManabaseReport` (only total `ActualLands`, ManabaseModels.cs:917); the one basic-count helper is a private classifier gate (ManabaseClassifier.cs:1673), so new deck-side computation is required.
- Descriptive, not prescriptive: no verdict attaches. "More basics than average" is not wrong when Karsten color-source math already proves the colors adequate — and when they aren't, the color-source shortage already flags it with an actionable fix. Duplicates existing, stronger signal.
- Already ruled YAGNI in the Inc2 design spec (`2026-07-17-manabase-commander-baseline-inc2-design.md:52` — "Basic/nonbasic split not bundled").
- Cost: ~8–10 files + 4 test suites + snapshot regeneration for two extra columns.

## 2. Type-mix comparison — **DROP** (value LOW, effort S–M)

- Mechanically cheap: type lines are available post-Scryfall-resolution everywhere (`CardFact.TypeLine`, DeckStatAggregator already counts creatures; six more `Contains` checks). Lookup reuse free via `ManabaseBaselineProvider`.
- Decisive against: the codebase already litigated "EDHREC averages are casual-dominated" for the land cell (ManabaseAnalysisService.cs:603-605 restricts to brackets 2–3; cEDH got its own tournament corpus instead). Type-mix inherits the same bias but with LESS floor-signal than lands — a stax deck and a swarm deck under the same commander are both correct with wildly different mixes, so "distance from average" is noise for the serious-builder audience.
- Injecting community type averages into the ChatGPT packets would ground the AI toward the casual mean — fights the product thesis. Do not.

## 3. oracle_id keying — **DEFER** (value LOW→MED hardening, effort M)

- No observed failure today: by lookup time BOTH sides are Scryfall-canonical names (deck side = resolved `ScryfallCardData.Name`, ManabaseAnalysisService.cs:815; EDHREC dump publishes Scryfall spellings). The "482 duplicate names" are partner-pair display recurrences the `first||second` key already disambiguates.
- All name-join failure modes (UB/Godzilla alt names, Secret Lair names, Scryfall errata renames, homonym collapse) fail OPEN to the bracket-global baseline — silent quality degradation, never an error.
- Real cost is not the key swap: `oracle_id` exists NOWHERE in the deck-side pipeline (`ScryfallCardData`/`CardFact` lack the field; zero grep hits) — threading a new Scryfall field through resolution + snapshot regen + ~5 test files.
- Trigger to revisit: a reproduced commander-lookup miss in the wild, or the next scheduled snapshot regeneration (piggyback the field then at marginal cost).

## Bottom line

Ship nothing from this list now. The dump's remaining fields serve the casual population; DeckFlow's differentiators (Karsten math, cEDH tournament corpus, castability sim) already outperform them where it counts. Revisit oracle_id opportunistically at next snapshot regen.
