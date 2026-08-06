---
title: Deck Version Tracker
summary: Track a deck's evolution in a JSON history file you own, with snapshot notes, pair diffs, and an AI-ready evolution prompt.
order: 46
requires_flag: tool.deck-history.enabled
---

# Deck Version Tracker

The Deck Version Tracker (Deck History) page (`/deck-history`) turns a deck into a portable history file you own. Each time you load the current list, DeckFlow can append a new snapshot with an optional label and note, show the saved versions in order, diff any two versions, and build a prompt that explains how the deck has evolved over time.

## File-you-own model

DeckFlow does not keep the history on the server. The history lives in a downloadable JSON file that you can save anywhere, version in git, share, or archive with the rest of your deck notes.

A typical cycle is:

- Load a deck from a public URL or pasted list.
- Upload an existing Deck History JSON file, or start a new one.
- Add an optional label and note for what changed.
- Download the updated JSON file after the snapshot is appended.

If you come back later, upload that same file again and append the next version.

## Append, inspect, and diff

The page shows every saved snapshot in the file, including when it was captured and any note you attached to it.

- **Append** adds the current deck as the newest snapshot when the card list changed.
- **Inspect** lets you review the ordered history as a timeline of the deck's revisions.
- **Diff** compares any two saved versions so you can see which cards were added, removed, or had quantity changes between those points in time.

If the uploaded file already contains the same card list as the current deck, DeckFlow keeps the file valid and avoids adding a redundant duplicate snapshot.

## Hand-edit tolerance

The history file is meant to be readable and durable, not opaque. If you hand-edit notes, labels, or other non-structural fields, DeckFlow tolerates that and will normalize the file when it is loaded again.

If the file is still recognizable as a DeckFlow history file but some derived diff data is stale or missing, DeckFlow rebuilds that derived data from the saved snapshots instead of failing hard. If the file is not valid JSON or is not a DeckFlow history file, the page tells you that directly.

## AI evolution prompt

Once a history file has multiple snapshots, DeckFlow can generate an **evolution prompt** for ChatGPT or Claude. The prompt summarizes the saved progression and asks the AI to reason about how the deck's plan, curve, interaction, or win conditions changed across versions.

Use this when you want help answering questions like:

- How did the deck's plan shift over time?
- Which changes improved consistency or speed?
- Did recent edits move the deck toward a different bracket or play pattern?
- What themes are emerging from the cumulative card swaps?
