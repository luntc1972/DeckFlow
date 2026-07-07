# Manabase Analysis — Efficacy Findings R2

**Captured:** 2026-07-07
**Source:** 3-agent parallel review (simulator math / classifier / page+verdict) + orchestrator
verification against source, live Scryfall API, and prod feature-flag state.
**Supersedes:** `manabase-efficacy-findings.md` (2026-06-22, grade C-) — all four of that audit's
shipped fixes (mana quantity, commander library leak, ramp-credit v2, color-aware mulligan)
verified present and sound.

**Overall grade:** core statistical math **B+** · oracle-text classification **C** · presentation **B**.
The weak link moved: the Monte Carlo/Karsten core is now solid; the errors live in the
oracle-text predicates feeding it and a few presentation escapes.

**Prod flag state (verified live 2026-07-07):** ALL 10 `manabase.*` flags ON, including
`commander-castability`, `plain-language-verdict`, `tap-analyzer` (flipped 2026-07-01).
Every flag-on code path is what users get; several findings previously assumed "latent" are LIVE.

**Verified solid (do not re-audit):** partial Fisher-Yates unbiased; FNV-1a per-spell seeding
(page ⇄ download reproducibility real); 20k trials → SE ≈ 0.35 pt; hypergeometric log-space
math + CastConsistency structure correct; MQ-02 pip-cover DFS exact; ramp timing cannot
self-pay; IsCommander sources excluded from library; fetch-land coloring conservative;
ManaProductionAmount parsing conservative; split/adventure name indexing correct.

---

## HIGH (live wrong numbers)

### H1 — Tapland detection dead on live Scryfall data  *(one-line fix, biggest live error)*
`ManabaseClassifier.cs:643` — `EntersTapped` matches only `"enters the battlefield tapped"`.
Scryfall's Aug-2024 oracle rewording is `"This land enters tapped."` — verified live
(Azorius Guildgate, Temple of Enlightenment). Every Guildgate/Triome/Temple/bounce land
classifies **untapped**: `tap-analyzer` reads ~100% untapped for any deck, turn-1 untapped
supply inflated, sim ETB-tapped timing (`OnlineTurn+1`) never applies.
**Fix:** also match `"enters tapped"`; keep old string for stale fixtures. Regression tests
with the new wording.

### H2 — Treasure-makers / sac one-shots counted as permanent 5-color sources
`ManabaseClassifier.cs:418-426` (`IsRockOrDork`) + `:525-527` (`ProducesMana`) — gate is
`ProducedMana.Count > 0` + Creature/Artifact. Verified live: Dockside Extortionist
`produced_mana: [B,G,R,U,W]` → permanent 0.5-weight WUBRG dork inflating all five colors'
`ActualSources`, and (as `IsManaSource`) hidden from castability rows. Same class: Lotus Petal
(0.75 rock forever), Goldspan Dragon, Pitiless Plunderer, Phyrexian/Ashnod's Altar,
Springleaf Drum, Krark-Clan Ironworks.
**Fix:** require a real front-face `{T}: Add` mana ability with parenthesized reminder text
stripped; downgrade/skip sacrifice-cost activations.

### H3 — Swap prompt contradicts page on ramp-covered land shortfall  *(core-value violation)*
`ManabaseSwapPromptBuilder.cs:61-66` — only two branches (`on target` / `add ~N lands`).
Page (`Manabase.cshtml:327-332`), .txt (`ManabaseReportTextBuilder.cs:64-79`) and `PrimaryFix`
(`ManabaseModels.cs:1040`) all have the third branch: "~N under Karsten but ramp covers it".
Ramp-saturated deck with LandDelta=-3 and clean sim → page says don't add lands, ChatGPT prompt
says "add ~3 more land(s)". One-round-trip prompt correctness broken.
**Fix:** mirror the three-way `landNote` (incl. `LandShortfallCoveredByRamp`) in the swap prompt.

