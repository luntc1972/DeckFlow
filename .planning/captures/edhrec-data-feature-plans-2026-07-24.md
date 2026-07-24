# EDHREC Data Feature Plans

Captured: 2026-07-24
Status: backlog research / future-cycle planning
Data basis: `artifacts/edhrec/data-jul26-uigloqve/edhrec.csv` + `artifacts/edhrec/averages-jul26-m5o50xfj/averages.csv`

## Context

EDHREC now publishes two sanctioned noncommercial community data archives:

- `averages.tgz` -> `averages.csv`: commander / partner-pair deck counts plus average card-type and land counts.
- `data.tgz` -> `edhrec.csv`: `commander,card,count`, where `count` is the number of decks for that commander that include the card.

Local scan of the July 2026 files:

- `edhrec.csv`: 14,150,219 commander-card rows; 3,378 commanders; 31,788 unique card names; ~618 MB extracted.
- `averages.csv`: 6,585 commander/partner rows; 9,442,363 total deck count across rows.
- Top raw global inclusions: `Sol Ring`, `Command Tower`, `Arcane Signet`, then basics.

License note from the archive: EDHREC / Space Cow Media encourage community developer use, provide no warranty, and do not permit commercial use. This is a better posture than live scraping. Keep any feature tied to the published archives, with data timestamp and attribution.

Relevant external research:

- EDHREC FAQ: EDHREC computes decklist statistics from Archidekt, Moxfield, and Scryfall; synergy is commander/theme inclusion minus same-color-identity inclusion. https://edhrec.com/faq
- EDHREC deckbuilding tools article: players use EDHREC for top cards and on-theme draw/removal, but are cautioned that EDHREC is only one tool and card evaluation still requires judgment. https://edhrec.com/articles/the-most-important-commander-deckbuilding-tools
- Commander Spellbook Find My Combos already covers combo/near-combo discovery from a decklist. https://commanderspellbook.com/find-my-combos/
- EDHPowerLevel and ArcMind validate market demand for paste-a-deck power/bracket/impact analysis, but they do not solve DeckFlow's deck-delta and one-round-trip prompt artifact lane. https://edhpowerlevel.com/ and https://arcmind.cards/
- MTG dashboard research says players prioritize contextual, outcome-driven metrics over peripheral charts, supporting deck-specific deltas over generic population dashboards. https://arxiv.org/abs/2512.09802

Relevant existing DeckFlow research:

- `docs/research/creator-style-llm-system.md`: creator profiles, staple stripping, lift over raw synergy, Scryfall grounding, Commander Spellbook, and prompt-artifact-first architecture.
- `docs/research/creator-style-roadmap.md`: prior creator-style phase arc; do not duplicate this for Cycle17 work.
- `docs/research/plan-presence-research.md` and `docs/research/plan-presence-plan.md`: role coverage / opening-hand plan presence.
- `docs/research/p88-measured-style-prototype-snail.md`: measured style prototype; validates staple stripping and metric extraction from public deck data.
- `docs/research/p88-archidekt-crawl-feasibility.md`: public deck crawling feasibility and privacy boundaries.

## Shared Data Substrate

Build one reusable EDHREC community-stat substrate before feature work:

1. Add an offline import path for both archives, using the existing `edhrec-download` CLI and a new converter/indexer.
2. Normalize card and commander names through the existing Scryfall grounding path; preserve raw names and data version.
3. Store or generate compact indexes:
   - `CommanderCardStat`: commander, card, count, denominator, inclusionRate.
   - `CardGlobalStat`: card, totalCount, commanderCount, globalRate.
   - `CommanderSupportStat`: commander, deckCount, rowCount, confidence band.
4. Treat partner commanders carefully. `edhrec.csv` aggregates by commander name; `averages.csv` includes both singleton and partner-pair rows. For a per-commander denominator, sum `number_decks` for rows where the commander appears in either `commander` or `commander2`; label it as an approximation.
5. Keep full `edhrec.csv` out of source control. Commit only compact derived artifacts needed by runtime, or use an operator-local import.

