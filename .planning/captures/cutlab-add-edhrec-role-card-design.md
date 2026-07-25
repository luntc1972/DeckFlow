# Cut Lab — Add a role card from the commander's EDHREC cards (DESIGN ONLY)

**Date:** 2026-07-24 · **Status:** design, not built (user: "just design it") · **Data source (approved):** live EDHREC commander page.

## Problem
Cut Lab is *subtractive* — trim an oversized pool to 100. When a user is under 100 (or short on a role), the only "add" today is the quantity tuner, which adds **basic lands only** (`CutLabAdjustmentApplier.cs:27` rejects non-basics). There's no way to add a good spell of a needed role (draw, ramp, interaction…). Users want to fill a slot with a card that EDHREC recommends **for their commander**, filtered by role.

## Goal
In Cut Lab, let the user pick a role (draw / ramp / interaction / …) and add a card that EDHREC recommends for their commander, filtered to that role — added into the working list and reflected in the export.

## Two hard parts (both required)
### A. Data source — live EDHREC commander page
- **Endpoint:** `https://json.edhrec.com/pages/commanders/<slug>.json` (mirror the existing card endpoint + `Slugify` in `DeckFlow.Core/Integration/EdhrecCardLookup.cs:80`). **Verify the exact JSON shape against a live sample during build.**
- **Shape (to confirm):** `container.json_dict.cardlists[]`, each with a category header/tag (e.g. "Ramp", "Card Draw", "Removal", "Board Wipes", "Counterspells", "Card Advantage") and `cardviews[]` (name, `num_decks`/inclusion, synergy). 
- **Category → Cut Lab role mapping:** align EDHREC category headers to Cut Lab's role keys (`CutLabRoleAssigner`: lands, ramp, card draw, interaction, …). Interaction likely aggregates Removal + Counterspells + Board Wipes. Keep the mapping in one table.
- **Ranking / filtering:** top-N per role by EDHREC inclusion; exclude cards already in the pool, off-color (commander color identity), or banned (`ICommanderBanListService`).
- **License/compliance:** EDHREC is non-commercial + wants a contact User-Agent — reuse the Tagger UA-contact convention. Cache aggressively (commander pages change slowly): `IMemoryCache` keyed by commander slug, long TTL. Route through the existing RestSharp + Polly named-pipeline pattern (`ResiliencePipelineFactory`) — add an `edhrec` pipeline or reuse; **do not** `new HttpClient()`.

### B. Cut Lab must be able to add a NON-basic card (core invariant change — highest risk)
Today only basics can be materialized into the working list. To add a spell:
- **State/adjustment model:** extend the added-card representation beyond `IsAddedBasic` to a general "added card" carrying name + type line + color identity (needed downstream).
- **Working-list derivation** (`CutLabWorkingList.Derive`, the added-basic block at `:72-79`): materialize added non-basics too — needs the card's `TypeLine` (fetch via `IScryfallCardLookupService`, analogous to how basics use `CutLabBasicLands.TryResolve`).
- **Legality:** singleton (`LegalMax=1` for non-basics), color-identity-legal vs commander, not banned, not already present.
- **Role assignment** (`CutLabRoleAssigner`) must classify the added card (from its type line/oracle) so it counts toward role floors.
- **Export reconstruction** (`CutLabExportService`): added non-basics appear in the finished 100 **and** as `ADD` patch lines; the existing color-identity + banlist verification on export must cover them.
- This is the biggest blast radius: it changes Cut Lab's "add only basics" invariant and touches state, derivation, roles, floors, and export.

## UI
- Near the "Tune quantities" panel (under-100 path), add an **"Add a recommended card"** panel:
  - Role selector populated from the commander's available EDHREC roles.
  - On pick → top-N EDHREC candidates for that role (name + inclusion% + type), excluding pool/off-color/banned. Reuse the **card popup** (this branch) for oracle text on each candidate.
  - "Add" per candidate → materializes it into the working list; counts update. Consider auto-locking added cards (intentional adds).
  - Graceful degradation if EDHREC unavailable (soft empty state, like Spellbook/Tagger).

## Recommended phasing (each independently testable)
1. **EDHREC commander service** — fetch + parse + category→role mapping + color/banlist filtering. Read-only, fixture-tested. No Cut Lab change yet.
2. **Cut Lab add-non-basic capability** — state/derivation/roles/export. Pure-domain where possible (Core), heavy xUnit coverage. Highest risk.
3. **UI** — the add panel + candidate list + popup reuse; e2e (add a draw card under 100 → count updates → export ADD line).

## Testing
- Unit: commander JSON parse + category→role mapping (fixture JSON + internal test seam, canonical pattern); add-non-basic working-list derivation + export CUT/ADD reconstruction; color/banlist filters.
- e2e: add recommended role card under 100 → sticky/count update → export includes it.

## Constraints / risks
- Prod fits (live fetch + cache; no big data shipped — the 618MB `edhrec.csv` stays offline-only).
- Non-commercial license posture preserved; contact UA required.
- Invariant change (add non-basics) is the main risk — phase it, test it hard.
- Additive mode softens Cut Lab's "trim to 100" identity; scope it to the under-100 / role-gap case.

## Open questions (resolve before build)
- Exact EDHREC category→role mapping (need a live commander-page sample).
- Top-N and ranking (raw popularity vs synergy score).
- Auto-lock added cards? 
- Partner/background/DFC commander slug handling.

## Out of scope
- No build this pass. No shipped-subset pipeline (live fetch chosen). No change to the cut/decision flow.
