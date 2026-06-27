---
slug: primer-moxfield-style
created: 2026-06-27
type: quick
---

# Quick Task: Moxfield-style rich primer option

## Description
Add a "Primer style" output toggle to the deck-primer workflow: **Standard** (current
behavior) vs **Moxfield-style rich**. When Moxfield-style is selected, all three primer
prompt variants (ChatGPT / Claude / Gemini) append rich-format directives instructing the
AI to produce a Moxfield-paste-ready primer with:

- clickable table of contents (markdown anchor links)
- callout boxes (💡 Tips, ⚠️ Common Mistakes, 🎯 Tutor Priorities)
- collapsible sections for combo lines (`<details><summary>`)
- combo diagrams (ASCII/Markdown)
- tutor flowcharts (ASCII/Markdown)
- matchup tables (markdown tables)
- mana curve + game plan graphics (ASCII/Markdown)
- consistent formatting throughout

## Decisions (locked by user)
- Control = output-style toggle (Standard vs Moxfield-rich), section selection unchanged.
- Applies to all 3 platform variants.
- Graphics are AI-generated via prompt directives — NO new C# rendering of decklist data.

## Approach
1. New enum `PrimerOutputStyle { Standard, MoxfieldRich }`.
2. `DeckPrimerRequest.PrimerStyle` property (default Standard).
3. Each variant reads `request.PrimerStyle`; when MoxfieldRich, emit a rich OUTPUT FORMAT
   directive block (hand-edited per variant — decoupling rule, no shared helper).
4. View: radio fieldset bound to PrimerStyle in the section-selection step.
5. Controller: preserve PrimerStyle across every re-render reconstruction of DeckPrimerRequest.
6. Tests: request default + binding; per-variant rich directives present when MoxfieldRich,
   absent when Standard; Playwright toggle across themes + mobile.

## Files (fence)
- DeckFlow.Web/Models/PrimerOutputStyle.cs (new)
- DeckFlow.Web/Models/DeckPrimerRequest.cs
- DeckFlow.Web/Services/PromptBuilders/Primer/ChatGptPrimerPromptVariant.cs
- DeckFlow.Web/Services/PromptBuilders/Primer/ClaudePrimerPromptVariant.cs
- DeckFlow.Web/Services/PromptBuilders/Primer/GeminiPrimerPromptVariant.cs
- DeckFlow.Web/Controllers/DeckPrimerController.cs (only re-render reconstructions)
- DeckFlow.Web/Views/Deck/DeckPrimer.cshtml
- DeckFlow.Web.Tests/PrimerPromptVariantTests.cs
- DeckFlow.Web.Tests/DeckPrimerRequestTests.cs
- e2e Playwright spec for the toggle
