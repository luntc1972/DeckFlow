---
slug: manabase-too-optimistic
status: resolved
trigger: "Manabase analyzer reports over-optimistic castability (96% / Solid) and misses free/alternative-cost cards (Force of Negation, Fierce Guardianship, Deflecting Swat, Flawless Maneuver)"
created: 2026-06-24
updated: 2026-06-24
---

# Debug: Manabase analyzer too optimistic + misses free-cost cards

## Symptoms

- **Expected:** Manabase verdict for the Avatar (Sokka/Aang) Jeskai deck should match an independent baseline. Salubrious Snail reports **89.1%** cast rate and flags **white** as weakest color. Free/alt-cost cards (Force of Negation, Fierce Guardianship, Deflecting Swat, Flawless Maneuver) should be recognized as free/alternative-cost.
- **Actual:** DeckFlow reports **96% avg on-curve / "Solid" health** — ~7 pts more optimistic than Snail. Only "2 cards approximated/skipped" flagged; the four free-cost cards above are NOT recognized (analyzed at printed cost 2U/3U/1U).
- **Errors:** None — wrong numbers, not a crash.
- **Timeline:** Ongoing through Phase 70 manabase-accuracy work. Surfaced when comparing DeckFlow output to Salubrious Snail on the same decklist.
- **Reproduction:** Analyze the Avatar deck (100 cards, 31 lands) in the manabase tool. Full decklist captured in the originating session.

## Prior investigation (this session — codebase map + web research vs 17Lands/Karsten/ManaTuner)

Web sources: blog.17lands.com/posts/manabase-evaluator (primary), github.com/gbordes77/manatuner-pro (primary), scrollvault.net, medium @schulze.mtg, canadianhighlander.ca, gist teryror/881d60e0. Best-practice tools deliberately do the OPPOSITE of DeckFlow on the optimism levers (no mulligan / no extra-draw / ramp only when itself castable).

Five suspected defects, prioritized:

- **P1 — Grace window (biggest, ~5-10 pts).** `CastabilitySimulator.cs:559-564,421-446`. Spell counts "castable" if it lands up to **3 turns late** (1-2 drops), 2 late (3-5), 1 late (6+). 17Lands models strict on-curve or +1 max. CONFIRM pt-impact before editing.
- **P2 — Alt-cost detection too narrow.** `ManabaseClassifier.cs:728-731`. `DetectSelfCost` regex only matches `"rather than pay this spell's mana cost"`; misses `"you may pay [cost] instead of paying"` and the "free if you control your commander" cycle → Force of Negation / Fierce Guardianship / Flawless Maneuver / Deflecting Swat analyzed at printed cost.
- **P3 — Alt-cost suggestion-only.** `ManabaseClassifier.cs:215-217`, `ManabaseAnalyzer.cs:60`. Detected alt costs land in `CostSuggestions` but are NOT auto-applied; headline 96% + per-card cast% use printed cost unless user manually overrides.
- **P4 — Ramp treated as always-available.** `CastabilitySimulator.cs:499-507` (`TryDeployRamp`). Drawn ramp credited as deployable with optimal sequencing; 17Lands only credits a ramp source once the ramp itself was castable first. Likely tied to `land-ramp-sim` flag (ON).
- **P5 — Re-baseline + regression.** After fixes, re-run vs `.planning/phases/70-manabase-accuracy-mana-quantity/70-flag-baseline.html`, target ~89% to match Snail; lock a regression test asserting this deck lands in Workable/Needs-work band, not Solid.

Flags ON: `manabase.source-mana-quantity`, `ramp-credit-v2`, `color-aware-mulligan`, `land-ramp-sim` (`FeatureFlagStore.cs:160-163`).

**Direction note:** P2/P3 cut OPPOSITE to P1/P4. Fixing free-spell detection makes those 4 cards look EASIER (correct); P1/P4 reduce overall inflation. Two related defects, not one.

Key files: `DeckFlow.Core/Manabase/{CastabilitySimulator,ManabaseClassifier,ManabaseAnalyzer,KarstenManabase,ManabaseModels,ManabaseDisplay}.cs`.

## Current Focus

