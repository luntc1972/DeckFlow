# Project Research Summary

**Project:** DeckFlow v1.5 — Deck Primer Generator + Content KB Integration + Housekeeping
**Domain:** MTG Commander / cEDH paste-ready AI prompt workflow (brownfield ASP.NET 10 app)
**Researched:** 2026-06-03
**Confidence:** HIGH

## Executive Summary

DeckFlow v1.5 is a brownfield milestone with three tracks of work on top of a fully operational
ASP.NET 10 / RestSharp / Polly / Razor MVC system. Track A (Deck Primer Generator) adds a
fourth packet workflow — a peer of DeckAnalysis / DeckComparison / CedhMetaGap — that generates
a complete Moxfield-formatted primer prompt in one round-trip from decklist + bracket selection.
Track B (Content KB Integration) wires the v1.4 Knowledge Base into deck-analysis prompts as
expert-grounded context ("What Experts Say" RAG-style injection). Track C (Housekeeping) closes
carry-forward debt: 186 undocumented Core sites, the deferred KB-12 Codex distill backend, and
VERIFICATION.md hygiene. The defining characteristic of this milestone is that no new
dependencies are required — every building block (data sources, HTTP infrastructure, prompt
variants, artifact storage, session caching, feature flags) is already registered in DI and
proven in production. The work is composition, not acquisition.

The recommended build order is: KB-12 Codex backend (fast win, pure Core, no web surface) ->
Core XML-doc backfill -> Core doc gate widen -> Content KB integration (smaller web surface,
validates the prod flag-flip path) -> Deck Primer Generator (largest, most visible, no upstream
blockers from the other tracks). Tracks A and B are independent of each other; either can ship
first, but Track A is the milestone headline and should be the majority focus. The combo-data
spike (spike-combo-data-to-primer-grounding) must run before the primer service is implemented
to confirm Spellbook field richness and prompt-size characteristics — it is the only sequencing
hard dependency within Track A.

The three most consequential risks are: (1) primer prompts blowing the Gemini paste cap due to
31-section combinatorics — measure prompt size during the spike and gate Gemini on the primer
the same way it is gated on analysis; (2) AI hallucinating combo lines because the grounded and
speculative sections are not structurally fenced in the emitted prompt — model these as two
distinct code blocks with explicit null-state handling; and (3) KB injection injecting irrelevant
content due to tag-vocabulary mismatch between the Tagger's functional categories and KB content
authors' strategic tags — enforce AND-based two-dimension tag matching with a minimum threshold,
and audit the live tag distribution before writing any matching code.

## Key Findings

### Recommended Stack

Zero new dependencies. All v1.5 capabilities are fully deliverable by composing existing
installed services. The primer generator follows the DeckAnalysisPacketService packet/zip
pattern exactly: IDeckPrimerPacketService + sealed implementation + result record + three
IPrimerPromptVariant implementations (ChatGPT, Claude, Gemini) + PrimerPromptVariantRegistry
dispatching on AiPlatform. KB injection follows a read-path RAG pattern:
IContentSiteIndexStore.GetPublishedRowsAsync() -> in-memory tag-overlap scoring ->
ContentKbArtifactPathResolver disk reads -> ContentArtifactParser.SplitHeader for front-matter
stripping -> appended to prompt text via a new IContentKbRelevanceService.

**Core existing technologies consumed by v1.5 (unchanged, no version changes):**
- ICommanderSpellbookService — combo ground truth for primer sections 10, 11, 20
- IEdhTop16Client — named cEDH archetypes for bracket-5 matchup sections 22, 23, 25
- ICategoryKnowledgeStore — engine / mulligan / tutor category buckets for primer sections 8, 9, 14, 17, 29
- IContentSiteIndexStore + ContentKbArtifactPathResolver + ContentArtifactParser — KB retrieval pipeline
- PacketArtifactStore + PacketSessionCache — artifact zip + preview-to-download short-circuit
- AiPlatform value object + IFeatureFlagCache — AI dispatch and content.kb.enabled flag gate
- LlmDistillationProviderFactory — KB-12 codex backend plugs into the existing "codex" stub

**What NOT to add:**
- No templating engine (Scriban, Fluid, etc.) — StringBuilder + raw-string literals is the established pattern
- No Microsoft.Extensions.Http.Resilience standard handler — prohibited by project constraints
- No Microsoft.SemanticKernel or vector DB — prompt artifacts are pre-built for user paste; no server-side LLM calls
- No EDHREC integration — explicitly out of scope; EdhTop16 + 5 generic strategy buckets fully covers brackets 1-5

### Expected Features

