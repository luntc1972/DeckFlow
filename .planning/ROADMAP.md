# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- 🟢 **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 (started 2026-05-13 on `v1.3` branch)

## Phases

<details>
<summary>✅ v1.0 Polish & Quality (Phases 1-5) — SHIPPED 2026-05-02</summary>

- [x] Phase 1: Visual System Tokens — 3/3 plans (UI-VS-01..04)
- [x] Phase 2: Layout, Hierarchy & UX Copy — 3/3 plans (UI-LH-01..02, UX-01..03)
- [x] Phase 3: Tech-Debt Cleanup — 4/4 plans (TD-01..04)
- [~] Phase 4: Security & Bug Fixes — 4/4 plans, ABANDONED 2026-05-02 (rerouted to Phase 5)
- [x] Phase 5: Security & Bug Fixes v2 — 3/3 plans (BUG-01, BUG-02, TD-04 patch + integration test)

Verification: 27/27 must-haves passed. 15/15 v1 requirements shipped.
Full archive: `.planning/milestones/v1.0-ROADMAP.md`

</details>

<details>
<summary>✅ v1.1 Admin Console (Phases 6-8) — SHIPPED 2026-05-08</summary>

- [x] Phase 6: Admin Shell + Flags Foundation — 7/7 plans (ADMIN-01..05, FLAG-01..05)
- [x] Phase 7: Harvest Controls + Stats — 7/7 plans (HARV-01..07)
- [x] Phase 7.1: Categories Flag + SameOrigin AJAX Fix — 2/2 plans (inserted hotfix)
- [x] Phase 8: Analytics — 5/5 plans (ANL-01..05)

</details>

<details>
<summary>✅ v1.2 Multi-AI Prompts (Phases 9-10) — SHIPPED 2026-05-13</summary>

- [x] Phase 9: Bracket UX + AI Selector Foundation — 3/3 plans (BRKT-01, AISEL-01, AISEL-04 Packets portion)
- [x] Phase 10: Claude + Gemini Artifact Optimization — 5/5 plans (AISEL-02, AISEL-03, AISEL-04 Comparison + CedhMetaGap)

Full archive: `.planning/milestones/v1.2-ROADMAP.md`
Audit: `.planning/milestones/v1.2-MILESTONE-AUDIT.md` — documentation-only gaps, all 5 v1.2 reqs functionally satisfied via manual T1-T8 + filename verify.

</details>

### 🟢 v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene (Phases 11-15)

