# Phase MBGAP-09: cEDH Castability Surface - Pattern Map

**Mapped:** 2026-07-13
**Files analyzed:** 9 (2 net-new: Web-side flag/description entries only; the rest are edits)
**Analogs found:** 9 / 9

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Core/Manabase/CastabilitySimulator.cs` (extend `SimulateGame`/`Simulate`) | service (pure-CPU sim) | transform (per-trial Monte-Carlo) | Same file's existing `hadUntappedT1`/`Turn1UntappedTrials` TAP-02 addition | exact (same file, same seam) |
| `DeckFlow.Core/Manabase/ManabaseModels.cs` (new interaction-lens record + reuse `PlanRole.Interaction`) | model (additive DTO) | CRUD (pure data) | `ManabasePlanPresence` / `ManabaseTapAnalysis` records | exact |
| `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` (compute lens, cEDH gate, thread into `ManabaseReport`) | service (orchestrator) | transform | `ComputeTapAnalysis` / ritual-burst `mode == ManabaseMode.Cedh` gate (lines 169, 179) | exact |
| `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` (new flag const + `IsFlagOn` read + thread option + `ShowXxx` on result) | service (Web orchestrator) | request-response | `TapAnalyzerFlagKey`/`RitualBurstFlagKey` + `ShowTapAnalyzer` wiring | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (new description entry) | config | CRUD | Existing `analysis.manabase.*` description entries | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (new seed row, both dialects, TRUE) | config/migration (seed SQL) | CRUD | `analysis.manabase.mulligan-eval` / `plan-presence` seed-ON rows | exact |
| `DeckFlow.Web/Models/ManabaseViewModel.cs` (`ShowCastability` mode-aware; new `ShowInteractionLens`) | model (view model) | request-response | Existing `ShowCastability` / `ShowTapAnalyzer` boolean properties | exact |
| `DeckFlow.Web/Models/ManabaseDisplay.cs` (new marker/gloss helpers for the interaction lens) | utility (pure presentation) | transform | `TapMarker` / `KeepableMarker` / `CastChip` | exact |
| `DeckFlow.Web/Views/Deck/Manabase.cshtml` (3rd lens, table column, mode-note removal) | component (Razor view) | request-response | `manabase-twolens` block (430-489) + tap/mulligan lens blocks (490-599) + capped castability table (222-238, 800-814) | exact |
| `DeckFlow.Web/wwwroot/css/site-common.css` (`manabase-twolens` 3-up responsive) | config (layout CSS) | transform (presentation) | `.manabase-twolens` / `.manabase-twolens--single` / 640px breakpoint | exact |
| `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` (new "Early interaction" block) | service (text formatter) | transform | `AppendTapAnalysisBlock` / `AppendMulliganEvaluationBlock` | exact |
| `DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs` (upgrade line 50-52 prose) | service (prompt formatter) | transform | Same file's cEDH-mode prose block (lines 48-53) | exact (in-place upgrade) |
| `DeckFlow.Web/Help/manabase.md` (new subsection) | doc | — | "Untapped-source (Tap) analyzer" / "Opening hand and plan presence" subsections | exact |
| `README.md` (behavior-change entry) | doc | — | Existing `analysis.manabase.ritual-burst-mana` / `tap-analyzer` changelog bullets | exact |

## Pattern Assignments

### `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — add per-trial "by-turn-3 holdable" bookkeeping

**Analog:** the file's own existing TAP-02 `hadUntappedT1` addition (same simulator, same technique — a new per-trial boolean/counter recorded inside the existing turn loop, no second simulation).

**Existing `Simulate` signature to extend** (`CastabilitySimulator.cs:244-254`):
```csharp
public static CardCastability Simulate(
    ManabaseDeck deck,
    int librarySize,
    SpellRequirement spell,
    int effectiveTurn,
    int genericReduction,
    int trials = DefaultTrials,
    bool useManaQuantity = false,
    bool colorAwareMulligan = false,
    bool gateRampOnCastable = false,
    bool ritualBurst = false)
```
`CardCastability.Turn1UntappedTrials` (`ManabaseModels.cs:242-248`) is the existing additive-counter precedent to copy for a new `ByTurn3HoldableTrials` (or similar) field — same doc-comment shape ("Additive — safe default 0...").