- hypothesis: REFINED + PARTIALLY DISPROVEN — the specified P4 fix (gate ramp on its OWN colored cost) is implemented, builds clean, is correct in principle, but is a NO-OP for the Avatar fixture (96%→96%) because this deck's ramp is almost entirely COLORLESS rocks/land-ramp whose colored cost is trivially payable. The −7pt over-credit is the always-available DEPLOY of drawn ramp itself (free, perfectly-sequenced, no opportunity cost), not its color. Reaching ~89% needs a stronger ramp-credit correction than the colored-cost gate — a larger design change the directive did not scope.
- test: Implemented castability-gated ramp, re-measured on fixture (live, 20k trials). Result 96% (gated) vs 89% (full-off) vs 96% (always-on).
- expecting: Orchestrator decision. The directive's explicit CHECKPOINT clause is triggered ("gated-ramp fix does NOT bring headline near ~89% (unexpected — another lever)"). Need a decision on the stronger ramp-credit fix (e.g. partial/fractional ramp deploy success, or a per-turn mana-economy opportunity cost) before proceeding to P2/P3/P1/P5 — the whole sequence's P5 target rides on a P4 lever that did not move.
- next_action: CHECKPOINT REACHED (decision). Colored-cost gate implemented (CastabilitySimulator.TryDeployRamp + LibraryCard.RampPips + gateRampOnCastable threaded Simulate→Analyze→Web under land-ramp-sim) but it lands 96%, not ~89%. Awaiting orchestrator direction on the stronger ramp-credit fix. Temp seams + harness still present (uncommitted). Code builds clean (0 err). No commits yet.
- reasoning_checkpoint:
    hypothesis: "P4 ramp-credit (TryDeployRamp deploys any generic-mana-affordable ramp without checking the ramp's OWN colored cost is payable) is the dominant optimism lever; gating ramp deployment on the ramp's colored cost being coverable by online sources will drop the headline from 96% toward Snail's 89%."
    confirming_evidence:
      - "Empirical sweep: ramp fully OFF at baseline grace == 89% EXACTLY (matches Snail 89.1%); ramp ON == 96%. P4 is −7pts, the dominant lever."
      - "TryDeployRamp (CastabilitySimulator.cs:672) gates only on `card.DeployCost <= availableNow` (generic mana), never on the ramp's colored pips being coverable — so an uncastable-by-color ramp is still credited."
    falsification_test: "If the castability-gated ramp variant lands materially ABOVE ~92% (close to the always-on 96%), the gate is too weak / ramp colored costs are nearly always trivially payable, and another lever dominates."
    fix_rationale: "Gating ramp deployment on the ramp's own colored cost being payable by the board's online sources mirrors the 17Lands rule (ramp credited only when itself castable). It is between full-off (89%) and always-on (96%), correcting the root over-credit without the blunt full-disable."
    blind_spots: "Most ramp in this deck is colorless rocks/land-ramp whose colored requirement may be cheap (1 pip) or none — the gate may move the number only partway; the combined P2/P3 free-cost bump then nudges back up. Final target is the re-measured combined value, not a guessed 89%."
- tdd_checkpoint:

## Evidence

- timestamp: 2026-06-24 — codebase map produced exact file:line for all 5 defects (see Prior investigation). Not yet empirically reproduced via a run.
- timestamp: 2026-06-24
  checked: CastabilitySimulator.cs live code vs P1 claim
  found: GraceWindow(turn) at lines 559-564 returns `<=2 => 3, <=5 => 2, _ => 1` — exactly the 3/2/1 window. Used at line 445 (lastTurn = turn + grace) so a spell counts castable up to `grace` turns LATE. CONFIRMED P1 exists in live code. The simulator has evolved into a full Monte-Carlo + London-mulligan model (not the old analytic product), so grace is one of several optimism levers alongside the mulligan itself.
  implication: P1 is real and editable, but its DOMINANCE over the mulligan/ramp levers is unmeasured. The user mandates measuring before editing.
- timestamp: 2026-06-24
  checked: ManabaseClassifier.DetectSelfCost (lines 715-776) vs P2 claim + Scryfall oracle text of the 4 cards
  found: Free-cost detection (case 1, line 728) ONLY matches `"rather than pay this spell's mana cost"`. Verified oracle wording: Fierce Guardianship / Deflecting Swat / Flawless Maneuver all read `"If you control a commander, you may cast this spell without paying its mana cost."` — the `"without paying its mana cost"` form, which the line-727 comment DELIBERATELY excludes as "casts OTHER spells for free". That exclusion is wrong here: `this spell ... without paying its mana cost` is self-anchored. → 3 of the 4 cards are missed by P2. Force of Negation DOES contain `"rather than pay this spell's mana cost"` (exile a blue card variant), so it SHOULD already be detected — if it's reported missed, that's a separate thread (oracle text not reaching classifier, or P3 suggestion-not-applied).
  implication: P2 fix = broaden case 1 to also catch self-anchored `"cast this spell without paying its mana cost"` (guard against the OTHER-spell `"without paying its/their mana cost"` forms). P3 fix = these land in CostSuggestions (lines 215-223) and are NOT auto-applied to the headline; only a user override substitutes the cost (ManabaseAnalyzer.ApplyCostOverrides, lines 115-167). Whether to AUTO-apply free-cost is a product decision.
