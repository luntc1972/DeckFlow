# Phase 98: Card-Grounding Guard - Context

**Gathered:** 2026-07-14
**Status:** Ready for planning

<domain>
## Phase Boundary

One reusable, cached validation service guaranteeing **no hallucinated or illegal card
ever ships** in a creator-style artifact or critique. Cross-cutting substrate — no
user-visible surface, no flag this phase. Independent of Fusion (P97); needs only the
existing Scryfall client + `ScryfallThrottle`; consumed by Phase 99 (artifact engine)
and, later, the Tier-2 critique (CS-33 re-validation).

Requirements: **CS-21, CS-22, CS-23, CS-24, CS-25** (see REQUIREMENTS.md).

**In scope:** strict fuzzy validator; constrained-selection whitelist builder;
singleton/color-identity/castability checks; the reusable cached service; hallucination
fixtures.
**Out of scope:** the artifact/rubric engine (P99), any tool page or flag (P100), the
lenient distill-time grounding (already shipped in P96 — do not change its semantics),
Karsten Monte-Carlo castability simulation.

</domain>

<decisions>
## Implementation Decisions

### Guard vs P96 grounder split (architecture)
- **D-01:** **Separate strict guard service** — new `ICardGroundingGuard` +
  `CardGroundingGuard`, NOT an extension of `ScryfallCardNameGrounder`. Both compose the
  SAME `IScryfallCardResolver` (+ cache) underneath. Grounder stays lenient
  (distill rewrites + flags, P96 D-07); guard is strict (artifact rejects). Different
  consumers, different behavioral contracts; P96 golden tests untouched.
- **D-02:** **Layering mirrors the P96 pattern exactly:** interface (seam) in Core,
  implementation in `DeckFlow.Web/Services/Scryfall/` (throttle + resolver live in the
  Web host). Pure decision logic — legality/identity/singleton/pip rules — stays
  Core-side and unit-testable without HTTP.
- **D-03:** **Fuzzy-accept + canonicalize (CS-21 literal).** Single fuzzy match →
  accept AND return the canonical Scryfall name; callers MUST use the canonical name,
  never the raw input. 404 / ambiguous / none → reject. Every accepted name is verified
  real; harmless typos heal.
- **D-04:** **Single + batch call surface.** `TryValidateAsync(name, deckContext)` plus a
  batch `ValidateAllAsync(names, deckContext)`. P99's assembly gate ("every card in
  assembled content passed the guard before assembly returns") is one batch call + one
  aggregate result. CS-33 re-validation reuses the same surface later.

### Whitelist candidate pool (CS-22)
- **D-05:** **Pool source = the creator's own deck corpus ONLY** (P95 crawled decks —
  e.g. 39 Snail decks). Every candidate is real, creator-endorsed, on-style; bounded;
  zero extra Scryfall traffic for pool assembly. No category-KB or Scryfall-search
  top-up this phase.
- **D-06:** **Cached per-creator raw pool + per-request filter.** The raw pool is built
  once per creator and cached; each request filters it against the submitted deck's
  context (commander color identity, singleton duplicates, legality) before use.
- **D-07:** **Frequency-ranked, capped.** Candidates ranked by how often they appear
  across the creator's decks (the lift-over-raw-synergy guardrail), capped so the packet
  stays paste-sized. Exact cap = planner's call against the artifact token budget.
- **D-08:** **The whitelist ships INSIDE the ChatGPT packet** with an explicit
  "suggest swaps ONLY from this list" instruction — the paste-ready analog of DeepMTG
  constrained selection. (The $0 P100 tool has no server-side LLM; printing the list is
  the only way to constrain the external LLM.)

### Legality + castability checks (CS-23)
- **D-09:** **Legality source = Scryfall `legalities` field.** Add `legalities` to the
  `ScryfallCard` DTO; check `legalities.commander == "legal"` on the same fetch that
  validates the name — atomic per card, cached with it, and also rejects never-legal
  cards (un-sets/acorn) that a ban list alone misses. `CommanderBanListService`
  (mtgcommander.net) remains the project's ban SoT for its existing surfaces; the guard
  does NOT take a dependency on it.
- **D-10:** **Singleton check = basics-exempt only.** Reject a suggestion already
  present in the submitted deck, except basic lands (incl. snow). "Any number of copies"
  cards (Shadowborn Apostle etc.) still reject — near-zero-value edge, and re-suggesting
  a card the deck already runs is useless advice anyway.
- **D-11:** **Castability = lightweight pip check, NOT Karsten simulation.** Color
  identity ⊆ commander identity, plus the submitted deck's manabase actually produces
  every colored pip the candidate needs (≥1 source per required color), plus optional
  mana-value sanity vs the deck's curve. Pure-Core, deterministic, fast per candidate.
  Do NOT drag `CastabilitySimulator`/`ManabaseAnalyzer` into the guard's blast radius.

### Failure + caching semantics (CS-24)
- **D-12:** **Fail-closed per card.** A card that cannot be validated (Scryfall outage,
  timeout) is rejected/omitted — never shipped unvalidated. If rejects push required
  content below a usable floor (e.g. the commander itself is unvalidatable), surface the
  standard `UpstreamErrorMessageBuilder` 503 copy instead of emitting a degraded packet.
- **D-13:** **Rich verdict record.** Guard returns a sealed record: accepted flag,
  canonical name, and a reject-reason enum
  (`NotFound / Ambiguous / NotLegal / IdentityViolation / SingletonDuplicate / Uncastable / UpstreamUnavailable`).
  P99 can log why; CS-25 hallucination fixtures assert exact reasons.
