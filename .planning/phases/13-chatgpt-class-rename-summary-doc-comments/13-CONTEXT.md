# Phase 13: ChatGpt* Class Rename + Summary Doc Comments - Context

**Gathered:** 2026-05-17
**Status:** Ready for planning
**Mode:** `--auto` — gray areas auto-selected, recommended options applied per `discuss-phase/modes/auto.md`

<domain>
## Phase Boundary

Strip the `ChatGpt` prefix from every C# public type in the AI-workflow surface (request DTOs, services, view models, parsers, artifact stores, response shape classes) so the code layer matches the AI-agnostic naming locked in Phase 12 at the URL + UI layer. Use the rename pass as the natural moment to backfill XML `<summary>` doc comments on every renamed class so `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in `DeckFlow.Web.csproj` compiles clean without leaning on `NoWarn 1591` suppressions for these types.

**What this phase does NOT do** (deferred per CLASSRENAME boundary):
- Split DeckController god-class (own refactor milestone — CLAUDE.md "Out of Scope")
- Extract ChatGpt services into PromptBuilder / ScryfallReferenceResolver helpers (own refactor milestone)
- Replace `string TargetAiPlatform` with `AiPlatform` value object — that is AIPLATFORM-01/02, Phase 15
- Change response JSON schemas, prompt content, or any user-visible artifact format
- Rename internal `data-cache-key="chatgpt-packets"` JS identifiers and the `chatgpt-packets-form` CSS class — those are TS/CSS-coupled internal identifiers, separate cleanup (could be Phase 16 hygiene if surfaced)
- Replace `"ChatGPT"` literal in `AiPlatform.Key` / `targetAiPlatform` request strings — Phase 10 invariant; this is a real AI platform identifier, not the page-naming prefix

</domain>

<decisions>
## Implementation Decisions

### Naming convention
- **D-01 [auto/recommended]:** Adopt the explicit target names already locked by CLASSRENAME-01 + AIPLATFORM-01 spec text. Map ChatGpt-prefixed types page-by-page to the Phase 12 URL slug terminology:
  | Old name | New name |
  |---|---|
  | `ChatGptDeckRequest` | `DeckAnalysisRequest` |
  | `ChatGptDeckViewModel` | `DeckAnalysisViewModel` |
  | `ChatGptDeckPacketService` | `DeckAnalysisPacketService` (interface `IDeckAnalysisPacketService`) |
  | `ChatGptDeckAnalysisResponse` (+ nested `ChatGptWeakSlot`, `ChatGptQuestionAnswer`, `ChatGptDeckVersion`) | `DeckAnalysisResponse` (+ `WeakSlot`, `QuestionAnswer`, `DeckVersion`) |
  | `ChatGptSetUpgradeResponse` (+ nested `ChatGptSetUpgradeSet`) | `SetUpgradeResponse` (+ `SetUpgradeSet`) |
  | `ChatGptDeckComparisonRequest` | `DeckComparisonRequest` |
  | `ChatGptDeckComparisonViewModel` | `DeckComparisonViewModel` |
  | `ChatGptDeckComparisonService` | `DeckComparisonService` (interface `IDeckComparisonService`) |
  | `ChatGptDeckComparisonResponse` | `DeckComparisonResponse` |
  | `ChatGptCedhMetaGapRequest` | `MetaGapRequest` (per AIPLATFORM-01 explicit target) |
  | `ChatGptCedhMetaGapViewModel` | `MetaGapViewModel` |
  | `ChatGptCedhMetaGapService` | `MetaGapService` (interface `IMetaGapService`) |
  | `ChatGptCedhMetaGapResponse` (+ 12 nested `ChatGptCedh*` shape classes) | `MetaGapResponse` (+ nested classes drop both `ChatGpt` and `Cedh` prefixes: `WinLines`, `Interaction`, `Speed`, `ManaEfficiency`, `CoreConvergenceCard`, `MissingStaple`, `PotentialCut`, `TopAdd`, `TopCut`, etc.) |
  | `ChatGptPacketArtifactStore` | `PacketArtifactStore` (kept as single shared static-helper class — used by all three pages; not page-scoped) |
  | `ChatGptRequestContextParser` | `RequestContextParser` |
  | `ChatGptResponseParsers` | `ResponseParsers` |
  | `ChatGptJsonTextFormatterService` | `JsonTextFormatterService` (interface `IJsonTextFormatterService`) |
  | `DeckPageTab.ChatGptPackets` (enum value) | `DeckPageTab.DeckAnalysis` |
  | `DeckPageTab.ChatGptDeckComparison` | `DeckPageTab.DeckComparison` |
  | `DeckPageTab.ChatGptCedhMetaGap` | `DeckPageTab.CedhMetaGap` |

  **Why:** matches the page-name layer landed in Phase 12 (deck-analysis / deck-comparison / cedh-meta-gap), keeps the request → service → response triplet symmetric per page, and aligns with the AIPLATFORM-01 spec text that already names `DeckAnalysisRequest, DeckComparisonRequest, MetaGapRequest` as the targets.