- timestamp: 2026-06-24
  checked: Headline + verdict derivation (ManabaseDisplay.AvgOnCurve, ManabaseModels.Health getter lines 574-607, ManabaseAnalyzer.BuildCastability/BuildColorFindings)
  found: Headline "avg on-curve %" = plain mean of per-spell CastPercent across non-source rows (ManabaseDisplay.cs:105-120). Health band (Excellent/Solid/Workable/NeedsWork) is NOT a direct function of the headline %; it derives from per-color source deficits + ColorLimitedUnderSupportedCount via ComputeColorSignals (lines 638-681). Both ultimately ride on the simulator's CastPercent, which the grace window inflates. So narrowing P1 lowers the headline AND can tip color findings → worse band.
  implication: P5 regression target ("lands in Workable/Needs-work band, NOT Solid, at ~89%") is assertable on the real deck once P1/P2/P3/P4 land. The two outputs (headline %, health band) are coupled through CastPercent but computed independently — a regression test should pin BOTH.
- timestamp: 2026-06-24
  checked: Repo-wide search for the Avatar (Sokka/Aang Jeskai) decklist
  found: NOT present. 8 baseline decks in 70-flag-baseline.html are Brago/Kenrith/Meren + 5 Archidekt decks; none is the Sokka/Aang Jeskai cEDH deck (89.1% Snail, white weakest). snail-decklists/ holds 8 unrelated decks. The "Aang, Airbending Master" rows are that single card inside the Brago deck, not the Avatar commander deck.
  implication: BLOCKER (since RESOLVED — deck is now a fixture).

- timestamp: 2026-06-24 — EMPIRICAL P1 MEASUREMENT (temp harness, live Scryfall, 99/99 resolved, all 4 manabase flags ON, 20k trials)
  checked: Avatar fixture run through ManabaseClassifier.Classify → ManabaseAnalyzer.Analyze with the grace window swept via a temp internal CastabilitySimulator.GraceWindowOverrideForTest seam.
  found: |
    Grace window sweep (headline avg-on-curve % / health band):
      3/2/1 (baseline)        → 96% / Solid   ← BUG REPRODUCED exactly (matches the 96%/Solid report)
      2/1/1 (+1 cap cheap)    → 95% / Solid   (−1)
      1/1/1                   → 94% / Solid   (−2)
      0/0/0 (strict on-curve) → 92% / Solid   (−4, the MAXIMUM grace can ever remove)
    Snail target = 89.1%. Grace removes AT MOST 4 of the ~7-pt gap, and the health band is "Solid"
    in every config. Weakest color flips Blue↔White depending on grace, but Snail says White.
    The 4 free-cost cards already cast 91–98% at PRINTED cost (Force of Negation 91%, the other
    three 98%) — so P2/P3 (recognizing them as free) would push them HIGHER, never toward 89%.
  implication: |
    CHECKPOINT-TRIGGERING. P1 grace is NOT the dominant lever — the directive's checkpoint condition
    ("if grace alone does NOT close most of the 96→89 gap, STOP and reassess P4 ramp") is met. Even
    the most aggressive grace (0/0/0) leaves 92% / Solid, ~3 pts above target and one full band too
    optimistic. The dominant optimism must live elsewhere: (a) the 20k-trial London-mulligan model
    baseline, and/or (b) P4 ramp credit (TryDeployRamp credits drawn ramp as deployable with optimal
    sequencing regardless of whether the ramp itself was castable). Narrowing grace alone will NOT
    reach Snail's number or move the band off Solid.