Core primitive formulas:

- `commanderInclusionRate = count / commanderDeckCount`
- `globalRate = cardTotalCount / totalDeckRows`
- `signatureProxy = commanderInclusionRate - globalRate`
- `ubiquity = number of commanders where card appears`

`signatureProxy` is not EDHREC's exact synergy because exact synergy needs same-color-identity background rates. It is still useful for DeckFlow's deck-delta work when labeled honestly.

## Feature Plan 1: Community Delta Analyzer

Goal: compare an imported deck against the EDHREC population for its commander.

User value:

- "What obvious commander cards am I missing?"
- "Is my list close to the average deck or intentionally different?"
- "Which includes are commander-specific versus generic staples?"

General plan:

1. Resolve commander and normalize the user's deck.
2. Load the top commander-card stats for that commander.
3. Split cards into present, missing, and off-meta.
4. Produce a compact deck-delta panel and prompt-artifact block:
   - top missing high-inclusion cards,
   - included high-signature cards,
   - included low-population cards,
   - staple concentration,
   - confidence from commander deck count.

What EDHREC already provides:

- Commander pages with popular cards and signatures.

What DeckFlow adds:

- Applies those population stats to the user's exact deck, with present/missing/off-meta deltas and paste-ready analysis.

Dependencies:

- Shared EDHREC data substrate.
- Scryfall name grounding.
- Existing deck import pipeline.

Risks:

- Low-support commanders can look more authoritative than the data justifies.
- Partner denominators are approximate.

Recommended phase shape:

- One small phase for substrate read model.
- One feature phase for the deck-delta panel and prompt artifact.

## Feature Plan 2: Missing Role, Not Missing Card

Goal: recommend missing functional roles using EDHREC commander stats plus card-role classification, not just individual cards.

User value:

- "I need on-theme draw/removal/ramp, not a generic staple list."
- "What function is underrepresented compared with similar decks?"

General plan:

1. Classify EDHREC top cards by role using the existing category knowledge store, Scryfall Tagger-derived categories, and oracle-text fallback.
2. For the user deck, compute role counts and role-specific commander inclusion baselines.
3. Surface deficits as role statements:
   - "This commander commonly runs sacrifice outlets; your list has one."
   - "Your removal exists, but little of it is commander-specific."
4. Only after the role statement, list 3-8 statistically fitting candidate cards.

What EDHREC already provides:

- Popular cards and thematic browsing.

What DeckFlow adds:

- Role-level explanation attached to the user's current list and one-round-trip prompt output.

Dependencies:

- Shared EDHREC substrate.
- Existing category/tagging work.
- Optional Scryfall Tagger bulk import if current categories are incomplete.

Risks:

- Role classification quality controls trust.
- Multi-role cards must count in each relevant role, matching existing research.

Recommended phase shape:

- Best paired after Community Delta Analyzer because it reuses present/missing calculations.

## Feature Plan 3: Signature vs Staple Classifier

Goal: label each card in a deck by statistical meaning.

Labels:

- `commander-signature`: high commander rate, low global rate.
- `format-staple`: high global rate or high commander ubiquity.
- `theme-support`: high commander rate, medium global rate.
- `personal/off-meta`: low commander rate and low global rate.
- `low-signal`: insufficient data or ambiguous commander support.

User value:

- Players can tell whether a card says something about the commander or is merely a generic good card.
- Cut decisions become clearer: cutting a staple and cutting a signature card are different acts.

General plan:

1. Create threshold bands for commander inclusion, global rate, and confidence.
2. Add a pure classifier service that returns card labels and rationale strings.
3. Expose labels in Deck Analysis, Cut Lab, and generated prompt artifacts.
4. Keep labels nonjudgmental: "off-meta" is a signal, not an error.

