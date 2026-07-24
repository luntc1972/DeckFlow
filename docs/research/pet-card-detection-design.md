# Pet-Card Detection — Design

- **Date:** 2026-07-24
- **Branch:** `feature/deck-tendencies` (extends the Deck Tendencies personal tool)
- **Status:** Approved design, pending implementation plan
- **Scope:** local-only admin feature; no public surface, no feature flag

## Problem

Deck Tendencies already reports *what* a player repeats (`RepeatCards`, `RepeatCommanders`,
`CategoryTendencies`). It cannot distinguish a **pet card** — a card this player reaches for
unusually often — from a **format staple** that everyone plays. Its only staple filter is
`ContentTagVocabulary.Staples`, a hand-typed list of 11 card names (5 basics plus Command
Tower, Sol Ring, Arcane Signet, Exotic Orchard, Rogue's Passage, Negate). At that size, a
"repeat cards" list is dominated by cards the format plays, not cards the player chose.

**Goal:** surface the cards that are genuinely this player's signature, ranked, with enough
context that a reader can tell a real preference from a small-sample artifact.

## Non-goals

- No public page, no feature flag, no prod path. `category-knowledge.db` is 133 MB and
  gitignored; this feature is inherently local, like the rest of `/Admin/CreatorProfile`.
- No new packages.
- No card recommendations or cut suggestions. This describes, it does not advise.
- Not a replacement for `RepeatCards`. Pet cards are a ranked, baseline-adjusted view
  alongside it.

## Locked decisions

| ID | Decision | Rationale |
|----|----------|-----------|
| D-01 | Baseline is **crowd play-rate, colour-adjusted** | A card is a pet only if the player runs it more than others who *could* run it. Flat rates penalise narrow colour identities. |
| D-02 | Candidate gate is a **flat 3+ of the player's decks** | Keeps the row explainable ("in 3 of your 28"). 2+ admits coincidence; colour-scaled gates are hard to state in a table cell. |
| D-03 | Colour identity resolved via **Scryfall for both sides** | One platform-agnostic path. Using Archidekt's payload for one side and Scryfall for the other is precisely how the colour-mapping bug below arose. |
| D-04 | `edhrecRank` and `salt` are captured and **displayed, never scored** | They are useful context next to a lift figure. They must not enter the ratio — see D-06. |
| D-05 | No `edhrecRank` fallback tier for corpus gaps | YAGNI until UAT shows real gaps. The corpus covers 22,858 cards. |
| D-06 | EDHREC population data must not drive any verdict | `.planning/quick/260718-nip/REPORT.md` established that EDHREC averages are casual-dominated; the manabase feature restricts them to brackets 2–3 and built a separate cEDH tournament corpus rather than trusting them. That report's conclusion — *"grounding toward the casual mean fights the product thesis. Do not."* — applies with equal force here. |

### On D-04's reversal

An earlier draft deferred both fields for baseline consistency. The concern was real but
narrower than stated: the risk is EDHREC data entering the **denominator**, not appearing in
the **table**. As descriptive columns they add signal at near-zero cost and are subject to
D-06, which forbids the thing that was actually dangerous.

The two fields have very different acquisition costs:

- **`edhrecRank` is free.** Scryfall exposes `edhrec_rank` on the card object, so it arrives in
  the same batched collection call already required for colour identity. No extra HTTP, no
  schema change. (Verified: Moggcatcher → `edhrec_rank` 8816, which independently corroborates
  its lift-83 pet ranking — the two signals agree.)
- **`salt` is not free.** Scryfall does not carry it; it is EDHREC data that Archidekt relays
  in `oracleCard`. Capturing it requires persisting per-card oracle data from the crawl, which
  `CreatorDeckSample.Entries` (a plain `IReadOnlyList<DeckEntry>`) does not currently hold.
  It is also Archidekt-only, so it is null for any future non-Archidekt source.

## Data sources (verified 2026-07-24)

**Local corpus — `artifacts/category-knowledge.db`:**

- `deck_queue`: 3,964 decks with `processed = 1`; 3,648 (92%) carry a `commander_name`;
  1,258 distinct commander names.
- `card_deck_totals`: 349,596 rows over 22,858 distinct cards. Board split is
  `mainboard` 298,129 / `maybeboard` 47,569 / `commander` 3,898.
- Play-rate curve is real and steep: Sol Ring 2,691 decks (68%), Command Tower 2,450 (62%),
  Arcane Signet 2,285 (58%), then Swords to Plowshares 895 (23%), Counterspell 644 (16%).
  The existing 11-name `Staples` list is effectively the head of this same query, hand-copied.

**Archidekt** publishes no bulk download (`/downloads`, `/data`, `/api/downloads` all 404),
but every deck response embeds an `oracleCard` with `colorIdentity`, `edhrecRank`, `salt`,
`gameChanger`, `tutor`, `massLandDenial`, `legalities`, `oTags`, `cmc`, `types`. Nothing in
the repo reads any of it today.

**Scryfall** supplies commander colour identity *and* `edhrec_rank`. The existing
`ScryfallCollectionResolver` and `ScryfallBatching` already do 75-identifier batched collection
lookups; 1,258 commanders is ~17 calls, cached. `salt` is absent from Scryfall.

**Existing EDHREC assets on `main` were reviewed and are not usable here** — the granularity is
wrong. `DeckFlow.Web/Data/manabase-baseline/latest.json` and `EdhrecAveragesConverter` are
commander-keyed land averages (`commander, commander2, avg_land, number_decks`), and
`EdhrecCardLookup` fetches per-card *category tags* from `json.edhrec.com`, not play rates.
None is a card-level inclusion-rate source, so none can supplement or replace
`card_deck_totals` for this feature.

Note also that these assets live on `main`, which `feature/deck-tendencies` is ~700 commits
behind. Nothing in this design depends on them, so no rebase is required for it.

## Algorithm

```
candidates  = player cards in >= 3 decks, excluding basics and the commander slot

for each candidate card C with colour identity CI(C):
    myEligible    = player decks whose commander colour identity is a superset of CI(C)
    crowdEligible = corpus decks whose commander colour identity is a superset of CI(C)

    myRate    = playerDecksContaining(C) / myEligible
    crowdRate = corpusDecksContaining(C) / crowdEligible
    lift      = myRate / crowdRate
```

Mainboard only, on both sides. `commander` and `maybeboard` boards are excluded — maybeboard
alone is 47,569 of 349,596 corpus rows, and a repeated commander is already reported by
`RepeatCommanders`.

The **gate and the rate are deliberately different**: the gate is a flat deck count for
readability, the rate is colour-adjusted on both numerator and denominator. Adjusting one side
only would be worse than not adjusting at all.

Colourless cards need no special case — their colour identity is the empty set, which is a
subset of every deck's, so their cohort is the full corpus.

## Components

Four new units, three modified. The crawler is only touched to capture `salt`; dropping that
one field removes it from the change set entirely, which is the cheap way to descope if the
persistence cost turns out higher than expected.

| Unit | Layer | Purpose | Depends on |
|------|-------|---------|------------|
| `PetCardCandidateSelector` | Core, pure | Player decks → candidates in 3+ decks, basics and commander slot excluded | `CreatorDeckSample[]` |
| `ColorCohortResolver` | Core interface, Web impl | Card or commander name → colour identity **and `edhrecRank`**, batched and cached | `ScryfallCollectionResolver` |
| `CreatorProfileDeckCrawler` | Web, **modified** | Captures `salt` from the Archidekt `oracleCard` payload; null for other sources | Archidekt payload |
| `CardPlayRateReader` | Core | Crowd numerators and colour-cohort denominators | `CardCategoryRepository` |
| `PetCardCalculator` | Core, pure | Candidates + rates → ranked `PetCardRow[]` | none (pure math) |
| `DeckTendenciesReportBuilder` | Core, **modified** | Emits `PetCards` alongside existing lists | the above |
| `Views/AdminCreatorProfile/Index.cshtml` | Web, **modified** | Renders the pet table | report |

The Core/Web split follows the cycle-17 D-11 precedent: Scryfall access is a Web concern
behind a narrow interface, so the scoring maths stays pure and unit-testable.

## Data flow

```
player decks ──> candidates (3+ decks, no basics, no commander slot)
                     │
                     ├──> ColorCohortResolver ──> CI(card)
                     │
local corpus ────────┴──> per candidate:
  3,964 decks               myRate    = yours    ÷ your colour-legal decks
  1,258 commanders          crowdRate = corpus   ÷ corpus colour-legal decks
  22,858 cards              lift      = myRate ÷ crowdRate
                                     └──> rank desc, tier, cap at 50
```

## Presentation

Tiers, not a hard cutoff. Measured distribution over the reference player's 28 decks:
257 candidates, of which lift ≥ 2 → 224, ≥ 3 → 180, ≥ 5 → 123, ≥ 10 → 42, ≥ 25 → 11.
A single threshold either floods the page or truncates the interesting tail.

- **Signature** — lift ≥ 25
- **Strong** — lift 10 to 25
- **Notable** — lift 5 to 10, collapsed by default
- Below 5 is not shown.

The rendered table caps at 50 rows. The cap is applied **after** tiering, and Signature and
Strong are never truncated: those tiers render in full (42 rows in the reference data), and
Notable fills the remainder up to 50. If Signature plus Strong ever exceeds 50 on some other
profile, the cap yields to them and Notable is omitted entirely — a truncated Signature tier
would be the one genuinely misleading outcome.

Each row shows card name, tier, lift, the raw fraction on both sides (`4/17` vs `7/1607`),
colour identity, `edhrecRank`, `salt`, and any guard flag.

`edhrecRank` and `salt` are **presentational only** (D-04, D-06). They are not inputs to lift,
not tie-breakers, and not filters. Their value is as a cross-check a reader performs: a high
lift beside a deep `edhrecRank` is a corroborated pet, whereas a high lift beside a strong rank
suggests the local corpus under-represents that card rather than that the player is unusual.
Both fields render as "—" when unavailable, and an absent value never suppresses a row.

## Guards

**Low sample.** Rows whose `myEligible < 5` are flagged. With a 3-deck cohort a single deck
swings the rate by 33 points, so `Orcish Lumberjack 3/3` is real but fragile. The threshold
reuses the existing `CreatorStyleProfile.MinDeckFloor = 5`
(`DeckFlow.Core/Knowledge/CreatorStyleProfile.cs:9`) rather than introducing a second number.
`ConflictCalculator.cs:31` already uses that constant as a sample gate, so this follows an
established precedent rather than inventing a convention.

**Zero-crowd cards** go to an `unrated` bucket, never `inf`. A card absent from the corpus is
unknown, not infinitely rare. Unrated cards are excluded from ranking and listed separately.

**Name normalisation** must use the repo's own normaliser. The corpus maps every
non-alphanumeric character to a **space**, not to nothing: `Sensei's Divining Top` is stored as
`sensei s divining top`, `Storm-Kiln Artist` as `storm kiln artist`. A stripping normaliser
misses every card containing an apostrophe or hyphen.

**Colour identity representation.** Archidekt emits full colour names (`"Blue"`, `"Black"`);
Scryfall emits WUBRG letters. Naive first-letter mapping collapses Blue and Black into `B` and
silently merges the two cohorts.

**Double-faced cards.** Commander names are stored in `A // B` form. Scryfall collection
lookups must be sent the **front face only** — the full `//` string returns not-found. The
returned `color_identity` already covers both faces, so front-face lookup loses nothing.

## Error handling

- Commanders that still fail to resolve are **excluded from denominators**, never counted as
  zero. Counting them as zero would inflate every lift. The count is surfaced as a coverage
  line so a silent shortfall is visible.
- The 8% of corpus decks with no recorded `commander_name` are likewise excluded, not zeroed.
- `category-knowledge.db` absent → the pet section reports unavailable and the rest of the
  tendencies report still builds, matching the existing null-degrading behaviour in
  `DeckTendenciesReportBuilder`. A fresh clone hits this by default since the DB is gitignored.
- Scryfall failure → same degrade path. A cheap guard, not an expected condition; Scryfall has
  been reliable throughout development.

## Testing

Pure-Core unit tests on `PetCardCalculator` and `PetCardCandidateSelector`, plus explicit
regression tests for the three boundary bugs found while prototyping. All three produced a
fully populated, correctly formatted, confidently ranked table that was simply wrong, with no
exception thrown — so these are required, not optional.

1. **Normalisation:** `Sensei's Divining Top` → `sensei s divining top`;
   `Storm-Kiln Artist` → `storm kiln artist`. Punctuation becomes a space.
2. **Colour mapping:** `"Blue"` → `U` and `"Black"` → `B` never collide.
3. **DFC lookup:** `Esika, God of the Tree // The Prismatic Bridge` resolves via its front
   face and yields the full-card colour identity.
4. **Subset logic:** a colourless card is eligible in every deck; a `{G}` card is not eligible
   in a mono-blue deck.
5. **Zero crowd count** → `unrated`, no divide-by-zero.
6. **Low-sample flag** fires at `myEligible < 5` and not at 5.
7. **Board exclusion:** maybeboard and commander rows do not reach either side of the ratio.
8. **`edhrecRank` and `salt` are display-only:** a test asserts that changing either value
   leaves every lift, tier, and row ordering byte-identical. This is the executable form of
   D-06 — without it, a later change could quietly promote them into the score.
9. **Missing `edhrecRank` or `salt` renders "—" and never drops the row.** `salt` is null for
   any non-Archidekt source, so this is the normal case, not an edge case.
10. **Golden fixture** from the validated prototype run.

## Prototype evidence

The algorithm was run end-to-end against 28 real Archidekt decks and the live corpus before
this design was written. After all three normalisation fixes, the top of the ranking:

| lift | card | player | crowd | CI |
|-----:|------|-------:|------:|----|
| 83.2 | Moggcatcher | 3/20 | 3/1664 | R |
| 63.8 | Terra, Herald of Hope | 3/3 | 5/319 | BRW |
| 54.0 | Banishing Knack | 4/17 | 7/1607 | U |
| 47.3 | Benthic Biomancer | 3/17 | 6/1607 | U |
| 37.0 | Goblin Sharpshooter | 4/20 | 9/1664 | R |
| 33.6 | Nowhere to Run | 5/9 | 29/1753 | B |
| 31.5 | Delay | 8/17 | 24/1607 | U |
| 28.1 | Orcish Lumberjack | 3/3 | 24/674 | GR |

Colour adjustment moved results materially in **both** directions versus a flat baseline,
confirming D-01 was worth the cost: Conduit of Worlds 6.1 → 15.6, Six 4.1 → 10.6,
Terra 74.2 → 63.8, Moggcatcher 123.6 → 83.2.

## Known limitations

The 3,964-deck local corpus is a convenience sample from the category harvest, not a
controlled one, and it plausibly carries the same casual skew D-06 attributes to EDHREC. Lift
therefore means "unusual relative to the decks DeckFlow happened to crawl", not "unusual in
Commander". The report must not imply the stronger claim. This is a limitation of the only
card-level corpus available, not a solvable defect — the manabase feature hit the same wall and
answered it by building a separate cEDH corpus, which is out of scope here.

Consequently a low-lift result is weak evidence of anything, while a high-lift result with a
healthy sample is reasonably trustworthy. The tiering reflects that asymmetry: only lift ≥ 5
is shown at all.

## Deferred

- `edhrecRank` fallback tier for corpus gaps (D-05). Revisit only if UAT shows real gaps.
- Moxfield as a crawl source. Blocked independently: Cloudflare 403s the server-side .NET
  client by TLS fingerprint on both API versions. Archidekt is the working path.
