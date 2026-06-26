---
title: Deck Primer
summary: Build a grounded AI prompt that turns your decklist into a pilot-facing deck primer.
order: 15
requires_flag: tool.deck-primer.enabled
---

# Deck Primer

The Deck Primer page (`/deck-primer`) builds a prompt you paste into ChatGPT, Claude, or Gemini to get back a **deck primer** — a structured, pilot-facing guide for playing a Commander deck. A primer can cover the commander's identity and role, win conditions and card choices, verified combo lines, gameplay sequencing and mulligan rules, matchup strategy, budget cuts and upgrade paths, and common-mistake reminders.

DeckFlow does not write the primer itself. It assembles a prompt grounded in your real decklist, live combo data, and metagame context, so the AI answers from facts instead of guessing. You pick which sections you want, copy the prompt, and paste it into your AI.

The workflow is three steps.

## Step 1 — Import the deck

Choose an **Input method**:

- **Paste text** — paste exported deck text (Moxfield bulk-editor output, Archidekt export, or plain text).
- **Use public deck URL** — paste a public Moxfield or Archidekt deck URL (for example `https://moxfield.com/decks/…` or `https://archidekt.com/decks/…`).

The deck is parsed and resolved. It must contain a commander and at least one mainboard card, or an error is shown. The chosen input method round-trips with the form, so it survives refreshes and step navigation.

## Step 2 — Choose bracket, AI platform, and sections

Configure the primer:

| Setting | Purpose |
|---|---|
| **AI platform** | ChatGPT, Claude, or Gemini. This selects which prompt variant Step 3 shows — the wording is tuned per platform. |
| **Target Commander Bracket** | Exhibition, Core, Upgraded, Optimized (default), or cEDH. The bracket controls which sections are available and which matchup/meta context is pulled in. |
| **Primer sections** | The parts of the primer you want the AI to write. |

Sections are grouped into five collapsible groups — **Identity**, **Combos**, **Gameplay**, **Matchups**, and **Maintenance** — with checkboxes for each section and a running "selected" count per group. Each section has a short "What this adds" note describing what good AI output for that part looks like.

The available sections depend on the bracket. Some are **cEDH only** (for example a cEDH-metagame matchup section), and some are **Brackets 1–4 only** (for example battlecruiser-politics guidance). The list updates as you change the bracket. If you leave the section choices alone, a sensible preset for the selected bracket is used.

Click **Generate Primer** to build the prompt. When building, DeckFlow:

- Loads and parses the deck and resolves the commander.
- Queries the **Commander Spellbook** API for verified combos and near-combos in the list.
- For cEDH, pulls current archetype names from **EDH Top 16** for the matchup targets.
- Counts functional roles (ramp, draw, tutors, interaction) from category knowledge to ground the deck summary.

## Step 3 — Generate and copy the prompt

Step 3 shows three things:

- **Suggested chat title** — for example `Deck Name | Deck Primer`. Rename your AI conversation to this before pasting, so the primer is easy to find later. A copy button is provided.
- **Deck summary** — mainboard/maybeboard/sideboard counts, commander, bracket and format, and the ramp/draw/tutor/interaction breakdown when it could be grounded.
- **Primer prompt** — the prompt to paste into your AI, with a copy button and an approximate size (for example `~2.5 KB`). If the prompt is large enough to risk exceeding the platform's paste limit, a caution appears suggesting you trim sections or paste in parts.

Copy the prompt, paste it into ChatGPT, Claude, or Gemini, and the AI returns the finished primer inside a single fenced `markdown` block, with sections in the order you selected.

## Saving and resuming a session

Use **Download session (.zip)** to save the current setup — deck source, bracket, AI platform, and selected sections — along with the generated prompt variants and the normalized decklist. Use **Upload & Resume** later to restore that setup from the `.zip`; the primer is regenerated on restore in case upstream combo or meta data has changed.