**Per-trial recording pattern** (`CastabilitySimulator.cs:1092-1233`, `SimulateGame`): `hadUntappedT1` is computed via `HasColorMatchedUntappedT1(landsOnBoard, rampOnBoard, pipReq)` gated to `currentTurn == 1`, evaluated **before** the `if (currentTurn < turn) continue;` early-exit so it always fires regardless of the spell's own on-curve turn:
```csharp
// CastabilitySimulator.cs:1223-1233
if (currentTurn == 1)
{
    hadUntappedT1 = HasColorMatchedUntappedT1(landsOnBoard, rampOnBoard, pipReq);
}

// From the effective turn onward, test castability; succeed on the first turn it lands.
if (currentTurn < turn)
{
    continue;
}
```
For D-06 ("by-turn-3 holdable" = at least one of turns 1-3), OR the same per-turn check across `currentTurn in {1,2,3}` (widen the `currentTurn == 1` guard to `currentTurn <= 3`), independent of the spell's own effective turn — this is a property of the SPELL's OWN pips, not of when it's cast on curve, matching D-06/D-07 ("raw availability").

**Helper to reuse as-is:** `HasColorMatchedUntappedT1` (`CastabilitySimulator.cs:1628-1648`) already does exactly the "any online land whose OnlineTurn <= N matches the spell's needed-color mask (or colorless)" check — generalize its `<= 1` to `<= turnLimit` or call it once per turn 1-3 and OR the results.