- timestamp: 2026-06-24 — EMPIRICAL P4 MEASUREMENT (temp DisableRampForTest seam, same harness/flags/trials)
  checked: Crossed the grace sweep with ramp ON vs ramp-fully-OFF (drawn ramp never deployed).
  found: |
    Headline avg-on-curve % (health = Solid in EVERY row):
                       ramp ON     ramp OFF
      grace 3/2/1      96%         89%   ← ramp OFF at baseline grace == Snail's 89.1% EXACTLY
      grace 2/1/1      95%         —
      grace 1/1/1      94%         86%
      grace 0/0/0      92%         80%   (most pessimistic — over-corrects below Snail)
    P4 (ramp) is the DOMINANT lever: −7 pts on its own (96→89). P1 (grace) is −4 at most.
    Force of Negation per-card: 91% (baseline) → 75% (ramp off, baseline grace) → 55% (both off).
  implication: |
    P4 ramp credit is the primary cause of the over-optimism, not P1 grace. Disabling ramp entirely
    with the CURRENT grace window reproduces Snail's exact headline (89%). But fully disabling ramp is
    too blunt — the directive's real P4 fix is to GATE ramp credit on the ramp itself being castable
    first (TryDeployRamp currently deploys any affordable-by-generic-mana ramp without checking the
    ramp's OWN colored cost was payable). A correct gate lands between 89% (ramp fully off) and 96%
    (always-on), most likely near Snail's ~89%. SECONDARY FINDING: the health BAND never leaves "Solid"
    even at 80% headline — the band logic (ManabaseModels.Health / ComputeColorSignals) is decoupled
    from the headline and does not escalate for this deck; reaching the P5 target ("NOT Solid") may
    require more than the headline drop, or a band-logic adjustment. This needs the orchestrator's
    P4-first re-sequencing decision before any edit.

- timestamp: 2026-06-24 — EMPIRICAL P4 GATED-RAMP MEASUREMENT (real fix implemented, temp harness, live Scryfall 99/99, all 4 flags ON, 20k trials)
  checked: Implemented the castability-GATED ramp (TryDeployRamp now refuses a ramp piece whose OWN colored cost the board's online sources cannot yet pay, via ColorsCoverable on the ramp's pips). Threaded through Simulate/Analyze under a gateRampOnCastable flag tied to land-ramp-sim. Re-measured the Avatar fixture at baseline grace 3/2/1.
  found: |
    Headline avg-on-curve % (Avatar fixture, grace 3/2/1):
      ramp ON (always-available, pre-fix)   → 96% / Solid   (baseline)
      ramp GATED on castable (the real fix) → 96% / Solid   (−0 pts — gate is a NO-OP here)
      ramp OFF (full disable, reference)    → 89% / Solid   (Snail-matching, −7)
    Per-card Force of Negation: 91% (ON) → 90% (GATED) → 75% (OFF).
    The colored-cost gate moves the headline by ~0 points and Blue avg by only 0.2 pts.
  implication: |
    DIRECTIVE CHECKPOINT CONDITION MET ("gated-ramp fix does NOT bring headline near ~89%").
    ROOT CAUSE REFINED: the −7pt over-credit is NOT the ramp's COLORED cost being unpayable — it is
    that the sim deploys drawn ramp AT ALL with perfect, free, no-opportunity-cost sequencing. This
    deck's ramp is almost entirely COLORLESS mana rocks (Sol Ring, Mana Crypt-class Petal/Mox, Arcane
    Signet, Fellwar Stone, three Talismans cast at {2}, Springleaf Drum, Relic of Legends, Paradise
    Mantle) plus colorless land-ramp — so "is the ramp's colored cost payable?" is trivially YES on
    turn 1+, and the 17Lands-style colored gate never fires. The dominant optimism is the always-
    available DEPLOY of a drawn rock the same turn it would be needed, with the simulator free to play
    a land AND a rock AND cast off both every turn with optimal ordering. The colored-cost gate, while
    correct in principle (and it does shave Force of Negation 1pt + flips a couple limiting factors to
    "color"/"both" honestly), is the wrong lever for THIS deck's mostly-colorless ramp.
    → CHECKPOINT to orchestrator: the literal directive fix (colored-cost gate) is implemented and
    correct but does not reach the target. A stronger ramp-credit correction is needed (e.g. a deploy-
    opportunity-cost / one-spell-per-turn-mana model, or a partial ramp discount), which is a larger
    design change than the directive scoped. Surfacing before proceeding to P2/P3, per the directive's
    explicit checkpoint clause.

