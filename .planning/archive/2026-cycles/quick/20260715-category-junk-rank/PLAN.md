---
slug: category-junk-rank
status: complete
date: 2026-07-15
---

# Quick: Harden category junk filter + fix Suggest-Categories ranking

## Problem
Display-time category weeding still lets junk through, and the Suggest
Categories merged list looks alphabetical because cross-source agreement
(SourceCount) is almost always 1, so the alpha tie-break dominates.

## Approved design (do not re-decide)

### 1. `DeckFlow.Core/Reporting/CategoryFilter.cs` — extend `IsJunk`
Add rejections (display-time only; raw DB untouched). Keep every existing
rejection (blank, digit-start, len<=1, len>40, `?`/`!`/`...`, non-ASCII):
- **word count >= 5** — split on whitespace with `RemoveEmptyEntries`.
- **any ASCII digit anywhere** in the string (generalizes the digit-start check).
- **sentence punctuation** — contains `,` or `;`, or ends with `.`.
Hyphens, apostrophes, and `&` remain allowed.

### 2. `DeckFlow.Core/Reporting/CategorySuggestionReporter.cs` — `Merge` tie-break
- Per `MergeEntry` track `Authority` = MAX rank among contributing sources:
  Tagger=3, Exact=3, Inferred(cached)=2, Edhrec=1.
- Update `Authority` in `MergeSource` as each source contributes (max, not overwrite).
- New sort: `OrderByDescending(SourceCount).ThenByDescending(Authority)
  .ThenBy(DisplayLabel, OrdinalIgnoreCase)`.

### 3. `CommanderCategoryService` threshold — UNCHANGED (`>=3 decks OR >=5%`).

## Tests (DeckFlow.Core.Tests, xUnit)
- `CategoryFilterTests`: 5-word phrase = junk; 4-word = kept; digit-mid
  (`3-Drop`, `Turn 1`) = junk; comma/semicolon/trailing-period = junk;
  legit 2–4 word cats + hyphen/apostrophe = kept.
- `CategorySuggestionReporterMergeTests`: authority tie-break — same
  SourceCount, Tagger-sourced ranks above Edhrec-sourced; alpha only breaks
  equal-authority ties.

## Constraints
- Both pages benefit from `IsJunk` (`Merge` + `CommanderCategoryService` call it).
- Preserve LF line endings (`.gitattributes`).
- Codex codes (gpt-5.4 medium); Claude reviews + blind-verifies.
