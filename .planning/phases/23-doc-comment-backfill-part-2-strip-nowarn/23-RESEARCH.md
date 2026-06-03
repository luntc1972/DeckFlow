# Phase 23: Doc-Comment Backfill — Part 2 + Strip NoWarn — Research

**Researched:** 2026-06-02
**Domain:** C#/.NET 10 XML documentation comments + MSBuild/Roslyn warning-gate configuration
**Confidence:** HIGH (authoritative compiler-driven inventory; methodology validated with an injected probe)

## Summary

Phase 23 finishes the DOC cluster: backfill XML `<summary>` (and where required `<param>`)
doc-comments on every remaining undocumented public member under `DeckFlow.Web`, then flip the
suppression so missing-doc warnings become a real build gate (DOC-02).

The single most important finding — and a trap baked into the current ROADMAP Success Criteria —
is that **CS1591/CS1573/CS1587 are suppressed in TWO places, not one**. The ROADMAP only mentions
the csproj `<NoWarn>` line. There is also a root-level `.editorconfig` (lines 94–96) that sets
`dotnet_diagnostic.CS{1591,1573,1587}.severity = none` **solution-wide**. I verified by experiment:
stripping ONLY the csproj `<NoWarn>` line changes nothing — a deliberately undocumented public type
still produced 0 warnings, and the ROADMAP's `dotnet build -warnaserror:CS1591` "gate" passed
trivially without verifying anything. The gate only becomes real when **both** suppressions are
removed. `.editorconfig` is git-tracked AND on CLAUDE.md's "Do Not Modify Without Explicit
Permission" list, so editing it is a deliberate, user-approved task — not an incidental edit.

With both suppressions disabled from a clean `obj/`, a full non-incremental Release build of
`DeckFlow.Web` surfaces **475 unique doc-warning sites across 54 files** (424× CS1591 missing-doc,
22× CS1573 missing-`<param>`, 29× CS1587 mis-placed-comment). This is the authoritative scope — far
larger than a file-level grep would report, and larger than "type summaries only" because **CS1591
fires on public methods and properties too**, not just types. The bulk (327 sites) lives in
`Models/` (which Phase 17 explicitly deferred to this phase). Good news: **Phase 22's new Content KB
types are already fully documented** (zero ContentKb files in the warning list) — the v1.4 new
surface from Phases 16/18/20/21/22 was authored with complete XML docs as it landed. Razor-generated
partials are a **non-issue**: .NET 10 MVC does not compile `.cshtml` into the main C# compile pass
at build time (0 `.cshtml.g.cs` generated), so no CS1591 originates from generated Razor code — the
SC2 "scoped 1591 retention" escape hatch is NOT needed.

**Primary recommendation:** Treat DOC-02 as a two-file flip (`DeckFlow.Web.csproj` `<NoWarn>` line
+ `.editorconfig` lines 94–96), gated behind user approval for `.editorconfig`. Backfill all 475
sites first (CS1591 add summaries; CS1573 complete `<param>` sets; CS1587 relocate misplaced `///`
blocks above attributes), then strip both suppressions, then prove the gate with the validated
"editorconfig-as-warning + probe" build. Make the strip the LAST task. Reuse the Phase 17 D-01/D-02
conventions but note D-02's partial-`<param>` policy directly conflicts with CS1573 (see Pitfall 2).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| XML doc-comments on public types/members | Source (`DeckFlow.Web/*.cs`) | — | Pure source annotation; compile-stripped, zero runtime effect |
| Missing-doc warning gate (DOC-02) | Build config (`.csproj` + `.editorconfig`) | Roslyn/MSBuild | Both files gate the diagnostic; csproj `<NoWarn>` and editorconfig severity are independent suppressors |
| Verification | Build (Windows `dotnet.exe` over WSL) | CI push-watch | VSTest unreliable in WSL; `dotnet build` clean is the local gate |

## Standard Stack

No new packages. This is a config + source-comment phase. Relevant existing settings:

