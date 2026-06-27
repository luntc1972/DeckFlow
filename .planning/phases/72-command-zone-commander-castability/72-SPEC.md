# Phase 72 SPEC — Command-Zone Modeling & Commander Castability

Status: SPEC (scoping). Depends on: Phase 71 (shares the manabase verdict/flag/UI
scaffolding and the ramp/draw threshold). Source: user asks 2026-06-26.

Scoping decisions (user, 2026-06-26):
- Add partner / companion / background handling to the manabase tool.
- Show each commander's castability SEPARATELY in a callout ABOVE the per-card
  castability table; MOVE the commander row(s) OUT of that table (callout only).
- Companion: PREFER auto-detecting it from Archidekt / Moxfield import data; fall
  back to a manual UI designator only when the source doesn't carry it (e.g.
  pasted text). Prior art for the manual UI: salubrioussnail.com/manabase-tool —
  but auto-detect is the primary path.

## What it delivers

Make the manabase tool correctly model the full command zone and surface
commander-cast clarity:

1. **Partner / Background** — a deck with two command-zone cards (partner+partner,
   or commander+Background) is modeled with both flagged commander and
   `commanderCount = 2` (already feeds Karsten). Close the import gaps so this
   works on both platforms.
2. **Companion** — detect the companion from import data where possible; otherwise
   let the user designate it. Model it as outside-the-99 with its real cast cost
   (the +3 "to hand" tax). A companion is NOT a commander for Karsten/commanderCount.
3. **Separate commander castability callout** — a new section ABOVE the castability
   table showing each command-zone card's chance to cast on its ideal turn
   (1-2 commanders / background) plus the designated companion (tax noted). The
   commander row(s) are REMOVED from the per-card table and the table average is
   recomputed without them.

Flag-gated; flag OFF = prod byte-identical (this changes existing output — the
table contents/average — so it must gate).

## Why

Today partners/background only work if the source platform happens to tag both
cards as commander; a companion is NOT recognized as such — on Archidekt its
"Companion" category falls through to `mainboard` (`ArchidektApiDeckImporter.cs`
`DetermineBoard` routes only Commander/Maybeboard/Sideboard) so it is analyzed as
a normal 99 card; on the Moxfield Commander-Spellbook fallback path it is never
imported at all. Either way it is never modeled AS a companion. And the commander is
just one row inside the castability table (`ManabaseAnalyzer.cs:281` keeps it in),
rolled into the deck average — so its individual castability is buried. Casting the
commander on curve is the single most important line in a Commander deck; it
deserves its own callout, and the command zone needs correct modeling for the
Phase 71 ramp/draw threshold to be right.

## Current-state map (verified 2026-06-26)
- Commander flagging: `ManabaseAnalysisService.cs:314` (`Board == "commander"` →
  `IsCommander`) → `ScryfallCardFactMapper.ToCardFacts()` → `CardFact.IsCommander`.
- `commanderCount` summed in `ManabaseClassifier.cs:101-103`; Karsten uses it
  (`KarstenManabase.cs:21,29,37`) — partners/background = 2 already supported math-side.
- Companion: no model. Archidekt importer `DetermineBoard` routes only
  Commander/Maybeboard/Sideboard (`ArchidektApiDeckImporter.cs:126-143`) →
  Companion category falls to `mainboard` (analyzed as a normal card, not dropped).
  Moxfield: the **direct API** path reads only four hard-coded top-level boards
  (`MoxfieldApiDeckImporter.cs:95-98`); the **Commander-Spellbook fallback** path
  (used on common edge blocks, `:35-37`,`:60-68`) imports only `commanders` + `main`
  (`:103-127`) — so a Moxfield companion is unavailable on the fallback path.
  ⚠ Must confirm the direct-payload "companions" board shape with a fixture before
  planning.
- Castability table INCLUDES commander rows (`ManabaseAnalyzer.cs:281`; view
  `Manabase.cshtml:378-382` `manabase-row--commander` + star). No separate
  per-commander cast-rate metric (`ManabaseDisplay.cs:96`).
- No command tax (+2 recast) modeled (`CastabilitySimulator.cs:186`).

## In scope

### A. Partner / Background correctness
- Ensure two command-zone cards both → `IsCommander`, `commanderCount = 2`.
- Close import gaps: Archidekt "Background" / "Companion" categories and Moxfield
  "companions" board are currently not routed. Route Background to the commander
  board (so commander+Background = 2 commanders).
- Verify on both platforms + paste-text (Moxfield "Commander" section, Archidekt
  "Commander"/"Background" categories).

### B. Companion detection (auto-first)
- Detect the companion from import metadata where available: Archidekt "Companion"
  category; Moxfield "companions" board on the **direct API path only**.
- ⚠ Moxfield Commander-Spellbook fallback path does NOT carry companion → on that
  path, fall back to the manual designator. cEDH-edge Moxfield decks frequently
  hit the fallback, so do not assume auto-detect always succeeds for Moxfield.
