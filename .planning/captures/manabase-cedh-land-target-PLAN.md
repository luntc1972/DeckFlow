# CONTEXT + PLAN — cEDH land baseline pipeline (A) + archetype-sensitive land target (B)

**Origin:** session research (2026-07-11). 1597 real cEDH decks (EDHTop16, 3-month size-tiered:
16-32p winner / 33-63p top4 / 64+p top16; cEDH-gated). Finding: real cEDH runs **mean 26.3 lands**
(per-commander 21→31, SD ~1); the tool's `KarstenManabase.CedhLandTarget` floors at **28**, flagging
76-90% of winning decks under-target. Research data + first baseline: `_calib/cedh-land-baseline.{md,json}`.
**Roles:** Claude planned (this doc) → Codex plan-reviews → Codex builds → Claude reviews. Codex gpt-5.4
coding / gpt-5.5 plan-review, full-access, LF endings.

## Locked decisions (gsd-discuss, 2026-07-11)
- **B design = Hybrid, curve-anchored + baseline nudge.** Karsten curve computes the target; a
  commander's meta baseline *shifts* it toward the meta norm (weighted blend), so a high-curve build of
  a low-land commander still gets a higher target. Not a raw replacement.
- **Per-commander override only when N ≥ 10** in the baseline; else fall back to the recalibrated formula.
- **A fetch = Python script** (`scripts/`) for EDHTop16 GraphQL + Scryfall bulk; **compute = DeckFlow.CLI
  command** using the real `ManabaseClassifier` (math stays in Core).
- **Artifacts committed:** `data/cedh-land-baseline/YYYY-MM.{md,json}` + `latest.json` (app reads latest).
- **cEDH gate:** avgMV ≤ 2.7 AND 95–101 cards (drops high-curve + partial API lists).
- **Rollout:** new flag `analysis.manabase.cedh-land-target`, **default OFF**; re-baseline goldens;
  flag-off byte-identical.

## Canonical refs
- `DeckFlow.Core/Manabase/KarstenManabase.cs:55-68` — `CedhLandTarget` = `Math.Max(28.0, singleton-3.5)` (the target to change).
- `DeckFlow.Core/Manabase/ScryfallCardFactMapper.cs` / `ScryfallCardData.cs` — Scryfall JSON → CardFact (reused by the CLI compute).
- `_calib/cedh-land-baseline.json` — first baseline (seed for `data/.../latest.json`).
- EDHTop16 GraphQL `https://edhtop16.com/api/graphql` — `tournaments(filters:{minSize,maxSize,minDate}, sortBy:DATE){edges{node{size tournamentDate entries(maxStanding){standing commander{name} maindeck{name}}}}}`.
- Scryfall `POST /cards/collection` (75 ids/batch, by name).

---

## PHASE A — Monthly cEDH land-baseline pipeline

**Goal:** a repeatable, committed, per-commander cEDH land baseline the tool (Phase B) can read.

### A1 — Fetch script `scripts/cedh-baseline/fetch.py`
- Args: `--since YYYY-MM-DD` (default = today − 3 months), `--outdir _calib` (raw cache).
- Three tiered EDHTop16 queries (server-side size filter + matching `maxStanding`): `{maxSize:32,minSize:16}`→st1; `{minSize:33,maxSize:63}`→st≤4; `{minSize:64}`→st≤16. Paginate each to completion.
- Collect commanders (split partner pairs on ` / `) + maindeck names; Scryfall bulk-resolve (75/batch, retry not_found by front face `// `); cache to `cards_full.json` (reuse across months — only resolve new names).
- Emit `decks_all.json` (each: tier, size, cmdkey, commanders[], standing, maindeck[]) + `cards_full.json`.
- Idempotent + resumable (incremental card-cache save). Pure stdlib (no new pip deps).

