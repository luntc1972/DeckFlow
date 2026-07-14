---
status: complete
commit: c84403f1
---
# Summary — Arena-format paste hardening

Shipped on branch `quick/arena-paste-hardening` (worktree), commit `c84403f1`.

- MoxfieldParser: About label ignored; "Name <deckname>" ignored in pre-entry preamble only (GeneratedRegex idiom); `SB:` = entry-local sideboard + explicit board marker (exempt from trailing-commander promotion — pinned by test).
- Loader message + Analysis/Convert/Sync/Comparison hints + 5 help pages + README name MTG Arena.
- Verified: Core 1425/0, Web 1371/0, live E2E (About/Name+SB: paste → full packet, commander detected), screenshots 2 pages × 2 viewports, EOL clean, format gate clean, /simplify applied (5 fixes; skips noted below).
- Follow-up candidates (from /simplify, deliberately out of scope): shared accepted-formats constant/partial (3 phrasings across 9+ files, DeckPrimer/CedhMetaGap hints still generic); SB: helper sharing if ArchidektParser ever needs .dec.
- MERGE PENDING: main repo dir held by concurrent MBGAP-09 session — ff `quick/arena-paste-hardening` → main after that lands (files disjoint from their manabase set).
