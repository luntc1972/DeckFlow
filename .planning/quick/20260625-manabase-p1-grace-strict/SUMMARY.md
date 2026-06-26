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
- Follow-up option: widen strict-P1 to the color-requirement probe if desired.
