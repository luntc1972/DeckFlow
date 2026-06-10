# Feature Research — v1.5 Deck Primer Generator + Content KB → Deck-Analysis Integration

**Domain:** MTG cEDH/Commander paste-ready AI prompt workflow; expert-content injection into deck-analysis prompts
**Researched:** 2026-06-03
**Confidence:** HIGH on primer section catalog (grounded in real Moxfield primers + BlazeHero/Eisenherz community templates + design note from 4 real primers); HIGH on KB injection pattern (well-established RAG/context-injection industry pattern); MEDIUM on combo-data richness (Commander Spellbook data structure confirmed via search, exact field completeness TBD from spike); HIGH on existing-system reuse (codebase context grounded)
**Audience:** `gsd-roadmapper` — phase structure, sequencing, complexity for two new features only

> **Scope note:** This file scopes the **two new v1.5 workflow features only**: (A) Deck Primer Generator and (B) Content KB → deck-analysis integration. Housekeeping items (Core doc backfill, KB-12 Codex distill backend, VERIFICATION.md hygiene) are not re-researched here — they are carry-forward work from v1.4. Pre-made design decisions in `.planning/seeds/deck-primer-generator.md` and `.planning/notes/deck-primer-prompt-design.md` are treated as settled and are not relitigated. Research builds on top of those decisions.

---

## Feature A: Deck Primer Generator (New Paste-Ready Workflow)

A new fourth workflow tab (peer of DeckAnalysis / DeckComparison / CedhMetaGap): user provides decklist + bracket selection, DeckFlow builds a paste-ready AI prompt that produces a complete Moxfield-formatted primer in one round-trip.

### What Real Moxfield Primers Contain (community grounding)

Research surveyed community primer guides (BlazeHero's Guide to Writing Primers, Eisenherz' cEDH Primer Template, The Mana Base Primers: A Primer) and the design note's 4-primer union. Confirmed section taxonomy is complete and accurate. Key community findings:

- **Critical pain points for hand-writing:** combo lines (step-by-step format) and matchup notes are cited by every source as the hardest to write. This directly validates the DeckFlow grounding strategy: inject Spellbook combo data (pieces → numbered steps → result) and EdhTop16 archetypes (matchup buckets).
- **AI-generated primers are known to be verbose and hollow without grounding data.** Community feedback on AI-assisted primers: "ungodly amounts of filler" and "insufficient info for a newcomer to play the deck properly." The DeckFlow approach of grounding combos, categories, and archetypes before asking AI to narrate is the right countermeasure.
- **cEDH-specific sections (Must-Counter, Counter Cheat Sheet) are expected by the cEDH community** but are out-of-place in casual primers. The bracket-routed preset model handles this correctly.
- **Change Log section** appears in most real primers. Simple to support as an always-off default section (AI cannot write it meaningfully from deck data alone; user populates over time). Flag as optional in the catalog.

### Table Stakes

Features cEDH/Commander players expect a primer generator to provide. Missing any of these = the output is not a real primer.