What EDHREC already provides:

- Synergy/signature sections on its own pages.

What DeckFlow adds:

- Reusable card-level labels across DeckFlow workflows, especially where a user is deciding what to cut or justify.

Dependencies:

- Shared EDHREC substrate.

Risks:

- Exact EDHREC synergy is color-identity adjusted; DeckFlow's first version should call the metric `signature proxy`, not `synergy`.

Recommended phase shape:

- Build as a foundation phase before Cut Lab or Deck Analysis surfaces.

## Feature Plan 4: Uniqueness / Homogenization Score

Goal: quantify how close the user's deck is to the commander population.

User value:

- "Am I building the average EDHREC deck?"
- "How many slots are personal choices?"
- "Did this upgrade pass make the deck more generic?"

General plan:

1. Compute overlap with top-N commander cards, excluding basics and known staples.
2. Compute staple concentration using global card rates and ubiquity.
3. Compute signature concentration using `signatureProxy`.
4. Present multiple axes instead of one moralized number:
   - community overlap,
   - staple reliance,
   - signature density,
   - off-meta density.

What EDHREC already provides:

- Top cards, themes, and signatures.

What DeckFlow adds:

- Deck-specific "average vs personal" framing.

Dependencies:

- Signature vs Staple Classifier.
- Staple stripping thresholds from creator-style research.

Risks:

- Players may interpret uniqueness as better. UI copy must avoid rewarding weirdness for its own sake.

Recommended phase shape:

- Follow the classifier foundation; surface first in Deck Analysis.

## Feature Plan 5: Budget Replacement Finder With Statistical Fit

Goal: suggest lower-cost alternatives that fit the commander population and role.

User value:

- "I cannot buy this card; what statistically fits this commander and role?"
- "Find me the budget version that still belongs in the deck."

General plan:

1. Given a target card, determine its role(s), commander inclusion, and signature/staple classification.
2. Find candidate cards in the same role for the same commander.
3. Join price data from existing card data / Scryfall where available.
4. Rank by role match, commander inclusion, signature fit, price, and color legality.
5. Emit a small explanation: "less popular, same role, lower price, still seen in this commander."

What EDHREC already provides:

- Card lists and broad budget browsing through site UX.

What DeckFlow adds:

- Direct replacement reasoning for a card in the user's exact deck.

Dependencies:

- Role classifier.
- Scryfall/current price data if price is available in the current pipeline.

Risks:

- Price freshness.
- Role equivalence can be false for unique effects.

Recommended phase shape:

- Good follow-on after Missing Role and Signature/Staple; do not build first.

## Feature Plan 6: Commander Fit Score For Individual Cards

Goal: answer whether a card belongs statistically in a chosen commander.

User value:

- Useful during brewing, card search, and Cut Lab inspection.
- Lets users ask "is this pet card actually supported by the commander population?"

General plan:

1. For `(commander, card)`, load inclusion rate, global rate, ubiquity, and confidence.
2. Return a fit classification:
   - strong fit,
   - common staple,
   - niche/signature,
   - off-meta,
   - no data.
3. Provide a single-sentence reason with counts.
4. Integrate as a hover/disclosure in card rows before adding any broad UI.

What EDHREC already provides:

- A card may appear or not appear on commander pages.

What DeckFlow adds:

- Fast card-specific explanation inside the workflow currently being used.

Dependencies:

- Shared EDHREC substrate.

Risks:

- A low fit score can discourage innovation; wording must preserve agency.

Recommended phase shape:

- Small reusable phase; can precede larger Deck Analysis surfaces.

## Feature Plan 7: Cut Lab Statistical Evidence

Goal: make EDHREC population evidence available during cut decisions.

User value:

- "Why is this card protected?"
- "Is this candidate cut a common commander card, a signature card, or just a staple?"
- "What does the community usually keep in this slot?"

General plan:

