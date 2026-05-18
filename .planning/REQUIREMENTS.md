# Milestone v1.3 Requirements — Frontend Hardening + AI-Agnostic Rename + Code Hygiene

**Status:** active
**Milestone:** v1.3
**Started:** 2026-05-13
**Branch:** `v1.3`

## v1.3 Requirements

### Frontend Hardening (Web Design Guidelines audit)

Source: `.planning/quick/260513-wdg-web-design-guidelines-audit-findings/260513-wdg-FINDINGS.md` (10 sweep PRs sequenced by leverage).

- [ ] **WDG-01**: Admin shell renders visible keyboard focus indicators on all interactive elements. `admin.css` includes universal `:focus-visible` block (mirrors `site.css:109-118`) covering `a, button, input, select, textarea, summary, [role="tab"]`. Verified by Tab-navigating every page under `/Admin/*` and observing focus ring.
- [ ] **WDG-02**: Autocomplete suggestions on all typeahead inputs (SuggestCategories card-name, DeckConvert commander, JudgeQuestions card, CommanderCategories, CardLookup single) are keyboard-navigable. `df-typeahead.ts` implements ArrowDown/Up/Enter/Escape, ARIA combobox attributes (`role="combobox"`, `aria-autocomplete="list"`, `aria-expanded`, `aria-controls`, `aria-activedescendant`), and options have `role="option"`.
- [ ] **WDG-03**: ChatGPT-flow / Comparison-flow / CedhMetaGap-flow workflow tablists pre-select the current step on server-render. `_WorkflowStepTabs.cshtml` emits `aria-selected="true" tabindex="0"` for the current step and `aria-selected="false" tabindex="-1"` for the rest. Keyboard users land on the correct tab without JS.
- [ ] **WDG-04**: No inline `style` / `onclick` / `onchange` / `onsubmit` handlers remain in AdminFeedback Detail, AdminFeedback Index, or Error views. CSP `script-src 'self'` and `style-src 'self'` ready.
- [ ] **WDG-05**: Info tooltips on SuggestCategories + CommanderCategories are keyboard- and screen-reader-accessible. `<span class="info-tooltip" title="...">i</span>` converted to `<button aria-describedby>` or `<details><summary>` pattern.
- [ ] **WDG-06**: All admin and result tables expose `<caption>` (visible or `sr-only`) and `<th scope="col">` semantics. Applies to AdminFlags, AdminFeedback Index, AdminHarvest (stats + recent runs + run log), AdminAnalytics, DeckSync, CommanderCategories, CedhMetaGap.
- [ ] **WDG-07**: Razor `selected="@(condition)"` patterns produce valid HTML across DeckSync, DeckConvert, SuggestCategories, AdminHarvest. Switch to `selected="@(condition ? "selected" : null)"` per v1.2 commit `32bf620` lesson — no more `selected="True"` in rendered output.
- [ ] **WDG-08**: Cross-cutting accessibility CSS lives in `site-common.css` so all 22 guild themes inherit without per-fork edit. Added rules: `:root { color-scheme: light dark }`, `@media (prefers-reduced-motion: reduce) { … }` global gate, `button, a, summary { touch-action: manipulation }`, `.tabular { font-variant-numeric: tabular-nums }` utility, `h1, h2, h3, [id] { scroll-margin-top }`.
- [ ] **WDG-09**: `<input type="url">` inputs and user-paste `<textarea>` blocks across all forms have correct attributes. URL inputs gain `autocomplete="url" inputmode="url"` and placeholders ending in `…`. User-paste textareas gain `autocomplete="off"`. Affects DeckSync, DeckConvert, SuggestCategories, AdminHarvest, ChatGPT views.
- [ ] **WDG-10**: AdminHarvest live status region announces state transitions to screen readers. `#harvest-status-live` element has `role="status" aria-live="polite"`, so each AJAX poll update from `admin-harvest.ts:151` is heard by SR users (delivers Phase 7 SC #1/#3 intent).

### AI-Agnostic Rename — URL + page layer

Source: `.planning/AI-AGNOSTIC-RENAME-BRAINSTORM.md` (Option A recommended: drop brand, evergreen URLs, explainer lines).

- [x] **RENAME-01**: Three AI-agnostic page URLs replace `/chatgpt-packets`, `/chatgpt-deck-comparison`, `/chatgpt-cedh-meta-gap`. Permanent 301 redirects from old URLs preserve bookmarks and inbound links. Final URL slugs TBD during planning (candidates: `/deck-analysis`, `/deck-comparison`, `/meta-gap` from brainstorm).
- [x] **RENAME-02**: Page `<h1>`, top-nav labels (`_DeckToolTabs.cshtml`), hub-card titles (`Home.cshtml`), and `<title>` element values reflect AI-agnostic naming. Explainer text under each `<h1>` preserves the "this generates something to paste into an AI" cue.
- [x] **RENAME-03**: Session zip download filenames and `Content-Disposition` headers use new artifact terminology consistent with the page naming. Filename sanitizer in `ChatGptPacketArtifactStore` updated.

### Code Hygiene — ChatGpt* class rename

- [x] **CLASSRENAME-01**: All `ChatGpt*`-prefixed classes renamed to AI-agnostic terms. Targets include `ChatGptDeckRequest`, `ChatGptDeckPacketService`, `ChatGptRequestContextParser`, `ChatGptPacketArtifactStore`, `ChatGptDeckComparisonService`, `ChatGptCedhMetaGapService`, `ChatGptDeckViewModel`, `ChatGptDeckComparisonViewModel`, `ChatGptCedhMetaGapViewModel`, `ChatGptCedhMetaGapRequest`, `ChatGptDeckComparisonRequest`. Final names decided during planning.
- [x] **CLASSRENAME-02**: Every renamed class has an XML `<summary>` doc comment describing its current responsibility. `<GenerateDocumentationFile>true</GenerateDocumentationFile>` (`DeckFlow.Web.csproj`) compiles clean without `NoWarn 1591`-suppressed warnings for the renamed types.
- [x] **CLASSRENAME-03**: DI registrations (`Program.cs`), `[InternalsVisibleTo("DeckFlow.Web.Tests")]` (`AssemblyInfo.cs`), namespace imports, controller actions, view-model bindings, test fixtures, and Razor `@model` directives updated. Zero behavior change.

### Code Hygiene — broader codebase audit

- [x] **AUDIT-01**: All public classes across `DeckFlow.Core`, `DeckFlow.Web`, `DeckFlow.CLI`, `DeckFlow.Core.Tests`, `DeckFlow.Web.Tests` reviewed for name-vs-behavior alignment. Classes whose names don't describe their current behavior are renamed. Examples to verify: `ScryfallTaggerService` (does it just call tagger, or also normalize/cache?), `CommanderSpellbookService` (lookup vs full client?), `Null*`/`Fake*`/`Stub*` test doubles (scoping consistent?).
- [x] **AUDIT-02**: Every public class and interface has an XML `<summary>` doc comment. Missing comments backfilled. `<GenerateDocumentationFile>` clean across `DeckFlow.Web` (already on) and verified across `DeckFlow.Core`, `DeckFlow.CLI`.
- [x] **AUDIT-03**: `dotnet build DeckFlow.sln --configuration Release` produces zero new warnings. Test discovery succeeds (`dotnet test --no-build`) where WSL permits; otherwise verified via push-and-watch CI.

### AiPlatform value object refactor

Source: `.planning/milestones/v1.2-phases/10-claude-gemini-artifact-optimization/10-AISEL-PLATFORM-DESIGN.md`.

- [ ] **AIPLATFORM-01**: `string TargetAiPlatform` property replaced by `AiPlatform` sealed record value object on all three request DTOs (`DeckAnalysisRequest`, `DeckComparisonRequest`, `MetaGapRequest` — final names per CLASSRENAME-01). Value object encapsulates name, display label, enabled flag, response-extraction strategy. OCP forecast: 3/10 → 8/10.
- [ ] **AIPLATFORM-02**: All five per-AI prompt builders (`BuildAnalysisPrompt`, `BuildSetUpgradePrompt`, `BuildComparisonPrompt`, `BuildFollowUpPrompt`, `BuildMetaGapPrompt`), the unified `<result>` extractor in `ExtractJsonPayload`, the artifact store round-trip (`LoadFromZip` / `BuildZip`), and view models switch over to the value-object API. `DECKFLOW_GEMINI_ENABLED` flag still gates Gemini option visibility.
- [ ] **AIPLATFORM-03**: Zero user-visible behavior change. All three ChatGPT pages produce identical artifacts and round-trip identical zips before and after refactor. Verified by re-running manual integration tests T1-T8 + filename verify (full T1-T8 spec in `.planning/milestones/v1.2-MILESTONE-AUDIT.md`).

## Future Requirements (deferred from v1.3 — candidates for v1.4+)

- Gemini paste-limit workaround (split-message prompt OR direct API integration) — kept flag-gated via `DECKFLOW_GEMINI_ENABLED`; needs upstream paste cap raise OR API key strategy.
- v1.1 phase directory archive backfill — `06-admin-shell-flags-foundation`, `07-harvest-controls-stats`, `07.1-categories-feature-flag-sameorigin-ajax-fix`, `08-analytics` still live in `.planning/phases/` instead of `.planning/milestones/v1.1-phases/`. Move to archive in v1.4 cleanup OR as a quick task.

## Out of Scope (explicit exclusions)

- **Harvest debug `harvest-killed-by-suggestion`** — H1 hypothesis parked in `.planning/debug/`. Promoted as v1.3 candidate during 2026-05-13 backlog review but deferred from this milestone scope because root-cause investigation is gating, not parallel-shippable with the other v1.3 work. Kept deferred for now.
- **Visual regression harness for 22 guild themes** — flagged in v1.0 deferred list. Own testing-infra milestone.
- **DeckController god-class split / ChatGPT services extraction** — own refactor milestone. CLASSRENAME-01 + AUDIT-01 may rename DeckController internals but will NOT split the controller.
- **Browser-extension test coverage gap** — manifest-version protocol bumps documented; deferred.
- **Health/ready endpoint + correlation ID middleware** — own observability milestone.
- **Disk-backed Scryfall set cache** — own caching milestone.

## Traceability

(populated by gsd-roadmapper after roadmap creation — 2026-05-13)

| REQ-ID | Phase | Status |
|--------|-------|--------|
| WDG-01 | 11 | active |
| WDG-02 | 11 | active |
| WDG-03 | 11 | active |
| WDG-04 | 11 | active |
| WDG-05 | 11 | active |
| WDG-06 | 11 | active |
| WDG-07 | 11 | active |
| WDG-08 | 11 | active |
| WDG-09 | 11 | active |
| WDG-10 | 11 | active |
| RENAME-01 | 12 | active |
| RENAME-02 | 12 | active |
| RENAME-03 | 12 | active |
| CLASSRENAME-01 | 13 | active |
| CLASSRENAME-02 | 13 | active |
| CLASSRENAME-03 | 13 | active |
| AUDIT-01 | 14 | active |
| AUDIT-02 | 14 | active |
| AUDIT-03 | 14 | active |
| AIPLATFORM-01 | 15 | active |
| AIPLATFORM-02 | 15 | active |
| AIPLATFORM-03 | 15 | active |