- timestamp: 2026-06-24 — P4 DEPLOY-FRICTION IMPLEMENTED + MEASURED (operator decision, real fix, temp harness, live Scryfall 99/99, all 4 flags ON, 20k trials)
  checked: |
    Replaced the always-free ramp deploy with a deploy-friction model in TryDeployRamp/SimulateGame:
    deploying a drawn rock now CONSUMES that turn's mana (TryDeployRamp returns the deploy cost spent;
    ReserveGenericForRamp taps the least-color-flexible online sources for that cost), so the rock
    competes with the payoff spell on the deploy turn and only its OUTPUT comes online next turn
    (0-cost fast mana stays same-turn free). Colored-cost gate kept as a correctness sub-improvement.
    Both ride the land-ramp-sim flag; flag-OFF path byte-identical (reserve gated on the flag).
    Crossed deploy-friction with the grace window to expose the friction once grace stops masking it.
  found: |
    Headline avg-on-curve % (Avatar fixture):
      grace 3/2/1, ramp ON always-free (pre-fix)   → 96% / Solid   weakest Blue   (baseline)
      grace 3/2/1, DEPLOY-FRICTION (the fix)        → 96% / Solid   weakest Blue   (−0 at this grace)
      grace 1/1/1, DEPLOY-FRICTION                  → 94% / Solid   weakest WHITE  (−2; White = Snail's call)
      grace 0/0/0, DEPLOY-FRICTION                  → 91% / Solid   weakest WHITE  (−5)
      grace 3/2/1, ramp OFF full-disable (ref)      → 89% / Solid   weakest Blue
    Force of Negation per-card: 91% (pre) → 90% (friction 3/2/1) → 87% (1/1/1) → 82% (0/0/0) → 75% (off).
  implication: |
    DEPLOY-FRICTION IS CORRECT AND FIRING, but its effect at the current 3/2/1 grace is ~0 on the
    HEADLINE because the wide grace window forgives the 1-turn deploy delay the friction introduces (a
    spell pushed 1 turn late by paying for its own rock is still "on curve" under a 3-turn grace). The
    friction's real signal shows once grace tightens: at +1 grace (1/1/1) the headline drops to 94% AND
    the weakest color correctly flips to WHITE (Snail's call), and at strict 0/0/0 it is 91%. This is
    exactly the combined-lever situation the operator decision anticipated: P4 friction is the principled
    correction; the grace window (P1) is a SEPARATE over-optimism lever that currently masks it. Per the
    decision, P1 grace trim is applied ONLY after P2/P3, and only if the combined headline is still well
    above ~89%. Proceeding to P2/P3 (free-cost auto-apply) next, then the grace decision.

- timestamp: 2026-06-24 — P2/P3 FREE-COST AUTO-APPLY + P1 GRACE TRIM (combined measurement, temp harness, live Scryfall 99/99, all 4 flags ON, 20k trials)
  checked: |
    P2: broadened DetectSelfCost case 1 to also match the self-anchored "cast this spell without paying
    its mana cost" form → Fierce Guardianship / Deflecting Swat / Flawless Maneuver now detected (Force
    of Negation already matched the "rather than pay this spell's mana cost" form).
    P3: AUTO-APPLY the free/alt-cost category to the spell requirement (cast at effective 0, like the
    greatest-power reducer), with the suggestion kept + noted "— auto-applied"; user override still wins.
    Re-measured the combined model across the grace window, then made the P1 grace decision.
  found: |
    All four free-cost cards now cast 100% (MV 0, turn 1) — no longer false "demanding" rows.
    Combined P2/P3/P4 headline by grace (Avatar fixture):
      grace 3/2/1 → 96% / Solid   weakest Blue
      grace 1/1/1 → 94% / Solid   weakest WHITE   ← chosen (uniform +1, 17Lands convention)
      grace 0/0/0 → 92% / Solid   weakest WHITE
    P1 DECISION: combined headline at the OLD 3/2/1 grace is 96% — well above ~89%, so the grace trim is
    warranted per the operator rule. Trimmed grace to a UNIFORM +1 (1/1/1): the 17Lands "strict on-curve
    or +1 max" convention cited in the prior research, NOT reverse-engineered from Snail. The old 3/2/1
    credited a 1-2 drop as on-curve up to THREE turns late and was silently forgiving the deploy-friction
    delay, masking the very ramp over-credit P4 corrects.
  implication: |
    HONEST POST-FIX HEADLINE = 94% / Solid, weakest color WHITE. This is the model's number, not a dialed
    one: P4 deploy-friction + P2/P3 free-cost + P1 uniform-+1 grace each justified independently. 94% sits
    just above Snail's 89.1% headline but now agrees with Snail on the WEAKEST COLOR (White) — the
    qualitative call the printed-cost model got wrong (it said Blue). Per the operator decision, 94% IS the
    answer (no fudge factor to force 89%). Band stays Solid (Decision 2: band logic out of scope, deferred
    6th defect). Regression test will pin the Avatar fixture headline near this measured value, band not
    asserted. Proceeding to P5 (remove seams/harness, add the clean regression test, update unit tests
    whose old over-generous assertions changed).

## Eliminated

- hypothesis: P1 grace window (3/2/1) is the DOMINANT lever behind the 96% vs 89% gap; narrowing it to +1 max moves the headline most of the way to Snail's 89.1%.
  evidence: Empirical harness (99/99 cards resolved, all four Phase-70 flags ON, 20k trials) shows narrowing grace barely moves the headline: 3/2/1 → 96%, 1/1/1 → 94% (−2), 2/1/1 → 95% (−1), and even 0/0/0 (strict on-curve, max possible) → 92% (−4). Grace closes at most 4 of the ~7-point gap, and the HEALTH BAND stays "Solid" (Functional) in EVERY configuration. The 20k-trial London-mulligan model itself — not grace — is the dominant optimism source.
  timestamp: 2026-06-24

## Resolution

- root_cause: |
    The castability Monte-Carlo simulator over-credited drawn RAMP. TryDeployRamp
    (CastabilitySimulator.cs) deployed every drawn rock/dork for free with perfect
    sequencing and no opportunity cost, so the SAME turn's mana paid both for the rock
    AND for the payoff spell — double-counting the deploy turn. Empirically this was the
    dominant lever: ramp fully OFF reproduced Snail's 89% exactly while ramp ON gave 96%
    (~7 pts). A secondary cause: four self-anchored free-cast cards (Force of Negation +
    the Fierce Guardianship / Deflecting Swat / Flawless Maneuver commander cycle) were
    analyzed at printed colored cost because DetectSelfCost matched only the "rather than
    pay this spell's mana cost" form, not "cast this spell without paying its mana cost".
    A third (smaller) lever: the 3/2/1 grace window credited a 1-2 drop as on-curve up to
    THREE turns late, masking the deploy delay. (NOT the ramp's COLORED cost — a colored
    gate was a no-op for this deck's mostly-colorless rocks.)
- fix: |
    P4 — deploy-friction ramp model: TryDeployRamp returns the cost it spends and the
    simulator reserves that generic mana out of the turn's online sources (least
    color-flexible first) before testing the payoff; the rock's output comes online next
    turn, 0-cost fast mana stays same-turn. A colored-cost gate (ColorsCoverable on the
    ramp's pips) is kept as a correctness sub-improvement. Rides the land-ramp-sim flag;
    flag-OFF byte-identical.
    P2 — DetectSelfCost case 1 broadened to also match the self-anchored "cast this spell
    without paying its mana cost" form, guarded against the OTHER-spell forms.
    P3 — the detected free/alt cost is auto-applied to the default analysis (suggestion
    kept, annotated "— auto-applied"; user override still wins).
    P1 — grace trimmed from 3/2/1 to a uniform +1 (17Lands on-curve+1 convention), applied
    because the combined headline was still well above ~89%.
    No fudge factor toward 89% — 94% is the model's honest number.
- verification: |
    Honest post-fix headline on the Avatar fixture = 94% avg-on-curve (was 96%), weakest
    color WHITE (was Blue) — now agreeing with the independent Salubrious Snail baseline.
    All four free-cast cards auto-apply (MV 0, >=98% cast). dotnet build clean (0 err);
    DeckFlow.Core.Tests 774/774 pass; DeckFlow.Web.Tests Manabase 82/82 pass; the gated
    live AvatarManabaseRegressionTests confirms 94% / White / free-cost auto-applied.
    Temp seams (GraceWindowOverrideForTest / DisableRampForTest) and the measurement
    harness are removed; one clean regression test remains.
- files_changed: |
    DeckFlow.Core/Manabase/CastabilitySimulator.cs (P4 deploy-friction + colored gate + P1 grace)
    DeckFlow.Core/Manabase/ManabaseAnalyzer.cs (gateRampOnCastable threading)
    DeckFlow.Core/Manabase/ManabaseClassifier.cs (P2 detection + P3 auto-apply)
    DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs (wire gate to land-ramp-sim)
    DeckFlow.Core.Tests/Manabase/CastabilitySimulatorCoverageTests.cs (grace assertion 4.0->2.0)
    DeckFlow.Core.Tests/Manabase/LandRampSimTests.cs (deploy-friction guard test)
    DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs (P3 auto-apply assertions)
    DeckFlow.Core.Tests/Manabase/AvatarManabaseRegressionTests.cs (new, gated regression test)
