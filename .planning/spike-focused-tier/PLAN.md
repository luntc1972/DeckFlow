# Spike: Manabase "Focused" (mid-power) tier

Branch: `spike/manabase-focused-tier` (worktree `../deckflow-focused-tier`, off `main` @ f43198c9)

## Goal

Add a third manabase analysis mode, **Focused (mid-power)**, sitting between Casual
and cEDH. It differs from Casual on exactly **one axis**: the color-support
reliability threshold is **85%** (Casual 80, cEDH 88). Everything else — land
target, castability table, plain-language verdict, ramp/draw budget — behaves
**identically to Casual**. No cEDH-only lens (ritual credit/burst, keep-shapes,
interaction lens, plan-presence, baseline range) turns on in Focused.

Exposure is gated behind a new feature flag `analysis.manabase.focused-tier`,
seeded **OFF**. With the flag off the app is byte-identical to today (radio hidden;
a Focused POST falls back to Casual).

### Why (grounded in measurement)

A 9-deck pull-through (committed fixtures, both modes, prod flags held constant)
showed the mode axis is dominated by the **color-reliability threshold**, not land
count: Stale Brago dropped Workable→NeedsWork purely from the 80→88 bar despite its
land count *improving*. The two modes sit far enough apart (undersupported-card
counts roughly double: Army-now 5→18, Brago 3→9) that an 85% midpoint lands on
genuinely distinct guidance — not false precision. Land target is intentionally
left on the Casual curve because the cEDH land path (flat-28 `CedhDisabledFloor`
in prod) is a clamp with artifacts (low-curve decks go *up*), not a continuum worth
interpolating.

## Non-goals

- **No** wiring of `BracketClassifier` into manabase (decided: explicit radio, not auto).
- **No** change to Casual or cEDH numbers/behavior.
- **No** land-target changes for any mode.
- **No** new cEDH lenses in Focused.

## Design decision record

- Mechanism: **third radio** `Focused`, value between Casual/cEDH. (User pick.)
- Threshold: **85**. (User pick.)
- Flag default: **OFF** (spike).
- Focused inherits **all Casual display surfaces** (castability table, verdict, budget).

## Edit inventory (surgical)

### Core — `DeckFlow.Core/Manabase/`
1. `ManabaseMode.cs` — add `Focused` enum value (between `Casual` and `Cedh`) with
   xmldoc. Update the type-level `<summary>` to mention the mid-power profile.
2. `ManabaseAnalyzer.cs:16-17` — add `internal const int FocusedSupportThreshold = 85;`
   next to the Casual/Cedh consts.
3. `ManabaseAnalyzer.cs:1342` (`ColorThreshold`, `baseThreshold`) — 3-way:
   `Cedh→Cedh(88)`, `Focused→Focused(85)`, else `Casual(80)`. Preserve the existing
   Central-commander floor logic unchanged (it only *raises*, so still valid).
4. `ManabaseAnalyzer.cs:965` (colorless/snow finding threshold) — same 3-way.
5. `ManabaseModels.cs:1103` (health-verdict `supportThreshold`) — 3-way
   `Cedh→88, Focused→85, else 80`.
6. `ManabaseAnalyzer.cs:1791` `ModeLabel` — return `"Focused"` for the new value.
7. `ManabaseLabels.cs:22` — return `"Focused"` for the new value.
8. `ManabaseReportTextBuilder.cs:209` — widen castability-table gate from
   `mode == Casual` to `mode != Cedh` (Casual **or** Focused shows the table).

### Web — `DeckFlow.Web/`
9. `Services/Manabase/ManabaseAnalysisService.cs` — add flag key const
   `FocusedTierFlagKey = "analysis.manabase.focused-tier"`, read it, surface a
   `ShowFocusedTier` bool onto the result/viewmodel path. `:465` verdict/budget
   gate: widen `options.Mode == Casual` to `options.Mode != Cedh`.
10. `Services/FeatureFlags/FeatureFlagCatalog.cs` — register the flag, **seed OFF**,
    with description.
11. `Models/ManabaseViewModel.cs:122` (`ShowCastability`) — widen
    `report.Mode == Casual` to `report.Mode != Cedh`. Add `ShowFocusedTier` bool
    for the radio gate.