| Setting | Location | Current Value | Phase 23 Action |
|---------|----------|---------------|-----------------|
| `GenerateDocumentationFile` | `DeckFlow.Web.csproj:38` | `true` | Keep — produces the XML doc + is what *would* enable CS1591 |
| `<NoWarn>` | `DeckFlow.Web.csproj:40` | `$(NoWarn);1591;1573;1587` | **Remove** (DOC-02) |
| `dotnet_diagnostic.CS1591.severity` | `.editorconfig:94` | `none` | **Change to `warning`** (DOC-02) — REQUIRES USER APPROVAL (Do-Not-Modify list) |
| `dotnet_diagnostic.CS1573.severity` | `.editorconfig:95` | `none` | **Change to `warning`** (DOC-02) — same approval |
| `dotnet_diagnostic.CS1587.severity` | `.editorconfig:96` | `none` | **Change to `warning`** (DOC-02) — same approval |

**Installation:** none.

## Package Legitimacy Audit

Not applicable — no external packages installed in this phase.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DOC-01 | All public types/members in `DeckFlow.Web/{Controllers,Services,Models,Models/Api,Infrastructure,Security}/` carry XML doc-comments | Authoritative 475-site inventory below (compiler-derived); per-file counts; CS1591/1573/1587 split; remediation per code |
| DOC-02 | `DeckFlow.Web.csproj` no longer suppresses CS1591/1573/1587; `dotnet build -warnaserror:CS1591` succeeds from clean `obj/` | TWO-file suppression discovered (csproj + `.editorconfig`); validated gate-proof methodology; SC3 command as written is a no-op without the editorconfig change |
</phase_requirements>

> Note on scope vocabulary: there is **no `ViewModels/` directory** in `DeckFlow.Web`. View models
> live under `Models/` (e.g. `DeckDiffViewModel.cs`, `CommanderCategoryViewModel.cs`,
> `Models/Admin/AdminHarvestViewModel.cs`). The ROADMAP/REQUIREMENTS `ViewModels/` reference maps to
> `Models/` + `Models/Admin/`. Plan against actual directories: `Controllers/`, `Services/`,
> `Models/`, `Models/Api/`, `Models/Admin/`, `Infrastructure/`. `Security/` had **0** warnings.

## Architecture Patterns

### The doc-warning gate has TWO independent suppressors

```
CS1591/1573/1587 emitted?  ──► requires GenerateDocumentationFile=true  (✓ already on)
                                       AND
                           ──► NOT suppressed by csproj <NoWarn>          (currently suppressed)
                                       AND
                           ──► NOT suppressed by .editorconfig severity   (currently = none)
```
All three conditions must hold for a warning to surface. The csproj `<NoWarn>` and the
`.editorconfig` severity are **independent** — removing one leaves the other in force. The ROADMAP
SC2/SC3 reference only the csproj; they silently rely on the editorconfig staying suppressed, which
makes `-warnaserror:CS1591` a false-green. **The plan MUST address both files.**

### CS1591 fires on members, not just types

`Models/MetaGapResponse.cs` alone has **58** warning sites for a handful of types — because every
public property of every public record triggers CS1591 when undocumented. This is why the file-level
`grep -L '<summary>'` (ROADMAP SC1) undercounts so badly: a record with a type-`<summary>` but
undocumented properties passes the grep yet fails the compiler 58 times.

### Razor partials do not enter the C# compile (SC2 escape hatch unneeded)

Verified: `dotnet build` generates **0** `.cshtml.g.cs` files; no `AddRazorRuntimeCompilation`; the
Razor SDK resolves but views are not compiled into the assembly's CS pass at build time on this .NET
10 setup. No CS1591 originates from generated Razor. **Recommendation: do the full strip; do NOT add
a `Condition=`-scoped 1591 retention.** (If a future SDK bump changes this, the validated probe build
below will catch it — but today it is clean.)

### Anti-Patterns to Avoid
- **Stripping only the csproj `<NoWarn>` and declaring victory** — the editorconfig still suppresses;
  the gate is a no-op. This is the central trap of this phase.
- **Adding `<param>` only on multi-arg methods (Phase 17 D-02 policy)** — CS1573 fires on ANY
  undocumented param once a method has any documented param. Partial `<param>` sets fail the strip.
