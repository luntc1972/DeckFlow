# P89/P90 Prototype + Deck-Ownership Finding — Salubrious Snail

*Fable prototype (2026-07-05): stated-rules extraction (P89) + say-vs-do fusion (P90) on real data, plus a deck-ownership/folder audit answering "are any of the 39 decks patron decks?".*

## Deck ownership — no patron contamination
All 39 Archidekt decks are `owner=SalubriousSnail`; zero hit patron/patreon/commission/review/deck-doctor keywords in name or description; all names are personal brews. Patron-reviewed decks would live on the *patrons'* accounts (viewed, not owned) so they never appear in an `ownerUsername` crawl.

**Folders (5, all personal-workflow — none patron):**
| folder | name | decks | profile weight |
|---|---|---|---|
| 486499 | Current Decks | 5 | ⬆ canonical |
| 724004 | Secondary decks | 5 | ⬆ canonical |
| 563240 | Budget deck pool | 10 | ⬇ skews cheap |
| 486501 | Decks in consideration | 7 | ⬇ WIP |
| 486502 | Other | 12 | neutral |

→ **P88 refinement (CS-04d):** capture `parentFolder` and segment/weight the profile by folder; don't pool all 39 equally (Budget skews cheap, In-consideration is WIP). Folder name is in the Archidekt API — cheap.

## P89 feasibility: YES — usable stated-rules extractable from current distill output

27 measurable stated-rules extracted from ~41 read artifacts. Coverage: ~44% of the 85 artifacts yield ≥1 measurable rule; ~86% yield at least a usable principle; ~14% are pure content/meta (no deckbuilding signal). The **Key Clips section is the gold seam** — near-verbatim, quotable, numbers survive with timestamps.

**Representative extracted rules:** lands 37-42 (28 only for low-curve+aggressive-mull); ramp 7-12 baseline; draw 13-18; removal 8-14 (15-20 broad); interaction ~20 slow / 5-8 proactive; **board wipes 3-5 max ("reconsider at your 4th-5th")**; counterspells ≥8 in blue; tutors ~3 at Bracket 2; copies-to-see-in-opener ≥10; colored-symbol shorthand 30/25/20/15; full template ≈98 slots.

## P90 fusion: works and DISCRIMINATES

| Metric | Stated | Measured (39 decks) | Verdict |
|---|---|---|---|
| Land count | 37-42, 28-exception | avg 37.4, and the one 28-land deck is his lowest-curve build | ✅ strongest agreement (walks his crusade incl. the carve-out) |
| Ramp | 7-12, deck-dependent | avg 12.0 (2-29) | ✅ agree |
| Card draw | 13-18 | avg 11.1 | ⚠ mild delta (builds below his headline) |
| **Board wipes** | **3-5 max, overrated** | ~1.2/deck, 19 decks run zero | ✅ **agreement, not hypocrisy** — decks underweight wipes vs format canon AND his philosophy explicitly endorses that |
| Counterspells | ≥8 in blue | 10-14 in blue *control*, 1-3 in blue *splash* | ⚠ delta — rule true only for control shells |
| Salt/power | anti-fast-mana | low salt, but Sol Ring everywhere | 😏 self-acknowledged cosmetic delta |

**The differentiating insight:** fusion correctly separates (i) matches-rule, (ii) deviates-from-canon-but-matches-own-philosophy (board wipes — the flagship), (iii) narrower-in-practice-than-stated (counters). Case (ii) is the product gold: "this creator's decks look wrong by template but right by their own philosophy."

## Gaps P89 must close (feed the phase plan)
1. **`stated_rules:` YAML block** in the distill template (`{category,metric,value,comparator,condition,clip_ts}`) — near-free at distill time; retrofit via one re-distill pass. Turns extraction from an LLM reading task into a cheap aggregation query.
2. **`content_type:` frontmatter** — skip the ~14% non-deckbuilding artifacts, clean coverage denominator.
3. **Conditionality is first-class** (`applies_when`: archetype/curve/bracket) — else fusion emits false deltas (hit on the draw metric). Highest-risk modeling decision.
4. **Provenance/recency** (video date) — creator revises his own positions; newer supersedes.
5. **Measured side is the weaker leg** — Archidekt labels sparse (ramp 44%, draw 28%, wipes 3% of decks); lean on oracle/name classifiers (harvested store + CS-06); **filter the 19 >105-card maybeboard-contaminated decks** before per-deck ratios.

**Effort signal:** one creator (~85 artifacts) → ~30 measurable rules + ~15 principles at ~40 artifact-reads; with the `stated_rules` frontmatter, extraction becomes cheap aggregation and fusion is a small deterministic join.
