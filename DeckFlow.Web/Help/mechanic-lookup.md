---
title: Mechanic Rules
summary: Look up the official WOTC Comprehensive Rules text for a keyword mechanic or rules term.
order: 55
requires_flag: tool.mechanic-lookup.enabled
---

# Mechanic Rules

The Mechanic Rules page (`/mechanic-lookup`) looks up the current official Wizards of the Coast **Comprehensive Rules** text for a keyword mechanic or rules term — no AI involved, the text is quoted straight from the rules document.

## How to use it

Type a keyword or rules term (for example `Prowess`, `Cascade`, or `Battle`) and look it up. The page returns:

- **Exact rules sections** — a term like `Prowess` returns its matching numbered rules section and summary.
- **Glossary terms** — a term like `Battle` resolves through the glossary; when the glossary points at a major rules section (e.g. `310`), the page returns the **full referenced section body**, not just the one-line glossary sentence or the section header.

The **Clear** button empties the saved input, the summary block, and the rendered rules text together.

## Where it fits

- The **Card Lookup** page (Single Card mode) detects keyword mechanics and ability words on a resolved card and shows their rules in a **Keyword Rules** panel — this page is the standalone version for looking up a term directly.
- The parsed Wizards rules document is cached in memory for 6 hours, so repeated lookups don't re-download the full rules text.
- This tool can be turned off by an administrator; when it is, this help topic is hidden too.
