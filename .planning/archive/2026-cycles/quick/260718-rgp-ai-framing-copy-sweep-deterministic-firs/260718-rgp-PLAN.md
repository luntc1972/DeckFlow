# DeckFlow AI-Framing Copy Audit

*2026-07-18. Scope: every user-visible surface where AI/ChatGPT framing defines the site. Meta descriptions feed both `<meta>` tags and JSON-LD (single edit surface). Nothing in this audit changes behavior — copy only.*

## Framing principle

Lead with the deterministic thing the tool does; demote AI to the export mechanism. The Manabase and Bracket tiles already model this ("…No AI needed") — extend that voice.

**SEO tradeoff to decide:** "ChatGPT deck analysis"-type queries drive real traffic to the prompt tools. Recommendation: **scrub the keyword from identity surfaces** (site default, home, About, Help — where it defines the brand) but **keep it in tool-page descriptions** for tools that genuinely are prompt generators, just reordered so the deck value leads and ChatGPT is the mechanism.

## A. Identity surfaces (highest priority — brand definition)

| # | Location | Current | Proposed |
|---|----------|---------|----------|
| A1 | `_Layout.cshtml:3` site default description | "DeckFlow — Magic: The Gathering deck analysis for cEDH and Commander. Compare, analyze, and generate ChatGPT-ready deck prompts." | "DeckFlow — Magic: The Gathering deck analysis for cEDH and Commander. Deterministic manabase math, bracket checks, meta comparison, and deck history." |
| A2 | `Home.cshtml:10` home description | "Free Magic: The Gathering deck tools for Commander and cEDH. Sync, compare, and analyze decks, then generate ChatGPT-ready prompts in one paste." | "Free Magic: The Gathering deck tools for Commander and cEDH. Manabase math, bracket check, meta gap, and deck history — with optional one-paste AI prompt export." |
| A3 | `Home.cshtml` hero card ("Headline workflow: Analyze Your Deck — …copy the prompt, paste into ChatGPT/Claude…") | Hero literally headlines the AI workflow | **Decision needed.** Option 1: reword description to "load your deck, pick your questions, and get a structured review — one-paste workflow." Option 2 (stronger): make Manabase or Deck History the hero and demote Deck Analysis to a regular tile. |
| A4 | `About/Index.cshtml:4` | "…deck-analysis tools for cEDH and Commander players, with ChatGPT-ready prompt output. Credits and version." | "…free Magic: The Gathering deck-analysis tools for cEDH and Commander players — deterministic math first, optional AI prompt export. Credits and version." |
| A5 | `Help/Index.cshtml:4` | "…sync, compare, analyze, and generate ChatGPT prompts for Commander and cEDH decks." | "…sync, compare, and analyze Commander and cEDH decks, and export analysis prompts for AI platforms." |
| A6 | README intro section | Mirrors the ChatGPT-first framing | Align with A1 voice; one-paragraph edit. |

## B. Tool meta descriptions (keep keyword, reorder value-first)

| # | Location | Current | Proposed |
|---|----------|---------|----------|
| B1 | `DeckAnalysis.cshtml:9` | "Generate a ChatGPT-ready analysis prompt for your Commander deck. Paste an Archidekt or Moxfield deck and get a structured AI review in one round-trip." | "Get a structured review of your Commander deck — strengths, weaknesses, and upgrade paths. One-paste prompt workflow for ChatGPT or Claude." |
| B2 | `DeckComparison.cshtml:8` | "Compare two Commander decks side by side and generate a ChatGPT-ready prompt to decide which build is stronger. Built for cEDH deck-builders." | "Compare two Commander decks side by side — exact card diffs plus a one-paste analysis prompt to decide which build is stronger. Built for cEDH deck-builders." |
| B3 | `DeckPrimer.cshtml:10` | "Turn your Commander decklist into a ChatGPT-ready primer prompt covering strategy, lines, and key combos." | "Build a primer for your Commander deck — strategy, lines, and key combos — as a one-paste ChatGPT prompt." |
| B4 | `JudgeQuestions.cshtml:4` | "Turn a tricky Magic: The Gathering rules interaction into a ChatGPT-ready judge prompt." | "Resolve a tricky Magic: The Gathering rules interaction — links to real judge chat, with a ChatGPT judge prompt as backup." (matches the tile's existing honest ordering) |
| B5 | `CedhMetaGap.cshtml:8` | Already value-first ("Find what your cEDH deck is missing…"), ChatGPT in sentence 2 | Keep; optional soften "Generates a ChatGPT-ready gap-analysis prompt" → "with a one-paste gap-analysis prompt." |
| B6 | Manabase, Bracket, DeckSync, DeckConvert, CardLookup, MechanicLookup descriptions | No AI mention | No change — already the model voice. |

## C. "Crawled" language (adjacent optics fix, same sweep)

| # | Location | Current | Proposed |
|---|----------|---------|----------|
| C1 | `SuggestCategories.cshtml:11` | "…drawn from crawled deck history." | "…drawn from DeckFlow's category knowledge base of public deck data." |
| C2 | `CommanderCategories.cshtml:10` | "…derived from crawled deck history." | "…derived from aggregated public deck data." |
| C3 | `ContentKb/Index.cshtml:8` | "…creator videos distilled for serious…deck-builders." | "…creator videos distilled — every entry credited and linked to its source video." (turns disclosure into the selling point) |

## D. Tile copy (`ToolRegistry.cs`) — minor

| # | Tile | Current | Proposed |
|---|------|---------|----------|
| D1 | deck-primer | "Generate a staged, ChatGPT-ready primer…" | "Build a staged primer for your deck's plan, lines, and key interactions — one-paste AI prompt." |
| D2 | judge-questions | "Get rules answers from real MTG judges 24/7, with a ChatGPT prompt generator as backup." | Keep — already the right order. |
| D3 | deck-history | "…generate an AI prompt about how the deck has grown." | Keep — AI already last. |
| D4 | manabase / bracket | "…No AI needed." | Keep — the voice anchor. |

## E. New page: AI methodology disclosure

Add one Help topic ("How DeckFlow uses AI — and where it doesn't"):

- Analysis tools (Manabase, Bracket, Sync, Convert, lookups) are pure deterministic math — no AI in any result.
- Prompt tools generate text **you** choose to paste into an AI platform; DeckFlow never runs AI on your behalf or publishes AI output as site content.
- Knowledge Base entries are LLM-distilled summaries of credited creator videos, each linked to its source.
- Category knowledge is aggregated from public deck data (no AI classification).

Link it from the footer or About. This page is the single biggest trust move — it converts the framing weakness into a transparency asset and pre-answers the Reddit question before it's asked.

## Out of scope (functional copy, fine as-is)

`_AiSelector` platform radio, "Copy prompt for ChatGPT" buttons, in-tool step copy, `AnalysisQuestionCatalog` — these describe the mechanism at point of use, which is honest and expected.

## Execution plan on approval

One quick-task branch, Codex-implemented: A1–A6 + B1–B5 + C1–C3 + D1 are string edits (~12 files); E is one new Help markdown + nav entry. A3 hero swap is the only structural decision — pick option 1 or 2. Tests: description strings appear in SEO/render tests — expect a handful of assertion updates.