- **D-14:** **Cache mirrors the grounder's proven pattern:** `IMemoryCache`, 24h positive
  / 1h negative TTL, keyed on normalized name. Cache only the verdict-relevant fields
  (canonical name, color identity, legality, mana cost) — never full card JSON
  (512MB web-tier RAM cap). Short negative TTL lets new-set cards heal fast.

### Claude's Discretion
- Exact whitelist cap value (D-07) — sized against the artifact token budget at plan time.
- Precise result-record field names/shape (D-13) and the batch aggregate shape (D-04).
- The "usable floor" threshold that escalates per-card rejects into a 503 (D-12).
- Whether mana-value sanity is included in the pip check v1 (D-11 "optional").
- Known-hallucination fixture list for CS-25 (draw from LLM-typical fakes + prototype
  observations).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` §CS-21..CS-25 — locked requirement text (incl. the
  DeepMTG constrained-selection note on CS-22).
- `.planning/ROADMAP.md` §"Phase 98" — goal + 4 success criteria (the phase gate).
- `docs/research/creator-style-roadmap.md` §"P91 — Card-Grounding Guard" — origin
  intent ("report's #1 pitfall"), touchpoints, cross-phase guardrails (never let
  parametric card memory override snapshot).

### Existing grounding/Scryfall assets (compose, do not fork)
- `DeckFlow.Core/Knowledge/StatedRulesExtraction/ICardNameGrounder.cs` — the P96 Core
  seam pattern D-02 mirrors; `CardGroundingResult` is the minimal-shape counterexample
  to D-13.
- `DeckFlow.Web/Services/Scryfall/ScryfallCardNameGrounder.cs` — the lenient P96
  implementation; its cache TTLs (24h/1h) are the D-14 template. Do NOT change its
  semantics.
- `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` — `IScryfallCardResolver`,
  `ResolveSingleAsync` (fuzzy `/cards/named`), `NormalizeLookupName`/`NormalizeForScryfall`
  — the shared resolution layer under both grounder and guard (D-01).
- `DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs` — `ScryfallCard` (has `ColorIdentity`;
  needs `legalities` added per D-09).
- `DeckFlow.Web/Extensions/ScryfallServiceCollectionExtensions.cs` — DI registration
  home for the new guard.

### Consumer + prior context
- `.planning/phases/96-stated-rules-distiller/96-CONTEXT.md` — D-07 ("fuzzy-correct then
  flag; the hard reject is Phase 98's guard") — the semantic boundary D-01 preserves.
- `.planning/phases/97-profile-fusion-conflict-ledger/97-CONTEXT.md` — fused-profile
  shape P99 will diff against; guard is independent of it but P99 consumes both.
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — the packet-service shape P99
  mirrors; the guard's batch API (D-04) is designed for that consumption.
- `DeckFlow.Web/Services/UpstreamErrorMessageBuilder.cs` — the 503 copy path for D-12's
  usable-floor escalation.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IScryfallCardResolver` + `ScryfallThrottle` — the guard's entire Scryfall access
  layer already exists; the guard adds decision logic, not HTTP plumbing.
- `ScryfallCardNameGrounder`'s IMemoryCache pattern (24h/1h, normalized-name key) —
  copy for the guard's verdict cache (D-14).
- P95 creator deck corpus (crawled decks per creator profile) — the whitelist pool
  source (D-05); frequency data across decks drives D-07 ranking.
- `ScryfallCard.ColorIdentity` — already deserialized; identity check needs no new
  fetch. `legalities` is the only DTO addition (D-09).

### Established Patterns
- Core-seam + Web-impl split for Scryfall-touching services (P95 D-11 / P96 grounder) —
  D-02 applies it verbatim; pure legality/pip rules live Core-side for direct xUnit.
- Named Polly pipeline (`scryfall`) + `ScryfallThrottle.ExecuteAsync` around every
  Scryfall call — mandatory, already enforced in the resolver.
- Internal test-seam ctor + `[InternalsVisibleTo]` — use for guard tests that stub the
  resolver instead of mocking HTTP.

### Integration Points
- P99 `CreatorStylePacketService` calls `ValidateAllAsync` as its assembly gate and
  embeds the whitelist (D-08) in the packet.
- DI registration in `ScryfallServiceCollectionExtensions` alongside grounder/resolver.
- Whitelist pool reads the creator's stored deck corpus (P95 store) — read-only.

</code_context>

<specifics>
## Specific Ideas

- The packet instruction pairing is the point: "critique only with the provided cards" +
  "suggest swaps ONLY from this list" (D-08) — the whitelist and the instruction ship
  together or constrained selection doesn't constrain anything.
- CS-25 fixtures should include the classic LLM hallucination shapes: plausible-but-fake
  names, real-card-wrong-name variants (e.g. "Dockside Extortonist" → resolves), banned
  staples (e.g. Dockside Extortionist is banned 2024-09-23 — a real card the guard must
  now REJECT on legality, good fixture), off-identity staples, and a card already in the
  submitted deck.

</specifics>

<deferred>
## Deferred Ideas

- **Whitelist top-up from category-KB / Scryfall search** (D-05 rejected for v1) — if
  creator corpora prove too thin for useful swaps, widen the pool in a later phase.
- **"Any number of copies" singleton exemptions** (D-10) — revisit only if a real
  creator corpus surfaces Shadowborn/Persistent-Petitioners archetypes.
- **Full Karsten castability rating for suggestions** (D-11) — belongs with the manabase
  engine refactor lane, not the guard.
- **Server-side re-validation loop for Tier-2 critique** (CS-33) — API is shaped for it
  (D-04), implementation belongs to the deferred critique phase.

</deferred>

---

*Phase: 98-card-grounding-guard*
*Context gathered: 2026-07-14*
