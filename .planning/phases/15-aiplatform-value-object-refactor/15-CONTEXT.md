# Phase 15: AiPlatform Value Object Refactor - Context

**Gathered:** 2026-05-17
**Status:** Ready for planning
**Mode:** interactive `discuss` (4 gray areas selected, all answered)

<domain>
## Phase Boundary

Replace the stringly-typed `TargetAiPlatform` dispatch with a sealed `AiPlatform` record value object per `.planning/milestones/v1.2-phases/10-claude-gemini-artifact-optimization/10-AISEL-PLATFORM-DESIGN.md`. Push OCP forecast from 3/10 → 8/10 so a hypothetical 4th platform is **one** `AiPlatform.All` entry + N variant classes + N DI lines, **zero** edits to switches/setters/Razor/parser.

**Scope of this phase:**

1. New `AiPlatform` sealed record (`Key`, `DisplayName`, `Description`) with `All`, `Default`, `Normalize(string?)` API per design doc.
2. All three request DTOs (`DeckAnalysisRequest`, `DeckComparisonRequest`, `MetaGapRequest` — final names per Phase 13 CLASSRENAME-01) migrate setter from inline string switch to `_targetAiPlatform = AiPlatform.Normalize(value).Key`.
3. All five per-AI prompt builders (`BuildAnalysisPrompt`, `BuildSetUpgradePrompt`, `BuildComparisonPrompt`, `BuildFollowUpPrompt`, `BuildMetaGapPrompt`) extracted from their host services into **per-family strategy registries** (`IAnalysisPromptVariant` / `AnalysisPromptVariantRegistry` × 5 families) with one variant implementation per `AiPlatform` (15 variants).
4. `Views/Shared/_AiSelector.cshtml` iterates `AiPlatform.All` instead of hard-coding 3 `<input type="radio">` blocks (Gemini `@if (geminiEnabled)` gate stays around the option's render, not the iteration).
5. `Services/RequestContextParser.cs` and `Services/PacketArtifactStore.cs` defensively call `AiPlatform.Normalize` on inbound zip-load values before assigning to `request.TargetAiPlatform` setter.
6. SC5 4th-platform extension proof — actual test in `DeckFlow.Web.Tests` asserting that adding `AiPlatform.Test` + 5 stub variants requires **no** edits to switches, setters, Razor, or `RequestContextParser`.

**What this phase does NOT do:**

- Split `DeckAnalysisPacketService` god-class — deferred per CLAUDE.md "Out of Scope" + design doc §"Out of scope". Phase 15 extracts only the 5 named builder families.
- Migrate `JsonTextFormatterService.ExtractJsonPayload` to per-AI strategy — the unified `<result>` regex is already AI-agnostic (Phase 10 Decision); SC1's "response-extraction strategy" is satisfied by the per-builder registry pattern, not by per-AI extractor delegates.
- Change any user-facing string contract — `"ChatGPT"` / `"Claude"` / `"Gemini"` literal Keys, `request.TargetAiPlatform` property name, `targetAiPlatform` form field name, `"chatgpt"` zip filename fallback all stay byte-identical (Phase 14 D-10 preservation list applies).
- Touch any of the 22 guild theme CSS forks under `wwwroot/css/site-*.css`.
- Add the `Enabled` flag onto the record itself — stays on `AiPlatformOptions.GeminiEnabled` (DI-injected `IOptions<AiPlatformOptions>` accessor in `_AiSelector.cshtml`).
- Migrate v1.1-era `NoWarn 1591;1573;1587` block in `DeckFlow.Web.csproj` — separate hygiene phase.
- Resolve the Gemini paste-limit issue — `DECKFLOW_GEMINI_ENABLED` stays the gate; flag still env-controlled.

</domain>

<decisions>
## Implementation Decisions

### AiPlatform record surface scope (AIPLATFORM-01)

- **D-01:** `AiPlatform` is a **data-only** sealed record with three string properties — `(Key, DisplayName, Description)` per design doc. No `Enabled` property on the record. No `ResponseExtractor` delegate on the record. No `Strategy` interface reference.

  Rationale: SC1's "encapsulate name, display label, enabled flag, response-extraction strategy" is satisfied by:
  - **name + display label** → `Key` + `DisplayName` (record fields)
  - **enabled flag** → stays on `AiPlatformOptions.GeminiEnabled` (DI-bound from `DECKFLOW_GEMINI_ENABLED` env var in `Program.cs:69-72`). Reading enabled state requires the same DI access whether it lives on the record or on options — putting it on the record would couple the immutable value object to a runtime configuration snapshot (anti-pattern for value objects).
  - **response-extraction strategy** → unified `<result>` regex in `JsonTextFormatterService.ExtractJsonPayload` is already AI-agnostic (Phase 10 hardening, all 3 AIs use identical `<result>...</result>` wrapper). No per-AI extractor strategy needed; the registry pattern at the prompt-builder layer is the "strategy" surface SC1 names.

  This means the record file in `DeckFlow.Web/Models/AiPlatform.cs` matches the design doc snippet at §"Layer 1" lines 42-95 exactly.

### Registry wiring + DI strategy (AIPLATFORM-02)

- **D-02:** **Manual DI registration. No Scrutor. No static `Default` shim.**

  `Program.cs` adds explicit lines:
  - 15 `services.AddSingleton<IAnalysisPromptVariant, ChatGptAnalysisPromptVariant>()` (etc., one per family × platform)
  - 5 `services.AddSingleton<AnalysisPromptVariantRegistry>()` (one per family)
  - 3 `services.Configure<AiPlatformOptions>(...)` already exists; no change

  Services that previously called the internal-static `Build*Prompt` method gain a ctor parameter `XxxPromptVariantRegistry`. Variant lookup is in-memory `ToDictionary(v => v.Platform)` per design doc §"Layer 2" line 130, called once at registry construction.

  Rejected alternatives:
  - **Scrutor auto-scan** — adds a NuGet dep to a public repo for ~20 lines of explicit wiring; magic registration grep can't trace; cost > benefit at this scale.
  - **Static `Default` fallback shim** — design doc shows both DI and static patterns; design doc itself says DI is "preferable for testability" (line 168). Keeping both creates two code paths to maintain. Dual-path violates one-way-to-do-it.
  - **Keyed services** (`AddKeyedSingleton<I, T>(key, ...)`) — .NET 10 supports it natively but registries already encapsulate dictionary lookup; keyed services would replace the registry surface with `IServiceProvider.GetKeyedService` calls in consumers. The registry boundary is more useful for testability than the keyed-services shortcut.

### Plan decomposition (execution strategy)

- **D-03:** **3 plans** by surface type. Each plan ends with a green build; mirrors Phase 14 D-07 wave pattern.

  - **Plan 15-01 (Value object + string-touchpoint migration):**
    - Create `DeckFlow.Web/Models/AiPlatform.cs` — sealed record + `All` + `Default` + `Normalize`.
    - Migrate `DeckAnalysisRequest.cs`, `DeckComparisonRequest.cs`, `MetaGapRequest.cs` setters to one-liner `AiPlatform.Normalize(value).Key`.
    - Migrate `Views/Shared/_AiSelector.cshtml` to iterate `AiPlatform.All` (Gemini `@if (geminiEnabled)` gate wraps **the option's render block**, not the loop — `AiPlatform.All` order stays canonical including Gemini, the radio is just hidden when flag off).
    - Migrate `Services/RequestContextParser.cs` zip-load + `Services/PacketArtifactStore.cs` 3 round-trip sites to call `AiPlatform.Normalize` defensively before string assignment.
    - **5 prompt builders still use the internal-static switch arms** — this plan keeps them intact (build stays green; string contract preserved).
    - Migrate existing `AiPlatformPhase10RoundTripTests.cs` `[InlineData]` → `[MemberData(nameof(AllPlatforms))]` driven from `AiPlatform.All`. Search-and-replace any other test files using the InlineData triple-platform pattern.
    - Build green. Manual smoke on one page (T1 ChatGPT round-trip) before committing.

  - **Plan 15-02 (Variant extraction + registries + DI):**
    - Create `DeckFlow.Web/Services/PromptBuilders/` parent directory with 5 family subdirectories (`Analysis/`, `SetUpgrade/`, `Comparison/`, `FollowUp/`, `MetaGap/`).
    - Per family, create: 1 interface (`IXxxPromptVariant`), 3 sealed variant classes (`ChatGpt`, `Claude`, `Gemini` prefixes), 1 registry. Total 25 new files.
    - Extract switch-arm bodies from `Services/DeckAnalysisPacketService.cs:844 / 1501`, `Services/DeckComparisonService.cs:698 / 1002`, `Services/MetaGapService.cs:471` into corresponding variant `Build(...)` methods. Method signatures match the original internal-static method's parameter list (per design doc §"Layer 2" lines 105-116).
    - Add 5 ctor parameters (one registry per family) to `DeckAnalysisPacketService`, `DeckComparisonService`, `MetaGapService` — registries replace the per-method switch.
    - Delete the now-dead switch-arm bodies + internal-static method bodies from the host services. Each switch deletion is one commit (5 commits) — clean git-blame.
    - `Program.cs` registers 15 variants + 5 registries + reroutes any service ctor that gained a parameter. Update DI test scaffolding in `DeckFlow.Web.Tests` if existing service-construction tests broke.
    - Build green after each commit. Smoke check T1/T3/T5 (one page per builder family) before plan commit.

  - **Plan 15-03 (4th-platform extension proof + verification):**
    - Add internal seam to `AiPlatform.cs`: either `[InternalsVisibleTo("DeckFlow.Web.Tests")] internal static IReadOnlyList<AiPlatform> AllForTesting(AiPlatform extra) => [..All, extra];` OR convert `All` to a settable internal via `internal static IReadOnlyList<AiPlatform> All { get; private set; } = ...` + `internal static void SetAllForTesting(IReadOnlyList<AiPlatform> next)`. Plan 15-03 author picks whichever yields a cleaner test (likely the helper method — no production-side mutability).
    - In `DeckFlow.Web.Tests/AiPlatformExtensionTests.cs`, define `AiPlatform.Test = new("Test", "Test Platform", "Stub for extension test")`, 5 stub `IXxxPromptVariant` implementations (return e.g., `"<test-build/>"`), 5 test-only `XxxPromptVariantRegistry` instances constructed with `[..ProductionVariants, testStub]`, and assertions that each registry's `Build(AiPlatform.Test, ...)` returns the stub's output without exception.
    - Full T1–T8 manual integration suite per `.planning/milestones/v1.2-MILESTONE-AUDIT.md` against post-refactor HEAD. Capture artifact-filename verify outputs (regex match on `*-{kind}-{ai}-{timestamp}.zip` pattern preserved).
    - Byte-identical artifact verification: zip-and-`sha256sum` a representative analysis/comparison/metagap on pre-refactor HEAD (`v1.3` tip before Phase 15 starts) vs post-15-02 HEAD for ChatGPT + Claude (Gemini round-trip stays gated). If byte-mismatch surfaces, hold for triage — likely zip-entry ordering or timestamp leak, both fixable.
    - Final build clean + push-and-watch CI on `v1.3` branch.

  Plans are sequential. Within Plan 15-02 the 5 family extractions can parallelize via `isolation="worktree"` if execution-phase chooses, but Phase 14's small-surface model (each rename gets its own commit) suggests sequential is also fine — 5 families × 1-2 commits each = 5-10 commits in the plan.

### Variant class file layout (AIPLATFORM-02)

- **D-04:** **Per-family subdirs, file-per-type.**
  ```
  DeckFlow.Web/Services/PromptBuilders/
    Analysis/
      IAnalysisPromptVariant.cs
      ChatGptAnalysisPromptVariant.cs
      ClaudeAnalysisPromptVariant.cs
      GeminiAnalysisPromptVariant.cs
      AnalysisPromptVariantRegistry.cs
    SetUpgrade/    (5 files, same pattern)
    Comparison/    (5 files, same pattern)
    FollowUp/      (5 files, same pattern)
    MetaGap/       (5 files, same pattern)
  ```
  Total: **25 new files** under `Services/PromptBuilders/`. Honors `.planning/codebase/CONVENTIONS.md` file-per-type rule (no `internal` co-location even where convenient). Grep-by-family is trivial; future 4th-platform PR is `+3-0` per family (only the new variant classes).

  Rejected: flat `PromptBuilders/` dir (25 files, harder to scan by family); co-located per-family file (5 files but breaks file-per-type, conflicts with Phase 14 D-07 anchor pattern).

### 4th-platform extension test design (AIPLATFORM-03, SC5)

- **D-05:** **Test-assembly-only test platform via `InternalsVisibleTo` seam.**

  `AiPlatform.cs` adds an internal helper (Plan 15-03 author picks):
  - Variant A: `internal static IReadOnlyList<AiPlatform> AllForTesting(AiPlatform extra) => [..All, extra];` (preferred — `All` stays immutable production-side)
  - Variant B: `internal static IReadOnlyList<AiPlatform> All { get; private set; }` + `internal static void SetAllForTesting(IReadOnlyList<AiPlatform>) {…}` (allows mutation around the test scope, requires careful cleanup)

  The test class (`DeckFlow.Web.Tests/AiPlatformExtensionTests.cs` or similar) constructs an `AiPlatform.Test` instance, 5 stub variant implementations (returning marker strings like `$"<test-build-{platform.Key}/>"`), and 5 test-only registry instances built from `[..productionRegistry.Variants, testStub]`. Assertions confirm `registry.Build(AiPlatform.Test, ...)` returns the stub marker for each of the 5 families.

  Rejected:
  - **Production `AiPlatform.Test` field** — leaks test surface into runtime (`_AiSelector.cshtml` would render it). `#if DEBUG` strip works but contaminates release builds with conditional code paths.
  - **Pure compile-time SC5 proof** — `var test = new AiPlatform(...); var registry = new AnalysisPromptVariantRegistry([stub]); Assert(registry.Build(test, ...) != null);` works but doesn't exercise the `AiPlatform.All`-iteration paths (Razor partial, test theory data sources), so doesn't prove SC5's actual claim about "no edits to switches/setters/Razor/parser".

### Preservation discipline (carried from Phase 13/14 D-10)

- **D-06:** What stays byte-identical this phase:
  - `"ChatGPT"` / `"Claude"` / `"Gemini"` string literal values as `AiPlatform.Key` (form post + zip `target_ai_platform` round-trip)
  - `request.TargetAiPlatform` property name on all 3 request DTOs
  - `targetAiPlatform` form field name (in `<input name="TargetAiPlatform" …>`)
  - `"chatgpt"` zip filename fallback (`PacketArtifactStore.cs:536-542`)
  - `DECKFLOW_GEMINI_ENABLED` env var name + `AiPlatformOptions.GeminiEnabled` property name + Razor `@if (geminiEnabled)` gate behavior
  - All 22 guild theme CSS forks (`wwwroot/css/site-*.css`) — untouched
  - Existing test pass count — every test currently green must remain green
  - `Co-Authored-By` trailer: NEVER added (CLAUDE.md commit hygiene)
  - All zip artifact entries' byte content (response prompts must produce identical bytes pre/post-refactor for ChatGPT + Claude on at least one canonical input deck)

### Claude's Discretion

- Specific method signatures inside each `IXxxPromptVariant` — must match the original internal-static method signature of the host service's switch-method exactly. Plan 15-02 author copies signatures verbatim from `DeckAnalysisPacketService.cs:844`, `DeckAnalysisPacketService.cs:1501`, `DeckComparisonService.cs:698`, `DeckComparisonService.cs:1002`, `MetaGapService.cs:471`.
- Order of family extraction within Plan 15-02 — alphabetical by family (Analysis → Comparison → FollowUp → MetaGap → SetUpgrade) is fine, OR by frequency of call (Analysis first since it's used on the most-visited page).
- Plan 15-03's internal-seam variant (A: `AllForTesting` helper vs B: settable `All`) — plan author picks based on which yields a cleaner test class. Variant A is preferred as a default.
- Whether to use a Theory `[MemberData(nameof(AllPlatforms))]` data source for the SC5 test (iterating all 5 family registries) vs 5 separate `[Fact]` methods. Either works; Theory is more concise.
- Whether to migrate the existing `AiPlatformPhase10RoundTripTests.cs` `[InlineData("ChatGPT")/("Claude")/("Gemini")]` blocks at the same commit as the value object lands (Plan 15-01) or in a follow-up commit within the same plan. Either order works as long as both happen inside 15-01.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + spec
- `.planning/REQUIREMENTS.md` — AIPLATFORM-01, AIPLATFORM-02, AIPLATFORM-03 acceptance gates
- `.planning/ROADMAP.md` — Phase 15 entry, Success Criteria 1..5 (SC5 = hypothetical 4th-platform extension proof)

### Design source of truth (binding)
- `.planning/milestones/v1.2-phases/10-claude-gemini-artifact-optimization/10-AISEL-PLATFORM-DESIGN.md` — **binding design** for the value object + registry pattern. `<summary>` block contents, `Normalize` semantics, registry shape, migration sequence, OCP-score reasoning all defined here. Plan 15-01 and 15-02 implement this design verbatim per D-01 / D-02 / D-04.

### Prior-phase context (preservation invariants)
- `.planning/phases/14-broader-codebase-name-vs-behavior-audit/14-CONTEXT.md` — **D-10 preservation list** carried verbatim into D-06 of this phase. Final class names (CLASSRENAME-01) locked: `DeckAnalysisRequest`, `DeckComparisonRequest`, `MetaGapRequest`, `DeckAnalysisPacketService`, `DeckComparisonService`, `MetaGapService`, `RequestContextParser`, `PacketArtifactStore`.
- `.planning/phases/14-broader-codebase-name-vs-behavior-audit/14-VERIFICATION.md` — what Phase 14 closed clean; Phase 15 must not regress GenerateDocumentationFile-ON build state on the 4 newly-flipped csproj files.
- `.planning/phases/13-chatgpt-class-rename-summary-doc-comments/13-CONTEXT.md` — D-03 XML `<summary>` tone anchor; D-07 preservation list source.
- `.planning/milestones/v1.2-MILESTONE-AUDIT.md` — **T1–T8 manual integration test spec**. Plan 15-03 re-runs full T1–T8 against post-refactor HEAD; SC4 verification gate.

### Project constraints
- `CLAUDE.md` — VSTest WSL constraint (rely on `dotnet build` clean + push-and-watch CI for test discovery); commit hygiene (no Co-Authored-By trailer; plain default author; one logical change per commit); Formatting constraint (do NOT run Format Document / Code Cleanup; preserve `{ get; init; }`; preserve raw-string indentation; preserve switch expressions; LF endings).
- `.editorconfig` — Allman braces, file-scoped namespace, separate-line attributes, raw-string preservation, `init` accessor preservation.
- `.gitattributes` — LF line endings repo-wide.
- `DeckFlow.Web/DeckFlow.Web.csproj` — `<GenerateDocumentationFile>true</GenerateDocumentationFile>` + `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` already on. Phase 15 leaves this block unchanged; new public types (`AiPlatform`, 5 interfaces, 5 registries) MUST carry `<summary>` because they are new public surface — but the NoWarn block in Web means missing-summary warnings won't break the build. Style anchor: `DeckFlow.Web/Services/CardLookupService.cs` + `DeckFlow.Web/Services/CommanderSpellbookService.cs`.

### Codebase intel
- `.planning/codebase/CONVENTIONS.md` — file-per-type rule (D-04 binding); naming conventions; xUnit Fake/Stub/Throwing test-double taxonomy.
- `.planning/codebase/ARCHITECTURE.md` — DI registration patterns in `Program.cs:50-189`.
- `.planning/codebase/INTEGRATIONS.md` — service composition patterns (registries fit established service-with-collaborators model).
- `.planning/codebase/TESTING.md` — test fixture conventions; `[InternalsVisibleTo("DeckFlow.Web.Tests")]` already wired in `DeckFlow.Web/AssemblyInfo.cs:3` (D-05 internal seam piggybacks on existing grant).

</canonical_refs>

<code_context>
## Existing Code Insights

### Confirmed touchpoints (scout 2026-05-17)

**Setters (3 sites — all internal-static switch):**
- `DeckFlow.Web/Models/DeckAnalysisRequest.cs:138-148`
- `DeckFlow.Web/Models/DeckComparisonRequest.cs:98`
- `DeckFlow.Web/Models/MetaGapRequest.cs:84`

**Per-AI prompt-builder switches (5 builder families, 5 internal-static switch sites):**
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs:844` (BuildAnalysisPrompt dispatcher)
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs:1501` (BuildSetUpgradePrompt dispatcher)
- `DeckFlow.Web/Services/DeckComparisonService.cs:698` (BuildComparisonPrompt dispatcher)
- `DeckFlow.Web/Services/DeckComparisonService.cs:1002` (BuildFollowUpPrompt dispatcher)
- `DeckFlow.Web/Services/MetaGapService.cs:471` (BuildMetaGapPrompt dispatcher)

**Round-trip + parser sites:**
- `DeckFlow.Web/Services/RequestContextParser.cs:44, 92, 198, 340` (string `TargetAiPlatform` parse + record property)
- `DeckFlow.Web/Services/PacketArtifactStore.cs:309-311, 385-387, 470-472` (3 zip-load assigns to request)
- `DeckFlow.Web/Services/PacketArtifactStore.cs:536, 539, 542` (filename helper — `"chatgpt"` fallback stays)

**UI surface (1 file):**
- `DeckFlow.Web/Views/Shared/_AiSelector.cshtml` — hard-coded 3-radio block; `@if (geminiEnabled)` gate currently wraps Gemini option only.

**Configuration (DO NOT touch except defensive Normalize calls):**
- `DeckFlow.Web/Configuration/AiPlatformOptions.cs` — `GeminiEnabled` flag (binding owner)
- `DeckFlow.Web/Program.cs:69-89` — env var binding + service registration

**Existing test surface to migrate:**
- `DeckFlow.Web.Tests/AiPlatformPhase10RoundTripTests.cs` — `[InlineData]` patterns migrate to `[MemberData(nameof(AllPlatforms))]` in Plan 15-01.

### Reusable patterns to follow

- **Sealed record + static registry** — see existing pattern of `AiPlatform.All`-style enumerables in `DeckFlow.Core` models (e.g., `MatchMode`, `SyncDirection`). Same idiom.
- **Internal test seam via `InternalsVisibleTo`** — already used by `CardLookupService.cs:106-121` test-seam ctor pattern; AiPlatform's internal `AllForTesting` helper follows the same convention.
- **DI service ctor injection** — every service in `Services/` injects via single public ctor (CONVENTIONS.md anchor pattern). Adding 1 registry param per host service follows established style.
- **XML `<summary>` doc-comment tone** — anchor `DeckFlow.Web/Services/CardLookupService.cs` + `DeckFlow.Web/Services/CommanderSpellbookService.cs` (Phase 13 D-03 + Phase 14 D-03 anchor). Terse single-sentence; same anchor binds new types in Plan 15-01/15-02.
- **Phase 14 wave decomposition** (D-07) — Phase 15's 3-plan split mirrors Phase 14's pattern. Each plan ends green. Per-rename single-purpose commits in Plan 15-02 mirror Phase 14 Plan 14-02.

### Integration points to watch

- **DI container** (`Program.cs:50-189`) — Plan 15-02 adds 15 + 5 = 20 lines of explicit registration. Group under a single `// AiPlatform prompt-builder strategy registries (Phase 15)` comment block at the appropriate point in the registration sequence (after services, before middleware).
- **`AssemblyInfo.cs:3`** — `[InternalsVisibleTo("DeckFlow.Web.Tests")]` already in place; D-05 internal seam needs no new attribute.
- **Razor `@model` directives** — `_AiSelector.cshtml` `@model string` stays as-is (model is still the currently-selected platform string `Key`, just consumed differently in the loop).
- **Existing 22 guild theme CSS forks** — untouched; `.ai-selector` + `.ai-selector__options` + `.ai-selector__option-label` classnames stay identical, so theme CSS keeps matching.

### Risks identified during scout

- **Byte-identical artifact bar (SC4)** — the registry pattern inserts a vtable hop in the prompt-builder call path. As long as the variant's `Build(...)` produces identical bytes to the corresponding switch-arm body (mechanical copy-paste), zip artifact contents stay identical. Risk: variable-capture or string-interpolation subtleties when extracting switch-arm bodies. Mitigation: side-by-side diff of variant body vs original switch arm before each extraction commit.
- **Ctor injection cascade** — adding 5 registry params to 3 host services (`DeckAnalysisPacketService`, `DeckComparisonService`, `MetaGapService`) — `DeckAnalysisPacketService` already has the longest ctor signature (~7 deps). Adding 2 more (`AnalysisPromptVariantRegistry`, `SetUpgradePromptVariantRegistry`) pushes it toward 9 deps. Acceptable for v1.3; god-class split is a separate milestone.
- **Test discovery in WSL** — VSTest is broken in WSL per CLAUDE.md. Plan 15-03 verification depends on push-and-watch CI for full test pass. `dotnet build --configuration Release` clean is the local gate; CI is the test-pass gate.

</code_context>

<specifics>
## Specific Ideas

- Use Phase 14 D-07's wave decomposition as the structural template for Plan 15-02's 5-family extraction (each family = its own commit cluster, smoke-test before commit).
- Plan 15-03's byte-identical artifact verify can lean on the existing `AiPlatformPhase10RoundTripTests.cs` test class as a starting harness — that file already exercises zip round-trip; extend it to assert byte-identical comparison against a pre-refactor snapshot.
- Design doc §"Acceptance criteria for the v1.3 plan" (lines 229-238) is essentially a verbatim sketch of SC1–SC5 — Plan 15-03's verification checklist can copy criterion 4 ("New unit tests asserting the registry pattern for each builder family") directly into its test plan.
- Each new `IXxxPromptVariant` interface needs an `AiPlatform Platform { get; }` property per design doc §"Layer 2" line 107 — used by the registry's `ToDictionary(v => v.Platform)` lookup.

</specifics>

<deferred>
## Deferred Ideas

- **Per-AI response extractor strategy on the record** — SC1 literal reading suggested adding `Func<string,string> ExtractJsonPayload` to the record. Rejected by D-01 because the unified `<result>` regex is already AI-agnostic. If a future AI ships with a different output format (e.g., bare JSON without `<result>` wrapper), the registry pattern can extend to a `ResponseExtractorRegistry` family with the same shape as the prompt-builder registries — no record surface change needed.
- **`Enabled` flag on the record** — Rejected by D-01. If multiple per-AI flags accumulate (e.g., `GeminiEnabled` + `ClaudePreviewEnabled` + ...), consider a `AiPlatform.IsEnabledIn(AiPlatformOptions options)` extension method or a `AiPlatformAvailabilityService` — but not as record fields.
- **Scrutor / convention-based variant registration** — Rejected by D-02. Revisit if variant count climbs past ~30 (e.g., 6 platforms × 5 families). For 15 variants, manual DI is more grep-able.
- **`DeckAnalysisPacketService` god-class split** — already on the v1.4+ refactor candidate list per CLAUDE.md and PROJECT.md "Out of scope". Phase 15 extraction (variants out of host service) is a partial step toward split but explicitly does NOT break the host service's public surface.
- **Migration of unified `<result>` regex into a per-platform `ResponseExtractor`** — out of scope; D-01 keeps the regex unified. Captured as a deferred candidate if future AIs ship divergent output formats.
- **v1.1-era `NoWarn 1591;1573;1587` removal from `DeckFlow.Web.csproj`** — carried over from Phase 14 deferred list. Phase 15 leaves the block in place; new public surface (`AiPlatform`, registries, interfaces, variants) MUST carry `<summary>` because they're new code, but the NoWarn block keeps the build green for v1.1-era undoc'd surface in Web.
- **Gemini paste-limit workaround** — explicit out-of-scope per ROADMAP.md + PROJECT.md "Out of Scope". `DECKFLOW_GEMINI_ENABLED` stays flag-gated for v1.3.

</deferred>

---

*Phase: 15-AiPlatform Value Object Refactor*
*Context gathered: 2026-05-17*
