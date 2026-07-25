---
task: manabase-tier3-minors
type: quick
created: 2026-07-14
source: .planning/captures/manabase-backlog-2026-07-13.md §3 + .planning/captures/manabase-efficacy-findings-r2.md (L-items) + .planning/research/manabase-math.md:117-131 (Karsten credits)
branch: quick/manabase-tier3-minors
worktree: /mnt/c/users/chrislunt/source/personal/deckflow-quick-tier3
status: planned
---

# Quick Task: Manabase tier-3 research minors (MBGAP-06, MBGAP-08, MBGAP-10)

## Objective

Close the remaining tier-3 research-vs-implementation gaps in the manabase engine:

1. **MBGAP-10 LOW sweep** — L4 (`ReserveGenericForRamp`, verify-then-fix: empirical
   test decides keep-and-fix-rationale vs delete), L5
   (Karsten ceiling on-play/draw mismatch for singleton), L9 (`DetectGranter` misses
   singular "legendary creature you control" — Relic of Legends), L13 (land-ramp sim
   library-thinning gap — document only).
2. **MBGAP-06** — cheap scry-1 effect ≈ **0.2 source** credit (Karsten,
   `manabase-math.md:120`), flag-gated.
3. **MBGAP-08 + L6 combined** — snow ({S}) and true-colorless ({C}) as first-class
   color categories (Karsten treats colorless/snow "as its own color",
   `manabase-math.md:128-129`; L6: {C} pips currently folded into generic), flag-gated.

Behavioral changes that alter live numbers without a bug being fixed MUST be flag-gated
with byte-identical flag-off parity (established pattern: `ritual-land-credit`,
`plan-presence`). Bugfixes (L5, L9) ship unflagged. Dead-code removal (L4) must prove
zero behavior change. L13 is documentation only.

## Context (verified facts from reconnaissance, 2026-07-14)

- `CastabilitySimulator.cs:1498-1537` — `ReserveGenericForRamp` definition; sole call
  at `:1273`. **Order of operations (per gpt-5.5 plan review, verified): the reserve
  runs BEFORE the castability check** — reserve at `:1273`, then `ApplyRitualBurst`
  can mutate the same pool at `:1284`, then `TotalMana` check at `:1289`. So the
  original "provably dead" claim (efficacy finding L4) is NOT established: deleting
  the reserve can change ritual-burst outcomes when ramp was deployed this turn and
  rituals bridge the remaining gap. No config/test/doc references to the method.
- `KarstenManabase.cs:216-270` — `CastConsistency(…, bool onPlay = true)`;
  `CardsSeenByTurn(mv, onPlay)` = `7 + (onPlay ? turn-1 : turn)`. `SourcesNeeded`
  (`:285-293`) passes it through. The Karsten ceiling lives INSIDE `SimRequiredSources`
  at `ManabaseAnalyzer.cs:910` (called from `BuildColorFindings` at `:706`), currently
  using the `onPlay` default (true), while the Commander sim re-baselines at 7+T
  (draw). Result: singleton ceiling occasionally ~1 source too high. **The path is
  shared with 60-card analyses — fix must be conditional on singleton.**
