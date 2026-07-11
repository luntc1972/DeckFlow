# EXECUTION PLAN — Phase B: archetype-sensitive cEDH land target (hybrid, flag-gated)

**Milestone:** cEDH land-target recalibration. Phase A shipped (`11650766`, branch
`feat/manabase-cedh-land-target`, pushed). Phase B continues on the **same branch**.
**Parent plan:** `.planning/captures/manabase-cedh-land-target-PLAN.md` (B1-B4 + conditions C1/C5/C6).
This doc refines that against the live code (file:line verified 2026-07-11 by 3 read-only mappers).
**Roles:** Claude planned (this) → Codex plan-reviews → Codex builds → Claude reviews → commit.
Codex gpt-5.4 coding, full-access, **LF endings** (`.gitattributes`).

**Goal:** replace the flat-28 cEDH land floor with a curve-anchored target that nudges toward a
commander's real meta land count when the Phase-A baseline has **N ≥ 10**; behind a **default-OFF**
flag. Flag OFF = byte-identical to today. Casual / 60-card paths untouched.

---

## Locked design (from parent-plan discuss)
- **Hybrid, curve-anchored + baseline nudge.** `curveTarget = singleton − 3.5` (drop the 28 floor);
  apply a **safety floor = 22** (real cEDH histogram bottoms ~22, min-16 outliers). When baseline
  `n ≥ 10`: `target = curveTarget − w·(curveTarget − mean)` with **w = 0.5** (midpoint blend toward
  the meta mean), clamped ≥ floor. Else `target = max(curveTarget, floor)`.
- **`w` and the floor are calibration knobs** — ship OFF, tune against the 1597-deck set, then flip.
  Define both as named consts so tuning is a one-line change.
- **Core stays name-agnostic (C1).** Web resolves the commander baseline and passes only a
  `(mean, n, enabled)` value object into Core. Core never sees commander names.

---