1. Add EDHREC stat badges to Cut Lab card rows and evidence disclosures.
2. For each proposed cut, include:
   - commander inclusion rate,
   - global rate,
   - signature/staple label,
   - role scarcity impact if available.
3. Keep the data advisory. Do not let EDHREC stats override structural/combo/lock rules.
4. Include a prompt-artifact section summarizing high-risk cuts.

What EDHREC already provides:

- Commander card population stats separately from the user's cut workflow.

What DeckFlow adds:

- Decision-time evidence inside Cut Lab.

Dependencies:

- Signature vs Staple Classifier.
- Current Cut Lab view-model and evidence disclosure patterns.

Risks:

- Cut Lab already has dense UI after Cycle 19. Use compact badges and disclosures, not another large panel.

Recommended phase shape:

- Future Cut Lab phase after Cycle 19 regression gate; not part of current Phase 111.

## Feature Plan 8: Deck Confidence / Support Score

Goal: show how much population support exists for every EDHREC-derived claim.

User value:

- "Can I trust this stat?"
- "Is my obscure commander underrepresented?"

General plan:

1. Compute support bands from commander deck counts and row density:
   - high support,
   - medium support,
   - low support,
   - partner approximation.
2. Attach support labels to all EDHREC-derived surfaces.
3. Suppress or soften recommendations below a minimum support threshold.
4. Include data version and deck count in prompt artifacts.

What EDHREC already provides:

- Deck counts on commander pages.

What DeckFlow adds:

- Confidence propagation across every downstream conclusion.

Dependencies:

- Shared EDHREC substrate.

Risks:

- None significant; this should be a required guardrail, not a standalone user-facing feature.

Recommended phase shape:

- Include in the first substrate phase.

## Feature Plan 9: Precon Effect Detector

Goal: warn when population stats may be inflated by preconstructed deck imports.

User value:

- "Is this card actually good here, or just in the precon?"
- "How far from the stock precon is my upgraded list?"

General plan:

1. Ingest precon decklists and release dates.
2. For commanders with known precons, compare EDHREC high-inclusion cards to stock-list membership.
3. Label likely precon-inflated cards in deck-delta and Cut Lab surfaces.
4. Add an "upgrade distance" view:
   - stock cards retained,
   - common upgrade-away cards,
   - common upgrade-in cards.

What EDHREC already provides:

- Precon pages and population stats.

What DeckFlow adds:

- Bias warning and stock-precon distance directly on a user's list.

Dependencies:

- Precon list data source.
- EDHREC substrate.

Risks:

- Needs careful sourcing for precon lists.
- Precon inflation is a hypothesis unless validated by time-series or stock-list overlap.

Recommended phase shape:

- Later-cycle feature. Valuable, but not first because it needs a new precon data source.

## Feature Plan 10: Commander Comparison / Build Direction Tool

Goal: help a user choose or validate a commander from a card package.

User value:

- "Which commander best supports this 20-card package?"
- "Does this pile actually belong under my chosen commander?"
- "Am I mixing two commander plans?"

General plan:

1. Accept a card package or full deck.
2. For each candidate commander, sum normalized commander inclusion over the provided cards.
3. Penalize pure staples so `Sol Ring` and `Command Tower` do not dominate.
4. Return top-fit commanders with explanation:
   - supported cards,
   - unsupported cards,
   - signature overlaps,
   - confidence.
5. Optional later: compare two named commanders directly.

What EDHREC already provides:

- Commander-first browsing.

What DeckFlow adds:

- Deck-first / package-first commander selection.

Dependencies:

- Shared EDHREC substrate.
- Staple stripping.

Risks:

- Five-color commanders can over-score because they can contain more cards; normalize by color identity or card legality once Scryfall color identity is joined.

Recommended phase shape:

- Good standalone future feature after substrate and classifier are stable.

## Recommended Sequencing

## Level Of Effort