**Must have — Deck Primer Generator (Track A):**
- Decklist input (URL or paste) using existing import flow — users expect consistency with all other workflows
- Bracket selector (1-5) with preset section defaults: cEDH preset + Casual/Upgraded preset
- 31-section catalog organized into 5 collapsible groups (Identity, Combos, Gameplay, Matchups, Maintenance)
- Per-section on/off toggles within groups — power users exclude irrelevant sections
- Combo lines section grounded by Commander Spellbook (pieces + steps + result) with speculative-fence separator
- Matchup section bracket-routed: bracket 5 -> EdhTop16 named archetypes; brackets 1-4 -> 5 generic strategy buckets
- Category-derived mulligan heuristics (ramp/draw/payoff counts injected as numeric context)
- Paste-ready artifact per AI (ChatGPT / Claude / Gemini), stored via PacketArtifactStore
- Zip round-trip: download + re-upload with section selections restored

**Must have — Content KB Integration (Track B):**
- content.kb.enabled prod flag flipped ON (prerequisite step, first action of Track B)
- Clip retrieval by tag overlap (archetype + bracket, AND-based, minimum threshold)
- ## Expert Context block injected into deck-analysis prompt artifacts with attribution block-quotes
- "What Experts Say" UI panel on DeckAnalysis result page (source, title, timestamp deep-link, harvest date)
- Graceful empty-state: panel hidden when no matching clips; prompt continues unchanged
- Content freshness disclosure in prompt header; staleness warning in Admin Flags UI next to the toggle