**Aggregation into the report — analog `ComputeTapAnalysis`** (`ManabaseAnalyzer.cs:1035-1081`): averages a per-row counter across non-commander rows, divided by trial budget:
```csharp
int turn1Pct = avgRows.Count > 0 && defaultTrials > 0
    ? (int)Math.Round(100.0 * avgRows.Average(r => r.Turn1UntappedTrials) / defaultTrials)
    : 0;
```
Copy this shape for the by-turn-3 holdable percent per interaction spell (this one is PER-SPELL, D-04's "list per-spell rows", so it likely stays a plain `int` percent on each row rather than a deck-level average — see `ManabasePlanPresence` for the per-role deck-level aggregate pattern below).

---

### `DeckFlow.Core/Manabase/ManabaseModels.cs` — new additive lens record

**Analog:** `ManabasePlanPresence` (`ManabaseModels.cs:1387-1423`) — closest match because it is ALSO a `PlanRole`-keyed, deck-level Monte-Carlo read with per-role/per-spell rows, a headline percent + band, and a nullable slot on `ManabaseReport` populated only when applicable spells exist.

```csharp
// ManabaseModels.cs:1387-1423 (structure to mirror)
public sealed record ManabasePlanPresence
{
    public required int PayoffPercent { get; init; }
    public required string PayoffBand { get; init; }
    public required int PlanPresencePercent { get; init; }
    public required string Band { get; init; }
    public required IReadOnlyDictionary<PlanRole, int> RolePercents { get; init; }
    public int KeepableTrials { get; init; }
    public IReadOnlyList<OpeningHandSample> RepresentativeOpeners { get; init; } = Array.Empty<OpeningHandSample>();
}
```

**Carve-out reminder (CLAUDE.md):** every new property MUST be `{ get; init; }`, never `{ get; }` — System.Text.Json silently drops get-only properties in .NET 9+ (this exact regression broke `EdhTop16Client` before; `CarveOutGuard` test enforces it).

**Slot on `ManabaseReport` — analog** (`ManabaseModels.cs:1179`):
```csharp
/// <summary>
/// TAP-01/TAP-02: tap-quality metrics (...), or null when not computed. Additive — defaults null so
/// existing serialization/tests are unaffected. Populated by <see cref="ManabaseAnalyzer"/> when the
/// tap-analyzer flag is on.
/// </summary>
public ManabaseTapAnalysis? TapAnalysis { get; init; }
```
Add e.g. `public ManabaseInteractionLens? InteractionLens { get; init; }` following the identical doc-comment shape, populated cEDH-only per D-15.

**Per-spell row shape** — reuse `CardCastability` directly (D-04 wants per-spell rows; `CardCastability` already carries `Name`, `ManaValue`/`OnCurveTurn`, and would need only the new by-turn-3-holdable percent added, either as a new field on `CardCastability` itself, following the `Turn1UntappedTrials` precedent (`ManabaseModels.cs:242-248`), or as a small paired record `{ Name, ByTurn3HoldablePercent }` built from the qualifying rows in `ManabaseAnalyzer`).

---

### `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` — compute + cEDH-only gate + thread into `Analyze`

**Analog 1 — cEDH-only gate pattern** (`ManabaseAnalyzer.cs:169, 179`, the ritual-burst/ritual-land-credit precedent):
```csharp
bool ritualLandCreditActive = ritualLandCredit && mode == ManabaseMode.Cedh;
...
// Ritual burst is hard-gated to cEDH: ... in Casual the credit is suppressed so the
// flag-on path stays byte-identical there. The simulator itself is mode-agnostic — the policy gate
// lives here where the mode is known.
bool ritualBurstActive = ritualBurst && mode == ManabaseMode.Cedh;
```
Copy this exact shape for `interactionLensActive = interactionLens && mode == ManabaseMode.Cedh` — the simulator computes the by-turn-3 metric unconditionally (mode-agnostic), and the analyzer gates its use to cEDH.

**Analog 2 — "no tags → null, byte-identical" pattern** (`ManabaseAnalyzer.cs:218-224`, plan-presence):
```csharp
// Plan-presence: a dedicated single deck-level pass, run ONLY when the deck carries plan-tagged
// spells (the Web layer tags them only when the plan-presence flag is on). No tags → null, so
// the flag-off path adds no sim and stays byte-identical.
ManabasePlanPresence? planPresence = deck.Spells.Any(s => s.PlanRoles != PlanRole.None)
    ? CastabilitySimulator.SimulatePlanPresence(...)
    : null;
```
D-01 qualifying-spell filter is `s.PlanRoles.HasFlag(PlanRole.Interaction) && <effective MV> <= 2` — filter on `deck.Spells` (post cost-override substitution, since `deck = ApplyCostOverrides(...)` already ran at `ManabaseAnalyzer.cs:163` before this point) so D-02's "after the override machinery" requirement is satisfied for free.

**Analog 3 — deriving lens data from already-computed castability rows (no second sim)**, `ComputeTapAnalysis` (`ManabaseAnalyzer.cs:1035-1081`) and `ComputeMulliganEvaluation` (`ManabaseAnalyzer.cs:1090-1163`): both are private static helpers called once inside the `Analyze(...)` return-object construction, reading `castability` (already built at `ManabaseAnalyzer.cs:184`) rather than re-simulating. If the by-turn-3 metric can be read off the same per-spell `castability` rows (once `CastabilitySimulator.Simulate` is extended to also populate the new counter — see the `CastabilitySimulator.cs` section above), follow this exact "derive, don't re-simulate" shape:
```csharp
private static ManabaseTapAnalysis ComputeTapAnalysis(
    ManabaseDeck deck,
    IReadOnlyList<ColorSourceFinding> colorFindings,
    IReadOnlyList<CardCastability> castability,
    int defaultTrials)
{ /* ... averages a per-row counter divided by defaultTrials ... */ }
```

**Wiring into `Analyze`'s return** (`ManabaseAnalyzer.cs:226-266`) — add the new field next to `TapAnalysis`/`MulliganEvaluation`:
```csharp
TapAnalysis = ComputeTapAnalysis(deck, findings, castability, CastabilitySimulator.DefaultTrials),
MulliganEvaluation = ComputeMulliganEvaluation(deck, castability, CastabilitySimulator.DefaultTrials, planPresence),
// new:
InteractionLens = interactionLensActive
    ? ComputeInteractionLens(deck, castability, CastabilitySimulator.DefaultTrials, CedhSupportThreshold)
    : null,
```

**D-08 headline constant to reuse** — do NOT fork:
```csharp
private const int CedhSupportThreshold = 88;  // ManabaseAnalyzer.cs:17
```

**New bool parameter on `Analyze(...)`** — add to the existing long parameter list (`ManabaseAnalyzer.cs:143-155`) following the exact style of `ritualBurst`/`ritualLandCredit` (XML `<param>` doc explaining flag-off byte-identical behavior, default `false`).

---

### `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — flag key, `IsFlagOn` read, thread into `Analyze`, `ShowXxx` on result

**Analog — flag key const + XML doc** (`ManabaseAnalysisService.cs:213-225`, the ritual-burst/ritual-land-credit precedent):
```csharp
/// <summary>
/// Ritual-burst flag key: seeded OFF. Credits instant/sorcery rituals (...) as one-shot burst mana in
/// the castability sim, cEDH mode only. Read fail-safe OFF; off = byte-identical output.
/// </summary>
public const string RitualBurstFlagKey = "analysis.manabase.ritual-burst-mana";
```
New: `public const string CedhInteractionLensFlagKey = "analysis.manabase.cedh-interaction-lens";` — per D-15 the doc comment differs from the analog in ONE respect: **seeded ON** (not OFF), so word it like the `TapAnalyzerFlagKey`/`MulliganEvalFlagKey` doc comments instead (`ManabaseAnalysisService.cs:193-204`, "seeded OFF" language flipped to "seeded ON").

**Read + thread pattern** (`ManabaseAnalysisService.cs:296-299, 378-385`):
```csharp
bool ritualBurst = IsFlagOn(RitualBurstFlagKey);
...
ManabaseReport report = ManabaseAnalyzer.Analyze(
    resolved.Deck, options.Mode, options.CommanderImportance, options.CostOverrides,
    useManaQuantity, colorAwareMulligan, gateRampOnCastable: true,
    ritualBurst: ritualBurst,
    ritualLandCredit: ritualLandCredit,
    ...);
```

**Result-side `ShowXxx` gate** (`ManabaseAnalysisResult` record, `ManabaseAnalysisService.cs:115-121`, plus the two assembly sites at `ManabaseAnalysisService.cs:332-334` and `430-432`):
```csharp
public bool ShowTapAnalyzer { get; init; }
```
Add `ShowInteractionLens` (or reuse the flag bool directly since D-09/D-10 render is cEDH-mode-conditioned in the view, not a separate distinct show-flag) — mirror both assembly sites (the early-return "commander selection required" branch AND the normal-path return) since `ShowTapAnalyzer`/`ShowMulliganEval`/`ShowPlanPresence` are set in BOTH.

**IMPORTANT — D-09 also changes `ShowCastability`,** currently in `ManabaseViewModel.cs`, not this file — see that section below.

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` + `FeatureFlagStore.cs` — flag seeding precedent (default-ON batch)

**Analog — `FeatureFlagCatalog.cs` description dict entry** (lines 99-110, `mulligan-eval` / `plan-presence`, both seeded ON):
```csharp
["analysis.manabase.mulligan-eval"] =
    "Show the opening-hand / mulligan evaluator block on the mana base page and its paste " +
    "artifact - a keepable-hand band, London mulligan keep-depth process, and representative " +
    "openers with a per-play on-curve and has-a-plan read, all a heuristic consistency signal " +
    "derived from the existing simulation. Off = byte-identical output.",
```
New entry key: `"analysis.manabase.cedh-interaction-lens"` — description should state: cEDH-only, gates the "Early interaction" lens + full castability table exposure + the two prompt-artifact blocks, seeded ON, off = byte-identical (matches D-15's "kill switch" framing).

**Analog — `FeatureFlagStore.cs` seed SQL, BOTH dialects** (Postgres `TRUE`/SQLite `1`, lines 228-233 and 270-275):
```sql
-- Postgres (FeatureFlagStore.cs:228)
('analysis.manabase.mulligan-eval', TRUE),
('analysis.manabase.plan-presence', TRUE),
('analysis.manabase.ritual-burst-mana', FALSE),
```
```sql
-- SQLite (FeatureFlagStore.cs:270)
('analysis.manabase.mulligan-eval', 1),
('analysis.manabase.plan-presence', 1),
('analysis.manabase.ritual-burst-mana', 0),
```
Add `('analysis.manabase.cedh-interaction-lens', TRUE)` / `1` to BOTH blocks (must stay in sync — a `FeatureFlagCatalogTests` guard fails if a seeded key has no `Descriptions` entry). `ON CONFLICT (key) DO NOTHING` / SQLite equivalent preserves any operator override on re-bootstrap — do not touch that clause.

---

### `DeckFlow.Web/Models/ManabaseViewModel.cs` — mode-aware `ShowCastability` + new lens gate

**Current (Casual-only) gate to change per D-09** (`ManabaseViewModel.cs:106-110`):
```csharp
/// <summary>
/// True when the castability table should render: a report exists, it was run in Casual mode,
/// and it carries at least one castability row. cEDH hides the table (v1) and shows a note.
/// </summary>
public bool ShowCastability => Report is { Mode: ManabaseMode.Casual, Castability.Count: > 0 };
```
Becomes mode-aware: cEDH renders the table too, but ONLY when the new flag is on (kill-switch, D-15). Needs a new `bool ShowInteractionLens` or similar passed in from `ManabaseAnalysisResult` (mirror the existing `ShowTapAnalyzer`/`ShowMulliganEval` boolean properties at `ManabaseViewModel.cs:56-63`, which are populated from the analysis-service result in the controller — see `ManabaseAnalysisService.cs:430-432` for the assembly site pattern), e.g.:
```csharp
public bool ShowCedhInteractionLens { get; init; }
public bool ShowCastability =>
    Report is { Castability.Count: > 0 } report
    && (report.Mode == ManabaseMode.Casual || (report.Mode == ManabaseMode.Cedh && ShowCedhInteractionLens));
```

---

### `DeckFlow.Web/Models/ManabaseDisplay.cs` — marker/gloss helpers to copy

**Analog — met/short marker** (`ManabaseDisplay.cs:107-121`, `TapMarker`/`KeepableMarker`, both reuse the SAME `manabase-lens-met`/`manabase-lens-short` CSS classes — no new tokens):
```csharp
public static (string Css, string Marker) TapMarker(int untappedPercent)
    => untappedPercent >= 80
        ? ("manabase-lens-met", "✓")
        : ("manabase-lens-short", "⚠");
```
Copy this exact shape for the interaction-holdable badge, thresholded against `CedhSupportThreshold` (88) per D-08 (reuse the constant from Core via the report, do not hardcode 88 again in the Web layer if avoidable — pass it through or expose it).

**Analog — gloss constants** (`ManabaseDisplay.cs:30-49`, `KarstenSourceGloss`/`CastRateGloss`/`TapAnalyzerGloss`): plain-English one-liner `const string`, referenced from the view only when `showPlainLanguage` is true. Add `CedhInteractionLensGloss` alongside these, carrying D-07's raw-availability caveat ("assumes you hold mana open").

**Analog — capped-table helpers (D-11's "worst 5 + `<details>` expander")** (`ManabaseDisplay.cs:184-236`, `DefaultVisibleCastabilityCount` + `CastabilitySummaryText`): this IS the "capped-table mobile pattern from gap-closure plan 10" referenced in CONTEXT.md — it already implements exactly D-11's shape (N always-visible worst rows + a `<details>` "show all" expander with a summary sentence, never silent truncation — satisfies L2 from the efficacy findings). For the interaction lens's "worst-holdable 5 + expander," either reuse these helpers directly with a fixed count of 5 (bypass the `MinVisibleCastabilityRows`/`MaxVisibleCastabilityRows` clamp logic, which is tuned for the full castability table) or write a small dedicated `DefaultVisibleInteractionCount` / just hardcode `Take(5)` — the `<details>` markup pattern below is the reusable part.

---

### `DeckFlow.Web/Views/Deck/Manabase.cshtml` — 3rd lens, table badge column, mode-note removal

**D-09 — remove this block** (`Manabase.cshtml:820-823`):
```cshtml
else if (report.Mode == ManabaseMode.Cedh)
{
    <p class="mode-note manabase-castability-note">Castability view is available in Casual mode.</p>
}
```

**D-10 — extend `manabase-twolens` to a 3rd lens.** Current two-lens wrapper + anchor-nav wiring (`Manabase.cshtml:207-240, 430-489`):
```cshtml
var showRightLens = Model.ShowCastability && castRows.Count > 0;
var showLeftLens = report.ColorFindings.Count > 0;
...
(string Id, string Label, bool Show)[] resultNavItems =
[
    ("manabase-karsten-source-check", "Karsten source check", showLeftLens),
    ("manabase-simulated-cast-rate", "Simulated cast rate", showRightLens),
    ...
];
...
@if (showLeftLens || showRightLens)
{
    <div class="manabase-twolens @(!(showLeftLens && showRightLens) ? "manabase-twolens--single" : null)">
        @if (showLeftLens) { <section id="manabase-karsten-source-check" class="manabase-lens"> ... </section> }
        @if (showRightLens) { <section id="manabase-simulated-cast-rate" class="manabase-lens"> ... </section> }
    </div>
    @if (showLeftLens && showRightLens)
    {
        <p class="manabase-twolens-note">Read the two together: ...</p>
    }
}
```
Add `var showInteractionLens = Model.ShowCedhInteractionLens && report.Mode == ManabaseMode.Cedh && report.InteractionLens is not null;` plus a 3rd `resultNavItems` entry and a 3rd `<section id="manabase-early-interaction" class="manabase-lens">` inside the same wrapping `<div class="manabase-twolens ...">`. The single/dual/triple CSS modifier class needs a THIRD state — see the CSS section below. Each existing lens section's internal structure (`manabase-lens-label` → `manabase-lens-big` headline → `manabase-lens-row` per-item rows → `manabase-lens-note` caption → optional `manabase-lens-gloss`) is the shape to copy verbatim for the interaction lens, e.g.:
```cshtml
<section id="manabase-early-interaction" class="manabase-lens">
    <h3 class="manabase-lens-label">Early interaction</h3>
    <div class="manabase-lens-big manabase-lens-big--soft">@onTarget / @totalQualifying<span>interaction held up by turn 3</span></div>
    @foreach (var row in report.InteractionLens.Rows) { <div class="manabase-lens-row"> ... </div> }
    <p class="manabase-lens-note">Assumes you hold mana open — does not account for competing proactive plays.</p>
</section>
```
D-03's empty-state caution (zero qualifying spells) should follow the existing "short/warning" marker styling (`manabase-lens-short` class, ⚠ glyph) rather than hiding the section — do not gate the section's rendering on `Rows.Count > 0`.

**D-11 — worst-5 + `<details>` expander**, analog is the EXISTING castability table's own progressive-disclosure block (`Manabase.cshtml:800-814`):
```cshtml
@{ RenderCastabilityTable(visibleCastRows); }
@if (hiddenCastRows.Count > 0)
{
    <details class="manabase-castability-details">
        <summary>Show all @castRows.Count castability rows</summary>
        @{ RenderCastabilityTable(hiddenCastRows); }
    </details>
}
```
Copy this exact `<details>`/`<summary>` shape for the lens's worst-5-then-expand list (never silent truncation — L2).

**D-12 — castability table gains a holdable badge column on interaction rows only.** Analog is the existing `RenderCastabilityTable` local function (`Manabase.cshtml:242-281`) — same `<table class="manabase-table castability-table manabase-table--card">` shape, same per-cell `data-label` attributes for the mobile card-stack CSS (critical: `data-label` must be present on any new `<td>` or the 640px breakpoint's `::before` content-injection silently fails). Add one conditional `<td data-label="Held up (T1-3)">` cell rendered only when `c.PlanRoles.HasFlag(PlanRole.Interaction)` (or equivalent), using the same `manabase-chip` badge pattern as `CastChip`:
```cshtml
<td data-label="Cast on curve">
    <span class="manabase-chip @chip.Css">@c.CastPercent% · @chip.Label</span>
</td>
```

---

### `DeckFlow.Web/wwwroot/css/site-common.css` — 3-up responsive `manabase-twolens`

**Analog — current 2-up grid + single-state override + mobile collapse** (`site-common.css:2669-2675, 2992-2994, 3006-3008`):
```css
/* Two-lens result header (70-06): Karsten source check + simulated cast rate, side by side. */
.manabase-twolens {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.85rem;
  margin: 0.75rem 0 1rem;
}
...
.manabase-twolens--single {
  grid-template-columns: 1fr;
}

@media (max-width: 640px) {
  .manabase-twolens {
    grid-template-columns: 1fr;
  }
}
```
Extend with a `.manabase-twolens--triple { grid-template-columns: repeat(3, 1fr); }` (or switch the base rule to `grid-template-columns: repeat(auto-fit, minmax(...))`) applied via a Razor conditional class when all three lenses show; the existing 640px breakpoint already collapses ANY multi-column state to `1fr`, so no new mobile rule is needed there — just confirm the triple state also collapses (it inherits the same `.manabase-twolens` selector). Per Claude's Discretion: dark themes must use `--panel` not `--theme-surface` (matches the existing `.manabase-lens` rule at line 2678: `background: var(--panel-soft-bg, var(--panel));`).

**Layout-CSS placement constraint (CLAUDE.md):** ALL of this goes in `site-common.css`, never `site.css` — guild themes `@import site-common.css`, so a change there alone (with the existing `--panel`/`--line`/`--muted` token fallbacks already in place) themes correctly across all 24 forks without touching any theme file.

**Print-mode rule to also check** (`site-common.css:3775-3796`, `[data-print-region] .manabase-lens, .manabase-twolens, ...`) — verify the new lens/section IDs are covered by the existing selector (it targets the classes, not specific IDs, so no change needed unless a new distinct class is introduced).

---

### `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` — new "Early interaction" paste-artifact block

**Analog — `AppendTapAnalysisBlock`** (`ManabaseReportTextBuilder.cs:248-271`) is the closest shape: a nullable-parameter-gated private static append method, called only when the corresponding data is non-null, placed between the existing "Biggest fix" callout and the castability table:
```csharp
// --- Untapped sources (TAP-01/TAP-02) --------------------------------
// Only when tap metrics were computed (flag on). tap == null appends zero bytes, so the
// flag-off artifact stays byte-identical. Placed after the "Biggest fix" callout so the
// per-color untapped table never collides with that callout's "Colors:" wording.
if (tap is not null)
{
    AppendTapAnalysisBlock(sb, tap, report.ColorFindings.Count);
    sb.AppendLine();
}
```
```csharp
private static void AppendTapAnalysisBlock(StringBuilder sb, ManabaseTapAnalysis tap, int colorCount)
{
    sb.AppendLine("Untapped Sources:");
    sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
        $"Turn-1 untapped availability: {tap.Turn1UntappedPercent}% (...)"));
    ...
}
```
Add a new `ManabaseInteractionLens? interactionLens = null` parameter to `Build(...)` (mirrors the existing `tap`/`mulligan` optional-parameter pattern in the SAME method signature, `ManabaseReportTextBuilder.cs:47-58`) and an `AppendInteractionLensBlock` following the identical "N/M spells... — DeckFlow first-pass read" framing style used by `AppendMulliganEvaluationBlock` (`ManabaseReportTextBuilder.cs:277-322`, note its consistent "first-pass read only... not a ... recommendation" closing line — D-13 informational-v1 requires the same disclaiming tone here).

---

### `DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs` — upgrade the generic prose line (D-14)

**Exact target to replace** (`ManabaseSwapPromptBuilder.cs:48-53`):
```csharp
if (mode == ManabaseMode.Cedh)
{
    sb.AppendLine(
        "This is a cEDH deck — favor low land counts and fast mana, and prioritize early "
        + "(turn 1–3) untapped colored access for cheap interaction.");
}
```
Replace the generic clause with the REAL N/M number + worst spells, following the report-text builder's `string.Create(CultureInfo.InvariantCulture, $"...")` formatting convention used throughout this same file (e.g. lines 83-84, 97-98). Needs a new optional parameter on `Build(...)` (mirror the existing `verdict`/`budget`/`companionRow` optional-parameter additions at `ManabaseSwapPromptBuilder.cs:28-36`) carrying the interaction-lens data, e.g.:
```csharp
public static string Build(
    ManabaseReport report,
    string? deckName,
    string? decklistText,
    ManabaseMode mode = ManabaseMode.Casual,
    ManabaseVerdict? verdict = null,
    ManabaseRampDrawBudget? budget = null,
    bool includeCommandZone = false,
    CardCastability? companionRow = null,
    ManabaseInteractionLens? interactionLens = null)  // new
