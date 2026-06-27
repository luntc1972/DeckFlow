---
slug: primer-moxfield-style
status: complete
created: 2026-06-27
completed: 2026-06-27
branch: feat/primer-moxfield-style
commit: 8f41fbc0
---

# Summary — Moxfield-style rich primer + Full cEDH option

## What shipped
A "Primer style" output toggle on the deck-primer workflow:
- **Standard** — existing clean markdown (output byte-identical to before).
- **Moxfield-style rich** — prompt asks the AI for a clickable TOC, 💡/⚠️/🎯 callout
  boxes, collapsible `<details>` combo lines, combo diagrams, tutor flowcharts, matchup
  tables, and ASCII/markdown mana-curve + game-plan graphics.
- **Full cEDH primer** — bracket-5 only. Rich formatting + forced full cEDH section
  coverage + cEDH-depth directives (fast mana / turn 1-3, stax navigation, free-interaction
  counts, win-by-turn windows, named-archetype matchups).

Applies to all three prompt variants (ChatGPT/Claude/Gemini), each directive block
hand-written per the ADR-0001 decoupling rule. Graphics are AI-generated via prompt
directives (no C# rendering).

## Implementation notes
- `PrimerOutputStyle` enum on `DeckPrimerRequest`.
- `DeckPrimerPacketService.NormalizePrimerOptions` centralizes effective-style logic:
  FullCedh→MoxfieldRich off the cEDH bracket; the same effective style + section set feed
  cache key, prompt build, and persisted request-context. Style round-trips through
  download/upload zips (`PacketArtifactStore`).
- Bracket-5 radio gated server-side + in `primer-selection.ts` (show/hide + fallback).
- Added `CommanderBracketCatalog.IsCedh` helper (drift reduction, from /simplify).

## Quality gates
- Reviewed (code) — Codex implemented, Claude reviewed each dispatch.
- /simplify — collapsed within-file rich-line duplication in all 3 variants, removed dead
  `.is-selected` class, added `IsCedh` predicate.
- UI review — 22/24; nits fixed (dead class, redundant double-help now mutually exclusive).
  Transition-on-reveal deferred (low/polish).
- Build 0 warnings / 0 errors. xUnit: 897 passed, 12 skipped, 0 failed.
- Live Playwright `primer-style-toggle.spec.ts`: 4 passed (desktop 1280×900 + mobile
  390×844; themes azorius + selesnya). Caught + fixed a test-only locator bug (getByRole
  excludes a11y-hidden elements).
- README + in-app help (`Help/deck-primer.md`) updated.

## State
Committed `8f41fbc0` on `feat/primer-moxfield-style` (worktree
`../deckflow-primer-moxfield`). NOT landed/pushed — awaiting user go-ahead to ff onto main.
