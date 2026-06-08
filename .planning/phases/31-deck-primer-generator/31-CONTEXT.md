# Phase 31: Deck Primer Generator — Context

**Created:** 2026-06-08
**Source:** /gsd-discuss-phase 31

## Domain

A fourth paste-ready workflow (peer of DeckAnalysis / DeckComparison / CedhMetaGap): generate a bracket-routed, combo-grounded **Deck Primer** prompt from a decklist, with a 31-section catalog (5 collapsible groups), per-AI artifact variants (ChatGPT/Claude/Gemini), and zip round-trip. Requirements PRM-01..12 lock the WHAT; this phase decides the HOW.

This discussion clarifies implementation — it does not add capabilities. New capabilities belong in their own phase.

## Decisions

### D-1 — Spike (PRM-01) gates execution only, not planning
- **Plan all plans up front.** The prompt-builder plan specifies BOTH combo-ranking branches: priority-ranked (piece count / assembly cost / immediacy) when the spike verdict is "data sufficient", and AI-ranked fallback otherwise. The spike's recorded verdict selects the branch **at execution time** — no replan cycle.
- PRM-01 remains the **first execution unit** (gating): it must record (a) Spellbook `Instructions` richness verdict (sufficient / needs enrichment / fallback), (b) a representative cEDH primer prompt **byte-size** measured against paste caps, and (c) the EdhTop16 bracket-5 archetype-query verdict (does the GraphQL schema expose a meta-wide named-archetype query, or only the per-commander `commander(name:)` query?), before `DeckPrimerPacketService` is built.
- The spike verdict is recorded in a decision doc (e.g. `31-SPIKE.md` or a STATE decision) that the builder plan reads.

### D-2 — Combo grounding: two structurally separated blocks + null disclosure (PRM-05/08)
- Prompt contains a fenced **"Known Combos (ground truth — do not speculate)"** block populated from Commander Spellbook, then a **separate** fenced **"Speculative synergies (you propose)"** ask. The two are never merged — the AI must not invent combos in the ground-truth block.
- **Null-Spellbook disclosure:** when `CommanderSpellbookService.FindCombosAsync` returns null, emit an explicit line: *"No verified combos available — treat all synergies as speculative."* (graceful degradation, never a hard failure).
- Combo ranking (PRM-08) degrades per D-1: priority-ranked when spike confirms data sufficiency, AI-ranked fallback otherwise.