- **Inlining `[Attribute]` onto the declaration line while fixing CS1587** — forbidden by CLAUDE.md.
  Fix CS1587 by moving the `///` block ABOVE the existing attributes, leaving attributes untouched.
- **Running Format Document to "tidy" the backfilled files** — forbidden (CLAUDE.md R-6).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Finding undocumented members | A custom grep/awk scanner | The Roslyn compiler via `dotnet build` with both suppressions disabled | grep undercounts (file-level blind spot + member-level misses); the compiler is exhaustive and authoritative — proven this session |
| Verifying the gate works | Trusting `-warnaserror:CS1591` exit code alone | The "editorconfig-as-warning + injected probe" build (see Code Examples) | `-warnaserror:CS1591` passes trivially when CS1591 is suppressed upstream; the probe proves the warning actually fires |

**Key insight:** In this repo, the only trustworthy inventory is "build with both suppressors off
from a clean `obj/` and read the warning list." Everything else (grep, training intuition about
`GenerateDocumentationFile` auto-enabling 1591) is wrong here.

## Runtime State Inventory

This phase edits source comments + build config only. No stored data, no live-service config, no
OS-registered state, no secrets, no build artifacts carry the changed state.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — doc comments are compile-stripped; no DB/datastore touched | none |
| Live service config | None — no deployed config references doc state | none |
| OS-registered state | None | none |
| Secrets/env vars | None | none |
| Build artifacts | `DeckFlow.Web.xml` (generated doc file) regenerates from source on every build; not committed | none (verified `bin/` is build output) |

**Nothing found in any category** — verified by inspection; doc-comments and warning-severity config
have zero runtime/persisted footprint.

## Authoritative Undocumented Inventory (compiler-derived, 2026-06-02)

**Method:** clean `obj/bin`, temporarily strip BOTH `DeckFlow.Web.csproj` `<NoWarn>` line AND
`.editorconfig` CS1591/1573/1587 severity (set to `warning`), `dotnet build -c Release
--no-incremental`. Methodology validated by injecting a temporary undocumented `public class` probe
and confirming CS1591 fired on it (2 sites: type + property). Both files restored to byte-identical
HEAD state afterward; `git diff` confirmed empty.

**Totals:** **475 unique warning sites · 54 files**

| Warning | Count | Meaning | Remediation |
|---------|-------|---------|-------------|
| CS1591 | 424 | Missing XML comment for publicly visible type or member | Add `<summary>` (or `<inheritdoc/>` for interface impls) on the type AND each undocumented public member/property |
| CS1573 | 22 | Parameter has no matching `<param>` (method already has some param docs) | Complete the `<param>` set — every param documented or none |
| CS1587 | 29 | XML comment not on a valid language element (placed after attributes) | Relocate the `///` block ABOVE the `[Attribute(...)]` lines (do not inline attributes) |

### Per-directory rollup
| Directory | Sites | Notes |
|-----------|-------|-------|
| `Models/` | 277 | v1.1-era DTOs/records/view models — Phase 17 deferred these here |
| `Models/Api/` | 36 | `SuggestionResponses.cs` (36) |
| `Models/Admin/` | 14 | `AdminHarvestViewModel` (10) + `MaintenanceViewModel` (4) |
| `Controllers/` | 96 | DeckController.cs (55) holds most CS1587; CS1587 also in CommanderController.cs (28/40/93) + Api/DeckSyncApiController.cs (17) [see footnote] |
| `Services/` | 49 | Includes Phase-17-"done" interfaces now failing on members/params |
| `Infrastructure/` | 3 | `BasicAuthMiddleware.cs` |
| `Security/` | 0 | already clean |

