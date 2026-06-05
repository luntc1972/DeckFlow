# Phase 30: Content KB Integration - Context

**Gathered:** 2026-06-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire curated Content KB knowledge into the deck-analysis workflow and take the KB live in production:

1. **KBI-01** — `content.kb.enabled` flipped ON in prod after a fresh harvest run; at least one clip visible on the public KB browse page.
2. **KBI-02/03** — Generated deck-analysis prompt artifacts include a `## Expert Context` block: up to K=5 curated clip excerpts (block-quoted, attributed, ≤150 words each, `is_kept = true` only), selected by tag-based relevance; block absent — not empty — when nothing matches.
3. **KBI-04/05** — DeckAnalysis result page shows a collapsed "What Experts Say" panel (source channel, title, timestamp deep-link, harvest date, grouped by channel); hidden entirely when no clips matched.
4. **KBI-06** — Admin sources view exposes a per-clip relevance match score for curation tuning.

DeckAnalysis-only in v1.5 (other workflows/builders deferred per KBI-F01/F02).

**Hard pre-implementation guard (carried from v1.5 research + STATE.md):** a live tag-distribution audit on the prod KB (clips + content_tags / site-index tags) MUST run before any relevance-matching code is specced or written. Matching thresholds and dimension weights are calibrated against that audit, not assumed.

</domain>

<decisions>
## Implementation Decisions

### Clip granularity + curation (is_kept model)
- **D-01:** `is_kept` = the existing **artifact-level** `ContentSiteIndexRow.IsVisible` curation flag. NO new per-clip schema or per-clip keep/drop admin surface. Injection parses the `## Key Clips` section from visible artifacts' markdown at prompt-build time.
- **D-02:** Inject **clips only, never the Summary**, in document order within an artifact. K=5 total across all matched artifacts, filling from the best-scoring artifact first. Deterministic selection.
- **D-03:** The injected clip set (source, title, timestamp, excerpt, harvest date, score) is **persisted in the packet session + zip artifact** so the "What Experts Say" panel always shows exactly what the prompt contained and survives zip re-upload. Requires a zip allowlist entry + round-trip regression test (PacketArtifactStore silently drops non-allowlisted names — known pitfall).
- **D-04:** Clips exceeding the 150-word cap are **truncated at the last full sentence under the cap** with an ellipsis (rare — distill spec targets 1-3 sentences).

### Commander-name matching
- **D-05:** Commander relevance is **free-text matching**: the deck's commander name(s) searched against artifact title + summary + clip text at build time (normalized, partner-aware). No new tag dimension; no corpus re-distill — works with artifacts shipped today.
- **D-06:** A commander-name hit **counts as a relevance dimension** in the ≥2-dimension AND gate: commander hit + bracket match qualifies even when archetype tags don't align. Tag-only matches still require bracket + archetype per the research pitfall guard.

### Relevance scoring inputs
- **D-07:** Deck-side archetype signal is **derived from existing deck data** — category-knowledge distribution (e.g., tutor/counter-heavy → combo/control) plus the commander free-text hit. No new user-facing form control on DeckAnalysis.
- **D-08:** Admin KBI-06 score is a **live test-input preview**: the admin sources view gets a small commander + bracket input; per-clip scores compute on demand through the exact production scoring path, answering "why did/didn't this clip inject for deck X".

### Flag flip + harvest ops (KBI-01)
- **D-09:** **Flip early** — the flag flip is the first execution unit of the phase: incremental harvest → commit artifacts → deploy → admin curates rows visible → flip `content.kb.enabled` via live /Admin/Flags → verify browse page (SC1 done). KB browse runs live while injection is built, and the tag-distribution audit gets real prod data.
- **D-10:** Injection + panel are **gated by the same `content.kb.enabled` flag** — flag OFF means Expert Context block absent and panel hidden, reusing the KBI-05 empty-state code path. No second flag.
- **D-11:** "Fresh harvest" = **incremental top-up**: user runs the local CLI harvest+distill over the existing 5 channels to pick up videos published since the v1.4 run; existing artifacts unchanged. User runs the harvest locally (never auto-launched).

### Claude's Discretion
- Score weights, dimension weighting, and injection threshold values — planner decides, **calibrated against the mandatory live tag-distribution audit** (D-07 derivation rules included).
- Commander-name normalization and partner/background handling details.
- Expert Context block placement within the three decoupled AI prompt variants (ChatGPT/Claude/Gemini prose is intentionally duplicated — hand-edit all three; never extract shared guidance).
- Panel markup/CSS specifics (layout CSS in `site-common.css`, per the standing theme constraint; phase has UI hint = yes, `/gsd-ui-phase 30` available).
- Plan split and sequencing beyond D-09's "flip first" ordering.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + acceptance
- `.planning/ROADMAP.md` — Phase 30 block (goal, SC 1-4, depends-on Phase 28).
- `.planning/REQUIREMENTS.md` — KBI-01..06 definitions + KBI-F01..F04 deferrals + Out of Scope table (no embedding retrieval, no real-time KB query, no "inject all clips").

