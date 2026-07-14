# Phase 98: Card-Grounding Guard - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-14
**Phase:** 98-card-grounding-guard
**Areas discussed:** Guard vs P96 grounder split, Whitelist candidate pool, Legality + castability depth, Failure + caching semantics

---

## Guard vs P96 grounder split

| Option | Description | Selected |
|--------|-------------|----------|
| Separate guard service | New ICardGroundingGuard composing the same IScryfallCardResolver + cache; grounder stays lenient, guard strict | ✓ |
| Extend grounder with strict mode | One service, mode flag per call | |
| Guard wraps grounder | Guard calls ICardNameGrounder then layers legality on top | |

**User's choice:** Separate guard service (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Web impl + Core seam | Mirror P96 pattern: interface in Core, impl in DeckFlow.Web/Services/Scryfall | ✓ |
| Web-only service | Interface + impl both in Web | |
| You decide | Planner picks | |

**User's choice:** Web impl + Core seam (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Fuzzy-accept + canonicalize | Single fuzzy match → accept + return canonical name; 404/ambiguous → reject | ✓ |
| Exact-only | Any fuzzy correction is a reject | |
| You decide | Planner picks per CS-21 | |

**User's choice:** Fuzzy-accept + canonicalize (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Single + batch validate | TryValidateAsync + ValidateAllAsync; batch backs P99 assembly gate | ✓ |
| Single-name only | P99 loops per name | |
| You decide | Planner shapes API | |

**User's choice:** Single + batch validate (Recommended)

---

## Whitelist candidate pool

| Option | Description | Selected |
|--------|-------------|----------|
| Creator deck corpus | Cards from creator's own crawled decks only | ✓ |
| Creator corpus + category KB | Add synergy/category-knowledge cards | |
| Creator corpus + Scryfall search | Top up with identity-legal staples | |

**User's choice:** Creator deck corpus (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Cached pool + request filter | Per-creator raw pool cached; per-request deck-context filter | ✓ |
| Build fully per request | Assemble + validate every submission | |
| You decide | Planner picks | |

**User's choice:** Cached pool + request filter (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Frequency-ranked, capped | Rank by appearance frequency across creator decks; cap for paste size | ✓ |
| Full pool, uncapped | Ship every legal corpus card | |
| You decide | Planner sizes vs token budget | |

**User's choice:** Frequency-ranked, capped (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Printed in packet | Whitelist ships in ChatGPT packet with "swaps ONLY from this list" instruction | ✓ |
| Server-side only | Internal validation only; no suggestion list in packet | |
| Both roles explicit | Same builder feeds packet + future server-side CS-33 validation | |

**User's choice:** Printed in packet (Recommended)

---

## Legality + castability depth

| Option | Description | Selected |
|--------|-------------|----------|
| Scryfall legalities field | Add `legalities` to DTO; check legalities.commander on the name-validation fetch | ✓ |
| CommanderBanListService | Reuse mtgcommander.net scrape (project ban SoT) | |
| Scryfall + banlist cross-check | Both, belt-and-suspenders | |

**User's choice:** Scryfall legalities field (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Basics exempt only | Reject cards already in deck except basic lands (incl. snow) | ✓ |
| Full rules-aware | Also exempt any-number cards via oracle-text check | |
| You decide | Planner picks exemption set | |

**User's choice:** Basics exempt only (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Lightweight pip check | Identity ⊆ commander + deck produces each required pip color + optional MV sanity | ✓ |
| Full Karsten simulation | CastabilitySimulator Monte-Carlo per candidate | |
| Identity-only | Skip castability beyond color identity | |

**User's choice:** Lightweight pip check (Recommended)

---

## Failure + caching semantics

| Option | Description | Selected |
|--------|-------------|----------|
| Fail-closed per card | Unvalidatable card rejected/omitted; below usable floor → UpstreamErrorMessageBuilder 503 | ✓ |
| Fail-open with annotation | Pass through marked unvalidated on outage | |
| Fail whole request | Any validation error 503s the artifact | |

**User's choice:** Fail-closed per card (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Rich verdict + reason | Sealed record: accepted, canonical name, reject-reason enum | ✓ |
| Boolean + canonical name | Mirror CardGroundingResult minimal shape | |
| You decide | Planner shapes record | |

**User's choice:** Rich verdict + reason (Recommended)

| Option | Description | Selected |
|--------|-------------|----------|
| Mirror grounder TTLs | IMemoryCache 24h/1h, normalized-name key, small verdict fields only | ✓ |
| Long-lived + size cap | Multi-day TTL with explicit size limits | |
| You decide | Planner picks TTLs | |

**User's choice:** Mirror grounder TTLs (Recommended)

---

## Claude's Discretion

- Exact whitelist cap value (vs artifact token budget)
- Result-record field names/shape + batch aggregate shape
- "Usable floor" threshold escalating per-card rejects into a 503
- Whether mana-value sanity ships in pip check v1
- Known-hallucination fixture list for CS-25

## Deferred Ideas

- Whitelist top-up from category-KB / Scryfall search (if corpora too thin)
- "Any number of copies" singleton exemptions
- Full Karsten castability rating for suggestions (manabase-refactor lane)
- Server-side re-validation loop for Tier-2 critique (CS-33)
