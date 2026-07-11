# PLAN — P3: flag + surface + docs for manabase ritual burst

**Feature:** manabase ritual / one-shot burst mana. SPEC:
`.planning/captures/manabase-ritual-burst-mana-spec.md` (§3.4 flag, §4 P3 tests, §6 P3).
**Branch:** `feat/manabase-ritual-burst-mana` (P1 `f420d457`, P2 `22d1b3b1`, MDFC fix `885e9b61`).
**Prereqs already done:** classifier emits `ManabaseDeck.OneShots` UNCONDITIONALLY (P1). Sim
consumes them behind a Core `ritualBurst` bool, HARD-GATED to cEDH inside
`ManabaseAnalyzer.Analyze` (`ritualBurstActive = ritualBurst && mode == ManabaseMode.Cedh`).
So P3 is pure Web-side flag wiring + tests + docs. No Core change.

**Roles:** Claude wrote this plan → Codex plan-reviews → Codex executes → Claude reviews.
Codex: gpt-5.4 medium coding; full-access sandbox; LF endings (repo `.gitattributes`).

## Goal / invariant
Ship the flag **default OFF** (dark). Flag OFF ⇒ `ritualBurst=false` ⇒ sim omits OneShots ⇒
**byte-identical** to today at classifier, service, and report level. Flag ON only shifts
castability % **in cEDH mode** (Casual stays byte-identical via the existing gate). No new UI
block, no new I/O (OneShots already classified), no prompt-artifact copy change in this phase.
Do NOT flip the default — calibration + operator flip is a later step.

## Scope — files to touch

### Wiring (DeckFlow.Web)
1. **`Services/Manabase/ManabaseAnalysisService.cs`**
   - Add `public const string RitualBurstFlagKey = "analysis.manabase.ritual-burst-mana";` with
     an xmldoc: seeded OFF; credits instant/sorcery rituals (Dark Ritual etc.) as one-shot burst
     mana in the castability sim, **cEDH only** (Casual byte-identical); read fail-safe OFF; off =
     byte-identical. Place beside the other manabase flag-key consts (~line 190).
   - In `AnalyzeAsync`, near the other `IsFlagOn` reads (~236-244): `bool ritualBurst = IsFlagOn(RitualBurstFlagKey);`
   - Pass it into the `ManabaseAnalyzer.Analyze(...)` call (~277-281): add `ritualBurst: ritualBurst`
     (that overload already has the param, default false; the cEDH gate lives inside it).
   - Do NOT touch `ResolveAndClassifyAsync` (no flag-gated classification — OneShots are
     unconditional) and do NOT pass ritualBurst to `SimulateCompanion` (companions have no mode
     context; leave the companion path unchanged).

2. **`Services/FeatureFlags/FeatureFlagCatalog.cs`**
   - Add an entry after `["analysis.manabase.plan-presence"]` (before the closing `}` at ~96):
     `["analysis.manabase.ritual-burst-mana"] = "<operator description>"` — describe: credit
     instant/sorcery rituals (Dark Ritual, Rite of Flame, Cabal Ritual) as one-shot burst mana in
     the manabase castability sim; **cEDH mode only**; raises early-turn cast % for ritual-fuelled
     lists; land count / color counts unchanged; off = byte-identical output.

3. **`Services/FeatureFlags/FeatureFlagStore.cs`**
   - Add to `PostgresSeedSql` (before `('tool.primer.stale-flag', FALSE)` at line 230):
     `('analysis.manabase.ritual-burst-mana', FALSE),`
   - Add to `SqliteSeedSql` (before `('tool.primer.stale-flag', 0)` at line 266):
     `('analysis.manabase.ritual-burst-mana', 0),`
   - Fix the trailing commas so the value lists stay valid (the current last-before-stale entries
     have commas; insert the new row cleanly).
   - Do NOT add a `RenamedFlagKeys` entry — this key is brand-new, never shipped un-namespaced.

### Tests (DeckFlow.Web.Tests)
4. **`FeatureFlagCatalogTests.cs`** — add `analysis.manabase.ritual-burst-mana` to whatever the
   test asserts (known-keys set / count / non-empty description). Match the existing pattern.
5. **`FeatureFlagStoreSeedTests.cs`** — assert the new key seeds **OFF** (mirror how an existing
   OFF-seeded flag such as `analysis.command-zone-awareness` or `tool.bracket.enabled` is
   asserted; seed defaults come from the SeedSql blocks).
