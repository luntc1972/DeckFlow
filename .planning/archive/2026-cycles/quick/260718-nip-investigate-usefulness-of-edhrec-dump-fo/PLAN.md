---
quick_id: 260718-nip
type: investigation
status: in-progress
date: 2026-07-18
---

# Quick Task: Investigate usefulness of EDHREC-dump follow-on features

The sanctioned EDHREC `averages.tgz` dump carries per-commander fields DeckFlow does not yet use. Judge the usefulness of three candidates surfaced at the 2026.07.7 release:

1. **Basic/nonbasic split** — compare a deck's basic/nonbasic land counts against the commander's community average (`avg_basicland`, `avg_nonbasicland`) on /manabase.
2. **Type-mix comparison** — compare the deck's card-type counts (creature/instant/sorcery/artifact/enchantment/battle/planeswalker) against commander averages in deck-analysis/primer surfaces.
3. **oracle_id keying** — key the per-commander baseline lookup on `oracle_id`/`oracle_id2` instead of normalized names.

## Tasks
- T1 (agent): usefulness + placement analysis for candidate 1 against existing manabase report surfaces.
- T2 (agent): usefulness + placement analysis for candidate 2 against deck-analysis/primer/prompt surfaces + core-value fit (paste-artifact first).
- T3 (agent): engineering-value analysis for candidate 3 against ManabaseCommanderKey normalization + real failure modes.
- T4 (foreman): synthesize REPORT.md with per-candidate verdict (BUILD / DEFER / DROP) + effort class.

No code changes. Deliverable: REPORT.md + SUMMARY.md.