```

---

### `DeckFlow.Web/Help/manabase.md` — new subsection (mandatory closing task)

**Analog — subsection structure + flag-state framing** ("Untapped-source (Tap) analyzer", lines 83-93, and "Opening hand and plan presence", lines 95-111): each existing flagged-feature subsection follows the same shape — one-paragraph mechanism explanation, then "By default (the `analysis.manabase.X` flag, on/off), the report (and its paste artifact) add..." framing, then a bulleted breakdown of each displayed number, then a closing "This layer is informational/advisory only. It never changes the land count, color counts, castability table, or health verdict" disclaimer sentence. Copy this exact five-part shape (mechanism → flag-state → bullets → scope disclaimer → cross-reference to formula panels) for the new "cEDH: Early interaction" subsection, explicitly naming:
- The `PlanRole.Interaction` + effective-MV≤2 qualifying-spell definition (D-01/D-02).
- The `CedhSupportThreshold` (88) headline threshold, reusing the exact wording style already used for it in the Health-verdict bullet (line 57: "...short by more than about two Karsten sources...").
- The raw-availability caveat verbatim (D-07): "assumes you hold mana open" — must appear here per the Mandatory Closing Tasks note (help-doc overclaim class, M12).
- Cross-reference to Step 3's two formula panels (see below).

**Also update — "How the analysis works" + "This deck's numbers" panels** (`Manabase.cshtml:873-938`, and `manabase.md` lines 123-129, Step 3 description): both panels must cover the new metric with the deck's plugged-in numbers (Mandatory Closing Tasks, M12 precedent). The `this deck's numbers` panel's existing per-term breakdown pattern (`Manabase.cshtml:895-917`, the `<ul class="manabase-formula-list manabase-formula-terms">` land-target term list) is the shape to copy for a new interaction-lens numbers block (e.g., "N of M interaction spells qualified (PlanRole.Interaction, effective MV ≤ 2); X held up by turn 3 at the 88% threshold").