## Canonical refs (verified file:line)
- `DeckFlow.Core/Manabase/KarstenManabase.cs:49-69` — current `CedhLandTarget(int totalCards, int
  commanderCount, double averageManaValue, double rampAndDrawUnderThree, double fastMana=0)` →
  `Math.Max(28.0, singleton−3.5)`. **Only prod caller:** `ManabaseAnalyzer.cs:341`.
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs:318-351` — `ComputeTargetLands`; mode ternary at `:340`;
  `Analyze` calls it at `:158`, sets `TargetLands` at `:198`; `BuildBreakdown` records
  `CedhAdjustment = finalTarget − baseTarget` at `:369`.
- `DeckFlow.Core/Manabase/CedhLandBaseline.cs:262-295` — reusable `CedhLandBaselineSnapshot` /
  `CedhCommanderBaselineSnapshot` records (the JSON contract deserialization targets).
- `DeckFlow.Web/Data/cedh-land-baseline/latest.json` — the committed baseline (Content-copied to output).
- **Flag template (ritual-burst-mana):** const `ManabaseAnalysisService.cs:192-197`; read `:252`;
  thread `:285-290`; catalog `FeatureFlagCatalog.cs:96-99`; seed `FeatureFlagStore.cs:230` (PG FALSE) /
  `:267` (SQLite 0); tests `FeatureFlagStoreSeedTests.cs:43,69` + `FeatureFlagCatalogTests.cs:44`.
- **Content-provider template:** `DeckFlow.Web/Services/Bracket/GameChangerCatalogService.cs`
  (ContentRootPath/Data/…, `IMemoryCache` 24h, internal test-seam ctor); DI `Program.cs:92`; warm-load
  `Program.cs:278`. Test template `DeckFlow.Web.Tests/Bracket/GameChangerCatalogServiceTests.cs`.
- **Consumer service:** `ManabaseAnalysisService` ctor `:207-224` (optional deps default null); DI
  Scoped `Program.cs:177`; `ResolvedManabaseDeck` record `:644-650`, constructed `:513`; command board
  reflagged `:638`, `DeckCardEntry{IsCommander}` at `:480`.
- **Goldens (must stay green flag-OFF):** `ManabaseAnalyzerTests.cs:247-272`,
  `KarstenManabaseCastabilityTests.cs:212-231` — assert **ranges/inequalities only** (`>=28`, `<casual`,
  `InRange(28,30)`), NO exact numeric land goldens.
- **Packet cache:** `PromptMutatingAnalysisFlags` (`DeckAnalysisPacketService.cs:159-166`) — **DO NOT add
  this flag.** The manabase swap prompt is built by `ManabaseSwapPromptBuilder`, which does NOT go through
  `PacketSessionCache`; ritual-burst is correctly absent for the same reason.

---

## B1 — Core: hybrid `CedhLandTarget` overload (name-agnostic)
**File:** `DeckFlow.Core/Manabase/KarstenManabase.cs`
- Add a value object (same file or `ManabaseModels.cs`, whichever neighbors fit — prefer KarstenManabase.cs):
  ```csharp
  public readonly record struct CedhLandContext(double? BaselineMean, int BaselineN, bool Enabled)
  {
      public static readonly CedhLandContext Disabled = new(null, 0, false);
  }
  ```
- Add named consts: `private const double CedhSafetyFloor = 22.0;` and
  `private const double CedhBaselineBlendWeight = 0.5;` (the two calibration knobs, xmldoc'd as tunable).
- **Keep the existing 5-arg `CedhLandTarget` (C6)** — change its body to delegate:
  `=> CedhLandTarget(totalCards, commanderCount, averageManaValue, rampAndDrawUnderThree, fastMana, CedhLandContext.Disabled);`
- **New overload** `CedhLandTarget(int totalCards, int commanderCount, double averageManaValue,
  double rampAndDrawUnderThree, double fastMana, CedhLandContext context)`:
  ```
  singleton   = SingletonLandTarget(...)               // unchanged
  if (!context.Enabled)   return Math.Max(28.0, singleton - 3.5);   // OLD behavior, byte-identical
  curveTarget = singleton - 3.5;                        // drop the 28 floor
  useBaseline = context.BaselineN >= 10
                && context.BaselineMean is double mean
                && double.IsFinite(mean) && mean is >= 10 and <= 60;   // outlier/corrupt guard (RV-2)
  target      = useBaseline
                  ? curveTarget - CedhBaselineBlendWeight * (curveTarget - mean)
                  : curveTarget;
  return Math.Clamp(target, CedhSafetyFloor, CedhTargetCeiling);        // sane ceiling caps corrupt data
  ```
  Add `private const double CedhTargetCeiling = 45.0;` (a corrupt/absurd baseline mean can't produce an
  insane target; legit high-land archetypes like Lumra N18 mean46.8 still land near the cap, which is the
  desired "this deck really runs many lands" signal — see RV-2 test in B6). The `[10,60]` mean band + the
  finite check reject NaN/Inf and garbage without discarding real high-land commanders.
- Update the xmldoc: note the 28-floor is superseded (flag ON) by the 2026-07 baseline (mean 26.3);
  cite the Phase-A pipeline; document `w`/floor as calibration knobs.
- **N ≥ 10 gate is enforced in Core** (defence-in-depth) even though the provider also gates on read.

## B2 — Core: thread the context through the analyzer
**File:** `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs`
- `Analyze(...)` gains a trailing optional param `CedhLandContext cedhContext = default` (`default` =
  `Disabled`, so every existing caller is byte-identical). Pass it into `ComputeTargetLands`.
- `ComputeTargetLands(deck, mode, cedhContext, out breakdown)`: at the mode ternary (`:340-341`), call
  the **new 6-arg** `CedhLandTarget(..., cedhContext)` on the cEDH branch. Casual/60-card branches
  untouched. `BuildBreakdown`'s `CedhAdjustment` (`:369`) keeps working (`finalTarget − baseTarget`).
- No signature change to `SixtyCardLandTarget` / `SingletonLandTarget`.

## B3 — Web: baseline provider (`ICedhLandBaselineProvider`)
**New file:** `DeckFlow.Web/Services/Manabase/CedhLandBaselineProvider.cs` (interface + impl in one file,
per repo convention).
- Mirror `GameChangerCatalogService`: DI ctor `(IWebHostEnvironment env, IMemoryCache cache,
  ILogger<CedhLandBaselineProvider>? logger = null)` → `Path.Combine(env.ContentRootPath, "Data",
  "cedh-land-baseline", "latest.json")` (logger optional, `NullLogger` fallback per repo convention, so
  the "log once on load failure" requirement has a sink — RV-4); internal test-seam ctor
  `(string path, IMemoryCache cache, ILogger? logger = null)`; deserialize to **`CedhLandBaselineSnapshot`**
  (reuse the Core record) with `JsonSerializerDefaults.Web`; cache in `IMemoryCache` 24h; fail-**open**
  (missing/garbage file → treat as "no baseline", log once, DO NOT throw — the flag defaults OFF and the
  tool must never hard-fail on a data gap; this differs from bracket-data which is load-bearing).
- API (name-agnostic result):
  ```csharp
  void EnsureLoaded();   // warm-load hook for Program.cs startup; loads+caches, swallows failure (fail-open)
  bool TryGetBaseline(IReadOnlyList<string> commanderNames, out double mean, out int n);
  ```
  `TryGetBaseline` calls the same private load-or-get-cached helper `EnsureLoaded` uses, so the startup
  warm-load (RV-1) and request-time lookup share one code path.
  Matching (Ordinal, mirrors how the baseline dict was keyed):
  1. 1 name → look up that name directly.
  2. 2 names → try `"{a} / {b}"`, then `"{b} / {a}"` (join order not guaranteed).
  3. ≥3 or 0 names, or no hit → return false.
  - **Do NOT split on `" // "`** — an MDFC commander (e.g. `"Ral, Monsoon Mage // Ral, Leyline Prodigy"`)
    is a single card name that is already a baseline key; treat each runtime commander name atomically.
- Register **Singleton** in `Program.cs` near `:92` (beside `IGameChangerCatalogService`); add a warm-load
  near `:278` calling `EnsureLoaded()` so first request has no disk I/O (RV-1).

## B4 — Web: flag const, read, resolve baseline, thread context
**File:** `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` (mirror ritual-burst exactly)
- Const near `:192-197`:
  `public const string CedhLandTargetFlagKey = "analysis.manabase.cedh-land-target";` (xmldoc: seeded OFF;
  hybrid curve-anchored cEDH land target nudged toward the meta baseline when N≥10; off = byte-identical).
- Ctor `:207-224`: add `ICedhLandBaselineProvider? cedhLandBaseline = null` as the **LAST** ctor
  parameter (after `logger`) + backing field. It MUST go last — existing `ManabaseAnalysisServiceTests`
  (~`:636-644`) pass `categoryKnowledge`/etc. positionally, so inserting mid-list is a compile trap
  (RV-3). Optional/defaulted so those call sites keep compiling untouched.
- `ResolvedManabaseDeck` record `:644-650`: add `IReadOnlyList<string> CommanderNames`. Populate at
  construction `:513` from `deckEntries.Where(e => e.IsCommander).Select(e => e.Card.Name).ToList()`
  (post-`ReflagInferredCommanders`, so reliable). Casual path also fills it (harmless; unused there).
- In `AnalyzeAsync` near `:252`: `bool cedhLandTarget = IsFlagOn(CedhLandTargetFlagKey);` then build the
  context only for cEDH mode:
  ```csharp
  CedhLandContext cedhContext = CedhLandContext.Disabled;
  if (cedhLandTarget && options.Mode == ManabaseMode.Cedh && _cedhLandBaseline is not null
      && _cedhLandBaseline.TryGetBaseline(resolved.CommanderNames, out double mean, out int n))
      cedhContext = new CedhLandContext(mean, n, Enabled: true);
  else if (cedhLandTarget && options.Mode == ManabaseMode.Cedh)
      cedhContext = new CedhLandContext(null, 0, Enabled: true);   // recalibrated (no baseline) path
  ```
  Pass `cedhContext` into `ManabaseAnalyzer.Analyze(...)` at `:285-290` (new trailing arg).
- **Flag OFF** → `cedhContext` stays `Disabled` → Core returns `Math.Max(28, singleton−3.5)` → byte-identical.
- **Do NOT** touch `PromptMutatingAnalysisFlags` (see refs).

## B5 — Flag catalog + seed (mirror ritual-burst)
- `FeatureFlagCatalog.cs` (~`:96-99`): add description entry for `"analysis.manabase.cedh-land-target"`
  (what it does, cEDH-only, off = byte-identical).
- `FeatureFlagStore.cs`: Postgres seed `('analysis.manabase.cedh-land-target', FALSE)` (~`:230`) + SQLite
  `('analysis.manabase.cedh-land-target', 0)` (~`:267`). NOT in `RenamedFlagKeys` (brand-new flag).

## B6 — Tests, goldens, docs
**Core tests** (`DeckFlow.Core.Tests/Manabase/`):
- New `CedhLandTargetHybridTests` (or extend `KarstenManabaseCastabilityTests`):
  - Disabled context → equals `Math.Max(28, singleton−3.5)` (byte-identical to the 5-arg overload).
  - Enabled, no baseline (`N<10`/null) → `max(singleton−3.5, 22)` — LOWER than 28 for a normal curve.
  - Enabled, baseline mean 25, N≥10, a curve giving curveTarget≈28 → nudged ≈ **26.5** (w=0.5).
  - Enabled, baseline N=9 → ignored (falls to curveTarget path).
  - Safety-floor clamp: degenerate low curve → never below 22.
  - **RV-2 high/corrupt baseline:** legit high-land archetype (mean 46.8, N18 — Lumra) → target raised but
    clamped ≤ ceiling 45; corrupt mean (`double.NaN`, `double.PositiveInfinity`, mean 999) → guard rejects
    → falls to curveTarget path (no absurd target).
- `ManabaseAnalyzer` test: `Analyze` with default context = today's output (the existing `:247-272`
  goldens stay green — assert this explicitly).
**Web tests** (`DeckFlow.Web.Tests/`):
- `CedhLandBaselineProviderTests` (mirror `GameChangerCatalogServiceTests`): binds `latest.json`; single
  + partner (both orders) match; miss returns false; **missing/garbage file → returns false, no throw**
  (fail-open); second call cached (`Assert.Same` or single file read).
- Service-level (`ManabaseAnalysisServiceTests`): flag OFF → cEDH `TargetLands` byte-identical; flag ON +
  cEDH + seeded commander (**Kinnan, Bonder Prodigy** — N157 mean25.7 in `latest.json`) → `TargetLands`
  **lower** than flag-OFF; flag ON + **Casual** → unchanged. Log/verify a partner-pair match too (R1).
- Flag tests: `FeatureFlagStoreSeedTests.cs` inline `[InlineData("analysis.manabase.cedh-land-target", false)]`
  + Postgres literal `Assert.Contains("('analysis.manabase.cedh-land-target', FALSE)", ...)` (~`:69`);
  `FeatureFlagCatalogTests.cs` inline `[InlineData("analysis.manabase.cedh-land-target")]` (~`:44`).
**Docs:**
- `docs/manabase-analysis-rules.md`: new rule row + flag row; supersede the 28-floor note (cite Phase-A
  baseline mean 26.3, N=1597).
- In-app Help (manabase topic) + root `README.md`: mention the flag + that it's cEDH-only, default OFF.

---

## Sequencing (single branch `feat/manabase-cedh-land-target`)
1. B1 → B2 (Core hybrid + thread) — self-contained, goldens prove byte-identical with default context.
2. B3 (provider) — depends on Phase-A `latest.json` (already committed) + Core snapshot records.
3. B4 → B5 (Web flag wiring + seed) — depends on B1-B3.
4. B6 (tests/goldens/docs) — throughout; full suite green before commit.
Commit per logical change (Core; provider; flag-wiring; docs) or one squashable feature commit — Codex's
call, but keep flag-off byte-identity provable at each step.

## Codex plan-review (gpt-5.5, APPROVE-WITH-CONDITIONS — folded)
- **RV-1 (MED):** provider exposes `EnsureLoaded()` for the `Program.cs:278` warm-load, sharing the
  load-or-cached path with `TryGetBaseline` (B3).
- **RV-2 (MED):** Core guards the baseline mean (`double.IsFinite` + `[10,60]` band) and clamps the final
  target to `[22,45]` so corrupt/absurd data can't produce an insane target; legit high-land archetypes
  still read high. High/corrupt-baseline test added (B1/B6).
- **RV-3 (LOW):** new provider ctor param goes **last** on `ManabaseAnalysisService` — existing tests pass
  earlier optionals positionally (B4).
- **RV-4 (LOW):** provider takes an optional `ILogger` (NullLogger fallback) so the fail-open "log once"
  has a sink (B3).
No blocking wiring gap: anchors verified, `TargetLands` flows only through `ComputeTargetLands`, flag-OFF
byte-identity holds via the disabled-context delegate, swap prompt bypasses `PacketSessionCache`.

## Conditions carried from the parent plan
- **C1:** Core name-agnostic; Web resolves baseline, passes only `(mean,n,enabled)` — satisfied by
  `CedhLandContext`. **C5:** explicit flag seed/catalog test edits (B5/B6). **C6:** old 5-arg overload
  preserved delegating to `Math.Max(28,…)`; existing `>=28`/`<casual` tests stay green; new tests are
  flag-ON only.

## Risks / open
- **R1 (key matching):** partner-pair join order + MDFC `//` vs partner `/`. Mitigate: both-order match,
  atomic MDFC names, a Web test with a seeded partner pair (e.g.
  `"Rograkh, Son of Rohgahh / Thrasios, Triton Hero"`, N111), and a debug-log on miss to measure miss-rate.
- **R2 (blend/floor):** `w=0.5`, floor 22 are starting guesses — named consts, tuned post-ship against the
  1597-deck harness (must stop the 76-90% under-flag WITHOUT over-correcting grindy Sisay/Tayam ~27-31).
- **R3 (golden churn):** flag ON shifts cEDH targets; keep every change flag-gated so the default-OFF suite
  is byte-identical (the `>=28` goldens are the tripwire).
- **R4 (provider fail-open):** unlike bracket-data (load-bearing, throws), the baseline is advisory behind a
  default-OFF flag — a missing/corrupt file must degrade to "no baseline", never 500 the manabase tool.
- **Open:** confirm the exact safety-floor value from the 2026-07 histogram (`2026-07.md`) — plan uses 22;
  Codex may adjust to the observed real-deck minimum band if the histogram argues otherwise (note it).

## Non-goals
No change to Casual/60-card targets, color math, or castability. No auto-refresh of the baseline (Phase A is
manual/monthly). No UI redesign. Flipping the flag ON in prod is a **separate** post-calibration step (also
unblocks re-judging [[project_manabase_ritual_burst]] default).