### Per-file counts (the work list)
```
 58  Models/MetaGapResponse.cs
 55  Controllers/DeckController.cs            (holds MOST of the 29 CS1587 — but NOT all; see footnote)
 36  Models/Api/SuggestionResponses.cs
 28  Models/DeckComparisonResponse.cs
 26  Models/DeckAnalysisResponse.cs
 24  Models/DeckDiffViewModel.cs
 16  Models/SetUpgradeResponse.cs
 16  Models/EdhTop16Entry.cs
 11  Models/FeedbackItem.cs
 10  Models/WorkflowStepTabsModel.cs
 10  Models/DeckDiffRequest.cs
 10  Models/Admin/AdminHarvestViewModel.cs
 10  Controllers/Admin/AdminFeedbackController.cs   ← Phase-17-"done" file, members still bare
  9  Services/ICategoryKnowledgeStore.cs            ← Phase-17 file (CS1573 param gaps)
  9  Models/CommanderCategoryViewModel.cs
  8  Models/CommanderBracketCatalog.cs
  8  Controllers/Admin/AdminHarvestController.cs
  7  Models/DeckConvertRequest.cs
  7  Models/CedhMetaTimePeriod.cs
  7  Controllers/CommanderController.cs             ← Phase-17 file
  6  Services/IFeedbackStore.cs                     ← Phase-17 file (CS1573)
  6  Services/CategoryKnowledgeStore.cs             ← Phase-17 file (CS1573)
  6  Models/ScryfallSetOption.cs
  6  Models/DeckConvertViewModel.cs
  5  Services/AdminBruteForceTrackerStore.cs
  5  Models/FeedbackSubmission.cs
  5  Models/FeedbackListQuery.cs
  5  Models/CategorySuggestionMode.cs
  4  Services/HelpContentService.cs
  4  Models/FeedbackType.cs
  4  Models/FeedbackStatus.cs
  4  Models/CommanderCategorySummary.cs
  4  Models/Admin/MaintenanceViewModel.cs
  4  Controllers/Api/ArchidektCacheJobsController.cs ← Phase-17 file
  3  Services/VersionService.cs
  3  Services/Harvest/HarvestStatsAggregator.cs
  3  Services/FeedbackStore.cs                       ← Phase-17 file
  3  Models/DeckInputSource.cs
  3  Models/CedhMetaSortBy.cs
  3  Infrastructure/BasicAuthMiddleware.cs
  3  Controllers/HelpController.cs
  3  Controllers/FeedbackController.cs               ← Phase-17 file
  2  Services/EdhTop16Client.cs                      ← Phase-17 file
  2  Services/DeckFlowDatabaseConnectionFactory.cs   ← Phase-17 file
  2  Services/CardLookupService.cs
  2  Models/AnalysisQuestionCatalog.cs
  2  Controllers/Api/DeckSyncApiController.cs
  2  Controllers/AboutController.cs
  1  Services/ScryfallSetService.cs                  ← Phase-17 file
  1  Services/MetaGapService.cs
  1  Services/IVersionService.cs
  1  Services/DeckComparisonService.cs
  1  Controllers/Api/SuggestionsApiController.cs      ← Phase-17 file
  1  Controllers/Admin/AdminLandingController.cs
```

**Phase-17 regression note:** ~13 files Phase 17 marked "done" reappear here. Phase 17 added
type-level `<summary>` (+ partial `<param>` per D-02) but did NOT document every public member, and
its partial `<param>` policy now trips CS1573. These files need member/param completion, NOT
re-documentation of the types.

## Common Pitfalls

### Pitfall 1: The hidden `.editorconfig` suppression (DOC-02's real blocker)
**What goes wrong:** Plan strips only `DeckFlow.Web.csproj` `<NoWarn>`; build stays green; everyone
believes the gate is live. It is not — `.editorconfig:94-96` still silences the diagnostics
solution-wide.
**Why it happens:** ROADMAP SC2/SC3 name only the csproj. The editorconfig block was added (per its
own comment) to mirror the csproj suppression for IDE/analyzer parity.
**How to avoid:** DOC-02 = remove the csproj `<NoWarn>` line AND change the three `.editorconfig`
severities from `none` to `warning` (or delete the three lines). Then prove with the validated probe
build.
**Warning signs:** `dotnet build -warnaserror:CS1591` passes immediately on a known-undocumented tree.

