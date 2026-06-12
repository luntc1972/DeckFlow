# Phase 37: Retire Clip-Injection + Un-Dark KB Browse - Research

**Researched:** 2026-06-10
**Domain:** Brownfield code removal (C#/.NET 10 + Razor + TypeScript) — surgical excision of a failed feature path with back-compat and XSS-safety verification
**Confidence:** HIGH (every consumer claim is grep-verified against the live tree at file:line; build baseline captured)

## Summary

Phase 37 retires the Content-KB clip-injection path from deck analysis (gate-condemned in Phase 35) while keeping the `/content-kb` browse-site, the admin curation grid, the harvest/distill CLI, the Core content stores, and the corpus — and un-darks the browse-site by flipping `content.kb.enabled` ON. This research empirically verifies the CONTEXT.md remove/partial/keep lists against the actual code rather than restating them.

**The CONTEXT removal list is substantially correct** — the three retriever services, `ContentKbExcerpt`, the expert-selection types/endpoints/TS, and the `_ContentKbPanel` partial have no consumers outside the injection path and its tests. However, this research surfaces **five planner-must-handle discrepancies** the CONTEXT list under-specifies (detailed below): (1) a SECOND persisted packet field `ExpertContextJson` that CONTEXT never names, alongside the named `ExpertSelectionJson`; (2) `content-kb-admin.ts` is *entirely* score-preview and can be deleted wholesale, not partially edited; (3) the nav-link copy in `Home.cshtml` and `_DeckToolTabs.cshtml` advertises the removed injection feature and must be rewritten; (4) the DeckAnalysis accordion spans lines 182-232, not the CONTEXT estimate of 182-206; (5) `ContentKbArchetypeDeriver` depends on `ICategoryKnowledgeStore` (kept), so removing the deriver must not touch that store.

**Primary recommendation:** Sequence the removal bottom-up — delete the leaf TS/Razor/API first, then the prompt-variant `kbExcerpts` params, then `DeckAnalysisPacketService` injection wiring, then the request/viewmodel fields, then the services + DI, then tests — verifying `dotnet build` stays at the captured **0-warning / 0-error baseline** after each layer. Treat RET-05 (packet back-compat) and RET-04 (XSS) as explicit verification tasks, not assumptions: both already hold in the current code (forgiving zip-load + Markdig `.DisableHtml()`), and the job is to preserve them through the edits.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **Width = A-only.** Retire the analysis-prompt clip-injection + expert-selection ONLY. KEEP the browse-site (Group B) and harvest/distill CLI + Core content stores (Group C).
- **Method = full code removal** (not disable). Delete the injection code so no dead expert-selection threading survives into the Phase 38 SRP split.
- **Un-dark = YES.** Flip `content.kb.enabled` ON so `/content-kb` browse is live (it has only ever shipped dark). The prompt-injection attack surface leaves with the injection path; the remaining surface is read-only HTML browse — must verify the browse views HTML-encode harvested clip/summary text (XSS) before/as part of un-darking.
- **Why:** Phase 35's 5-deck blind gate proved injection adds ~zero analysis value (0/5 decks changed a recommendation) and is corpus-bound. The KB-as-reference retains value per the user — keep that, drop the injection.

REMOVE entirely (per CONTEXT): `ContentKbRelevanceService.cs` (+ `IContentKbRelevanceService`, `ExpertSelection` record), `ContentKbArchetypeDeriver.cs`, `ContentKbClipSanitizer.cs`, `ContentKbExcerpt.cs`, `ContentKbSearchApiController.cs`, `_ContentKbPanel.cshtml`, `kb-selection.ts`, and the named tests.

PARTIAL edits (per CONTEXT): `Program.cs`, `DeckAnalysisPacketService.cs`, the 3 `*AnalysisPromptVariant.cs` + `IAnalysisPromptVariant` + `AnalysisPromptVariantRegistry`, `DeckAnalysisRequest.cs`, `DeckAnalysisViewModel.cs`, `PacketArtifactStore.cs` + `PacketSessionCache.cs`, `DeckController.cs`, `DeckAnalysis.cshtml`, `AdminContentKbController.cs`, `AdminContentKb/Index.cshtml` + `content-kb-admin.ts` + admin view model.

KEEP (do NOT touch): `ContentKbController` + `Views/ContentKb/*` (UN-DARK), admin curation grid + `ContentKbSeedLoader` + `Views/AdminContentKb/*` (minus score-preview), `IContentSiteIndexStore`/`ContentSiteIndexStore`, `ContentKbClipParser`, Core content stores, CLI harvest/distill + `LlmDistillation*`, TS `content-kb.ts` + `kb-entry-filter.ts`, the `content.kb.enabled` flag (now gates browse only) — flip default ON.

### Claude's Discretion
- Whether to keep `is_evergreen` column/migration — prefer leaving the column (no destructive migration), drop only the code that reads it for selection.
- Exact back-compat handling for old packet `ExpertSelectionJson` (ignore-unknown vs explicit drop).

### Deferred Ideas (OUT OF SCOPE)
- Removing the KB browse-site or harvest pipeline — KEPT.
- Re-fixing/re-validating retrieval — retired, not re-attempted.
- The DeckController/CommandRunners SRP split — Phase 38.
- Dropping `is_evergreen` / expert-selection DB columns — leave columns; only code usage removed.
</user_constraints>

<phase_requirements>
## Phase Requirements

RET-01..06 are defined in `37-CONTEXT.md` and `ROADMAP.md` (lines 179-189); they are NOT in `REQUIREMENTS.md` (which carries the original v1.6 KBR/KBV/PHIL/KBD/SRP set — RET is the post-gate pivot, recorded only in CONTEXT + ROADMAP).

| ID | Description | Research Support |
|----|-------------|------------------|
| RET-01 | No `## Expert Context` block in prompt artifact; no expert-selection widget / "What Experts Say" panel — all 3 AI variants | `## Expert Context` literal appears in all 3 variants (ChatGpt:256, Claude:248, Gemini:263); widget = `kb-selection.ts` + accordion `DeckAnalysis.cshtml:182-232`; panel = `_ContentKbPanel.cshtml` rendered at `DeckAnalysis.cshtml:605` |
| RET-02 | Injection code fully removed; build clean, 0 new warnings; no dead refs | Removal graph verified below; build baseline = **0 warn / 0 err** (must stay there) |
| RET-03 | KB reference intact end-to-end (harvest/distill + browse + admin curation) | `ContentSiteIndexStore`, CLI harvest/distill, `ContentKbController`, curation grid all in KEEP set and have no dependency on removed types except the admin score-preview |
| RET-04 | `/content-kb` un-darked + browse/detail views HTML-encode harvested text (XSS-safe) | Flag default FALSE at `FeatureFlagStore.cs:180/190`; **zero** `Html.Raw`/`MarkupString` in `Views/ContentKb/*`; Detail.cshtml `@Model.RenderedHtml` is `HtmlString` from Markdig with **`.DisableHtml()`** (XSS mitigation) |
| RET-05 | Pre-retire packet zips (carrying `ExpertSelectionJson`) load without error | Load path is forgiving (allowed-name filter + try/catch `JsonException`); see Back-Compat section. **Discrepancy: there are TWO fields — `ExpertContextJson` AND `ExpertSelectionJson`.** |
| RET-06 | Deck-analysis page points users to KB for copyable prompts | **Copy affordance ALREADY EXISTS** on the KB *Detail* page (`Detail.cshtml:35-43`, wired by `content-kb.ts:84-114`). RET-06 = surface/link existing, NOT build new. |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Codex implements / Claude reviews.** This phase is planned by Claude; implementation dispatches to Codex (`cross_ai_execution: true`, `gpt-5.4`). Research is prescriptive so Codex has exact file:line targets.
- **Preserve LF line endings** (`.gitattributes`); **no formatter reflow** — touch only the lines that need touching. Removing members from a file must not re-indent or reformat untouched code.
- **No new dependencies** (NuGet/npm). This phase removes code; it must add zero packages.
- **`{ get; init; }` must not be collapsed to `{ get; }`** (System.Text.Json silently skips get-only properties — has broken deserialization before). Relevant: `ContentKbDetailViewModel.RenderedHtml`, `KbEntryRow`, request/viewmodel records all use `init`.
- **Plain default-author commits**, no Co-Authored-By; commit per logical change; README updated when behavior changes.
- **Testing:** VSTest is unreliable in WSL — rely on `dotnet build` clean (verified working: Windows `dotnet.exe`) plus push-and-watch CI. The repo HAS a test framework (xUnit) so test edits/deletions are required, not optional.
- **Compiled `.js` is gitignored** — never stage `wwwroot/js/*.js`. Deleting `kb-selection.ts` / `content-kb-admin.ts` means the build stops emitting their `.js`; do not hand-delete committed `.js` (there is none).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Clip retrieval/scoring (REMOVE) | API/Service (`DeckAnalysisPacketService` + `ContentKbRelevanceService`) | — | Pure server-side selection; no client role |
| Expert-selection widget (REMOVE) | Browser (`kb-selection.ts`) | Frontend SSR (accordion in `DeckAnalysis.cshtml`) | Client widget posts hidden inputs back to the analysis form |
| Expert-selection typeahead API (REMOVE) | API (`ContentKbSearchApiController`) | — | Consumed ONLY by `kb-selection.ts` |
| Packet persistence/back-compat (PARTIAL) | API/Service (`PacketArtifactStore`) | — | Zip round-trip; must tolerate legacy fields |
| KB browse (KEEP, un-dark) | Frontend SSR (`ContentKbController` + Razor) | Browser (`content-kb.ts` filter + copy) | Read-only HTML render of distilled artifacts |
| KB persistence (KEEP) | Database/Storage (`ContentSiteIndexStore`, Core) | — | Harvest/distill writes; browse reads |
| Admin curation (KEEP) / score-preview (REMOVE) | Frontend SSR (`AdminContentKbController`) | Browser (`content-kb-admin.ts` = preview only) | Grid stays; relevance-preview leaves with the service |

## Standard Stack

No new libraries — this is a removal phase. Existing relevant components (all KEPT):

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Markdig | 0.38.0 | Renders KB Detail body to HTML | Already the project's MD renderer; pipeline uses `.DisableHtml()` → XSS-safe |
| System.Text.Json | (net10 built-in) | Packet zip JSON round-trip | Existing; back-compat hinges on its forgiving defaults |
| xUnit | 2.9.3 | Test framework (edits/deletions required) | Project standard |

**Installation:** None. `dotnet build` only. (No `npm install` needed — removing TS files, not adding.)

## Package Legitimacy Audit

Not applicable — Phase 37 installs **zero** external packages (removal-only; CLAUDE.md forbids new deps). slopcheck not run because no packages are added.

## Removal-Graph Verification (RET-02 — highest risk)

Every reference below was grep-verified across `*.cs`/`*.cshtml`/`*.ts` (excluding `.planning/` and build output). **"Confirmed"** = CONTEXT's "no other consumers" claim holds. **⚠ DISCREPANCY** = planner must handle.

### `ContentKbRelevanceService` / `IContentKbRelevanceService` / `ExpertSelection`
**Production consumers:**
- `Program.cs:185-190` — DI registration (`AddSingleton<IContentKbRelevanceService>`); `Program.cs:331` — injected into `DeckAnalysisPacketService` ctor.
- `DeckAnalysisPacketService.cs:79,144` — `_contentKbRelevanceService` field + ctor param; `:701` (`GetMergedClipsAsync`), `:703` (`new ExpertSelection(...)`), `:743` (`ResolvePinTitlesAsync`).
- `AdminContentKbController.cs:26,39,45,50` — `_relevanceService` field/param/guard; `:84` (`ScoreAllAsync`). ⚠ **This is a real production consumer outside `DeckAnalysisPacketService`** — CONTEXT lists it under PARTIAL edits (correct), but the planner must remove `_relevanceService` from the ctor *and* the `IContentKbRelevanceService` interface goes away entirely, so the admin ctor signature changes.
- **Interface surface:** `GetRelevantClipsAsync` (`:27`), `GetMergedClipsAsync` (`:45`), `ResolvePinTitlesAsync` (`:56`), `ScoreAllAsync` (`:67`). All four leave with the interface. `GetRelevantClipsAsync` appears to be a legacy method — verify it has no live caller (grep shows only test doubles implement it).

**Verdict: CONFIRMED no production consumer survives** once `DeckAnalysisPacketService` and `AdminContentKbController` are edited. Both edits are already in the PARTIAL list.

### `ContentKbArchetypeDeriver`
- `Program.cs:182-184` — DI registration; `:189` — passed to `ContentKbRelevanceService`.
- `ContentKbRelevanceService.cs:187,203,220` — field + both ctors.
- ⚠ **DISCREPANCY (dependency, not consumer):** `ContentKbArchetypeDeriver` *depends on* `ICategoryKnowledgeStore` (`Program.cs:183`, ctor `ContentKbArchetypeDeriver.cs:71`). `ICategoryKnowledgeStore` is **KEPT** (commander-category feature). Removing the deriver must delete only the deriver + its DI line; it must NOT touch `ICategoryKnowledgeStore` or its registration. **Verdict: CONFIRMED safe to delete** — nothing else consumes the deriver; the store it reads is independent.

### `ContentKbClipSanitizer`
- Consumed at `ChatGptAnalysisPromptVariant.cs:264-265`, `GeminiAnalysisPromptVariant.cs:271-272`, `ClaudeAnalysisPromptVariant.cs:256-257` (the `Sanitize(...)` calls inside the Expert Context block).
- **Verdict: CONFIRMED** — its only callers are the three Expert-Context blocks being stripped. Once those blocks go, the sanitizer is dead and deletable.

### `ContentKbExcerpt`
- `DeckAnalysisViewModel.cs:71` (`ExpertContextClips`), `ContentKbExcerpt.cs:6` (def), `DeckAnalysisPacketService.cs:57,543,555,1161` (record field + replay deserialize + `BuildAnalysisPrompt` param), `ContentKbRelevanceService.cs` (many — leaves with the service), all 3 prompt variants (`kbExcerpts` param), `_ContentKbPanel.cshtml:1` (`@model`), `AnalysisPromptVariantRegistry.cs:41`, `IAnalysisPromptVariant.cs:37`.
- ⚠ **DISCREPANCY:** CONTEXT says `ContentKbExcerpt` is "consumed only by the injection path + DeckAnalysisViewModel." Grep confirms that, **but also surfaces additional consumers the planner must edit/delete:** `AdminContentKbControllerTests.cs:380-398` (a test double implementing the interface), `AiPlatformExtensionTests.cs:42` (a stub variant's param), `AnalysisPromptVariantExpertContextTests.cs` (whole file), `DeckControllerTests.cs:717,741` (two test methods), `DeckAnalysisPacketServiceExpertContextTests.cs` (whole file), `PacketArtifactStoreTests.cs:82,124` (round-trip asserts), `ContentKbExcerptTests.cs` (whole file), `Spike001KbValueAbHarness.cs` (whole file). **Verdict: CONFIRMED no non-test production consumer beyond the injection path** — but the test blast radius is larger than CONTEXT's named list (see Test Impact).

### `ContentKbSearchApiController`
- Route `api/content-kb` with `[HttpGet("entries")]` (`:33`) and `[HttpGet("creators")]` (`:69`).
- **Only client consumer:** `kb-selection.ts:488-489` (`fetch('/api/content-kb/entries...')`, `.../creators...`). The `/content-kb` browse search is **client-side filtering** (`content-kb.ts:24` `[data-kb-search]` + `kb-entry-filter.ts`), NOT this API.
- **Verdict: CONFIRMED** — the typeahead API has exactly one consumer (the widget being removed). ⚠ Note (memory 4099): this controller is **not** `[FeatureFlagGate]`-guarded; it has been a live unauthenticated endpoint even while the KB shipped dark. Removing it *closes* that surface — a security positive worth noting in the plan.

### `_ContentKbPanel.cshtml`
- Rendered once: `DeckAnalysis.cshtml:605` (`@await Html.PartialAsync("_ContentKbPanel", Model.ExpertContextClips)`).
- **Verdict: CONFIRMED** — single render site; delete partial + that one line.

### `kb-selection.ts`
- Script tag in **two** views: `ContentKb/Index.cshtml:145` AND `DeckAnalysis.cshtml:981`. ⚠ **DISCREPANCY:** CONTEXT only flags the deck-analysis side. The KB browse Index *also* loads `kb-selection.js` and renders a `kb-selection-tray` (`ContentKb/Index.cshtml:62-71`) + per-entry pin/follow buttons (`Index.cshtml:104-118`) + a "Run analysis with this selection →" CTA to `/deck-analysis`. **All of this is expert-selection UI on the KEPT browse page and must also be removed** for RET-01/RET-02 to hold — otherwise the browse page keeps dead pin/follow buttons that POST to a deleted API. The planner MUST add `ContentKb/Index.cshtml` to the PARTIAL-edit list (strip tray + pin/follow buttons + CTA + script tag), even though CONTEXT lists `Views/ContentKb/*` under KEEP.

## Partial-Edit Blast Radius

### `Program.cs`
- Delete `:182-190` (deriver + relevance DI). At `:331` remove the `IContentKbRelevanceService` ctor arg to `DeckAnalysisPacketService` (signature shrinks by one param).

### `DeckAnalysisPacketService.cs` — signature changes
- Record `DeckAnalysisPacketResult` (`:57-58`): drop `ExpertContextClips` and `ResolvedPinTitles` params.
- Ctor (`:144`): drop `IContentKbRelevanceService? contentKbRelevanceService`; field `:79` removed.
- Body: remove replay block `:548-555` + `:699-711` (fresh `GetMergedClipsAsync`), the `ExpertSelectionJson` serialize `:730-738`, the `ResolvePinTitlesAsync` `:741-743`, and the result-construction `:770-771`. `kbExcerpts` local becomes unused → delete it.
- `BuildAnalysisPrompt` (`:1161`): drop the trailing `IReadOnlyList<ContentKbExcerpt>? kbExcerpts = null` param; update the single call at `:717`.
- ⚠ The replay path reads **`request.ExpertContextJson`** (`:548-555`) — a field CONTEXT never names (see Back-Compat).

### 3 prompt variants + interface + registry
- `IAnalysisPromptVariant.cs:26,37` — remove `kbExcerpts` param + its doc line.
- `AnalysisPromptVariantRegistry.cs:41,47` — remove param + the pass-through arg.
- `ChatGptAnalysisPromptVariant.cs` — remove param `:34` + Expert-Context block `:251-265`.
- `ClaudeAnalysisPromptVariant.cs` — remove param `:35` + block `:243-257`.
- `GeminiAnalysisPromptVariant.cs` — remove param `:36` + block `:254-279` **+ the private helper `EstimateExpertContextLength` `:283-...`** (dangling after block removal; the Gemini variant has extra length-budget logic the other two lack).

### `DeckAnalysisRequest.cs`
- Remove `PinnedVideoIds` (`:138`), `FollowedCreators` (`:147`), `ExpertSelectionJson` (`:156`), and their backing fields. ⚠ **ALSO `ExpertContextJson` (`:129`)** — CONTEXT lists only the three, but `ExpertContextJson` is part of the same injection round-trip and is read at `DeckAnalysisPacketService.cs:548` and `DeckController.cs:793/822`. Decide: remove it too (cleanest) — see Back-Compat for the load-path consequence.

### `DeckAnalysisViewModel.cs`
- Remove `ExpertContextClips` (`:71`) and `ResolvedPinTitles` (`:76`).

### `PacketArtifactStore.cs` + `PacketSessionCache.cs`
- `PacketArtifactStore.cs`: remove `ExpertSelectionJsonOptions` (`:31-35`), `ExpertSelectionState` record (`:911-...`), and the load-restore block `:319-337`. ⚠ The `BuildZip` params `expertContextJson`/`selectionJson` (`:128-129`) and the entry tuples `:141-142` write `32-expert-context.json` / `33-expert-selection.json`. Decide whether to drop these params (cleaner) or keep them writing-null (lower blast radius into `DeckController`). See Back-Compat for the *read* side (`:287-299`).
- `PacketSessionCache.cs:187-188`: remove `NormalizedPinnedVideoIds` / `NormalizedFollowedCreators` from the cache-key record; update the populate sites in `DeckAnalysisPacketService.cs:295,301`.

### `DeckController.cs`
- `:503-504` (map `ExpertContextClips` + `ResolvedPinTitles` into the viewmodel), `:793-800` and `:822-829` (the `expertContextJson`/`selectionJson` BuildZip args), `:904` (second `ExpertContextClips` map). Remove all. After removing the request fields these references won't compile, so they are self-documenting compile errors if missed.

### `DeckAnalysis.cshtml`
- Remove the accordion **`:182-232`** (CONTEXT said ≈182-206 — **actual end is 232**, `</details>`), the `_ContentKbPanel` render `:605`, and the `kb-selection.js` script tag `:981`.

### `AdminContentKbController.cs`
- Remove `_relevanceService` field `:26`, ctor param `:39` + guard `:45` + assign `:50`, the `previewCommander`/`previewBracket`/`sortBy` action params + `previewActive`/`previewScores` logic `:78-89`, the `RelevanceScore` projection `:100`, and the score-sort block `:110-116`. KEEP: `GetAllRowsAsync`, visibility filter, sources, status, `SetEvergreen`, `SetVisibility`, `ReloadSeed`.

### `AdminContentKb/Index.cshtml` + `content-kb-admin.ts` + `AdminContentKbViewModel`
- ⚠ **DISCREPANCY: `content-kb-admin.ts` is 227 lines and is *entirely* the preview-commander typeahead** (`wireCommanderPreviewTypeahead`, `setCommanderSearchError`, all of it). CONTEXT says "edit" — but there is nothing to keep. **Delete the whole file** and remove its `<script>` tag from `AdminContentKb/Index.cshtml`. The Phase-33 instant filter is `kb-entry-filter.ts` (separate file, KEEP).
- `Index.cshtml`: remove the preview form (PreviewCommander/Bracket inputs) and the score column/sort. KEEP the grid, publish/unpublish, evergreen toggle (`:233-243`), per-source bulk, reload-seed, and the `data-kb-search` filter row (`:196`).
- `AdminContentKbViewModel` / `KbEntryRow`: remove `RelevanceScore` (and any `PreviewCommander`/`PreviewBracket`/`SortBy` fields). KEEP `IsEvergreen`, `IsVisible`, etc.

## Back-Compat (RET-05)

**How packets serialize/deserialize today:** `PacketArtifactStore.BuildZip` writes named text entries into a zip; `LoadFromZip`-style reader (`ReadEntries(zipStream, PacketAllowedNames)`, `:281`) reads **only names in the `PacketAllowedNames` allow-list** (`:37-53`), which includes BOTH `32-expert-context.json` and `33-expert-selection.json`. Unknown entry *names* are silently dropped by the allow-list filter — they never reach a deserializer.

**Two persisted fields, not one (⚠ key discrepancy):**
- `32-expert-context.json` → `request.ExpertContextJson` (`:298`) — serialized `List<ContentKbExcerpt>` (the clips).
- `33-expert-selection.json` → `request.ExpertSelectionJson` (`:299`) → deserialized to `ExpertSelectionState` via `JsonSerializer.Deserialize<ExpertSelectionState>(selectionJson, ExpertSelectionJsonOptions)` inside a **`try { ... } catch (JsonException) { }`** (`:319-337`).

**Current JSON options:** `ExpertSelectionJsonOptions` = `{ CamelCase, PropertyNameCaseInsensitive = true }` (`:31-35`). There is **NO `UnmappedMemberHandling.Disallow`** and **NO `[JsonRequired]`** on `ExpertSelectionState` or `ContentKbExcerpt` — verified by reading both. System.Text.Json's default is `UnmappedMemberHandling.Skip`, so unknown members are ignored, not thrown.

**What must change so a pre-retire packet loads without throwing after the fields are removed:**
1. The zip *contains* `32-`/`33-` entries, but after removal the loader must simply **not read them into removed properties.** Remove lines `:287-288` (the `TryGetValue` for both), `:298-299` (the property assigns), and the `:319-337` selection-restore block. Because `ReadEntries` filters by allow-list and never deserializes the *whole* request from the zip, **dropping the read sites is sufficient** — the extra zip entries are simply ignored.
2. **Decision (Discretion):** also drop `32-expert-context.json` / `33-expert-selection.json` from `PacketAllowedNames`? Either works. Leaving them in the allow-list is harmless (they're read into nothing). Removing them is cleaner. **Recommendation: remove both from `PacketAllowedNames`** so the allow-list reflects the real artifact set — but verify no test asserts their presence (`PacketArtifactStoreTests.cs:161,186` assert `ExpertContextJson == ""`/`"[]"` and will need updating/removal).
3. **The real RET-05 risk is NOT a throw on load — it's a *compile* break:** after removing `request.ExpertContextJson` / `ExpertSelectionJson`, any line still referencing them won't build. The forgiving runtime behavior already satisfies "loads without throwing"; the task is to remove all read/write sites so nothing dangles. **No `JsonSerializerOptions` change is required for safety** — current defaults already ignore unknown members.

**Existing regression evidence:** `DeckAnalysisPacketServiceExpertContextTests.cs:68` (`BuildAsync_CorruptReplayExpertContextJson_DegradesToNullWithoutThrowing`) proves the replay path already swallows bad JSON. That test is deleted with the feature, but its existence confirms the graceful-degrade design.

## Un-Dark + XSS (RET-04)

**Flag location & default:**
- Read at `ContentKbController.cs:51,92` (`[FeatureFlagGate("content.kb.enabled", ...)]`), `Home.cshtml:64`, `_DeckToolTabs.cshtml:41`, `AdminContentKbController.cs:133` (status display only).
- ALSO read at `ContentKbRelevanceService.cs:245,278` — **these leave with the removed service**, so post-retire the flag gates ONLY the browse path. ✔ **Confirms CONTEXT.**
- **Default = FALSE**, seeded at `FeatureFlagStore.cs:180` (Postgres `('content.kb.enabled', FALSE)`) and `:190` (SQLite `('content.kb.enabled', 0)`). Un-dark = flip these defaults to TRUE/1 (and/or flip live via `/Admin/Flags`). Note prior decision in MEMORY: the flag was deliberately shipped OFF; flipping the *seed default* changes behavior for fresh DBs while existing prod rows are flipped via admin toggle. Plan should do both (seed default ON + verify/flip live).

**XSS audit of KEPT views — RESULT: SAFE, with one nuance.**
- `Views/ContentKb/Index.cshtml`, `Views/ContentKb/Detail.cshtml`, `Views/AdminContentKb/Index.cshtml`: **grep found ZERO `Html.Raw` / `@Html.Raw` / `MarkupString` / `IHtmlContent` / `WriteLiteral`.** All harvested fields (`@entry.Title`, `@entry.Source`, `@Model.Title`, tags) render through default Razor `@`, which HTML-encodes. ✔
- **The one raw sink:** `Detail.cshtml:44` renders `@Model.RenderedHtml`, where `RenderedHtml` is `HtmlString` (`ContentKbDetailViewModel.cs:29`, `required HtmlString`). It is produced at `ContentKbController.cs:123` via `Markdown.ToHtml(body, Pipeline)` where the pipeline (`:19-20`) is `new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build()`. **`.DisableHtml()` strips raw HTML from the Markdown source**, so a `<script>` embedded in a harvested transcript is rendered as literal text, not executed. This is the XSS mitigation and it is already in place. ✔
- The `<textarea ... >@Model.CleanBodyText</textarea>` (`:43`) renders inside a textarea via `@` (encoded) — safe; `</textarea>` injection is prevented by Razor encoding of `<`/`>`.

**RET-04 verification task (not a code change):** confirm the pipeline retains `.DisableHtml()` and that no edit introduces `Html.Raw`. The un-dark itself is a config flip; the XSS posture is already correct — the plan's job is to *not regress* it and to assert it (e.g., a unit test that `Markdown.ToHtml("<script>alert(1)</script>", Pipeline)` does not contain `<script>`).

## RET-06 Copyable-Prompt Affordance

**It already exists — on the KB Detail (entry) page, not the list page.**
- `Views/ContentKb/Detail.cshtml:35-43`: a `<button class="copy-button run-button" data-copy-target="kb-artifact-text">Copy</button>` inside a `chatgpt-sticky-download` region, copying the `<textarea id="kb-artifact-text">@Model.CleanBodyText</textarea>` (the distilled artifact body — the paste-into-ChatGPT prompt).
- Wired by `content-kb.ts:84-114` (`attachCopyButtons` → `navigator.clipboard.writeText(target.value)`), with copied/failed states.
- The **list** page (`ContentKb/Index.cshtml`) has search/filter but no copy button — copy is per-entry on Detail.

**Therefore RET-06 = "surface/link existing," not "build new."** The deck-analysis page should carry a short note + link to `/content-kb` explaining users can browse distilled creator advice and copy a prompt from any entry's Detail page. Add the note where the removed accordion was (around `DeckAnalysis.cshtml:182`), or in the existing "What you do" instructions block (`:234-245`).

⚠ **Planner-must-handle nav-copy fix (not in CONTEXT):** the KB nav links advertise the *removed* feature. `Home.cshtml:67-69` reads *"...then pin videos or follow creators to inject their advice straight into your deck analysis prompt."* and `_DeckToolTabs.cshtml:43` labels the link "Knowledge Base." The Home description is now false and must be rewritten to reference browse + copyable prompts only. This is part of RET-01's spirit (no injection surface) and RET-06 (accurate pointer).

## is_evergreen Column (Discretion)

**Findings:**
- **Schema/persistence (KEEP):** `Core/Content/ContentSiteIndexStore.cs` owns the column — migration add (`:66-72`), upsert (`:113,148,541-596`), CREATE TABLE (`:624,644`), read (`:498`), `SetEvergreenAsync` (`:316-327`). Interface `IContentSiteIndexStore.cs:80`. None of this depends on removed types.
- **Admin curation (KEEP):** `AdminContentKbController.cs:222-232` `SetEvergreen` action + `AdminContentKb/Index.cshtml:233-243` toggle buttons. This is curation UI, not selection code.
- **Selection usage (REMOVE):** ONLY `ContentKbRelevanceService.cs:332-336` reads `IsEvergreen` to fill the "evergreen" clip tier — leaves with the service. Also `ContentArtifactSpec.cs:138-139` (`IsEvergreen` on the spec) and `_ContentKbPanel.cshtml:36,45,53` (the "evergreen" origin label) — the panel is deleted; `ContentArtifactSpec.IsEvergreen` is a distill-time field, verify it's still set by the harvest/distill path (it is — `RunDistillAsyncTests.cs:688-698` and `FakeContentSiteIndexStore.cs:75-82` exercise it).

**Verdict: CONTEXT recommendation is SAFE.** Leave the `is_evergreen` column and the admin evergreen toggle (curation). Remove only the selection-tier read in `ContentKbRelevanceService` (which goes away wholesale). The admin evergreen toggle becomes "curation metadata with no downstream consumer" — harmless dead-but-visible; **discretion:** the plan may keep the toggle (zero risk, preserves admin workflow) or hide it. **Recommend keeping** — no destructive migration, no behavior change to harvest/browse.

## Test Impact

Grep-verified test files referencing removed types (source files only; `bin/`/`obj/` XML ignored):

| Test file | Disposition | Reason |
|-----------|-------------|--------|
| `ContentKbRelevanceServiceTests.cs` | **DELETE** | Tests the removed service |
| `ContentKbClipSanitizerTests.cs` | **DELETE** | Tests the removed sanitizer |
| `ContentKbMergedClipsTests.cs` | **DELETE** | Tests `GetMergedClipsAsync` on removed service |
| `ContentKbArchetypeDeriverTests.cs` | **DELETE** | Tests the removed deriver (CONTEXT says "any ArchetypeDeriver tests" — this is it) |
| `ContentKbExcerptTests.cs` | **DELETE** | JSON round-trip of removed record |
| `Spike001KbValueAbHarness.cs` | **DELETE** | Gate harness; references removed retriever (CONTEXT-named) |
| `AnalysisPromptVariantExpertContextTests.cs` | **DELETE** | Whole file tests the Expert-Context prompt block |
| `DeckAnalysisPacketServiceExpertContextTests.cs` | **DELETE** | Whole file tests injection/replay path + fake relevance service |
| `AdminContentKbControllerTests.cs` | **EDIT** | Remove `FakeContentKbRelevanceService` double (`:369-398`) + score-preview test cases (`:127,156,172,195,254`); KEEP grid/visibility/seed tests. Ctor calls (`:331`) drop the relevance arg. |
| `DeckControllerTests.cs` | **EDIT** | Delete the two methods `DeckAnalysis_MapsExpertContextClips_...` (`:715`) and `..._LeavesExpertContextClipsNull_...` (`:780`) + the `clips` fixture (`:717`); update any `DeckAnalysisPacketResult` constructions dropping `ExpertContextClips`/`ResolvedPinTitles`. |
| `DeckAnalysisPacketServiceTests.cs` | **EDIT** | Remove `PinnedVideoIds`/`FollowedCreators` assignments (`:1374-1430`) — these test pin/follow normalization that no longer exists. |
| `DeckAnalysisRequestTests.cs` | **EDIT** | Remove `PinnedVideoIds`/`FollowedCreators` null-coalesce asserts (`:13-18`). |
| `PacketArtifactStoreTests.cs` | **EDIT** | Remove expert-context/selection round-trip asserts (`:82,124,161,186,193-220,257-296`). KEEP deck-profile/set-upgrade round-trip. |
| `AiPlatformExtensionTests.cs` | **EDIT** | The stub variant's `Build(...)` signature (`:42`) drops the `kbExcerpts` param to match `IAnalysisPromptVariant`. |

**Other incidentally-touched tests:** `ContentSiteIndexStoreTests.cs` and `RunDistillAsyncTests.cs` reference `IsEvergreen` but via the KEPT store/spec — **no change needed** (they test the retained column, not selection). `FakeContentSiteIndexStore.cs:75-82` keeps `SetEvergreenAsync` (KEEP). `FakeFeatureFlagCache` usages with `content.kb.enabled` in non-deleted tests are fine.

## Common Pitfalls

### Pitfall 1: Forgetting the browse-page selection UI (`ContentKb/Index.cshtml`)
**What goes wrong:** The plan strips the deck-analysis accordion but leaves the pin/follow tray + buttons + `kb-selection.js` on the KEPT `/content-kb` browse page, which then POST to a deleted API → JS errors + dead UI.
**Why it happens:** CONTEXT lists `Views/ContentKb/*` under KEEP, so it's easy to assume the whole file is untouched.
**How to avoid:** Add `ContentKb/Index.cshtml` to the PARTIAL list: remove `:62-71` (tray), pin/follow buttons (`:104-118`), the CTA (`:71`), and the `kb-selection.js` script tag (`:145`). Keep search/filter/listing.
**Warning signs:** `kb-selection.ts` still referenced after deletion; `data-video-id`/`data-creator` attributes remain in markup.

### Pitfall 2: Two JSON fields, only one named
**What goes wrong:** Removing `ExpertSelectionJson` but leaving `ExpertContextJson` (or vice-versa) → half-removed state, dangling reads at `DeckController.cs:793/822` and `DeckAnalysisPacketService.cs:548`.
**How to avoid:** Treat `ExpertContextJson` + `ExpertSelectionJson` (+ `PinnedVideoIds` + `FollowedCreators`) as one atomic removal across `DeckAnalysisRequest`, `PacketArtifactStore`, `DeckController`, `DeckAnalysisPacketService`.

### Pitfall 3: Gemini variant's extra helper
**What goes wrong:** Removing the Expert-Context block from `GeminiAnalysisPromptVariant` but leaving `EstimateExpertContextLength` (`:283`) → unused-private-method (CS0169-class) warning, breaking RET-02's 0-warning gate.
**How to avoid:** Delete the helper too. The other two variants don't have it.

### Pitfall 4: Formatter reflow on member removal
**What goes wrong:** An IDE/format-on-save re-indents the surrounding method after a member is deleted → huge diff, violates CLAUDE.md.
**How to avoid:** Codex dispatch must edit only the target lines; no "Format Document." Verify the diff touches only intended regions before commit.

### Pitfall 5: Assuming the build will catch everything
**What goes wrong:** Razor view references (`@Model.ExpertContextClips`, `_ContentKbPanel`) and TS are not always caught by `dotnet build`; Razor compiles at runtime/publish and TS compiles via the MSBuild `tsc` target.
**How to avoid:** After C# edits, also grep the views/TS for removed symbols and run the full `dotnet build` (which triggers the `tsc` target) — confirm 0 warnings AND no `tsc` errors.

## Runtime State Inventory

This is a code-removal phase with one config flip; little runtime state, but verified explicitly:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | `is_evergreen` column in `content_site_index` (SQLite + Postgres) — KEPT. Old packet zips on the `/data` disk carry `32-expert-context.json`/`33-expert-selection.json`. | None (column kept). Old zips must still load — covered by RET-05 (forgiving allow-list read). No data migration. |
| Live service config | `content.kb.enabled` feature flag row in the flags table (currently FALSE in prod, set via `/Admin/Flags`, NOT in git seed alone). | Flip to TRUE live in prod via admin toggle **and** flip the seed default (`FeatureFlagStore.cs:180/190`) for fresh DBs. |
| OS-registered state | None — no scheduler/pm2/systemd entries reference these types (verified: no matches outside source). | None. |
| Secrets/env vars | None — no env var names reference expert-selection/clip-injection. | None. |
| Build artifacts | Compiled `wwwroot/js/kb-selection.js` and `content-kb-admin.js` are **gitignored** and regenerated by the MSBuild `tsc` target. Deleting the `.ts` sources stops emission. | None to commit; a stale `.js` may linger in a dev `wwwroot/js/` working dir — harmless (gitignored), cleared on next clean build. |

**Nothing found** in OS-registered state and secrets — verified by grep returning zero matches outside `.cs`/`.cshtml`/`.ts` source.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK (`dotnet.exe`) | Build / RET-02 0-warning gate | ✓ | net10.0 | — |
| Node 20 + `tsc` (MSBuild target) | TS removal compiles clean | ✓ (build-time, via MSBuild target) | TS 6.0.2 | — |
| SQLite / Postgres | KB persistence (KEEP) — unaffected | ✓ | Sqlite 10 / Npgsql 10 | — |

**Build baseline captured this session:** `dotnet build DeckFlow.sln -c Debug` → **Build succeeded, 0 Warning(s), 0 Error(s).** RET-02 requires the post-removal build to remain at 0/0.

**Missing dependencies:** none blocking.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (+ xunit.runner.visualstudio 3.1.4, MockHttp 7.0.0) |
| Config file | none (csproj-driven; `DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`) |
| Quick run command | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug` (VSTest unreliable in WSL — build-clean is the primary gate per CLAUDE.md) |
| Full suite command | CI (`push-and-watch`) runs the xUnit suites; locally `dotnet test` may be flaky in WSL |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| RET-01 | No `## Expert Context` in any of 3 prompt variants | unit | assert `Build(...)` output lacks `## Expert Context` (replaces deleted `AnalysisPromptVariantExpertContextTests`) | ❌ Wave 0 (new assertion) |
| RET-02 | Build 0 warn / 0 err, no dead refs | build | `dotnet build DeckFlow.sln -c Debug` → 0/0 | ✅ baseline captured |
| RET-03 | Browse + harvest/distill intact | smoke | manual: run CLI harvest/distill; load `/content-kb` (flag ON) renders entries | ✅ existing `ContentKbControllerTests` cover render |
| RET-04 | Markdig pipeline strips raw HTML | unit | assert `Markdown.ToHtml("<script>...", Pipeline)` contains no `<script>` | ❌ Wave 0 (new XSS-regression test) |
| RET-05 | Pre-retire zip loads without throwing | unit | new test: load a zip containing `32-`/`33-` entries → no exception, no missing-property | ❌ Wave 0 (replaces deleted round-trip asserts) |
| RET-06 | Deck-analysis note links `/content-kb` | manual/view | visual: note + link present where accordion was | ❌ manual |

### Sampling Rate
- **Per task commit:** `dotnet build DeckFlow.sln -c Debug` (must stay 0/0).
- **Per wave merge:** full build + CI xUnit run (push-and-watch).
- **Phase gate:** build 0/0 + CI green before `/gsd:verify-work`; manual smoke of `/content-kb` browse + a fresh analysis packet (no Expert Context block) + loading one pre-retire packet zip.

### Wave 0 Gaps
- [ ] New RET-01 assertion: prompt-variant output omits `## Expert Context` (3 platforms) — add to a surviving variant test or new small test.
- [ ] New RET-04 XSS-regression test on the Markdig pipeline (`.DisableHtml()` behavior).
- [ ] New RET-05 back-compat test: a fixture zip with legacy `32-`/`33-` entries loads cleanly.
- [ ] Delete 8 test files + edit 6 (see Test Impact) — net test count drops; ensure no orphaned `using`/helper after deletions.

## Security Domain

`security_enforcement` is not disabled in config — included.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Admin already behind BasicAuth (unchanged) |
| V3 Session Management | no | — |
| V4 Access Control | yes | Removing `ContentKbSearchApiController` closes a non-flag-gated public API (memory 4099) — net reduction in attack surface |
| V5 Input Validation / Output Encoding | **yes** | RET-04: Razor `@` auto-encoding + Markdig `.DisableHtml()` on harvested (untrusted) clip/summary/title text |
| V6 Cryptography | no | — |

### Known Threat Patterns for {.NET 10 Razor + Markdig render of harvested text}
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Stored XSS via harvested transcript rendered raw | Tampering / Elevation | Markdig pipeline `.DisableHtml()` (in place, `ContentKbController.cs:20`) + default Razor `@` encoding; verify no `Html.Raw` introduced |
| `</textarea>` breakout in copyable-prompt block | Tampering | `@Model.CleanBodyText` is `@`-encoded inside the textarea — safe |
| Prompt-injection via clip text into LLM | Tampering | **Eliminated by this phase** — the injection path (the only route untrusted clips reached an LLM prompt) is removed; sanitizer + structural fence become unnecessary |
| Dangling unauthenticated typeahead API | Info disclosure | Removing `ContentKbSearchApiController` closes `/api/content-kb/entries|creators` |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | System.Text.Json default `UnmappedMemberHandling.Skip` means removed-field zips won't throw on load | Back-Compat (RET-05) | If a future option set `Disallow`, an old zip could throw — verified NOT set today; low risk |
| A2 | `GetRelevantClipsAsync` (interface method `:27`) has no live production caller (only test doubles) | Removal Graph | If a caller exists off the grepped extensions, build catches it — self-correcting |
| A3 | Flipping `FeatureFlagStore` seed default to ON is the right un-dark mechanism for fresh DBs; prod row flipped via `/Admin/Flags` | Un-Dark (RET-04) | Prod already has a flags row (FALSE); seed default only affects fresh DBs — plan must do the live toggle too |
| A4 | Keeping the admin evergreen toggle (now consumer-less) is acceptable | is_evergreen | If a reviewer wants dead-UI removed, it's a trivial follow-up; zero functional risk |

## Open Questions

1. **Drop or keep the `BuildZip` `expertContextJson`/`selectionJson` params and `PacketAllowedNames` entries?**
   - What we know: removing the *read* sites is sufficient for RET-05; the write params can stay (writing null) or be dropped.
   - What's unclear: blast radius preference — dropping the params changes `DeckController` call sites (2) and `BuildZip` signature; keeping them minimizes diff but leaves vestigial params.
   - Recommendation: **drop them** for a clean post-retire surface (Phase 38 SRP split benefits from a slimmer `BuildZip`); update the 2 `DeckController` call sites and the `PacketArtifactStoreTests` round-trip asserts accordingly.

2. **Keep the admin evergreen toggle?**
   - What we know: only `ContentKbRelevanceService` (removed) consumed `IsEvergreen` for selection; the toggle persists curation metadata.
   - Recommendation: **keep** — no destructive migration, preserves admin workflow; revisit in Phase 37.5 (corpus rebuild) if curation semantics change.

## Sources

### Primary (HIGH confidence — verified this session)
- Live codebase grep across `*.cs`/`*.cshtml`/`*.ts` (every file:line above) — DeckFlow `v1.6` branch, 2026-06-10.
- `dotnet build DeckFlow.sln -c Debug` → 0 warn / 0 err (build baseline).
- `.planning/phases/37-retire-clip-injection/37-CONTEXT.md`; `.planning/REQUIREMENTS.md`; `.planning/ROADMAP.md`; `.planning/phases/35-value-re-validation-gate/35-GATE-VERDICT.md`; `./CLAUDE.md`; `.planning/config.json`.

### Secondary (MEDIUM)
- MEMORY.md observations (Phase 32/33/35 history; memory 4099 — search API not flag-gated; 4392 — retirement scope).

### Tertiary (LOW)
- None — all claims are tool-verified against the live tree.

## Metadata

**Confidence breakdown:**
- Removal graph (RET-02): HIGH — every consumer grepped at file:line; build baseline captured.
- Back-compat (RET-05): HIGH — read both `JsonSerializerOptions` and both target records; confirmed no `Disallow`/`[JsonRequired]`.
- XSS (RET-04): HIGH — zero `Html.Raw` in kept views; confirmed Markdig `.DisableHtml()`.
- Copy affordance (RET-06): HIGH — located the existing button + TS wiring on Detail page.
- Discrepancies vs CONTEXT: HIGH — five flagged with file:line evidence.

**Research date:** 2026-06-10
**Valid until:** ~2026-06-24 (stable; brownfield removal — only invalidated if the v1.6 branch changes the named files before planning).