| Feature | Why Expected | Complexity | Notes |
|---|---|---|---|
| Decklist input using existing import flow (URL or paste) | Every DeckFlow workflow uses the same loader. Users expect consistency. | LOW | Reuse `IDeckEntryLoader` + existing validation. No new input surface needed. |
| Bracket selector with preset section defaults | Bracket drives what sections are appropriate (cEDH vs casual). Without presets, users face a 31-item wall of checkboxes — confirmed anti-pattern. | LOW | Dropdown (Bracket 1–5) pre-populates the section group toggles. Two presets: cEDH + Casual/Upgraded. Already designed. |
| 31-section catalog organized into 5 collapsible groups | The section union of real primers; the collapsible grouping eliminates the "wall of checkboxes" UX failure. Industry-standard accordion pattern for long option lists. | MEDIUM | Groups: Identity (#1–7), Combos (#10–12, #20), Gameplay (#13–19), Matchups (#21–26), Maintenance (#8, #9, #27–31). Per design note. |
| Per-section on/off toggle within groups | Power users need to exclude sections irrelevant to their deck (e.g., no combo deck → uncheck #10–12). | LOW | Simple checkbox per section within each collapsible group. State collected as bool array on form POST. |
| Combo lines section grounded by Commander Spellbook data | Community universally cites combo-line write-up as most painful section. AI cannot reliably generate correct MTG combo steps from card names alone — hallucination risk is very high. Spellbook provides: pieces, numbered steps, result. | MEDIUM | Inject fetched combos as structured ground truth in the prompt. Add fenced speculative block asking AI to extend cautiously. Reuse existing `ICommanderSpellbookService`. |
| Matchup section bracket-routed | Bracket 5 (cEDH) → EdhTop16 named archetypes (already fetched by `IEdhTop16Client`). Brackets 1–4 → 5 generic strategy buckets (Aggro / Control / Midrange / Combo / Stax-Hate). | MEDIUM | No new data sources. Routes on bracket value already carried in form submission. |
| Deck Identity / Engine grounded by category data | Category knowledge (ramp, card draw, interaction, combo-piece, tutor) gives AI a structural skeleton instead of asking it to infer from 99 card names. Mulligan guide and engine breakdown both rely on this. | MEDIUM | Reuse `ICategoryKnowledgeStore` category lookups. Already used by deck-analysis prompt builders. |
| Paste-ready prompt artifact, stored via PacketArtifactStore | Core Value: output the user pastes into ChatGPT/Claude/Gemini. Output must be stored on disk exactly like DeckAnalysis / DeckComparison artifacts. | LOW | Reuse `PacketArtifactStore`. Follow `DeckPrimerPacketService` pattern from seed. |
| Per-AI variant (ChatGPT / Claude / Gemini) | All three existing analysis workflows produce per-AI output. Users will expect the same selector here. | LOW | Reuse `AiPlatform` sealed record + per-AI branch pattern from v1.2 Phase 10. Claude gets `<result>` wrapper stripped; Gemini gets split-if-over-threshold. |
| Bracket-scoped section visibility (cEDH-only sections hidden for casual; casual-only hidden for cEDH) | Sections #24/#25 (Must-Counter, Counter Cheat Sheet) are meaningless in a bracket-1 casual pod; section #26 (Meta Positioning) is meaningless at bracket 5. Showing them to the wrong bracket = confusing noise. | LOW | Pre-check/uncheck driven by preset. UI: sections outside bracket scope are either hidden or grayed with a tooltip. |

### Differentiators

Features that make the primer generator better than a user copy-pasting their decklist into ChatGPT and asking it to "write a primer."

| Feature | Value Proposition | Complexity | Notes |
|---|---|---|---|
| Spellbook speculative-combo fence in prompt | AI extending beyond known combos is valuable but unreliable. Clearly fencing the speculative block ("verify these interactions") lets users get AI creativity without mistaking hallucinated combos for ground truth. Already decided. | LOW | Prompt template: `## Known Combos (verified, inject as-is)\n{spellbook_combos}\n\n## Possible Synergies (speculative — verify before publishing)\n[ask AI to extend]` |
| Category-derived mulligan heuristics (ramp/draw/payoff distribution) | "Keep hands with at least 2 ramp + 1 card draw source" is more useful than "keep good hands." Category buckets enable this. Community primer guides consistently flag mulligan as a high-skill section worth detailing. | MEDIUM | Compute distribution from category tags: `ramp_count`, `card_draw_count`, `interaction_count`, `tutor_count` injected as numeric context. AI writes the prose; numbers ground the guidance. |
| Combo priority framework using Spellbook data richness (spike gating) | Ranking combo lines by: (a) number of pieces, (b) mana cost of assembly, (c) whether they win immediately vs set up — is only possible if Spellbook data includes step count, color requirements, and result type. The `spike-combo-data-to-primer-grounding` todo exists to validate this. | MEDIUM | **Gate on spike result.** If Spellbook steps/result fields are sufficiently structured, inject ranked combo list. If not, AI ranks based on card names only (acceptable fallback). |
| Section groupings remember user's last selection (localStorage) | Power user opens the primer generator for their 15th deck and doesn't want to re-configure the same section set. | LOW | Store last selection per bracket preset in `localStorage`. ~20 lines TS. Not required for launch but trivial to add. |
| Section count badge on group headers | Shows "3/7 sections selected" on the Identity group header when collapsed. Avoids the "did I turn that on?" uncertainty without expanding. | LOW | Pure CSS + TS data-attribute update on toggle. |
| Help text modal per section explaining what good AI output looks like | Each section has a `?` link: "What does a good Combo Lines section contain?" Especially useful for new primer writers who don't know the standard. | LOW | Static help copy per section. Reuse existing `_InfoTooltip` pattern or Help center links. |

### Anti-Features (DO NOT BUILD in v1.5)

| Anti-Feature | Why | What to Do Instead |
|---|---|---|
| **Automatic primer publishing to Moxfield** | Moxfield has no public write API. Scraping + form-submit automation is brittle and violates ToS. Core Value is paste-ready output — the user does the publish step. | User copies primer from the artifact preview and pastes into Moxfield's editor. |
| **EDHREC integration for matchup data** | No EDHREC API access. Scraping EDHREC crosses the same TOS lines as Moxfield write automation. The design note explicitly excludes EDHREC in v1. EdhTop16 archetypes + generic buckets fully satisfy brackets 1–5 without it. | Use EdhTop16 named archetypes (bracket 5) and 5-bucket strategy model (brackets 1–4). |
| **Per-card narrative for all 99 cards (Individual Card Roles section #9 for full list)** | 99 card-by-card descriptions in a single prompt will exceed context window for the AI and produce token-bloated output. Real primers focus card descriptions on key cards / non-obvious choices, not the entire list. | In the prompt, instruct AI to cover the top 15–20 key/non-obvious cards, grouped by category. Category data identifies which cards carry the highest role density. |
| **"Minimal" preset in v1.5** | Seed notes Minimal (#2 Identity + #10 Win Cons + #11 Combo Lines + #14 Mulligan) as a future preset. Three presets at launch adds form complexity for marginal differentiation. Casual/Upgraded already covers low-section-count use. | Ship cEDH + Casual/Upgraded only. Add Minimal in v1.6 if users request it. |
| **Real-time primer preview / streaming response** | DeckFlow is a prompt-artifact tool, not an analysis service. Streaming AI responses server-side changes the architecture fundamentally (SSE + AI API key management). | User pastes the prompt artifact into ChatGPT/Claude/Gemini and reads the response there. |
| **Multiple primer "versions" / history** | PacketArtifactStore already handles regeneration. Version history adds DB schema + admin UI complexity not warranted for v1. | User can re-run with different section selections. Each run overwrites the stored artifact. |
| **"Smart" section auto-detection from deck analysis** | Auto-inferring whether a deck needs the Pivot-Plans section (#19) from category data is complex, error-prone, and undermines user agency. | Bracket preset + user toggle. The grouping keeps decision cost low. |
| **Primer quality scoring or feedback loop** | Out of scope — no user account model in DeckFlow. Cannot collect and correlate feedback. | Feedback form already exists for general feedback. |

### Dependencies on Existing Infrastructure

| Existing System | How v1.5 Primer Uses It |
|---|---|
| `IDeckEntryLoader` + `IDeckSyncService` | Decklist loading. No change. |
| `ICommanderSpellbookService.FindCombosAsync` | Fetches combos for sections #10/#11/#20. Existing service. Check data richness (spike). |
| `IEdhTop16Client` | Fetches meta archetypes for bracket-5 matchup sections (#22/#23/#25). Existing client. |
| `ICategoryKnowledgeStore` | Category bucket distribution for mulligan (#14) and engine breakdown (#8). Existing store. |
| `PacketArtifactStore` | Stores paste-ready artifact. Identical pattern to DeckAnalysisPacketService. |
| `AiPlatform` sealed record | Per-AI dispatch. Gemini gets split-if-over-threshold. Same v1.2 Phase 10 pattern. |
| `_WorkflowStepTabs` partial | Navigation chrome. New "Deck Primer" tab added to the step strip. |
| `DeckController` | New action method (or new `PrimerController` — see Architecture note). |
| `_AiSelector` partial | AI selector on the primer form. Reuse unchanged. |
| Zip artifact filename convention (v1.3 RENAME-03) | `deck-primer-{ai}.txt` in download zip. AI-segment invariant preserved. |

**Architecture note on controller placement:** `DeckController` is already a god class flagged for future split. Adding a new primer action there grows the debt. Recommend a new `PrimerController` to isolate the new workflow. Not a hard requirement for v1.5, but cleaner and aligns with SOLID-S.

### Complexity

**LARGE overall: 5–8 plans across 2–3 phases.**

Natural phase splits:

1. **Primer form + section model + bracket preset logic** — `DeckPrimer.cshtml`, view model, controller action, section selection state, collapsible group TS, bracket-to-preset mapping. No AI logic yet.
2. **DeckPrimerPacketService — prompt builder with grounding** — Spellbook combos injected, EdhTop16 archetypes injected, category data injected, per-AI variants, fenced speculative block, artifact stored.
3. **Combo data richness spike** — verify Spellbook steps/result field structure for combo priority ranking. Should run BEFORE phase 2 finalizes the combo section template. Low-effort read of the API response.

Phases 1–2 can be one numbered phase if the spike (phase 3) runs first and confirms Spellbook data structure. The spike is documented as a todo (`spike-combo-data-to-primer-grounding`) and should be the first execution unit.

---

## Feature B: Content KB → Deck-Analysis Integration ("What Experts Say")

Injects curated expert content (clips from harvested videos/podcasts, already stored in v1.4 KB tables) into deck-analysis prompts AND surfaces matching clips in a UI panel on the analysis result page. Deferred from v1.4 per project scope boundary.

Prerequisite: prod flag `content.kb.enabled` must be flipped ON. This is part of this feature's work, not a separate task.

### How Expert Content Injection Works (industry pattern)

The standard pattern is Retrieval-Augmented Generation (RAG): retrieve semantically relevant passages from a curated store → inject them into the prompt as grounded context → AI generates analysis informed by real expert knowledge rather than training data alone.

For DeckFlow's specific shape (prompt-artifact tool, not a live AI call), RAG takes a modified form:
- **Retrieval happens server-side** (DeckFlow queries `clips` table, scores relevance, selects top-N clips).
- **Injection happens into the prompt artifact** (selected clips are formatted as a `## Expert Context` section in the artifact the user pastes).
- **The AI receives expert quotes as grounded context** it cites in its analysis, reducing hallucination on commander-specific or archetype-specific strategy.
- **The UI panel shows which clips were injected** (attribution, timestamp, source name, video title) so users understand what knowledge informed the prompt and can deep-link to the source.

This is NOT a real-time RAG system. DeckFlow pre-builds the context and embeds it in a paste artifact. Simpler, cheaper, no vector DB required.

### Relevance Matching Strategy

The v1.4 KB stores clips tagged with:
- `archetype` (controlled vocabulary: voltron, aristocrats, stax, combo, etc.)
- `format/bracket` (bracket-1 through bracket-5-cedh)
- `card_category` (ramp, removal, card-draw, tutors, interaction, wincon, etc.)

For a deck-analysis prompt injection, relevance matching is keyword/tag-based (not embedding-based):
1. **Commander name match** — clips tagged with the commander's name in free-text tags (high weight).
2. **Archetype match** — deck's detected archetype (from category distribution) matched against `archetype` tag (medium weight).
3. **Bracket match** — deck's bracket input matched against `format/bracket` tag (filter, not score).
4. **Category match** — deck's dominant card categories matched against `card_category` tags (low weight, tiebreaker).

No vector embeddings in v1.5 — the controlled vocabulary tags are sufficient for meaningful retrieval at the current KB size. Embedding search can be added in v1.6+ when the corpus grows large enough to need it.

**Selection:** Top-K clips by match score (K configurable, suggested default K=3–5). Clip text length capped to avoid bloating prompt artifact (suggest ~150 words per clip = 3–7 sentences, consistent with v1.4 clip granularity target). Total expert context block ≤ ~750 words across all clips.

### Staleness Model

Clips harvested from KB should be surfaced with a publication date. Community considers content from 6+ months ago potentially stale for fast-moving metas (cEDH meta shifts frequently). UI panel must show clip harvest date + source video publication date. No automatic expiry — operator decides when to re-harvest. Admin `is_kept` flag (v1.4 differentiator) is the manual curation gate.

### Table Stakes

Features users expect from a "What Experts Say" integration. Missing any = the feature ships as marketing-only, not functional.

| Feature | Why Expected | Complexity | Notes |
|---|---|---|---|
| `content.kb.enabled` flag flipped ON before injection feature can work | Flag was explicitly deferred from v1.4. It is a prerequisite for this entire feature — flip via `/Admin/Flags` or env var as appropriate. | LOW | Verify in Render dashboard prod. Add to the execution checklist. |
| Relevant clips injected into deck-analysis prompt artifact as a grounded `## Expert Context` section | Core of the feature. Users want AI analysis informed by real expert content, not AI hallucinations about their commander. The injection must appear in the paste artifact — UI panel alone is not sufficient. | MEDIUM | Server-side: query clips, score by tag relevance, select top-K, format into prompt section. Inject into all 5 prompt builders (analysis, set-upgrade, comparison, follow-up, meta-gap) where relevant. |
| "What experts say" UI panel on deck-analysis result page | Users need to see WHICH clips were injected and WHERE they came from. Attribution is not optional — it grounds trust in the injection. Industry pattern: accordion or sidebar with source attribution. | MEDIUM | New Razor partial `_ExpertContextPanel.cshtml`. Shows per-clip: excerpt text, source name, video title, timestamp deep-link, harvest date. Attached to DeckAnalysis result view. |
| Clip attribution with source name + video title + timestamp deep-link | Without attribution users cannot verify the clip is real or find its context. RAG best practice: always surface provenance. | LOW | Data already stored in v1.4 tables (`clips`, `videos`, `sources`). Emit `youtube.com/watch?v=ID&t=NNs` link per clip. |
| Clip harvest date shown in panel | Users need to assess staleness. cEDH meta moves fast. A 2-year-old clip about Thrasios is likely outdated for the current meta. | LOW | `videos.published_at` column available in v1.4 schema. Display as relative age ("8 months ago") + absolute date on hover. |
| "No relevant clips found" graceful degradation | When KB has no clips matching the commander/archetype/bracket, prompt artifact must still work. Panel shows empty state, prompt omits the expert section. | LOW | Guard at the service layer: `if (clips.Count == 0) skip injection`. UI panel shows: "No expert content found for this commander yet. Consider harvesting more sources." |
| Bracket filter applied to clip selection | cEDH-bracketed content is not relevant for a bracket-2 casual deck analysis. Bracket mismatch produces noise. | LOW | Filter `WHERE bracket_tag = deck_bracket` OR `bracket_tag IS NULL` (general content). |
| Admin `is_kept` flag respected (curated clips only) | `is_kept = false` clips were rejected by the operator as low-quality. They must not appear in prompts. | LOW | `WHERE clips.is_kept = true` — already the v1.4 schema intent. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---|---|---|---|
| Commander-name match as highest-weight signal | A clip specifically discussing Najeela, the Blade-Blossom is directly relevant to a Najeela analysis. This is the kill-it-or-not relevance signal. | LOW | Store commander card name in free-text tags during harvest tagger pass. Match against commander card in the loaded deck. |
| Inject expert context into ALL prompt variants (not just analysis) | Set-upgrade prompt benefits from "experts recommend X card for this archetype." Comparison prompt benefits from "experts note this weakness in the archetype." | MEDIUM | Per-prompt-builder injection. Analysis and meta-gap prompts get the fullest injection; comparison and set-upgrade get a lighter slice (1–2 clips, not 5). |
| "Expert says" pull-quote formatting in the prompt artifact | Formatting the clips as block-quotes with attribution (rather than flat prose) helps the AI treat them as authoritative context rather than part of its own reasoning. | LOW | Prompt template: `> [Source: {channel_name}, {video_title}, {timestamp}]: "{clip_text}"`. Per RAG best-practice: labeled source passages. |
| Relevance score shown in admin view (not user-facing) | Helps operator understand why specific clips surface for a given commander/archetype and tune `is_kept` decisions. | LOW | Admin-only: show match score alongside clip in the sources admin view. Compute at retrieval time, don't persist. |
| Panel collapsed by default (expand-on-click) | Expert context panel should not dominate the analysis result page. Most users care about the prompt, not the citation list. | LOW | CSS `<details>`/`<summary>` element or existing accordion pattern. Panel renders collapsed with summary: "3 expert clips injected." |
| KB source diversity indicator | "2 clips from The Command Zone, 1 from Salty Spitoon Radio" — ensures the injection isn't dominated by a single channel's viewpoint. | LOW | Render source names in panel header. Group clips by source in display order. |

### Anti-Features (DO NOT BUILD in v1.5)

| Anti-Feature | Why | What to Do Instead |
|---|---|---|
| **Embedding-based semantic search for clip retrieval** | DeckFlow has no vector DB infrastructure. Adding pgvector to the Render Postgres instance + computing embeddings for all clips is a substantial new dependency. At current KB size (single-operator harvest), tag-based matching is sufficient and cheaper. | Tag-based relevance matching (archetype + commander name + bracket + category). Revisit embeddings in v1.6 when corpus exceeds ~1000 clips. |
| **Real-time KB query on every page load** | Deck-analysis page already makes multiple upstream calls (Scryfall, Spellbook, EdhTop16). Adding a KB query adds latency. The KB query should happen server-side at prompt-build time, cached with the artifact. | Cache the clip selection with the packet artifact. If the user re-downloads, serve the same clip list. No live re-query on page load. |
| **User ability to add/edit KB clips from the analysis page** | Single-operator admin model. Public-user contribution to the KB opens content moderation, attribution, and spam surfaces. | Clip curation stays in `/Admin/Sources/*` (v1.4 admin surface). |
| **Personalized "your favorite channels" clip weighting** | No user accounts in DeckFlow. Cannot store per-user preferences. | Single global operator-curated KB. `is_kept` flag is the curation gate. |
| **Clip translation for non-English content** | KB scope is English-first (v1.4 decision). Multi-language translation adds complexity and cost. | Filter by `language = 'en'` during clip selection. Language column exists in v1.4 schema. |
| **"Inject all clips" mode** | Prompt bloat is the enemy. 50 clips × 150 words = 7500 words = prompt too large to paste without hitting context limits. Injection must be bounded. | Top-K selection (default K=5). Let operator configure K in feature flags if needed. |
| **Expert panel on non-analysis pages (Sync, Convert, Categories)** | KB content is analysis-relevant, not workflow-tool-relevant. Sync and Convert pages have no prompt artifact — nothing to inject into. | Expert panel only on pages that generate AI prompt artifacts: DeckAnalysis, DeckComparison, CedhMetaGap, DeckPrimer. |

### Dependencies on Existing Infrastructure

| Existing System | How v1.5 KB Integration Uses It |
|---|---|
| `clips`, `videos`, `sources`, `content_tags` tables (v1.4 KB schema) | Clip retrieval query. Tag matching. Deep-link construction. `is_kept` filter. |
| `IFeatureFlagStore` / `IFeatureFlagCache` | `content.kb.enabled` flag gates all injection code paths. Flag must be ON in prod. |
| `ICategoryKnowledgeStore` (category distribution for deck) | Feeds `card_category` match signal. Already called for existing analysis prompts. |
| All 5 prompt builders (analysis, set-upgrade, comparison, follow-up, meta-gap) | Each gets an injection point for the `## Expert Context` block. Low-coupling: each builder checks flag, queries clips, formats block, or skips. |
| `PacketArtifactStore` | Expert clip selection can be included in the stored artifact metadata (for consistent re-downloads). |
| `RelationalDatabaseConnection` + `IRelationalDialect` | KB query uses same connection abstraction. Works on both SQLite (local dev) and Postgres (prod). |
| `ScryfallCardLookupService` | Commander card lookup for name-match signal. Already available. |
| Admin `/Admin/Sources/*` (v1.4) | `is_kept` curation already in place. No change needed. |
| `BasicAuthMiddleware` | Expert panel is public-facing (on user-facing analysis page). Admin relevance-score view is gated. |

### Complexity

**MEDIUM overall: 3–5 plans across 1–2 phases.**

Natural phase splits:

1. **KB injection service + prompt builder integration** — New `IExpertContextService` that queries clips by relevance (commander + archetype + bracket + category signals), scores, selects top-K, formats `## Expert Context` block. Wire into all 5 prompt builders behind `content.kb.enabled` flag. Flip flag in prod.
2. **"What Experts Say" UI panel** — `_ExpertContextPanel.cshtml` partial. Render on DeckAnalysis (and optionally DeckComparison, CedhMetaGap, DeckPrimer) result views. Attribution with deep-link, harvest date, collapse-by-default. Graceful empty state.

Phases 1 and 2 can be one numbered phase given the moderate scope, or two small phases if the team wants clear checkpoints between backend injection and frontend panel.

---

## Feature C: Housekeeping Debt Bundle

*(Documented briefly for completeness — not re-researched, all items are carry-forward from v1.4 closed phases.)*

Three items:

1. **DeckFlow.Core XML-doc backfill + gate widen** — 186 undocumented sites in Core flagged at v1.4 close (Phase 23). Widen `editorconfig` doc-warning severity to cover `DeckFlow.Core/*.cs`. Mechanical Codex task.
2. **KB-12 Codex distill backend** — Adds Codex CLI as a third distill backend (alongside OpenAI + Claude). Low-complexity adapter addition. Backlogged since Phase 21.2.
3. **VERIFICATION.md hygiene** — 7 v1.4 phases missing VERIFICATION files; stale UAT labels. Pure documentation catch-up. No code changes.

**Complexity:** SMALL per item (1 plan each). Can be bundled into a single housekeeping phase. Not on the critical path for Features A or B.

---

## Feature Dependencies

```
Feature A (Deck Primer Generator)
  ├──requires──> spike-combo-data-to-primer-grounding (confirm Spellbook field richness)
  ├──reuses──>   ICommanderSpellbookService (v1.3 — no change needed)
  ├──reuses──>   IEdhTop16Client (v1.3 — no change needed)
  ├──reuses──>   ICategoryKnowledgeStore (v1.4 — no change needed)
  └──reuses──>   PacketArtifactStore, AiPlatform, _AiSelector (v1.2/1.3 — no change needed)

Feature B (Content KB Integration)
  ├──requires──> content.kb.enabled = true (prod flag flip — prerequisite step)
  ├──requires──> v1.4 KB tables: clips, videos, sources, content_tags (already shipped)
  ├──reuses──>   ICategoryKnowledgeStore (deck category distribution for relevance signal)
  ├──touches──>  All 5 prompt builders (inject Expert Context block behind flag guard)
  └──reuses──>   IFeatureFlagStore / IFeatureFlagCache (existing flag gate pattern)

Feature C (Housekeeping)
  └──independent──> no dependencies on A or B; can run in parallel or after

Feature A ──no dependency on──> Feature B (independent; can ship in either order)
Feature B ──no dependency on──> Feature A (independent)
```

### Dependency Notes

- **Spike-before-Phase-A-plan:** The `spike-combo-data-to-primer-grounding` should run as the first unit of work in Feature A. Confirm Spellbook API response includes structured `steps` + `result` fields adequate for combo-section narration. Low effort: call the existing API, inspect one combo for a known 2-card line. Outcome determines whether the combo priority framework differentiator is achievable in v1.5.
- **`content.kb.enabled` flip is a Feature B prerequisite:** It must be the first step of Feature B execution, not a post-ship followup. Without it, zero clips are returned and the feature cannot be UAT'd.
- **Feature A and B are independent:** They do not need to ship together or in a specific order. However, shipping A first (Deck Primer Generator) is recommended because it is the larger user-facing feature and the explicit milestone headline.
- **Prompt builder touches in Feature B:** All 5 prompt builders need an injection point. This is additive (guarded by flag, no change to existing prompt structure when flag is off). Regression risk is LOW.

---

## MVP Definition for v1.5

### Must Ship (Feature A — Primer Generator)

- [ ] Decklist input + bracket selector + section preset (cEDH + Casual/Upgraded)
- [ ] 5 collapsible section groups with per-section toggles
- [ ] Spellbook combo injection (ground truth) + speculative-combo fence
- [ ] EdhTop16 matchup archetypes (bracket 5) / generic buckets (brackets 1–4)
- [ ] Category-derived mulligan heuristics (ramp/draw distribution)
- [ ] Paste-ready artifact per-AI (ChatGPT / Claude / Gemini)
- [ ] Artifact stored via PacketArtifactStore

### Must Ship (Feature B — KB Integration)

- [ ] `content.kb.enabled` flipped ON in prod
- [ ] Clip retrieval service with commander + archetype + bracket tag matching
- [ ] `## Expert Context` block injected into deck-analysis prompt artifacts
- [ ] "What Experts Say" panel on DeckAnalysis result page (attribution + deep-link + harvest date)
- [ ] Graceful empty state when no matching clips found

### Must Ship (Feature C — Housekeeping)

- [ ] DeckFlow.Core XML-doc backfill (186 sites) + gate widened to Core
- [ ] KB-12 Codex distill backend adapter
- [ ] VERIFICATION.md hygiene (7 missing files + stale labels)

### Add After v1.5 (future milestones)

- [ ] Minimal preset for Primer Generator (quick 4-section primer)
- [ ] Embedding-based semantic clip retrieval (when KB corpus > ~1000 clips)
- [ ] Expert panel on DeckComparison, CedhMetaGap, DeckPrimer result views (start with DeckAnalysis only in v1.5)
- [ ] Scheduled KB harvest (cron) — explicitly deferred in v1.4
- [ ] localStorage section-selection persistence across sessions

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---|---|---|---|
| Primer generator — form + section model | HIGH | MEDIUM | P1 |
| Primer generator — prompt builder with grounding | HIGH | MEDIUM | P1 |
| Combo data richness spike | HIGH (gates above) | LOW | P1 (first) |
| KB integration — clip injection into prompt | HIGH | MEDIUM | P1 |
| KB integration — "What Experts Say" UI panel | MEDIUM | MEDIUM | P1 |
| `content.kb.enabled` prod flag flip | HIGH (prerequisite) | LOW | P1 |
| Core doc backfill (housekeeping) | LOW user / HIGH quality-gate | MEDIUM | P2 |
| KB-12 Codex distill backend | LOW user / MEDIUM ops value | LOW | P2 |
| VERIFICATION.md hygiene | LOW user / MEDIUM process | LOW | P2 |
| Expert panel on Comparison / CedhMetaGap / Primer pages | LOW-MEDIUM | LOW | P3 (v1.6) |
| Minimal primer preset | LOW | LOW | P3 (v1.6) |
| Embedding-based clip retrieval | MEDIUM | HIGH | P3 (v1.6+) |

---

## Sequencing Recommendations (for gsd-roadmapper)

**Critical path:** spike → Feature A prompt builder → Feature B injection service → Feature B UI panel

**Suggested phase ordering:**

```
Phase 1: combo-data spike + Feature A form/section model
  └──outputs──> confirmed Spellbook field richness + working primer form

Phase 2: Feature A prompt builder (DeckPrimerPacketService)
  └──requires──> Phase 1 spike result

Phase 3: Feature B KB injection service + flag flip + prompt builder wiring
  └──requires──> v1.4 KB tables (already shipped)

Phase 4: Feature B "What Experts Say" UI panel
  └──requires──> Phase 3 injection service

Phase 5: Housekeeping bundle (Core doc + KB-12 + VERIFICATION hygiene)
  └──independent──> can run any time; recommend last to not block Features A/B
```

**Recommended bundling:** Phases 1+2 as one numbered phase ("Deck Primer Generator"), Phases 3+4 as one numbered phase ("Content KB Integration"), Phase 5 as one numbered phase ("v1.5 Housekeeping"). Total: 3 numbered phases.

**Off-critical-path:** Housekeeping (Phase 5) can run in parallel with or after any of Phases 1–4. It does not block or unblock any feature work.

---

## Sources

### MTG Primer Community Research

- [BlazeHero's Guide to Writing Primers — Moxfield](https://moxfield.com/decks/icKufeoz_U-4HMNlorzgnw/primer) — HIGH confidence (authoritative community primer guide; cited in multiple MTG communities)
- [Eisenherz' cEDH Primer Template — Moxfield](https://moxfield.com/decks/5NlTg6-6o0C8-weQEtt0tQ/primer) — HIGH confidence (cEDH-specific sections well-documented)
- [The Metaworker — Primers: A Primer — The Mana Base](https://themanabase.com/the-metaworker-primers-a-primer/) — HIGH confidence (independent analysis of what sections matter; grounded in fetched content)
- [Moxfield — Writing Primers Help Page](https://moxfield.com/help/writing-primers) — MEDIUM confidence (403 at fetch time; confirmed existence via search; content cross-referenced via search result snippets)
- [Greg's MTG Deck Tools — AI Scribe Tool](https://www.mtgdecktools.com/blog/commander-deck-tools-launch) — MEDIUM confidence (competitor analysis; confirms AI primer generation market exists; noted weaknesses of AI-only primers)
- [Commander Spellbook — About](https://commanderspellbook.com/about/) — MEDIUM confidence (confirmed combo fields via search; exact step/result structure requires spike to verify)
- [Commander Spellbook — GitHub Backend](https://github.com/SpaceCowMedia/commander-spellbook-backend) — MEDIUM confidence (schema reference; steps/results fields confirmed present in backend model)

### RAG / Context Injection Patterns

- [IBM — What is RAG (Retrieval Augmented Generation)](https://www.ibm.com/think/topics/retrieval-augmented-generation) — HIGH confidence (authoritative industry overview)
- [AWS — What is RAG](https://aws.amazon.com/what-is/retrieval-augmented-generation/) — HIGH confidence
- [PromptingGuide.ai — RAG](https://www.promptingguide.ai/techniques/rag) — MEDIUM confidence (well-maintained prompt engineering reference)
- [apxml.com — Attributing Sources in RAG Generated Output](https://apxml.com/courses/getting-started-rag/chapter-4-rag-generation-augmentation/attributing-sources) — MEDIUM confidence (attribution UI pattern described)
- [Atlan — Context Poisoning (staleness in RAG)](https://atlan.com/know/context-poisoning/) — MEDIUM confidence (staleness / freshness handling in RAG)
- [Towards Data Science — Temporal RAG](https://towardsdatascience.com/rag-is-blind-to-time-i-built-a-temporal-layer-to-fix-it-in-production/) — MEDIUM confidence (time-weighted ranking, freshness signals)

---
*Feature research for: v1.5 Deck Primer Generator + Content KB Integration*
*Researched: 2026-06-03*