### H4 — Plain-language verdict can say "no changes needed" beside a Workable chip  *(LIVE)*
`ManabaseVerdictSynthesizer.cs:47-96` — `CollectIssues` uses different predicates than the
health band (`ManabaseModels.cs:800-855`): misses color-starved counts
(`ColorLimitedUnderSupportedCount`), the health-band sim-weakest path, and uses
`LandDelta <= -2.0` where the band/fix use `< -1` → "add ~2 land(s)" on the Lands line with
"no changes needed" underneath. `plain-language-verdict` ON in prod since 07-01.
**Fix:** derive `CollectIssues` from the same `ComputeColorSignals()` + thresholds the `Health`
getter uses.

### H5 — `SixtyCardLandTarget` ships 100-card-scaled coefficients
`KarstenManabase.cs:82-83` — `32.65 + 3.16·MV` is Karsten's 60-card regression
(19.59 + 1.90·MV) pre-multiplied by 5/3. A 60-card deck, avg MV 2.5, 8 ramp/draw →
recommends ~38 lands (Karsten: ~22-23). Only hit on the `!IsSingleton` branch
(`ManabaseAnalyzer.cs:269`) — Commander-first product limits exposure, but flatly wrong math.
**Fix:** constants → `19.59 + (1.90 * averageManaValue)`, keep credit terms.

---

## MEDIUM

### M1 — London mulligan puts bottomed cards on TOP of the library
`CastabilitySimulator.cs:1430-1457` (`BottomCards` swaps worst keeps into slots
`[keptSize,7)`) + `:617` (`drawPtr = handCount = keptSize`) → after a mull to 6 the turn-1
draw is deterministically the exact card just bottomed (worst non-land). Understates
castability / inflates AverageDelay ~0.5-2 pt on the marginal hands mulligans exist to rescue.
**Fix:** swap bottomed indices past the drawable prefix (any slot ≥ prefix never drawn).

### M2 — Land sequencing never plays a tapped fixer over a useless untapped land
`CastabilitySimulator.cs:849-851` — pick priority untapped-needed → untapped-any → tapped,
even on slack turns when the ETB-tapped penalty is free. Tapland-heavy fixing decks read
worse than real play (colorShort fails a real player avoids). Compounds with H1 once fixed.
**Fix:** when `currentTurn < turn`, prefer a tapped land intersecting `neededColors` over an
untapped land adding no needed color.

### M3 — Colored-cost ramp gate exists, is tested, never enabled
`CastabilitySimulator.cs:165` `gateRampOnCastable = false` default; `ManabaseAnalysisService`
never passes it (verified: zero references). Sim deploys a `{G}` dork from a zero-green hand
and credits its mana — inflates cast% for thin-splash ramp packages.
**Fix:** wire it (flag or fold into base path) after a golden-deck diff.

### M4 — ramp-credit-v2 does not strip reminder text
`ManabaseClassifier.cs:686-710` — permanent test is `frontText.Contains("Add ")`; Treasure
reminder text contains "…Sacrifice this token: Add one mana of any color." → Prosperous
Innkeeper / Goldhound still earn the −0.28 credit V2 exists to remove.
**Fix:** strip `(…)` reminder text before the `"Add "` check.

### M5 — Unknown reducer scope words default to deck-wide discount
`ManabaseClassifier.cs:909-930` — "Giant/Goblin/Historic spells cost less" falls through to
`ReductionScope.All`, discounting every spell (cap −2).
**Fix:** unrecognized non-empty scope → null (no reducer).

### M6 — Transform cards with land backs treated as playable MDFC lands
`ScryfallCardFactMapper.cs:83-94` — `HasLandFace` ignores `Layout`. Search for Azcanta /
Growing Rites get 0.8-1.0 color-source weight + mdfc land-target credit; transform backs are
not playable from hand.
**Fix:** gate back-face land check on `layout == "modal_dfc"`.

### M7 — Draw predicates miss "draws two cards" + budget/credit disagreement
`ManabaseClassifier.cs:654-655, 690-691` match only "draw a/two card(s)"; Night's Whisper
("Target player draws two cards") gets no land-target credit while `IsDrawPieceForBudget`
(`:670-675`, `BudgetDrawRegex`) counts it — same card classified differently in two subsystems;
regex also credits opponent-only draw.
**Fix:** one shared you-anchored draw predicate across all three.