### A2 — CLI command `cedh-land-baseline` (DeckFlow.CLI)
- Wire into `DeckFlow.CLI/Program.cs` (System.CommandLine root) + `CommandRunners.cs`, mirroring existing commands (`archidekt-categories` etc.).
- Args: `--data <dir>` (reads `decks_all.json`+`cards_full.json`), `--out <dir>` (default `data/cedh-land-baseline`), `--month YYYY-MM` (label; passed in — no `DateTime.Now` in Core paths per convention, take from arg).
- Per deck: build `CardFact`s via `ScryfallCardFactMapper.ToCardFact`; classify (`ManabaseClassifier.Classify`, accuracy-on: rampCreditV2/landRampSim/payLifeUntapped/checkLandUntapped=true) — **classify-only, no Monte-Carlo** (fast). Land count = facts where `HasLandFace || FrontFace(type) contains "Land"`. avgMV = `deck.AverageManaValue`.
- cEDH gate: keep iff `avgMV ≤ 2.7 && 95 ≤ cards ≤ 101`. Track dropped counts (curve vs incomplete).
- Aggregate by `cmdkey`: N, land mean, land SD, min-max, avgMV. Also per-tier + overall + land histogram.
- Write `YYYY-MM.md` (human) + `YYYY-MM.json` and copy to `latest.json`. **`latest.json` schema (the app contract):**
  ```json
  { "generated": "2026-07", "sampleSize": 1597, "overallMeanLands": 26.3,
    "commanders": { "Kinnan, Bonder Prodigy": { "n": 157, "landsMean": 25.7, "landsSd": 0.9 }, ... } }
  ```
  Include every commander with N ≥ 3 (Phase B applies its own N≥10 gate on read; keeping ≥3 lets the file show more).
- Seed `data/cedh-land-baseline/2026-07.*` + `latest.json` from this session's `_calib/cedh-land-baseline.json` (regenerated through the finished command so format is canonical).

### A3 — Runbook + tests
- `scripts/cedh-baseline/README.md`: monthly = `python fetch.py --since <date>` → `dotnet run --project DeckFlow.CLI -- cedh-land-baseline --data _calib --month YYYY-MM` → commit the new dated files.
- Tests (DeckFlow.Core.Tests, or a CLI test if one exists): the aggregation + cEDH gate as pure functions over a tiny fixture (extract the gate + per-commander rollup into a testable Core helper so the CLI stays thin). Assert gate drops a high-curve + a short list, keeps a cEDH deck; per-commander mean/SD correct.
- README (root) updated: new pipeline + monthly step.

**A boundary / non-goals:** no live network from the app; no CI cron (manual/local monthly); the fetch stays a script (Core stays HTTP-light). Runtime consumption of `latest.json` is Phase B.

---

## PHASE B — Archetype-sensitive cEDH land target (hybrid, flag-gated)

**Goal:** replace the flat-28 cEDH floor with a curve-anchored target that nudges toward a commander's
meta land count when the baseline has N ≥ 10; behind a default-OFF flag.

### B1 — Baseline provider (Web)
- `ICedhLandBaselineProvider` + impl loading `data/cedh-land-baseline/latest.json` (ship as `<Content>` copied to output, like `Help/**`; read once, cache in memory). Lookup by commander key: returns `(mean, n)` or null.
- Commander key match: the deck's commander(s) → the baseline `cmdkey` (partner pairs are the joined ` / ` string, matching how the pipeline keys them). Provide a normalized match (exact first; for partners, try the joined name in either order).

