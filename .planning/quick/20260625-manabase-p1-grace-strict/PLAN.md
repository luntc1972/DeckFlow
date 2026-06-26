---
slug: manabase-p1-grace-strict
created: 2026-06-25
type: quick
---

# Adjust the P1 (turn-1) grace window in the mana-analysis castability sim

## Decision (user)
- Turn-1 spells get grace = **0** (a 1-drop must be castable exactly on turn 1),
  instead of the current uniform +1. Other turns keep +1.
- Ship **behind a new feature flag seeded OFF** (prod inert until toggled), like
  the other manabase castability flags.

## Current state
`CastabilitySimulator.GraceWindow(int turn) => 1` (`CastabilitySimulator.cs:659`)
is uniform +1. Called at two sites: the prefix-shuffle calc (`:226`) and
`SimulateGame` (`:491`). The stale comment at `:488-490` describes a
curve-scaled window that no longer matches the uniform impl.

## Change
New flag `manabase.p1-grace-strict`, threaded Web→Core like `colorAwareMulligan`:
1. `CastabilitySimulator`:
   - `GraceWindow(int turn)` → `GraceWindow(int turn, bool strictP1Grace)`:
     `return strictP1Grace && turn <= 1 ? 0 : 1;`
   - Add `bool strictP1Grace = false` to `Simulate(...)` (default false), thread
     to both `GraceWindow` calls (`:226` and `:491` via `SimulateGame`). Add the
     same param to the private `SimulateGame(...)` and pass it at its call (`:242`).
   - Add an internal test seam:
     `internal static int GraceWindowForTest(int turn, bool strictP1Grace) => GraceWindow(turn, strictP1Grace);`
   - Fix the now-stale `:488-490` comment to describe the uniform-+1 / strict-P1 behavior.
2. `ManabaseAnalyzer.Analyze(...)` and the internal helper(s) that reach the two
   `CastabilitySimulator.Simulate` calls (`ManabaseAnalyzer.cs:293` and `:652`):
   add `bool strictP1Grace = false` and pass through to `Simulate`.
3. `ManabaseAnalysisService.cs`: add
   `public const string P1GraceStrictFlagKey = "manabase.p1-grace-strict";`,
   resolve `bool strictP1Grace = IsFlagOn(P1GraceStrictFlagKey);`, and pass it to
   `ManabaseAnalyzer.Analyze(...)`.
4. Flag registration:
   - `FeatureFlagCatalog.cs`: add a `["manabase.p1-grace-strict"] = "..."` description.
   - `FeatureFlagStore.cs`: add `('manabase.p1-grace-strict', FALSE)` to the
     Postgres seed block AND `('manabase.p1-grace-strict', 0)` to the SQLite block.
   - `FeatureFlagStoreSeedTests.cs`: add
     `[InlineData("manabase.p1-grace-strict", false)]`.

## Tests
- New Core test (`DeckFlow.Core.Tests`, xUnit) for `GraceWindowForTest`:
  `(1,false)=1, (1,true)=0, (2,true)=1, (3,true)=1, (1, ...)` etc.
- Seed test updated (above).
- Flag default OFF → existing Avatar / health-band fixtures MUST stay green
  (no re-baseline). Run the full Core manabase suite to confirm.

## Out of scope
- No re-baselining fixtures (flag is OFF by default).
- No web/UI change beyond flag wiring; no README (internal flag, default OFF).

## Verify
- `dotnet build DeckFlow.sln` clean (0 new warnings).
- `DeckFlow.Core.Tests` manabase suite + the seed test green.
