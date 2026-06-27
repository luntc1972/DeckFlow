# Phase 74: Cross-Tool Deck-Input Persistence - Context

**Gathered:** 2026-06-27
**Status:** Ready for planning
**Source:** Interactive discuss (this session) + 74-SPEC.md + read-only investigation

<domain>
## Phase Boundary

When a user enters a deck (public URL or pasted text) in one DeckFlow tool and navigates
to another single-deck tool, the deck input is restored automatically so they don't
re-paste. Client-side only; server stays stateless.

**In scope (single-deck tools):** `/deck-analysis`, `/manabase`, `/cedh-meta-gap`,
`/convert`, `/deck-primer`.

**Deferred to a follow-up phase:** 2-deck tools `/deck-comparison` (A/B) and `/sync`
(Moxfield + Archidekt) — they need named secondary slots; out of scope here.

**Out of scope (no deck-source input):** card-lookup, mechanic-lookup, judge-questions,
content-kb, commander-categories, suggest-categories.
</domain>

<decisions>
## Implementation Decisions (LOCKED)

### Storage mechanism
- **Client-side `sessionStorage`** (NOT localStorage, NOT server `ISession`). Per-tab,
  clears on tab close, tab-independent, zero server RAM (512MB web cap), no cleanup.
- One shared key namespace holding the canonical last deck source:
  `{ inputSource, deckUrl, deckText }`. Tool-specific extras (e.g. manabase `DeckName`,
  meta-gap `CommanderName`) are NOT shared in v1 — only the core deck source.

### Restore behavior
- **Silent prefill only.** On page load, if the tool's deck-source field(s) are EMPTY,
  populate them from the stored deck. No notice, no "restored" banner, no prompt.
- Restore the `DeckInputSource` selection (URL vs paste radio) too, so the correct input
  mode shows. If both URL and text are present in the store, the stored `inputSource`
  decides which is active.
- Never overwrite a field the user has already typed into (only fill when empty).

### Write behavior
- On deck-source input/change, write the current deck source to the store. Keep it
  current as the user edits so the latest deck carries to the next tool.

### Flag
- **No feature flag.** Ships on by default (additive client-side UX). No kill-switch.

### DeckText size cap (Claude's discretion → planner sets concrete value)
- `sessionStorage` per-origin quota is ~5MB; a deck list is small (<10KB typical). Cap
  stored `deckText` at a safe ceiling (planner picks, e.g. ~100KB). If over cap, store
  the URL only / skip the text write rather than throwing on quota.

### Field-shape mapping (per-tool reality, from investigation)
The tools do NOT share one request shape — the restore glue must map the canonical
store to each tool's actual fields:
- `DeckAnalysisRequest`: `DeckInputSource` (enum), `DeckUrl`, `DeckText`
- `ManabaseRequest`: `DeckInputSource` (enum), `DeckUrl`, `DeckText` (+ `DeckName`, not shared)
- `MetaGapRequest`: `DeckSource` (single string = URL OR text) (+ `CommanderName`, not shared)
- `DeckConvertRequest`: `InputSource`, `DeckUrl`, `DeckText` (+ formats, not shared)
- `DeckPrimerRequest`: `DeckInputSource` (enum), `DeckUrl`, `DeckText` (SPLIT, verified at
  `DeckPrimer.cshtml:119-132` — NOT a combined single field)
- **Only `MetaGapRequest` uses the combined single `DeckSource` field.**
The shared TS module exposes a canonical `{inputSource, deckUrl, deckText}` and each
tool's page script adapts to/from its own field IDs.

### Claude's Discretion
- Exact TS module name/location under `wwwroot/ts/`, sessionStorage key name(s).
- Whether meta-gap/primer's combined `DeckSource` field maps from `deckUrl` vs `deckText`
  based on stored `inputSource`.
- Concrete `deckText` size cap value.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing sessionStorage precedent (reuse the pattern)
- `DeckFlow.Web/wwwroot/ts/category-suggestions.ts` — sessionStorage form-state pattern
- `DeckFlow.Web/wwwroot/ts/card-lookup.ts` — `SINGLE_CARD_STATE_KEY` sessionStorage state
- `DeckFlow.Web/wwwroot/ts/content-kb.ts` — `FILTER_STORAGE_KEY` sessionStorage filter state
- `DeckFlow.Web/wwwroot/ts/site.ts` — localStorage theme key (contrast: persistent vs session)

### Tool views (deck-source input fields to wire)
- `DeckFlow.Web/Views/.../DeckAnalysis.cshtml` (URL field, DeckText textarea, inputSource radio)
- `DeckFlow.Web/Views/.../Manabase.cshtml`
- `DeckFlow.Web/Views/.../CedhMetaGap.cshtml` (combined DeckSource field)
- `DeckFlow.Web/Views/.../DeckConvert.cshtml`
- `DeckFlow.Web/Views/.../DeckPrimer.cshtml`

### Request models (field shapes)
- `DeckAnalysisRequest.cs`, `ManabaseRequest.cs`, `MetaGapRequest.cs`,
  `DeckConvertRequest.cs`, `DeckPrimerRequest.cs`

### Build coupling
- `DeckFlow.Web/tsconfig.json` (strict, module:none) — TS compiles via MSBuild; compiled
  `wwwroot/js/*.js` is gitignored, NEVER commit it.

### Phase SPEC
- `.planning/phases/74-cross-tool-input-persistence/74-SPEC.md`
</canonical_refs>

<specifics>
## Specific Ideas

- Shared module shape: `getLastDeck()` / `setLastDeck({inputSource, deckUrl, deckText})`
  over a single sessionStorage JSON key. Each tool page calls `getLastDeck()` on
  DOMContentLoaded (fill-if-empty) and `setLastDeck(...)` on field change.
- Restore must run AFTER the server-rendered values are in the DOM, and only fill when
  the rendered field is empty (so a POST round-trip's own values win).
</specifics>

<deferred>
## Deferred Ideas

- 2-deck tools (`/deck-comparison`, `/sync`) — named secondary slots, follow-up phase.
- Sharing tool-specific extras (DeckName, CommanderName, convert formats) across tools.
- localStorage persistence across browser sessions (privacy/stale-deck tradeoff).
- "Restored your last deck — clear" affordance (rejected for v1: silent prefill chosen).
</deferred>

---

*Phase: 74-cross-tool-input-persistence*
*Context gathered: 2026-06-27 via interactive discuss*
