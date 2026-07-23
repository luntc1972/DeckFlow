# Commander Deck-Builder Feature Opportunities

## Research and implementation priorities for DeckFlow

**Research date:** July 18, 2026  
**Scope:** Commander and cEDH deck-building tools that players request, that remain missing or poorly served, and that fit DeckFlow's existing capabilities.

## Executive recommendation

The strongest opportunity is not another tool that generates a complete 100-card Commander deck. That market is crowded. The better opportunity is decision-support software that helps a human finish a deck, quantify tradeoffs, learn from real games, and communicate the intended play experience.

Recommended implementation order:

1. **Cut Lab**
2. **Goal-Based Consistency Lab**
3. **Deck Experiment Journal**
4. **Pod Fit and Rule Zero Passport**
5. **Collection-Aware Upgrade Planner**
6. **Originality and Anti-Staple Explorer**
7. **Collaborative Deck Review**
8. **Commander Pilot Trainer**

The first four can work without any AI prompt. AI can remain an optional explanation layer rather than a dependency.

## Existing DeckFlow coverage

DeckFlow already provides strong foundations:

- Deterministic mana-base analysis and Monte Carlo simulation
- Opening-hand, mulligan, castability, and plan-presence analysis
- Commander bracket classification
- Combo and near-combo discovery through Commander Spellbook
- Deck analysis, comparison, primers, and cEDH metagame comparison
- Portable deck history, snapshots, and diffs
- Category and plan-role inference
- Moxfield and Archidekt synchronization and conversion
- Set-upgrade workflows
- Card, mechanic, and rules lookup

This makes DeckFlow well suited to become a specialist decision layer that works with existing deck builders rather than replacing them.

## Rating summary

Request level is based on repeated community discussions, feature-request voting, and the number of new products pursuing the problem. It is directional evidence, not a formal market survey. Effort assumes reuse of DeckFlow's current engines.

| Priority | Feature | Request | Effort | AI needed? |
|---:|---|---:|---|---|
| 1 | Cut Lab | 5/5 | Medium | No |
| 2 | Goal-Based Consistency Lab | 4.5/5 | Medium | No |
| 3 | Deck Experiment Journal | 4/5 | Medium | Optional |
| 4 | Pod Fit / Rule Zero Passport | 4/5 | Medium-Large | Optional |
| 5 | Collection-Aware Upgrade Planner | 5/5 | Large | Optional |
| 6 | Originality / Anti-Staple Explorer | 3.5/5 | Medium-Large | Optional |
| 7 | Collaborative Deck Review | 3/5 | Large | No |
| 8 | Commander Pilot Trainer | 3/5 | Large-XL | Scope-dependent |

## 1. Cut Lab

**Request level:** 5/5  
**Effort:** Medium  
**AI assistance:** Not required  
**Implementation priority:** Highest

Players can find possible cards easily. The recurring pain is reducing a 110- to 150-card pool to a legal 100-card deck without cutting lands, draw, interaction, or the deck's identity. Commander discussions repeatedly call this the hardest part of deckbuilding.

A deterministic Cut Lab should:

- Accept an oversized decklist.
- Ask for the primary plan, secondary plan, bracket, budget, and desired play experience.
- Let the user lock commanders, pet cards, lands, and essential packages.
- Group cards that compete for the same functional slot.
- Detect curve congestion, stranded subthemes, redundant finishers, weak floor cases, and cards with too few enablers.
- Protect configurable role floors for lands, ramp, draw, interaction, protection, engines, payoffs, and win conditions.
- Show measurable consequences after every proposed cut.
- Work in rounds: obvious cuts, structural choices, then preference calls.
- Export the final list and an add/cut patch for Moxfield or Archidekt.

The tool should not claim one card is objectively worse. It should explain the tradeoff. For example: cutting one card may lower the average mana value while cutting another may reduce repeatable draw engines.

DeckFlow already has parsing, role classification, categories, mana simulation, combo data, bracket rules, and diff output. Most effort lies in defensible comparison rules and interaction design.

## 2. Goal-Based Consistency Lab

**Request level:** 4.5/5  
**Effort:** Medium  
**AI assistance:** Not required  
**Implementation priority:** Very high

Players routinely ask how many copies of an effect they need to see by a specific turn. Existing calculators tend to be generic, slow, or disconnected from an actual deck's mulligan policy and mana requirements.

The user should be able to define milestones such as:

- Cast the commander by turn three.
- See ramp by turn two.
- Hold interaction by turn two.
- Find a sacrifice outlet by turn five.
- Have both an engine and a payoff by turn six.
- Recover from a board wipe within two turns.

The highest-value interaction is a what-if swap. When the player replaces one payoff with one ramp spell, DeckFlow should immediately recalculate:

- Commander-on-time probability
- Keepable-hand rate
- Mana and color reliability
- Early-interaction availability
- Plan-presence rate
- Probability of seeing each functional category by the selected turn
- Flood, screw, and curve risk

DeckFlow already performs Monte Carlo simulation, London mulligan evaluation, plan-presence analysis, and per-card castability. This feature primarily adds user-defined goals, saved scenarios, swap comparison, and visualization.

## 3. Deck Experiment Journal

**Request level:** 4/5  
**Effort:** Medium  
**AI assistance:** Optional only  
**Implementation priority:** High

Deck History records what changed but not whether the change helped. Players increasingly build personal trackers because memory is unreliable and win rate alone is misleading in a political multiplayer format.

Extend the portable Deck History file with optional game observations:

- Deck snapshot used
- Pod size and opposing commanders
- Stated bracket or game expectation
- Mulligans and play order
- Turn the commander was first cast
- Turn the deck first executed its plan
- Mana screw or flood
- Cards marked dead, stranded, clutch, or overperforming
- Win, loss, or draw
- Whether the game felt enjoyable and evenly matched
- One-sentence experiment hypothesis

Normal statistics can produce useful findings:

- Commander-on-time frequency before and after a change
- Mana-problem and mulligan rates
- Cards repeatedly marked dead or useful
- Plan-execution rate
- Results by pod composition or opposing commander
- Minimum sample-size warnings

The tool should distinguish correlation from causation. AI could optionally summarize the statistics, but it should not calculate or invent them.

The primary implementation risk is backward-compatible evolution of the portable history-file schema.

## 4. Pod Fit and Rule Zero Passport

**Request level:** 4/5  
**Effort:** Medium-Large  
**AI assistance:** Optional only  
**Implementation priority:** High

A bracket number cannot fully describe a Commander experience. Wizards has said bracket adoption is growing while players still need better language for finding compatible games.

The tool should compare three to five decks across measurable dimensions:

- Bracket and Game Changers
- Intended setup and win window
- Infinite or deterministic combos
- Tutors and redundancy
- Free or cheap interaction
- Mass land denial, stax, hard locks, extra turns, and theft
- Commander dependence
- Board-wipe recovery
- Combat versus noncombat wins
- Snowball potential
- High-salt cards worth disclosing

The result should be a compatibility matrix, not a winner prediction. It can identify timing or experience mismatches and suggest a concrete pregame disclosure. A phone-friendly share card or QR code would make it useful at an LGS.

No AI is required. DeckFlow can generate plain-language disclosure templates from structured results. Optional AI could soften or personalize the wording.

## 5. Collection-Aware Upgrade Planner

**Request level:** 5/5  
**Effort:** Large  
**AI assistance:** Optional only  
**Implementation priority:** Medium

This may have the highest raw demand. Multiple 2026 products focus on building from owned cards, and Archidekt feature voting shows demand for preferring owned versions and preserving collection editions.

The market is also crowded. ManaBox already handles physical locations, exact versions, binders, and allocation between decks well. DeckFlow should not build a complete collection manager.

A narrower DeckFlow implementation should:

- Import ManaBox, Moxfield, and Archidekt collection exports.
- Match exact printings and acceptable alternate printings.
- Suggest owned upgrades first.
- Show the best additional purchases within a stated budget.
- Identify cards already committed to other decks.
- Produce cards-to-pull, cards-to-buy, and cards-to-return checklists.

AI is unnecessary for ownership, legality, price, printing identity, and role matching. It could optionally explain unusual recommendations.

Effort is high because collection formats, exact printings, quantities, price sources, physical locations, and cross-deck allocation create many edge cases.

## 6. Originality and Anti-Staple Explorer

**Request level:** 3.5/5  
**Effort:** Medium-Large  
**AI assistance:** Optional  
**Implementation priority:** Medium

EDHREC reports what people include. It does not prove that those choices form the right deck for a particular player. Players frequently complain that optimized recommendations produce repetitive lists and generic staples.

An Originality Explorer should find role-equivalent alternatives based on:

- Functional role and Oracle-text behavior
- Mana value and permanent type
- Required enablers
- Price and popularity
- Salt score
- Flavor, plane, set, creature type, artist, or era
- Preference for creatures, permanents, or spells
- Desired variance and replayability

The tool could show a familiarity profile without treating unusual as automatically better.

AI is optional. The hard part is identifying true functional equivalents. Simple Oracle-text substring matching will produce poor recommendations, so DeckFlow would need stronger structured effect classification.

## 7. Collaborative Deck Review

**Request level:** 3/5  
**Effort:** Large  
**AI assistance:** Not required  
**Implementation priority:** Low-Medium

The strongest form is GitHub-style review for decks:

- A reviewer proposes paired add/cut changes.
- Each proposal includes a rationale and expected effect.
- The owner accepts or rejects changes individually.
- DeckFlow verifies legality and structural health.
- Accepted changes export as a builder-compatible patch.

AI is unnecessary because humans supply the review. Effort comes from accounts or share tokens, persistence, permissions, concurrent edits, comments, notifications, and abuse controls. Existing products already cover portions of this workflow.

## 8. Commander Pilot Trainer

**Request level:** 3/5  
**Effort:** Large-XL  
**AI assistance:** Scope-dependent  
**Implementation priority:** Lowest

A restricted trainer can work without AI:

- Generate opening hands.
- Ask whether to keep or mulligan.
- Check whether the hand meets declared goals.
- Quiz verified combo lines.
- Ask which land or spell supports the defined plan.
- Provide rules exercises grounded in authoritative rules text.

An advanced trainer that evaluates arbitrary multiplayer boards would require either AI assistance with correctness risk or a substantial Magic rules and strategy engine. The safe starting scope is deterministic opening-hand and verified-combo training.

## Features not recommended

DeckFlow should avoid these crowded or strategically weak directions:

- Another complete 100-card deck generator
- Another universal one-to-ten power score
- A replacement for Moxfield or Archidekt
- A full collection scanner and inventory platform
- Another generic goldfish table
- Recommendations based only on EDHREC inclusion percentages
- Another standalone combo finder
- Another raw bracket checker

## Recommended product sequence

### Phase 1: Cut Lab foundation

Build card locking, slot competition, structural safeguards, iterative cuts, and builder-compatible export.

### Phase 2: Goal simulation integration

After every cut or swap, recalculate keepable hands, commander timing, mana reliability, plan presence, category availability, and curve.

### Phase 3: Experiment Journal

Record whether theoretical improvements appear in actual games. Preserve DeckFlow's file-you-own model.

### Phase 4: Pod Fit

Use the same structural signals to help players select compatible decks and conduct a more useful pregame conversation.

The resulting non-AI product loop is:

> Build an oversized list -> make evidence-backed cuts -> simulate the finished deck -> record real games -> revise using observations -> choose a compatible pod.

## Final product thesis

Deck builders do not primarily need software to make every decision for them. They need software that makes their decisions legible.

> Moxfield and Archidekt hold your lists. EDHREC shows what everyone else plays. DeckFlow helps you understand your deck, finish it, test it, and fit it to the games you actually want.

The strongest first investment is a combined **Cut Lab plus Goal-Based Consistency Lab**. It has the best balance of demonstrated demand, moderate implementation effort, reuse of existing DeckFlow engines, and zero dependence on AI.

## Research sources

- Wizards of the Coast, [Commander Brackets Beta Update, February 9, 2026](https://magic.wizards.com/en/news/announcements/commander-brackets-beta-update-february-9-2026)
- Archidekt, [Feature Voting](https://archidekt.com/features)
- Archidekt, [Playtester Logs](https://archidekt.com/news/13186079?page=1)
- ManaBox, [Decks in the Collection](https://www.manabox.app/guides/decks/collection-decks/)
- EDHREC, [FAQ and data methodology](https://edhrec.com/faq)
- EDHREC, [Is Optimization Ruining Commander Deck Building?](https://edhrec.com/articles/is-optimization-ruining-commander-deck-building)
- Reddit r/EDH, [How do I get better at cutting cards?](https://www.reddit.com/r/EDH/comments/1ru1uh5/how_do_i_get_better_at_cutting_cards_from_my/)
- Reddit r/EDH, [How do you cut cards?](https://www.reddit.com/r/EDH/comments/1uofemj/how_do_you_cut_cards/)
- Reddit r/EDH, [The struggle of cutting cards](https://www.reddit.com/r/EDH/comments/1l4ow4c/the_struggle_of_cutting_cards/)
- Reddit r/EDH, [206 tracked Commander games](https://www.reddit.com/r/EDH/comments/1rnkj13/followup_to_my_earlier_post_ive_now_tracked_206/)
- Reddit r/EDH, [318 tracked Commander games](https://www.reddit.com/r/EDH/comments/1v021tt/i_tracked_318_games_of_commander_i_tried_to_power/)
- Reddit r/EDH, [Commander probability reference table](https://www.reddit.com/r/EDH/comments/1r6bl0i/how_many_x_do_i_run_when_will_i_draw_them_i_made/)
- Reddit r/EDH, [Moxfield versus Archidekt](https://www.reddit.com/r/EDH/comments/1e4dpd4/moxfield_v_archidekt/)
- TriomeLab, [Changelog and Deck Diary](https://triomelab.com/changelog)
- TCGSwap, [Collaborative Deck Builder](https://tcgswap.ie/)