### Research (binding design guidance)
- `.planning/research/SUMMARY.md` — Track B (KB integration) design, key risks #3, open question on live tag density; "Phase 3 pre-implementation step" = the tag-distribution audit.
- `.planning/research/PITFALLS.md` — Pitfall 3 (tag-mismatch: AND-based ≥2 dimensions, score threshold, no empty section header), Pitfall 4 (prompt budget: ~4,000-char hard cap on injection, prefer excerpts), Pitfall 7 (stale content: fresh harvest prerequisite).
- `.planning/STATE.md` — Phase 30 pre-implementation guard wording ("live tag-distribution audit … before `ContentKbRelevanceService` is specced").

### Content KB code surfaces (injection inputs)
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — `ArtifactFileFormat` (front matter + `## Key Clips` shape), `ContentSiteIndexRow` (lines ~107+: `IsVisible`, tag lists, `ArtifactPath`), tag serialization helpers.
- `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` — the three allowlisted tag dimensions (15 archetypes, 5 brackets, 11 card categories). NO commander dimension — D-05 free-text match exists because of this.
- `DeckFlow.Core/Content/IContentSiteIndexStore.cs` + `ContentSiteIndexStore.cs` — `GetPublishedRowsAsync` (visible rows), visibility setters.
- `DeckFlow.Web/Services/ContentArtifactParser.cs` — front-matter/body splitter; clip parsing builds on this.
- `DeckFlow.Web/Services/ContentKbArtifactPathResolver.cs` — resolves repo `content-kb/` tree; artifact reads must go through it (path-traversal guards).
- `DeckFlow.Web/Services/ContentKbSeedLoader.cs` — seed-load path populating index rows after deploy.

### Injection + panel integration points
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — the deck-analysis packet builder Expert Context injects into; bracket at `request.TargetCommanderBracket` (~line 506/755); combo null-handling precedent at lines 562-564.
- `DeckFlow.Web/Services/PromptBuilders/` — per-AI prompt variants (ChatGPT/Claude/Gemini). Prose intentionally decoupled — edit all three by hand.
- `DeckFlow.Web/Services/PacketArtifactStore.cs` — zip allowlist pattern (D-03 needs a new allowlisted entry + round-trip test).
- `DeckFlow.Web/Controllers/ContentKbController.cs` — `[FeatureFlagGate("content.kb.enabled", …)]` usage pattern D-10 reuses.
- `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` + `DeckFlow.Web/Views/AdminContentKb/Index.cshtml` — admin sources view KBI-06 extends (D-08 preview input).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ContentSiteIndexRow.IsVisible` + admin curation UI (Phase 22) — D-01 reuses as the `is_kept` gate; zero schema change.
- `ContentArtifactParser.SplitHeader` — front-matter parsing; extend with a `## Key Clips` section parser.
- `ContentKbArtifactPathResolver` — safe artifact-file resolution (traversal-guarded) for build-time clip reads.
- `FeatureFlagGate` attribute + `IFeatureFlagCache` — D-10 flag gating for injection + panel.
- `PacketArtifactStore` allowlist + zip round-trip test pattern (AISEL-04 precedent) — D-03 persistence.
- Category-knowledge distribution data (ramp/draw/interaction/tutor counts already computed for analysis prompts) — D-07 archetype derivation input.

### Established Patterns
- Prompt-variant decoupling: ChatGPT/Claude/Gemini prose duplicated intentionally (a1fa5ad→b2ffba7 revert) — Expert Context block added per-variant by hand, never extracted to shared prose.
- `{ get; init; }` records for zip-serialized DTOs — System.Text.Json skips get-only properties; add serialization round-trip tests for any new record in the zip (known regression class).
- Graceful degradation: services return null/empty on no-match; controllers render without the section (CommanderSpellbookService precedent) — KBI-05 follows it.
- Layout CSS goes in `site-common.css`, never `site.css`; guild themes are standalone forks.

### Integration Points
- `DeckAnalysisPacketService` prompt assembly — `## Expert Context` block insertion (respecting the ~4KB injection cap and prompt budget hierarchy from Pitfall 4).
- DeckAnalysis result view (Views/Deck/) — collapsed "What Experts Say" panel.
- `AdminContentKb/Index.cshtml` — preview-match score column (D-08).
- Live /Admin/Flags — D-09 flip (user-performed, prod).

</code_context>

<specifics>
## Specific Ideas

- The Expert Context block must read as authoritative pull-quotes to the AI: block-quoted, attributed ("— Source Channel, *Video Title* [02:14]") per KBI-03.
- A zero-match build emits NO Expert Context header at all (an empty section confuses the AI — research Pitfall 3); panel shows a friendly empty message only when the flag is ON.
- Admin score view exists to tune `IsVisible` curation: "why did/didn't this clip inject for deck X" is the question D-08's preview input answers.

</specifics>

<deferred>
## Deferred Ideas

- Expert panel on DeckComparison / CedhMetaGap / DeckPrimer result pages — KBI-F01 (v1.6+).
- Expert Context injection into the other four prompt builders — KBI-F02 (v1.6+).
- Embedding-based semantic clip retrieval — KBI-F03 (corpus > ~1000 clips).
- Scheduled (cron) KB harvest cadence — KBI-F04.

### Reviewed Todos (not folded)
- `spike-combo-data-to-primer-grounding.md` — surfaced by todo matching (score 0.6) but tagged `resolves_phase: 31` (Deck Primer); keyword-noise match, deferred to Phase 31 (same call as Phase 28 discussion).

</deferred>

---

*Phase: 30-Content KB Integration*
*Context gathered: 2026-06-05*