- ⚠ Do NOT globally remap the canonical `DeckEntry.Board` to a new `"companion"`
  value at import time — deck-analysis consumes every non-sideboard/non-maybeboard
  entry as active deck content (`DeckAnalysisPacketService.cs:165-169`,`:409-418`),
  so a global remap would change prod deck-analysis output even with the flag OFF
  (violates byte-identity). Instead carry companion as side metadata (e.g. a
  category tag / a separate detected-companion field on the request/result) that
  ONLY the flagged manabase + flagged deck-analysis paths read. See §G.
- Fallback UI designator: when the source carries no companion (pasted text, or the
  Moxfield fallback path), a UI input lets the user name the companion (pattern:
  the existing cost-overrides box). Auto-detected value pre-fills; user can
  override/clear.
- PLAN prerequisite: capture a real Moxfield direct-API payload fixture proving the
  "companions" board name/shape before relying on it.

### C. Companion modeling
- A designated/detected companion is modeled as outside-the-99 with effective cost
  = printed cost **+ 3 generic** (the "to hand" tax). It does NOT count toward
  `commanderCount` or the Karsten land target, and is not in the library draw.
- ⚠ This is a HEURISTIC, not a rules-exact simulation. The real sequence is "pay 3
  at sorcery speed to move it to hand, then cast it later"; we approximate it as a
  single castability event at printed+3. `CastabilitySimulator` supports the flat
  extra generic via `effectiveGeneric`/`effectiveCost` (`CastabilitySimulator.cs:178-187`)
  and does not require the card to be in the library. State the approximation in copy.
- Its castability appears only in the commander callout (Section D), labeled as a
  companion with the +3 tax disclosed.

### D. Separate commander castability callout (above the table)
- New UI section ABOVE the castability table: one line per command-zone card
  (1-2 commanders / background) showing its cast-on-ideal-turn %, plus the
  companion line (tax noted).
- ⚠ DISPLAY-LAYER move-out ONLY — do NOT mutate `report.Castability`. That list
  feeds `ManabaseReport.AvgOnCurvePercent` (`ManabaseModels.cs:788-803`), which
  drives `Health` (`:577-620`) and `LandShortfallCoveredByRamp` (`:633-652`), and
  is also read by the right-lens view (`Manabase.cshtml:163-202`) and the text
  artifact (`ManabaseReportTextBuilder.cs:128-140`). Removing rows from the
  underlying list would silently shift the health verdict + fix selection. So:
  keep `report.Castability` intact (verdict/health byte-identical); the per-card
  TABLE rendering filters OUT commander rows for display, and the callout reads the
  commander rows. Provide a SEPARATE display-level "table average excluding
  commanders" for the visible table/right-lens count so the shown rows and the
  shown average agree — without touching report-level `AvgOnCurvePercent`.
- Headline/summary rule for 2 command-zone cards: use the WORST command-zone
  castability for any verdict copy (current code takes the first commander row,
  `ManabaseAnalyzer.cs:861-867` — make multi-commander deterministic: worst-of).
- Mirror a concise version into the manabase prompt artifact
  (`ManabaseSwapPromptBuilder`) — the commander cast line(s) help the LLM.

### E. Deck-analysis prompt command-zone awareness (second surface) — ⛔ MOVED TO PHASE 73 (2026-06-27)
> **Out of scope for Phase 72.** Per user decision 2026-06-27, Section E is carved out into a
> dedicated **Phase 73 (Deck-Analysis Command-Zone Awareness)** so the `DeckAnalysisPacketService`
> plumbing + 3-variant edits ship independently of the manabase callout. Phase 72 planner: IGNORE
> §E — do not plan deck-analysis work. Phase 73 depends on Phase 72's command-zone detection +
> companion side-metadata. The text below is retained as the Phase 73 scope source.

- The deck-analysis page (`/deck-analysis`) is prompt-artifact-centric. Reuse the
  Phase 72 command-zone detection so the generated analysis prompt correctly states
  the command zone: name the commander(s) (partner pair), the Background, and the
  companion (if any) so the AI analysis treats the command zone correctly.
- Scope here is AWARENESS only — annotate the command-zone composition in the
  prompt artifact. NOT the castability callout, NOT on-page sim UI.
- ⚠ This is BIGGER than "edit 3 prompt strings". The variants today receive a
  SINGULAR `commanderName` (`ChatGptAnalysisPromptVariant.cs:24-33`,
  `Claude…:25-34`, `Gemini…:26-35`) and the dispatcher passes one value
  (`DeckAnalysisPacketService.cs:1115-1121`); the deck text only distinguishes
  `Commander` vs `Mainboard` (`:790-841`). So Section E requires command-zone
  PLUMBING in `DeckAnalysisPacketService` first: surface the full command zone
  (partner pair / commander+Background / companion) to the variants, then render it.