12. `Views/Deck/Manabase.cshtml:163-174` — add a third pill `Focused`, rendered only
    when `Model.ShowFocusedTier`. Update the help `<p>` copy to mention Focused.
13. `Controllers/ManabaseController.cs:204` — after `Enum.IsDefined` validation, if
    `request.Mode == Focused && !focusedTierEnabled` → coerce to `Casual` (flag-off
    safety: a hand-crafted POST cannot reach the Focused path when the flag is off).
14. `Models/ManabaseRequest.cs:23` — xmldoc note for the new value.

### Docs (behavior change — required)
15. `README.md` — manabase modes line: add Focused.
16. `DeckFlow.Web/Help/manabase.md` — document the mid-power tier + 85% bar.
17. `docs/manabase-analysis-rules.md` — add Focused threshold row.

## Tests

### Core (`DeckFlow.Core.Tests/Manabase/`) — xUnit, existing `CardFact` helper
- `Focused_ColorThreshold_Is85`: a color that clears 80 but not 85 (and not 88)
  → passes in Casual, flagged in Focused, flagged harder in cEDH. Assert on
  `WorstColorCastPercent`/`WeakestColor` or the sources-needed for a representative
  demanding card.
- `Focused_LandTarget_MatchesCasual`: same deck, `TargetLands` equal for Casual and
  Focused (proves land axis untouched); and **not** equal to cEDH.
- `Focused_ModeLabel_IsFocused`: `ModeLabel(Focused) == "Focused"` and
  `ManabaseLabels` likewise; Summary prefix reads `Mode: Focused`.
- `Focused_NoCedhLenses`: a deck that would trigger a cEDH-only lens (e.g. ritual
  credit / interaction lens) shows none of it in Focused (parity with Casual output
  on the relevant report fields).

### Web (`DeckFlow.Web.Tests/`)
- `ShowCastability_TrueForFocused`: viewmodel renders the castability table in Focused.
- Radio gate: view/model — `ShowFocusedTier` true only when flag on; the third pill
  renders only then. (Follow existing manabase view-render test pattern.)
- Controller fallback: `Mode=Focused` POST with flag **off** → coerced to Casual
  (assert the analyzed mode is Casual); with flag **on** → stays Focused.

## Side Effects Report

**Files/modules affected (direct):** the 17 files above (10 code, 3 docs, tests).

**Files/modules affected (transitive):** any consumer switching on `ManabaseMode`.
Grep confirms **all** sites use binary `== Cedh` / `== Casual` comparisons — there is
**no exhaustive `switch` expression** on the enum, so adding a value causes no
compile break and no silent fallthrough. The only behavioral risk is `== Casual`
*positive* checks silently excluding Focused; all four such sites are enumerated
(items 8, 9/`:465`, 11; item `Analyzer:1642` is already `!= Cedh`).

**Shared state touched:** none. Pure per-request analysis + one new feature-flag row
(seeded OFF; existing flag-seed machinery).

**External surfaces (DB/API/FS/config):** one new feature-flag key auto-seeded at
startup (OFF). No schema change, no migration, no API contract change (the POST
already carries `Mode`; `Focused` is a new accepted enum value gated by the flag).

**Contract changes:** `ManabaseMode` gains a member — additive. `ModeLabel`/labels
gain a case. No signature changes. No perf/ordering change.

**Tests requiring updates:** none expected to break (Casual/cEDH paths unchanged).
`Enum.IsDefined`-based validation already accepts new members. Any test asserting the
enum has exactly 2 members would need updating — grep for such before finishing.

**Backward compatibility risks:** none with flag OFF (radio hidden, Focused POST →
Casual). Persisted artifacts store the mode label string; a Focused artifact only
exists once the flag is on. No stored-data migration.

**Open questions / assumptions:**
- Assumes the Central-commander threshold floor (raises a commander's own colors to
  ≥88 even in Casual) should apply **unchanged** in Focused — i.e. a Central commander
  in Focused still floors at 88 for its colors while other colors use 85. Reasonable
  (Central is an override that only tightens). Flag for reviewer confirmation.
- Assumes Focused shows the same per-card castability table + plain-language verdict
  as Casual (user intent: "Focused ≈ Casual + tighter bar").