- [x] **Phase 11: Web Design Guidelines Audit Fixes** — Land all 10 sweep PRs from `260513-wdg-FINDINGS.md`: cross-cutting `site-common.css` a11y rules, admin focus-visible foundation, df-typeahead keyboard nav + ARIA combobox, ARIA tablist server-render, CSP inline-handler removal, info-tooltip a11y, table semantics, URL/textarea autocomplete, Razor `selected=` bool sweep, and AdminHarvest live-region announcement. (completed 2026-05-13)
- [x] **Phase 12: AI-Agnostic URL + Page Rename** — Drop "chatgpt-" from the three multi-AI workflow URLs, swap H1/nav/hub labels, add `.page-lede` explainer lines, ship 301 permanent redirects, update artifact filenames to AI-agnostic terms. (completed 2026-05-17)
- [x] **Phase 13: ChatGpt* Class Rename + Summary Doc Comments** — Rename all `ChatGpt*` request/service/viewmodel/parser/store types to AI-agnostic names; backfill XML `<summary>` doc comments on every renamed class; update DI registrations, `InternalsVisibleTo`, namespaces, controller actions, test fixtures, and Razor `@model` directives with zero behavior change. (completed 2026-05-17)
- [x] **Phase 14: Broader Codebase Name-vs-Behavior Audit** — Sweep public classes across all 5 projects, rename any whose name doesn't describe current behavior, backfill missing XML `<summary>` doc comments, verify clean Release build with zero new warnings. (completed 2026-05-18)
- [x] **Phase 15: AiPlatform Value Object Refactor** — Replace `string TargetAiPlatform` with sealed `AiPlatform` record value object across request DTOs, prompt builders, response extractor, artifact store, and view models; preserve `DECKFLOW_GEMINI_ENABLED` gating; zero user-visible behavior change verified via full T1-T8 manual integration suite. (completed 2026-05-18)
- [x] **Phase 999.1: AI-Agnostic Prose Adaptation in Razor Views** — Strip the hardcoded `"ChatGPT"` brand from every user-visible prose surface across the three AI-workflow pages (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`) so the visible text reads correctly for any AiPlatform selection. Apply Hybrid pattern (universal noun above `_AiSelector`, `@aiPlatform.DisplayName` injection below); generalize C# exception messages, `data-busy-title` attribute, 3 Help markdown files, and `DeckAnalysisPacketService` log prefix; honor JudgeQuestions D-03 carve-out and Phase 10 / Phase 13 D-08 / Phase 15 identifier invariants. (planned 2026-05-18 on `v1.3` branch) (completed 2026-05-19)
- [x] **Phase 999.2: Claude `<result>` Wrapper — Direct JSON Output Option** — Stop the 5 Claude prompt variants from instructing Claude to wrap JSON in `<result>...</result>` tags. Claude empirically emits BOTH the wrapper AND a duplicate fenced ```json block (Phase 13 UAT T4 observation, 2026-05-17), cluttering chat output. Remove the two wrap-instruction `AppendLine` calls from each Claude variant and replace them in-place with the verbatim ChatGPT-counterpart fenced-block directive. Parser stays untouched (legacy zips still parse via `<result>` regex branch per D-12). ChatGPT + Gemini variants stay untouched (D-02). Update `ResultContractTests.cs` Claude theory rows to reflect divergence (D-13). (planned 2026-05-19 on `v1.3` branch) (completed 2026-05-19)
- [ ] **Phase 999.3: Packet Download Session Cache** — Eliminate full Scryfall pipeline replay on packet download. Today both `POST /deck-analysis` (preview) and `POST /deck-analysis/download` (zip) call `_deckAnalysisPacketService.BuildAsync(request, ...)` from scratch, so a large deck pays the multi-minute Scryfall round-trip cost twice. Add a per-request session cache (in-memory keyed by request hash, TTL bounded) so the download endpoint reuses the artifact built during preview when inputs match. Apply same pattern to `cedh-meta-gap` and `deck-comparison` download endpoints if they exhibit the same shape. Surfaced during Phase 999.2 UAT 2026-05-20. (planned 2026-05-20 on `v1.3` branch)
- [ ] **Phase 999.4: Truncated-JSON Response UX** — Catch `JsonReaderException` (and sibling `JsonException` shapes) thrown when user pastes a truncated Claude/ChatGPT/Gemini response into the AI-response textarea on `/deck-analysis`, `/deck-comparison`, and `/cedh-meta-gap`. Today the exception bubbles to the generic error page with a raw stack trace ("Expected end of string, but instead reached end of data. LineNumber: X | BytePositionInLine: Y"). Replace with a user-facing message ("The pasted response appears truncated — wait for the AI to finish generating before copying, then re-submit.") rendered inline on the workflow page. Surfaced during Phase 999.2 UAT 2026-05-20. (planned 2026-05-20 on `v1.3` branch)

## Phase Details

### Phase 11: Web Design Guidelines Audit Fixes

**Goal**: Land the 10 sweep PRs from the 2026-05-13 Web Design Guidelines audit so DeckFlow's frontend clears the P1 accessibility bar and removes guideline violations across admin + main shell + theme system.
**Depends on**: Nothing (kicks off v1.3).
**Requirements**: WDG-01, WDG-02, WDG-03, WDG-04, WDG-05, WDG-06, WDG-07, WDG-08, WDG-09, WDG-10
**Success Criteria** (what must be TRUE):

  1. Tab-navigating every page under `/Admin/*` shows a visible focus ring on the currently focused element (admin.css universal `:focus-visible` block mirrors `site.css:109-118`).
  2. Keyboard users can pick suggestions from every `df-typeahead` autocomplete input (SuggestCategories card-name, DeckConvert commander, JudgeQuestions card, CommanderCategories, CardLookup single) using ArrowDown/Up/Enter/Escape, with full ARIA combobox attributes wired (`role="combobox"`, `aria-autocomplete="list"`, `aria-expanded`, `aria-controls`, `aria-activedescendant`, options `role="option"`).
  3. With JavaScript disabled, the workflow-step tablist on Packets / DeckComparison / CedhMetaGap pre-selects the current step server-side (`aria-selected="true" tabindex="0"` on current, `aria-selected="false" tabindex="-1"` on others).
  4. No inline `style`/`onclick`/`onchange`/`onsubmit` handlers remain in AdminFeedback Detail, AdminFeedback Index, or `Views/Deck/Error.cshtml`; the app is CSP-ready for `script-src 'self'` + `style-src 'self'`.
  5. All 10 sweep PRs from `260513-wdg-FINDINGS.md` merge to `v1.3`, with cross-cutting a11y rules (`color-scheme`, global `prefers-reduced-motion`, `touch-action: manipulation`, `tabular-nums`, `scroll-margin-top`) added to `site-common.css` so all 22 guild themes inherit them without per-fork edit, and `Release` `dotnet build DeckFlow.sln` completes clean.

**Plans:** 10/10 plans complete
Plans:

- [x] 11-01-PLAN.md — Sweep 1 (WDG-08): cross-cutting a11y rules added to site-common.css (color-scheme, prefers-reduced-motion, touch-action, .tabular, scroll-margin-top)
- [x] 11-02-PLAN.md — Sweep 2 (WDG-01): universal :focus-visible block + color-scheme + tabular-nums added to admin.css
- [x] 11-03-PLAN.md — Sweep 3 (WDG-07): Razor `selected="@(x ? "selected" : null)"` sweep across DeckSync, DeckConvert, SuggestCategories, AdminHarvest/Index
- [x] 11-04-PLAN.md — Sweep 4 (WDG-04): inline style/onchange removal from Error.cshtml + AdminFeedback/Index.cshtml; D-05 deferral comment on AdminFeedback/Detail.cshtml
- [x] 11-05-PLAN.md — Sweep 5 (WDG-02): df-typeahead.ts ARIA combobox refactor + ArrowDown/Up/Enter/Escape keyboard handlers (5 consumer pages benefit)
- [x] 11-06-PLAN.md — Sweep 6 (WDG-06): `<caption>` + `<th scope="col">` table semantics across AdminFlags, AdminFeedback Index, AdminHarvest, DeckSync, CommanderCategories, CedhMetaGap
- [x] 11-07-PLAN.md — Sweep 7 (WDG-09): URL input autocomplete=url + inputmode=url + ellipsis placeholders; user-paste textarea autocomplete=off sweep
- [x] 11-08-PLAN.md — Sweep 8 (WDG-05): info-tooltip `<span title=...>` → `<details><summary>` conversion in SuggestCategories + CommanderCategories
- [x] 11-09-PLAN.md — Sweep 9 (WDG-03): _WorkflowStepTabs.cshtml server-renders aria-selected + tabindex based on current step
- [x] 11-10-PLAN.md — Sweep 10 (WDG-10): role="status" + aria-live="polite" added to #harvest-status-live element in AdminHarvest/Index.cshtml

**UI hint**: yes

### Phase 12: AI-Agnostic URL + Page Rename

**Goal**: Drop "ChatGPT" branding from the three multi-AI workflow pages at the URL + visible-label layer so the AI-agnostic reality of v1.2's per-AI dispatch is reflected in what users see and bookmark.
**Depends on**: Phase 11 (so explainer-line `.page-lede` CSS in `site-common.css` lands on Phase 11's foundation before Phase 12 uses it).
**Requirements**: RENAME-01, RENAME-02, RENAME-03
**Success Criteria** (what must be TRUE):

  1. The three current URLs (`/chatgpt-packets`, `/chatgpt-deck-comparison`, `/chatgpt-cedh-meta-gap`) plus their `/upload` / `/download` sub-routes serve permanent (301) redirects to the new AI-agnostic slugs; visiting an old bookmark lands on the renamed page with no broken links.
  2. Page `<h1>`, top-nav labels in `_DeckToolTabs.cshtml`, hub-card titles on `Home.cshtml`, and `<title>` element values on all three pages reflect AI-agnostic naming; explainer text under each H1 preserves the "this generates something to paste into an AI" cue per `.planning/AI-AGNOSTIC-RENAME-BRAINSTORM.md` Mock A.
  3. Session zip download filenames and `Content-Disposition` headers use AI-agnostic artifact terminology consistent with the new page naming (filename sanitizer in `ChatGptPacketArtifactStore` updated; AI-segment in the filename pattern preserved per Phase 10 commit `00e5bdd`).
  4. README, `DeckFlow.Web/Help/**/*.md`, and the browser-extension package (`browser-extensions/deckflow-bridge/`) reference the new URLs; no hardcoded `chatgpt-` paths remain in any tracked file outside of permanent-redirect registrations.

**Plans:** 5/5 plans complete
Plans:
**Wave 1**

- [x] 12-01-PLAN.md — UseRewriter 301 block (9 redirects) + DeckController 12 route attribute replacements
- [x] 12-04-PLAN.md — Suggest*ZipFileName helpers: deckflow-packet→deck-analysis, compare2→comparison, cedh→cedh-meta-gap (chatgpt AI fallback preserved)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 12-02-PLAN.md — git mv 3 view files (ChatGpt*.cshtml → AI-agnostic names) + DeckController View() literal-string updates

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 12-03-PLAN.md — Page-1 H1/title/nav/hub label swap + 3 page-lede explainer paragraphs + .page-lede CSS in site-common.css + 6 hrefs across nav and home

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 12-05-PLAN.md — README + Help/*.md URL sweep + browser-extension verification + manifest version bump (conditional) + phase-wide D-15 grep gate

**UI hint**: yes

### Phase 13: ChatGpt* Class Rename + Summary Doc Comments

**Goal**: Bring the C# class name layer in line with the user-facing rename from Phase 12 by stripping the "ChatGpt" prefix from request DTOs, services, view models, parsers, and artifact stores — and use the renaming pass to backfill missing XML `<summary>` doc comments on every touched class.
**Depends on**: Phase 12 (renamed classes line up with the user-facing terminology decided in Phase 12; pairs URL + class rename as one conceptual unit but shipped as separate phases per user execution order).
**Requirements**: CLASSRENAME-01, CLASSRENAME-02, CLASSRENAME-03
**Success Criteria** (what must be TRUE):

  1. All `ChatGpt*`-prefixed public types renamed to AI-agnostic names — including `ChatGptDeckRequest`, `ChatGptDeckPacketService`, `ChatGptRequestContextParser`, `ChatGptPacketArtifactStore`, `ChatGptDeckComparisonService`, `ChatGptCedhMetaGapService`, `ChatGptDeckViewModel`, `ChatGptDeckComparisonViewModel`, `ChatGptCedhMetaGapViewModel`, `ChatGptCedhMetaGapRequest`, `ChatGptDeckComparisonRequest` — with grep across the solution returning zero `ChatGpt` matches outside of explicitly-preserved string literals (e.g., `AiPlatform.Key = "ChatGPT"`).
  2. Every renamed class has an XML `<summary>` doc comment describing its current responsibility; `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in `DeckFlow.Web.csproj` compiles clean for the renamed types without relying on `NoWarn 1591` suppression.
  3. DI registrations in `Program.cs`, `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` in `AssemblyInfo.cs`, namespace imports, controller actions, view-model bindings, test fixtures, Razor `@model` directives, and form `name` attributes that bind to renamed properties are all updated; `dotnet build DeckFlow.sln --configuration Release` succeeds with zero new warnings.
  4. Zero user-visible behavior change verified by re-running the full manual T1-T8 integration suite (per `.planning/milestones/v1.2-MILESTONE-AUDIT.md`) against post-rename HEAD: all three pages still produce identical artifacts and round-trip identical zips.

**Plans:** 4/4 plans complete

Plans:

**Wave 1**

- [x] 13-01-PLAN.md — Rename 10 model files (Request/ViewModel/Response triplets for DeckAnalysis/DeckComparison/MetaGap + SetUpgradeResponse) + DeckPageTab enum values; backfill XML <summary> on 29 types

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 13-02-PLAN.md — Rename 7 service files (DeckAnalysisPacketService, DeckComparisonService, MetaGapService, PacketArtifactStore, RequestContextParser, ResponseParsers, JsonTextFormatterService) + Program.cs DI block + README.md mentions; ChatGptResultWrapInstruction const renamed

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 13-03-PLAN.md — Sweep DeckController.cs (142 hits: 12 action methods + ctor + body refs) + 3 Razor @model directives + _DeckToolTabs.cshtml enum refs + _BracketCallout.cshtml comment refs (preserving Phase 12 view-name string literals + route attributes)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 13-04-PLAN.md — Rename 9 test files + DeckControllerTests.cs (126 hits + 6 inline test doubles) + TestServiceFactory.cs; final dotnet build clean gate + allowlisted grep gate + CLI smoke + HUMAN-UAT.md T1-T8 manual round-trip checkpoint

### Phase 14: Broader Codebase Name-vs-Behavior Audit

**Goal**: Use the Phase 13 rename pass as a template to sweep the rest of the codebase for classes whose names no longer describe their current behavior, and backfill missing `<summary>` doc comments across `DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.CLI`, and both test projects.
**Depends on**: Phase 13 (uses class-rename pattern as template; runs after the largest rename surface has stabilized).
**Requirements**: AUDIT-01, AUDIT-02, AUDIT-03
**Success Criteria** (what must be TRUE):

  1. Every public class in `DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.CLI`, `DeckFlow.Core.Tests`, and `DeckFlow.Web.Tests` has been reviewed for name-vs-behavior alignment; classes whose names don't describe current responsibility are renamed (candidates to verify per REQUIREMENTS.md: `ScryfallTaggerService`, `CommanderSpellbookService`, `Null*`/`Fake*`/`Stub*` test-double scoping consistency).
  2. Every public class and interface across all 5 projects has an XML `<summary>` doc comment; `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is verified clean (or newly enabled) on `DeckFlow.Core` and `DeckFlow.CLI` in addition to the already-on `DeckFlow.Web`.
  3. `dotnet build DeckFlow.sln --configuration Release` produces zero new warnings vs. the pre-Phase-14 baseline; test discovery succeeds via `dotnet test --no-build` where WSL permits, otherwise verified via push-and-watch CI on the `v1.3` branch.
  4. Scope discipline observed: DeckController god-class split and ChatGPT-services extraction stay out of scope per PROJECT.md (own refactor milestones); renames touch class names + doc comments only, no responsibility splits.

**Plans:** 4/4 plans complete
Plans:

**Wave 1**

- [x] 14-01-PLAN.md — Capture pre-phase warning baseline + emit 14-AUDIT-REPORT.md (rename worklist + doc-backfill worklist + XML coverage-diff gate codification) and 14-BASELINE.md

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 14-02-PLAN.md — Execute every rename from 14-AUDIT-REPORT.md (ScryfallTaggerService→ScryfallTaggerLookupService + 8 test-double canonicalizations); one commit per rename; D-08 mid-plan green invariant

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 14-03-PLAN.md — Doc-comment backfill across DeckFlow.Core records (Models/ — DeckEntry, DeckDiff, LoadedDecks, PrintingConflict + others) and ~47 test classes; DeckPageTab discretionary opt-in; { get; init; } preservation grep on every commit

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 14-04-PLAN.md — Flip GenerateDocumentationFile=true in 4 csprojs (Core, CLI, Core.Tests, Web.Tests; Web stays as-is); run AUDIT-03 triple-gate (warning count vs baseline + XML coverage diff per RESEARCH.md Option A overriding D-04 + test discovery with Render push-and-watch fallback); emit 14-COVERAGE.md; mark phase complete

### Phase 15: AiPlatform Value Object Refactor

**Goal**: Replace the stringly-typed `TargetAiPlatform` dispatch with a sealed `AiPlatform` record value object per `10-AISEL-PLATFORM-DESIGN.md`, taking the OCP forecast from 3/10 to 8/10 so adding a 4th AI in the future is one registry entry plus N variant classes instead of edits across 9+ files.
**Depends on**: Phase 13 + Phase 14 (refactor sits on top of clean class names and audited responsibilities so the value object lands on a stable surface).
**Requirements**: AIPLATFORM-01, AIPLATFORM-02, AIPLATFORM-03
**Success Criteria** (what must be TRUE):

  1. `AiPlatform` sealed record value object replaces `string TargetAiPlatform` on all three renamed request DTOs (per CLASSRENAME-01 final names — likely `DeckAnalysisRequest`, `DeckComparisonRequest`, `MetaGapRequest`); the value object encapsulates name, display label, enabled flag, and response-extraction strategy with `AiPlatform.All` as the single source of truth and `AiPlatform.Normalize(string?)` handling form-post / zip-load deserialization.
  2. All five per-AI prompt builders (`BuildAnalysisPrompt`, `BuildSetUpgradePrompt`, `BuildComparisonPrompt`, `BuildFollowUpPrompt`, `BuildMetaGapPrompt`), the unified `<result>` extractor in `ExtractJsonPayload`, the artifact store round-trip (`LoadFromZip` / `BuildZip`), the request context parser, and the `_AiSelector.cshtml` Razor partial dispatch via the value-object API (registry pattern per design doc) — not via string switches.
  3. `DECKFLOW_GEMINI_ENABLED` env-var gating on Gemini option visibility is preserved end-to-end (server still hides the radio when the flag is unset; saved zips with `target_ai_platform: Gemini` still round-trip).
  4. Zero user-visible behavior change: full T1-T8 manual integration tests (per `.planning/milestones/v1.2-MILESTONE-AUDIT.md`) plus filename verify pass against post-refactor HEAD; all three pages produce byte-identical artifacts and round-trip identical zips before and after the refactor.
  5. Hypothetical 4th-platform extension test: adding `AiPlatform.Test` to `AiPlatform.All` + one stub variant per builder family does NOT require editing any switch expression, request-model setter, Razor partial, or context parser (proven by an actual test in the suite).

**Plans:** 3/3 plans complete

**Wave 1**

- [x] 15-01-PLAN.md — AiPlatform sealed record + 3 DTO setter migrations + _AiSelector.cshtml loop + RequestContextParser/PacketArtifactStore defensive Normalize + existing test migration to [MemberData(AllPlatforms)]

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 15-02-PLAN.md — Extract 5 prompt-builder families to 25 new files under Services/PromptBuilders/{Analysis,SetUpgrade,Comparison,FollowUp,MetaGap}/; wire 20 DI registrations; delete dispatcher switches

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 15-03-PLAN.md — Add internal AllForTesting seam + AiPlatformExtensionTests.cs (SC5 4th-platform proof) + full T1-T8 manual integration suite + byte-identical sha256 verification + final dotnet build clean + push-and-watch CI on v1.3

### Phase 999.1: AI-Agnostic Prose Adaptation in Razor Views

**Goal**: Strip the hardcoded `"ChatGPT"` brand from every user-visible prose surface across the three AI-workflow pages (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`) so the visible text reads correctly whether the user picked ChatGPT, Claude, or Gemini in the Step 2 `_AiSelector`. Phase 12 already renamed URLs + H1s; Phase 13 renamed C# class symbols; Phase 15 shipped the `AiPlatform` value object plumbing. Phase 999.1 closes the remaining "brand leaks into copy" surface.
**Depends on**: Phase 15 (uses `AiPlatform.Normalize(Model.Request.TargetAiPlatform).DisplayName` plumbing for below-selector Razor injection).
**Requirements**: D-01..D-05 from `.planning/phases/999.1-ai-agnostic-prose-adaptation-razor-views/999.1-CONTEXT.md` (backlog phase — no formal REQ-IDs assigned; CONTEXT.md decisions are the binding contract).
**Success Criteria** (what must be TRUE):

  1. Above-selector Razor prose across `DeckAnalysis.cshtml`, `DeckComparison.cshtml`, `CedhMetaGap.cshtml` uses the universal noun `your AI` (D-05); Phase 12 enumerated `ChatGPT, Claude, or Gemini` ledes (one per page) are preserved per D-05 exception clause.
  2. Below-selector Razor prose injects `@aiPlatform.DisplayName` via `AiPlatform.Normalize(Model.Request.TargetAiPlatform)` so the same view text reads correctly for ChatGPT / Claude / Gemini selections (D-01 Hybrid pattern).
  3. User-facing exception messages in `ResponseParsers.cs`, `MetaGapService.cs`, `DeckComparisonService.cs` (8 in-scope strings) use the universal noun convention; the Phase 10 fallback Key `"ChatGPT"` passed to `NormalizeSingleLine(...)` on lines `MetaGapService.cs:266`, `DeckComparisonService.cs:229`, `DeckAnalysisPacketService.cs:1309` is preserved byte-identical.
  4. `data-busy-title="Building ChatGPT Packets"` on `DeckAnalysis.cshtml:72` becomes `"Building Deck Analysis Packet"` (matches Phase 13 class naming + sibling pages' neutral busy titles). All other attributes on that line (Phase 10 / Phase 13 D-08 invariants) survive byte-identical.
  5. The 3 Help markdown files (`deck-analysis.md`, `deck-comparison.md`, `cedh-meta-gap.md`) use the universal noun convention; `Help/ask-a-judge.md` is NOT modified (D-03 JudgeQuestions carve-out).
  6. `DeckAnalysisPacketService.cs` log prefix renamed from "ChatGPT packet" to "Deck Analysis packet" across 5 `_logger.LogInformation` sites; the 2 doc comments mentioning ChatGPT are preserved per Phase 13 D-07.
  7. JudgeQuestions D-03 carve-out preserved: `JudgeQuestions.cshtml` (6 hits), `Home.cshtml` line 62 hub card (1 hit), `Help/ask-a-judge.md` (2 hits) all unchanged.
  8. Phase 13 D-08 TS/CSS-coupled identifiers preserved byte-identical: `data-cache-key="chatgpt-*"`, `data-chatgpt-*` attrs, `chatgpt-packets-form` + `chatgpt-step-eyebrow` CSS classes, `parseChatGptDownloadFilename` TS const. Phase 10 invariants preserved: AiPlatform.Key literals, `"chatgpt"` zip filename fallback in `PacketArtifactStore.cs`, `JsonTextFormatterService.ResultWrapInstruction` enumerated form.
  9. README.md describes the multi-AI workflow generically; D-03 Ask-a-Judge paragraph preserved verbatim.
  10. `dotnet build DeckFlow.sln -c Release` exits 0 with zero new warnings vs baseline; manual UAT confirms all three pages render correctly for ChatGPT / Claude / Gemini selections.

**Plans:** 7/7 plans complete

Plans:

**Wave 1** *(parallel; zero file overlap)*

- [x] 999.1-01-PLAN.md — DeckAnalysis.cshtml Hybrid prose + AiPlatform.Normalize block + data-busy-title rename (28 hits → 1 lede)
- [x] 999.1-02-PLAN.md — DeckComparison.cshtml Hybrid prose + AiPlatform.Normalize block (12 hits → 1 lede)
- [x] 999.1-03-PLAN.md — CedhMetaGap.cshtml Hybrid prose + AiPlatform.Normalize block + Home.cshtml DeckComparison hub-card generalization (8 + 1 hits; D-03 carve-out on Home line 62 preserved)
- [x] 999.1-04-PLAN.md — C# user-facing exception messages: ResponseParsers.cs (6 strings) + MetaGapService.cs (2 strings) + DeckComparisonService.cs (2 strings); Phase 10 fallback Key strings preserved
- [x] 999.1-05-PLAN.md — Help markdown: deck-analysis.md (11 hits) + deck-comparison.md (6 hits) + cedh-meta-gap.md (1 hit); ask-a-judge.md carved out per D-03
- [x] 999.1-06-PLAN.md — DeckAnalysisPacketService.cs logger prefix rename (5 log lines); 2 doc comments + Phase 10 fallback Key preserved

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 999.1-07-PLAN.md — README brand audit + full-phase invariants grep gate (Phase 10 / Phase 13 D-08 / Phase 15 / D-03 carve-outs) + `dotnet build DeckFlow.sln -c Release` clean + HUMAN-UAT.md manual sign-off

### Phase 999.2: Claude `<result>` Wrapper — Direct JSON Output Option

**Goal**: Stop the 5 Claude prompt variants from instructing Claude to wrap JSON in `<result>...</result>` tags. Claude empirically emits BOTH the wrapper AND a duplicate fenced ```json block (observed during Phase 13 UAT T4, 2026-05-17), cluttering chat output without parser benefit. Remove the two wrap-instruction `AppendLine` calls from each of the 5 Claude prompt-variant files and replace them in-place with the verbatim fenced-block directive used by the matching ChatGPT counterpart. Parser stays untouched (`JsonTextFormatterService.ExtractJsonPayload` already falls through `<result>` regex to brace-scan, so legacy zip artifacts containing pre-999.2 Claude responses still parse via the `<result>` branch). ChatGPT + Gemini variants stay untouched. Test contract `ResultContractTests.cs` updated to reflect Claude's divergence from the ChatGPT/Gemini `<result>`-wrap convention.
**Depends on**: Phase 15 (exercises the Phase 15 "registries are the per-platform strategy surface" claim — Claude variants diverge at the variant-class level without touching the `AiPlatform` record or adding any new strategy interface).
**Requirements**: D-01..D-14 from `.planning/phases/999.2-claude-result-wrapper-direct-json-output/999.2-CONTEXT.md` (backlog-style phase — no formal REQ-IDs assigned; CONTEXT.md decisions are the binding contract).
**Success Criteria** (what must be TRUE):

  1. (D-01, D-02) Zero `ResultWrapInstruction` references in any `DeckFlow.Web/Services/PromptBuilders/*/Claude*PromptVariant.cs`; zero `<result>` substrings in any Claude variant; all 5 Claude prompt-variant families edited.
  2. (D-03) The follow-on "Wrap your final structured output in `<result>...</result>` tags..." AppendLine deleted in every Claude variant alongside the `ResultWrapInstruction` line.
  3. (D-04, D-05, D-06) Each Claude variant emits a fenced ```json code-block directive verbatim-copied from its ChatGPT family counterpart (with leading numbered-list / bullet whitespace prefix stripped per D-05 exception clause); the new directive lands in the exact same line slot the deleted lines occupied — immediately before `builder.AppendLine("</" + "task>");`.
  4. (D-07) Per-version `deck_profile` and similar sub-object fenced-block directives in Claude variants are untouched.
  5. (D-08) No new property on `AiPlatform` record; no new `IResultWrapPolicy` interface; no new helper method on `JsonTextFormatterService`; Phase 15 D-01 data-only-record invariant preserved.
  6. (D-11) Phase grep gate passes: zero hits for `ResultWrapInstruction` and `<result>` in Claude variants; exactly 10 hits for `ResultWrapInstruction` across ChatGPT + Gemini variants; 5 hits each for Gemini `ResultWrapInstruction` and `GeminiJsonMandate`; `dotnet.exe build DeckFlow.sln -c Release` exits 0 with 0 new warnings; manual UAT T3 (Claude / deck-analysis) + T7 (Claude / cEDH meta-gap) + ad-hoc Claude / deck-comparison all show a single fenced ```json block in Claude's chat with no `<result>` tags AND round-trip paste-back parses cleanly.
  7. (D-12) `JsonTextFormatterService.cs` byte-identical to pre-phase state; legacy zip artifacts with pre-999.2 Claude `<result>` responses still parse via the regex branch.
  8. (D-13) `ResultContractTests.cs` updated: 5 `[InlineData("Claude")]` rows removed from prompt-body-asserting theories; 5 new Claude-specific facts assert the fenced-block substring AND `Assert.DoesNotContain("<result>", ...)`; ChatGPT + Gemini coverage intact via the unchanged `AssertContainsResultWrap` helper.

**Plans:** 1/1 plans complete
Plans:
**Wave 1**

- [x] 999.2-01-PLAN.md — Drop `<result>` wrapper from all 5 Claude prompt variants (1 commit per variant per D-09, alphabetical: Analysis → Comparison → FollowUp → MetaGap → SetUpgrade) + D-13 test impact audit fix in `ResultContractTests.cs` (5 Claude theory rows replaced with Claude-specific fenced-block facts) + Task 7 manual UAT T3 + T7 + ad-hoc Claude / deck-comparison gate

---

## Progress

| Phase | Milestone | Plans | Status | Completed |
|-------|-----------|-------|--------|-----------|
| 1. Visual System Tokens | v1.0 | 3/3 | Complete | 2026-04-30 |
| 2. Layout, Hierarchy & UX Copy | v1.0 | 3/3 | Complete | 2026-04-30 |
| 3. Tech-Debt Cleanup | v1.0 | 4/4 | Complete | 2026-05-01 |
| 4. Security & Bug Fixes | v1.0 | 4/4 | Abandoned (rerouted to Ph. 5) | 2026-05-02 |
| 5. Security & Bug Fixes v2 | v1.0 | 3/3 | Complete | 2026-05-02 |
| 6. Admin Shell + Flags Foundation | v1.1 | 7/7 | Complete | 2026-05-03 |
| 7. Harvest Controls + Stats | v1.1 | 7/7 | Complete | 2026-05-03 |
| 7.1 Categories Flag + SameOrigin Fix | v1.1 | 2/2 | Complete | 2026-05-03 |
| 8. Analytics | v1.1 | 5/5 | Complete | 2026-05-08 |
| 9. Bracket UX + AI Selector Foundation | v1.2 | 3/3 | Complete | 2026-05-08 |
| 10. Claude + Gemini Artifact Optimization | v1.2 | 5/5 | Complete | 2026-05-13 |
| 11. Web Design Guidelines Audit Fixes | v1.3 | 10/10 | Complete   | 2026-05-13 |
| 12. AI-Agnostic URL + Page Rename | v1.3 | 5/5 | Complete    | 2026-05-17 |
| 13. ChatGpt* Class Rename + Doc Comments | v1.3 | 4/4 | Complete    | 2026-05-17 |
| 14. Broader Codebase Name-vs-Behavior Audit | v1.3 | 4/4 | Complete   | 2026-05-18 |
| 15. AiPlatform Value Object Refactor | v1.3 | 3/3 | Complete    | 2026-05-18 |
| 999.1 AI-Agnostic Prose Adaptation in Razor Views | v1.3 | 7/7 | Complete    | 2026-05-19 |
| 999.2 Claude `<result>` Wrapper — Direct JSON Output Option | v1.3 | 1/1 | Complete   | 2026-05-19 |
| 999.3 Packet Download Session Cache | v1.3 | 0/0 | Planned    | — |
| 999.4 Truncated-JSON Response UX | v1.3 | 0/0 | Planned    | — |

---

## Backlog

### Phase 999.3: edhtop16 Filter Defaults vs DeckFlow Filter Defaults (BACKLOG)

**Goal:** [Captured for future planning]
**Requirements:** TBD
**Plans:** 0 plans

Captured 2026-05-17 during Phase 13 UAT T5. cEDH Meta-Gap fails to find Plagon, Lord of the Beach decks even though edhtop16.com shows multiple recent entries (2025-05 through 2026-01). DeckFlow filters (Six Months + Top Performing + minEventSize) return zero matches; edhtop16.com site UI likely uses different default filter window/event-size threshold/standing cutoff.

Repro (2026-05-17 14:18:57 + 14:19:09 in `web-20260517.log`):
- Commander: "Plagon, Lord of the Beach"
- Filters: SixMonths, TopPerforming, minEventSize=default, maxStanding=default
- Result: `InvalidOperationException` at `MetaGapService.cs:160` — "No EDH Top 16 decks matched your filters..."
- edhtop16.com browser shows entries from 2026-01-04, 2026-01-18, 2025-09-27, 2025-05-24

Pre-existing — predates Phase 13 (MetaGapService logic unchanged by rename). Investigate:
1. edhtop16 GraphQL `commander(name)` lookup: does "Plagon, Lord of the Beach" match the stored canonical name exactly?
2. Default DeckFlow form filter values vs site UI defaults — alignment audit.
3. minEventSize=50 default may be too restrictive — site UI may use 30.
4. timePeriod=SixMonths may map to ≤180 days where site uses calendar months (sometimes 183-184 days).

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)

---

*v1.0 shipped 2026-05-02 | v1.1 shipped 2026-05-08 | v1.2 shipped 2026-05-13 | v1.3 started 2026-05-13*