### M8 — Conditional lands counted as unconditional any-color sources  *(old #6 class, still open)*
`ManabaseClassifier.cs:322-352` — `produced_mana` at face value, `IsConditional=false`:
Cavern of Souls / Ancient Ziggurat / Unclaimed Territory credit all colors for noncreature
spells; Nykthos full any-color at zero devotion. No "spend this mana only" handling anywhere.
**Fix:** detect "spend this mana only" → conditional weight/restriction; still silent to user
(disclosure gap from old #6 remains).

### M9 — Two different "avg on-curve" numbers on one page  *(LIVE)*
Lens excludes commander rows (`Manabase.cshtml:176-179,221` via `ManabaseDisplay.AvgOnCurve`);
health band + verdict use `report.AvgOnCurvePercent` including them (`ManabaseModels.cs:907-924`,
`ManabaseVerdictSynthesizer.cs:130`). Hard-to-cast 6-MV commander → verdict quotes 82%, lens 87%.
**Fix:** one shared non-commander average consumed by both.

### M10 — Download .txt missing command-zone/companion block  *(LIVE)*
Page pulls commander rows into a callout + companion +3-tax note (`Manabase.cshtml:394-414`);
swap prompt has `AppendCommandZoneBlock`; `ManabaseReportTextBuilder.Build` has neither
(controller passes nothing: `ManabaseController.cs:129-132`). The "mirrors exactly" artifact
drops the companion story entirely.
**Fix:** add the same optional command-zone/companion parameters to the .txt builder.

### M11 — Override box: silent refill + silent no-op
`ManabaseViewModel.cs:67-70` — cleared box falls back to `SuggestedOverridesText`: user clears
to reject, next Analyze silently re-applies. `ManabaseCostOverrideParser.cs:39-66` — malformed
or typo'd (unmatched-name) lines vanish with zero feedback.
**Fix:** `OverridesTouched` hidden field; surface "N override line(s) not applied: …".

### M12 — Help/methodology overclaims
`Help/manabase.md:38` "empty box = printed costs" contradicts auto-applied reducers
(`ManabaseClassifier.cs:129-147`, and the doc's own line 36). `manabase.md:50` +
`Manabase.cshtml:646` claim a standalone "broad color-access" Needs-work trigger that code
never fires alone (`ManabaseModels.cs:708-726` — corroboration/veto only); headline-floor
sentence lacks its flag caveat.
**Fix:** reword both.

---

## LOW

- **L1** `Math.Ceiling(-LandDelta)` turns 1.05 shortfall into "add ~2 land(s)" (all surfaces
  consistent, all overstate ≤1). Round or show raw delta.
- **L2** Verdict truncates to 3 lines silently (`ManabaseVerdictSynthesizer.cs:90-93`) — paste
  artifact under-reports known issues. Append "…plus N more".
- **L3** Deficit computed from display-rounded `ActualSources` (`ManabaseAnalyzer.cs:571` +
  `ManabaseModels.cs:468`) — verdict band can flip on ≤0.05 rounding. Keep raw, round in view.
- **L4** `ReserveGenericForRamp` (`CastabilitySimulator.cs:757-759,930-969`) is provably dead
  code — runs only when the turn already failed `manaShort`. Its documented ~7-pt effect came
  from the concurrent GraceWindow change. Delete or fix rationale before anyone "tunes" it.
- **L5** Karsten ceiling computed on-the-play (7+T−1, `ManabaseAnalyzer.cs:658`) while the sim
  uses the Commander 7+T re-baseline — clamp occasionally ~1 source too high. Pass
  `onPlay:false` for singleton.
- **L6** True `{C}` pips folded into generic (`CastabilitySimulator.cs:181-192`) — Warping Wail
  payable by any source. Needs a sixth mask bit if ever modeled.
- **L7** `Hypergeometric.AtLeast` doc claims shorter-tail summation it doesn't do
  (`Hypergeometric.cs:72-74`). Fix comment.
- **L8** `.manabase-lens-short` ⚠ binds to theme `--warning` tokens (`site-common.css:2678-2681`)
  — same token class as the past invisible-label bug; verify 24 themes or bake status color.
- **L9** `DetectGranter` misses documented Relic of Legends (singular "creature")
  (`ManabaseClassifier.cs:944-984`).
- **L10** Dead commander-driver fallback branch + misleading comment
  (`ManabaseAnalyzer.cs:511-517`).
- **L11** Threshold-proxy label duplicated ×3 (`ManabaseVerdictSynthesizer.cs:171-175`,
  `ManabaseReportTextBuilder.cs:308-312`, `ManabaseSwapPromptBuilder.cs:167-171`) — move to
  `ManabaseLabels`.
- **L12** Unit-path greedy color matcher heuristic not exact (theoretical only; MQ-02 DFS path
  is exact; preserved byte-identical deliberately).
- **L13** Land-ramp sim adds delayed source without thinning library — offsetting
  approximations, documented behavior gap only.
- **L14** `ConsistencyThreshold` escalating 90→96% (`KarstenManabase.cs:94-98`) unconfirmed
  against Karsten 2022 (flat ~90%?). Check article once; also spot-check the −1.35 intercept
  and credits-outside-scale choices in `SingletonLandTarget`.

---

## Recommended order

1. **H1** — one-line, huge live impact (`tap-analyzer` currently emits fiction).
2. **H3 + H4** — presentation coherence; share one `ColorSignals`/land-note source across page,
   .txt, verdict, swap prompt.
3. **H5** — one-line constants.
4. **H2 (+M4, M7 same class)** — reminder-text-stripped front-face `{T}: Add` predicate
   centralization; kills the class.
5. **M1, M2** — sim realism (mulligan bottoming, tapped-fixer sequencing); golden-deck diff.
6. **Root-cause guard:** live-oracle canary test (or scheduled fixture refresh) asserting
   classifier predicates against current Scryfall wording — a 2024 rewording rotted a core
   predicate for ~a year with green tests.
7. Rest by opportunity.

---

## Follow-up findings (discovered during M4 deck-test, 2026-07-07)

### M4b — ramp-credit-v2 permanent branch missing one-shot / sacrifice guard
`ManabaseClassifier.cs` `IsRepeatableRampOrDraw` (~:849-856). After the M4 reminder-strip, the
permanent branch is still `permanent && frontText.Contains("Add ")` with **no sacrifice / one-shot
guard** — unlike H2's `HasRepeatableManaAbility` (`LineHasActivatedAdd`), which drops
`{cost incl. Sacrifice}: Add`. So a permanent whose ONLY mana ability is a one-shot sac
(**Lotus Bloom** `{T}, Sacrifice this artifact: Add three…`, **Lion's Eye Diamond**,
Chromatic Star/Sphere class) earns the −0.28 repeatable-ramp land credit despite giving no
persistent mana. **Confirmed live** on the Rakdos treasure deck: Lotus Bloom (MV0) → v2=1.
Subsystem divergence between the MQ-03 credit path and the H2 source-classification path.
**Fix:** reuse the H2 sacrifice/one-shot predicate (`LineHasActivatedAdd`-style) in the
permanent branch so the two paths agree, or share one "has a repeatable (non-sac) front-face
`Add` ability" helper across both. Same class as H2/M4; low magnitude (−0.28 per card) but a
real correctness + consistency gap.

*Deck-test note:* on a real 100-card Rakdos treasure deck (The Master, Multiplied), M4 itself
moved the land target ≈0 — its reminder-carrying makers were already excluded for other reasons
(Ragavan carries no `Add `; Reckless Lackey / Deadly Dispute credited via the DRAW branch, i.e.
M7 territory), and the MV≤2 slots are dominated by real rocks (Sol Ring, Mox Opal, Signet,
Fellwar, Talisman, The Soul Stone) which correctly keep credit. Result: 35 lands vs 33.4 target,
Health "Functional".