- **D-02 [auto/recommended]:** Where the new name collides with an unrelated existing type (e.g., `DeckAnalysisResponse` vs any existing analysis namespace), prefer the new AI-agnostic name and rename the conflicting type per CLAUDE.md "name reflects current responsibility" guidance. Currently no known collisions — verify in research phase.

### Doc-comment scope (CLASSRENAME-02)
- **D-03 [auto/recommended]:** Add a one-sentence `/// <summary>` on every renamed public type — class, sealed class, record, interface — plus their public constructors and public methods that don't already carry one. Anchor each summary to the class's current responsibility (read the method bodies to derive accurate wording — do NOT generate "TODO" or vague placeholders). Match the project's existing XML-doc tone from `CardLookupService` / `CommanderSpellbookService` (terse, single-sentence). Nested response-shape classes get a one-line summary describing what JSON shape they map to.
- **D-04 [auto/recommended]:** `NoWarn` for IDs 1591 (missing summary), 1573 (missing param doc), 1587 (XML doc on wrong element) STAYS in `DeckFlow.Web.csproj` — this phase only guarantees the renamed types compile clean against those warnings, not the whole assembly. Removing the suppression is a separate cleanup that would block on every untouched type also gaining docs.

### Rename execution strategy
- **D-05 [auto/recommended]:** Use `git mv` for file renames (one file rename = one logical commit) so blame and follow history survive. Per CLAUDE.md commit hygiene: one logical change per commit. Wave grouping:
  - **Wave 1 (Models):** rename all 3 request DTOs + 3 view models + 3 top-level response classes + their nested shape classes. No service / controller / view touches. Build won't be green at end of wave (downstream still references old names) — this is acceptable for an intra-phase intermediate state because next wave fixes it. Mitigation: each commit in the wave is atomic per file but build green is verified only at end of Wave 4.
  - **Wave 2 (Services):** rename 6 service classes + their interfaces + DI registrations in `Program.cs`. Update internal references (services that depend on other renamed services). Update `[InternalsVisibleTo]`.
  - **Wave 3 (Controller + Views):** update `DeckController.cs` action method names + ViewModel bindings + 142 type references. Update Razor `@model DeckAnalysisViewModel` directives in renamed views from Phase 12.
  - **Wave 4 (Tests + final build gate):** rename test classes (`*Tests.cs`) + fixture references. Final `dotnet build DeckFlow.sln --configuration Release` MUST succeed zero-warning zero-error. Run code review skill at end.