- `ManabaseClassifier.cs:1834-1870` — `DetectGranter`. TWO gates miss Relic of
  Legends ("{T}, Tap an untapped legendary creature you control: Add one mana of any
  color."): (a) the entry gate at `:1853` requires `"{t}: add"` or `have "{t}` —
  Relic's ability text is `"{t}, tap …: add"`, so it never reaches the scope match;
  (b) the scope match at `:1859` accepts only plural "legendary creatures you
  control". Both must be extended. `docs/manabase-analysis-rules.md` §1.5 documents
  granter support.
- `ManabaseClassifier.cs:270-287` — land-ramp sim adds fetched land as delayed
  non-land source without removing a land from library density (no thinning).
  Documented behavior gap only (offsetting approximations) — per finding L13, code
  change out of scope.
- `CastabilitySimulator.cs:187-195` — `ColorBit`: WUBRG = bits 0-4; colorless mask 0;
  true {C} pips folded into generic (`:181-192`). No snow awareness anywhere;
  `ManaCost.cs:141-151` folds {S} into colorless.
- No scry detection exists anywhere. Fractional credit precedents: dork 0.5, rock 0.75,
  basic-fetch 0.67 (`ManabaseClassifier.cs:1057-1080`).
- Flags: `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:175-248` — public
  const string keys `analysis.manabase.*`, read via `IsFlagOn`, fail-safe OFF when row
  missing. Follow the `ritual-land-credit` plumbing end-to-end (flag const → threading
  into Core call → seed row → admin flags description → README + docs).
- Parity test pattern: `Build_NullMulligan_OutputByteIdenticalToOverloadWithoutMulliganParam`
  style — flag-off output byte-identical to pre-change.

## Tasks

### Task 1 (Ticket A) — MBGAP-10 LOW sweep: L4 + L5 + L9 + L13

**L4 — verify-then-fix `ReserveGenericForRamp` (deletion NOT pre-authorized).**
- The efficacy finding claimed deadness; plan review REFUTED that: the reserve runs at
  `:1273` before `ApplyRitualBurst` (`:1284`) and the `TotalMana` check (`:1289`), so
  its pool mutation can be observable through the ritual-burst bridge.
- Do BOTH steps: (1) write a targeted unit test exercising the interaction — ramp
  deployed this turn + one-shot rituals that bridge the remaining mana gap — and
  determine empirically whether removing the reserve changes the sim outcome.
  (2a) If observable: KEEP the method; fix its rationale instead — replace any comment
  attributing a standalone ~7-pt effect with an accurate description of its real role
  (reserving generic before ritual burst), and keep the new interaction test as a
  regression guard. (2b) If genuinely unobservable even through ritual burst (prove
  why in SUMMARY — e.g. gating makes the branch unreachable with rituals present):
  delete method + call site + newly-unused locals.
- Acceptance: written proof in SUMMARY for whichever branch taken; full
  `DeckFlow.Core.Tests` suite green; if (2b), zero assertion changes.

**L5 — singleton Karsten ceiling uses draw baseline.**
- Exact site: `ManabaseAnalyzer.cs:910` inside `SimRequiredSources` — change the
  `SourcesNeeded(...)` call to pass `onPlay: !isSingleton` (thread `isSingleton` in if
  not already in scope). The path is shared with 60-card analyses: on-play default
  MUST remain in effect for non-singleton. No other call sites change.
- Acceptance: new unit test demonstrating a singleton case where the ceiling drops by
  ~1 vs before (assert exact new value) AND a 60-card case asserting unchanged value;
  existing tests updated only where expected values legitimately shift — list every
  changed expectation in SUMMARY with the arithmetic justification.

**L9 — granter detection: Relic of Legends pattern.**
- TWO fixes in `DetectGranter`: (a) entry gate `:1853` — additionally accept the
  tap-plus-additional-cost form (`"{t}, tap"` … `: add`, i.e. the ability's cost
  includes tapping other permanents); (b) scope match `:1859` — accept singular
  "legendary creature you control" alongside the existing plural. Keep matches
  ordinal/lowercase per existing style; do not loosen so far that non-mana tap-cost
  abilities match (require the ": add" mana-production anchor).
- Acceptance: unit test with Relic of Legends' real oracle text ("{T}, Tap an
  untapped legendary creature you control: Add one mana of any color.") classifying
  it as a legendary-scoped granter; negative test for a non-mana "{T}, Tap …" ability;
  no regression on existing granter tests (plural forms, equipment/aura paths).

**L13 — document land-ramp thinning gap.**
- `docs/manabase-analysis-rules.md` §1.6: add explicit "Known approximation" note —
  fetched land enters as a delayed source but the library is not thinned; the two
  errors partially offset. Add a matching `// Why:` comment at
  `ManabaseClassifier.cs:270-287`. No code behavior change.

### Task 2 (Ticket B) — MBGAP-06 scry-0.2 source credit (flag `analysis.manabase.scry-credit`)

- Detection (classifier): non-land spells with mana value ≤ 2 whose oracle text
  contains a scry effect (`scry N`, N ≥ 1, case-insensitive; exclude reminder-text-only
  matches per existing reminder-stripping precedent H2/M4). Lands are EXCLUDED
  (scry-lands already count as full sources — no double credit).
- Credit (analyzer): each detected card adds **0.2 of an any-color source** to the
  per-color effective source counts used by Karsten requirement checks (NOT to the
  land-count target). Multiple copies stack linearly. No cap (Karsten gives none) —
  but surface the count.
- **Implementation lane (per plan review): do NOT model the credit as `ManaSource`
  rows.** `EffectiveSources` sums `deck.Sources` (`ManabaseAnalyzer.cs:1057`) but the
  simulator converts non-land sources into ramp cards (`CastabilitySimulator.cs:964`)
  — a 0.2 pseudo-source would leak into sim/tap/ramp outputs. Instead carry an
  analyzer-only aggregate (e.g. `ScryCreditSources` on the classified-deck result)
  and add it inside the per-color effective-source computation when the flag is on.
  The castability simulator is untouched by this task.
- **Draw+scry stacking is INTENTIONAL:** a cheap cantrip that draws AND scries gets
  both the existing ramp/draw land-target adjustment (−0.28·count lane,
  `ManabaseClassifier.cs:226`) and the 0.2 source credit — Karsten's corpus derives
  these from different mechanisms (land count vs color-source count). State this in
  the doc update and the disclosure line so the math is auditable.
- Flag threading mirrors `ritual-land-credit`: const key in
  `ManabaseAnalysisService`, param default preserving old behavior, seed row
  (seed default ON per house pattern `e0042d89`; prod row flip is operator action),
  admin flag description, README + `docs/manabase-analysis-rules.md` §1.2/§3 update.
- Disclosure: when flag on and credit non-zero, one line in the sources/requirements
  surface naming the credit (mirror the ritual-credit breakdown-line precedent
  `75585b73`) so the math is auditable on-page and in the .txt artifact.
- Acceptance: unit tests for detection (scry spell yes; scry land no; MV 3 no;
  reminder-text-only no), credit arithmetic (N copies → 0.2·N), and byte-identical
  flag-off parity on a representative analysis output.

### Task 3 (Ticket C) — MBGAP-08 + L6: colorless {C} + snow {S} categories (flag `analysis.manabase.colorless-snow`)

- `ManaCost.cs`: **additive parsing only (parity-critical, per plan review).**
  `MapSymbol` and the existing `Pips`/`ManaColor` mapping stay byte-for-byte as-is —
  {C} and {S} keep mapping to `ManaColor.Colorless` unconditionally (a new enum value
  would leak into `EnumerateUsedColors` at `ManabaseAnalyzer.cs:1038` and break
  flag-off parity). Add NEW members instead: distinct counts for true-{C} pips and
  {S} pips on the parsed cost. Downstream consumption of the new fields is gated by
  the flag; flag-off consumers never read them.
- Source capability fields: add `IsSnow` and `ProducesColorless` to `ManaSource`
  (`ManabaseModels.cs:26`), populated during classification from `CardFact.TypeLine`
  / oracle production — do NOT infer snow later from source names (`CardFact` is
  gone after classification, per plan review).
- `CastabilitySimulator.cs`: extend the color mask with bit 5 = produces-colorless
  (payable against {C} pips) and bit 6 = snow source (payable against {S} pips),
  derived from the new `ManaSource` fields. Note the sim currently DROPS
  colorless pips entirely at `:894` — the gated path replaces that drop with a
  bit-5 requirement for true {C} (while {S} pips become bit-6 requirements);
  the ungated path keeps the drop exactly as today. Generic pips remain payable
  by anything — unchanged. Snow permanents get bit 6 in addition to their color
  bits.
- Analyzer: when the deck has {C} or {S} pips and flag is on, emit per-category
  source-requirement rows (Karsten table treats colorless/snow as own color —
  Thought-Knot 10 colorless sources/60; Astrolabe 14 snow/60; scale per existing
  singleton scaling) and count the deck's qualifying sources.
- All of it behind the flag; flag OFF → parsing may compute new fields but every
  output (page, .txt, swap prompt, verdict) stays byte-identical.
- Seed default ON per house pattern; operator flips prod row.
- Docs: `docs/manabase-analysis-rules.md` §1.2 (source categories), §3 (per-color
  requirements), §4 (sim mask) updated; README feature bullet.
- Acceptance: unit tests — {C} pip payable only by colorless producers in sim;
  {S} payable only by snow sources; Warping Wail-style cost fails when no colorless
  producers exist and succeeds with Wastes/Sol Ring-style sources; snow-covered land
  counts toward both its color and snow; flag-off byte-identical parity test;
  requirement-row rendering test.

## Verification

- `dotnet build` clean (Windows dotnet.exe per WSL convention) incl. test projects.
- Full `DeckFlow.Core.Tests` + `DeckFlow.Web.Tests` suites green after each ticket.
- EOL churn check per Codex dispatch (git diff --stat vs --ignore-all-space).
- Playwright manabase specs + 2-viewport screenshots (rows added to results surface).
- Blind foreman-verifier pass against this PLAN.md.
- /simplify on the accumulated diff before commit finalization.

## Out of scope

- Any casual/60-card land-target formula change; SRP refactor; L1-L3, L7, L8,
  L10-L12, L14; X-spells/landcycling/Treasure exclusions (backlog §6); UX LOW 8-10.
