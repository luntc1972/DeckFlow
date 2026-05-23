# Phase 15: AiPlatform Value Object Refactor - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-17
**Phase:** 15-aiplatform-value-object-refactor
**Areas discussed:** AiPlatform record surface scope, Registry wiring + DI strategy, Plan decomposition, Variant file layout + 4th-platform test design

---

## AiPlatform record surface scope

| Option | Description | Selected |
|--------|-------------|----------|
| Data-only 3-string per design doc | `(Key, DisplayName, Description)` only. Enabled stays on `AiPlatformOptions`; response extraction stays unified `<result>` regex. SC1 satisfied by registry pattern, not record fields. | ✓ |
| Add Enabled property only | `(Key, DisplayName, Description, bool Enabled)`. Razor filters `All.Where(p => p.Enabled)`. Couples record to options snapshot. | |
| Add Enabled + ResponseExtractor delegate | Most literal SC1 reading. Largest surface; introduces per-AI extraction even though unified regex already works. | |
| Add Enabled + Strategy interface ref | Couples record to DI graph (value object holding service refs — anti-pattern). | |

**User's choice:** Data-only 3-string per design doc (Recommended).
**Notes:** Matches `10-AISEL-PLATFORM-DESIGN.md` §"Layer 1" verbatim. SC1's "encapsulate enabled flag, response-extraction strategy" interpreted broadly: registry pattern at the prompt-builder layer is the strategy surface; `AiPlatformOptions.GeminiEnabled` remains the enabled-flag owner.

---

## Registry wiring + DI strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Manual DI, no static `Default` | 15 explicit `AddSingleton<IXxx, ...>` + 5 `AddSingleton<XxxRegistry>` lines in `Program.cs`. Zero new NuGet deps. | ✓ |
| Scrutor auto-scan | `services.Scan(...)` per family + 1 NuGet add. Magic registration, harder to grep. | |
| Manual DI + static `Default` for back-compat | Dual-path code paths (DI + static); violates one-way-to-do-it. | |
| Manual DI + keyed services (.NET 8+) | `AddKeyedSingleton<I>(key, ...)` + `GetKeyedService`. Replaces registry surface with `IServiceProvider` calls. | |

**User's choice:** Manual DI, no static `Default` (Recommended).
**Notes:** Design doc itself recommends DI for testability (line 168). Registry boundary is more useful for testability than keyed-services shortcut. ~20 explicit wiring lines is acceptable cost for clarity + zero new deps.

---

## Plan decomposition

| Option | Description | Selected |
|--------|-------------|----------|
| 3 plans | 15-01 value object + setters + Razor + RequestContextParser + PacketArtifactStore. 15-02 variant extraction + registries + DI. 15-03 4th-platform test + T1-T8 + byte-identical verify. | ✓ |
| 2 plans | All production code in 15-01; verify in 15-02. Large 15-01 diff (~25-30 files). | |
| 1 plan | Single big plan. Hard to bisect on T1-T8 fail. Violates Phase 14 D-08 build-green-between-commits. | |
| 4 plans | Split variants by family. More overhead; family extraction is mechanical. | |

**User's choice:** 3 plans (Recommended).
**Notes:** Mirrors Phase 14 D-07 wave pattern. Build green between plans. Plan 15-01 keeps string round-trip identical; Plan 15-02 swaps internal dispatch; Plan 15-03 proves SC5 + byte-identical artifacts.

---

## Variant file layout + 4th-platform test design

### Variant file layout

| Option | Description | Selected |
|--------|-------------|----------|
| Per-family subdirs, file-per-type | `Services/PromptBuilders/{Analysis,SetUpgrade,Comparison,FollowUp,MetaGap}/` with 5 files per family = 25 new files. Strict file-per-type. | ✓ |
| Flat `PromptBuilders/` dir | 25 files in one dir. Harder to scan by family. | |
| Co-locate per family (5 files) | Breaks file-per-type rule. Smaller diff but conflicts with Phase 13/14 CONVENTIONS anchor. | |

**User's choice:** Per-family subdirs, file-per-type (Recommended).
**Notes:** Honors `.planning/codebase/CONVENTIONS.md` file-per-type rule. Future 4th-platform PR is `+3-0` per family (new variant classes only).

### SC5 4th-platform test design

| Option | Description | Selected |
|--------|-------------|----------|
| Test-assembly-only via internal seam | `AiPlatform.cs` adds `internal AllForTesting(...)` helper. Test creates `AiPlatform.Test` + 5 stub variants + 5 test-only registries. Zero production runtime exposure. | ✓ |
| Production `AiPlatform.Test` field | `public static readonly Test = new(...)` in `All`. Runtime visible; requires `#if DEBUG` strip. Bleeds test code into prod. | |
| Pure compile-time SC5 proof | No `All`-mutation; new test class + stubs only. Doesn't exercise All-iteration paths (Razor partial). Weaker proof. | |

**User's choice:** Test-assembly-only via internal seam (Recommended).
**Notes:** `[InternalsVisibleTo("DeckFlow.Web.Tests")]` already exists in `AssemblyInfo.cs:3` — no new attribute. Plan 15-03 author picks variant A (`AllForTesting` helper, preferred) vs variant B (settable `All` getter) based on which yields cleaner test code.

---

## Claude's Discretion

- Specific `IXxxPromptVariant.Build(...)` method signatures — must match the original internal-static method signature of the host service's switch method exactly (verbatim copy from `DeckAnalysisPacketService.cs:844 / 1501`, `DeckComparisonService.cs:698 / 1002`, `MetaGapService.cs:471`).
- Order of family extraction within Plan 15-02 (alphabetical vs by call frequency — either fine).
- Plan 15-03's internal-seam variant (A `AllForTesting` helper vs B settable `All` getter) — A is preferred default.
- `[MemberData(nameof(AllPlatforms))]` Theory vs 5 separate `[Fact]` methods for SC5 test — Theory is more concise; either works.
- Whether to migrate `AiPlatformPhase10RoundTripTests.cs` `[InlineData]` blocks in the same commit as the value object lands or in a follow-up commit within Plan 15-01.

## Deferred Ideas

- Per-AI response extractor strategy on the record — rejected by D-01; revisit if future AI ships divergent output format (no `<result>` wrapper).
- `Enabled` flag on the record — rejected by D-01; if multi-flag accumulates, consider extension method or availability service.
- Scrutor / convention-based variant registration — rejected by D-02; revisit if variant count climbs past ~30.
- `DeckAnalysisPacketService` god-class split — explicit v1.4+ deferred per PROJECT.md / CLAUDE.md.
- Migration of unified `<result>` regex to per-platform `ResponseExtractor` — out of scope; D-01 keeps regex unified.
- `NoWarn 1591;1573;1587` removal from `DeckFlow.Web.csproj` — carried from Phase 14 deferred; Phase 15 does not address.
- Gemini paste-limit workaround — explicit out-of-scope per ROADMAP.md; `DECKFLOW_GEMINI_ENABLED` stays the gate.
