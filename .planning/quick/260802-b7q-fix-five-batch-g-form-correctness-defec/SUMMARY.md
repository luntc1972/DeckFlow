---
status: complete
date: 2026-08-02
commits: ec5c3749, 918c0adc, 1c6941f6
branch: feat/ui-audit-batch-g
---

# Batch G — five form-correctness defects

Fixed all five defects from `.planning/todos/pending/2026-08-02-batch-g-form-correctness-defects.md`,
plus both sweeps the ticket demanded. These were wrong-behavior bugs, not polish: two caused
silent data loss, one made a shipped feature unreachable.

| ID | Defect | Fix |
|----|--------|-----|
| G1 | Enter/mobile-"Go" triggered the sticky download bar (4 tools) | `data-default-action` per step + keydown guard in `deck-sync.ts`; download button demoted to `type="button"` at runtime only |
| G2 | `IncludeCardVersions` dropped from every mobile POST | `desktop-only` moved off the control onto its explanatory copy |
| G3 | `/resolve` unreachable with JS enabled | JS-path conflicts panel is now a real form with a "Use" radio column |
| G4 | Bracket + Mana Base lost pasted deck text | `data-cache-key` added; Bracket gained `data-clear-cache` |
| G5 | Card Lookup 100-line cap was client-side only | Enforced in `DownloadCardLookupAsync`, short-circuits before Scryfall |

## Ticket corrections found during re-verification

The ticket warned its `file:line` references needed re-checking. Three were wrong or incomplete:

1. G5 cited `wwwroot/js/card-lookup.js` — gitignored build output. Real source is `wwwroot/ts/card-lookup.ts:429`.
2. Mana Base's sticky download is a **separate form** (`:1078`), so its G1 defect is purely the
   load-vs-analyze button ordering.
3. **Not in the ticket:** demoting the download button promotes the *upload* button
   (`DeckAnalysis.cshtml:120`) to default, and upload needs a native multipart submit so it cannot be
   demoted the same way. This is why G1 needed a routing guard rather than just a type change.

## Verification

- `dotnet build DeckFlow.sln` — 0 errors, 0 warnings
- `dotnet test DeckFlow.Web.Tests` — 2270 passed, 0 failed, 16 skipped
- Playwright — 54 passed (Batch G spec + cross-tool-deck-persistence + deck-analysis-mobile +
  bracket-smoke), desktop 1280px and mobile 390px
- **Mutation-tested both layers.** Cap constant 100→99 and 100→101 each broke exactly 2 unit tests.
  Removing the download demotion and unmarking Mana Base's default action broke exactly the 4 e2e
  tests covering them. Neither suite is vacuous.
- All verification re-run against the committed HEAD, not just the working tree.

## Deliberately out of scope

- **`DeckPrimer.cshtml:76`** — resolved by convergence, not by this batch. Batch A fixes that exact
  line (`data-cache-key="deck-primer"`), and both branches are now on one branch, so the G4 sweep is
  fully closed: every deck-input form carries a cache key.
- **Card Lookup's `.desktop-only` Card List mode** (`CardLookup.cshtml:19,76`). The ticket scopes this
  as a design decision — make it responsive, or show an explicit "available on desktop" affordance —
  not a defect. Still open.

## Ticket bookkeeping

Converged with `feat/ui-audit-batch-a` on 2026-08-02 (rebase, no conflicts) so a single UAT pass
covers both batches. The source ticket has been moved to `.planning/todos/completed/` and marked
COMPLETE.

## Note on commit granularity

Requested shape was one commit per defect. Not achievable: the four front-end defects overlap within
the same files (`deck-sync.ts` carries G1 and G3; `DeckAnalysis.cshtml` carries G1 and G2), so a
per-defect split needs partial-line staging. A first attempt using zero-context `git apply --cached`
produced commits whose intermediate states were syntactically broken — hunks landed at wrong offsets
— and was reset. Final shape is three file-clean commits.
