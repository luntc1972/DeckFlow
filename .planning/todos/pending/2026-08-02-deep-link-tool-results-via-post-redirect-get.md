---
created: 2026-08-02T21:52:20.187Z
title: Deep-link tool results via POST-redirect-GET
area: ui
files:
  - DeckFlow.Web/wwwroot/ts/cut-lab.ts:491
  - DeckFlow.Web/Views/Shared/_ShareBar.cshtml
  - DeckFlow.Web/Views/Deck/Manabase.cshtml:1078-1093
  - DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml:106-206
  - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml:96,107
  - DeckFlow.Web/Views/Deck/Bracket.cshtml:103-319
  - DeckFlow.Web/Views/Deck/DeckHistory.cshtml:194-209
  - DeckFlow.Web/Views/Deck/DeckSync.cshtml:45
---

## Problem

Batch D of the 2026-08-02 site UI audit (`.planning/ui-reviews/2026-08-02-site-ui-audit.md`).
Largest payoff and largest scope in the report.

**`pushState` / `replaceState` / `URLSearchParams` appear exactly once across all 27 TS modules:
`cut-lab.ts:491`.** No other tool puts any state in the URL.

Consequences, every tool:

- Results are not shareable. These tools get passed around in Discord and on reddit; a link to a
  Manabase verdict or a bracket classification currently sends the recipient an empty form.
- Refresh triggers a form-resubmission prompt, and browser Back is unsafe.
- `_ShareBar` shares `window.location`, so on a results page **it shares an empty tool**. The
  feature is actively misleading where it matters most.
- Filter state is lost: CedhMetaGap's four EDH Top 16 filters (`:106-206`) are POST-only, so
  "Kinnan, Top 4, 100+ players" cannot be bookmarked; ContentKb's filters live only in
  `sessionStorage` (`content-kb.ts:10,34-66`).
- Manabase's "Download analysis (.txt)" (`:1078-1093`) re-POSTs every deck field and **re-runs the
  whole Monte-Carlo server-side** purely because there is no addressable result to fetch.
- Full-page POSTs are used where a GET would serve: DeckHistory's Compare (`:194-209`) rebuilds
  the entire page to swap two dropdowns; DeckAnalysis (`:96,107`) keeps workflow position in a
  hidden `WorkflowStep` input and re-renders 1,166 lines of Razor per step.

Sub-problems that disappear once results are addressable:

- The implicit-submission bug (Enter downloads a zip) is a symptom of everything living in one
  giant form.
- Server error banners rendering thousands of pixels above the viewport after a failed step POST
  (`DeckAnalysis.cshtml:65-73`).
- The `sessionStorage` state-restore machinery in `card-lookup.ts:90-132` and the excluded-field
  list in `deck-sync.ts:520-527` exist to paper over the lack of a canonical URL.

## Solution

POST-redirect-GET, with the result addressable by URL. Two shapes, pick per tool:

**Cheap tools (deterministic, fast to recompute):** put the inputs in the query string and
re-render on GET. `/manabase?deck=…&mode=cedh&importance=central`,
`/cedh-meta-gap?commander=…&period=THREE_MONTHS&minSize=100`, `/bracket?deck=…&target=4`.
Bookmarkable, refresh-safe, SEO-visible.

**Expensive tools (AI round-trip, parsed JSON):** persist the parsed result under a short id and
redirect to `/deck-comparison/r/{id}` — Deck Comparison and Deck Analysis Step 3 fit here.

Then:
- Move `_ShareBar` into the result panel and let it share the real result URL.
- Have Manabase's download fetch the addressed result instead of re-running the analysis.
- AJAX the step submits where the panel swap is local, `history.replaceState`-ing `?step=N`.

Scope check before planning: this touches `ManabaseController`, `DeckPacketController`,
`BracketController`, `ContentKbController`, `DeckSyncApiController`, most deck views, and
`deck-sync.ts`. Well past the ~5-file threshold, so it needs a full side-effects report and a
phase plan, not a quick pass. Codex `terra`.

Worth splitting into at least two phases — cheap-tool query-string rehydration first (self-contained,
immediate user-visible win), short-id result persistence second (needs a storage decision:
in-memory cache vs Postgres table vs signed payload in the URL).
