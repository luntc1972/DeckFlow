# Phase 74 — Technical Research

**Researched:** 2026-06-27 (read-only investigation + web research)
**Status:** Complete

## RESEARCH COMPLETE

## Question answered
What do I need to know to PLAN cross-tool deck-input persistence well?

## Current-state findings (verified this session)

### Existing persistence = within-form only
Every deck tool preserves input ONLY across POST round-trips within its own form, via
ASP.NET Core model binding + Razor field echo (`value="@Model.Request.DeckUrl"`,
`<textarea>@Model.Request.DeckText</textarea>`). Each GET action builds a **fresh empty**
request, so navigating to a different tool (a GET) loses the deck. **deck-analysis shares
this gap** — it does NOT persist across tools.

### No shared abstraction exists
No `ISession` deck key, no cookie, no TempData, no query-string round-trip, no shared
service for "last entered deck." `PacketSessionCache` caches packet **output** (5-min TTL,
SHA-256 of inputs) — not input, not used for restore. Confirmed: nothing to reuse
server-side; this phase introduces the first cross-tool input-restore mechanism.

### sessionStorage precedent already in the codebase
Three TS modules already use `sessionStorage` for form/UI state — the pattern to mirror:
- `wwwroot/ts/category-suggestions.ts` (form-state prefix + key)
- `wwwroot/ts/card-lookup.ts` (`SINGLE_CARD_STATE_KEY`)
- `wwwroot/ts/content-kb.ts` (`FILTER_STORAGE_KEY`)
- `wwwroot/ts/site.ts` uses localStorage for theme (contrast example).

### Per-tool field shapes differ (the real complexity)
| Tool | Route | Deck-source fields |
|---|---|---|
| Analyze | /deck-analysis | `DeckInputSource` enum, `DeckUrl`, `DeckText` |
| Manabase | /manabase | `DeckInputSource` enum, `DeckUrl`, `DeckText` (+ DeckName) |
| cEDH Meta Gap | /cedh-meta-gap | `DeckSource` (single string = URL or text) (+ CommanderName) |
| Convert | /convert | `InputSource`, `DeckUrl`, `DeckText` (+ formats) |
| Deck Primer | /deck-primer | `DeckSource` (single string) |
The restore glue must map a canonical `{inputSource, deckUrl, deckText}` to each tool's
actual field IDs (two distinct shapes: split url/text vs combined single field).

## Approach decision (research verdict)
**Client `sessionStorage`, shared TS module.** Beats server `ISession` (RAM cap on 512MB
web tier, CSRF, cleanup), hidden-field round-trip (within-form only = the gap),
query-string (leaks URL + CSRF), TempData (single redirect). Microsoft app-state guidance:
for transient user-created data, browser storage is preferred over server session —
nothing to clean up if abandoned; `sessionStorage` safer than `localStorage` (no cross-tab
clobber). Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state

## Implementation shape for the planner
- New shared TS module under `wwwroot/ts/` exposing `getLastDeck()` / `setLastDeck()` over
  one sessionStorage JSON key; cap stored `deckText` size (skip-store over cap).
- Each in-scope tool's page script: on `DOMContentLoaded`, fill deck fields from the store
  ONLY if rendered empty (POST-echoed values win); on field change, write to the store.
- TS compiles via MSBuild (`tsconfig.json`, module:none); compiled `wwwroot/js/*.js` is
  gitignored — never commit `.js`.

## Testing
- Pure client TS → no xUnit logic seam unless a C# helper is added (none planned).
- **Playwright e2e is the real coverage:** enter deck on tool A → navigate to tool B →
  assert deck fields prefilled; assert typed-in field NOT overwritten; assert per-tab
  isolation. Run desktop + mobile across themes per repo rule.
- Verify TS compiles clean (`dotnet build` runs tsc).

## Risks / landmines
- Restore must run after server-rendered values land in DOM and respect fill-if-empty,
  else it clobbers POST round-trip values.
- Combined `DeckSource` tools (meta-gap, primer): map from `deckUrl` or `deckText` per
  stored `inputSource`.
- sessionStorage quota / disabled storage → wrap in try/catch, degrade silently.