**Must have — Housekeeping (Track C):**
- 186 DeckFlow.Core undocumented XML-doc sites backfilled (dependency order: Models -> Parsing -> Diffing -> Exporting -> Filtering -> Normalization -> Knowledge -> Storage -> Content -> Integration -> Loading -> Reporting)
- .editorconfig gate widened to [DeckFlow.Core/**.cs] in final commit only (after all 186 sites are clean)
- KB-12: CodexCliLlmDistillationService replaces the NotSupportedException stub in LlmDistillationProviderFactory; uses CliEnvelopeKind.Raw (not ClaudeJson)
- VERIFICATION.md hygiene: 7 v1.4 phases missing VERIFICATION files + stale UAT labels

**Differentiators (should have, v1.5):**
- Spellbook speculative-combo fence in prompt — clearly labeled, separate code blocks
- Category-derived mulligan heuristics (ramp/draw/interaction/tutor distribution as numeric grounding)
- Section count badges on collapsed group headers
- DeckPageTab.DeckPrimer entry (tab int = 12) in the nav step strip
- Admin is_kept flag respected in clip selection (curated-only injection)
- KB source diversity indicator in the "What Experts Say" panel

**Defer to v1.6+:**
- Minimal primer preset (4-section quick primer) — low demand signal, adds form complexity
- Embedding-based semantic clip retrieval (add when KB corpus exceeds ~1000 clips)
- Expert panel on DeckComparison / CedhMetaGap / DeckPrimer result views (start with DeckAnalysis only)
- Scheduled KB harvest (cron) — explicitly deferred in v1.4
- localStorage section-selection persistence across sessions

### Architecture Approach

All three v1.5 tracks plug into existing seams without structural surgery. Track A follows the
established packet service pattern (DeckAnalysisPacketService is the template): interface +
sealed implementation + result record + prompt variant registry + per-AI variant classes. The
primer intentionally omits Scryfall card hydration (not needed for the 31 sections) — this makes
it cheaper and faster than DeckAnalysis. Track B is a pure read-path addition to the web tier:
ContentKbRelevanceService runs after deck load, before prompt composition, and injects a
contentKbBlock string that each IAnalysisPromptVariant.Build appends independently (variants
remain intentionally decoupled — prose is never shared). Track C housekeeping is fully orthogonal
to both A and B.

**New components:**

Track A:
1. IDeckPrimerPacketService / DeckPrimerPacketService — orchestrates deck load, combo fetch, category query, EdhTop16 fetch (bracket 5 only), prompt composition
2. IPrimerPromptVariant + ChatGptPrimerPromptVariant, ClaudePrimerPromptVariant, GeminiPrimerPromptVariant — per-AI prompt strategy (intentionally decoupled)
3. PrimerPromptVariantRegistry — dispatches by AiPlatform, falls back to Default
4. DeckPrimerRequest / DeckPrimerViewModel / DeckPrimerPacketResult — model layer
5. PrimerSectionCatalog static class — 31-section definitions, group assignments, preset defaults
6. PacketArtifactStore.BuildPrimerZip / LoadPrimerFromZip + PrimerAllowedNames — zip round-trip
7. DeckPrimer.cshtml — collapsible-group section selector, bracket dropdown, generate/download/upload

Track B:
1. IContentKbRelevanceService / ContentKbRelevanceService — loads index, scores by tag overlap, reads .md artifacts, returns IReadOnlyList<ContentKbExcerpt> (cap 3-5)
2. ContentKbExcerpt record — slim: source, title, url, body (~200 words)
3. _ContentKbPanel.cshtml — "What Experts Say" collapsible panel partial

Track C:
1. CodexCliLlmDistillationService — new sealed class using CliEnvelopeKind.Raw; plugs into existing LlmDistillationProviderFactory codex branch

**Modified existing files (Track B only):**
DeckAnalysisPacketService, IAnalysisPromptVariant (+ 3 variants), DeckAnalysisViewModel, DeckAnalysis.cshtml, Program.cs

### Critical Pitfalls

1. **Primer paste-cap blowout (Gemini)** — 20+ section primer with Spellbook + EdhTop16 data routinely hits 60-100KB; Gemini web UI caps at 30-60KB; ground truth is silently truncated. Prevention: measure prompt size during the spike; add PromptSizeWarning field to DeckPrimerPacketResult; gate Gemini on the primer the same way analysis does it. Address during spike phase, not verification.

2. **Hallucinated combo lines — grounded/speculative fence failure** — if FindCombosAsync returns null and the speculative ask is still emitted without a null-state disclosure, AI invents all combo lines from card names alone. Prevention: model combo section as two structurally distinct code blocks (KnownCombosBlock present only when non-null; SpeculativeComboAsk always present but explicitly labeled "speculative"). Unit test: BuildPrimerPrompt_NullComboResult_EmitsSpeculativeDisclosure.

3. **KB injection injects irrelevant content (tag-mismatch)** — KB archetype tags (voltron/aristocrats/stax) don't map to Tagger functional categories (ramp/draw/removal). Result: reanimator deck gets stax content. Prevention: audit live KB tag distribution before writing matching code; enforce AND-based two-dimension matching (bracket + archetype); set a minimum overlap threshold; never emit an empty "## What Experts Say" section header.

4. **PrimerAllowedNames omitted — silent zip data loss** — PacketArtifactStore.ReadEntries silently drops any artifact name not in the active allowlist; reusing PacketAllowedNames drops all primer-specific artifacts without throwing. Prevention: add PrimerAllowedNames as the FIRST task in the primer artifact store implementation; include round-trip unit test.

5. **get; init; -> get; regression on new records** — Codex or IDE formatting can silently drop init;, causing System.Text.Json to skip properties during serialization. This has already broken EdhTop16Client deserialization. Prevention: include the constraint verbatim in every phase CONTEXT.md; add serialization round-trip tests for every new request/result record used in zip artifacts.

## Implications for Roadmap

Based on combined research, the recommended phase structure is four numbered phases. Tracks A and
B are independent; the ordering below places Track C housekeeping items as the first two phases
because they are lowest-risk, have no web surface, and produce a clean build baseline before the
larger feature tracks land.

### Phase 1: KB-12 Codex Distill Backend + VERIFICATION.md Hygiene

**Rationale:** Pure Core change, no web surface, zero blast radius on existing features. The
"codex" factory slot is already stubbed — this is a bounded replace-one-throw-with-a-return task.
Ships fast, closes a v1.4 deferred item, and proves the Codex CLI envelope shape (Raw, not
ClaudeJson) is understood before the larger phases begin. VERIFICATION.md hygiene is pure
documentation (no code) and can bundle into the same phase.

**Delivers:** Working DECKFLOW_LLM_PROVIDER=codex distillation path; NotSupportedException stub
removed; LlmDistillationProviderFactoryTests gains Codex_ReturnsCliBackend test; 7 missing
VERIFICATION files + stale UAT labels resolved.

**Addresses:** Housekeeping Track C (KB-12 + VERIFICATION items)

**Avoids:** Pitfall 9 (stringly-typed extension / wrong envelope kind — bounded diff makes review trivial)

### Phase 2: Core XML-Doc Backfill + Gate Widen

**Rationale:** 186 undocumented Core sites must be documented before the gate is widened —
widening first breaks the build immediately. Must be complete before any other phase touches
DeckFlow.Core files for risk management. Backfill is mechanical (Codex is ideal) but must be
split by namespace to keep diffs reviewable. The editorconfig gate widen is the final commit.

**Delivers:** All 186 Core sites documented across 6-8 namespace-scoped plans; [DeckFlow.Core/**.cs]
CS1591 gate added to .editorconfig in the final commit; dotnet build -warnaserror:CS1591 clean
from a fresh obj/.

**Addresses:** Housekeeping Track C (doc backfill item)

**Avoids:** Pitfall 8 (gate widened before backfill complete — build breaks and blocks CI for
all parallel work)

### Phase 3: Content KB -> Deck-Analysis Integration

**Rationale:** Smaller web surface than the Primer Generator; validates the prod
content.kb.enabled flag-flip path in production before the Primer takes any downstream
dependency on KB context. Tag-distribution audit runs before any matching code is written.
KB injection is additive behind a flag guard — no change to existing prompt output when flag
is off.

**Delivers:** ContentKbRelevanceService with tag-overlap matching; ## Expert Context block
injected into deck-analysis prompts; _ContentKbPanel.cshtml on DeckAnalysis result page;
content.kb.enabled flipped ON in prod (with fresh harvest run first); freshness disclosure
in prompt header and staleness warning in Admin Flags UI.

**Addresses:** Content KB Integration (Track B), all Track B must-haves

**Avoids:** Pitfall 3 (irrelevant content injection — audit tag distribution first), Pitfall 4
(prompt budget competition — measure size before injection and enforce budget hierarchy), Pitfall
7 (stale content — fresh harvest is a prerequisite UAT step)

### Phase 4: Deck Primer Generator

**Rationale:** Largest feature, milestone headline, no upstream blockers once Phases 1-3 are
complete. The combo-data spike runs as the first execution unit to confirm Spellbook field
richness and prompt-size characteristics before the service is implemented. Split into four
sub-phases to limit blast radius.

**Delivers:**
- 4a: DeckPrimerRequest, DeckPrimerViewModel, DeckPrimerPacketResult, DeckPageTab.DeckPrimer, routing stubs, PacketArtifactStore.BuildPrimerZip, PrimerAllowedNames
- 4b: DeckPrimerPacketService.BuildAsync — deck load, Spellbook combo fetch (with null handling), category queries, bracket routing, EdhTop16 for bracket-5, structural KnownCombos/SpeculativeCombos blocks
- 4c: All three IPrimerPromptVariant implementations + PrimerPromptVariantRegistry + Program.cs DI registration; DeckPrimer.cshtml with collapsible groups and bracket selector
- 4d: Download/upload round-trip, PacketSessionCache key, JS section-preset logic, PromptSizeWarning field wired to UI, Gemini gating

**Addresses:** Deck Primer Generator (Track A), all Track A must-haves

**Avoids:** Pitfall 1 (paste-cap — spike measures size; Gemini gated; PromptSizeWarning in result), Pitfall 2 (hallucinated combos — two-block structural separation with null-state disclosure), Pitfall 5 (PrimerAllowedNames as first task in 4a), Pitfall 6 (get; init; in CONTEXT.md for all sub-phases), Pitfall 10 (section-combinatorics under-testing — PrimerSectionRenderTests written alongside conditional logic)

### Phase Ordering Rationale

- KB-12 first: Zero blast radius, closes backlog debt, reveals Codex CLI envelope behavior before it matters
- Doc backfill before Primer/KB integration: All phases touch DeckFlow.Core indirectly; a clean build signal is a prerequisite. Gate widened only after all sites are documented.
- KB integration before Primer: Smaller surface validates the prod flag-flip path; Primer may benefit from KB context in a future phase
- Primer last: Largest scope, most sub-phases, zero upstream blockers; benefits from the cleaner build environment established by Phase 2
- Tracks A and B are independent: The roadmapper may reorder Phases 3 and 4 if the user's priority ranking changes — neither depends on the other

### Research Flags

Phases needing deeper research during planning:
- **Phase 3 (KB integration), pre-implementation step:** Run a live tag-distribution audit on the prod KB (clips + content_tags tables) to understand actual tag vocabulary density before writing the relevance matching code. One-time query, not a full research phase, but must happen before ContentKbRelevanceService is specced.
- **Phase 4 (Primer), spike at start of 4b:** spike-combo-data-to-primer-grounding — inspect a real Spellbook API response for a known 2-card combo to confirm Instructions field richness for step-by-step narration in section 11. Low effort (one API call + read), high-gating value. Must complete before 4b is planned.

Phases with standard patterns (skip research-phase):
- **Phase 1 (KB-12):** Pattern is fully documented in codebase; diff is bounded (replace one throw)
- **Phase 2 (doc backfill):** Mechanical pattern; namespace dependency order documented in ARCHITECTURE.md; no API research needed
- **Phase 4, sub-phases 4a/4c/4d:** Follow established DeckAnalysisPacketService / AnalysisPromptVariantRegistry patterns exactly; no research needed beyond reading the template files

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Direct codebase inspection; every referenced service and interface verified to exist in production; no external library research required because no new libraries are added |
| Features | HIGH | Primer section catalog grounded in 4 real Moxfield primers + community guides (BlazeHero, Eisenherz, The Mana Base); RAG/KB injection pattern grounded in IBM/AWS/PromptingGuide sources; codebase reuse map verified |
| Architecture | HIGH | All source files inspected directly; patterns are extensions of verified production code; component boundaries well-defined; no speculative architecture |
| Pitfalls | HIGH (most) / MEDIUM (AI output quality) | Pitfalls 1, 4, 5, 6, 8, 9 grounded in direct codebase inspection and prior milestone post-mortems (HIGH); Pitfalls 2, 3, 7, 10 grounded in LLM behavior patterns + project retrospective lessons (MEDIUM) |

**Overall confidence:** HIGH

### Gaps to Address

- **Spellbook Instructions field richness (MEDIUM confidence):** SpellbookCombo.Instructions is confirmed to exist and is used in the existing DeckAnalysis prompt, but whether it is detailed enough for step-by-step primer narration in section 11 is a prompt-design question. The spike resolves this. Fallback: AI narrates from card names — no stack change either way.
- **EdhTop16 archetype label quality for primer matchup section (MEDIUM confidence):** IEdhTop16Client returns raw tournament metadata, not pre-labeled archetype strings. The primer passes raw entry data to the AI to derive labels — same approach MetaGapService uses successfully, but primer framing differs. Spike UAT should include a bracket-5 deck.
- **Live KB tag-distribution density:** Unknown until a query runs against prod. The relevance-matching design is correct in principle; matching thresholds and dimension weights must be calibrated against actual data. Handle during Phase 3 planning.
- **content.kb.enabled prod flip timing:** Flag has been OFF since v1.4. Before Phase 3 UAT, a fresh harvest must be triggered. Ops prerequisite, not a code gap — must appear explicitly in Phase 3 execution checklist.

## Sources

### Primary (HIGH confidence — direct codebase inspection)

- DeckFlow.Web/Services/DeckAnalysisPacketService.cs — packet service template, combo null-handling at lines 562-564
- DeckFlow.Web/Services/PacketArtifactStore.cs — allowlist pattern (three HashSet<string> sets), zip manifest conventions
- DeckFlow.Web/Services/PromptBuilders/Analysis/ — variant interface signature, registry pattern, intentional prose duplication
- DeckFlow.Web/Services/CommanderSpellbookService.cs — FindCombosAsync null-on-failure confirmed
- DeckFlow.Core/Content/IContentSiteIndexStore.cs + ContentSiteIndexStore.cs — no tag filter in SQL (in-memory only)
- DeckFlow.Core/Knowledge/ContentArtifactSpec.cs — ContentSiteIndexRow tag fields, DeserializeTags
- DeckFlow.Web/Services/ContentKbArtifactPathResolver.cs — artifact path resolution
- DeckFlow.Web/Services/ContentArtifactParser.cs — SplitHeader front-matter parsing
- DeckFlow.Core/Integration/LlmDistillationProviderFactory.cs — "codex" stub at lines 49-53
- DeckFlow.Core/Integration/CliCommandSpec.cs — CliEnvelopeKind.Raw vs ClaudeJson
- DeckFlow.Web/Models/DeckPageTab.cs — existing tab enum values 0-11; new entry = 12
- .editorconfig lines 93-115 — CS1591 gate scope: none globally, warning in [DeckFlow.Web/**.cs] only
- .planning/seeds/deck-primer-generator.md — feature shape and pre-made decisions
- .planning/notes/deck-primer-prompt-design.md — 31-section catalog, bracket routing, combo handling
- .planning/RETROSPECTIVE.md v1.0/v1.2/v1.3 — paste-cap lesson, get; init; regression history
- .planning/v1.4-MILESTONE-AUDIT.md — 186-site Core doc debt, KB-12 deferral, content.kb.enabled still OFF

### Secondary (MEDIUM confidence — community research)

- BlazeHero's Guide to Writing Primers (Moxfield) — primer section taxonomy, community pain points
- Eisenherz' cEDH Primer Template (Moxfield) — cEDH-specific sections validation
- The Metaworker — Primers: A Primer (The Mana Base) — section importance ranking
- IBM — What is RAG — RAG pattern validation
- AWS — What is RAG — RAG pattern corroboration
- Commander Spellbook GitHub Backend — steps/results fields confirmed present in backend model
- PromptingGuide.ai — RAG — attribution / provenance UI patterns

---
*Research completed: 2026-06-03*
*Ready for roadmap: yes*
