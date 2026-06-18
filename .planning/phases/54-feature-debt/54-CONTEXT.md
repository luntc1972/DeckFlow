# Phase 54: Feature debt - Context

**Gathered:** 2026-06-17
**Status:** Ready for planning
**Source:** Inline decision capture (orchestrator AskUserQuestion, plan-phase)

<domain>
## Phase Boundary

Two deferred feature-debt items. No net-new user features beyond unblocking Gemini.

- **FEAT-01** — Unblock the Gemini paste-limit path (`DECKFLOW_GEMINI_ENABLED`). Gemini was hidden in the UI since v1.4 because the full packet frequently exceeds Gemini's paste limit (truncates instructions → degraded output). All Gemini infra already exists and is wired (the flag in `Program.cs`, `AiPlatformOptions.GeminiEnabled`, and the six `Gemini*PromptVariant` services). The work is to VERIFY Gemini artifacts paste within Gemini's limit across the four workflows (analysis, comparison, meta-gap, primer), then ship the path unblocked.
- **FEAT-02** — Capture the `SpellbookCombo` ranking fields the parser currently drops (`manaValueNeeded`, `popularity`, `uses`) and use them to priority-rank combos in the Deck Primer (PRM-08). Today `SpellbookCombo` (`CommanderSpellbookService.cs:16`) holds only cards/results/instructions; `DeckPrimerPacketService.cs:420` has a known ranking stub.

In scope: parser field capture, primer ranking, Gemini verification + flag default. Out of scope: any change to the ChatGPT/Claude prompt prose or selector behavior; Studio/content-pipeline; SEO/growth.
</domain>

<decisions>
## Implementation Decisions

### FEAT-01 — Gemini unblock (LOCKED)
- **Verify, keep flag-gated default-off.** Confirm the flag works end-to-end and that Gemini artifacts paste within Gemini's limit when flipped, but **leave `DECKFLOW_GEMINI_ENABLED` default `false`**. The operator flips it on in prod. Do NOT change the default to true.
- Verification must cover all four workflows that emit a Gemini variant: deck analysis, deck comparison, cEDH meta-gap, and Deck Primer.
- "Within Gemini's limit" must be measured against Gemini's actual current paste/input ceiling (researcher to source the concrete figure) versus the real size of each generated Gemini artifact. If an artifact still exceeds the limit, that is a finding — record it; do NOT silently ship a truncating path. (Trimming the Gemini packet was explicitly NOT chosen for this cycle; if oversized, surface it rather than implement reduction.)

### FEAT-02 — Combo ranking (LOCKED)
- Add `manaValueNeeded`, `popularity`, and `uses` (already partially parsed) to the `SpellbookCombo` record and parse them from the commanderspellbook variant JSON. Preserve the existing `{ get; init; }` / record-positional conventions and System.Text.Json deserialization safety (per CLAUDE.md carve-out — never convert to get-only).
- **Ranking order: popularity DESC, then manaValueNeeded ASC as tiebreak.** Most-played combos first; cheaper-to-assemble breaks ties.
- Apply the ranking where `DeckPrimerPacketService` currently has the ranking stub (line ~420 / `ComboRankingVerdict`).
- Backward compatibility: the existing JSON artifact shape for non-primer consumers must not break; new fields are additive on the record. Missing/absent JSON properties must degrade gracefully (combo still parses with null/default ranking fields), matching the existing tolerant `uses` parse.

### Claude's Discretion
- Exact null/default handling for absent ranking fields, secondary tiebreak when both popularity and manaValueNeeded are equal/absent (stable order acceptable).
- Test placement: regression tests for new parse fields + ranking go in the existing test projects (`DeckFlow.Web.Tests`), matching the `CommanderSpellbookServiceTests` pattern.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### FEAT-01 (Gemini)
- `DeckFlow.Web/Program.cs` (lines ~75-81, 295-315) — flag parse + Gemini variant DI registration
- `DeckFlow.Web/Configuration/AiPlatformOptions.cs` — `GeminiEnabled` toggle + intent comment
- `DeckFlow.Web/Services/DeckPrimerPacketService.cs` (~131, 208, 512-518, 750) — Gemini enablement threading + enabled-platform filter
- `DeckFlow.Web/Services/PacketArtifactStore.cs` (~82, 216-240) — Gemini artifact file naming/emission

### FEAT-02 (combo ranking)
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` (record at :16, `ParseVariants` at :180, `uses` parse at :235) — model + parser
- `DeckFlow.Web/Services/DeckPrimerPacketService.cs` (:67 `ComboRankingVerdict`, :420 ranking stub comment) — where ranking applies
- `.planning/phases/31-*/31-SPIKE.md` (referenced by `ComboRankingVerdict` comment) — prior spike verdict on priority-rank branch

### Conventions
- `CLAUDE.md` (repo) — carve-outs: never convert `{ get; init; }` to get-only (STJ skips them); preserve switch expressions; LF line endings; changed-lines format gate
</canonical_refs>

<specifics>
## Specific Ideas

- commanderspellbook variant JSON: researcher to confirm exact property names/types for `manaValueNeeded`, `popularity`, `uses` and whether they are always present.
- Gemini paste limit: researcher to source the concrete current figure and a way to measure generated artifact size for the four workflows.
</specifics>

<deferred>
## Deferred Ideas

- Trimming/shrinking the Gemini packet to fit the paste limit — deferred this cycle (decision: surface oversize as a finding, don't implement reduction).
- Enabling Gemini by default in prod — deferred (operator-flipped).
- KB-12 (codex distill backend) — remains in Backlog, NOT part of Phase 54 (init misparse corrected).
</deferred>

---

*Phase: 54-feature-debt*
*Context gathered: 2026-06-17 via inline decision capture*