### Pitfall 2: CS1573 vs Phase 17's partial-`<param>` policy
**What goes wrong:** Files Phase 17 "completed" fail the strip because a method/record has
`<summary>` + SOME `<param>` tags but not all. CS1573 demands all-or-nothing param docs.
**Why it happens:** Phase 17 D-02 deliberately added `<param>` only on ≥2-real-arg methods and only
for non-obvious params — a reasonable noise-reduction rule that is incompatible with the strict
compiler gate.
**How to avoid:** For each CS1573 site, either document EVERY param or remove the partial `<param>`
set. Plan should pick a rule (recommend: complete the set — `FeedbackRequestContext` etc. are small).
**Warning signs:** CS1573 appears on `ICategoryKnowledgeStore`, `IFeedbackStore`, `EdhTop16Client`,
`ScryfallSetService`, `CategoryKnowledgeStore` (all Phase-17 files).

### Pitfall 3: CS1587 needs a reorder, not an addition (R-6 tension)
**What goes wrong:** 29 sites (26 in `DeckController.cs`) have a `///` block placed AFTER the
`[HttpGet(...)]` / `[ServiceUnavailable(...)]` attributes. Adding a second comment won't fix it; the
existing comment must move above the attributes.
**Why it happens:** Doc comment authored below the attribute decoration. The compiler requires
`/// comment` → attributes → declaration order.
**How to avoid:** Move the existing `///` block up above the attribute lines. Touch only those lines;
do NOT inline the attribute, do NOT reformat. This is a small multi-line move per site.
**Warning signs:** `warning CS1587: XML comment is not placed on a valid language element`.

### Pitfall 4: Ordering — strip LAST (ROADMAP Pitfall 8)
**What goes wrong:** Stripping the suppression before all 475 sites are documented turns the build
red mid-phase and blocks every other task.
**How to avoid:** Sequence all backfill tasks first; make the two-file suppression strip the final
task, immediately followed by the validation build.
**Warning signs:** A wave plan that edits the csproj/editorconfig before the Models backfill.

### Pitfall 5: `.editorconfig` is on the Do-Not-Modify list
**What goes wrong:** An agent edits `.editorconfig` without approval, violating CLAUDE.md.
**How to avoid:** The plan must call out the `.editorconfig` edit as a deliberate, user-approved part
of DOC-02 (it is unavoidable to complete the requirement). Surface it explicitly; get sign-off.
**Warning signs:** `.editorconfig` change buried silently inside a "strip NoWarn" task.

### Pitfall 6: VSTest unreliable in WSL — build is the gate
**What goes wrong:** Relying on `dotnet test` in WSL, which is flaky here.
**How to avoid:** Use `/mnt/c/Program Files/dotnet/dotnet.exe` for `dotnet build -c Release` as the
local gate; defer the full test suite to CI push-watch. Doc comments are compile-stripped — no
runtime regression is possible, so the build-clean + push-watch gate is sufficient.

## Code Examples

### Authoritative inventory build (what was run this session)
```bash
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"
# 1. clean
rm -rf DeckFlow.Web/obj DeckFlow.Web/bin
# 2. temporarily disable BOTH suppressors (back up first!)
#    csproj: remove the line  <NoWarn>$(NoWarn);1591;1573;1587</NoWarn>
#    editorconfig: set the three dotnet_diagnostic.CS15xx.severity = warning
# 3. full non-incremental build, capture doc warnings
"$DOTNET" build DeckFlow.Web/DeckFlow.Web.csproj -c Release --no-incremental /nologo -v:m \
  | grep -oE 'DeckFlow\.Web\\[^(]+\.cs\([0-9]+,[0-9]+\): warning CS(1591|1573|1587)' \
  | sed 's#\\#/#g' | sort -u
# 4. RESTORE both files from backup; verify `git diff` is empty
```