### D-3 — Bracket change applies preset but preserves per-bracket custom toggles (PRM-03/04/10)
- First visit to a bracket = that bracket's section **preset** (cEDH preset for 5, Casual/Upgraded preset for 1–4).
- If the user previously customized sections for that bracket, restore their custom set. **Presets seed; user edits stick.**
- Each bracket option carries its OWN preset — the section UI exposes per-bracket presets (per-`<option>` `data-preset-ids` or a serialized bracket→preset-ids map), so switching from the initial bracket to a DIFFERENT bracket on first visit (no saved state) applies THAT bracket's preset, not the initial bracket's.
- Persistence: localStorage **keyed per bracket** (mirror the `kb-selection.ts` localStorage + try/catch pattern). Bracket-scoped section gating still enforced (cEDH-only #24/#25 vs casual-only #26) regardless of stored toggles.

### D-4 — Gemini paste-cap: defensive char-cap guard like the analysis variant (PRM-01/09)
- Build the primer; if it exceeds the cap, **trim lowest-priority sections to fit** with a disclosure line — mirror the existing `GeminiAnalysisPromptVariant` `DefensivePromptCharCap` pattern (and the new `AiPlatform.PasteWarningBytes` surfaced on the analysis result).
- The exact threshold is set by the PRM-01 spike byte-size measurement. (Not a hard Gemini-disable; ChatGPT/Claude unaffected.)
- The Gemini trim guard only fires when Gemini is an **enabled** platform — i.e. when `DECKFLOW_GEMINI_ENABLED` is on and the `_AiSelector` exposes the Gemini radio. Gemini stays flag-gated per the v1.6 deferral; the primer reuses `_AiSelector` as-is and does NOT force-expose Gemini.

### Carried forward (locked before this discussion)
- **Mirror the analysis architecture:** new `DeckPrimerPacketService` + a primer prompt-variant registry + **3 decoupled variant files** (ChatGPT/Claude/Gemini). Prompt-variant decoupling is invariant — no shared prose; hand-edit all 3 for content changes (ADR 0001 + a1fa5ad revert lesson).
- **`PrimerAllowedNames` first:** add primer entries to the `PacketArtifactStore` allowlist as the first artifact-store task — `ReadEntries` THROWS `InvalidOperationException` on any entry name not in the active allowlist (it does NOT silently drop unlisted names; the allowlist must exist before any Build/Load method references it — Pitfall 2).
- **`{ get; init; }` guard:** every new record (section catalog entry, primer request/result DTOs) preserves the `init` accessor; include a System.Text.Json round-trip test per round-tripped record.

## Canonical Refs (MUST read before planning)

- `.planning/REQUIREMENTS.md` — PRM-01..12 (the locked requirements)
- `.planning/ROADMAP.md` — Phase 31 goal, success criteria, depends-on
- `docs/decisions/0001-prompt-variants-decoupled.md` — decoupling invariant (governs the 3 variant files)
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — the service to mirror (cache key, replay guard, BuildAsync pipeline)
- `DeckFlow.Web/Services/PromptBuilders/Analysis/` — `AnalysisPromptVariantRegistry.cs` + `{ChatGpt,Claude,Gemini}AnalysisPromptVariant.cs` + `IAnalysisPromptVariant.cs` — the registry+variant pattern to replicate for the primer
- `DeckFlow.Web/Services/PacketArtifactStore.cs` — zip allowlist + BuildZip/LoadFromZip + round-trip test pattern (PrimerAllowedNames goes here)
- `DeckFlow.Web/Models/AnalysisQuestionCatalog.cs` — the catalog analog for the 31-section catalog (groups, ids, help text, badges)
- `DeckFlow.Web/Models/CommanderBracketCatalog.cs` — bracket model (1–5) for preset routing
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` — combo ground-truth source (returns null on failure → D-2 disclosure)
- `DeckFlow.Web/Services/EdhTop16Client.cs` — bracket-5 matchups (PRM-06); exposes ONLY `SearchCommanderEntriesAsync(name)` (per-commander `commander(name:$name)` GraphQL). There is NO meta-wide top-archetypes query in the current client — the PRM-01 spike verifies whether the EdhTop16 schema exposes one; if not, bracket 5 falls back to the 5 generic strategy buckets (accepted degradation).
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — ramp/draw/interaction/tutor distribution numbers (PRM-07)
- `DeckFlow.Web/Views/Shared/_AiSelector.cshtml` — the AI target selector reused as-is; ChatGPT + Claude always shown, Gemini hidden unless `DECKFLOW_GEMINI_ENABLED` (persisted Gemini→ChatGPT rewrite preserved). "Enabled platforms" downstream means whatever this selector exposes.

## Code Context (reusable assets / patterns)

- **Workflow tab pattern:** primer page is a 4th `DeckPageTab` peer; reuse `_DeckToolTabs` / `_WorkflowStepTabs` and the existing import flow (URL/paste) for the decklist load (PRM-02).
- **Prompt-size warning:** `AiPlatform.PasteWarningBytes` (added 2026-06-08) — reuse for the primer size indicator/Gemini guard.
- **localStorage + per-AI variants:** `kb-selection.ts` (localStorage try/catch, hidden-field submit, progressive enhancement) is the model for section-selection persistence + the no-JS path.
- **Gemini defensive cap:** `GeminiAnalysisPromptVariant.DefensivePromptCharCap = 50000` is the template for D-4.

## Deferred Ideas

- None surfaced. (Gemini full-section paste-limit *workaround* beyond the defensive trim stays deferred to v1.6 per existing roadmap deferral — D-4 only adds the in-phase defensive guard.)

## Success Criteria

See `.planning/ROADMAP.md` Phase 31 "Success Criteria" + PRM-01..12 in REQUIREMENTS.md. Phase done when all 12 PRM requirements are delivered and the primer produces paste-ready output for ChatGPT/Claude/Gemini in one round-trip.
