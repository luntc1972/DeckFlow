# 0005 — `ScryfallCardNameIndex` collision precedence: stated by the caller, not by call order

Date: 2026-08-19

## Context

`ScryfallCardNameIndex.Add` wrote each of its three maps unconditionally:

```csharp
// DeckFlow.Core/Manabase/ScryfallCardNameIndex.cs:37 — before
_byName[Normalize(card.Name)] = card;
```

documented at `:24` as "Last write wins within a single map". Every card enters in Scryfall's
response order, so **which card won a name collision depended on the order the HTTP response
happened to arrive in** — not a contract, and not stable.

`ManabaseAnalysisService.ResolveCardsAsync` compensated by ordering its two `Add` loops, with a
comment stating that the loops themselves WERE the precedence rule: positioned cards ordered by
`GlobalPosition`, then all unpaired cards. That produced two rules nobody chose:

1. **Latest deck position won** a collision between two positioned cards.
2. **An unpaired card beat every paired card** — a card Scryfall returned that matched no
   submission uniquely outranked one we had confidently attributed to a specific entry.

Both were emergent consequences of append order, not decisions.

**Provenance, established 2026-08-19 before deciding:** `git log -S 'unpairedCards'` returns exactly
one commit, `bec78b79` (2026-08-18, partition-then-chunk), and `git branch --contains` places it on
`feat/cache-tier0` alone — never in `origin/main`. So neither rule had ever shipped, and preserving
them had no compatibility value.

A further trap: the caller-side ordering could not survive the cache work. Chunk membership now
depends on cache warmth, so indexing per chunk would let a warm cache pick a different winner, and
a fully warm deck would index nothing at all.

## Decision

`Add` takes an explicit precedence argument:

```csharp
public void Add(ScryfallCardData card, int priority = 0)
```

A collision is decided by what the caller states, then by the cards themselves — never by arrival
order, except in the single case rule 3 names:

1. higher `priority` wins — this is caller knowledge, not a card property;
2. tie -> the lower printing key (`set|collector`) wins, and a card carrying a printing key
   outranks one carrying none;
3. full tie -> the incumbent keeps the slot. This is the ONE arrival-order-dependent case, and it
   needs equal `priority` AND a missing printing key on both cards, so only a caller that states
   no priority can reach it (see the CLI note under Consequences).

All three maps (`_byName`, `_byFrontFace`, `_byPrinting`) route through one `Put` helper, so the
type has exactly one collision rule rather than three copies of an assignment.

`ManabaseAnalysisService` states its evidence ordering as named bands instead of loop order:

| Source | Priority | Rationale |
|---|---|---|
| paired to a submission | `int.MaxValue - GlobalPosition` | strongest evidence; earliest deck position wins |
| name-search repair | `2` | search may return a different printing than the batch |
| unpaired returned card | `1` | matched no submission uniquely; weakest |
| *(index default)* | `0` | no stated preference; loses to every band above |

**The bands sit ABOVE the default on purpose.** An earlier draft used `-GlobalPosition` and
`int.MinValue`, which put the default 0 at the TOP of the ladder — a future bare `index.Add(card)`
in the Web path would have silently outranked every paired card with no compile error and no failing
test. That is the implicit-precedence bug this ADR exists to remove, reintroduced through the
default. The ladder is inverted so that stating nothing is the weakest thing a caller can do.

Both emergent rules are deliberately reversed: **earliest** position wins, and **paired beats
unpaired**.

## Consequences

- The index is order-independent for every caller that states a priority. The same set of cards
  yields the same winner however they are added, so cache warmth, chunk boundaries and response
  ordering can no longer move the result. The exception is a full tie (rule 3), which requires two
  cards in the same band that BOTH lack `set` + `collector_number`; a Scryfall response does not
  produce that, so `ManabaseAnalysisService` cannot reach it.
  `ResolveCardsAsync_EarliestDeckPosition_BeatsTheBetterPrintingKey` and
  `ResolveCardsAsync_UnpairedCard_LosesToPairedCard` are built so the printing tiebreak would pick
  the WRONG card; only the priority bands produce the right one, so dropping the paired band — or
  inverting the comparison — fails a test. ⚠ Neither pins a band's VALUE: dropping
  `UnpairedPriority` to the 0 floor still loses to paired, so both stay green.
  `PriorityBands_RankSearchFallbackAboveUnpairedAboveFloor` pins the ladder itself.
- The caller-side ordering hack retires. `positionedCards.OrderBy(GlobalPosition)` is gone.
- ⚠ `DeckFlow.CLI/ManabaseCommandRunner.cs:193` has no positional information and keeps the default
  priority of 0 — the floor — so all its cards tie and its collisions now resolve by printing key
  instead of by response order. This is shipped code whose selected printing can change. Accepted
  deliberately: the previous selection was arbitrary rather than correct, and every printing of a
  card carries the same mana-relevant data.
- The tiebreak is ORDINAL on `set|collector`, not numeric. The goal is a stable total order, not a
  "best" printing.
- `Add_DuplicateKey_LastWriteWins` was replaced by `Add_DuplicateKeyFullTie_IncumbentWins`; the old
  test pinned the behavior this ADR removes.

## Alternatives rejected

- **First write wins.** Still an arrival-order rule — it inverts the dependence on Scryfall's
  response order instead of removing it.
- **Prefer the card carrying a printing key, alone.** Effectively every Scryfall card carries set
  and collector, so it almost never breaks a tie. Retained only as a sub-rule.
- **Printing order alone, no priority.** Order-independent, but cannot express paired-vs-unpaired:
  "unpaired" is caller knowledge that no card property encodes.
