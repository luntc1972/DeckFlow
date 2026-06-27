# Phase 74 — Cross-Tool Deck-Input Persistence

**Status:** 🟡 SPEC — research done, not yet planned.
**Branch / worktree:** `feat/phase-74-cross-tool-input-persistence` (`../deckflow-phase74`), off `origin/main`.

## Goal

When a user enters a deck (public URL or pasted text) in one DeckFlow tool and then
navigates to another tool or page, the deck input is **not lost** — it is restored
automatically so they don't re-paste. Apply this uniformly across the deck tools.

## Problem / current state (verified 2026-06-27)

- Input is preserved **within a single tool's form** only, via ASP.NET Core model
  binding + Razor field echo (`value="@Model.Request.DeckUrl"`,
  `<textarea>@Model.Request.DeckText</textarea>`). Every GET action builds a *fresh
  empty* request (e.g. `DeckPacketController` GET deck-analysis L53-62; deck-comparison
  L67-76; cedh-meta-gap L81-90; `ManabaseController` GET L34-39; `DeckConvertController`
  GET L41-46; `DeckSyncController` GET L41-49).
- **No** shared "last entered deck" abstraction exists: no `ISession` deck key, no
  cookie, no TempData, no query-string round-trip, no shared service.
  `PacketSessionCache` caches packet **output** (5-min TTL, SHA-256 of inputs) — NOT
  input, and is not used for restore.
- **Misconception corrected:** deck-analysis does NOT persist across navigation. It has
  the same gap. So "make it work like deck-analysis" would not fix the problem; this
  phase is an *upgrade* applied to all deck tools (analysis included).

### Tools in scope (deck-source input)

From `Services/Tools/ToolRegistry.cs` + `Views/Shared/_WorkflowStepTabs.cshtml`:

| Tool | Route | Deck-source field(s) | Request model |
|---|---|---|---|
| Analyze | `/deck-analysis` | `DeckInputSource`, `DeckUrl`, `DeckText` | `DeckAnalysisRequest` |
| Deck Comparison | `/deck-comparison` | `DeckASource`, `DeckBSource` | `DeckComparisonRequest` |
| cEDH Meta Gap | `/cedh-meta-gap` | `DeckSource`, `CommanderName?` | `MetaGapRequest` |
| Mana Base | `/manabase` | `DeckInputSource`, `DeckUrl`, `DeckText`, `DeckName` | `ManabaseRequest` |
| Convert | `/convert` | `InputSource`, `DeckUrl`, `DeckText`, formats | `DeckConvertRequest` |
| Deck Sync | `/sync` | Moxfield + Archidekt url/text pairs | `DeckDiffRequest` |
| Deck Primer | `/deck-primer` | `DeckSource` | `DeckPrimerRequest` |

(Out of scope — no deck-source input: card-lookup, mechanic-lookup, judge-questions,
content-kb, commander-categories, suggest-categories.)

## Chosen approach (research verdict 2026-06-27)

**Client-side `sessionStorage`, shared TS module.** A small module holds the canonical
last deck source `{ inputSource, deckUrl, deckText }` (plus optional per-tool extras
like `deckName`, second-deck slot for comparison/sync). Each deck tool's page script:
- on load → if its deck-source fields are empty, prefill from the shared store;
- on input/change → write the current deck source to the store.

### Why (vs alternatives)

- **Fits the 512 MB web RAM cap** — zero server memory, no Postgres round-trips.
- **Survives full-page GET navigation** between tools (the actual gap).
- `sessionStorage` (not `localStorage`) → tab-independent, auto-clears on tab close,
  nothing to clean up server-side. Microsoft guidance: for transient user-created data,
  browser storage is preferred over server session
  (https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state).
- **Precedent already in repo:** `category-suggestions.ts`, `card-lookup.ts`,
  `content-kb.ts` already use `sessionStorage` for form/UI state — this generalizes the
  pattern to the deck-source field and shares one key namespace.

### Rejected

- Server `ISession` — RAM/Postgres cost, CSRF care, cleanup. ✗
- Hidden fields / POST round-trip — within-form only (this is the current gap). ✗
- Query-string — leaks deck URL, CSRF surface. ✗
- TempData — single redirect only. ✗

## Open questions for /gsd-plan-phase

- [ ] Single shared deck slot vs. named slots (comparison/sync need 2 decks; analysis 1).
      Proposal: one primary slot + tool-specific secondary keys.
- [ ] Restore policy: only prefill when target fields are empty? Add a "restored from
      your last deck — clear?" affordance, or silent prefill?
- [ ] Does pasted `DeckText` (can be large) belong in `sessionStorage`? Cap size; if
      over cap, store URL only / skip text.
- [ ] Should `DeckInputSource` radio (URL vs paste) also restore, and which wins if both
      present?
- [ ] Feature-flag gate? (flag OFF → no client store read/write, byte-identical
      behavior.) Likely `tool.cross-tool-deck-persistence` per the `tool.*` namespace.
- [ ] Tests: xUnit can't cover client TS; rely on Playwright e2e (enter deck on tool A,
      navigate to tool B, assert prefilled) across desktop+mobile + themes per repo rule.
- [ ] README update + workflow-tabs UX note.

## Constraints / repo rules to honor

- New/changed page → add xUnit (where logic) + Playwright e2e, verify desktop+mobile
  across themes.
- TS source in `wwwroot/ts/`, compiled `js/` is gitignored — never commit `.js`.
- Codex implements; Claude plans/reviews. Route PLAN.md through Codex review before
  execute.

## Source

User ask 2026-06-27 + read-only investigation (this session) + MS Learn app-state guidance.
