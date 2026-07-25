---
slug: manabase-load-step
created: 2026-06-22
mode: quick
branch: feat/manabase-mulligan-accuracy
implementer: claude
---

# Quick: Manabase "Load deck" step before analysis

## Goal
On the mana-base page, give the user a **Load deck** step that resolves the deck and builds the
auto-detected reduced/alternative-cost list ("discounted cards") so they can review and edit those
overrides BEFORE running the (expensive) analysis. Today suggestions only appear AFTER a full
analyze run.

## Approach
- The cost suggestions come from `ManabaseClassifier.Classify` (after Scryfall resolve). The
  expensive part is the per-spell Monte-Carlo sim in `ManabaseAnalyzer.Analyze`. So "Load" =
  resolve + classify (suggestions) and SKIP the analyzer; "Analyze" = the existing full path.
- Two submit buttons on the same form: **Load deck** (primary, `formaction=/manabase/load`) and
  **Analyze Mana Base**. The overrides box already pre-fills from `Suggestions` and opens when
  `HasSuggestions`, so Load just needs to populate Suggestions + a "loaded" hint, no Report.

## Tasks
1. **Service** (`ManabaseAnalysisService`): extract shared resolve+classify into a private helper;
   add `LoadAsync(...)` returning a new `ManabaseLoadResult(InputSummary, Unresolved, ImportWarning,
   Suggestions)` (no sim). `AnalyzeAsync` reuses the helper. Move input validation into the helper.
2. **Controller** (`ManabaseController`): add `[HttpPost("/manabase/load")]` mirroring the analyze
   action's enum-normalization + error handling; render the view with `Suggestions`, `InputSummary`,
   `Unresolved`, `ImportWarning`, `Loaded = true`, NO Report.
3. **ViewModel**: add `bool Loaded`.
4. **View** (`Manabase.cshtml`): add the Load button (formaction + own busy text); show a
   "deck loaded — review the detected costs below, then Analyze" hint when `Loaded && !HasResult`.
5. **TS** (`deck-sync.ts`): let the submit `submitter` override the form-level `data-busy-*` so Load
   shows "Loading deck & detecting costs" instead of the analyze copy.
6. **Tests**: service `LoadAsync` returns suggestions w/o report; Playwright — Load button posts,
   surfaces the overrides box, Analyze still produces a report (desktop + mobile).

## Files (fence)
- DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs
- DeckFlow.Web/Controllers/ManabaseController.cs
- DeckFlow.Web/Models/ManabaseViewModel.cs
- DeckFlow.Web/Views/Deck/Manabase.cshtml
- DeckFlow.Web/wwwroot/ts/deck-sync.ts
- DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs
- DeckFlow.Web/e2e/manabase.spec.ts (or interactions.spec.ts)

## Constraints
- No new POST without antiforgery; keep `FeatureFlagGate`. Layout CSS in site-common.css only.
- Preserve existing analyze behavior + all current tests. Carve-outs + LF + changed-lines gate.
