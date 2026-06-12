# Phase 37: Retire Clip-Injection + Un-Dark KB Browse - Context / Scope

**Gathered:** 2026-06-10
**Status:** Ready for planning
**Source:** discuss-phase (interactive) — pivot from the Phase 35 MARGINAL gate (KBV-04).

<domain>
## Phase Boundary

Execute the Phase-35 retire pivot: **remove the Content KB clip-injection into deck-analysis prompts** (the gate-condemned feature), **keep the KB as a standalone browsable reference** (harvest → distill → browse), and **un-dark the `/content-kb` browse-site** so it is actually reachable. Full code removal of the injection path; the harvest/distill CLI + the browse-site + corpus are preserved.

Runs BEFORE the controller SRP split (now Phase 38) so the split operates on already-slimmed DeckController/DeckAnalysisPacketService.

NOT in scope: removing the KB browse-site, the harvest/distill CLI, or the corpus (those are KEPT — the user values the distilled videos as a reference). No retrieval re-design (the feature is retired, not re-fixed). No SRP split (Phase 38).
</domain>

<decisions>
## Decisions (locked)

- **Width = A-only.** Retire the analysis-prompt clip-injection + expert-selection ONLY. KEEP the browse-site (Group B) and harvest/distill CLI + Core content stores (Group C).
- **Method = full code removal** (not disable). Delete the injection code so no dead expert-selection threading survives into the Phase 38 SRP split.
- **Un-dark = YES.** Flip `content.kb.enabled` ON so `/content-kb` browse is live (it has only ever shipped dark). The prompt-injection attack surface leaves with the injection path; the remaining surface is read-only HTML browse — **must verify the browse views HTML-encode harvested clip/summary text (XSS) before/as part of un-darking.**
- **Why:** Phase 35's 5-deck blind gate (35-GATE-VERDICT.md) proved injection adds ~zero analysis value (0/5 decks changed a recommendation) and is corpus-bound. The KB-as-reference (browse the distilled videos) retains value per the user — keep that, drop the injection.

### REMOVE entirely (injection-only — confirmed no other consumers)
- `DeckFlow.Web/Services/ContentKbRelevanceService.cs` (incl. `IContentKbRelevanceService`, `ExpertSelection` record)
- `DeckFlow.Web/Services/ContentKbArchetypeDeriver.cs`
- `DeckFlow.Web/Services/ContentKbClipSanitizer.cs`
- `DeckFlow.Web/Models/ContentKbExcerpt.cs` (consumed only by the injection path + DeckAnalysisViewModel)
- `DeckFlow.Web/Controllers/Api/ContentKbSearchApiController.cs` (only the expert-selection typeahead used it; `/content-kb` search is client-side via `kb-entry-filter.ts`)
- `DeckFlow.Web/Views/Deck/_ContentKbPanel.cshtml` ("What Experts Say" panel)
- `DeckFlow.Web/wwwroot/ts/kb-selection.ts` (pin/follow expert-selection widget)
- Tests: `ContentKbRelevanceServiceTests.cs`, `ContentKbClipSanitizerTests.cs`, `ContentKbMergedClipsTests.cs`, `Spike001KbValueAbHarness.cs` (gate done; references the deleted retriever — must go), any ContentKbArchetypeDeriver tests.