---

### `README.md` — behavior-change entry (mandatory closing task)

**Analog — the most structurally similar recent entries** (README.md:801, ritual-burst; :823, plan-presence; :802, "all manabase display/verdict reads now default ON"): each entry names the flag key + default state explicitly, states the cEDH-only scope, states what does NOT change (land count / color counts / verdict — informational v1 per D-13), and (for the default-ON entries) explicitly calls out "flag-off output is byte-identical" / "off = byte-identical." Follow the `ritual-burst-mana` entry's structure most closely (also cEDH-only, also a sim-derived metric), but flip the seed-state framing to match `:802`'s "ships ON by default" precedent since D-15 seeds ON.

## Shared Patterns

### Additive `{ get; init; }` DTO + nullable-until-computed slot on `ManabaseReport`
**Source:** `ManabaseModels.cs:1179` (`TapAnalysis`), `:1188` (`MulliganEvaluation`), `:1171` (`LandTarget`)
**Apply to:** the new `ManabaseInteractionLens` record and its slot on `ManabaseReport`.
```csharp
public ManabaseTapAnalysis? TapAnalysis { get; init; }
```
Never `{ get; }` — carve-out enforced by `CarveOutGuard` test; breaks JSON round-trip in .NET 9+ otherwise.

### cEDH-only feature gate
**Source:** `ManabaseAnalyzer.cs:169, 179` (ritual-land-credit / ritual-burst)
**Apply to:** `ManabaseAnalyzer.cs` new interaction-lens computation, and the `Manabase.cshtml` render condition for the 3rd lens / table badge column.
```csharp
bool ritualBurstActive = ritualBurst && mode == ManabaseMode.Cedh;
```