6. **`Manabase/ManabaseAnalysisServiceTests.cs`** — service-level behavior:
   - **Flag OFF ⇒ byte-identical:** analyze a cEDH deck that carries a ritual (classifier will
     populate `OneShots`) with the flag OFF, and assert the result equals the same analysis of a
     deck with no ritual influence — i.e. OneShots present but flag off changes nothing. Simplest
     robust assertion: flag OFF, mono-black cEDH deck holding a Dark Ritual → the tracked triple-
     black spell's cast % equals the flag-OFF baseline (no lift). (Look at how existing service
     tests set a flag on the injected `IFeatureFlagCache` fake and call `AnalyzeAsync`.)
   - **Flag ON + cEDH ⇒ lift:** same deck, flag ON, `Mode = Cedh` → tracked spell cast % strictly
     higher than flag OFF.
   - **Flag ON + Casual ⇒ suppressed:** same deck, flag ON, `Mode = Casual` → cast % equals flag
     OFF (gate). Direction assertions only, no exact percentages.
   - Reuse the existing service-test fixtures/fakes; a ritual enters via a decklist line for a real
     ritual (e.g. "1 Dark Ritual") so the classifier's `DetectOneShotBurstMana` fires — verify the
     resolver/fixture path the existing tests use to feed card oracle text, and pick a ritual that
     classifies (Dark Ritual: `{B}` → `Add {B}{B}{B}`).
7. **`Manabase/ManabaseFlagBaselineHarness.cs`** — if this harness enumerates each manabase flag
   and asserts flag-off parity, ADD `ritual-burst-mana` to its flag list so the off-path parity is
   guarded like the siblings. If its shape doesn't fit a pure-sim flag (no display block), leave it
   and rely on the service test above — note which you did.

### Docs
8. **`docs/manabase-analysis-rules.md`** — add a ritual / one-shot burst-mana rule: what qualifies
   (instant/sorcery, net-positive `Add`), the own-cost gate, no-persistence, cEDH-only credit,
   flag name + default OFF, land-target untouched. Match the doc's existing section style.
9. **`DeckFlow.Web/Help/manabase.md`** — a short user-facing note: cEDH decks' rituals now count
   toward early-turn castability when the beta flag is on; doesn't change land count.
10. **`README.md`** — release-notes line under the current version: ritual / one-shot burst mana
    (cEDH), behind `analysis.manabase.ritual-burst-mana` (default OFF).

## Verify
- Build `DeckFlow.Web` + `DeckFlow.Web.Tests` + Core: 0 warnings / 0 errors.
- Run manabase + flag tests: `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~Manabase|FullyQualifiedName~FeatureFlag"` and the Core `~RitualBurst` set. Report counts.
- **Byte-identical guard:** the existing manabase golden/harness tests MUST still pass unchanged
  (flag defaults OFF). If any existing golden shifts, STOP — that means the off-path is not
  byte-identical (a bug), do not re-baseline to hide it.
- Admin flags page: new flag appears, toggle OFF by default (manual/e2e note — not required to run,
  but confirm the catalog+seed make it show).

## Out of scope (later)
- Flipping the default ON (needs cEDH calibration on 3–5 known lists first — separate step).
- In-prompt-artifact ritual messaging.
- cEDH land-target floor re-eval (SPEC backlog B).

## Plan-review conditions (Codex gpt-5.5, APPROVE-WITH-CONDITIONS — folded)
- **C1 (MED, test validity):** service tests must feed a REAL ritual via the resolver fixture that
  preserves oracle text — use an `Oracle(...)`-style card with `OracleText = "Add {B}{B}{B}."` for
  Dark Ritual, NOT a `Spell(...)` stub (which blanks oracle → `DetectOneShotBurstMana` won't fire).
  Compare the SAME deck flag-off vs flag-on (never a different-deck baseline). Assert the flag-ON
  cEDH lift is strictly > 0 so a mis-classified ritual fails loudly (guards R1). Resolver path is
  confirmed viable (fake resolver → `ScryfallCardDataMapper` preserves OracleText → `CardFact`).
- **C2 (MED, docs):** `docs/manabase-analysis-rules.md` has a Feature Flag Catalog TABLE (~line 264)
  and a note stating manabase display/verdict flags default ON (~line 298). Add a
  `analysis.manabase.ritual-burst-mana` row (default OFF, cEDH-only) to the table AND reword the
  "default ON" note so it does NOT sweep in this dark-launched sim flag.
- **C3 (LOW, rollout):** the new key AUTO-SEEDS on app startup — `ON CONFLICT DO NOTHING` inserts a
  brand-new key into existing prod/dev DBs. So NO manual operator DB insert is owed; only note that
  already-running processes need a restart to pick up the new seed. (README/memory: nothing owed.)
- **C4 (LOW, catalog test):** add the ritual key to `FeatureFlagCatalogTests` as planned; do NOT do
  the larger "derive seeded keys from the store" refactor (out of scope) — just extend the inline data.

## Risks
- **R1:** a chosen "Dark Ritual" test fixture must actually classify as a OneShot — verify oracle
  text feeds through the service test's resolver, else the lift test is vacuous. Mitigate: assert
  in the test that flag-ON cEDH lift is > 0 (fails loudly if the ritual didn't classify).
- **R2:** seed-SQL comma/format error breaks startup seeding — build + FeatureFlagStoreSeedTests
  catch it.
- **R3:** off-path drift — guarded by the byte-identical golden run above.