### Gate-proof methodology (REQUIRED for SC3 verification — do not trust exit code alone)
```bash
# Inject a probe to prove the warning actually fires, THEN remove it.
cat > DeckFlow.Web/__TempUndocProbe.cs <<'EOF'
namespace DeckFlow.Web;
public sealed class __TempUndocProbe { public int X { get; init; } }
EOF
# With both suppressors removed, this MUST produce CS1591 on __TempUndocProbe (type + .X).
# If it does NOT, a suppressor is still active — the gate is fake.
rm DeckFlow.Web/__TempUndocProbe.cs   # always clean up
```

### CS1587 fix shape (relocate, don't add)
```csharp
// BEFORE (CS1587 — comment after attribute):
    [HttpGet("/")]
    /// <summary>Renders the landing hub listing every tool in the app.</summary>
    public IActionResult Home()

// AFTER (comment above attribute; attribute line byte-identical):
    /// <summary>Renders the landing hub listing every tool in the app.</summary>
    [HttpGet("/")]
    public IActionResult Home()
```

### CS1573 fix shape (complete the param set)
```csharp
// BEFORE (CS1573 — only Ip documented):
/// <summary>Captures request-side metadata stored with a feedback submission.</summary>
/// <param name="Ip">Raw client IP; salted+hashed before persistence.</param>
public sealed record FeedbackRequestContext(string? Ip, string? UserAgent, string? PageUrl, string? AppVersion);

// AFTER (every positional param documented):
/// <summary>Captures request-side metadata stored with a feedback submission.</summary>
/// <param name="Ip">Raw client IP; salted+hashed before persistence.</param>
/// <param name="UserAgent">Client User-Agent header, or null when absent.</param>
/// <param name="PageUrl">Page the feedback was submitted from, or null.</param>
/// <param name="AppVersion">App version string captured at submit time, or null.</param>
public sealed record FeedbackRequestContext(string? Ip, string? UserAgent, string? PageUrl, string? AppVersion);
```

## Convention Reuse (from Phase 17)

Carry forward verbatim (Phase 17 CONTEXT D-01..D-04, validated by Codex 4-round review):
- **D-01:** Interface owns the prose (`<summary>` + `<param>`/`<returns>`); implementing class +
  members use `<inheritdoc/>`. ~co-located interface+impl pairs.
- **D-01a:** Standalone classes/records (no interface) get full `<summary>` directly.
- **D-02 (AMEND for this phase):** Phase 17 added `<param>` only on ≥2-real-arg / non-obvious-return
  methods. **Under the strip, CS1573 forces all-or-nothing** — when a method/record carries any
  `<param>`, document every param. Recommend: complete every param set on the 22 CS1573 sites.
- **D-03:** Seed summaries from CLAUDE.md Component-Responsibilities one-liners where present; verify
  against code; write fresh otherwise. Voice: explain WHY not what; house style of existing summaries.
- **D-04:** Every record gets a type-`<summary>`; add `<param>` on positional records (now required
  wherever any exists — see D-02 amendment).

**CLAUDE.md formatting landmines (binding, R-6 + Constraints):**
- Never inline `[Attribute]` onto the declaration line (matters for the 29 CS1587 reorders).
- Never re-indent C# raw-string literals (several Models hold prompt raw-strings).
- Never convert `{ get; init; }` → `{ get; }` (System.Text.Json drops get-only props).
- Preserve LF endings (`.gitattributes`). Touch only the lines you touch. No Format Document.

## State of the Art

| Old (assumed) | Current (verified this session) | Impact |
|---------------|----------------------------------|--------|
| `GenerateDocumentationFile=true` auto-enables CS1591 | On this .NET 10 SDK (10.0.300), with `.editorconfig` severity `none`, it does NOT — the diagnostic stays suppressed | The ROADMAP `-warnaserror:CS1591` "gate" is a no-op until editorconfig is changed |
| Suppression lives in csproj only | TWO suppressors: csproj `<NoWarn>` + `.editorconfig:94-96` | DOC-02 must edit both files |
| Razor partials may emit CS1591 (SC2 escape hatch) | .NET 10 MVC does not compile `.cshtml` into the build's C# pass (0 `.cshtml.g.cs`) | Full strip is safe; scoped 1591 retention NOT needed |
| Phase 17 files are "done" | ~13 reappear (member/param gaps + CS1573) | Plan must re-touch them — do not assume done |