### Flag key + fail-safe-OFF read + result-side `ShowXxx` bool
**Source:** `ManabaseAnalysisService.cs:197-225` (const declarations), `:296-299` (`IsFlagOn` reads), `:469-472` (`IsFlagOn` helper — missing key = false, unlike `IFeatureFlagCache.IsEnabled`'s ON-default)
**Apply to:** the new `CedhInteractionLensFlagKey` const + its two `Show...` assembly sites in `ManabaseAnalysisResult`.

### Flag catalog description + dual-dialect seed row (default-ON precedent)
**Source:** `FeatureFlagCatalog.cs:99-110`, `FeatureFlagStore.cs:228-233` (Postgres) and `:270-275` (SQLite)
**Apply to:** register `analysis.manabase.cedh-interaction-lens` in both files, seeded `TRUE`/`1` per D-15. Both blocks must move together (guarded by `FeatureFlagCatalogTests`).

### Layout CSS in `site-common.css` only, `--panel` in dark themes
**Source:** `site-common.css:2670-2788` (all `.manabase-lens*` / `.manabase-twolens*` rules use `var(--panel-soft-bg, var(--panel))` / `var(--line, ...)` / `var(--muted, ...)` fallback tokens, never `--theme-surface`)
**Apply to:** the new 3-up grid rule and the interaction-lens section markup — no theme-file edits needed.

### Progressive disclosure — never silent truncation
**Source:** `Manabase.cshtml:800-814` (castability table `<details>` expander), `ManabaseDisplay.cs:184-236` (`DefaultVisibleCastabilityCount`/`CastabilitySummaryText`)
**Apply to:** D-11's worst-5 + `<details>` "view all" expander for the interaction lens — always disclose the hidden-row count (L2 finding).

### Paste-artifact block: nullable-gated, byte-identical-when-null
**Source:** `ManabaseReportTextBuilder.cs:172-185` (`if (tap is not null) { AppendTapAnalysisBlock(...); }`)
**Apply to:** both `ManabaseReportTextBuilder.Build` (new block) and `ManabaseSwapPromptBuilder.Build` (upgraded prose line) — D-14 requires BOTH artifacts to carry the lens data.

## No Analog Found

None — every touch point named in CONTEXT.md's `<code_context>` has a direct, load-bearing analog already in the codebase. This phase is a "join an established lens/flag/gate pattern a 7th time" phase, not a new-pattern phase.

## Metadata

**Analog search scope:** `DeckFlow.Core/Manabase/`, `DeckFlow.Web/Models/`, `DeckFlow.Web/Services/Manabase/`, `DeckFlow.Web/Services/FeatureFlags/`, `DeckFlow.Web/Views/Deck/Manabase.cshtml`, `DeckFlow.Web/wwwroot/css/site-common.css`, `DeckFlow.Web/Help/manabase.md`, `README.md`
**Files scanned:** 9 target files + their existing sibling-lens analog sections (TapAnalysis/MulliganEvaluation/PlanPresence/ritual-burst/ritual-land-credit families) read in full or via targeted offset reads
**Pattern extraction date:** 2026-07-13