### PARTIAL edits (strip injection wiring; keep the file)
- `DeckFlow.Web/Program.cs` — remove `IContentKbRelevanceService` + `ContentKbArchetypeDeriver` DI registrations.
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — remove `_contentKbRelevanceService` field/ctor param, the `GetMergedClipsAsync` call, `kbExcerpts`, `ExpertSelection` build, and `ExpertSelectionJson` serialization.
- `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` + the 3 `*AnalysisPromptVariant.cs` + `AnalysisPromptVariantRegistry.cs` — remove the `kbExcerpts` param + the `## Expert Context` block + Sanitize calls.
- `DeckFlow.Web/Models/DeckAnalysisRequest.cs` — remove `PinnedVideoIds`, `FollowedCreators`, `ExpertSelectionJson`.
- `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` — remove `ExpertContextClips`, `ResolvedPinTitles`.
- `DeckFlow.Web/Services/PacketArtifactStore.cs` + `PacketSessionCache.cs` — remove `ExpertSelectionJsonOptions`, `ExpertSelectionState`. **Back-compat:** old packet zips may carry `ExpertSelectionJson` — deserialize must IGNORE the unknown field gracefully (no throw on round-trip of pre-retire packets).
- `DeckFlow.Web/Controllers/DeckController.cs` — remove the `ExpertSelectionJson`/`ResolvedPinTitles` thread-through.
- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` — remove the `⭐ Expert Context` accordion (≈ lines 182-206) + the `_ContentKbPanel` render.
- `DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs` — remove `_relevanceService` + the `ScoreAllAsync` relevance-score preview; KEEP the curation grid.
- `DeckFlow.Web/Views/AdminContentKb/Index.cshtml` + `wwwroot/ts/content-kb-admin.ts` + the admin view model — remove the score-preview (PreviewCommander/Bracket inputs, score column/sort); KEEP grid + Phase-33 instant filter (`kb-entry-filter.ts`).

### KEEP (the KB reference — do NOT touch)
- `ContentKbController` + `Views/ContentKb/*` (public browse) — and UN-DARK it.
- `AdminContentKbController` curation grid + `ContentKbSeedLoader` + `Views/AdminContentKb/*` (minus score-preview).
- `IContentSiteIndexStore` / `ContentSiteIndexStore`; `ContentKbClipParser` (used by harvest distill); Core content stores (`ContentVideo/Source/HarvestRun/SiteIndexStore`).
- `DeckFlow.CLI` harvest/distill commands + `LlmDistillation*`.
- TS: `content-kb.ts`, `kb-entry-filter.ts`.
- `content.kb.enabled` flag (now gates the browse-site only) — flip default ON.

### Claude's Discretion (decide at plan/impl)
- Whether to keep `is_evergreen` column/migration (Phase 32) — it was an expert-selection tier; harmless to leave in the schema, remove only the UI/usage. Prefer leaving the column (no destructive migration), drop only the code that reads it for selection.
- Exact back-compat handling for old packet `ExpertSelectionJson` (ignore-unknown vs explicit drop).
</decisions>

<requirements>
## Phase Requirements (RET — derived from the KBV-04 retire pivot)

- [ ] **RET-01**: The deck-analysis prompt artifact no longer contains a `## Expert Context` block, and the deck-analysis page no longer shows the expert-selection widget or the "What Experts Say" panel — for all 3 AI variants.
- [ ] **RET-02**: The injection code is fully removed (the 3 retriever services + ContentKbExcerpt + the expert-selection types/endpoints/TS + injection params), the solution builds clean with zero new warnings, and no dead references remain.
- [ ] **RET-03**: The KB reference is intact end-to-end — `DeckFlow.CLI harvest`/`distill` still populate the corpus, and `/content-kb` browse + admin curation still list/show the distilled videos. (Smoke: harvest/distill unchanged; browse renders entries.)
- [ ] **RET-04**: `/content-kb` is un-darked (`content.kb.enabled` ON) and the browse/detail views HTML-encode harvested clip/summary text (no `Html.Raw` on untrusted content; XSS-safe) — verified.
- [ ] **RET-05**: Pre-retire packet zips (carrying `ExpertSelectionJson`) still load without error (graceful ignore of the removed field).
- [ ] **RET-06**: The deck-analysis page carries a short note/comment pointing users to the Knowledge Base for copyable expert prompts/advice they can paste into ChatGPT/Claude (replacing the removed auto-injection with a manual pointer). Since injection no longer surfaces clips inline, this note + a link to `/content-kb` is how a user finds the expert content. NOTE: a copyable-prompt affordance reportedly already existed — locate it at plan time and surface/link it rather than rebuilding; confirm whether the prompt-to-copy lives on the KB entry (detail) page or needs adding.
</requirements>

<success_criteria>
1. Generating a ChatGPT/Claude/Gemini deck-analysis prompt produces NO `## Expert Context` block; the DeckAnalysis page has no expert-selection accordion and no experts panel.
2. `dotnet build` (Web + Core + CLI + Tests) is clean, 0 new warnings; the deleted-type references are all gone (grep proves no `ContentKbRelevanceService`/`ContentKbExcerpt`/`ExpertSelection` outside removed files).
3. `/content-kb` is reachable (flag ON) and renders distilled entries with harvested text HTML-encoded (XSS-safe); harvest/distill CLI still works.
4. Loading a pre-retire packet zip with `ExpertSelectionJson` does not throw.
</success_criteria>

<deferred>
## Deferred / Out of Scope
- Removing the KB browse-site or harvest pipeline — KEPT by user decision (distilled videos have reference value).
- Re-fixing or re-validating retrieval — the feature is retired, not re-attempted.
- The DeckController/CommandRunners SRP split — Phase 38 (runs after this).
- Dropping the `is_evergreen` / expert-selection DB columns — leave columns (no destructive migration); only the code usage is removed.
</deferred>

---

*Phase: 37-retire-clip-injection*
*Context gathered: 2026-06-10 via discuss-phase (Phase 35 retire-pivot execution)*
