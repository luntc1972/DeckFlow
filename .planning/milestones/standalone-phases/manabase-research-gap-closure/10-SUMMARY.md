---
phase: manabase-research-gap-closure
plan: 10
status: complete
completed: 2026-07-13
commits:
  - 26bc88d9 feat(manabase): capped castability table + display subset logic (gap-10)
  - 88616836 feat(manabase): result page UX polish (gap-10)
  - dac3b002 test(manabase): ux-polish e2e guards + README (gap-10)
executor: codex gpt-5.4 medium (cross-AI); Claude reviewed + committed
verifier: /simplify 2-agent pass (5 applied) + human checkpoint APPROVED 2026-07-12
---

# Plan 10 Summary — UX polish (research HIGH 1-3 + MED 4-7)

- Castability table capped (all sub-90% rows, min 10 / max 20) + "Showing the N hardest casts" summary + no-JS details expander; shared row-template fragment (markup stated once).
- Mobile long-name hard-clip fixed (wraps; Clive//Ifrit readable at 390px).
- Verdict narrative unified; cEDH copy mode-aware (dangling table ref gone); persistent mode chip; cEDH lens row full-width; h3 headings + On-this-page anchor nav rendered from one (id,label,show) list.
- Page heights: casual mobile 15,674→7,351px (−53%); casual desktop 5,661→3,994; cEDH 4,813→3,896 / 3,241→2,575.
- e2e: manabase-ux-polish.spec.ts (row cap/expander, cEDH copy, anchor nav, 390px readability, mode chip) 5/5; ALL manabase specs 78 passed live both viewports. Web 1362/0 (+217 filtered post-simplify), Core 1400/0. Themes spot-checked (Dimir/Selesnya).
- Checkpoint: user APPROVED screenshots (4 mode/viewport + 2 themes).
- /simplify: shared table fragment, chip-threshold constants, hoisted section gates (nav can't desync), ramp-details dedup, test-builder renames. Incident: Codex thread went unresponsive (capacity + empty reply) → fresh thread completed.
- LOW 8-10 from research remain backlog.
