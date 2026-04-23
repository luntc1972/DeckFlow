---
title: DeckFlow Bridge Browser Extension
summary: Install the companion browser extension to import Moxfield decks directly.
order: 90
---

# DeckFlow Bridge Browser Extension

The DeckFlow Bridge browser extension lets DeckFlow fetch Moxfield decks from your logged-in browser session when the server path is blocked.

## Why it's needed

Moxfield's edge blocks requests from server / datacenter IP ranges. When that happens from a web host, DeckFlow falls back to Commander Spellbook's public Moxfield helper to load the deck, but that fallback does not return card printings, set codes, collector numbers, author tags/categories, or sideboard/maybeboard entries. With the extension installed, the browser fetches the deck directly from your logged-in Moxfield session and submits it through the normal form flow — preserving full metadata.

## Install

1. Open `/extension-install.html` (linked from the Moxfield URL hints in the app).
2. Download the ZIP from that page.
3. Unzip it locally.
4. Load it unpacked via `chrome://extensions` or `edge://extensions` (enable Developer mode first).
5. Open the extension's Options page and add your DeckFlow origin to the allowed-origin list.

The extension only responds on origins you have explicitly allowed in its options — that is the default security posture.

## What it contains

- `deckflow-bridge.js` — the optional DeckFlow web-app bridge.
- `options.html` / `options.js` — the allowed-origin list manager.
- `background.js` — handles the cross-origin Moxfield API requests.

Mobile browsers are left on the normal server / fallback path and are not prompted for the extension.

## Full metadata alternative

If you can't install the extension, copy the Moxfield deck export text and paste it into DeckFlow's deck input directly. That path continues to work from anywhere and preserves all metadata.
