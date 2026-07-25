---
slug: reconcile-ramp-draw
created: 2026-07-24
completed: 2026-07-24
status: complete
---

# Summary — Reconcile ramp & draw + docs sweep

## What shipped

**Draw (code, commit 413f178a):** promoted Manabase's you-anchored draw regex
to `DeckStatClassifier.MatchesYouCardDraw` as the single shared literal-draw
source. `IsDrawCard` = robust union (`MatchesYouCardDraw` OR investigate OR
connive) for Cut Lab + analysis role tallies. `ManabaseClassifier.IsYouCardDraw`
delegates to the shared regex (byte-identical), and clue/connive stay OUT of the
Manabase draw budget.

**Ramp (docs, commit 413f178a):** NOT unified. ADR
`docs/decisions/0003-ramp-classifier-divergence.md` records why DeckStat's broad
`IsRampCard` and Manabase's tuned ramp predicates intentionally differ, with
`// Why:` pointers at both sites.

**Docs sweep (commit 4652d7ea):** README changelog (dedup `### Unreleased`
header; add By-type/subtype search, DFC oracle popup, sharper role counts) and
`cut-lab.md` role-counting note — covering all Cut Lab commits since 2026.07.9.

## Verification

- Full suite green: Core 1628, Web 2018, Studio 426, 0 failures.
- **Manabase output byte-identical** — no byte-identity or Manabase-test drift,
  so the verify-then-pin resolved to no pin and no golden recapture needed.
- Build clean, 0 warnings; LF preserved on all touched files.

## Manabase impact

None. Land verdict and plan-presence both unchanged (proven by the suite).

## Follow-ups

None. Ramp reconciliation deliberately declined (documented in ADR 0003).
