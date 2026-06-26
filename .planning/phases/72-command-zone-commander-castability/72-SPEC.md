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
cards as commander; companion is silently dropped (analysis whitelist =
mainboard+commander only, `ManabaseAnalysisService.cs:109`); and the commander is
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
  Companion category falls to mainboard. Moxfield parser handles
  commander/sideboard/maybeboard headers only (`MoxfieldParser.cs:174-186`).
  Companion data likely exists upstream (Archidekt "Companion" category, Moxfield
  "companions" board) but is dropped.
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
- Importers: capture companion from Archidekt "Companion" category +
  Moxfield "companions" board as a distinct board value (e.g. `Board == "companion"`)
  rather than collapsing it to mainboard/dropping it.
- Fallback UI designator: when the source carries no companion (pasted text, or
  absent), a UI input lets the user name the companion (pattern: like the existing
  cost-overrides box). Auto-detected value pre-fills; user can override/clear.

### C. Companion modeling
- A designated/detected companion is modeled as outside-the-99 with effective cost
  = printed cost **+ 3 generic** (the "to hand" tax). It does NOT count toward
  `commanderCount` or the Karsten land target, and is not in the library draw.
- Its castability appears only in the commander callout (Section D), labeled as a
  companion with the +3 tax disclosed.

### D. Separate commander castability callout (above the table)
- New UI section ABOVE the castability table: one line per command-zone card
  (1-2 commanders / background) showing its cast-on-ideal-turn %, plus the
  companion line (tax noted).
- REMOVE commander row(s) from the per-card castability table (move-out) and
  recompute the table's average (the right-lens "avg on-curve across N spells")
  WITHOUT the commanders, so the number isn't double-shown.
- Mirror a concise version into the manabase prompt artifact
  (`ManabaseSwapPromptBuilder`) — the commander cast line(s) help the LLM.

### E. Phase 71 coordination
- The ramp/draw budget threshold (Phase 71) uses the command zone: multi-commander
  threshold = the max MV across the command-zone cards (partners/background). Pin
  this so 71 and 72 agree on the proxy.

### F. Plumbing
- Flag (e.g. `manabase.commander-castability`), seeded OFF both dialects, MQ-flag
  pattern + fail-safe read. Flag OFF → commander stays in the table, no callout, no
  companion modeling, no importer board change effect on analysis → prod
  byte-identical. (Importer capturing a new board value is inert unless the flagged
  analysis path consumes it.)
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
- Exact importer representation for companion (new `Board=="companion"` vs a flag on
  the entry) and how the fallback UI designator threads through request → service.
- Moxfield API: confirm the "companions" board name/shape in the raw deck JSON
  (`MoxfieldApiDeckImporter`) before relying on it.
- Callout copy + placement relative to Phase 71's verdict block (both sit above the
  table — order them).
- Table-average recompute: confirm no downstream metric (health band) depends on the
  commander being in the table average.

## Done when
- Flag OFF → prod byte-identical (commander in table, no callout, no companion).
- Flag ON: partners/background → 2 commanders in the callout; companion auto-detected
  from Archidekt/Moxfield (fallback UI designator works for paste-text) and shown
  with +3 tax; commander row(s) removed from the table and the table average
  recomputed; prompt artifact carries the commander cast line(s).
- Core + Web tests green (partner pair, commander+background, companion auto-detect
  per platform, companion via UI fallback, table-average-excludes-commander,
  companion +3 tax cost); live Playwright callout screenshots Casual desktop+mobile
  × 2 themes; build clean.
- README + `Help/manabase.md` updated.

---

*Phase: 72-command-zone-commander-castability*
*Scoped: 2026-06-26. Depends on Phase 71.*