## Sequencing / Branch State (ROADMAP Pitfall 8)

- Current branch `v1.4`; HEAD `e9471fb`. Phase 22 is code-complete + merged on this branch (commits
  `679b0df`…`62ba4d7` present). Phases 16/18/20/21 surface is on-branch.
- **Phase 22 / Content KB types are already fully documented** — zero ContentKb files in the warning
  inventory. So the 475-site list is complete with respect to all merged v1.4 surface.
- The two-file suppression strip MUST be the LAST task, after all 475 sites are documented.

## Test Impact

- Doc comments are compile-stripped → **no runtime change possible** → test suite cannot regress.
- The csproj/editorconfig edits do not change test compilation: **NoWarn is per-project** (only
  `DeckFlow.Web.csproj` has it; test projects have none). The `.editorconfig` change DOES apply
  solution-wide, but the test projects are already documented (verified: `DeckFlow.Web.Tests` builds
  clean with `GenerateDocumentationFile=true` and no NoWarn — its public test classes carry
  `<summary>`). Changing editorconfig severity to `warning` will not surface new warnings in test
  projects, because they are already documented. **Confirm with a full-solution build** after the
  strip as the final guard.
- Recommended gate: `dotnet build -c Release` (Windows `dotnet.exe`) clean across the solution, then
  CI push-watch for the xUnit suites.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK (Windows, over WSL) | build gate | ✓ | 10.0.300 (`/mnt/c/Program Files/dotnet/dotnet.exe`) | — |
| Razor SDK | build | ✓ | Microsoft.NET.Sdk.Razor 10.0.300 (resolved) | — |
| VSTest in WSL | test run | ✗ (unreliable per CLAUDE.md) | — | CI push-watch / build-clean gate |

No blocking gaps.

## Validation Architecture

