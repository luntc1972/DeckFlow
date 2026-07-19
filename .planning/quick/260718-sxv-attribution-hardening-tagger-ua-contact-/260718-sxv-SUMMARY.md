---
quick_id: 260718-sxv
description: Attribution hardening — Tagger UA contact URL, About credits (EDHREC + Scryfall Tagger), point-of-use attribution
status: complete
completed: 2026-07-19
---

# Quick Task 260718-sxv: Summary

Closes risk-assessment fixes #2/#3 (except the EDHREC courtesy email, scheduled 2026-07-21).

## What changed (commit d538da6d)

- `HttpClientServiceCollectionExtensions.cs` — scryfall-tagger client UA: `DeckFlow/1.0 (+https://www.deckflow.gg)`; comment updated. Cloudflare BIC headers and SocketsHttpHandler untouched (load-bearing).
- `AboutController.cs` — credits +EDHREC, +Scryfall Tagger (8 total).
- `SuggestCategories.cshtml` — footnote after "Source used" panel linking Scryfall Tagger (reuses manabase-lens-note class).
- `Manabase.cshtml` — "Data from EDHREC" baseline badge now hyperlinks edhrec.com.
- Tests synced: `AboutControllerTests`, `ManabaseViewRenderTests`.

## Notes

- Kill-switch flag `service.scryfall-tagger.enabled` pre-existed (ScryfallTaggerLookupService gate, FLAG-04/D-11) — no work needed.
- Codex pass mangled indentation on an unrelated test line; restored by reviewer. Stale UAT web server held DeckFlow.Core.dll causing phantom MSB3027 build errors — killed PID, rebuilt clean.

## Verification

Build 0 errors; Web tests 1583/0 (16 skips); EOL clean (diff == ignore-whitespace diff); no Core files touched.
