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
- [x] **Phase 999.3: Packet Download Session Cache** — Eliminate full Scryfall pipeline replay on packet download. Today both `POST /deck-analysis` (preview) and `POST /deck-analysis/download` (zip) call `_deckAnalysisPacketService.BuildAsync(request, ...)` from scratch, so a large deck pays the multi-minute Scryfall round-trip cost twice. Add a per-request session cache (in-memory keyed by request hash, TTL bounded) so the download endpoint reuses the artifact built during preview when inputs match. Apply same pattern to `cedh-meta-gap` and `deck-comparison` download endpoints if they exhibit the same shape. Surfaced during Phase 999.2 UAT 2026-05-20. (planned 2026-05-20 on `v1.3` branch) (completed 2026-05-21)
- [x] **Phase 999.4: Truncated-JSON Response UX** — Catch `JsonReaderException` (and sibling `JsonException` shapes) thrown when user pastes a truncated Claude/ChatGPT/Gemini response into the AI-response textarea on `/deck-analysis`, `/deck-comparison`, and `/cedh-meta-gap`. Today the exception bubbles to the generic error page with a raw stack trace ("Expected end of string, but instead reached end of data. LineNumber: X | BytePositionInLine: Y"). Replace with a user-facing message ("The pasted response appears truncated — wait for the AI to finish generating before copying, then re-submit.") rendered inline on the workflow page. Surfaced during Phase 999.2 UAT 2026-05-20. (planned 2026-05-20 on `v1.3` branch) (completed 2026-05-21)
- [x] **Phase 999.5: v1.3 Backlog Catch-up + Test Hardening** — Four-plan omnibus closing v1.3 quality debt before milestone ship. P01 test-suite hardening: fix 4 pre-existing test failures surfaced by full `dotnet test` run on 2026-05-21 (FeedbackStoreTests SQLite file-lock race ~10 fails, ScryfallThrottleTests 429-no-retry-after timing 2 fails, DeckAnalysisPacketServiceTests:360 stale "ChatGPT" assertion vs Phase 999.1 "AI" message, DeckComparisonServiceTests:286 CRLF vs LF assertion). P02 D-07 semantic-completeness guards: mirror `HasMeaningful*Content` pattern (`ResponseParsers.cs:99-130`) into `DeckComparisonService.ParseComparisonResponse` + `MetaGapService.ParseResponse` to reject valid-JSON-but-semantically-empty AI responses (Codex pass-1 999.4 MED-2 carve-out). P03 harvest-killed-by-suggestion: verify gate-starvation fix shipped 2026-05-03, archive debug logs, add named regression facts. P04 ChatGPT `<result>` directive strip (added 2026-05-21 from user report): remove `JsonTextFormatterService.ResultWrapInstruction` from 5 ChatGptXxxPromptVariant files (cosmetic noise — ChatGPT honors fenced ```json reliably); Gemini variants stay (validated by 2026-05-09 integration test); parser stays (backward-compatible). (planned 2026-05-21 on `v1.3` branch)
- [ ] **Phase 999.7: v1.3 Audit Cleanup — STATE Arithmetic + WDG Checkboxes + Stale Doc-Comments** — Land the documentation + state-tracking cleanup surfaced by `/gsd-audit-milestone v1.3` (audit at `.planning/v1.3-MILESTONE-AUDIT.md`): correct STATE.md progress arithmetic (`completed_phases:9 → 11`, `completed_plans:66 → 46`), flip 10 WDG-* REQUIREMENTS.md checkboxes from `[ ]` to `[x]`, update 3 stale Claude variant `<summary>` doc-comments (audit F-01), document the intentional `/deck-analysis/download` short-circuit-tier omission (audit F-02), and finalize 999.5-UAT.md `status: testing → superseded-by-999.6`. No production-code behavior change. (planned 2026-05-22 on `v1.3` branch)
- [x] **Phase 999.6: v1.3 Ship-Gate Test Residual Cleanup** — Resolve 9 residual test failures blocking v1.3 ship per the new `no-ship-failing-tests` rule (established 2026-05-22). Triage breakdown from 999.5 close-out full-suite gate (`Failed 9, Passed 433, Skipped 3, Total 445`): (a) **6 stale tests** drifted behind shipped production renames — `PacketArtifactStoreTests.LoadFromZip_throws_when_no_response_json_present` (msg string drift, expects `"40-deck-profile.json"` but SUT emits `"recognized DeckFlow"`), 3× `AiPlatformPhase10RoundTripTests.SuggestComparisonZipFileName_includes_lowercased_ai_name(Claude|ChatGPT|Gemini)` (expect `atraxa-compare2-` prefix; production renamed page to `comparison`), `AiPlatformPhase10RoundTripTests.SuggestComparisonZipFileName_includes_compare2_page_segment` (expects `-compare2-` substring; dead premise), `PacketArtifactStoreRoundTripTests.LoadFromZip_AlsoRestoresUserInputs_FromArnaFixture` (reads hard-coded `C:\tmp\arna-test\00-input-summary.txt` dev-only fixture path absent from repo). (b) **1 flaky test** — `BasicAuthMiddlewareTests.CorrectCredentials_InvokesNext` passes 5/5 in isolation but fails in full-suite run (state-leak between facts; find shared mutable static or env var bleed). (c) **2 ambiguous** — `ArchidektCacheJobServiceTests.BackgroundService_SucceedsAndUpdatesProcessedCounts` + `GetActiveJob_ReturnsNullAfterCompletedJob` both fire `Assert.NotNull` on background-service job state. Smells like race (job polled before scheduler runs SUT) but warrants `/gsd-debug` pass before fix-or-update verdict; the second test's name vs assertion conflict ("ReturnsNullAfterCompletedJob" asserting NotNull) suggests genuine test-logic confusion. Acceptance: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -c Release` reports `Failed 0` before v1.3 ship. (planned 2026-05-22 on `v1.3` branch)

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

### Phase 999.3: Packet Download Session Cache

**Goal**: Eliminate full Scryfall pipeline replay on packet download. The three workflow services (`DeckAnalysisPacketService`, `DeckComparisonService`, `MetaGapService`) each expose preview + download endpoint pairs where both endpoints call `BuildAsync(request, ...)` from scratch — a large Commander deck pays the multi-minute Scryfall round-trip cost twice. Phase 999.3 adds an in-memory result cache keyed off the BuildAsync-relevant input fields so the download endpoint reuses the artifact built during preview when the same inputs are resubmitted. Layered BENEATH the existing response-json-only short-circuit on deck-comparison/cedh-meta-gap downloads (D-10). Pure optimization, never a correctness contract — cache miss falls through silently to `BuildAsync` (D-11). In-memory only, dedicated `MemoryCache` instance with `SizeLimit = 10_000_000` and 5-minute absolute TTL (D-05, D-06). All 3 workflow services symmetric scope (D-08).
**Depends on**: Phase 15 (`AiPlatform.Key` as cache-key dimension per D-01 keyed inputs).
**Requirements**: D-01..D-14 from `.planning/phases/999.3-packet-download-session-cache/999.3-CONTEXT.md` (backlog-style phase — no formal REQ-IDs assigned; CONTEXT.md decisions are the binding contract).
**Success Criteria** (what must be TRUE):

  1. (D-01, D-02) Cache key built from only Scryfall-pipeline-relevant fields (`commander`, normalized `DeckSource`, include flags, `AiPlatform.Key`, `selectedQuestionIds[]` for analysis; per-service equivalents for comparison + meta-gap). `WorkflowStep`, `*ResponseJson`, parsed-response objects, `ImportWarning` excluded. URL-mode and paste-mode of the same deck collapse to one key.
  2. (D-03) Key is lowercase hex SHA-256 of canonical-JSON-serialized field bag via `System.Text.Json` + `SHA256.HashData`. ~64-char string keys consistent with existing `IMemoryCache` string-key pattern.
  3. (D-04) Global key scope — no per-IP / per-session salt; matches existing `CardLookupCache` / `CommanderSpellbookService` patterns.
  4. (D-05, D-06, D-07) Dedicated `MemoryCache` instance with `SizeLimit = 10_000_000`, 5-minute absolute TTL, per-entry `Size` = sum of result payload char-lengths. NOT the shared singleton `IMemoryCache`.
  5. (D-08, D-09) All 3 services participate: `DeckAnalysisPacketService`, `DeckComparisonService`, `MetaGapService`. `/deck-analysis/upload` also writes to the cache (same shape as preview). `deck-comparison` and `cedh-meta-gap` have no `/upload` endpoint — out of scope.
  6. (D-10) Download endpoint precedence preserved: (1) existing response-json-only short-circuit FIRST (deck-comparison line 705, cedh-meta-gap line 956), (2) packet cache lookup SECOND, (3) `BuildAsync` fallthrough THIRD. The existing fast paths are byte-identical post-phase.
  7. (D-11, D-12) Cache miss falls through silently to `BuildAsync` (no "session expired" UX). Stampede accepted — no per-key SemaphoreSlim, no Lazy<Task<T>> singleflight. Matches existing cache patterns.
  8. (D-13) Structured Serilog log line per lookup: `_logger.LogInformation("Packet cache {Outcome} for {KeyPrefix} ({SizeBytes} bytes)", outcome, key.Substring(0,8), sizeEstimate)` where `Outcome ∈ {hit, miss, write, evicted}`. Greppable from `/data/logs/web-*.log`.
  9. (D-14) Test gate: manual UAT (large Commander deck, preview→download click <2s vs today's 2+ min) + `dotnet.exe build DeckFlow.sln -c Release` exits 0 with 0 new warnings + grep gate confirming `TryGetValue` + `Set` symmetry across all 3 services + the cache wrapper file is present. xUnit unit tests on the cache wrapper (pure-value key normalization) are planner-discretion IF they avoid VSTest WSL flakiness.
  10. README updated when behavior changes per `CLAUDE.md`. Plain default-author commits, no `Co-Authored-By` trailer.

**Plans:** 4/4 plans complete
Plans:
**Wave 1**

- [x] 999.3-01-PLAN.md — Create PacketSessionCache wrapper (with CachedEntry size-recoverable envelope) + PacketSizeEstimator + Program.cs DI registration + warning baseline

**Wave 2**

- [x] 999.3-02-PLAN.md — Cache write at end of BuildAsync in 3 services + shared private BuildXxxCacheInputs helpers (single source of truth for write↔read parity) + per-service async TryComputeCacheKeyAsync helpers + Program.cs factory blocks

**Wave 3**

- [x] 999.3-03-PLAN.md — Layer cache-read precedence onto 3 download handlers in DeckController.cs (D-10), calling Plan 02's async helpers; miss-path latency accepted

**Wave 4**

- [x] 999.3-04-PLAN.md — Manual UAT (incl. M3 empirical upload isolation + M4 absence-assertion in isolated log window) + grep gate + build gate + README/STATE/ROADMAP closure

### Phase 999.4: Truncated-JSON Response UX

**Goal**: Replace the raw `JsonReaderException` stack trace ("Expected end of string, but instead reached end of data. LineNumber: X | BytePositionInLine: Y") that bubbles to the generic error page when a user pastes a truncated Claude/ChatGPT/Gemini response into the AI-response textarea on `/deck-analysis`, `/deck-comparison`, or `/cedh-meta-gap`. Catch `JsonReaderException` and sibling `JsonException` shapes at the response-parse boundary, classify as truncation, and render an inline user-facing message ("The pasted response appears truncated — wait for the AI to finish generating before copying, then re-submit.") on the originating workflow page. Surfaced during Phase 999.2 UAT 2026-05-20.
**Depends on**: Phase 999.2 (Claude variants now emit a single fenced JSON block, so the truncation surface is well-defined per-AI) and Phase 15 (`AiPlatform` value object available if per-AI copy differs).
**Requirements**: D-01..D-XX from `.planning/phases/999.4-truncated-json-response-ux/999.4-CONTEXT.md` (backlog-style phase — no formal REQ-IDs; CONTEXT.md decisions are the binding contract).
**Success Criteria** (what must be TRUE):

  1. Pasting a truncated AI response on `/deck-analysis`, `/deck-comparison`, or `/cedh-meta-gap` renders the user-facing inline message instead of the generic error page; no raw stack trace surfaces to the browser.
  2. The truncation-classifier branch is reached only for `JsonReaderException` / `JsonException` shapes consistent with truncated input; unrelated parse failures and upstream Scryfall errors keep their existing handling.
  3. The inline message preserves the user's pasted response textarea content so they can edit/re-submit without re-pasting.
  4. `dotnet build DeckFlow.sln -c Release` exits 0 with zero new warnings vs the post-Phase-999.3 baseline; README updated when user-visible behavior changes per `CLAUDE.md`.
  5. Manual UAT confirms the new UX across all three pages for ChatGPT / Claude / Gemini selections; `D-03` carve-outs (if any) for non-AI-response pages preserved.

**Plans:** 1 plan
Plans:

- [x] 999.4-01-PLAN.md — Catch truncated JSON at 4 AI-response parse sites (ResponseParsers analysis + set-upgrade, DeckComparisonService.ParseComparisonResponse, MetaGapService.ParseResponse) + 4 xUnit truncation facts + manual UAT closure across 4 pages and 2+ AI selections (6 commits per revised D-05; D-06 upload-path carve-out added)

### Phase 999.5: v1.3 Backlog Catch-up + Test Hardening

**Goal**: Close four v1.3 quality-debt items before milestone ship. (1) Stabilize the test suite — `dotnet test DeckFlow.sln -c Release` on 2026-05-21 reported `441 total / 414 passed / 24 failed / 3 skipped`. None of the failures touch shipped 999.1-999.4 service code, but they prevent a clean CI gate. Fix `FeedbackStoreTests` SQLite file-lock race (~10 fails — xunit parallel cleanup contention on `%TEMP%\feedback-test-*.db`), `ScryfallThrottleTests.ExecuteAsync_*_DoesNotRetryFor429WithoutRetryAfter` timing flake (2 fails — `Expected 1, Actual 3`), `DeckAnalysisPacketServiceTests.BuildAsync_ThrowsValidationError_WhenDeckProfileJsonDoesNotMatchSchema:360` stale "ChatGPT response" assertion vs the Phase 999.1 "AI response" message, `DeckComparisonServiceTests.BuildAsync_EmitsCommanderSection_*:286` CRLF-vs-LF expectation. (2) Mirror the `HasMeaningful*Content` semantic-completeness pattern from `ResponseParsers.cs:99-130` into `DeckComparisonService.ParseComparisonResponse` + `MetaGapService.ParseResponse` so valid-JSON-but-semantically-empty AI responses (e.g., `{"deck_comparison":{"deck_a_name":"Atraxa"}}`) raise the same `InvalidOperationException` that wrong-shape and truncated inputs already raise. (3) Verify the `harvest-killed-by-suggestion` gate-starvation fix shipped 2026-05-03 in `CategorySuggestionService.cs` + `CommanderCategoryService.cs`, archive the parked `.planning/debug/` H1 hypothesis logs, remove the stale STATE Deferred Items row, lock the contract with a named regression fact per service. (4) Strip the `JsonTextFormatterService.ResultWrapInstruction` directive from all 5 `ChatGptXxxPromptVariant` files — user-reported 2026-05-21 7:40pm MDT that `<result>` shows up as cosmetic noise in the ChatGPT response UI. ChatGPT honors fenced ```json reliably and each variant already carries the fenced directive. Mirror Phase 999.2 D-13 Claude removal pattern. Gemini variants stay untouched (validated by 2026-05-09 integration test where Gemini ignored fenced-only output — `GeminiJsonMandate` is the last-instruction-wins lever). Parser stays untouched (`JsonTextFormatterService.ExtractJsonPayload` regex falls through to brace-finding when no `<result>` present — backward-compatible with legacy uploads).
**Depends on**: Phase 999.1 (AI-agnostic prose — surfaces the test-rot drift), Phase 999.4 (D-07 deferral creates the semantic-completeness scope).
**Requirements**: D-01..D-XX from `.planning/phases/999.5-v1.3-backlog-catchup-test-hardening/999.5-CONTEXT.md` (backlog-style phase — no formal REQ-IDs; CONTEXT.md decisions are the binding contract).
**Success Criteria** (what must be TRUE):

  1. `dotnet test DeckFlow.sln -c Release` exits 0 with all previously-failing facts resolved (FeedbackStore + ScryfallThrottle + DeckAnalysisPacket validation message + DeckComparison commander-section CRLF). Skipped tests stay skipped; no regressions in TruncatedInput / 999.4 facts.
  2. `DeckComparisonService.ParseComparisonResponse` and `MetaGapService.ParseResponse` raise `InvalidOperationException` carrying a service-appropriate "did not contain a valid X payload" message on semantically-empty input that previously parsed silently; new xUnit facts cover both services.
  3. `harvest-killed-by-suggestion` fix evidence verified (gate-starvation shipped 2026-05-03 in `CategorySuggestionService.cs` + `CommanderCategoryService.cs`); `.planning/debug/harvest-killed-by-suggestion*.md` moved to `.planning/debug/archive/` via `git mv` (history preserved); STATE Deferred Items row removed; one named regression fact added per service locking the `GetActiveJob() is not null` skip-sweep contract.
  4. All 5 `ChatGptXxxPromptVariant` files (Analysis, Comparison, FollowUp, MetaGap, SetUpgrade) no longer emit `JsonTextFormatterService.ResultWrapInstruction`; `ResultContractTests.cs` Theory rows pruned to Gemini-only and 5 new ChatGpt inverse-contract Facts added (`Assert.DoesNotContain("<result>", prompt)` + `Assert.Contains("fenced ```json code block", prompt)`); all 3 Gemini variants and `JsonTextFormatterService.ExtractJsonPayload` parser stay byte-identical.
  5. `dotnet build DeckFlow.sln -c Release` exits 0 with warning count <= post-Phase-999.4 baseline (commit `4af092a`).
  6. CLAUDE.md "README updated when behavior changes" gate: README sweep `grep -i "harvest\|killed-by\|result wrapper\|<result>" README.md` returns existing baseline (no new user-visible behavior changes from this phase — P01-P03 restore prior contracts; P04 removes a cosmetic noise element that was never user-documented).

**Plans:** 4 plans
Plans:

- [x] 999.5-01-PLAN.md — Test-suite hardening: fix FeedbackStore SQLite file-lock race + ScryfallThrottle 429-no-retry timing flake + DeckAnalysisPacketService validation-message rot + DeckComparisonService CRLF assertion
- [x] 999.5-02-PLAN.md — D-07 semantic-completeness guards: mirror `HasMeaningful*Content` pattern into `DeckComparisonService.ParseComparisonResponse` + `MetaGapService.ParseResponse` + xUnit coverage for both services
- [x] 999.5-03-PLAN.md — `harvest-killed-by-suggestion` archival: verify D-14 fix shipped 2026-05-03, `git mv` debug docs to archive, prune STATE row, add 2 regression facts (CategorySuggestionService + CommanderCategoryService)
- [x] 999.5-04-PLAN.md — ChatGPT `<result>` directive strip: remove `ResultWrapInstruction` from 5 ChatGptXxxPromptVariant files + refactor `ResultContractTests.cs` Theory→Fact + add 5 inverse-contract Facts (atomic single commit per D-28)

### Phase 999.6: v1.3 Ship-Gate Test Residual Cleanup

**Goal**: Drive the full `DeckFlow.sln -c Release` test suite to `Failed 0` so v1.3 can ship per the new `no-ship-failing-tests` rule (memory `feedback-no-ship-failing-tests`, established 2026-05-22). Phase 999.5 closed v1.3 quality debt but accepted 9 residual failures under a D-22 "out-of-scope deferral" rationale; that deferral is now revoked. Triage of the 9 (from full-suite gate `Failed 9, Passed 433, Skipped 3, Total 445` on 2026-05-22): **(a) 6 stale tests** — `PacketArtifactStoreTests.LoadFromZip_throws_when_no_response_json_present` (expected msg `"zip did not contain 40-deck-profile.json "`; SUT emits `"zip did not contain a recognized DeckFlow"` — generalization shipped with AI-agnostic prose work), 3× `AiPlatformPhase10RoundTripTests.SuggestComparisonZipFileName_includes_lowercased_ai_name(Claude|ChatGPT|Gemini)` (expect filename prefix `atraxa-compare2-`; production now emits `atraxa-comparison-{platform}-...` per Phase 12 URL rename), `AiPlatformPhase10RoundTripTests.SuggestComparisonZipFileName_includes_compare2_page_segment` (expects `-compare2-` substring; dead premise), `PacketArtifactStoreRoundTripTests.LoadFromZip_AlsoRestoresUserInputs_FromArnaFixture` (reads hard-coded `C:\tmp\arna-test\00-input-summary.txt` dev-machine fixture path — not in repo, fails on every machine). **(b) 1 flaky test** — `BasicAuthMiddlewareTests.CorrectCredentials_InvokesNext` passes 5/5 when filter-isolated, fails in full-suite run → state-leak between facts (shared static / env var / leftover middleware state). **(c) 2 ambiguous needing investigation** — `ArchidektCacheJobServiceTests.BackgroundService_SucceedsAndUpdatesProcessedCounts` + `GetActiveJob_ReturnsNullAfterCompletedJob` both fail `Assert.NotNull` on background-service job state; smells like race (poll-before-scheduler-runs) but warrants a `/gsd-debug` pass before final verdict. The 2nd test's name-vs-assertion conflict ("ReturnsNullAfterCompletedJob" yet asserts NotNull) strongly suggests genuine test-logic confusion, not just timing — possibly a real `GetActiveJob` lifecycle bug.
**Depends on**: Phase 999.5 (must be closed before this phase starts; this phase reverses 999.5 D-22 deferral).
**Requirements**: D-01..D-XX from `.planning/phases/999.6-v1-3-ship-gate-test-residual-cleanup/999.6-CONTEXT.md` (backlog-style phase — no formal REQ-IDs; CONTEXT.md decisions are the binding contract).
**Success Criteria** (what must be TRUE):

  1. `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -c Release --nologo` exits 0 with `Failed: 0`. No new skips beyond the 3 existing pre-phase skips. No regressions in 999.1-999.5 facts.
  2. The 6 stale tests are either updated to match shipped production behavior (with the rename / message-string baked into the assertion) or deleted with documented rationale per CONTEXT.md decision (the `compare2_page_segment` test has no surviving premise — deletion is on the table).
  3. The flaky `BasicAuthMiddlewareTests.CorrectCredentials_InvokesNext` is stabilized — root cause of full-suite-vs-isolation divergence identified and removed (shared mutable state, env-var contamination, or middleware ordering). 5/5 in both isolation and full-suite. No `Skip` directive workaround.
  4. The 2 Archidekt cache-job failures get a `/gsd-debug` session each (or one shared session) producing a clear verdict: stale test OR real bug. If stale, update assertion. If real bug, open and ship the fix in the same phase (P-fix-archidekt) — production fix takes precedence; no shipping with a known background-service race.
  5. README + STATE.md updated to record ship-gate compliance: v1.3 ready_to_ship status revoked at phase start (was set 2026-05-21 by 999.5 close), restored only when this phase closes with `Failed 0`.
  6. `dotnet build DeckFlow.sln -c Release` exits 0 with warning count <= post-Phase-999.5 baseline.

**Plans:** 3/3 plans complete
Plans:
**Wave 1**

- [x] 999.6-01-PLAN.md — Mechanical stale-test cleanup: PacketArtifactStore msg-string update (D-04) + AiPlatform Theory prefix `compare2`→`comparison` (D-05) + dead-premise compare2_page_segment Fact deletion (D-07) + Arna fixture deletion under D-06 Branch B (both probe paths missing); 4 atomic commits per D-08; full-suite gate moved 9→3 (Total 443 — Branch B). (completed 2026-05-22)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 999.6-02-PLAN.md — BasicAuth flaky-test stabilization: Claude orchestrates /gsd-debug 999.6-basicauth-flaky (D-09 hypothesis priority) then Codex applies fix per verdict (D-10 — test-side reset, fixture redesign, or BasicAuthMiddleware change per D-15); 5/5 focused + 5/5 full-suite verification per D-11; full-suite gate moves 3→2

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 999.6-03-PLAN.md — Archidekt cache-job ambiguous-failure resolution: F-PROD-CONTRACT applied (D-15 fired — real production bug ships inside 999.6). Added `Task<HarvestRunRow?> GetByIdAsync(Guid, CancellationToken)` to `IHarvestRunStore` + production impl in `HarvestRunStore` (provider-specific SQLite-vs-Postgres Guid binding matching three precedents) + rewired `ArchidektCacheJobService.GetJob` to delegate to `GetByIdAsync` + extended inline `FakeHarvestRunStore` with async wrapper. Closes both `Assert.NotNull(service.GetJob(jobId))` failures (lines 118 + 170) with one atomic commit. D-16 NOT fired (test names accurate for F-PROD-CONTRACT branch). 10/10 deterministic PHASE EXIT GATE (focused 5/5 Failed:0/Passed:14/Total:14; full-suite 5/5 Web Failed:0/Passed:440/Skipped:3/Total:443 + Core Failed:0/Passed:57/Total:57). Phase-wide net delta 9 → 0 failures. STATE.md ready_to_ship restoration handed off to /gsd-verify-work 999.6 per D-23 (commits d758609 fix + 1adf65b SUMMARY). (completed 2026-05-22)

### Phase 999.7: v1.3 Audit Cleanup — STATE Arithmetic + WDG Checkboxes + Stale Doc-Comments (INSERTED)

**Goal**: Land the documentation + state-tracking cleanup surfaced by `/gsd-audit-milestone v1.3` (audit report at `.planning/v1.3-MILESTONE-AUDIT.md`) so the milestone archive is consistent with shipped reality. The audit found 0 BLOCKERs, 0 unsatisfied requirements, and all 10 E2E flows wired — but accumulated documentation drift across 3 surfaces blocks a clean `/gsd-complete-milestone v1.3` handoff. Scope: (a) **STATE.md progress arithmetic** — `completed_phases: 9 / total_phases: 11` should be `11/11` (Phase 999.6 closure committed the wrong counter); `completed_plans: 66 / total_plans: 46` is mathematically impossible (66 > 46; user-flagged 2026-05-22); reconcile both counters against the actual 11-phase / 46-plan totals shown in the ROADMAP Progress table rows 391-401. (b) **REQUIREMENTS.md WDG-01..10 checkboxes** — all 10 still `[ ]` despite Phase 11 closure on 2026-05-13 (11-VERIFICATION.md status:passed); flip to `[x]` to match the 22/22 satisfied count in the audit report. (c) **Stale Claude variant doc-comments** — `ClaudeAnalysisPromptVariant.cs:14`, `ClaudeComparisonPromptVariant.cs:11`, `ClaudeMetaGapPromptVariant.cs:13` still describe behavior as "result-wrapped output" though Phase 999.2 stripped the `<result>` wrapper from those variants 2026-05-19 (audit finding F-01); replace with "direct JSON fenced-block output" or equivalent matching the actual emitted prompt body. (d) **Undocumented `/deck-analysis/download` asymmetry** — `DeckController.cs:508-521` lacks the response-json-only short-circuit tier that comparison + cedh-meta-gap implement per Phase 999.3 D-10; this is intentional (deck-analysis has no paste-response-only re-download path) but undocumented (audit finding F-02); add a one-line comment explaining the intentional omission. (e) **999.5-UAT.md frontmatter** — `status: testing` is stale (superseded by Phase 999.6 full-suite gate); update to `status: superseded-by-999.6` for archival clarity (optional polish per audit recommendation 5). Out of scope: WDG-04 deferred onsubmit (already overridden 2026-05-16; v1.4 modal); DeckFlow.Web.csproj NoWarn 1591;1573;1587 deferral (Phase 14 CONTEXT "Deferred Ideas"); Phase 15 SC4 empirical hash gate bypass (user-accepted residual risk); IN-01 _AiSelector vs view-level Normalize Gemini-flag fallback divergence (pre-existing).
**Depends on**: Phase 999.6 (closes the test gate that the audit verified against; this phase reads VERIFICATION.md tables and audit report as inputs).
**Requirements**: D-01..D-XX from `.planning/phases/999.7-v1-3-audit-cleanup/999.7-CONTEXT.md` (backlog-style phase — no formal REQ-IDs; CONTEXT.md decisions are the binding contract).
**Success Criteria** (what must be TRUE):

  1. `.planning/STATE.md` progress block reports `completed_phases: 11`, `total_phases: 11`, `completed_plans: 46`, `total_plans: 46`, `percent: 100` (or whatever reconciled counts match the 11-row v1.3 Progress table in ROADMAP.md lines 391-401).
  2. `.planning/REQUIREMENTS.md` WDG-01 through WDG-10 checkboxes (lines 14-23) all read `[x]`; no other checkbox state changes; traceability table at lines 73-94 untouched.
  3. The 3 Claude variant files (`DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs`, `Comparison/ClaudeComparisonPromptVariant.cs`, `MetaGap/ClaudeMetaGapPromptVariant.cs`) have their class-level `/// <summary>` doc-comments updated to drop "result-wrapped output" and replace with "direct JSON fenced-block output" (or equivalent matching the actual stripped behavior); ClaudeFollowUp + ClaudeSetUpgrade variants left untouched (their summaries don't carry the stale phrase per audit F-01 evidence inventory).
  4. `DeckFlow.Web/Controllers/DeckController.cs` line 514 (or wherever the deck-analysis download handler enters the cache-lookup block) carries a one-line comment documenting the intentional omission of the response-json short-circuit tier — explaining deck-analysis has no paste-response-only re-download path (cited reference: Phase 999.3 D-10 + audit F-02).
  5. `.planning/phases/999.5-v1-3-backlog-catch-up-test-hardening/999.5-UAT.md` frontmatter `status: testing` updated to `status: superseded-by-999.6` (or equivalent terminal label); body untouched (the test results documented in the UAT remain accurate as-of their capture timestamp).
  6. `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Release` exits 0 with warning count <= post-Phase-999.6 baseline (build is documentation-only sweep — should be byte-symmetric in compiled output; doc-comment text changes don't affect IL).
  7. `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -c Release` reports `Failed: 0, Passed: 497 (Web 440 + Core 57), Skipped: 3, Total: 500` (Phase 999.6 PHASE EXIT GATE baseline preserved; no test regressions from doc-comment / Razor-untouched / state-file-only edits).

**Plans:** 4 plans

Plans:

- [ ] 999.7-01-PLAN.md — Reconcile STATE.md progress counters against ROADMAP Progress table (SC1)
- [ ] 999.7-02-PLAN.md — Flip REQUIREMENTS.md WDG-01..10 checkboxes to `[x]` (SC2)
- [ ] 999.7-03-PLAN.md — Update 3 Claude variant class summaries (drop "result-wrapped output"); audit F-01 (SC3, SC6, SC7)
- [ ] 999.7-04-PLAN.md — Add DeckController F-02 asymmetry comment + finalize 999.5-UAT.md status (SC4, SC5, SC6, SC7)

**Cross-cutting constraints:**

- `dotnet test DeckFlow.sln -c Release` reports Failed:0 Passed:497 Skipped:3 Total:500.

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
| 999.3 Packet Download Session Cache | v1.3 | 4/4 | Complete   | 2026-05-21 |
| 999.4 Truncated-JSON Response UX | v1.3 | 1/1 | Complete   | 2026-05-21 |
| 999.5 v1.3 Backlog Catch-up + Test Hardening | v1.3 | 4/4 | Complete   | 2026-05-21 |
| 999.6 v1.3 Ship-Gate Test Residual Cleanup | v1.3 | 3/3 | Complete    | 2026-05-22 |
| 999.7 v1.3 Audit Cleanup — STATE Arithmetic + WDG Checkboxes + Stale Doc-Comments | v1.3 | 0/4 | Not started | — |

---

## Backlog

### edhtop16 Filter Defaults vs DeckFlow Filter Defaults (BACKLOG — unnumbered, was 999.3 before collision with active Packet Download Session Cache phase; renumber when promoted)

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