> `.planning/config.json` not inspected for `nyquist_validation`; this is a comment+config phase
> with zero runtime behavior, so test-mapping is degenerate. Documented for completeness.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`) |
| Quick run | `dotnet build -c Release` (build-clean is the real gate; doc comments are compile-stripped) |
| Full suite | xUnit via CI push-watch (VSTest unreliable in WSL) |

### Phase Requirements → Verification Map
| Req | Behavior | Type | Command | Exists? |
|-----|----------|------|---------|---------|
| DOC-01 | All 475 sites documented | build | `dotnet build -c Release` with both suppressors OFF → 0 doc warnings | ✅ (this session's harness) |
| DOC-02 | Gate live + provably real | build | strip both → probe injection fires CS1591 → real build 0 warnings | ✅ |

### Sampling Rate
- **Per task commit:** `dotnet build DeckFlow.Web/DeckFlow.Web.csproj -c Release` (clean).
- **Phase gate (after strip):** probe-validated build (both suppressors removed) → 0 doc warnings,
  then probe confirms the gate fires, then full-solution build clean.

### Wave 0 Gaps
- None — no test files needed. Verification is build-driven. (If `/gsd:verify-work` wants an
  automated guard, add a CI step running the two-suppressor build and failing on any CS1591/1573/1587
  — but that is optional polish, not a Wave 0 blocker.)

## Security Domain

Not applicable in substance: this phase adds documentation comments and flips two warning-severity
settings. No auth, session, access-control, input-validation, or cryptography surface is touched.
No ASVS category applies. STRIDE: none — zero attack surface change (comments are compile-stripped;
the warning gate is build-time only).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Razor partials remain non-compiled in the C# pass through phase execution (no SDK bump that changes Razor compile-on-build) | Architecture Patterns / SOTA | LOW — the probe-validated build would surface any new Razor CS1591; plan's final validation catches it |

**Everything else in this research is `[VERIFIED]` via direct `dotnet build` experiments this
session** (inventory counts, two-file suppression, gate-proof probe, Phase-22-already-documented,
test-project independence, repo restored clean).

## Open Questions

1. **Should DOC-02 delete the three `.editorconfig` lines or set them to `warning`?**
   - What we know: both make the diagnostic surface; setting to `warning` keeps an explicit,
     greppable record; deleting relies on SDK defaults (which, per this session, leave 1591
     effectively off unless `TreatWarningsAsErrors`/explicit severity is set).
   - Recommendation: **set to `warning`** (explicit, self-documenting, guarantees the gate is live
     regardless of SDK default drift). Get user sign-off on the `.editorconfig` edit either way.

2. **Does the team want `-warnaserror:CS1591` wired into the csproj/CI permanently, or just
   verified once?** ROADMAP SC3 says the command must "succeed from clean obj." Recommend leaving it
   as a CI/verification step rather than `TreatWarningsAsErrors` in the csproj (a stray future
   undocumented member should warn, not break local dev builds) — but this is a user decision.

## Sources

### Primary (HIGH confidence — direct experiment this session)
- `dotnet build` (Windows SDK 10.0.300) of `DeckFlow.Web.csproj` with suppressors toggled — full
  475-site inventory; probe-validated gate; per-file/per-code breakdown.
- `DeckFlow.Web/DeckFlow.Web.csproj:38,40` — `GenerateDocumentationFile`, `<NoWarn>`.
- `.editorconfig:93-96` — the second suppressor (CS1591/1573/1587 severity = none).
- `.planning/phases/17-*` CONTEXT/SUMMARY/REVIEWS/UAT — Phase 17 conventions (D-01..D-04) + lessons.
- `CLAUDE.md` — formatting constraints (R-6), Do-Not-Modify list (incl. `.editorconfig`), VSTest/WSL.
- `.planning/ROADMAP.md` §Phase 23, §Phase 17; `.planning/REQUIREMENTS.md` DOC-01/DOC-02.

### Secondary
- Per-project csproj scan — confirmed only `DeckFlow.Web` carries `<NoWarn>`; all others have
  `GenerateDocumentationFile=true` and build clean (so the editorconfig is the solution-wide gate).

## Project Constraints (from CLAUDE.md)

- **R-6 / Formatting:** touch only the lines you touch; NO Format Document / Code Cleanup; never
  inline `[Attribute]` (critical for the 29 CS1587 reorders); never re-indent raw-string literals;
  never convert `{ get; init; }`→`{ get; }`; preserve LF endings.
- **Do Not Modify Without Explicit Permission:** `.editorconfig` is on this list. DOC-02 cannot be
  completed without editing it — surface this and get user approval before the strip task.
- **Testing:** VSTest unreliable in WSL → `dotnet build` clean (Windows `dotnet.exe`) + CI
  push-watch is the gate. No new packages, no new test framework.
- **Commits:** plain default-author (no Co-Authored-By); commit per logical change.
- **Delegation (global CLAUDE.md):** Codex writes the doc comments; Claude plans/reviews/verifies.
  Route PLAN.md through Codex peer review before execute.

## Metadata

**Confidence breakdown:**
- Inventory (475 sites / 54 files / code split): HIGH — compiler-derived, probe-validated.
- Two-file suppression + gate behavior: HIGH — directly experimentally confirmed (this is the key
  finding the ROADMAP missed).
- Razor non-compilation: HIGH — 0 `.cshtml.g.cs`, no runtime-compilation, verbose-build inspected.
- Phase-22-already-documented: HIGH — no ContentKb files in warning list.
- Conventions: HIGH — lifted from shipped Phase 17 artifacts.

**Research date:** 2026-06-02
**Valid until:** ~2026-07-02 (stable; re-run the inventory build at plan/execute time only if new
commits land on `v1.4`, since the 475 count is a point-in-time snapshot).

> **CS1587 distribution footnote (correction):** The 29 CS1587 sites are NOT all in
> DeckController.cs. Verified ~4 also live in CommanderController.cs (`///` blocks below
> attributes at lines 28, 40, 93) and Api/DeckSyncApiController.cs (line 17). The per-file split
> shifts as HEAD moves — executor MUST re-run the suppressors-off inventory build at execute
> time to get the authoritative per-file CS1587 distribution; relocate EVERY CS1587 the compiler
> reports across the Controllers tree.