- **D-06 [auto/recommended]:** Sequential execution within each wave (no `isolation="worktree"` parallelism) because every wave overlaps the same DeckController.cs / Program.cs files. Inter-wave parallelism is impossible (each wave depends on prior wave's renames). Use single-executor agent per wave.

### String literal + content preservation (the "do not rename" list)
- **D-07 [auto/recommended]:** Preserve EXACTLY (Phase 10 + Phase 12 invariants — grep-test these at the end of each wave):
  - `"ChatGPT"`, `"Claude"`, `"Gemini"` AI platform Key string values in `AiPlatform` constants
  - `request.TargetAiPlatform` property name (Phase 15 will replace via AIPLATFORM-01)
  - `targetAiPlatform` form-field name (binds to that property)
  - "chatgpt" segment in artifact zip filename fallback in `PacketArtifactStore` (Phase 10 commit `00e5bdd` invariant)
  - "ChatGPT" as a *narrative* word in XML doc-comment summaries where it describes the AI's role (e.g., "Parses the ChatGPT-returned JSON payload into..."). Renaming to "AI" would be too vague; "ChatGPT/Claude/Gemini" is too long. Allowed because doc comments don't affect class names.
  - "ChatGPT" in Razor view *visible* prose where it accurately names the AI (e.g., result-panel labels). UI prose stays accurate; CLASSRENAME is a code-symbol phase.
- **D-08 [auto/recommended]:** Internal HTML/JS identifiers untouched in this phase (see Phase Boundary deferrals): `data-cache-key="chatgpt-packets"`, `data-chatgpt-*-form`, `class="chatgpt-packets-form"`, TS const names like `parseChatGptDownloadFilename`. These are not C# types and are coupled to TypeScript; renaming risks runtime breakage and the TS sweep is a distinct cleanup with its own test surface.

### Verification + manual UAT
- **D-09 [auto/recommended]:** Verification gate (T-13-01..T-13-04):
  1. `grep -rE "ChatGpt[A-Z]" --include="*.cs" DeckFlow.Web/ DeckFlow.Core/ DeckFlow.Web.Tests/ DeckFlow.Core.Tests/` returns ZERO hits outside permitted exceptions (test fixture string literal `"chatgpt-analysis"` in `HelpContentServiceTests.cs:44` — already independent of source files post-Phase 12; AiPlatform string keys; doc-comment narrative usage).
  2. `dotnet build DeckFlow.sln --configuration Release` → 0 warnings, 0 errors.
  3. CLI `DeckFlow.CLI` still builds + runs all 5 commands clean (no broken DI registrations).
  4. Manual T1–T8 integration suite (per `.planning/milestones/v1.2-MILESTONE-AUDIT.md`) re-run against post-rename HEAD — all three pages produce identical artifacts and round-trip identical zips.
- **D-10 [auto/recommended]:** Per CLAUDE.md "VSTest unreliable in WSL" — automated xUnit test runs are NOT part of the verifier gate. Build-clean is the gate. Manual T1–T8 round-trip becomes a HUMAN-UAT.md item if `dotnet test` cannot be sanely run.

### Claude's Discretion
- Naming of internal helper methods within renamed classes if they currently include "ChatGpt" in their identifier — strip the prefix if it clarifies, leave it if removal would create a less descriptive name. Decide per case during execution.
- Order of file renames within a wave — alphabetical by old filename is fine.
- Whether to introduce interface symmetry (`IDeckAnalysisPacketService`) for services that don't currently have an interface — only add interfaces where DI already resolves via an interface name; do NOT introduce new interfaces in this phase (that's a refactor, not a rename).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + spec
- `.planning/REQUIREMENTS.md` (CLASSRENAME-01, CLASSRENAME-02, CLASSRENAME-03 entries — defines target type list and acceptance gates)
- `.planning/ROADMAP.md` (Phase 13 entry — Success Criteria 1..4, dependency on Phase 12)

### Prior-phase context (binding decisions)
- `.planning/phases/12-ai-agnostic-url-page-rename/12-CONTEXT.md` — D-14 explicitly deferred class renames to Phase 13; D-11/D-13 Phase 10 invariants (AI fallback `"chatgpt"`, `targetAiPlatform`) carry forward; D-07 site-common.css theme rule still applies for any CSS touched
- `.planning/phases/12-ai-agnostic-url-page-rename/12-VERIFICATION.md` — lists every Phase-13 deferral surface that must remain unchanged here

### Future-phase coupling (do NOT break)
- `.planning/milestones/v1.2-phases/10-claude-gemini-artifact-optimization/10-AISEL-PLATFORM-DESIGN.md` — AIPLATFORM-01 value-object design that Phase 15 implements on top of the renamed `DeckAnalysisRequest` / `DeckComparisonRequest` / `MetaGapRequest`. Phase 13 names MUST line up with this design's expected target names.
- `.planning/milestones/v1.2-MILESTONE-AUDIT.md` — T1–T8 manual integration test suite invoked by D-09 SC4 + D-10

### Project constraints
- `CLAUDE.md` — VSTest WSL constraint (rely on `dotnet build` clean); commit hygiene (one logical change per commit, no Co-Authored-By trailer); README updated when behavior changes; nullable enable; `<GenerateDocumentationFile>true</GenerateDocumentationFile>` invariant
- `DeckFlow.Web/DeckFlow.Web.csproj` — verifies `NoWarn 1591;1573;1587` stance and `GenerateDocumentationFile` value

### Codebase intel
- `.planning/codebase/STRUCTURE.md` — service folder layout
- `.planning/codebase/CONVENTIONS.md` — interface + sealed class + record naming conventions, file-per-type rule
- `.planning/codebase/INTEGRATIONS.md` — RestSharp / Polly resilience pipeline named clients (do not break in service rename)

</canonical_refs>

<code_context>
## Existing Code Insights