Assumption: estimates are relative to DeckFlow's current codebase, after the raw EDHREC archives
are available locally. The shared substrate is counted separately because most features depend on it.

| Work | Effort | Notes |
|---|---:|---|
| EDHREC Community Stat Substrate | Medium-Large | Import/index `edhrec.csv` safely, derive compact stats, handle partner-denominator caveats, avoid committing raw 618 MB CSV, add tests and data versioning. |
| Deck Confidence / Support Score | Small | Mostly deck-count and row-density math from `averages.csv` plus support labels. Should ship inside the substrate, not as a standalone feature. |
| Commander Fit Score For Individual Cards | Small-Medium | Simple `(commander, card)` stat lookup plus fit labels. Can start as compact disclosure text. |
| Signature vs Staple Classifier | Medium | Needs threshold design, staple stripping, tests, and honest naming around `signatureProxy` versus true EDHREC color-identity synergy. |
| Community Delta Analyzer | Medium | Uses classifier output to compare an imported deck to commander population. Needs a panel and prompt-artifact block. |
| Uniqueness / Homogenization Score | Medium | Math is straightforward once classifier exists, but UX wording is delicate because "unique" must not imply "better." Salubrious Snail's EDHRECrec has a commander-level analog; DeckFlow's opportunity is deck-specific. |
| Cut Lab Statistical Evidence | Medium-Large | Data is ready once classifier exists, but Cut Lab is UI-dense and regression-sensitive after Cycle 19. Needs compact badge/disclosure integration. |
| Missing Role, Not Missing Card | Large | Requires reliable role classification, role baselines, multi-role handling, and explanation UX. High value, higher modeling risk. |
| Budget Replacement Finder With Statistical Fit | Large | Needs role equivalence, pricing freshness, legality/color checks, and ranking. Poor role matching would produce bad suggestions. |
| Commander Comparison / Build Direction Tool | Large | Scores a card package across many commanders; needs staple suppression, color-identity normalization, and a new deck/package-first workflow. |
| Precon Effect Detector | Large-XL | Requires a sourced precon decklist corpus, precon matching, bias logic, and careful wording. Valuable, but not an early slice. |

Do not build all ten as one milestone. Build shared primitives first, then user-visible slices:

1. **EDHREC Community Stat Substrate**: import/index `averages.csv` + `edhrec.csv`, confidence/support labels, card/global stats.
2. **Signature vs Staple Classifier**: reusable card labels with honest metric names.
3. **Community Delta Analyzer**: first user-visible deck-specific application.
4. **Missing Role, Not Missing Card**: higher-value interpretation layer.
5. **Cut Lab Statistical Evidence**: integrate into cut decisions once the evidence is trusted.

Later candidates:

- Budget Replacement Finder.
- Uniqueness / Homogenization Score.
- Commander Fit Score as a small embedded primitive.
- Precon Effect Detector.
- Commander Comparison / Build Direction Tool.

## Proposed Cycle 20 Candidate

Candidate name: **EDHREC Community Deck Delta**

Scope:

- Build the shared EDHREC stat substrate.
- Ship confidence labels.
- Ship Signature vs Staple classifier.
- Ship a Deck Analysis "Community Delta" panel and prompt-artifact section.

Out of scope:

- Precon effect.
- Budget replacement pricing.
- Commander comparison.
- Any live EDHREC scraping.
- Exact EDHREC synergy unless same-color-identity denominators are implemented.

Success criteria:

1. Importer can consume local `averages.csv` and `edhrec.csv` and produce compact derived stats without committing raw archives.
2. For a submitted commander deck, DeckFlow shows present/missing/off-meta/statistical labels with counts and support.
3. Prompt artifacts include the same community delta in one-round-trip form.
4. Low-support commanders get softened copy and no overconfident recommendations.
5. Tests cover exact formulas, partner denominator approximation, staple stripping, and low-support behavior.
