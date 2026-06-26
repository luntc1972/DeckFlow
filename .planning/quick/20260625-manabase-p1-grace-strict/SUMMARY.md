---
slug: manabase-p1-grace-strict
status: complete
completed: 2026-06-25
---

# Summary — P1 grace window (strict turn-1) + clearer flag descriptions

Added `manabase.p1-grace-strict` (seeded OFF). When on, turn-1 (one-mana) spells
get grace = 0 in the castability simulator: a 1-drop must be castable exactly on
turn 1, instead of the uniform +1. Turns 2+ keep the +1 grace. Threaded
Web → Core like `colorAwareMulligan`: `ManabaseAnalysisService` →
`ManabaseAnalyzer.Analyze`/`BuildCastability` → `CastabilitySimulator.Simulate`/
`SimulateGame`/`GraceWindow`. The color-requirement probe (`SimColorCast`) is
pinned to the non-strict path, so the flag affects displayed per-spell
castability only, not Karsten color sizing.

Also rewrote all seven manabase feature-flag descriptions in `FeatureFlagCatalog`
into plain operator language (dropped `MQ-02`/`70-03b`/`composite-weakest`
jargon) so the /Admin/Flags page reads clearly.

## Implementation / review
- Code by Codex (gpt-5.4 medium) under a hard scope fence; grace diff reviewed
  by Claude (PASS). Descriptions rewritten by Claude.

## Verification
- `dotnet build DeckFlow.sln` clean (0 warnings, 0 errors).
- `DeckFlow.Core.Tests` manabase suite: 157 passed (incl. new `GraceWindowTests`).
- Flag/seed/catalog tests: 23 passed (seed InlineData + catalog guard).
- Flag OFF by default, so Avatar / health-band fixtures are unchanged (no re-baseline).

## Notes
- No UI/README change: internal flag, default OFF, admin-only copy.

## REVERTED 2026-06-25
After running 10 real decks off vs on, the team decided the uniform +1 grace is the
better default (matches real play + the Karsten/Salubrious Snail baselines; strict
never flipped a verdict, only added 1-2 pt pessimism). The intended refinement
(strict turn-1 only for ramp **dorks**) turned out un-implementable as-is: tap-for-mana
1-drops are flagged `IsManaSource`/`IsRockOrDork` and are EXCLUDED from the castability
rows (`ManabaseAnalyzer.cs:287,411`), so the grace window has no dork to attach to.
Decision: drop `manabase.p1-grace-strict` entirely. Reverted the flag + all wiring
(simulator/analyzer/service/seed/catalog entry/tests); kept the plain-language
rewrites of the OTHER flag descriptions. Uniform +1 grace restored
(`GraceWindow(turn) => 1`).

Operator note: the flag row was seeded into prod on the earlier deploy (idempotent
insert). Removing the seed line does not delete the existing prod row — it is now an
inert orphan (enabled=false, no catalog description). Delete it via /Admin/Flags or
SQL if tidiness is wanted; harmless otherwise.
