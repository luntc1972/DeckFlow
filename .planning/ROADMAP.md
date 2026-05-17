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
- [ ] **Phase 12: AI-Agnostic URL + Page Rename** — Drop "chatgpt-" from the three multi-AI workflow URLs, swap H1/nav/hub labels, add `.page-lede` explainer lines, ship 301 permanent redirects, update artifact filenames to AI-agnostic terms.
- [ ] **Phase 13: ChatGpt* Class Rename + Summary Doc Comments** — Rename all `ChatGpt*` request/service/viewmodel/parser/store types to AI-agnostic names; backfill XML `<summary>` doc comments on every renamed class; update DI registrations, `InternalsVisibleTo`, namespaces, controller actions, test fixtures, and Razor `@model` directives with zero behavior change.
- [ ] **Phase 14: Broader Codebase Name-vs-Behavior Audit** — Sweep public classes across all 5 projects, rename any whose name doesn't describe current behavior, backfill missing XML `<summary>` doc comments, verify clean Release build with zero new warnings.
- [ ] **Phase 15: AiPlatform Value Object Refactor** — Replace `string TargetAiPlatform` with sealed `AiPlatform` record value object across request DTOs, prompt builders, response extractor, artifact store, and view models; preserve `DECKFLOW_GEMINI_ENABLED` gating; zero user-visible behavior change verified via full T1-T8 manual integration suite.

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

**Plans:** 4/5 plans executed
Plans:
**Wave 1**

- [x] 12-01-PLAN.md — UseRewriter 301 block (9 redirects) + DeckController 12 route attribute replacements
- [x] 12-04-PLAN.md — Suggest*ZipFileName helpers: deckflow-packet→deck-analysis, compare2→comparison, cedh→cedh-meta-gap (chatgpt AI fallback preserved)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 12-02-PLAN.md — git mv 3 view files (ChatGpt*.cshtml → AI-agnostic names) + DeckController View() literal-string updates

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 12-03-PLAN.md — Page-1 H1/title/nav/hub label swap + 3 page-lede explainer paragraphs + .page-lede CSS in site-common.css + 6 hrefs across nav and home

**Wave 4** *(blocked on Wave 3 completion)*

- [ ] 12-05-PLAN.md — README + Help/*.md URL sweep + browser-extension verification + manifest version bump (conditional) + phase-wide D-15 grep gate

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

**Plans**: TBD

### Phase 14: Broader Codebase Name-vs-Behavior Audit

**Goal**: Use the Phase 13 rename pass as a template to sweep the rest of the codebase for classes whose names no longer describe their current behavior, and backfill missing `<summary>` doc comments across `DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.CLI`, and both test projects.
**Depends on**: Phase 13 (uses class-rename pattern as template; runs after the largest rename surface has stabilized).
**Requirements**: AUDIT-01, AUDIT-02, AUDIT-03
**Success Criteria** (what must be TRUE):

  1. Every public class in `DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.CLI`, `DeckFlow.Core.Tests`, and `DeckFlow.Web.Tests` has been reviewed for name-vs-behavior alignment; classes whose names don't describe current responsibility are renamed (candidates to verify per REQUIREMENTS.md: `ScryfallTaggerService`, `CommanderSpellbookService`, `Null*`/`Fake*`/`Stub*` test-double scoping consistency).
  2. Every public class and interface across all 5 projects has an XML `<summary>` doc comment; `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is verified clean (or newly enabled) on `DeckFlow.Core` and `DeckFlow.CLI` in addition to the already-on `DeckFlow.Web`.
  3. `dotnet build DeckFlow.sln --configuration Release` produces zero new warnings vs. the pre-Phase-14 baseline; test discovery succeeds via `dotnet test --no-build` where WSL permits, otherwise verified via push-and-watch CI on the `v1.3` branch.
  4. Scope discipline observed: DeckController god-class split and ChatGPT-services extraction stay out of scope per PROJECT.md (own refactor milestones); renames touch class names + doc comments only, no responsibility splits.

**Plans**: TBD

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

**Plans**: TBD

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
| 12. AI-Agnostic URL + Page Rename | v1.3 | 4/5 | In Progress|  |
| 13. ChatGpt* Class Rename + Doc Comments | v1.3 | 0/3 | Not started | — |
| 14. Broader Codebase Name-vs-Behavior Audit | v1.3 | 0/3 | Not started | — |
| 15. AiPlatform Value Object Refactor | v1.3 | 0/3 | Not started | — |

---

*v1.0 shipped 2026-05-02 | v1.1 shipped 2026-05-08 | v1.2 shipped 2026-05-13 | v1.3 started 2026-05-13*
