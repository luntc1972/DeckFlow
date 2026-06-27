---
title: Knowledge Base
summary: Browse expert deck knowledge distilled from community videos, and copy any entry into your AI.
order: 100
requires_flag: tool.knowledge-base.enabled
---

# Knowledge Base

The Knowledge Base (`/content-kb`) is a browsable library of deck-building knowledge distilled from community Magic content. Each entry is a compact, AI-ready artifact derived from an expert video — a summary, key clips or takeaways, and tags — that you can read on the site or copy straight into ChatGPT, Claude, or Gemini as context for your own deck questions.

> The Knowledge Base is gated behind a feature flag. If it is turned off, visiting the page shows a short "Knowledge Base unavailable" notice instead.

## Browsing entries

The hub page (`/content-kb`) shows published entries as a grid of cards. Each card shows the entry **title** (a link to its detail page), a short description, and tag pills for its **source**, **bracket**, **archetype**, and **card-category** tags.

To narrow the list:

- **Search** — type in the search box to filter by entry title or source name.
- **Filters** — expand the Filters section and use the dropdowns to filter by **Source** (the creator/channel), **Archetype** (deck strategy), **Bracket** (power level), and **Card Category** (card role). Use **Clear filters** to reset.

Filtering and search happen instantly in the browser, so the list updates as you type and choose.

## Reading an entry

Click an entry's title to open its detail page (`/content-kb/{id}`). The page shows:

- The entry **title**.
- The **source** name, linked to the original video or article.
- The **published date** (or "Publication date unknown" when it isn't available).
- The primary **bracket** and **archetype** tags.
- The full artifact, rendered from Markdown — typically a summary, key clips or points, and tags.

## Copying an entry into your AI

A **Copy** button at the top of the article copies the clean artifact text to your clipboard. Paste it into ChatGPT, Claude, or Gemini to give your AI grounded, deck-relevant context before you ask it a question — for example, when analyzing a deck or asking how to play a given archetype.