## Execution / review flow

1. This PLAN → Codex plan-review (gpt-5.5, read-only). Block on HIGH findings.
2. Execute via fable-foreman; Codex (gpt-5.4) implements with per-file LF preservation.
3. Claude reviews diff + EOL, runs Core + Web suites (Windows dotnet.exe), `/simplify`.
4. UI: render + screenshot the third radio (2 viewports × themes) with flag on.
5. Commit on `spike/manabase-focused-tier`; user pushes. Do not touch main.

## Review revisions (Codex gpt-5.5 plan-review — folded in, authoritative)

Verdict: APPROVE-WITH-CHANGES. Confirmed good: no exhaustive `switch` on the enum;
Central-commander floor composes (`max(85,88)=88` → Focused never looser than Casual);
no test asserts a 2-member enum. Must-fixes below supersede/extend the inventory:

**R1 (was HIGH — controller flag plumbing).** `ManabaseController` injects **no**
`IFeatureFlagCache` today and GET returns `new ManabaseViewModel()` directly
(`ManabaseController.cs:42`), so the service can't light the radio. Fix: inject
`IFeatureFlagCache` into the controller; resolve `analysis.manabase.focused-tier`
**once per action** and set `ShowFocusedTier` on **every** viewmodel path — GET
(`:42`), `/manabase/load`, commander-selection rerender, analyze (`:56+`), and the
download-error rerender. Make `NormalizeKnobs` take the resolved bool (or read it on
the instance) and coerce `Focused→Casual` when the flag is off. NOTE: `IsEnabled`
defaults **missing** keys ON — so the flag MUST be seeded (R4); never rely on default.

**R2 (MEDIUM — extra `== Casual` display sites).** Widen these to `!= Cedh` so
Focused inherits the Casual surface:
- `DeckFlow.Web/Views/Deck/Manabase.cshtml:694` — keep-shapes "curve coverage" block.
- `DeckFlow.Web/Models/ManabaseViewModel.cs:122` — already in inventory.
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:465` — already in inventory.
- `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs:209` — already in inventory.

**R3 (MEDIUM — CLI).** `DeckFlow.CLI/ManabaseCommandRunner.cs` `TryParseMode` will
accept `focused` once the enum exists; its verdict/budget gate at `:125`
(`== Casual`) must widen to `!= Cedh`, and CLI help at `DeckFlow.CLI/Program.cs:60`
must list `focused`. (Keep CLI behavior consistent with the web tier.)

**R3b (MEDIUM — swap prompt).** `DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs:53`
labels non-cEDH as "This is a Casual Commander deck". Give Focused its own sentence
(or drive the label from `ManabaseLabels.Mode(mode)`), so a user-selected Focused run
is not described as Casual in the LLM prompt.

**R4 (MEDIUM — real flag seeding).** The catalog only *describes* flags; seeding is SQL
in `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — add
`('analysis.manabase.focused-tier', FALSE)` to `PostgresSeedSql` (~:198) **and**
`('analysis.manabase.focused-tier', 0)` to `SqliteSeedSql` (~:245). Also register the
description in `FeatureFlagCatalog.cs`. Update `FeatureFlagStoreSeedTests` and
`FeatureFlagCatalogTests` to include the new key.

**R5 (LOW — inventory doc).** `DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs:106,204`
are `== Cedh` so Focused correctly inherits Casual (counters stay reactive) — no code
change, but cover it in `Focused_NoCedhLenses` parity.

**R6 (tests — expand).**
- Flag-OFF **byte-identity**: a handcrafted `Mode=Focused` POST with the flag OFF must
  render/serialize identically to `Mode=Casual` (rendered HTML + download text where
  practical), proving zero exposure.
- Add Focused coverage for the **colorless/snow** threshold path
  (`ManabaseAnalyzer.cs:965`) and the **health-band** threshold (`ManabaseModels.cs:1103`),
  not only the colored-source path.

## Acceptance criteria

- Flag OFF: app byte-identical to today; no Focused radio; Focused POST → Casual.
- Flag ON: Focused radio present; selecting it yields 85% color bar, Casual land
  target, Casual display surfaces, zero cEDH lenses.
- Core + Web suites green; no new warnings; EOL clean; README/help/docs updated.
