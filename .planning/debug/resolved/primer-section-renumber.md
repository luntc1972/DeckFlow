---
status: resolved
trigger: Generated primer sections keep catalog absolute numbers (start at 3, skip to 8) instead of renumbering 1..N
created: 2026-06-27
updated: 2026-06-27
---

# Debug: primer-section-renumber

## Symptoms
- Generated deck primer numbered sections like `## 3. Archetype`, `## 4. Win Conditions`, `## 8. Verified Combos`.
- Expected sequential numbering starting at 1 (1, 2, 3, ...).

## Root Cause
All three primer prompt variants emitted `PrimerSectionEntry.Number` (the absolute 1-31
catalog/UI display value) into the prompt's section directives. When the user selected a
non-contiguous subset, the prompt instructed the AI to use those gapped absolute numbers,
and the AI faithfully reproduced them.

Sites:
- `ChatGptPrimerPromptVariant.cs:67`
- `ClaudePrimerPromptVariant.cs:74`
- `GeminiPrimerPromptVariant.cs:119,139,162,204` (four per-group blocks)

## Fix
Number by emission position, not catalog `Number`. Each variant fixed in place (no shared
helper, decoupling rule honored):
- ChatGpt/Claude: 1-based running index over `selectedSections`.
- Gemini: precompute an Id->ordinal map over only the groups Gemini emits (Identity,
  Gameplay, Matchups, Maintenance; Combos excluded so it consumes no number); each block
  looks up its sequential number, keeping numbering continuous across blocks.

`PrimerSectionEntry.Number` / `PrimerSectionCatalog` unchanged (still the UI/form value).

## Verification
- Regression tests added in `PrimerPromptVariantTests.cs` (ChatGpt/Claude/Gemini): assert
  sequential `1./2./3.` and absence of absolute numbers. Red-then-green confirmed.
- `dotnet build DeckFlow.sln -c Debug`: 0 warnings, 0 errors.
- `dotnet test DeckFlow.Web.Tests`: 878 passed, 12 skipped, 0 failed.
- Format-gate (changed lines): green.

## Files Changed
- DeckFlow.Web/Services/PromptBuilders/Primer/ChatGptPrimerPromptVariant.cs
- DeckFlow.Web/Services/PromptBuilders/Primer/ClaudePrimerPromptVariant.cs
- DeckFlow.Web/Services/PromptBuilders/Primer/GeminiPrimerPromptVariant.cs
- DeckFlow.Web.Tests/PrimerPromptVariantTests.cs

Branch: fix/primer-section-renumber (worktree ../deckflow-primer-renumber), uncommitted.