### Surface area scout (2026-05-17)
- 26 `ChatGpt`-prefixed public class definitions across `DeckFlow.Web/Models/` (11 files: request DTOs, view models, response shape classes incl. 12 nested `ChatGptCedh*` shapes) and `DeckFlow.Web/Services/` (6 files: PacketService, DeckComparisonService, CedhMetaGapService, RequestContextParser, ResponseParsers, JsonTextFormatterService, PacketArtifactStore)
- 142 `ChatGpt` identifier hits in `DeckFlow.Web/Controllers/DeckController.cs` alone — this single file is the highest-fanout target
- 27 hits in `Services/ChatGptCedhMetaGapService.cs`, 38 in `Services/ChatGptDeckComparisonService.cs`, 47 in `Services/ChatGptDeckPacketService.cs`, 24 in `Models/ChatGptCedhMetaGapResponse.cs`
- `DeckFlow.Web/Models/DeckPageTab.cs` enum has 3 `ChatGpt*` enum values (consumed by `_DeckToolTabs.cshtml` to render the active tab)
- `Program.cs:8` lines mention ChatGpt* type names in DI registration (`AddScoped<IChatGptDeckPacketService, ChatGptDeckPacketService>()` style)

### Reusable patterns to follow
- Existing services pair `I*Service` interface + `*Service` class in the same file (per CONVENTIONS.md). Maintain this; do not split when renaming.
- Sealed records / sealed classes per CONVENTIONS.md. Maintain.
- XML doc style: terse single-sentence `<summary>` matching `ScryfallCardLookupService` and `CommanderSpellbookService` in `DeckFlow.Web/Services/` for tone reference.
- `[InternalsVisibleTo("DeckFlow.Web.Tests")]` in `DeckFlow.Web/AssemblyInfo.cs` enables test seams — no change needed here, but tests must still resolve renamed types.

### Integration points to watch
- DI container: `Program.cs:60-180`-ish service registration block. Every renamed service interface + implementation pair needs the registration updated; missing one = runtime DI failure on first request.
- Razor `@model X` directives at top of each `.cshtml` — must match renamed ViewModel exact name.
- Razor form field `name="…"` attributes — bind to ViewModel property names by convention. ViewModel property names are NOT renamed in this phase (only the class name). Phase 15 (AIPLATFORM) handles property-level changes.
- Test fixture references (`new ChatGptDeckRequest { … }` etc.) in `DeckFlow.Web.Tests/` will fail compile after Models rename — must be addressed within Wave 1 or Wave 4 explicitly.
- `DeckFlow.Core` has zero `ChatGpt*` references — out of rename scope.

</code_context>

<specifics>
## Specific Ideas

- Page-name alignment over generic naming: Phase 12 chose `deck-analysis` / `deck-comparison` / `cedh-meta-gap` for URLs, and CLASSRENAME-01 + AIPLATFORM-01 spec text explicitly names `DeckAnalysisRequest, DeckComparisonRequest, MetaGapRequest` as targets — so the rename triplet is `DeckAnalysis*`, `DeckComparison*`, `MetaGap*`, not `Ai*` or some other generic prefix.
- The shared static helper `ChatGptPacketArtifactStore` renames to bare `PacketArtifactStore` (no page prefix) because it serves all three pages — same precedent as `CardNormalizer`, `MoxfieldApiUrl`, `ArchidektApiUrl` from CONVENTIONS.md "static classes for stateless helpers".

</specifics>

<deferred>
## Deferred Ideas

- **AIPLATFORM-01 / AIPLATFORM-02 — `AiPlatform` value object refactor:** Phase 15 picks up after this rename. The renamed `DeckAnalysisRequest` / `DeckComparisonRequest` / `MetaGapRequest` will have their `string TargetAiPlatform` property replaced with a sealed-record value object then. Naming chosen here is forward-compatible with that refactor.
- **DeckController god-class split:** Own refactor milestone per PROJECT.md "Carried from v1.0" deferred list. Phase 13 may surface that DeckController action-method names also carry the `ChatGpt` prefix — those can be renamed in lockstep with the type rename without splitting the controller.
- **JS / TS / CSS internal identifier sweep:** `chatgpt-packets-form` class, `data-cache-key="chatgpt-packets"`, `parseChatGptDownloadFilename` TS helper — not in CLASSRENAME-01..03 scope and would require a TS-level test pass; candidate for a future hygiene phase (Phase 16?).
- **Removing `NoWarn 1591;1573;1587` from `DeckFlow.Web.csproj`:** Blocked on every untouched type also gaining docs. Out of this phase.
- **Class-rename-related refactors discovered during execution** (e.g., extracting prompt-builder helpers, splitting response shape files): defer to AUDIT-01 / Phase 14 broader name-vs-behavior audit unless trivially in line with rename scope.

</deferred>

---

*Phase: 13-ChatGpt* Class Rename + Summary Doc Comments*
*Context gathered: 2026-05-17*