- Edit ALL THREE decoupled analysis prompt variants — do NOT extract a shared
  helper (variants are intentionally decoupled, see ADR
  `docs/decisions/0001-prompt-variants-decoupled.md`):
  - `DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs`
  - `DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs`
  - `DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs`
- Companion on deck-analysis follows the same auto-detect-first rule (Archidekt/
  Moxfield direct only); designator-UI fallback parity with manabase is a PLAN
  decision. Companion is carried as side metadata, NOT a remapped Board (see §B/§G).
- Flag-gated (may share `manabase.commander-castability` or a deck-analysis-specific
  flag — PLAN decides); flag OFF → `DeckAnalysisPacketService` + all three variants
  byte-identical.
- PLAN should decide whether E is large enough to split into its own phase.

### F. Phase 71 coordination
- The ramp/draw budget threshold (Phase 71) uses the command zone: multi-commander
  threshold = the max MV across the command-zone cards (partners/background). Pin
  this so 71 and 72 agree on the proxy.

### G. Plumbing
- Flag (e.g. `manabase.commander-castability`), seeded OFF both dialects, MQ-flag
  pattern + fail-safe read. Flag OFF → commander stays in the table, no callout, no
  companion modeling, deck-analysis variants unchanged → prod byte-identical.
- ⚠ Byte-identity hazard: do NOT change global importer output (no remapped
  `Board`). Companion is carried as INERT side metadata that only the flagged
  manabase + flagged deck-analysis paths read. With the flag OFF, deck-analysis
  (`DeckAnalysisPacketService.cs:165-169`,`:409-418`,`:790-841`,`:1087-1102`) and
  manabase must produce identical bytes to today for the SAME imported deck.
  Add a flag-OFF byte-identity regression test on BOTH surfaces.
- Web-page change rule: xUnit + Playwright + desktop/mobile across themes; layout
  CSS in `site-common.css`; README + `Help/manabase.md` updated.

## Out of scope (non-goals)
- Command tax (+2 per recast) for commanders — keep first-cast on curve (matches
  current sim). The +3 companion tax IS modeled (it's a fixed entry cost, not a
  recast tax).
- Recomputing color identity from partners/background beyond what the cards already
  contribute (color identity is already card-derived).
- A full sideboard/companion-legality validator (we model the designated companion's
  castability only; we do not police the companion deckbuilding restriction).

## Open / ambiguity to resolve in PLAN
- Whether companion gets its own flag or shares the commander-castability flag.
- Companion side-metadata representation (a category tag vs a dedicated
  detected-companion field on the request/result) — NOT a remapped `Board` — and how
  the fallback UI designator threads through request → service.
- ⚠ Moxfield: capture a real DIRECT-API payload fixture confirming the "companions"
  board name/shape BEFORE relying on auto-detect; define fallback-path behavior
  (Commander-Spellbook path → manual designator only).
- Callout copy + placement relative to Phase 71's verdict block (both sit above the
  table — order them).
- ⚠ Table move-out is DISPLAY-ONLY: confirm the separate display-average path leaves
  `ManabaseReport.AvgOnCurvePercent`/Health/LandShortfall untouched (regression-test
  health byte-identity with commander still in `report.Castability`).
- Multi-commander headline rule: confirm "worst command-zone castability" for verdict
  copy (vs current first-commander pick at `ManabaseAnalyzer.cs:861-867`).
- Whether Section E (deck-analysis command-zone plumbing + 3 variants) is large enough
  to split into its own phase.

## Done when
- Flag OFF → prod byte-identical on BOTH manabase AND deck-analysis (commander in
  table, no callout, no companion, analysis variants unchanged) for the same imported
  deck — guarded by a flag-OFF regression test on each surface.
- Flag ON: partners/background → 2 commanders in the callout; companion auto-detected
  from Archidekt category / Moxfield DIRECT API (manual designator fallback for
  paste-text + Moxfield Spellbook-fallback path) and shown with the +3 tax (labeled a
  heuristic); commander row(s) hidden from the per-card TABLE (display-only) with a
  display-average that excludes them, while `report.Castability`/Health stay intact;
  prompt artifact carries the commander cast line(s).
- Core + Web tests green (partner pair, commander+background, companion auto-detect
  per platform, companion via UI fallback, display-table-excludes-commander while
  report-level AvgOnCurve/Health unchanged, companion +3 tax cost, multi-commander
  worst-of headline); live Playwright callout screenshots Casual desktop+mobile
  × 2 themes; build clean.
- README + `Help/manabase.md` updated.
- Deck-analysis AWARENESS (§E) → ⛔ moved to **Phase 73**, not a Phase 72 done-criterion.
  Phase 72 must still keep deck-analysis byte-identical (companion side-metadata is INERT to
  `DeckAnalysisPacketService` whether the manabase flag is OFF or ON) — guarded by the §G
  flag-OFF + flag-ON deck-analysis byte-identity regression test.

---

*Phase: 72-command-zone-commander-castability*
*Scoped: 2026-06-26. Depends on Phase 71.*
