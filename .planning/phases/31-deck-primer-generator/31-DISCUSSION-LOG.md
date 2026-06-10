# Phase 31 Discussion Log — Deck Primer Generator

**Date:** 2026-06-08 · **Mode:** discuss (default) · Human reference only (not consumed by downstream agents).

## Areas selected for discussion

User selected all 4 presented gray areas: Spike scope & gating, Combo grounding structure, Section↔bracket-preset UX, Gemini paste-cap policy.

## Q1 — PRM-01 spike gating

- **Options:** (a) Plan all now, spike gates execution only; (b) Spike first then plan builder plans; (c) Spike gates both, pause after spike.
- **Chosen:** (a) Plan all now; spike gates only execution. Builder plan carries both ranking branches; verdict selects at exec.
- **Note:** spike stays the first execution unit; verdict recorded in a decision doc the builder reads.

## Q2 — Combo grounding structure (PRM-05/08)

- **Options:** (a) Two separated fenced blocks + null disclosure; (b) Single combos section with inline labels.
- **Chosen:** (a) Known Combos (ground truth, do-not-speculate) fenced block + separate fenced speculative-synergies ask; null-Spellbook → explicit "treat all synergies as speculative" line.

## Q3 — Bracket change vs section toggles (PRM-03/04/10)

- **Options:** (a) Apply preset, preserve per-bracket custom toggles; (b) always reset to preset; (c) preset only on first load.
- **Chosen:** (a) Preset seeds first visit; per-bracket custom toggles restored from localStorage (keyed per bracket); bracket-scoped gating still enforced.

## Q4 — Gemini paste-cap policy (PRM-01/09)

- **Options:** (a) Defensive char-cap guard (trim lowest-priority sections + disclosure); (b) hard-gate Gemini behind flag; (c) decide after spike.
- **Chosen:** (a) Defensive char-cap guard mirroring GeminiAnalysisPromptVariant; threshold from the spike measurement.

## Deferred / scope-creep redirected

None raised.

## Claude's discretion (not asked)

- Service/registry/variant architecture (mirror analysis), PrimerAllowedNames-first ordering, `{ get; init; }` + round-trip tests — all carried forward as locked invariants, not re-litigated.
