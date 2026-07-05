# Phase 85: `chatgpt-*` Naming Cleanup — Research

**Researched:** 2026-07-05
**Domain:** Repo-wide identifier rename (CSS classes, `data-*` attributes, TS symbols, C# types) with a byte-identical/behavior-neutral gate
**Confidence:** HIGH (all counts and classifications below are `[VERIFIED: grep/read against working tree]`, not training-data guesses)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D1 — Naming convention**
- Kebab identifiers: `chatgpt-<stem>` → `prompt-<stem>` (stem preserved: `chatgpt-packets-form`
  → `prompt-packets-form`, `chatgpt-step-tab` → `prompt-step-tab`, `data-chatgpt-print`
  → `data-prompt-print`).
- camelCase/PascalCase code symbols: `ChatGpt` → `Prompt` (`ChatGptUiMode` → `PromptUiMode`,
  `ChatGptDeckPacketService` → `PromptDeckPacketService`, `IChatGptDeckPacketService` →
  `IPromptDeckPacketService`). File names follow the type (one public type per file => rename
  the `.cs`/`.ts` file too where the type name drives the filename).

**D2 — Scope = EVERYTHING (kebab + camelCase TS + C#)**
User explicitly chose the broadest scope. AICLEAN-03's grep `chatgpt-*` → 0 covers the kebab
set; the camelCase/C# `ChatGpt*` renames go beyond that grep but are in scope by user decision.
Extend the grep-clean acceptance to also assert zero `ChatGpt` (any case) in `css/`, `ts/`,
`Views/`, AND the `.cs` sources — EXCEPT the D3 keep-list below.

**D3 — CRITICAL: do NOT rename genuine ChatGPT-MODEL references (keep-list)**
The prompt tool targets a ChatGPT / Claude / Gemini trio, and the three prompt variants are
intentionally decoupled/duplicated (ADR-0001). Any `ChatGpt*` symbol, class, attribute VALUE,
label, or copy that genuinely denotes the ChatGPT model as one of that trio MUST be kept. The
researcher classifies every occurrence as (a) generic-branding → rename or (b) ChatGPT-model-
variant → KEEP. When in doubt, KEEP and flag.

**D4 — Byte-identical render + green everything**
Rendered HTML/CSS byte-identical (pure identifier swap only). Full Playwright e2e suite
unchanged/green; `dotnet build DeckFlow.sln` 0/0; all xUnit tests pass. Acceptance grep: zero
`chatgpt-*` in css/ts/Views; zero `ChatGpt`/`chatgpt` outside the D3 keep-list across
css/ts/Views/*.cs.

**D5 — Attribute/string VALUES that are behavioral CONTRACTS**
`data-cache-key="chatgpt-packets|chatgpt-deck-comparison|chatgpt-cedh-meta-gap"`,
`data-sync-panel="chatgpt-deck-url|chatgpt-deck-text"`, sessionStorage/localStorage keys,
download filenames, and any client↔server key MUST be renamed in lockstep on both sides, or
explicitly KEPT if they cross into persisted/prod/cross-tool state a rename would break.

**D6 — Format & commit discipline**
Changed-lines-only, LF endings, no unrelated reflow. Commit per logical group (CSS forks,
shared CSS, TS, C# service, C# tests, views). Plain default-author commits, no Co-Authored-By.
Update README only if user-facing behavior/text changes (a pure rename should not).

### Claude's Discretion
- Plan wave/sequencing (interface-first vs. CSS-first).
- Whether to capture a pre-rename computed-style / rendered-HTML baseline (à la Phase 84 Task 0).
- How to prove "byte-identical render" concretely.

### Deferred Ideas (OUT OF SCOPE)
- Any functional, behavioral, layout, or COLOR change (byte-identical only).
- Typography / `font-size` → `var(--fs-*)` migration → Phase 86.
- Genuine ChatGPT-model-variant identifiers (D3 keep-list) — intentionally retained.
- UI-SPEC design contract — N/A, this phase runs `--skip-ui`.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AICLEAN-01 | All `chatgpt-*` CSS class names renamed across 25 theme forks + `site-common.css` + `site.css`, rendered output byte-identical | Full CSS class-stem inventory below (§ CSS Inventory); byte-identical proof strategy reuses Phase 84's `theme-baseline-pre84.json` pattern (§ Byte-Identical Proof Strategy) |
| AICLEAN-02 | Matching `chatgpt-*` TS constants, `data-*` attributes, Razor view refs renamed in lockstep; no dead/duplicated selectors | Full TS symbol + `data-*` attribute inventory (§ TS Inventory); cross-file coupling hazards enumerated (§ Ordering / Coupling Hazards) including the `moxfield-extension-bridge.ts` cache-key duplication Phase 82 deliberately left un-renamed |
| AICLEAN-03 | Zero `chatgpt-*` in `css/`/`ts/`/`Views/` (grep-clean); page render + Playwright e2e unchanged | Validation Architecture section gives exact grep commands + the 5 e2e spec files that assert `chatgpt-*` selectors and must be updated in the same wave |
</phase_requirements>

## Summary

This phase is **larger and more nuanced than 85-CONTEXT.md's inventory implies**. Verified
counts (case-insensitive `chatgpt`, current working tree): **CSS 1,555 occurrences / 25 files**
(1,553 strict kebab `chatgpt-`), **TS 268 occurrences / 4 files** (72 kebab + ~146 camelCase,
overwhelmingly in `deck-sync.ts`), **Razor Views 329 occurrences / 15 files** (not 8 as
CONTEXT.md's "Specific Ideas" section states — 7 additional view files were found:
`Bracket.cshtml`, `Home.cshtml`, `About/Index.cshtml`, `Help/Index.cshtml`,
`ContentKb/Index.cshtml`, `_AiSelector.cshtml`, `_Layout.cshtml`), and **C# 368 occurrences /
88 files** (38 production + 50 test — not "38 refs across ~14 files" as CONTEXT.md states; that
number appears to have conflated the production-file count with the reference count).

**The single highest-value finding (D3 crux):** the C# surface is dominated by genuine
ChatGPT-model-trio references that **must be kept**, not renamed. Seven `ChatGpt{Domain}Prompt-
Variant` classes exist (Analysis/Bracket/Comparison/FollowUp/MetaGap/Primer/SetUpgrade), each
with a confirmed sibling `Claude{Domain}PromptVariant` and `Gemini{Domain}PromptVariant` in the
same directory, each DI-registered by name. `AiPlatform.ChatGpt` (Key `"ChatGPT"`) is the
model's canonical enum-like value. A zip-artifact filename family
(`"30-primer-chatgpt-prompt.txt"` alongside `"...-claude-..."`/`"...-gemini-..."`) and a
download-filename platform segment (`CreateSafePathSegment(targetAiPlatform, "chatgpt")`) are
both genuine per-platform values, not generic branding. **Renaming any of these would be wrong
and would collapse the exact three-way distinction ADR-0001 protects.** After excluding this
keep-list, the actual C# rename surface shrinks to roughly a dozen files (one real symbol —
`ChatGptSwapPrompt` — plus prose/test-assertion cleanup), a small fraction of the 88 files the
raw grep touches.

**Second finding requiring correction:** 85-CONTEXT.md's `<canonical_refs>` section names
`IChatGptDeckPacketService`/`ChatGptDeckPacketService` as "the prompt-packet builder." **This
type does not exist in the current codebase** — `grep -rn "ChatGptDeckPacketService"` across all
`.cs` files returns zero hits. Phase 83 (PKTSVC-01..04, completed 2026-07-04, one phase before
this one) split whatever service this referred to into four services with no "ChatGpt" in their
names: `DeckAnalysisPacketService`, `DeckComparisonService`, `MetaGapService`,
`DeckPrimerPacketService`. This canonical reference is stale pre-Phase-83 context and should be
dropped from the plan; there is no `*DeckPacketService` symbol to rename.

**Primary recommendation:** Plan this as (1) a CSS-class-family + `data-*` attribute rename wave
across CSS/TS/Views/e2e-specs, executed together per D5's lockstep requirement, using a KEEP-LIST
gate on every C#/prose match before touching it, (2) a small, separate C# wave for the one real
symbol rename (`ChatGptSwapPrompt`) plus prose cleanup, and (3) reuse the Phase 84
computed-style/rendered-HTML baseline pattern (headless Playwright snapshot before/after) as the
byte-identical proof, since this phase touches the exact same 25 theme-fork files Phase 84 just
finished auditing.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| CSS class naming (`.chatgpt-*` → `.prompt-*`) | Browser / Client (CSS) | Frontend Server (Razor emits the class attribute) | Pure presentation identifier; Razor views are the only server-side surface that emits the class names, so both must move together |
| `data-*` attribute naming | Browser / Client (TS reads via `dataset`/`querySelector`) | Frontend Server (Razor emits the attribute) | TS is the sole consumer; Razor is the sole producer — classic two-sided client/markup contract, no backend involvement |
| sessionStorage cache-key VALUES (`chatgpt-packets` etc.) | Browser / Client | — | Entirely client-side (TS reads/writes sessionStorage keyed by the Razor-emitted `data-cache-key` value); no server persistence, no cross-session concern |
| `ChatGpt{Domain}PromptVariant` classes | API / Backend | — | Server-side prompt-text generation, DI-registered; **KEEP, not in scope** — genuine per-platform business logic |
| Zip artifact filenames (`30-primer-chatgpt-prompt.txt`, download-filename platform segment) | API / Backend | Browser / Client (JS reads `X-DeckFlow-Filename` header to name the saved blob) | Server generates and later re-parses these on upload (round-trip); **KEEP, not in scope** |
| `ChatGptSwapPrompt` (Manabase) | API / Backend (record property) | Frontend Server (Razor renders it) | Single-platform generic field mislabeled with model branding; in scope for rename, small blast radius (3 prod files + 1 test file + 1 CLI + 1 Razor view) |

## Standard Stack

No new libraries. This phase is a pure identifier rename executed with the project's existing
toolchain: `dotnet build`, xUnit (`DeckFlow.Web.Tests`), Playwright (`DeckFlow.Web/e2e`), Vitest
(`DeckFlow.Web/ts-tests`), and a one-off Node + `playwright-core` scratch script for the
before/after render snapshot (same pattern Phase 84 used, not a tracked dependency).

## CSS Inventory (AICLEAN-01)

**Verified counts** `[VERIFIED: grep against DeckFlow.Web/wwwroot/css/*.css]`:

| Metric | Count |
|--------|-------|
| Total case-insensitive `chatgpt` occurrences | 1,555 |
| Strict kebab `chatgpt-` occurrences | 1,553 |
| Files containing `chatgpt` text | 25 of 27 theme CSS files |
| Theme files with **zero** occurrences (cascade-only, inherit via `@import`) | `site-boros.css`, `site-izzet.css` |
| Admin CSS files (`admin.css`, `admin-common.css`, `admin-mobile.css`) | 0 (confirmed out of scope) |

**Per-file counts** (any-case): `site-abzan.css` 111, `site-bant.css` 103, `site-esper.css` 103,
`site-grixis.css` 103, `site-jeskai.css` 103, `site-jund.css` 107, `site-mardu.css` 108,
`site-naya.css` 105, `site-nyx.css` 103, `site-planeswalker-dark.css` 103, `site-sultai.css` 107,
`site-commander-table.css` 103, `site.css` 106, `site-common.css` 92, `site-azorius.css` 15,
`site-rakdos.css` 15, `site-mobile.css` 19, `site-gruul.css` 11, `site-orzhov.css` 9,
`site-temur.css` 7, `site-simic.css` 6, `site-dimir.css` 4, `site-golgari.css` 4,
`site-selesnya.css` 4, `site-theme-overrides.css` 4.

**Distinct class stems** (39, all `[VERIFIED: grep -ohE]`, RENAME per D1 — mechanical
`chatgpt-<stem>` → `prompt-<stem>`, no ambiguity, all are generic prompt-tool UI chrome, not
tied to a specific AI model):

```
chatgpt-card-specific-field   chatgpt-context-note          chatgpt-global-note
chatgpt-helper-panel          chatgpt-import-panel          chatgpt-instructions
chatgpt-layout-picker         chatgpt-layout-segment        chatgpt-packets-form
chatgpt-page-toolbar          chatgpt-print-button          chatgpt-question-bucket(s)
chatgpt-question-option(--disabled)  chatgpt-question-picker
chatgpt-score  chatgpt-score-band(--N)  chatgpt-score-card  chatgpt-score-crosscheck(--agree|--diverge)(__label)
chatgpt-score-grid  chatgpt-score-label  chatgpt-score-meter  chatgpt-score-pip(--filled)
chatgpt-score-rationale  chatgpt-score-value  chatgpt-score__eyebrow
chatgpt-step-actions  chatgpt-step-badge  chatgpt-step-eyebrow  chatgpt-step-footer
chatgpt-step-heading  chatgpt-step-nav  chatgpt-step-note  chatgpt-step-panel
chatgpt-step-tab(.is-complete/(__num)(__label))
chatgpt-sticky-download(__label)(__button)   chatgpt-workflow-setup   chatgpt-resume
```

**Distinct CSS attribute selectors** (4, `[VERIFIED]`, all RENAME):
`[data-chatgpt-ui-mode]`, `[data-chatgpt-ui-mode-button]`,
`[data-chatgpt-cedh-reference-checkbox]`, `table[data-chatgpt-cedh-reference-table]` (used for
a `:has(...)` selector at `site-common.css:1222-1223`, one of Phase 84's 3 swapped
cta-border affordance sites — **coordinate**: Phase 84 already touched this exact selector text
for a `var()` value swap; Phase 85 only renames the attribute name, not the declaration — no
conflict, but the planner should diff carefully since the same line was edited by both phases).

**Verified confirmation the CSS-only classification (score family) is generic branding, not
model-specific:** `DeckAnalysisScoreViewTests.cs` doc comment confirms `.chatgpt-score*` renders
a "Multi-Axis Deck Score" computed **locally** from decklist signals (Power/Speed/Control/
Consistency bands + a bracket cross-check), entirely independent of which AI platform the user
targets. "CROSS-CHECK" refers to the local score vs. the local bracket classification, not a
ChatGPT-vs-Claude comparison. RENAME confirmed correct.

## TS Inventory (AICLEAN-02)

**Verified counts** `[VERIFIED: grep against DeckFlow.Web/wwwroot/ts/*.ts]`:

| File | Kebab (`chatgpt-`) | camelCase/PascalCase (`ChatGpt`) | Disposition |
|------|---------------------|-----------------------------------|--------------|
| `deck-sync.ts` | 63 | ~146 | RENAME (bulk of the work) |
| `moxfield-extension-bridge.ts` | 8 | 0 | RENAME, **but see hazard below** |
| `busy-indicator.ts` | 1 | 0 | The 1 hit is a self-referential comment ("fully chatgpt-*-free"), not an identifier — reword, see Pitfalls |
| `content-kb.ts` | 0 | 0 | 1 any-case hit is UI copy `'Copy for ChatGPT'` (prose fallback button text) — KEEP+FLAG, see Prose classification |

**Distinct `data-chatgpt-*` attribute names** (48, `[VERIFIED]`, all RENAME, mechanical prefix
swap):

```
data-chatgpt-cedh-current-step        data-chatgpt-cedh-form                 data-chatgpt-cedh-mobile-sort(-dir|-select)
data-chatgpt-cedh-next-step           data-chatgpt-cedh-page(-nav|-size|-status)   data-chatgpt-cedh-pagination
data-chatgpt-cedh-reference-checkbox  data-chatgpt-cedh-reference-row        data-chatgpt-cedh-reference-table
data-chatgpt-cedh-result-anchor       data-chatgpt-cedh-show-step             data-chatgpt-cedh-sort(-type)
data-chatgpt-cedh-step                data-chatgpt-cedh-submit-step           data-chatgpt-cedh-validation-error
data-chatgpt-cedh-workflow-step       data-chatgpt-comparison-current-step    data-chatgpt-comparison-form
data-chatgpt-comparison-next-step     data-chatgpt-comparison-result-anchor   data-chatgpt-comparison-show-step
data-chatgpt-comparison-step          data-chatgpt-comparison-submit-step     data-chatgpt-comparison-validation-error
data-chatgpt-comparison-workflow-step data-chatgpt-current-step               data-chatgpt-download-submit
data-chatgpt-next-step                data-chatgpt-packets-form               data-chatgpt-print
data-chatgpt-result-anchor            data-chatgpt-resume                     data-chatgpt-setup-panel
data-chatgpt-show-step                data-chatgpt-step                      data-chatgpt-submit-step
data-chatgpt-ui-mode(-button|-picker) data-chatgpt-upload-submit              data-chatgpt-validation-error
data-chatgpt-workflow-step            data-chatgpt-zip-upload
```

**Distinct camelCase/PascalCase TS symbols** (38, `[VERIFIED]`, all in `deck-sync.ts`, all
RENAME — mechanical `ChatGpt` → `Prompt`):

```
ChatGptUiMode (type)               applyChatGptCedhSort            applyChatGptUiMode
attachChatGptCedhWorkflow          attachChatGptComparisonWorkflow attachChatGptPacketsWorkflow
chatGptUiModeStorageKey            clearChatGptPacketsState        getDefaultChatGptUiMode
maxChatGptCedhReferences           mobileChatGptUiModeQuery        parseChatGptCedhPage
parseChatGptCedhSortValue          parseChatGptCedhStep            parseChatGptComparisonStep
parseChatGptDownloadFilename       parseChatGptStep                parseChatGptUiMode
registerChatGptDownloadHandler     registerChatGptPrintHandler     scrollChatGptCedhResults
scrollChatGptComparisonResults     scrollChatGptResults            setChatGptCedhValidationMessage
setChatGptComparisonValidationMessage  setChatGptValidationMessage
showChatGptCedhReferencePage       showChatGptCedhStep             showChatGptComparisonStep
showChatGptStep                    sortChatGptCedhFromHeader       sortChatGptCedhFromMobileControl
syncChatGptCedhCheckboxState       triggerChatGptBlobDownload      validateChatGptCedhStep
validateChatGptComparisonStep      validateChatGptPacketsStep      wireChatGptZipUpload
```

**`CHATGPT_DOWNLOAD_FALLBACK_FILENAME`** (`deck-sync.ts:337`) — the **identifier** should rename
to `PROMPT_DOWNLOAD_FALLBACK_FILENAME`; its **value** is the literal string `'session.zip'`
(contains no "chatgpt" text) — zero contract risk.

## Views Inventory (AICLEAN-02)

**Verified: 15 files contain `chatgpt` (any case), not 8 as CONTEXT.md's inventory states.**
`[VERIFIED: grep -ril against DeckFlow.Web/Views]`. Counts (any-case):

| File | Count | Disposition |
|------|-------|--------------|
| `Deck/DeckAnalysis.cshtml` | 139 | RENAME (classes + `data-*` attrs) |
| `Deck/CedhMetaGap.cshtml` | 77 | RENAME |
| `Deck/DeckComparison.cshtml` | 58 | RENAME |
| `Deck/DeckPrimer.cshtml` | 16 | Mixed — `.chatgpt-sticky-download`/`.chatgpt-step-*`/`.chatgpt-resume` classes RENAME; 2 prose lines (`ViewData["Description"]`, page-lede "for ChatGPT, Claude, or Gemini") KEEP |
| `Deck/JudgeQuestions.cshtml` | 9 | **100% prose, no identifiers** — KEEP+FLAG (see Ambiguous Prose below) |
| `Deck/Manabase.cshtml` | 7 | Mixed — `.chatgpt-print-button`/`data-chatgpt-print` RENAME; `Model.ChatGptSwapPrompt` binding + `manabase-chatgpt-output` id + "Copy this prompt for ChatGPT / Claude" prose — KEEP+FLAG (tied to `ChatGptSwapPrompt` symbol decision) |
| `Shared/_AiSelector.cshtml` | 5 (actually 4 literal "ChatGPT" hits) | 100% KEEP — model-selector default/comment values |
| `Shared/_WorkflowStepTabs.cshtml` | 5 | RENAME — `.chatgpt-step-nav`/`.chatgpt-step-tab(.is-complete)`/`__num`/`__label`, shared partial reused by DeckAnalysis/DeckComparison/CedhMetaGap/DeckPrimer |
| `ContentKb/Detail.cshtml` | 4 | Mixed — `.chatgpt-sticky-download` class RENAME; "paste into ChatGPT, Claude, or Gemini" + "Copy this ChatGPT-ready prompt" + "Copy prompt for ChatGPT" prose — KEEP+FLAG (page's own lede admits multi-platform, singling out ChatGPT in the button copy looks like drift) |
| `Shared/_Layout.cshtml` | 2 | KEEP — SEO meta description prose |
| `Deck/Home.cshtml` | 2 | KEEP — hub-page prose |
| `About/Index.cshtml` | 1 | KEEP — meta description prose |
| `Help/Index.cshtml` | 1 | KEEP — meta description prose |
| `ContentKb/Index.cshtml` | 1 | KEEP — page-lede prose |
| `Deck/Bracket.cshtml` | 1 | KEEP — "Copy this prompt for ChatGPT / Claude / Gemini" prose (this page DOES have `_AiSelector`, confirmed trio support) |
| `Shared/_FormError.cshtml` | 1 | RENAME — doc-comment example value `"chatgpt-validation-error"` should track the attribute-name rename |

Strict kebab-only count across all Views: **291** (matches ROADMAP's "249 views" framing is
also stale — 291 is the verified strict-kebab count; 329 is the any-case total including prose).

## C# Inventory (AICLEAN-02/03 blast radius)

**Verified: 368 case-insensitive `chatgpt` occurrences across 88 `.cs` files (38 production +
50 test)** `[VERIFIED: grep -ril across DeckFlow.Core, DeckFlow.Web, DeckFlow.CLI, and both
test projects]` — **not "38 refs across ~14 files" as 85-CONTEXT.md states.** The 38 in
CONTEXT.md appears to be the production-file count mistaken for a reference count.

### KEEP-LIST (D3) — genuine ChatGPT-model references, do NOT rename

| Item | Evidence | Why KEEP |
|------|----------|----------|
| `AiPlatform.ChatGpt` static field, `Key: "ChatGPT"` | `DeckFlow.Web/Models/AiPlatform.cs:14-17` | Canonical enum-like source of truth for the 3-platform trio (`All = [ChatGpt, Claude, Gemini]`, `Default => ChatGpt`) |
| 7× `ChatGpt{Domain}PromptVariant` classes | `Services/PromptBuilders/{Analysis,Bracket,Comparison,FollowUp,MetaGap,Primer,SetUpgrade}/ChatGpt*PromptVariant.cs` | Each has a **confirmed sibling** `Claude*PromptVariant` + `Gemini*PromptVariant` in the same folder (verified via `find`) — this is the literal D3 trio |
| DI registrations of the above | `Extensions/PromptVariantServiceCollectionExtensions.cs:32-57` (`services.AddSingleton<IAnalysisPromptVariant, ChatGptAnalysisPromptVariant>()` × 7) | Registers the genuine model implementation, not a generic name |
| `"30-primer-chatgpt-prompt.txt"` zip-entry filename | `Services/Persistence/PacketArtifactStore.cs:84,248,255,504` | Sibling `"30-primer-claude-prompt.txt"` / `"...-gemini-..."` confirmed at lines 85-86, 249-250, 505-506 — genuine per-platform round-trip artifact segment inside downloadable/re-uploadable session zips |
| `CreateSafePathSegment(targetAiPlatform, "chatgpt")` default | `PacketArtifactStore.cs:736,739,742,751` (4 `Suggest*ZipFileName` methods) | Fallback platform segment for the download filename when `targetAiPlatform` is null; matches `AiPlatform.Default.Key` semantics; test `SuggestPacketZipFileName_falls_back_to_chatgpt_when_platform_null` (`AiPlatformPhase10RoundTripTests.cs:730-733`) explicitly locks this behavior |
| `"ChatGPT"` string-literal defaults for `TargetAiPlatform` | `DeckAnalysisRequest.cs:20`, `DeckComparisonRequest.cs:15`, `MetaGapRequest.cs:11`, `DeckPrimerRequest.cs:13`, `BracketRequest.cs:26`, `Api/AnalysisPromptApiController.cs:110`, `Controllers/DeckPrimerController.cs:154` | Genuine model-key default values, not identifiers to rename |
| All xUnit tests asserting trio behavior | `AiPlatformPhase10RoundTripTests.cs`, `AiPlatformExtensionTests.cs`, `PacketByteIdentityFixtures.cs` (`ChatGpt = "ChatGPT"` const), `ResultContractTests.cs`, `ScryfallLookupGuardTests.cs`, `GeminiVariantSizeTests.cs`, `PrimerPromptVariantTests.cs`, `Bracket/BracketPromptVariantParityTests.cs`, `Bracket/BracketClassificationServiceTests.cs`, `*ByteIdentityTests.cs` (DeckAnalysis/Comparison/MetaGap/Primer) + their `*Goldens.cs` fixture files, `InteractionAudit*Tests.cs`, `WinConMap*Tests.cs`, `AnalysisScorePromptParityTests.cs`, `AnalysisPromptVariantNoExpertContextTests.cs`, `DeckAnalysisPostFlagIdentityTests.cs`, `DeckPrimerResultRoundTripTests.cs` | Test names/constants/assertions verifying genuine per-platform behavior — renaming would either break the test or silently stop testing what it claims to test |
| `_AiSelector.cshtml` radio values `"ChatGPT"`/`"Claude"`/`"Gemini"` | Full file read | Model-selector literal values |
| Prose in doc comments naming the actual product where the tool has a real trio | `JsonTextFormatterService.cs:14,43,60`, `RequestContextParser.cs:343`, `Services/Bracket/IBracketClassificationService.cs:19`, `Configuration/AiPlatformOptions.cs:4-5`, `Services/DeckComparisonService.cs:299,612`, `Services/MetaGapService.cs:349`, `Services/DeckAnalysisPacketService.cs:2131` | Comments/default-value args describing genuine per-platform behavior for tools confirmed to have the trio (Analysis/Comparison/MetaGap/Bracket) |

### RENAME (unambiguous, D1 mechanical rule)

| Item | File(s) | Note |
|------|---------|------|
| `ChatGptSwapPrompt` record/property | `Services/Manabase/ManabaseAnalysisService.cs:74,84`, `Models/ManabaseViewModel.cs:33`, `Controllers/ManabaseController.cs:95` | See "Ambiguous — KEEP+FLAG" below; research recommends RENAME but flags for sign-off since it changes user-visible copy too |
| `result.ChatGptSwapPrompt` test references (×10) | `DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs` | Must track the property rename |
| CLI console header `"--- ChatGPT swap prompt ---"` | `DeckFlow.CLI/ManabaseCommandRunner.cs:139` | Paired with the property rename |
| Test-fixture slug `"chatgpt-analysis"` | `DeckFlow.Web.Tests/HelpContentServiceTests.cs:47,51,54`, `HelpControllerTests.cs:56,59` | Arbitrary synthetic test data (not a real Help topic — no `chatgpt-analysis.md` file exists in `DeckFlow.Web/Help/`), trivial/no user impact |
| Test assertions of renamed CSS classes/attrs | `DeckAnalysisScoreViewTests.cs` (`"chatgpt-score"`, `"chatgpt-score-grid"`, `"chatgpt-score-crosscheck"`), `DeckComparisonPrintButtonViewTests.cs:34`, `DeckAnalysisPrintButtonViewTests.cs:37` (both `PrintButtonHook = "data-chatgpt-print"`), `MetaGapViewRenderTests.cs:60,75` | Must move in lockstep with the CSS/View rename in the same commit/wave, or these tests silently start asserting the WRONG (pre-rename) string and pass for the wrong reason if the source no longer contains it — actually they'd FAIL correctly (string not found), so this is safe-fail, but still must be updated |
| Prose using "ChatGPT" as generic branding shorthand where no per-platform variant exists server-side | `FeatureFlagCatalog.cs:30` ("the ChatGPT deck-analysis prompt packet" — but Deck Analysis DOES have a trio; this is describing the FEATURE by its legacy name, not a technical claim about single-platform behavior — recommend rename to "AI deck-analysis prompt packet"), `Tools/ToolRegistry.cs:19,22` (Deck Primer + Judge Questions nav-card descriptions), `Controllers/DeckPacketController.cs:151` ("Processes a ChatGPT workflow postback" — internal doc comment, Deck Analysis has the trio, this is legacy naming), `Services/DeckAnalysisPacketService.cs:1005,1815` (doc comments) | Comment/description cleanup; `FeatureFlagCatalog.cs` and `ToolRegistry.cs` descriptions may render on an admin/hub page — verify at implementation time whether they are user-visible (if so, treat as a copy change, same caution as the KEEP+FLAG items) |

### AMBIGUOUS — KEEP+FLAG (needs planner/human sign-off, not mechanically obvious)

These are **single-platform-branded features with no per-variant class and no `_AiSelector`
partial** — i.e., they look exactly like the CSS/TS generic-branding pattern (RENAME), but
unlike those, they are **user-visible copy**, not just an internal class name, so a rename
changes what the user reads, not just what selects an element.

1. **Manabase `ChatGptSwapPrompt`** — `ManabaseController.cs`/`ManabaseAnalysisService.cs`/
   `ManabaseViewModel.cs` produce exactly ONE swap-prompt string (verified: no
   `ClaudeSwapPrompt`/`GeminiSwapPrompt` sibling exists, and `Manabase.cshtml` has no
   `_AiSelector` partial). The view renders `"Want specific land swaps? Copy this prompt for
   ChatGPT / Claude"` (`Manabase.cshtml:625` — note: mentions only 2 of 3 platforms, omits
   Gemini) into a `manabase-chatgpt-output` textarea. **Research recommendation:** rename the
   symbol/id/class to generic `Prompt*` naming (consistent with the rest of the app, and the
   prompt text itself is plain, not ChatGPT-format-specific) but flag for sign-off because the
   button copy is user-visible and currently inconsistent (names 2 of 3 platforms). Zero
   automated test locks the literal copy text, so a rename is low regression risk either way.
2. **JudgeQuestions "ChatGPT prompt generator"** — `JudgeQuestionsController.cs`/
   `JudgeQuestionViewModel.cs`/`JudgeQuestions.cshtml` are **100% prose, zero CSS/attribute
   identifiers**. Verified: no server-side per-platform prompt builder exists for this feature
   (the controller is a two-line `View()` call with no service dependency) — the "prompt" is
   built entirely client-side as plain text. This looks like the same generic-branding pattern
   as Manabase. **Research recommendation:** genericize ("AI prompt generator" / "Prompt for
   your AI") but flag as a user-visible copy decision, not a mechanical identifier rename.
3. **`Core/ManabaseReportTextBuilder.cs:8`** doc comment: "...dropped directly into ChatGPT or
   Claude without any reformatting" (omits Gemini) — stale/incomplete trio mention, zero
   runtime impact either way (doc comment only).
4. **`ContentKb/Detail.cshtml` + `content-kb.ts:128`** — page lede says "paste into ChatGPT,
   Claude, or Gemini" but the copy-button default text says `'Copy for ChatGPT'` / aria-label
   `"Copy this ChatGPT-ready prompt"` / visible text `"Copy prompt for ChatGPT"` — internally
   inconsistent (KEEP-worthy lede vs. RENAME-looking button copy). The `.chatgpt-sticky-download`
   **CSS class** is unambiguously RENAME (generic sticky-bar component name); the **button text**
   is flagged for product-copy sign-off separately.

## D5 — Contract Values (client↔server / persisted keys)

| Value | Where read | Where written | Persistence | Disposition |
|-------|-----------|----------------|-------------|--------------|
| `data-cache-key="chatgpt-packets"` | `deck-sync.ts:919,1159`, `moxfield-extension-bridge.ts:240` | `DeckAnalysis.cshtml:91` | `sessionStorage` only (`[VERIFIED: deck-sync.ts:463-468 storageAvailable = window.sessionStorage]`) — **not** localStorage, **not** cross-tool (unlike Phase 74's cross-tool deck-input persistence) | RENAME in lockstep across all 3 files — same commit/wave. Existing cached entries under the old key are orphaned (self-healing, cleared on next tab close), not a compat break |
| `data-cache-key="chatgpt-deck-comparison"` | `deck-sync.ts` (generic path via `attachGenericPersistedForms`), `moxfield-extension-bridge.ts:249` | `DeckComparison.cshtml:173` | sessionStorage | Same — RENAME in lockstep |
| `data-cache-key="chatgpt-cedh-meta-gap"` | `moxfield-extension-bridge.ts:256` | `CedhMetaGap.cshtml:42` | sessionStorage | Same — RENAME in lockstep |
| `data-sync-panel="chatgpt-deck-url"` / `"chatgpt-deck-text"` | `deck-sync.ts:72-73` | `DeckAnalysis.cshtml:163,169` | None (pure DOM toggle, no storage) | RENAME in lockstep, lowest risk of the D5 set |
| `decksync-chatgpt-ui-mode` (sessionStorage key literal) | `deck-sync.ts:1190` (`chatGptUiModeStorageKey`) | same file (read+write) | sessionStorage | RENAME — single-file, no cross-file coupling |
| `"30-primer-chatgpt-prompt.txt"` (zip entry name) | `PacketArtifactStore.cs` (read on re-import) | `PacketArtifactStore.cs` (written on export) | **Persisted on the user's local disk** inside downloaded `.zip` files; re-uploaded later (round-trip import, cf. `260507-o20-restore-full-round-trip-on-chatgpt-saved` milestone history) | **KEEP (D3)** — genuine per-platform artifact name; because it's kept, there is **zero backward-compat risk** for any already-downloaded zip from before this phase |
| Zip download-filename platform segment (`"chatgpt"` fallback via `CreateSafePathSegment`) | Client only reads `X-DeckFlow-Filename` response header verbatim (`deck-sync.ts:427-432`, `parseChatGptDownloadFilename`) | `PacketArtifactStore.cs` `Suggest*ZipFileName` methods | Not persisted/parsed back by any code (purely a suggested Save-As name) | **KEEP (D3)** — genuine per-platform value; the **JS function name** `parseChatGptDownloadFilename` and constant `CHATGPT_DOWNLOAD_FALLBACK_FILENAME` are identifiers and DO rename, independent of the filename segment's content |

**Conclusion on D5:** every contract value in this phase is either (a) purely client-side
sessionStorage with no cross-session/cross-tool persistence (safe to rename in lockstep, single
deploy), or (b) a genuine per-platform artifact-naming convention that is entirely on the D3
keep-list (no rename at all, so no compat question arises). There is **no case in this phase**
requiring a dual-read migration shim (unlike, hypothetically, a renamed database column).

## Byte-Identical Render Proof Strategy

**Recommendation: reuse Phase 84's exact pattern** (`theme-baseline-pre84.json` /
`theme-snapshot-post84.json`), since this phase touches the identical 25 theme-fork CSS files
Phase 84 just finished auditing, and the harness already exists as a proven, reviewed, working
pattern in this repo:

1. **Before any CSS/TS/View edit**, capture a computed-style + rendered-HTML baseline via a
   one-off Node + `playwright-core` scratch script (not a tracked dependency) driving the
   headless `scripts/run-web-test.sh` server (`DECKFLOW_DISABLE_AUTO_BROWSER=true`). Unlike
   Phase 84 (which probed individual CSS custom-property values), Phase 85 should snapshot:
   - `document.documentElement.outerHTML` for each of the 6 packet-building routes
     (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`, `/deck-primer`, `/manabase`,
     `/judge-questions`) across a representative set of themes (Classic + 2-3 forks is
     sufficient for a pure identifier swap, since CSS *values* never change — Phase 84 already
     proved cross-theme value-parity infrastructure exists) — **with all `chatgpt`/`ChatGpt`
     substrings normalized to a placeholder token before diffing**, so the diff proves
     "nothing else changed" rather than literally failing on every intentional rename.
   - `getComputedStyle` on a handful of representative renamed selectors (e.g. `.chatgpt-score`,
     `.chatgpt-step-tab`, `.chatgpt-packets-form`) to prove the resolved CSS values (color,
     spacing, display) are pixel-identical pre/post rename — this is the actual byte-identical
     proof, since class NAME changing but CSS RULE VALUES staying identical is exactly what
     "pure identifier swap" means.
2. **After the rename**, re-run the same script and diff. The normalization step (substring
   `s/chatgpt/PLACEHOLDER/gi; s/ChatGpt/PLACEHOLDER/g` then diff, or equivalently
   `s/prompt/PLACEHOLDER/gi` applied to the post-snapshot before compare) turns this into a
   structural "only the tokens I expected to change, changed" assertion — cheaper and more
   reliable than a perceptual/pixel-diff tool, and consistent with Phase 84's "probe declared
   values, not pixels" approach.
3. **Supplement with the existing Playwright e2e suite**, which already contains real assertions
   against these exact selectors (see Validation Architecture below) — these serve as the
   "byte-identical BEHAVIOR" proof (buttons still work, panels still toggle) to complement the
   "byte-identical RENDER" proof from step 1-2.
4. Do **not** rely on git-diff-is-rename-only as the sole proof — CSS/TS/Razor renames are
   text substitutions inside larger files, not file-level `git mv`, so there is no native git
   rename-detection signal to lean on; the diff will show every touched line as modified.

## Ordering / Coupling Hazards

**Hazard 1 — the `moxfield-extension-bridge.ts` cache-key duplication (HIGH, already
documented by Phase 82).** `moxfield-extension-bridge.ts:240,249,256` contains its own
`if (cacheKey === 'chatgpt-packets')` / `'chatgpt-deck-comparison'` / `'chatgpt-cedh-meta-gap'`
checks, **deliberately left un-refactored by Phase 82** specifically because "Phase 85 will
rename them" (see the file's header comment, lines 6-11, and
`.planning/phases/82-.../REFACTOR-BACKLOG.md` row 1b). This means the SAME three literal
strings exist in **three places that must move together in one wave**: the Razor
`data-cache-key="..."` attribute VALUE, `deck-sync.ts`'s `attachGenericPersistedForms`/
`clearChatGptPacketsState` branch, and `moxfield-extension-bridge.ts`'s three `if` checks. If
these desync even briefly (e.g., CSS/Razor wave lands before the TS wave), the "Clear and
restart" button silently stops calling `clearChatGptPacketsState` (falls through to the generic
clear, a *behavior* regression, not just a cosmetic one) and the Moxfield browser-extension
import silently stops routing imported deck text into the right form (a *silent* regression —
no error, just wrong behavior; would only surface via manual browser-extension testing, which
CLAUDE.md's own instructions call out as import-critical). **The two TS files' own top-of-file
comments must also be updated** in the same commit — both literally quote the `chatgpt-*`
strings as documentation ("The `chatgpt-packets` / ... cache-key string literals ... are moved
VERBATIM — no rename"), which becomes stale/self-contradictory once Phase 85 actually renames
them, and (see Pitfall below) trips a literal `chatgpt-` grep if left untouched.

**Hazard 2 — CSS class ↔ Razor class ↔ TS selector ↔ e2e spec, 4-way coupling.** Every
`.chatgpt-*` CSS class has (a) the CSS rule, (b) the Razor `class="..."` emission, (c) zero-to-
several TS `querySelector('.chatgpt-...')` consumers, and (d) **5 confirmed Playwright e2e spec
files** asserting these classes/attributes directly:
`DeckFlow.Web/e2e/print-analysis-results.spec.ts`, `print-button-appearance.spec.ts`,
`deck-analysis-render.spec.ts`, `print-manabase-results.spec.ts`, `ui-responsive.spec.ts`
(verified via `grep -n chatgpt` against each). **These 5 spec files MUST be updated in the same
wave as the CSS/TS/Razor rename**, or the e2e suite goes red (false-negative regression signal,
not a real behavior break) — this is explicitly required by D4/AICLEAN-03 ("Playwright e2e
suite ... unchanged"). No vitest (`ts-tests/*.test.ts`) files reference `chatgpt` (verified —
none of `deck-sync.ts`'s chatgpt-prefixed functions have unit-test coverage; they are tested
only via the e2e specs above).

**Hazard 3 — Phase 84 already touched one of the same lines.** `site-common.css:1222-1223`
(`table[data-chatgpt-cedh-reference-table] tr:has(...:checked)`) was one of Phase 84's 19
swapped cta-border affordance sites (`var(--accent-strong)` → `var(--cta-border, ...)`). Phase
85 touches the *attribute name* on the same selector line. Not a logical conflict (different
token within the same line), but the planner should diff this specific line carefully to avoid
clobbering Phase 84's `var()` fallback-chain edit.

**Hazard 4 — self-referential Phase-82 comments will trip a literal grep gate.**
`busy-indicator.ts:3` ("fully chatgpt-\*-free...") and `moxfield-extension-bridge.ts:6-11`
("the `chatgpt-packets` / ... string literals ... moved VERBATIM") both contain the literal
substring `chatgpt-` **as prose describing the identifiers elsewhere**, not as an identifier
themselves. A naive `grep -c "chatgpt-"` gate over `ts/` will count these as failures even
after every real identifier is renamed. The planner must either (a) reword both comments as
part of this phase (recommended — they're stale once Phase 85 executes, since "moved VERBATIM
— no rename" is no longer true), or (b) special-case them in the grep-clean acceptance check
with an explicit allowlist. Recommend (a): reword to past-tense ("Phase 85 renamed the
`prompt-packets` cache-key literals here to match `deck-sync.ts`").

**Recommended wave sequence:**
1. **Wave 0 (baseline):** capture the pre-rename render/computed-style snapshot (script from
   the previous section), committed before any source edit.
2. **Wave 1 (CSS):** rename all 39 class stems + 4 attribute selectors across the 25 CSS files.
   No behavior risk yet (nothing references the new names), pure text substitution, verifiable
   by `dotnet build` + a full-file diff showing only the token change.
3. **Wave 2 (TS + Views + e2e specs, atomically together):** rename all 48 `data-*` attribute
   names, 38 camelCase symbols, and the 3 D5 contract values across `deck-sync.ts`,
   `moxfield-extension-bridge.ts`, all Razor views, and the 5 e2e spec files, **in one commit or
   one tightly-sequenced set of commits with no intermediate state where Razor/TS/e2e disagree
   on the literal string**. Reword the two self-referential comments in this same wave.
4. **Wave 3 (C# small surface):** `ChatGptSwapPrompt` rename (3 prod files + CLI + 1 test file)
   + `chatgpt-analysis` test-fixture slug + prose cleanup (`FeatureFlagCatalog.cs`,
   `ToolRegistry.cs`, `DeckPacketController.cs`, `DeckAnalysisPacketService.cs` comments) +
   test-assertion updates for the renamed CSS/attribute strings
   (`DeckAnalysisScoreViewTests.cs`, `*PrintButtonViewTests.cs`, `MetaGapViewRenderTests.cs`) —
   can run in parallel with Wave 2 since it touches disjoint files, but must land before the
   final grep-clean gate.
5. **Wave 4 (proof + gate):** re-capture the post-rename snapshot, diff against Wave 0's
   baseline, run the full xUnit + Vitest + Playwright e2e suites, run the grep-clean acceptance
   checks (below).

## Runtime State Inventory

*(Included because this is a rename/refactor phase per the trigger condition.)*

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | sessionStorage keys in the **user's browser** only: `chatgpt-packets`, `chatgpt-deck-comparison`, `chatgpt-cedh-meta-gap` (form-cache), `decksync-chatgpt-ui-mode` (UI-mode preference) — all `sessionStorage` (verified, not `localStorage`), scoped to a single browser tab session | None required — code edit only. Existing entries under old keys become orphaned/unreadable after deploy (self-healing: sessionStorage clears on tab close; worst case the user's in-progress form draft in that one tab is lost, same as any other sessionStorage key rename) |
| Live service config | None found — no external service (Render, Fly, n8n, Datadog, etc.) references `chatgpt-*`/`ChatGpt*` identifiers | None |
| OS-registered state | None found | None |
| Secrets/env vars | None found — no env var, SOPS key, or CI secret name contains "chatgpt" (verified: none of `render.yaml`/`fly.toml`/`Program.cs`'s env-var reads reference it) | None |
| Build artifacts | `DeckFlow.Web/wwwroot/js/*.js` (compiled TS output) — **gitignored, never committed**, rebuilt fresh from `wwwroot/ts/*.ts` on every `dotnet build`/`publish` (per `CompileTypeScriptAssets` MSBuild target). No stale-artifact risk: the renamed `.ts` source recompiles automatically | None — do not manually touch `wwwroot/js/` |
| Persisted external artifacts | Already-downloaded session `.zip` files on a **user's local disk** from before this phase, containing `30-primer-chatgpt-prompt.txt` and a `-chatgpt-` filename segment | **None** — both are on the D3 keep-list (untouched by this phase), so re-uploading a pre-Phase-85 zip continues to work identically. Confirmed no compat break. |

## Common Pitfalls

### Pitfall 1: Treating the C# grep count (368/88 files) as the rename scope
**What goes wrong:** A naive "rename every ChatGpt in .cs" sweep would break 7 production
classes, their DI registrations, and ~50 test files that are testing genuine per-platform
behavior — a catastrophic collapse of the ChatGPT/Claude/Gemini distinction ADR-0001 protects.
**Why it happens:** CONTEXT.md's own inventory ("38 refs across ~14 files") undersold the true
size of the C# surface, inviting an executor to assume "small, just rename it all."
**How to avoid:** Gate every C# match through the KEEP-LIST table above before touching it.
**Warning signs:** `dotnet build` failures in `PromptVariantServiceCollectionExtensions.cs` (DI
registration referencing a renamed-away type), or `ByteIdentityTests`/`Goldens.cs` failures.

### Pitfall 2: Self-referential Phase-82 comments trip the grep-clean gate
See Ordering Hazard 4. **Warning sign:** `grep -rc "chatgpt-" DeckFlow.Web/wwwroot/ts/` returns
non-zero on `busy-indicator.ts`/`moxfield-extension-bridge.ts` even after every real identifier
is renamed.

### Pitfall 3: Renaming a D5 contract value on only one side
**What goes wrong:** Renaming `data-cache-key="chatgpt-packets"` in the Razor view without the
matching change in both `deck-sync.ts` AND `moxfield-extension-bridge.ts` silently breaks the
"Clear and restart" button and the Moxfield extension's form-routing, with no build error and no
obviously-failing test (the e2e specs listed above test the `data-chatgpt-show-step`/print
selectors, not this specific cache-key branch — there is **no existing automated test** for
`clearChatGptPacketsState`'s branch-selection logic or the extension-bridge's 3 cache-key `if`
checks). **How to avoid:** grep for the exact 3 string literals across the whole `ts/` +
`Views/` tree as a single lockstep edit; consider adding a regression test for
`attachGenericPersistedForms`'s cache-key branch if none exists (flagged as a coverage gap, see
Validation Architecture).

### Pitfall 4: Assuming `ChatGptDeckPacketService` exists
Per the Summary section, this type does not exist (confirmed zero grep hits). If the plan
references it (as 85-CONTEXT.md's canonical_refs does), drop the reference — there is nothing
to rename there. The four post-Phase-83 packet services already have generic names
(`DeckAnalysisPacketService`, `DeckComparisonService`, `MetaGapService`,
`DeckPrimerPacketService`) and contain zero `ChatGpt*` symbols of their own (their few
`chatgpt`-matching lines, per the C# inventory above, are either KEEP defaults/comments or the
2 minor RENAME-eligible doc comments already listed).

### Pitfall 5: CSS attribute-selector line collision with Phase 84's edit
See Ordering Hazard 3 (`site-common.css:1222-1223`). Diff carefully; do not regenerate this line
from scratch (would risk reverting Phase 84's `var(--cta-border, var(--accent-strong, ...))`
fallback chain).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework (C#) | xUnit 2.9.3, `DeckFlow.Web.Tests` + `DeckFlow.Core.Tests` |
| Framework (TS unit) | Vitest ^3.0.0, config `DeckFlow.Web/vitest.config.ts`, tests in `DeckFlow.Web/ts-tests/*.test.ts` |
| Framework (e2e) | Playwright ^1.60.0, config `DeckFlow.Web/playwright.config.ts`, specs in `DeckFlow.Web/e2e/*.spec.ts` |
| Quick run command (C#) | `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~Manabase\|FullyQualifiedName~Score\|FullyQualifiedName~PrintButton\|FullyQualifiedName~MetaGapViewRender\|FullyQualifiedName~HelpContentService\|FullyQualifiedName~HelpController"` (targets the files this phase actually edits) |
| Quick run command (e2e) | `npx --no-install playwright test print-analysis-results print-button-appearance deck-analysis-render print-manabase-results ui-responsive theming` (the 5 touched specs + `theming.spec.ts` as a Phase-84 regression check since both phases share CSS files) |
| Full suite command (C#) | `dotnet.exe test DeckFlow.sln` |
| Full suite command (e2e) | `npx --no-install playwright test` (run against headless `scripts/run-web-test.sh`, `DECKFLOW_DISABLE_AUTO_BROWSER=true`, per CLAUDE.md — never a Windows-host browser) |
| Full suite command (TS unit) | `npm run test` (Vitest) — no chatgpt-referencing vitest files exist today; expected 0 new failures |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| AICLEAN-01 | Every `.chatgpt-*` CSS class renamed, rendered output byte-identical | Structural diff + build | Wave-0/Wave-4 snapshot diff script (new, scratch) + `dotnet build DeckFlow.sln` | ❌ Wave 0 (new scratch script, not committed as a tracked test, matching Phase 84's pattern) |
| AICLEAN-01 | No non-identifier CSS declaration/value changed | Grep gate | `git diff -- DeckFlow.Web/wwwroot/css | grep -E '^[+-]' | grep -viE 'chatgpt|prompt'` returns empty | N/A — ad hoc gate, not a test file |
| AICLEAN-02 | TS/Views/`data-*` renamed in lockstep, no dead selector | e2e | `npx playwright test print-analysis-results print-button-appearance deck-analysis-render print-manabase-results ui-responsive` | ✅ existing, needs source-string updates in the same wave |
| AICLEAN-02 | `data-cache-key`/`data-sync-panel` contract values renamed on both client/server sides | Manual/new unit test (coverage gap, see below) | none automated today | ❌ Wave 0 gap |
| AICLEAN-03 | Zero `chatgpt-*` in `css/`, `ts/`, `Views/` | Grep gate | `grep -rli 'chatgpt-' DeckFlow.Web/wwwroot/css DeckFlow.Web/wwwroot/ts DeckFlow.Web/Views` returns empty **after** rewording the 2 self-referential comments (Pitfall 2) | N/A — ad hoc gate |
| AICLEAN-03 | Zero `ChatGpt`/`chatgpt` outside D3 keep-list in `.cs` | Grep gate + manual keep-list cross-check | `grep -rli 'chatgpt' --include=*.cs <dirs>` then diff the file list against this research's KEEP-LIST table | N/A — ad hoc gate, but the KEEP-LIST table above should be copied into the plan as the literal allowlist |
| AICLEAN-03 | Full e2e suite green | e2e | `npx --no-install playwright test` | ✅ existing full suite |
| AICLEAN-03 | All xUnit green | unit | `dotnet.exe test DeckFlow.sln` | ✅ existing full suite |

### Sampling Rate
- **Per task commit:** the file-scoped quick-run commands above (only the touched test files).
- **Per wave merge:** full `dotnet.exe test DeckFlow.sln` + full `npx playwright test` + the
  grep-clean gates.
- **Phase gate:** all of the above green, plus the Wave-4 render/computed-style diff, before
  `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] Scratch Node + `playwright-core` snapshot script (baseline + post-rename), mirroring
      `.planning/phases/84-theme-semantic-token-migration/theme-baseline-pre84.json`'s pattern —
      not a tracked dependency, one-off per Phase 84 precedent.
- [ ] **Coverage gap, not blocking:** no existing automated test locks
      `attachGenericPersistedForms`'s `data-cache-key === 'chatgpt-packets'` branch-selection
      logic, nor `moxfield-extension-bridge.ts`'s 3 cache-key `if` checks. Recommend adding one
      Vitest case (or extending an existing `ts-tests/*.test.ts` file) asserting the renamed
      cache-key routes to the renamed clear-function, to close Pitfall 3's blind spot — this is
      a "nice to have" the planner should explicitly accept or defer with reasoning, not silently
      skip, per the milestone's "no test harness is not a valid deferral reason" precedent from
      Phase 82.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Manabase's `ChatGptSwapPrompt` and JudgeQuestions' "ChatGPT prompt generator" copy should be renamed to generic AI/Prompt wording rather than kept as genuine single-model branding | KEEP+FLAG (Ambiguous) | If wrong (i.e., these really are meant to stay ChatGPT-specific — e.g., a future intent to format them with ChatGPT-specific prompt conventions), renaming would be a product-copy regression a human should catch at plan-review, not silently execute |
| A2 | `FeatureFlagCatalog.cs`/`ToolRegistry.cs` description strings are safe to rename as prose cleanup without being a "behavior change" | RENAME table (C# prose) | If these strings render verbatim on an admin flags page or the tool-hub cards (user-visible), a text change technically violates "byte-identical" framing even though it's not layout/color — verify rendering surface before executing, may need explicit user sign-off same as README-worthy text changes per D6 |
| A3 | No existing automated test would catch a lockstep-desync of the 3 sessionStorage cache-key values (Pitfall 3) | Wave 0 Gaps | If a hidden test does exist and is missed, the planner might redundantly add a duplicate test; low risk, err toward checking `grep -rn "chatgpt-packets\|chatgpt-deck-comparison\|chatgpt-cedh-meta-gap"` across ts-tests before deciding to add one |

**If this table is empty:** N/A — see rows above.

## Open Questions

> **STATUS: ALL RESOLVED at plan review (2026-07-05).** Both reviewers (Claude gsd-plan-checker +
> Codex gpt-5.5) + orchestrator confirmed the resolutions inline below; encoded in 85-04 / 85-05.

1. **Should `ChatGptSwapPrompt` (Manabase) and JudgeQuestions' ChatGPT-branded copy be renamed
   or kept?**
   - **RESOLVED: RENAME the C# SYMBOL, KEEP the user-visible COPY.** `ChatGptSwapPrompt` is a generic
     symbol (no Claude/Gemini sibling, no `_AiSelector`) and the user chose "Everything incl C#", so it is
     renamed `ChatGptSwapPrompt` -> `PromptSwapPrompt` across the Manabase service/view-model/controller/
     tests + the `@Model` binding (85-04 Task 1). Renaming a property does not change the STRING VALUE it
     holds, so the rendered swap-prompt copy stays byte-identical. All user-visible all-caps "ChatGPT" copy
     (Manabase "Copy this prompt for ChatGPT / Claude", the CLI header, JudgeQuestions "ChatGPT prompt
     generator" prose) is KEPT verbatim (D3/D4). JudgeQuestions has zero identifiers to rename (100% prose).
   - What we know: no per-platform variant exists for either feature (verified: no
     `ClaudeSwapPrompt`, no `_AiSelector` on either view); the underlying prompt text is
     generic/AI-agnostic; existing copy is already visually inconsistent (Manabase's own view
     says "for ChatGPT / Claude" — omitting Gemini).
   - What's unclear: whether this is intentional (these two tools were deliberately never
     extended past ChatGPT for a product reason) or simply legacy naming debt identical to the
     CSS-class pattern.
   - Recommendation: default to RENAME (consistent with the rest of the app, low regression
     risk, no test locks the exact copy), but surface this explicitly to the user/planner as a
     confirmation checkpoint before executing, since it's the one part of this phase that
     changes what an end user reads, not just what an element's class name is.

2. **Are `FeatureFlagCatalog.cs`/`ToolRegistry.cs` description strings rendered to end users?**
   - **RESOLVED: KEEP as user-visible copy.** `FeatureFlagCatalog.cs`/`ToolRegistry.cs` description
     strings are treated as user-visible product copy (hub-card / admin-flags text) and are NOT renamed in
     Phase 85 — they use all-caps "ChatGPT", which the case-sensitive `ChatGpt|chatgpt` identifier grep does
     not match, so they need no allowlist entry and trip no gate. Only the 2 internal (non-visible) doc
     comments in `DeckPacketController.cs` and `DeckAnalysisPacketService.cs` are genericized (85-04 Task 2).
   - What we know: `ToolRegistry.Create(...)` populates hub-card descriptions (used in
     `Deck/Home.cshtml`'s tool cards, based on the constructor signature's `"Deck Primer"`/
     `"Generate a staged, ChatGPT-ready primer..."` argument shape); `FeatureFlagCatalog`
     descriptions likely render on `/Admin/Flags`.
   - What's unclear: exact rendering location wasn't traced to the Razor consumption site in
     this research pass (out of the phase's file-scope core, but relevant to whether this is a
     "prose cleanup" or a "user-facing copy change").
   - Recommendation: planner should grep `ToolRegistry` consumption in `Deck/Home.cshtml` and
     `FeatureFlagCatalog` consumption in the Admin views before finalizing whether these 3 edits
     need the same "ask before commit" caution as other user-facing text changes.

## Sources

### Primary (HIGH confidence — direct repo inspection)
- `grep`/`find` against the current working tree (`/mnt/c/users/chrislunt/source/personal/deckflow-cycle15`) for every count, file list, and classification in this document — all counts are reproducible via the exact commands shown inline above.
- `.planning/phases/85-chatgpt-naming-cleanup/85-CONTEXT.md` — locked decisions D1-D6.
- `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`, `.planning/STATE.md` — requirement text, phase sequencing rationale, and the Phase 82 `chatgpt-packets` deferral note.
- `.planning/phases/82-refactor-review-sweep-ui-baseline-audit/REFACTOR-BACKLOG.md` — row 1b, the explicit "coordinated with Phase 85" deferral.
- `.planning/phases/84-theme-semantic-token-migration/84-01-SUMMARY.md`, `84-02-SUMMARY.md` — the baseline-capture/no-drift-diff pattern this research recommends reusing.
- `./CLAUDE.md` — theme-fork CSS model, changed-lines format gate, UI-testing-no-browser rule.

### Secondary / Tertiary
None — no WebSearch/Context7 lookups were needed; this phase is entirely repo-internal identifier renaming with no external library or API surface.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new tooling, existing xUnit/Vitest/Playwright/dotnet build confirmed present and already exercising the touched files.
- Architecture / inventory: HIGH — every count is a direct `grep`/`find` result against the working tree, not an estimate.
- D3 classification (keep-list): HIGH for the unambiguous majority (trio classes, DI, AiPlatform, zip filenames — all cross-checked against sibling Claude/Gemini files); MEDIUM for the 4 flagged ambiguous items (Manabase/JudgeQuestions/ContentKb copy) — these are product-copy judgment calls, not technical facts, hence flagged rather than asserted.
- Pitfalls / ordering hazards: HIGH — hazard 1 (extension-bridge cache-key duplication) is drawn from the Phase 82 authors' own explicit, committed comment, not inferred.

**Research date:** 2026-07-05
**Valid until:** Should be re-verified if any other phase touches `deck-sync.ts`, the CSS theme forks, or `PacketArtifactStore.cs` before Phase 85 executes (none currently planned between now and Phase 85 per ROADMAP sequencing, but Phase 86 is queued next and does not touch these files).
