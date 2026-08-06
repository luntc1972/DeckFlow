---
title: MTG Decklist Converter
summary: Reformat a single decklist between Moxfield and Archidekt without an existing target deck.
order: 45
requires_flag: tool.convert.enabled
---

# MTG Decklist Converter

The MTG Decklist Converter (Convert Deck) page (`/convert`) reformats one decklist from Moxfield format to Archidekt format, or the other way round. Unlike **Moxfield–Archidekt Deck Sync**, there is no second deck and no diff — you paste one list (or a public URL), pick the direction, and copy the reformatted output straight into your deck builder's bulk-edit field. No AI is involved.

## Step 1 — Choose the direction and input

- **Convert from** — *Moxfield* or *Archidekt* (the format your list is currently in).
- **Convert to** — the format you want out. Pick the opposite platform to move a list across, or the same platform to normalize its formatting.
- **Input method** — *Paste text* or *Public deck URL*.

## Step 2 — Provide the deck

- **Paste text** — paste the exported deck text. Supported formats include Moxfield bulk-editor output, Archidekt export, and MTG Arena export.
- **Public deck URL** — paste a public Moxfield or Archidekt deck link (for example `https://moxfield.com/decks/…` or `https://archidekt.com/decks/…`). The Moxfield URL field supports the optional DeckFlow Bridge extension when a datacenter IP is blocked by Moxfield.

### Commander (Moxfield source)

When you convert *from* Moxfield, an optional **Commander** field appears. Moxfield exports don't always include a commander line, so if none is found in your import you can type the commander name here (with typeahead over legendary creatures and planeswalkers) and convert again to include it. If your export already names the commander, leave it blank. When a conversion finds no commander, the page shows a notice prompting you to fill this in and re-convert.

## Step 3 — Copy the converted deck

Click **Convert** and DeckFlow renders the reformatted list in a **Converted deck** panel with a **Copy** button. Paste it into the target builder's bulk-edit field. The **Clear** button empties the form.

## Notes

- The converted deck carries over to the other single-deck tools within the same browser tab (see **Deck Analysis** for how cross-tool carry-over works).
- This tool can be turned off by an administrator; when it is, this help topic is hidden too.
