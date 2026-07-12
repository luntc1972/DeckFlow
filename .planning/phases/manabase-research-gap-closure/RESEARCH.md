# Phase: Manabase Research-Gap Closure - Research

**Researched:** 2026-07-12
**Domain:** Magic: The Gathering manabase analysis engine (C# / .NET 10, `DeckFlow.Core.Manabase`)
**Confidence:** MEDIUM-HIGH (engine patterns HIGH — read from source; MTG oracle-text facts MEDIUM — cross-checked WebSearch, not Context7/official Scryfall API due to 403; one item flagged LOW/unresolved — see Open Questions #1)

## Summary

This phase closes six bounded research-vs-implementation gaps in the shipped
`/manabase` analyzer, already scoped tightly by CONTEXT.md's D-01..D-14. The
engine is well-documented in `docs/manabase-analysis-rules.md` (actively
maintained, "code wins" caveat) — that file plus direct source reads of
`ManabaseClassifier.cs`, `KarstenManabase.cs`, `CastabilitySimulator.cs`, and
`ManabaseVerdictSynthesizer.cs` are the HIGH-confidence backbone of this
research. All patterns needed for MBGAP-01/02/03/05 already exist in the
codebase in a directly reusable form (check-land census, alt-cost disclosure
marker, ritual-burst flag/calibration-harness templates); none of this phase
requires new architecture beyond one new `CardKind` enum case for count-based
per-trial conditional lands (MBGAP-02 fast/slow/ELD).

The one item that could NOT be verified is "Training Compound" (named in
CONTEXT.md/ROADMAP as one of the six MBGAP-02 cycles) — no card by that exact
name was found on Scryfall via WebSearch. This must be resolved with the user
before planning locks the cycle list (see Open Questions #1) — it is likely a
misremembered name for a different card/cycle, not a fictional requirement to
drop silently.

MBGAP-04 (consistency threshold) is explicitly a **research spike**, not an
implementation task — the deliverable is a decision doc that resolves a real
corpus contradiction: `.planning/research/manabase-math.md` tags the
escalating (89+M)% Karsten threshold `[H, verbatim]` (fetched via headless
browser from TCGplayer's 2022 article), while
`.planning/captures/manabase-efficacy-findings-r2.md` L14 calls the same
number "unconfirmed." This research resolves that contradiction directly
(see MBGAP-04 section) so the spike plan doesn't have to re-derive it.

**Primary recommendation:** Follow the reusable patterns exactly as they
exist in the codebase — census-based static classification for MBGAP-01/most
of MBGAP-02, a new per-trial dynamic `CardKind` for fast/slow/ELD cycles only,
the alt-cost `1*`-marker template verbatim for MBGAP-01 disclosure, and the
`CedhCalibrateCommandRunner` shape verbatim as the template for a ritual
land-credit calibration pass. Resolve the "Training Compound" naming gap with
the user before the plan locks card lists.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Conditional-restriction land classification (MBGAP-01) | API/Backend (`DeckFlow.Core.Manabase`) | — | Pure classification logic, no HTTP/UI; mirrors existing check-land census which lives entirely in `ManabaseClassifier.cs` |
| Untapped-cycle classification + per-trial sim rules (MBGAP-02) | API/Backend (`DeckFlow.Core`) | — | Classifier (static census) + `CastabilitySimulator` (per-trial dynamic evaluation for fast/slow/ELD) — both Core, no Web dependency |
| Ritual land-target credit + calibration (MBGAP-03) | API/Backend (`DeckFlow.Core.Manabase.KarstenManabase`) | CLI (`DeckFlow.CLI` calibration harness) | Land-target math is Core; the calibration replay tool that tunes the constant is the CLI, matching the `cedh-land-calibrate` precedent |
| Consistency-threshold research spike (MBGAP-04) | Docs / Research artifact | API/Backend (`KarstenManabase.ConsistencyThreshold`) if implemented | This phase's deliverable is a decision doc; only a follow-up phase touches the Core constant |
| Verdict-polish batch (MBGAP-05a-d) | API/Backend (`ManabaseVerdictSynthesizer`) | Frontend/Razor (`Manabase.cshtml`, swap prompt) | Synthesizer owns the wording logic; the view/prompt-builder consume its strings, so both may need touch for 05c (plural artifacts appear in both layers) |
| Help doc re-audit (MBGAP-11) | Docs (`DeckFlow.Web/Help/manabase.md`) | — | Static markdown content, no code change |
| Lens visual verification (MBGAP-12) | Browser/Client (visual QA) | Frontend/Razor (`Manabase.cshtml`) | Verification-only task against the already-shipped tap-analyzer + mulligan lens views |

## Package Legitimacy Audit

Not applicable — this phase adds no new NuGet/npm/pip packages. All work is
within `DeckFlow.Core`, `DeckFlow.Web`, and `DeckFlow.CLI` using existing
dependencies (no new `<PackageReference>` needed for any MBGAP item).

## User Constraints (from CONTEXT.md)

### Locked Decisions

**Scope & tier cut**
- **D-01:** Phase ships Tier 1 (MBGAP-01/02/03/04) + Tier 2 (MBGAP-05a-d) +
  MBGAP-11/12 as closing tasks. Tier 3 stays in backlog.
- **D-02:** MBGAP-09 (cEDH castability surface / early-interaction turns-1-3
  color-access lens) = own later phase. Keep the ROADMAP backlog pointer.

**MBGAP-01 — conditional-restriction lands**
- **D-03:** Composition-gated per-class modeling (mirrors the check-land
  census pattern): Cavern of Souls / Unclaimed Territory = full color source
  only for the deck's dominant-creature-type share (heavy discount otherwise);
  Ancient Ziggurat = weight by creature share of deck; Nykthos = conditional
  low weight. NOT a flat discount, NOT full spend-restriction sim masks.
- **D-04:** New flag `analysis.manabase.restricted-lands`, ships OFF.
  Golden-deck diff + calibration before operator flip. Flag-off byte-identical.
- **D-05:** Disclosure = per-row marker in the castability table (reuse the
  alt-cost `1*` marker pattern) + entry in the existing unsupported-interactions
  panel.

**MBGAP-02 — untapped-land cycles (Phase 2 of conditional-untapped work)**
- **D-06:** All six cycles get real rules: fast lands, slow lands, ELD
  threshold lands (Mystic Sanctuary class), Verge cycle, Vivid lands, Training
  Compound. Closes the classifier's documented backlog completely
  (`ManabaseClassifier.cs:479-501`).
- **D-07:** Count-based conditions evaluated per-trial in the sim (fast /
  slow / ELD: sim already tracks lands-in-play — evaluate at land-play time).
  Type-based cycles (Verge) use the static census pattern like check lands
  (≥6 matching-type rule). Vivid = ETB-tapped + limited any-color charges
  (modeling depth at planner discretion).
- **D-08:** Rides the `analysis.manabase.accuracy` bundle, ON — same lane as
  bond/check/Snarl conditional-untapped precedent. No new flag.

**MBGAP-03 — ritual land-target credit (cEDH-only, RIT O-4)**
- **D-09:** Calibration-fit weight: start ~0.5 land per net-positive ritual
  (capped), tune against the 1597-deck cEDH harness (`cedh-land-calibrate`
  CLI) until under-flag% stays calibrated — same method that set floor 22 /
  blend 0.5. The data decides the constant.
- **D-10:** New flag `analysis.manabase.ritual-land-credit`, ships OFF. Do NOT
  reuse `ritual-burst-mana` (already ON in prod — reuse would change live
  land targets the moment the code deploys). Calibrate → operator flip.

**MBGAP-04 — consistency threshold**
- **D-11:** Research spike first. This phase delivers a decision doc that
  (a) re-verifies Karsten 2022's escalation (settles EF2 L14 + the corpus
  contradiction between manabase-math.md "[H, verbatim]" and EF2
  "unconfirmed"), (b) evaluates the (85+M)% multiplayer relaxation case.
  Implement only if evidence supports — as a follow-up or a small gated plan.
  Doc contradiction fix in `docs/manabase-analysis-rules.md` regardless.

**Tier 2 — verdict polish batch (one plan)**
- **D-12:** MBGAP-05a (`Math.Ceiling(-LandDelta)` overstatement,
  `ManabaseVerdictSynthesizer.cs:63`), 05b (silent 3-line truncation → append
  "…plus N more"), 05c (`(s)` plural artifacts in synthesizer + view), 05d
  (label per-color deficit as heuristic guidance on page + .txt + swap prompt
  per the parked EF1 #4 decision). Exact copy at Claude's discretion.

**Closing tasks**
- **D-13:** MBGAP-11: re-audit `DeckFlow.Web/Help/manabase.md` line-by-line
  for overclaims (file rewritten since EF2; old line numbers dead).
- **D-14:** MBGAP-12: visual verification of tap-analyzer + mulligan lenses in
  a browser, 2 viewports (never done; UI review scored markup only).

### Claude's Discretion
- Vivid charge-counter modeling depth (D-07 note).
- Verdict-polish exact copy/wording (D-12).
- Calibration acceptance bars for D-04/D-09 flips (default: match the
  cEDH-land-target precedent — no re-opened under-flag regression, grindy
  decks stay healthy).

### Deferred Ideas (OUT OF SCOPE)
- **MBGAP-09** — cEDH castability surface (own later phase; ROADMAP backlog).
- Tier 3: MBGAP-06 scry-0.2 source credit, MBGAP-07 casual low-curve guard
  (verify with a real 1.8-MV deck first — may be a non-issue), MBGAP-08 snow
  color category, MBGAP-10 LOW sweep (L4 verify-then-fix, L5, L6, L9, L13).
- Research-corpus deliberate exclusions: X-spells (Salubrious X=3/2/1),
  landcycling, "+1 Wastes vs +1 Basic" perturbation diagnostic, cost-cheater
  toggle, Treasure stockpiling / sac-outlet engines, Chrome Mox / Spirit Guide
  imprint-exile producers, commander recast tax, rocks-removability toggle.
- The manabase engine SRP refactor (separate backlog, needs parity harness
  first); Casual/60-card land-target formula changes.

## Project Constraints (from CLAUDE.md)

- **Tech stack pinned:** ASP.NET 10 + Razor, no framework migration.
- **No new packages** without explicit user approval (none needed here).
- **Formatting:** `.editorconfig` + changed-lines gate; do not mass-reflow
  `ManabaseClassifier.cs`/`KarstenManabase.cs`/`CastabilitySimulator.cs` —
  touch only the lines that change. LF line endings enforced by
  `.gitattributes`; Codex is the top EOL-churn risk — verify `git diff --stat`
  vs `git diff --ignore-all-space --stat` after every Codex dispatch.
- **Carve-outs apply:** do not convert `{ get; init; }` to `{ get; }`; do not
  re-indent raw-string literals; preserve switch expressions and xmldoc
  single-space indent.
- **Testing:** xUnit (DeckFlow.Core.Tests / DeckFlow.Web.Tests); VSTest via
  `dotnet build` clean + targeted harness; Playwright via
  `scripts/run-web-test.sh` + `npx --no-install playwright test` (never
  open a browser on the Windows host; never rely on WSL `gstack`).
- **Delegation:** Codex implements code; Claude plans, reviews, verifies.
  Codex reviews plans too (per user's global CLAUDE.md). Every UI-touching
  change (MBGAP-05/12) needs xUnit + Playwright + 2-viewport visual review.
- **README + docs/manabase-analysis-rules.md** must be updated in the SAME
  change whenever behavior ships (explicit CONTEXT.md canonical-ref
  requirement, reinforced by global CLAUDE.md "Always update README" rule).
- **Flag rollout precedent (mandatory):** new flag ships OFF → golden-deck
  diff proves flag-off byte-identical → calibration harness run →
  `docs/manabase-analysis-rules.md` updated → operator flips in prod. This
  exact sequence must appear in the plan for D-04 and D-10.

## Phase Requirements

No formal REQUIREMENTS.md IDs are mapped to this phase (backlog/gap-closure
phase, not requirement-driven). Scope is fully defined by the MBGAP-01..05,
11, 12 item list in CONTEXT.md's `<decisions>` block, reproduced above under
User Constraints.

---

## MBGAP-01 — Conditional-restriction lands

### Oracle text (verified via WebSearch, cross-referenced against Scryfall
search UI results — MEDIUM confidence; not fetched via Context7/official API
because `api.scryfall.com` returned 403 to WebFetch in this session)

| Card | Oracle text (paraphrase-free) | Restriction shape |
|---|---|---|
| Cavern of Souls | "As this land enters, choose a creature type. {T}: Add {C}. {T}: Add one mana of any color. **Spend this mana only to cast a creature spell of the chosen type**, and that spell can't be countered." | Named creature-type-only spend |
| Unclaimed Territory | "As Unclaimed Territory enters, choose a creature type. {T}: Add {C}. {T}: Add one mana of any color. **Spend this mana only to cast a creature spell of the chosen type.**" | Named creature-type-only spend (no uncounterable clause) |
| Ancient Ziggurat | "Haste: Add one mana of any color. **Spend this mana only to cast a creature spell.**" | Any-creature-only spend (not type-restricted) |
| Nykthos, Shrine to Nyx | "{T}: Add {C}. **{2}, {T}: Choose a color. Add an amount of mana of that color equal to your devotion to that color.**" | Devotion-scaled, no hard spend restriction, but effectively worthless at low devotion |

**Detection pattern (regex, to mirror `CheckLandRegex`/`SnarlRevealRegex`
convention in `ManabaseClassifier.cs`):**
- `SpendOnlyCreatureRegex` ≈ `spend this mana only to cast a creature spell(?: of the chosen type)?` — distinguishes Cavern/Unclaimed (type-restricted, has "of the chosen type") from Ziggurat (any creature). `[ASSUMED]` — exact regex shape not yet coded; the classifier author should verify against the live Scryfall wording via the existing `ManabaseLiveOracleCanaryTests.cs` pattern before locking the regex (that canary test exists specifically to catch oracle-text rot — see H1 root-cause note in `manabase-efficacy-findings-r2.md`).
- Nykthos needs a distinct detector (no "spend this mana only" clause) —
  match on `"devotion to that color"` in the activated-ability line.

### D-03's "dominant-creature-type share" census — NOT yet built

Grepped `ManabaseClassifier.cs` for existing creature-subtype handling: only
`IsType(card.TypeLine, "Creature")` (front-face-type-line substring check)
exists today (5 call sites). **There is no existing subtype/tribal-share
census** — `CardFact.TypeLine` is the raw Scryfall string (e.g. "Legendary
Creature — Elf Druid"); nothing in the engine currently splits on the em-dash
to extract subtypes. This is genuinely new logic for D-03, not a reuse of an
existing helper. The census needed:

1. Split each creature's `TypeLine` on the em-dash (`—`) to get the subtype
   list (mirrors how a human reads "Creature — Elf Druid").
2. Build a per-deck histogram: `{subtype: count of creatures bearing it,
   weighted by Quantity}`.
3. "Dominant creature type share" = `max(histogram.Values) /
   totalCreatureCount` (or similar — planner should pin the exact formula;
   this phase's research does not mandate one, D-03 only says "weight by
   creature share of deck" for Ziggurat and "full source only for the
   dominant-creature-type share" for Cavern/Unclaimed).
4. Cavern/Unclaimed additionally need to know WHICH type is dominant, since
   their restriction is type-specific — a Cavern in an Elf-tribal deck is a
   full source; a Cavern in a 3-type deck with no dominant tribe is a heavy
   discount regardless of which type is "chosen" (the classifier cannot know
   which type the user picked at cast time — it must model the *best case*,
   i.e. assume the user names the deck's dominant type).

**Reusable machinery to model the weight itself:** the granted/conditional
source pattern (`DetectGranter`/`AddGrantedSources`,
`ManabaseClassifier.cs:1173-1276`) already has the `IsConditional=true`,
`Weight=0.25` shape that gates a source with a per-trial Bernoulli roll in
the sim (`CastabilitySimulator.cs` §4.9, `IsConditional` sources only). This
is the correct template for Nykthos's "conditional low weight" — reuse the
existing Bernoulli-gated conditional-source path rather than inventing a new
one. Cavern/Unclaimed/Ziggurat instead want a **weight scaled by a computed
deck fraction** (not a fixed 0.25) — this is closer to the existing
fetch-land weight pattern (0.67 in 3+ color decks, `Cls:347-349`), which
already demonstrates "weight computed from deck composition, not a flag."

### D-05 disclosure — exact template exists, verbatim reusable

The alt-cost marker is the precise pattern to copy (`Manabase.cshtml:697-698,
710-712`; `ManabaseModels.cs` `IsCostOverridden` bool on `CastabilityRow`):
- Add a bool (e.g. `IsRestrictedSourceUsed` or similar name at planner's
  discretion) to the castability row record.
- Render a marker span in the table cell (`manabase-override-mark`-styled,
  new CSS class if a visually distinct marker is wanted) + one explanatory
  `<p>` footnote below the table, gated on `report.Castability.Any(c =>
  c.IsRestrictedSourceUsed)` exactly like the existing `IsCostOverridden`
  gate.
- Additionally: one new `UnsupportedInteraction` entry
  (`ManabaseModels.cs:454`, `Cls:181-189` construction pattern) so the
  existing `<details>` panel (`Manabase.cshtml:655-665`) also surfaces the
  restricted-land approximation, per D-05's explicit "reuse... + entry in
  the existing unsupported-interactions panel" instruction.

---

## MBGAP-02 — Untapped-land cycles (Phase 2)

### Oracle text per cycle (verified via WebSearch — MEDIUM confidence)

| Cycle | Example | Oracle text (untapped condition) | Detection shape |
|---|---|---|---|
| Fast lands | Deathcap Glade (also Botanical Sanctum, Concealed Courtyard, Inspiring Vantage, Spirebluff Canal, etc.) | "This land enters tapped **unless you control two or fewer other lands**. {T}: Add {_} or {_}." | Count-based: `otherLandsInPlay <= 2` |
| Slow lands | Deathcap Glade cycle (SOS/VOW — Deserted Beach, Haunted Ridge, etc.) | "This land enters tapped **unless you control two or more other lands**. {T}: Add {_} or {_}." | Count-based: `otherLandsInPlay >= 2` |
| ELD threshold ("Mystic Sanctuary class") | Mystic Sanctuary, Dwarven Mine, Gingerbread Cabin, Idyllic Grange, Witch's Cottage | "This land enters tapped **unless you control three or more other [Basic Type]s**." Mystic Sanctuary's exact wording: "enters tapped unless you control three or more other Islands." | Count-based, type-scoped: `otherLandsOfNamedType >= 3` |
| Verge cycle | Floodfarm Verge (DSK) and the FDN reprint cycle | Verge lands **enter untapped unconditionally** and always tap for one fixed color; the SECOND color is conditional: "{T}: Add {W}." + "{T}: Add {U} **as long as you control a Plains or an Island**." (paraphrase — the conditional clause names two basic types) | Type-based, static census (like check lands): the land is always untapped; only whether its SECOND color is "on" depends on the census. This is a different shape than fast/slow/ELD — no timing dependency, so it fits the existing static `ConditionalUntappedTypes` census pattern almost exactly, but here it gates a *color*, not the *tapped state*. |
| Vivid lands | Vivid Meadow (LRW/C15/C17/NCC) | "Vivid Meadow enters the battlefield tapped **with two charge counters on it**. {T}: Add {W}. {T}, Remove a charge counter from this land: Add one mana of any color." | ETB tapped (always) + limited-uses any-color ability (charge-counter budget, not conditional-untapped at all) |
| "Training Compound" | **NOT FOUND** — see Open Questions #1 | Unresolved |

### D-07's architectural split — this is the important planning input

The existing conditional-untapped machinery (`IsConditionallyUntapped`,
`ManabaseClassifier.cs:1063-1071`) is **entirely static**: it runs once per
card at classification time (before any sim trial), using a deck-wide
census (`CountLandsBearingAnyType`) to decide `EntersUntapped` as a single
boolean per `ManaSource`. That boolean then becomes a fixed `CardKind`
(`UntappedLand` or `TappedLand`) at `Cls:888`, and the sim treats it
identically in every one of the 20,000 trials.

**Fast/slow/ELD cannot use this static path** — D-07 explicitly requires
per-trial evaluation because whether "you control two or fewer other lands"
is true depends on the SHUFFLE and the LAND-PLAY SEQUENCE of that specific
trial, not a deck-wide static property. This means:

1. A **new `CardKind`** (e.g. `CardKind.ConditionalCountLand`) is needed in
   `CastabilitySimulator.cs`'s private `CardKind` enum (`CS:44`), carrying
   enough metadata (min/max other-lands threshold, direction) to be resolved
   dynamically.
2. The resolution point is `PlayOneLand` (`CastabilitySimulator.cs:1194-
   1247`), which already computes `landsOnBoard` (a
   `List<(int Mask, int OnlineTurn, int Amount)>`) BEFORE deciding how to
   play a new land each turn — this is the exact per-trial land-count state
   D-07's note ("sim already tracks lands-in-play") refers to. The new land
   kind's tapped/untapped resolution should happen at the moment it is
   chosen to be played, using `landsOnBoard.Count` (all previously-played
   lands, "other lands" from the new land's own perspective) compared
   against its threshold (≤2 for fast, ≥2 for slow, ≥3-same-type for ELD).
3. ELD threshold additionally needs the type-match filter — it is
   simultaneously count-based (per-trial) AND type-scoped (only "other
   Islands" count, not any land) — this makes it the most complex of the
   three: the sim needs to know, per already-played land, which basic
   type(s) it "counts as" for the threshold check. This may require carrying
   a type-tag alongside each `landsOnBoard` entry, which the census-only
   path never needed.
4. Verge is explicitly D-07 type-based/static (like check lands) — reuse
   `ConditionalUntappedTypes`/`CountLandsBearingAnyType` directly, no new
   `CardKind`. But note Verge gates a COLOR not the tapped state (the land
   itself is always untapped) — this means the existing `IsLand`/
   `EntersUntapped` model doesn't fit either; Verge needs its second color
   either always modeled as present (optimistic, matching D-06's "closes the
   backlog" framing) or gated by the same static census used for check
   lands, applied to a color-availability flag rather than a tapped flag.
   Planner discretion on exact mechanism, but the type-share math is
   identical to `CheckLandMatchTypeThreshold` (≥6).
5. Vivid is explicitly "modeling depth at planner discretion" (D-07 note) —
   simplest compliant model: classify as `TappedLand` (ETB tapped, matches
   reality) for the untapped/tapped question, and treat the 2 charge
   counters as a capped conditional any-color source (reuse the
   `IsConditional`+Bernoulli-gate pattern from granted sources, capped at 2
   uses per game — though the sim doesn't currently model "uses remaining"
   across turns, so a simpler approximation, e.g. weight discount, may be
   more consistent with existing sim mechanics). This needs explicit
   planner discretion in the plan, not assumed.

### D-08 — rides `analysis.manabase.accuracy`, no new flag

Confirmed: the existing bond/check/Snarl conditional-untapped logic is
already bundled under `checkLandUntapped`, itself part of
`analysis.manabase.accuracy` (default ON,
`docs/manabase-analysis-rules.md` §1 intro + Feature Flag Catalog table).
D-08 says the six new cycles join this same bundle — no new flag, no
golden-diff-before-flip step needed (the bundle is already ON in prod). This
is a materially lower-risk rollout than MBGAP-01/03 (D-04/D-10's new-flag
sequence) — the plan should NOT gate MBGAP-02 behind a flag flip, but SHOULD
still run the flag-parity regression tests that guard
`analysis.manabase.accuracy`'s existing byte-identical-when-off invariant
(the bundle itself stays OFF-able; adding new logic inside it must not break
that toggle).

---

## MBGAP-03 — Ritual land-target credit (cEDH-only)

### Existing groundwork (from `manabase-ritual-burst-mana-spec.md`, already shipped)

The **castability-sim** ritual credit (`analysis.manabase.ritual-burst-mana`,
already ON in prod, flipped 2026-07-11) is fully separate from what this
phase builds. That spec's own §3.3/O-4 explicitly deferred **land-target**
credit: "Karsten's regression excludes one-shots; v1 changes castability
only, not the recommended land count... revisit under a cEDH-only credit
during calibration if the data warrants." MBGAP-03/RIT-O-4 is that deferred
follow-up.

**Detection is already built** — `DetectOneShotBurstMana`
(`ManabaseClassifier.cs:550-599`) already identifies net-positive
instant/sorcery rituals and produces an `OneShotMana` record with
`NetMana`/`Colors`/`OwnCost`. D-09's land-target credit work does **not**
need new classification — it needs a NEW consumer of the already-classified
`OneShotMana` list: a land-target credit term in `KarstenManabase`, gated to
cEDH mode and gated behind the new flag.

### D-09 — calibration-fit weight, using the exact `cedh-land-calibrate` template

The `cedh-land-target` Phase-B precedent (`manabase-cedh-land-target-
phaseB-PLAN.md`) is the exact template to follow for D-09/D-10:
- Named calibration constants in `KarstenManabase.cs` (e.g.
  `RitualLandCreditWeight = 0.5`, capped — planner to decide cap shape,
  "capped" per D-09).
- `CedhCalibrateCommandRunner.cs` (`DeckFlow.CLI`) is the literal harness to
  extend: it already replays 1597 cached cEDH decks
  (`decks_all.json`/`cards_full.json`) through the real
  `ManabaseClassifier.Classify` + `KarstenManabase.CedhLandTarget`, comparing
  old vs. new targets and reporting via `CedhCalibration.Build`/
  `RenderMarkdown`. Extending it for ritual credit means: (a) read
  `classifiedDeck`'s ritual/`OneShotMana` list (needs classifier to expose
  it — check whether `ManabaseDeck` already carries `OneShotMana` list from
  the burst-mana P1 work), (b) compute a THIRD target column
  (`newTargetWithRitualCredit`), (c) extend `CedhCalibration`'s
  report/markdown to show the delta.
- Same acceptance bar as the cEDH-land-target precedent (per Claude's
  Discretion note in CONTEXT.md): "no re-opened under-flag regression, grindy
  decks stay healthy" — i.e. re-run the 76%→22% under-flag metric with the
  ritual credit applied and confirm it doesn't regress.

### D-10 — new flag, NOT reusing ritual-burst-mana

Confirmed critical distinction: `analysis.manabase.ritual-burst-mana` is
**already ON in prod** (flipped 2026-07-11 per canonical refs + user's
MEMORY.md). Any new land-target behavior folded into that flag would change
live land targets the instant the code deploys — this is exactly the
mistake D-10 pre-empts. Use the `FeatureFlagCatalog.cs`/`FeatureFlagStore.cs`
seed-OFF template verbatim (see the `cedh-land-target` flag's own
add-pattern, `FeatureFlagStore.cs:231,271`) for
`analysis.manabase.ritual-land-credit`.

---

## MBGAP-04 — Consistency threshold (research spike)

### The contradiction, resolved

`.planning/research/manabase-math.md` §1/§2 states, with an explicit
verification caveat and confidence tags: "Karsten's published bar =
**(89 + M)%** by mana value M: 90% (1-drop) → 96% (7-drop)... **[H,
verbatim]**" and separately documents the exact escalating table (Table 1/2)
as "AUTHORITATIVE — verbatim from Karsten's TCGplayer 2022 update (fetched
via headless browser 2026-06-20)." This is the SAME source
`KarstenManabase.ConsistencyThreshold` implements (`Kar:94-98`: `pct = 89 +
Math.Max(1, manaValue)`).

`manabase-efficacy-findings-r2.md` L14 flags this as "unconfirmed against
Karsten 2022 (flat ~90%?)" — but offers no counter-evidence, just an
unresolved doubt raised during the efficacy audit. Given the manabase-math.md
capture explicitly documents its verification method (headless-browser fetch
of the primary TCGplayer source, with quality-tagged sourcing throughout the
rest of the document), **the escalating (89+M)% formula is very likely
correctly implemented already** — L14's doubt is not backed by a
contradicting citation, it is a flag raised without a resolution step ever
having been performed. `[CITED: .planning/research/manabase-math.md]` — this
research recommends the spike doc simply re-fetch the TCGplayer 2022 article
(or find an archived/cached copy) and quote the exact threshold sentence
directly into `docs/manabase-analysis-rules.md`, closing L14 as "confirmed,
no code change needed" rather than treating it as an open code bug.

**Attempted to re-verify directly in this session:** `WebFetch` to
`api.scryfall.com` returned 403 in this sandbox; a live re-fetch of the
TCGplayer article was not attempted (out of scope for research — the spike
task itself should do this, ideally via the same headless-browser method the
original capture used, since TCGplayer's Karsten article is JS-rendered and
blocks plain WebFetch, per manabase-math.md's own sourcing note).

### The (85+M)% multiplayer-relaxation case

`.planning/manabase-mode-research.md` §4 point 2 (a curated research-backed
proposal, not a primary Karsten citation) explicitly proposes: "Relax the
(89+M)% threshold for casual multiplayer... for long 4-player games you see
far more cards, so requirements can drop by 1–2 sources. Consider a casual
threshold of ~(85+M)% or a 'games run long' draw-count bonus." **This is a
DeckFlow-authored proposal reasoning from Karsten's own acknowledgment that
his thresholds are "arbitrary,"** not itself a verified Karsten number —
`[ASSUMED]`. D-11 asks the spike to evaluate this specific idea, not to
implement it outright. Key consideration for the spike doc: Karsten's
published thresholds are calibrated for 1v1 Constructed/Limited draw
patterns; DeckFlow's own draw model (`CastabilitySimulator.cs` §4.4) already
draws every turn including turn 1 because Commander is always multiplayer
(CR 103.8a doesn't apply) — this means DeckFlow's simulation ALREADY
diverges from Karsten's 1v1 assumption in the draw model, which is a
different (and arguably stronger/more-implemented) form of "multiplayer
relaxation" than lowering the consistency bar. The spike should weigh
whether a SECOND relaxation (lowering 89→85) on top of the already-more-
generous draw model would double-count the multiplayer benefit, or whether
they address different things (draw model = more cards seen; threshold =
how sure you need to be) and are legitimately additive.

### Recommendation for the spike plan
1. Re-fetch/re-quote the TCGplayer 2022 article's exact threshold language
   (headless browser method, matching the original capture) — settle L14 as
   confirmed-correct with a citation, update `docs/manabase-analysis-rules.md`
   §3.1 to remove any residual doubt.
2. Produce a reasoned recommendation on the (85+M)% multiplayer case —
   whether it's additive to the existing every-turn-draw model or redundant
   — with an explicit "implement" or "do not implement" verdict and, if
   "implement," a proposed flag name following the D-04/D-10 new-flag
   pattern (ships OFF, calibrated).
3. This is a DOCUMENT deliverable; do not write code in this phase for
   MBGAP-04 beyond the mandatory `docs/manabase-analysis-rules.md`
   contradiction fix (D-11 requires the doc fix "regardless" of the
   implementation verdict).

---

## MBGAP-05a-d — Verdict polish batch

All four anchors verified directly in `ManabaseVerdictSynthesizer.cs`:

### 05a — `Math.Ceiling(-LandDelta)` overstatement
`ManabaseVerdictSynthesizer.cs:59-64`:
```csharp
if (report.LandDelta < -1 && !report.LandShortfallCoveredByRamp)
{
    issues.Add(string.Create(
        CultureInfo.InvariantCulture,
        $"Add ~{Math.Ceiling(-report.LandDelta):F0} more land(s) - the base is short for this curve."));
}
```
`manabase-efficacy-findings-r2.md` L1 confirms: "turns 1.05 shortfall into
'add ~2 land(s)' (all surfaces consistent, all overstate ≤1). Round or show
raw delta." The identical `Math.Ceiling` pattern also appears in
`BuildColorIssue` (`VS:107`, `int shortfall = (int)Math.Ceiling(finding.
Deficit)`) — the fix should be applied consistently to both call sites
(planner to confirm both are in scope; CONTEXT.md's D-12 anchor cites only
`VS:63` but the same overstatement class exists at `VS:107` too — this
research flags it as an open question, see below).

### 05b — silent 3-line truncation
`ManabaseVerdictSynthesizer.cs:94-97`:
```csharp
if (issues.Count > 3)
{
    issues.RemoveRange(3, issues.Count - 3);
}
```
L2 confirms: "Verdict truncates to 3 lines silently... paste [artifact loses
information]." Fix per D-12: append "…plus N more" when truncating — this
must also propagate to whatever consumes `ManabaseVerdict.Lines` downstream
(the page render AND the .txt paste artifact, per the core-value "paste
artifact must not silently omit information" principle stated throughout
CLAUDE.md's project section).

### 05c — `(s)` plural artifacts
Confirmed present at MULTIPLE sites in `ManabaseVerdictSynthesizer.cs` alone:
`"source(s)"` (line 110), `"land(s)"` (line 63), `"spell(s)"` (line 119),
`"piece(s)"` (line 135). D-12 also calls out "in synthesizer + view" — the
Razor view (`Manabase.cshtml`) likely has its own independent `(s)` literals
that need a separate grep pass during planning (not verified in this
research session; flag for the plan's task list to `grep -rn "(s)\b"
DeckFlow.Web/Views/Deck/Manabase.cshtml` before finalizing the file list).

### 05d — per-color deficit labeled as heuristic guidance
This is `manabase-efficacy-findings.md` **Finding #4** (verified — CONTEXT.md
cites "EF1 #4 parked decision"): `ActualSources` (analytic weighted sum) vs
`SimRequiredSources` (mono-color isolation sim + flat bump) "measure
different objects, so the deficit is heuristic — weakest on 3-5c shared-
fixer piles." The **explicit prior decision** (quoted from the capture): "do
NOT re-base the deficit (would silently invalidate the verdict; requires a
joint multicolor-capacity model, not a calibration). Either build that joint
model deliberately or explicitly label the per-color deficit as 'heuristic
guidance.'" D-12 exercises the SECOND branch (label, don't rebuild) — this
is copy/labeling work only, confirmed no math change, across three surfaces
per CONTEXT.md: the page, the .txt artifact, and the swap prompt.

---

## MBGAP-11 — Help doc re-audit

`DeckFlow.Web/Help/manabase.md` is 138 lines (verified via `wc -l`). D-13
notes it was "rewritten since EF2; old line numbers dead" — meaning the
original `manabase-efficacy-findings.md`/`-r2.md` M12 finding ("Help/
methodology overclaims") line-citations no longer point at the same content.
This is a plain line-by-line content audit task with no code dependency —
the plan should treat it as: read the current 138 lines, cross-check every
factual claim against `docs/manabase-analysis-rules.md` (the authoritative
engine-behavior doc), flag any claim that overstates precision/certainty
beyond what the engine actually computes (echoing the M12 finding class),
and rewrite in-place.

## MBGAP-12 — Lens visual verification

Two lenses named in D-14: the tap-analyzer block and the mulligan-evaluator
block, both on the Manabase page (`Manabase.cshtml`), gated by
`analysis.manabase.tap-analyzer` and `analysis.manabase.mulligan-eval`
respectively (both default ON per the Feature Flag Catalog table in
`docs/manabase-analysis-rules.md`). D-14 states this "has never been done —
UI review scored markup only," meaning a prior UI-REVIEW pass
(`.planning/ui-reviews/manabase-UI-REVIEW.md`, cited in canonical_refs) read
the Razor/CSS statically but never rendered the page in a real browser.
Per the project's standing UI rules (global CLAUDE.md memory: "UI review
after every UI change... render+screenshot the page (2 viewports) before
done"), this task should:
1. Start the web app via `scripts/run-web-test.sh` (never opens a Windows
   browser; sets `DECKFLOW_DISABLE_AUTO_BROWSER=true`).
2. Drive Playwright (`npx --no-install playwright test`, headless, `env -u
   DISPLAY -u WAYLAND_DISPLAY`) or a manual browser against the running
   headless server.
3. Screenshot the tap-analyzer block and mulligan-evaluator block at 2
   viewports (desktop + mobile) — existing e2e specs
   `manabase-mulligan.spec.ts` already exercise the mulligan lens
   functionally; this task is specifically VISUAL verification, a different
   goal than the existing functional e2e coverage.
4. This phase's existing e2e suite (`DeckFlow.Web/e2e/manabase-*.spec.ts`,
   10 files) gives good functional-regression coverage already — MBGAP-12
   is additive visual QA, not new functional test-writing (though the
   `[Playwright]` visual-check pattern itself may warrant a lightweight new
   spec if none currently does pixel/layout assertions on these two blocks;
   verify during planning whether a visual-regression spec is warranted or
   a manual screenshot pass suffices given D-14's "never done" framing).

---

## Architecture Patterns

### System Architecture Diagram

```
decklist (Moxfield/Archidekt paste)
  │
  ▼
[1] ManabaseClassifier.Classify (Core)
    - fast/slow/ELD/Verge/Vivid/restricted-land detection (NEW, this phase)
    - existing bond/check/Snarl census (REUSE pattern)
    - ritual OneShotMana detection (EXISTING, already shipped)
  │
  ▼ ManabaseDeck (sources, spells, one-shot list)
  │
  ▼
[2] KarstenManabase.CedhLandTarget (Core)
    - existing singleton/60-card/cEDH-hybrid math
    - NEW: ritual land-target credit term (MBGAP-03, flag-gated)
  │
  ▼ recommended land count
  │
  ▼
[3] ManabaseAnalyzer per-color source requirement (Core)
    - existing Karsten + sim-clamp math
  │
  ▼
[4] CastabilitySimulator (Core, 20,000-trial Monte Carlo)
    - existing land-play priority / mulligan / ramp-deploy / ritual-burst
    - NEW: per-trial dynamic resolution for fast/slow/ELD (MBGAP-02, new
      CardKind case, resolved in PlayOneLand)
    - NEW: restricted-land conditional Bernoulli gate (MBGAP-01, reuses
      existing IsConditional pattern)
  │
  ▼ per-spell cast%, health signals
  │
  ▼
[5] ManabaseVerdictSynthesizer (Core)
    - NEW: MBGAP-05a/b/c/d wording fixes (no math change)
  │
  ▼
[6] Manabase.cshtml (Razor view) + .txt artifact + swap prompt (Web)
    - NEW: MBGAP-01 disclosure marker (D-05, reuse alt-cost template)
    - NEW: MBGAP-05 wording propagation to page/.txt/swap-prompt surfaces
    - MBGAP-12: visual verification of tap-analyzer + mulligan blocks
```

### Recommended file touch-map (not a new structure — this phase edits existing files)

```
DeckFlow.Core/Manabase/
├── ManabaseClassifier.cs        # MBGAP-01 restricted-land detection,
│                                 # MBGAP-02 fast/slow/ELD/Verge/Vivid detection
├── CastabilitySimulator.cs      # MBGAP-02 new CardKind + PlayOneLand logic,
│                                 # MBGAP-01 conditional Bernoulli gate reuse
├── KarstenManabase.cs           # MBGAP-03 ritual land-target credit term
│                                 # MBGAP-04 (only if spike verdict = implement)
├── ManabaseVerdictSynthesizer.cs # MBGAP-05a/b/c/d
└── ManabaseModels.cs            # new bool/record fields for MBGAP-01 disclosure

DeckFlow.Web/
├── Views/Deck/Manabase.cshtml   # MBGAP-01 disclosure marker, MBGAP-05c plural fixes
├── Services/FeatureFlags/
│   ├── FeatureFlagCatalog.cs    # D-04, D-10 new flag descriptions
│   └── FeatureFlagStore.cs      # D-04, D-10 seed OFF (PG + SQLite)
└── Help/manabase.md             # MBGAP-11 re-audit

DeckFlow.CLI/
└── CedhCalibrateCommandRunner.cs # MBGAP-03 D-09 calibration extension

docs/manabase-analysis-rules.md   # updated in-change for EVERY MBGAP item
```

### Pattern 1: Static census-based classification (reuse for MBGAP-01 weight math, Verge)
**What:** Compute a deck-wide count/fraction once at classification time
(before the sim runs), then bake the result into a fixed per-source property.
**When to use:** When the condition depends only on deck composition, not on
a specific trial's shuffle/sequence (check lands, Snarls, Verge, and the
weight-scaling parts of Cavern/Unclaimed/Ziggurat/Nykthos).
**Example (existing code, the template to mirror):**
```csharp
// Source: DeckFlow.Core/Manabase/ManabaseClassifier.cs:1063-1071
private static bool IsConditionallyUntapped(CardFact card, IReadOnlyList<CardFact> cards)
{
    IReadOnlyList<string> types = ConditionalUntappedTypes(card);
    return types.Count > 0 && CountLandsBearingAnyType(cards, types, card) >= CheckLandMatchTypeThreshold;
}
```

### Pattern 2: Per-trial dynamic resolution (new for MBGAP-02 fast/slow/ELD)
**What:** Defer the tapped/untapped decision to sim runtime, evaluated
against that trial's actual land-play sequence.
**When to use:** When the condition is a "lands in play" COUNT that varies
trial-to-trial based on draw order (fast/slow/ELD).
**Insertion point (existing code, the hook to extend):**
```csharp
// Source: DeckFlow.Core/Manabase/CastabilitySimulator.cs:1194-1204 (PlayOneLand)
scratchOnlineMasks.Clear();
foreach ((int Mask, int OnlineTurn, int Amount) land in landsOnBoard)
{
    if (land.OnlineTurn <= currentTurn)
    {
        scratchOnlineMasks.Add(land.Mask);
    }
}
// NEW: for a ConditionalCountLand candidate, compare landsOnBoard.Count
// (or a type-filtered subset for ELD) against its threshold HERE, at the
// moment it is chosen to enter, to decide its actual OnlineTurn.
```

### Pattern 3: Disclosure marker (reuse verbatim for MBGAP-01 D-05)
**What:** A single-character superscript marker in a table cell + one
explanatory footnote paragraph, gated on `report.Castability.Any(...)`.
**Example (existing code, copy this shape exactly):**
```razor
@* Source: DeckFlow.Web/Views/Deck/Manabase.cshtml:697-698, 710-712 *@
@c.ManaValue@if (c.IsCostOverridden)
{<span class="manabase-override-mark" title="reduced / alternative cost applied" aria-label="reduced or alternative cost applied">*</span>}
...
@if (report.Castability.Any(c => c.IsCostOverridden))
{
    <p class="manabase-help"><span class="manabase-override-mark">*</span> reduced / alternative cost applied from your overrides.</p>
}
```

### Pattern 4: New-flag rollout sequence (mandatory for D-04, D-10)
**What:** new flag seeded OFF → golden-deck-diff proves flag-off byte-
identical → calibration harness run against real data → doc update →
operator flip.
**Template (existing precedent, `ritual-burst-mana`/`cedh-land-target`):**
1. Add flag key to `FeatureFlagCatalog.cs` (description).
2. Seed `FALSE`/`0` in both `FeatureFlagStore.cs` PG and SQLite branches.
3. Add `[InlineData(...)]` assertions to `FeatureFlagStoreSeedTests.cs` and
   `FeatureFlagCatalogTests.cs`.
4. Thread the flag through `ManabaseAnalysisService` → `ManabaseAnalyzer.
   Analyze` as a trailing optional parameter (default = disabled state) so
   every existing caller stays byte-identical.
5. Extend/run the CLI calibration harness against real cached data.
6. Update `docs/manabase-analysis-rules.md` flag table + rule sections.
7. Leave the operator flip as a deferred follow-up (not part of this
   phase's definition of done — matches D-04/D-09's "calibration →
   operator flip" framing).

### Anti-Patterns to Avoid
- **Flat discount for restricted lands (D-03 explicit anti-pattern):** do
  NOT model Cavern/Unclaimed/Ziggurat/Nykthos as a fixed weight (e.g. always
  0.5) regardless of deck composition — this was explicitly rejected in
  favor of composition-gated weighting.
- **Full spend-restriction sim masks (D-03 explicit anti-pattern):** do NOT
  build a new sim mechanism that tracks "this mana can only pay for a
  creature spell" as a hard constraint inside `ColorsCoverable` — the
  locked decision is a coarser weight/discount model, not a new sim
  primitive.
- **Reusing `ritual-burst-mana` for land-target credit (D-10 explicit
  anti-pattern):** this flag is live in prod; folding land-target changes
  into it would silently move live decks' recommended land counts.
- **Static classification for fast/slow/ELD (would silently misrepresent
  reality):** classifying these as always-tapped or always-untapped (the
  two options the CURRENT static model supports) is explicitly what D-06/
  D-07 close out — a static approximation was presumably already considered
  and rejected in favor of per-trial modeling, per the explicit "Count-based
  conditions evaluated per-trial in the sim" instruction.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Conditional-source probabilistic weighting | A new per-trial dice-roll mechanism for MBGAP-01 | The existing `IsConditional=true` + Bernoulli-gate path (`CastabilitySimulator.cs` §4.9, granted-source precedent) | Already tested, already wired into the sim loop; Nykthos's "conditional low weight" fits this shape directly |
| Land-count-based conditional logic | A brand-new "count lands of type X in play" helper from scratch | Extend the existing `landsOnBoard` tracking already computed in `PlayOneLand` per-trial | The sim already maintains exactly this state every trial; duplicating it risks divergent land-count semantics between two code paths |
| Flag rollout scaffolding | Ad-hoc flag wiring | The `ritual-burst-mana`/`cedh-land-target` flag templates (catalog entry, PG+SQLite seed, `InlineData` tests, trailing-optional-param threading) | This exact 7-step sequence has shipped twice already in this codebase with a proven byte-identical-when-off guarantee |
| Calibration tooling | A new calibration CLI command from scratch | Extend `CedhCalibrateCommandRunner.cs` (already replays 1597 cached cEDH decks) | Building a parallel calibration harness would double-maintain the deck-loading/classification/gate logic that already exists |

**Key insight:** every piece of NEW machinery this phase needs (per-trial
count-based land resolution, weighted conditional sources, calibration
extension, flag rollout) has a directly analogous EXISTING implementation
already shipped in this codebase for a structurally similar problem. The
highest-risk item is the ONE genuinely new primitive: per-trial dynamic
`CardKind` resolution for fast/slow/ELD (Pattern 2 above), because it's the
only piece without a full existing template — everything else is closer to
"extend an existing pattern" than "invent a new one."

## Common Pitfalls

### Pitfall 1: Double-crediting rituals between land-target and castability
**What goes wrong:** A ritual could get credited both by the existing
`ritual-burst-mana` sim path AND the new `ritual-land-credit` land-target
term, inflating a deck's perceived resource base twice.
**Why it happens:** Both features read the same `OneShotMana` classification
output; without care, "credit this ritual toward the land target" and
"credit this ritual's burst in the sim" are not mutually exclusive by
construction (unlike FastMana vs. one-shot rituals, which the original spec
explicitly hard-excluded from each other, §2 O-2).
**How to avoid:** The ritual-burst-mana spec's own §3.3 already flagged this
class of risk generically (O-2, resolved for FastMana vs. one-shot). D-09/
D-10 should explicitly state whether a ritual counted in the land-target
credit is STILL eligible for the sim burst (probably yes — the land target
is a strategic-level number, the sim burst is tactical per-cast — but this
must be an explicit decision in the plan, not left implicit).
**Warning signs:** Calibration harness shows land-target reduction PLUS
already-elevated cast% for ritual-heavy decks stacking to an implausible
combined effect; watch for over-correction in the `cedh-land-calibrate`
before/after delta.

### Pitfall 2: ELD threshold lands need type-tagged land-in-play tracking that doesn't exist yet
**What goes wrong:** `landsOnBoard` currently stores `(Mask, OnlineTurn,
Amount)` — a COLOR mask, not a basic-land-TYPE tag. "Three or more other
Islands" cannot be evaluated from a color mask alone (a Watery Grave produces
blue but isn't an Island; conversely a colorless-producing land could still
be an Island subtype in principle, though rare in practice).
**Why it happens:** The existing sim was built for check-lands (color-based
static census), which never needed to distinguish "produces this color" from
"IS this basic type."
**How to avoid:** Either (a) extend the `landsOnBoard` tuple to carry a
type-tag bitmask alongside the color mask, or (b) resolve ELD lands
statically using the EXISTING type-census machinery (like check lands)
rather than per-trial — this may be an acceptable approximation given ELD
lands are rare in most decks and the existing `CheckLandMatchTypeThreshold`
pattern already handles "type census, not color census." The plan should
explicitly choose (a) or (b); D-07 groups ELD with fast/slow as "count-based
per-trial," implying (a), but this is more invasive than (b) and the planner
should weigh the accuracy-vs-complexity tradeoff explicitly rather than
default to the more complex option without consideration.

### Pitfall 3: The verdict-polish batch's `Math.Ceiling` pattern has more than one call site
**What goes wrong:** Fixing only `ManabaseVerdictSynthesizer.cs:63` (the
land-delta line CONTEXT.md's D-12 cites) leaves the structurally identical
`Math.Ceiling(finding.Deficit)` at line 107 (`BuildColorIssue`, the
source-short color message) unfixed, producing an inconsistent user
experience where one overstatement class is fixed and a visually-identical
one is not.
**Why it happens:** CONTEXT.md's canonical-ref line-anchor (`VS:63`) points
at one instance; the efficacy finding L1 says "all surfaces consistent, all
overstate ≤1" — implying the OTHER surfaces (including line 107) share the
SAME bug pattern and were previously already noted as consistent-but-wrong.
**How to avoid:** Grep `ManabaseVerdictSynthesizer.cs` and any sibling
surfaces (`.txt` builder, swap-prompt builder — `ManabaseReportTextBuilder`,
`ManabaseSwapPromptBuilder`) for `Math.Ceiling` before finalizing the
MBGAP-05a task's file list.
**Warning signs:** A code-review flags "fixed A, why not B" where A and B are
visually adjacent lines with the same pattern.

### Pitfall 4: "Training Compound" may not exist as a real card, blocking MBGAP-02's stated scope
**What goes wrong:** If the plan locks a task to implement detection for a
card that doesn't exist under that name, the task becomes unimplementable
and blocks the "closes the classifier's documented backlog completely"
framing of D-06.
**Why it happens:** Likely a misremembered name during an earlier research
pass (possibly confused with "Training Center," the ALREADY-shipped bond
land at `ManabaseClassifier.cs:472`, or a different, as-yet-unidentified
card/cycle).
**How to avoid:** See Open Questions #1 — resolve with the user before
planning locks the six-cycle list.

## Code Examples

### Existing check-land/Snarl detection (template for MBGAP-01/Verge census logic)
```csharp
// Source: DeckFlow.Core/Manabase/ManabaseClassifier.cs:487-502
private static readonly Regex CheckLandRegex = new(
    @"tapped unless you control (?:a|an) ([^.]+)",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

private static readonly Regex SnarlRevealRegex = new(
    @"reveal ([^.]+?) card from your hand",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

private static readonly Regex[] ConditionalTypeTemplates = { CheckLandRegex, SnarlRevealRegex };
// Future census families (Verge, Vivid, Training Compound) add one entry here.
```

### Existing granted-source conditional weighting (template for Nykthos-style low-weight modeling)
```csharp
// Source: DeckFlow.Core/Manabase/ManabaseClassifier.cs:1211-1276 (AddGrantedSources, summarized)
// Adds one ManaSource per eligible creature named "<name> (granted)",
// Produces = deck colors, Weight 0.25, IsConditional = true.
// The sim gates these with a per-trial Bernoulli roll (CastabilitySimulator.cs §4.9).
```

### Existing ritual detection (already shipped — reused, not rebuilt, for MBGAP-03)
```csharp
// Source: DeckFlow.Core/Manabase/ManabaseClassifier.cs:550-599 (DetectOneShotBurstMana, summarized)
// front-face Instant/Sorcery, unconditional Add {…} clause, net-positive mana
// over own cost, no Sacrifice clause. Returns OneShotMana{NetMana, Colors, OwnCost}.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Cavern/Ziggurat/Nykthos counted as unconditional any-color full sources | Composition-gated weight per D-03 (this phase) | This phase | Fixes M8 (efficacy r2) — currently these lands overstate color fixing in tribal-but-not-dominant-tribe decks |
| Flat 28-land cEDH floor | Curve-anchored hybrid target with commander-baseline blend | 2026-07-11 (already shipped, `cedh-land-target` flag ON) | Precedent this phase's ritual-credit work extends further |
| Ritual credit: castability only | + land-target credit (this phase, MBGAP-03) | This phase (flag OFF at ship) | Closes the RIT O-4 deferred item from the burst-mana spec |
| Check/Snarl/bond conditional-untapped only | + fast/slow/ELD/Verge/Vivid (this phase, MBGAP-02) | This phase | Closes the documented `Cls:479-501` backlog completely per D-06 |
| Verdict truncates/overstates silently | Explicit "…plus N more" + rounding fix (this phase, MBGAP-05) | This phase | Closes L1/L2 (efficacy r2) |

**Deprecated/outdated:** None — no removals in this phase, only additions
and wording fixes to already-shipped, still-current logic.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Oracle text quotes for fast/slow/ELD/Verge/Vivid/Cavern/Unclaimed/Ziggurat/Nykthos, gathered via WebSearch (not Context7/official Scryfall API, which 403'd this session) | MBGAP-01, MBGAP-02 oracle-text tables | A regex built directly from these paraphrases could mismatch the live Scryfall wording by a word or two, missing cards or false-matching unrelated text — mitigate by having the implementer verify against `ManabaseLiveOracleCanaryTests.cs`-style live data before locking regexes, exactly as the H1 root-cause note in the efficacy findings recommends generally |
| A2 | "Training Compound" is assumed to be a real, findable MTG card that this research simply failed to locate, rather than a name that should be dropped from scope | MBGAP-02 cycle list | If no such card exists, D-06's "all six cycles" framing needs a correction from the user before the plan can enumerate six real cycles — planning six cycles with one undefined blocks that task |
| A3 | The (85+M)% multiplayer-relaxation idea (manabase-mode-research.md §4) is a DeckFlow-authored proposal reasoning from Karsten's own "arbitrary threshold" admission, not a verified alternate Karsten publication | MBGAP-04 | If treated as equally authoritative to the 89+M table, the spike could recommend implementing a change not actually grounded in primary-source Karsten math |
| A4 | The exact regex/detection shape for `SpendOnlyCreatureRegex` (Cavern/Unclaimed/Ziggurat) has not been written or tested — this research describes the pattern in prose, not as compiled/verified code | MBGAP-01 | Actual card text may have subtle variations (e.g. Cavern's extra "can't be countered" clause) that a naive shared regex could mishandle if not carefully scoped per-card-class |
| A5 | "Dominant-creature-type share" formula (max-subtype-count / total-creature-count) is this researcher's proposed interpretation of D-03's prose, not a formula pinned by CONTEXT.md itself | MBGAP-01 | If the planner/user intended a different threshold or share calculation, the calibration constants (D-04 flip criteria) would be tuned against the wrong metric |

**If this table is empty:** N/A — see above, five assumptions logged.

## Open Questions

1. **Does "Training Compound" exist as a real MTG card, and if not, what card
   was actually meant?**
   - What we know: CONTEXT.md, ROADMAP, and this phase's own DISCUSSION-LOG
     all name it as one of "all six" MBGAP-02 cycles. No card by this exact
     name was found via WebSearch against Scryfall's index in this session.
     "Training Center" (a DIFFERENT, already-shipped bond land,
     `ManabaseClassifier.cs:472`) is the closest name match but is not a
     plausible candidate — it's already handled and isn't an
     untapped-conditional-cycle card of the kind D-06 describes.
   - What's unclear: whether this is a simple name typo/misremembering for
     a real card/cycle (candidates worth checking with the user: a
     "Case"-mechanic land, a different Bloomburrow/Duskmourn/Foundations
     land cycle, or a delirium/threshold-style land), or whether the
     six-cycle count in CONTEXT.md is itself slightly off.
   - Recommendation: the planner (or a pre-planning discussion turn) should
     confirm the correct card/cycle name with the user before the plan locks
     a specific implementation task for it — do not guess a substitute card
     silently, since this could implement detection for the wrong card
     entirely.

2. **Are BOTH `Math.Ceiling` overstatement sites (VS:63 land-delta AND
   VS:107 color-source-short) in scope for MBGAP-05a, or only the one
   CONTEXT.md's D-12 anchor cites?**
   - What we know: L1 (efficacy r2) says "all surfaces consistent, all
     overstate ≤1," implying multiple surfaces share the bug; D-12's
     anchor cites only line 63.
   - What's unclear: whether D-12's narrower anchor is deliberate scope-
     limiting or just the most-prominent example.
   - Recommendation: default to fixing both (consistency argument from L1
     is compelling), but flag it as a scope question for Claude's Discretion
     per CONTEXT.md's own framing ("Exact copy at Claude's discretion").

3. **Should Verge lands' second-color gate use the census-based "always
   works" optimistic model (matching D-06's "closes the backlog" framing) or
   the stricter "gated like a check land" model?**
   - What we know: D-07 says Verge uses "the static census pattern like
     check lands (≥6 matching-type rule)" — this settles the mechanism.
   - What's unclear: nothing structural, but the exact `CardKind`/model
     representation (does the sim need a new "partial-color-availability"
     concept, since the land is never tapped but sometimes only produces
     one of its two colors?) is left to implementation and should be spelled
     out explicitly in the plan rather than assumed identical to the
     tapped/untapped binary the census currently drives.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`), Playwright (`DeckFlow.Web/e2e/*.spec.ts`) |
| Config file | `DeckFlow.sln` (xUnit via `dotnet test`); Playwright config under `DeckFlow.Web/` (existing e2e harness) |
| Quick run command | `dotnet build DeckFlow.sln` (clean 0/0 warnings gate) then `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~Manabase"` |
| Full suite command | `dotnet test DeckFlow.sln` (both test projects) + `scripts/run-web-test.sh` then `npx --no-install playwright test` for e2e |

### Existing manabase test inventory (verified via Glob, 2026-07-12)

**`DeckFlow.Core.Tests/Manabase/`** (20 files):
`AvatarManabaseRegressionTests.cs`, `CedhCalibrationTests.cs`,
`CedhLandBaselineTests.cs`, `CedhLandTargetHybridTests.cs`,
`KarstenManabaseCastabilityTests.cs`, `ManabaseAnalyzerCoverageTests.cs`,
`ManabaseAnalyzerRampNamesTests.cs`, `ManabaseAnalyzerTests.cs`,
`ManabaseClassifierCoverageTests.cs`, `ManabaseClassifierTests.cs`,
`ManabaseHealthVerdictTests.cs`, `ManabaseLiveOracleCanaryTests.cs`,
`ManabaseMulliganEvaluationTests.cs`, `ManabasePrimaryFixTests.cs`,
`ManabaseRampDrawBucketTests.cs`, `ManabaseRampDrawBudgetTests.cs`,
`ManabaseReportAvgOnCurveTests.cs`, `ManabaseReportTextBuilderMulliganTests.cs`,
`ManabaseReportTextBuilderTests.cs`, `ManabaseSwapPromptBuilderTests.cs`,
`ManabaseTapAnalysisTests.cs`, `ManabaseVerdictSynthesizerTests.cs`.

**`DeckFlow.Web.Tests/Manabase/`** (11 files + fixtures):
`CedhLandBaselineProviderTests.cs`, `ManabaseAnalysisServiceTests.cs`,
`ManabaseCastChipContrastTests.cs`, `ManabaseControllerDownloadTests.cs`,
`ManabaseControllerFlagGateTests.cs`, `ManabaseControllerModeTests.cs`,
`ManabaseCostOverrideParserTests.cs`, `ManabaseDisplayTests.cs`,
`ManabaseFlagBaselineHarness.cs` (manual, gated on
`DECKFLOW_MANABASE_HARNESS=1`, never runs in CI),
`ManabaseHealthBandRegressionTests.cs`, `ManabaseViewModelTests.cs`,
`ManabaseViewRenderTests.cs`. Plus 8 cached `.manabase-*-facts.json` fixture
files (Scryfall-response snapshots avoiding live API hits in tests).

**`DeckFlow.Web/e2e/`** (10 manabase specs):
`manabase.spec.ts`, `manabase-castability.spec.ts`,
`manabase-cedh-commander-display.spec.ts`,
`manabase-commander-callout.spec.ts`, `manabase-download.spec.ts`,
`manabase-headerless-commander.spec.ts`, `manabase-mulligan.spec.ts`,
`manabase-primer-ui.spec.ts`, `manabase-ramp-disclosure.spec.ts`,
`manabase-verdict.spec.ts`, `print-manabase-results.spec.ts`.

`ManabaseLiveOracleCanaryTests.cs` deserves special note: it exists
specifically to catch oracle-text drift against live Scryfall wording (per
the H1 root-cause note in `manabase-efficacy-findings-r2.md`, "a 2024
rewording rotted a core predicate for ~a year with green tests"). Any new
oracle-text regex added in this phase (MBGAP-01/02) should add a
corresponding canary assertion in this file.

### Phase Requirements → Test Map

No formal REQ-IDs (backlog phase); mapping by MBGAP item instead:

| MBGAP | Behavior | Test Type | Automated Command | File Exists? |
|-------|----------|-----------|-------------------|-------------|
| 01 | Cavern/Unclaimed/Ziggurat/Nykthos composition-gated weighting | unit | `dotnet test --filter FullyQualifiedName~ManabaseClassifierTests` | ✅ (extend existing) |
| 01 | Flag-off byte-identical (`restricted-lands`) | unit | `dotnet test --filter FullyQualifiedName~ManabaseFlagBaselineHarness` or a new parity test | ❌ Wave 0 — new flag needs its own parity test, mirroring `ritual-burst-mana`'s pattern |
| 01 | Disclosure marker renders | view/e2e | Playwright, extend `manabase-ramp-disclosure.spec.ts` (closest existing analog) or new spec | ❌ Wave 0 — no existing spec targets restricted-land disclosure |
| 02 | Fast/slow/ELD per-trial resolution | unit | `dotnet test --filter FullyQualifiedName~KarstenManabaseCastabilityTests` or new `ConditionalCountLandTests` | ❌ Wave 0 — new CardKind needs new test file |
| 02 | Verge/Vivid classification | unit | `dotnet test --filter FullyQualifiedName~ManabaseClassifierTests` | ✅ (extend existing) |
| 02 | `analysis.manabase.accuracy` bundle stays byte-identical off | unit | Existing accuracy-flag parity tests (verify which file currently covers this — `ManabaseFlagBaselineHarness.cs` docstring references "Phase-70 flag baseline") | ✅ (extend existing) |
| 03 | Ritual land-target credit calculation | unit | `dotnet test --filter FullyQualifiedName~CedhLandTargetHybridTests` or new `RitualLandCreditTests` | ❌ Wave 0 — new credit term needs new/extended test file |
| 03 | Calibration harness produces sane delta | manual (CLI) | `dotnet run --project DeckFlow.CLI -- cedh-land-calibrate --data ... --baseline ...` | ✅ (extend existing runner) |
| 03 | New flag seed/catalog parity | unit | `dotnet test --filter FullyQualifiedName~FeatureFlagCatalogTests|FeatureFlagStoreSeedTests` | ✅ (extend existing) |
| 04 | N/A (research spike, no code by default) | manual-only | Doc review only | — |
| 05a-d | Verdict wording (rounding, truncation, plural, labeling) | unit | `dotnet test --filter FullyQualifiedName~ManabaseVerdictSynthesizerTests` | ✅ (extend existing) |
| 05 | Page/txt/swap-prompt propagation | unit + e2e | `dotnet test --filter FullyQualifiedName~ManabaseReportTextBuilderTests|ManabaseSwapPromptBuilderTests`, extend `manabase-verdict.spec.ts` | ✅ (extend existing) |
| 11 | Help doc accuracy | manual-only | Manual content review against `docs/manabase-analysis-rules.md` | — (no automated test for markdown prose accuracy) |
| 12 | Visual verification, 2 viewports | manual (Playwright screenshot) | `npx --no-install playwright test` + manual screenshot review | ✅ existing specs give functional coverage; visual/pixel check is additive |

### Sampling Rate
- **Per task commit:** `dotnet build DeckFlow.sln` (0 warnings/errors) +
  targeted `dotnet test --filter FullyQualifiedName~Manabase` for the
  touched area.
- **Per wave merge:** Full `dotnet test DeckFlow.sln` (both test projects)
  + `scripts/run-web-test.sh` + `npx --no-install playwright test` for any
  wave touching `Manabase.cshtml` or flag wiring.
- **Phase gate:** Full suite green + `cedh-land-calibrate` harness re-run
  with a documented before/after delta (MBGAP-03) before
  `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] New unit test file for the per-trial `ConditionalCountLand` `CardKind`
      (fast/slow/ELD) — no existing file covers this new sim primitive.
- [ ] New/extended unit test file for the ritual land-target credit term in
      `KarstenManabase` (extend `CedhLandTargetHybridTests.cs` or add a
      sibling file).
- [ ] New flag-parity test for `analysis.manabase.restricted-lands` (mirror
      the existing `ritual-burst-mana`/`cedh-land-target` byte-identical-off
      pattern already proven in `ManabaseAnalysisServiceTests.cs`).
- [ ] New flag-parity test for `analysis.manabase.ritual-land-credit` (same
      pattern).
- [ ] New/extended Playwright spec for the MBGAP-01 disclosure marker (no
      existing spec targets restricted-land rows specifically; closest
      analog is `manabase-ramp-disclosure.spec.ts`).
- [ ] `ManabaseLiveOracleCanaryTests.cs` — add canary assertions for every
      new oracle-text regex this phase introduces (fast/slow/ELD/Verge/
      Vivid/restricted-land), per the H1 root-cause-prevention convention
      already established in this file.

## Security Domain

Not applicable — this phase is pure deck-analysis math/classification logic
and Razor-view rendering of already-user-supplied deck data (no new input
surface, no auth/session/crypto changes, no new external HTTP calls beyond
what the classifier already consumes from Scryfall via the existing pipeline).
No ASVS categories are newly triggered by this phase's scope.

## Sources

### Primary (HIGH confidence — read directly from repo source)
- `DeckFlow.Core/Manabase/ManabaseClassifier.cs` (1563 lines, read in full
  relevant sections) — conditional-untapped machinery, granted-source
  pattern, ritual detection, creature-type handling (absence confirmed)
- `DeckFlow.Core/Manabase/KarstenManabase.cs` (255 lines, read in full) —
  land-target math, cEDH hybrid target, consistency threshold formula
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` (relevant sections read:
  1140-1260, `CardKind` grep) — land-play priority, `landsOnBoard` tracking
- `DeckFlow.Core/Manabase/ManabaseVerdictSynthesizer.cs` (196 lines, read in
  full) — MBGAP-05a/b/c/d exact anchors
- `docs/manabase-analysis-rules.md` (325 lines, read in full) —
  authoritative engine-behavior reference, "code wins" caveat
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (read in full)
- `DeckFlow.CLI/CedhCalibrateCommandRunner.cs` (read in full) — calibration
  harness template for D-09
- `.planning/phases/manabase-research-gap-closure/CONTEXT.md` (locked
  decisions D-01..D-14)
- `.planning/captures/manabase-ritual-burst-mana-spec.md` (read in full)
- `.planning/captures/manabase-cedh-land-target-phaseB-PLAN.md` (read
  relevant sections) — calibration/flag-rollout template
- `.planning/captures/manabase-efficacy-findings-r2.md` /
  `manabase-efficacy-findings.md` (grepped for M8/L1/L2/L14/Finding#4)
- `.planning/research/manabase-math.md` (336 lines, read in full) —
  Karsten threshold sourcing, confidence tags
- Test file inventory via Glob (`DeckFlow.Core.Tests/Manabase/`,
  `DeckFlow.Web.Tests/Manabase/`, `DeckFlow.Web/e2e/*manabase*`)

### Secondary (MEDIUM confidence — WebSearch, cross-checked against multiple results)
- Fast/slow land oracle text (mtg.fandom.com Fast/Slow land wiki pages,
  Scryfall search-result summaries)
- Mystic Sanctuary (ELD) oracle text — Star City Games / Card Kingdom /
  Scryfall card-page search results, consistent across sources
- Verge cycle (Floodfarm Verge, DSK) — MTG Salvation "Guide to Lands in
  Standard" article
- Vivid Meadow oracle text — Scryfall card-page search results
- Cavern of Souls / Unclaimed Territory / Ancient Ziggurat oracle text —
  MTGAssist rulings pages
- Nykthos, Shrine to Nyx oracle text — Scryfall/Gatherer/CasualPlaneswalker
  search results, consistent across sources

### Tertiary (LOW confidence / unresolved)
- "Training Compound" — WebSearch found NO matching card under this exact
  name; flagged as Open Question #1, not assumed resolved.

## Metadata

**Confidence breakdown:**
- Standard stack / engine architecture: HIGH — read directly from source,
  cross-referenced against the actively-maintained `docs/manabase-analysis-
  rules.md`.
- MTG oracle-text facts: MEDIUM — WebSearch cross-checked across 2-3
  independent sources per card/cycle, but not fetched from Context7 or the
  official Scryfall API (which returned 403 in this session); implementer
  should re-verify exact wording before finalizing regexes, per the existing
  `ManabaseLiveOracleCanaryTests.cs` convention.
- "Training Compound" card identity: LOW/UNRESOLVED — could not be found;
  explicit open question, not silently assumed away.
- MBGAP-04 threshold contradiction resolution: MEDIUM-HIGH — resolved by
  direct comparison of two in-repo research documents' own sourcing/
  confidence tags, not by re-fetching the primary Karsten source in this
  session (WebFetch to Scryfall API 403'd; TCGplayer's JS-rendered article
  was not attempted via headless browser in this research pass — the spike
  task itself should do this fetch).

**Research date:** 2026-07-12
**Valid until:** 30 days (stable engine codebase; MTG oracle text is
essentially permanent once printed, so the card-text portions of this
research do not expire on a calibration timescale — only the "Training
Compound" open question and any new-set reprints of these cycles could
change this).

---

## Addendum — Open Questions Resolved (LEAD verification, 2026-07-12)

### Q1 RESOLVED: "Training Compound" is real — MSH (Marvel Super Heroes, 2026-06-26) 5-card allied cycle

Verified live against Scryfall API (post-cutoff set; WebSearch missed it):

| Card | Colors |
|------|--------|
| Gleaming Bastion | W/U |
| Hidden Lair | U/B |
| Dark Fortress | B/R |
| Training Compound | R/G |
| Gathering Place | G/W |

Oracle (identical across cycle, colors vary):
```
{T}: Add {C}.
{T}: Add {R} or {G}. Activate only if this land entered this turn or if you control a basic land.
```

**Key modeling facts:**
- **Always enters UNTAPPED** — no tapped clause at all. This cycle is NOT a conditional-untapped problem; it is a conditional-COLOR problem (like Verge).
- Colorless {C} is unconditional; the two allied colors are gated on "entered this turn OR you control a basic land".
- **Detection pattern:** oracle contains `Activate only if this land entered this turn or if you control a basic land` (exact clause, 5 cards only as of 2026-07).
- **Recommended model (static census, Verge-lane):** count basic lands in deck; if `basics >= threshold` (reuse `CheckLandMatchTypeThreshold`-style constant), treat as untapped allied dual + colorless; else treat as untapped colorless-only (or low conditional weight for the colored half). "Entered this turn" makes the colored half available the turn it's played even with zero basics, so the census under-counts slightly in its favor — acceptable, matches check-land precedent conservatism.

### Q2 RESOLVED (LEAD decision): both `Math.Ceiling` overstatement sites in scope for MBGAP-05a

`ManabaseVerdictSynthesizer.cs:63` (CONTEXT-cited) AND the second site the researcher found (~VS:107) — same defect class, fixing one and not the other leaves the same overstatement in a different sentence. Planner: cite both file:line anchors in the plan task.

### Q3: Verge sim data-model shape → planner discretion

CONTEXT.md D-07 already grants modeling-depth discretion. Constraint: Verge is always-untapped + conditionally-colored (same family as the MSH cycle above) — model the color gate, not tapped-ness.