### B2 — Core target: `CedhLandTarget` hybrid overload
- Add params (or a small `CedhLandContext`) carrying optional `commanderBaselineMean` + `commanderBaselineN` + `enabled`. Keep the existing 5-arg signature delegating to the new one with nulls (byte-identical).
- New logic (flag ON):
  - `curveTarget = singleton - 3.5` (drop the flat 28 floor; apply a lower safety floor ~**22** so a degenerate low-curve deck can't read absurdly low — floor value = Codex/research to confirm from the histogram, which bottoms ~22 for real decks, min 16 outliers).
  - If baseline `n ≥ 10`: `target = curveTarget - w*(curveTarget - mean)` (curve-anchored nudge toward the meta mean; **w ≈ 0.5** start, tune in calibration). Never below the safety floor.
  - Else: `target = curveTarget` (recalibrated, floor ~22 not 28).
- Flag OFF: unchanged `Math.Max(28.0, singleton - 3.5)`.
- Update the xmldoc: the 28-floor rationale is superseded by the 2026-07 baseline (mean 26.3); cite the pipeline.

### B3 — Wire flag + thread commander/baseline
- New flag const `CedhLandTargetFlagKey = "analysis.manabase.cedh-land-target"` in `ManabaseAnalysisService`; catalog entry; Postgres/SQLite seed OFF; catalog + seed tests (mirror the ritual-burst P3 wiring exactly).
- `ManabaseAnalysisService` reads the flag + resolves the commander baseline via `ICedhLandBaselineProvider`, passes into the analyzer → `CedhLandTarget`. The analyzer must know the deck's commander name (available from `deck` commander spells) — thread it (or the resolved `(mean,n)`) down to where `CedhLandTarget` is called. Find the call site in `ManabaseAnalyzer`/`ManabaseModels` land-target computation and thread the context.
- Casual mode + 60-card untouched (only `CedhLandTarget` changes).

### B4 — Tests + goldens + docs
- Core: `CedhLandTarget` unit tests — flag off = `Math.Max(28,…)` byte-identical; flag on no-baseline = curveTarget w/ ~22 floor (lower than 28 for a low-curve deck); flag on with a commander mean 25 + N≥10 nudges a curve-28 deck toward ~26.5 (w=0.5); N<10 ignored.
- Web: service-level — flag off byte-identical; flag on + cEDH + a known commander (Kinnan, in seeded baseline) lowers `TargetLands` vs off; flag on + Casual unchanged.
- **Re-baseline** any manabase golden that asserts a cEDH `TargetLands`/health, since flag ON changes numbers — but only ON-path goldens; the default-OFF suite MUST stay byte-identical (guard).
- Docs: `docs/manabase-analysis-rules.md` (new rule + flag row + supersede the 28-floor note), Help, README.

### B boundary / non-goals
- No change to Casual/60-card targets, color math, or castability. No auto-refresh of the baseline (Phase A is manual/monthly). Blend weight `w` + floor are calibration knobs — ship OFF, tune, then flip.

---

## Plan-review conditions (Codex gpt-5.5, APPROVE-WITH-CONDITIONS — folded)
- **C1 (commander names, MED):** do NOT derive commander keys from `deck.Spells` (variable-cost cards are
  skipped by `AddSpellRequirement`, unreliable). Add `CommanderNames` to `ResolvedManabaseDeck` from the
  commander-board `DeckEntry`/`DeckCardEntry` in `ManabaseAnalysisService` (~:476); resolve the baseline in
  Web; pass ONLY `(mean, n, enabled)` into Core `CedhLandTarget`. Core stays name-agnostic.
- **C2 (content path, MED):** commit the baseline under **`DeckFlow.Web/Data/cedh-land-baseline/`** (same
  place as `bracket-data.json`, already `<Content>`-copied) — NOT repo-root `data/`. The CLI writes there;
  the provider reads the output-copied file. Avoids an ad-hoc `<Content Include="..\data\...">`.
- **C3 (CLI runner, MED):** there is no `CommandRunners.cs`. Add a dedicated `CedhBaselineCommandRunner.cs`
  mirroring `ManabaseCommandRunner.cs` (which already uses `ScryfallCardFactMapper`/`ManabaseClassifier`),
  wired in `Program.cs` (~:227). No DI; Serilog only if needed.
- **C4 (land count parity, MED):** compute the baseline land count as `classified.Sources.Count(s => s.IsLand)`
  (the app's own definition, `ManabaseAnalyzer.cs:154` / `MAS:506`), NOT a separate fact predicate, so the
  baseline can never drift from app math. (Note: this session's `_calib` used a fact predicate — the canonical
  CLI run may shift a few counts; re-seed from the finished command.)
- **C5 (flag test checklist, LOW):** explicitly update `FeatureFlagStoreSeedTests` inline data + the Postgres
  literal assertion (~:61) + `FeatureFlagCatalogTests` inline data. AdminFlags picks it up automatically.
- **C6 (goldens, LOW):** keep the existing `CedhLandTarget` 5-arg overload delegating to the OLD
  `Math.Max(28,…)` behavior so flag-OFF stays byte-identical — existing `>=28` tests
  (`ManabaseAnalyzerTests.cs:261`, `KarstenManabaseCastabilityTests.cs:213`) stay green. Add NEW
  overload/context tests for the flag-ON path only. No broad golden churn.

## Sequencing & dependencies
1. **A first** — it produces `latest.json` (the contract B reads) + seeds `data/`. Land, review, commit.
2. **B second** — depends on A's `latest.json` schema + seed baseline.
3. Each phase: Claude plan (this doc) → Codex plan-review → Codex build → Claude review → commit. Ship both OFF; calibrate; flip flags later (B flip also unblocks re-judging ritual-burst default).

## Risks / open
- **R1 (commander-key matching):** partner-pair naming must match between the pipeline `cmdkey` and the deck's commanders at runtime, or overrides silently never fire. Mitigate: B2 test with a partner commander; log a miss-rate.
- **R2 (blend/floor values):** `w=0.5` and floor ~22 are starting guesses; calibration against the 1597-deck set (re-run the harness with the new target) must confirm the tool stops flagging 76-90% under-target without over-correcting grindy decks (Sisay/Tayam should still read ~healthy at 27-31).
- **R3 (golden churn):** flag ON shifts cEDH targets widely; keep the change flag-gated so OFF goldens are untouched (byte-identical guard is the tripwire).
- **R4 (data in public repo):** `data/cedh-land-baseline/*` is derived public tournament data — fine to commit, no secrets.
