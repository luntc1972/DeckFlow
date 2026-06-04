# Phase 29: Core XML-Doc Backfill + Gate Widen — Research

**Researched:** 2026-06-04
**Domain:** C#/.NET 10 XML documentation comments + MSBuild/Roslyn warning-gate configuration (DeckFlow.Core)
**Confidence:** HIGH (authoritative compiler-driven inventory via probe build; methodology validated against Phase 23 precedent)

## Summary

Phase 29 is the DeckFlow.Core counterpart of Phase 23. Phase 23 documented DeckFlow.Web (475 sites) and set up a CS1591 gate scoped to `[DeckFlow.Web/**.cs]` in `.editorconfig`. Phase 23 explicitly deferred DeckFlow.Core, leaving the global `[*.cs]` suppressor (`severity = none`) intact. Phase 29 documents the remaining Core public surface and widens the gate.

The authoritative compiler-driven inventory (probe build method from Phase 23, adapted for Core — see Code Examples) finds **90 unique undocumented public sites across 29 files** at the time of this research. The previously-cited "186" count was accurate at Phase 23 research time (2026-06-02); Phases 19–28 added extensively-documented new Core code (Content KB, schema normalization, distillation), which reduced the pending backfill count from 186 to 90. The 90-site figure is the authoritative work scope.

The suppressor situation for Core is **simpler than Phase 23's Web situation** — only ONE suppressor is active. DeckFlow.Core.csproj has `GenerateDocumentationFile=true` but **no** `<NoWarn>` line. The only suppressor is the global `[*.cs]` section in `.editorconfig` (lines 97–99: `dotnet_diagnostic.CS1591/1573/1587.severity = none`). The gate widen requires adding a new `[DeckFlow.Core/**.cs]` scoped section (identical pattern to the existing `[DeckFlow.Web/**.cs]` section at lines 111–115). No csproj changes are needed.

Scope of work: 84 CS1591 (missing `<summary>`) + 6 CS1573 (missing `<param>` on methods that already have partial param docs) + 0 CS1587. The CS1573 sites are in two files: `MoxfieldApiDeckImporter.cs` (1 site: missing `executeAsync` param) and `CategoryKnowledgeRepository.cs` (5 sites across 2 methods: missing `board`, `deckCount`, `boardFilter`, `deckCountIncrement` params). No CS1587 (misplaced `///`) warnings exist in Core — unlike Phase 23's Web work, no comment-relocation tasks are required.

**Primary recommendation:** Follow Phase 23's proven plan structure: Wave 1 = parallel backfill plans (one per logical folder group), Wave 2 = single gate-widen plan (the final commit of the phase). The gate-widen plan must be explicitly non-autonomous with a human-verify checkpoint, because `.editorconfig` is on CLAUDE.md's Do-Not-Modify list.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| HSK-01 | DeckFlow.Core XML-doc backfill (90 sites as of 2026-06-04) complete and doc-warning gate widened to `[DeckFlow.Core/**.cs]` in the final commit — build clean, 0 new warnings | Authoritative compiler-driven inventory: 90 unique sites / 29 files; single suppressor (editorconfig only); gate-widen edit identified precisely |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| XML doc-comments on public types/members | Source (`DeckFlow.Core/*.cs`) | — | Pure source annotation; compile-stripped, zero runtime effect |
| Missing-doc warning gate | Build config (`.editorconfig`) | Roslyn/MSBuild | For Core, only ONE suppressor — the global editorconfig `severity = none`; no csproj NoWarn to remove |
| Verification | Build (Windows `dotnet.exe` over WSL) | CI push-watch | VSTest unreliable in WSL; `dotnet build` clean is the local gate; doc comments are compile-stripped so no runtime regression is possible |

## Standard Stack

No new packages. Pure source-comment + build-config phase. Relevant settings:

| Setting | Location | Current Value | Phase 29 Action |
|---------|----------|---------------|-----------------|
| `GenerateDocumentationFile` | `DeckFlow.Core/DeckFlow.Core.csproj:7` | `true` | Keep — already present; no change needed |
| `<NoWarn>` | `DeckFlow.Core/DeckFlow.Core.csproj` | **absent** | No change needed — Core never had a NoWarn line |
| `dotnet_diagnostic.CS1591.severity` | `.editorconfig:97` (global `[*.cs]`) | `none` | Keep `none` on global section; ADD new scoped section (see Architecture Patterns) |
| `dotnet_diagnostic.CS1573.severity` | `.editorconfig:98` (global `[*.cs]`) | `none` | Keep `none` on global section; ADD new scoped section |
| `dotnet_diagnostic.CS1587.severity` | `.editorconfig:99` (global `[*.cs]`) | `none` | Keep `none` on global section; ADD new scoped section |
| `[DeckFlow.Web/**.cs]` gate | `.editorconfig:111-115` | `severity = warning` | Keep unchanged — Phase 23 work; do NOT touch |

**Installation:** none.

## Package Legitimacy Audit

Not applicable — no external packages installed in this phase.

## Architecture Patterns

### The suppressor situation for Core differs from Phase 23's Web

```
Phase 23 (DeckFlow.Web) had TWO independent suppressors:
  1. DeckFlow.Web.csproj: <NoWarn>$(NoWarn);1591;1573;1587</NoWarn>   ← removed in Phase 23
  2. .editorconfig [*.cs]: severity = none                             ← changed to warning in Phase 23
                                                                          (scoped to [DeckFlow.Web/**.cs])

Phase 29 (DeckFlow.Core) has ONE suppressor:
  1. .editorconfig [*.cs]: severity = none                             ← override with [DeckFlow.Core/**.cs]
  DeckFlow.Core.csproj: NO <NoWarn> line (confirmed — csproj only has GenerateDocumentationFile=true)
```

This means the gate-widen for Phase 29 requires only ONE file edit (`.editorconfig`), not two.

### Gate-widen edit — exact shape

Current `.editorconfig` (lines 111–116, from Phase 23):

```
[DeckFlow.Web/**.cs]
# Phase 23 DOC-02: XML doc-comment gate scoped to DeckFlow.Web
dotnet_diagnostic.CS1591.severity = warning
dotnet_diagnostic.CS1573.severity = warning
dotnet_diagnostic.CS1587.severity = warning
```

New section to ADD (append after the Web section, do NOT modify the Web section):

```
[DeckFlow.Core/**.cs]
# Phase 29 HSK-01: XML doc-comment gate widened to DeckFlow.Core
dotnet_diagnostic.CS1591.severity = warning
dotnet_diagnostic.CS1573.severity = warning
dotnet_diagnostic.CS1587.severity = warning
```

**Path-matching safety:** `[DeckFlow.Core/**.cs]` matches files under `DeckFlow.Core/` and does NOT match `DeckFlow.Core.Tests/` (different directory prefix). The test project has 574+ undocumented internal test members — they must NOT be gated; the path match confirms they are unaffected. [VERIFIED: probe build confirmed DeckFlow.Core.Tests with severity=warning produced 574 warnings — these are internal test code safely excluded by the path prefix]

### CS1591 fires on members, not just types (Phase 23 learning — applies here too)

A file with a documented class but undocumented properties fires CS1591 once per undocumented public member. `ReconciliationReporter.cs` has 8 unique warning sites for a single `static class` — each public constant and method is an individual CS1591 site. The per-file counts in the inventory section are compiler-derived (not type-level grep counts).

### Wave structure (reuse Phase 23 pattern)

```
Wave 1: parallel backfill plans (non-overlapping file sets)
  Plan 29-01: Storage folder (21 sites, 4 files) — IRelationalDialect + PostgresRelationalDialect +
              SqliteRelationalDialect + RelationalDatabaseConnection
  Plan 29-02: Reporting + Filtering folder (16 sites, 5 files) — ReconciliationReporter +
              CategoryCardReporter + CategoryCountReporter + CategorySuggestionReporter +
              CategoryFilter + DeckEntryFilter
  Plan 29-03: Knowledge folder (12 sites, 3 files) — ArchidektDeckCacheSession +
              BoardCategoryComparer + CategoryKnowledgeRepository
  Plan 29-04: Integration + Diffing + Exporting + Models + Normalization + Parsing folders
              (41 sites, 17 files) — remaining files

Wave 2: gate-widen (depends on all Wave 1 plans)
  Plan 29-05: .editorconfig gate widen + probe-validated build
              non-autonomous; human-verify checkpoint required (.editorconfig is Do-Not-Modify)
```

Wave 1 plans are independent (non-overlapping file sets) and can run in parallel per Codex dispatch.

### Anti-Patterns to Avoid

- **Editing `.editorconfig` before backfill is complete** — the gate widen must be the LAST commit of the phase. A widen-before-backfill turns the build red mid-phase and blocks every other task.
- **Using file-level grep to count undocumented sites** — CS1591 fires per public member, not per file. A class with a type-level `<summary>` but 8 undocumented public properties passes grep but produces 8 warnings. Always use the compiler probe.
- **Editing the `[DeckFlow.Web/**.cs]` section** — leave the Phase 23 gate untouched; the Phase 29 edit is an ADDITIVE new `[DeckFlow.Core/**.cs]` section.
- **Running Format Document while adding doc comments** — CLAUDE.md forbids this. Touch ONLY the `///` lines you add.
- **Converting `{ get; init; }` to `{ get; }`** — System.Text.Json silently drops get-only properties in .NET 9+. NONE of the 29 warning files contain `{ get; init; }` (confirmed by grep) — but the rule must be stated for Codex to know.
- **Adding `<param>` to only some params** — CS1573 fires when a method has ANY `<param>` tags but not ALL. The 6 CS1573 sites require completing the existing partial param sets, not removing them.
- **Relying on `-warnaserror:CS1591` exit code alone** — without a probe, this passes trivially when the suppressor is still active. The gate-widen plan must inject and verify an undocumented probe class, then remove it.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Finding undocumented sites | grep for `<summary>` absence | Compiler probe: temp `.editorconfig` override + `dotnet build` | grep undercounts — a type with a `<summary>` but undocumented properties passes grep but fires CS1591 per-member |
| Verifying gate is live | Trust `-warnaserror:CS1591` exit code | Inject `__TempUndocProbe.cs`, confirm CS1591 fires, then remove | Exit code passes trivially if a suppressor is still active — probe proves the gate is real |

**Key insight:** In this repo, the only trustworthy doc-warning inventory is "build with the suppressor temporarily overridden from a clean `obj/` and read the warning list." Everything else undercounts.

## Authoritative Undocumented Inventory (compiler-derived, 2026-06-04)

**Method:** Created a temporary `DeckFlow.Core/.editorconfig` with `root = false` and `[*.cs] severity = warning`, ran `dotnet build DeckFlow.Core/DeckFlow.Core.csproj --no-incremental` via Windows `dotnet.exe` (WSL path), captured output. Temp file removed after; `git diff` confirmed clean.

**Totals: 90 unique warning sites across 29 files**
(Build output listed each site twice — 180 raw lines / 2 = 90 unique. Duplication is a non-incremental build artifact, not a real doubling.)

| Warning | Unique Count | Meaning | Remediation |
|---------|-------------|---------|-------------|
| CS1591 | 84 | Missing XML comment for publicly visible type or member | Add `<summary>` (or `<inheritdoc/>` for interface impls) |
| CS1573 | 6 | Parameter has no matching `<param>` tag (method has partial param docs) | Complete the `<param>` set — all params or none |
| CS1587 | 0 | XML comment not placed on valid language element | None needed |

**Reduction from 186 → 90:** Phases 19–28 added extensive new Core code with complete documentation (Content KB contracts/stores, schema normalization, distillation models, ContentArtifactSpec, etc.). The 186 was accurate at Phase 23 research time; the 90 is accurate today.

### Per-folder rollup

| Folder | Unique Sites | Files | Notes |
|--------|-------------|-------|-------|
| `Storage/` | 21 | 4 | IRelationalDialect, Postgres/SqliteDialect, RelationalDatabaseConnection |
| `Reporting/` | 15 | 5 | ReconciliationReporter (8), CategoryCard/Count/Suggestion reporters |
| `Integration/` | 13 | 5 | DeckImporterInterfaces, ArchidektRecentDecksImporter, both ApiUrl classes, MoxfieldApiDeckImporter |
| `Knowledge/` | 12 | 3 | CategoryKnowledgeRepository (7, incl. 5 CS1573), BoardCategoryComparer, ArchidektDeckCacheSession |
| `Exporting/` | 10 | 4 | DeltaExporter, FullImportExporter, MoxfieldTextExporter, CategoryNormalization (1) |
| `Parsing/` | 7 | 4 | IParser, ArchidektParser, MoxfieldParser, DeckParseException |
| `Models/` | 7 | 2 | PrintingChoice (4), MatchMode (3) |
| `Normalization/` | 2 | 1 | CardNormalizer |
| `Diffing/` | 2 | 1 | DiffEngine |
| `Filtering/` | 1 | 1 | DeckEntryFilter |
| `Content/` | 0 | — | Already fully documented (Phases 19–22) |
| `Loading/` | 0 | — | Already fully documented |

### Per-file counts (the work list)

```
  8  Storage/RelationalDatabaseConnection.cs
  8  Reporting/ReconciliationReporter.cs
  7  Knowledge/CategoryKnowledgeRepository.cs   ← includes 5 CS1573 sites
  5  Storage/SqliteRelationalDialect.cs
  5  Storage/PostgresRelationalDialect.cs
  4  Models/PrintingChoice.cs
  4  Integration/DeckImporterInterfaces.cs
  4  Exporting/FullImportExporter.cs
  4  Exporting/DeltaExporter.cs
  3  Storage/IRelationalDialect.cs
  3  Models/MatchMode.cs
  3  Knowledge/BoardCategoryComparer.cs
  3  Integration/ArchidektRecentDecksImporter.cs
  2  Reporting/CategorySuggestionReporter.cs
  2  Reporting/CategoryCountReporter.cs
  2  Reporting/CategoryCardReporter.cs
  2  Parsing/MoxfieldParser.cs
  2  Parsing/IParser.cs
  2  Parsing/ArchidektParser.cs
  2  Normalization/CardNormalizer.cs
  2  Knowledge/ArchidektDeckCacheSession.cs
  2  Integration/MoxfieldApiUrl.cs
  2  Integration/MoxfieldApiDeckImporter.cs    ← includes 1 CS1573 site (executeAsync param)
  2  Integration/ArchidektApiUrl.cs
  2  Exporting/MoxfieldTextExporter.cs
  2  Diffing/DiffEngine.cs
  1  Reporting/CategoryFilter.cs
  1  Parsing/DeckParseException.cs
  1  Filtering/DeckEntryFilter.cs
```

### CS1573 sites requiring param-set completion

| File | Method | Missing Params | Action |
|------|--------|---------------|--------|
| `Integration/MoxfieldApiDeckImporter.cs:23` | `MoxfieldApiDeckImporter(RestClient?, Func<...>?)` | `executeAsync` | Add `<param name="executeAsync">` tag |
| `Knowledge/CategoryKnowledgeRepository.cs:235` | `GetCategoryRowsForCardAsync(string, string?, CancellationToken)` | `boardFilter` | Add `<param name="boardFilter">` tag |
| `Knowledge/CategoryKnowledgeRepository.cs:409` | `ReplaceSourceRowsAsync(string, IReadOnlyList<...>, string, int, CancellationToken)` | `board`, `deckCount` | Add both `<param>` tags |
| `Knowledge/CategoryKnowledgeRepository.cs:508` | `PersistObservedCategoriesAsync(string, string, IReadOnlyList<string>, int, string, int, CancellationToken)` | `board`, `deckCountIncrement` | Add both `<param>` tags |

## Risk Files

### Raw-string literals in warning files

Four warning files contain raw-string literals (`"""`). Doc-comment additions must NEVER re-indent these literals (CLAUDE.md). The `///` lines being added go ABOVE the member declarations, not inside raw strings — but Codex must be explicitly warned.

| File | Raw-String Count | Content Type | Risk Level |
|------|-----------------|--------------|------------|
| `Knowledge/CategoryKnowledgeRepository.cs` | 50 | SQL queries | HIGH — largest file, most warnings, SQL raw strings interspersed |
| `Storage/PostgresRelationalDialect.cs` | 2 | SQL column type strings | LOW — doc comment additions are at property/method declaration level, not near raw strings |
| `Storage/SqliteRelationalDialect.cs` | 2 | SQL column type strings | LOW |
| `Reporting/ReconciliationReporter.cs` | 4 | Instruction text for CLI output | MEDIUM — constants with raw-string values near public member declarations |

### Files already confirmed safe from init-accessor mutation

None of the 29 warning files contain `{ get; init; }` — confirmed by grep. The init-accessor landmine does not apply to the backfill file set. (The `{ get; init; }` files in Core — e.g., `Models/DeckEntry.cs`, `Integration/YouTubeChannelVideo.cs` — are already fully documented and appear in zero warning sites.)

## Common Pitfalls

### Pitfall 1: Attempting to probe with `-warnaserror-` flag (invalid switch)
**What goes wrong:** `-warnaserror-` is an MSBuild switch, not a `dotnet build` flag. Passing it as a CLI argument produces `MSB1001: Unknown switch`.
**Why it happens:** Confusion between MSBuild switch syntax and dotnet CLI switch syntax.
**How to avoid:** Use `--no-incremental` with the temp `.editorconfig` override technique (see Code Examples). Do NOT pass `-warnaserror-`.
**Warning signs:** `error MSB1001: Unknown switch` in build output.

### Pitfall 2: Gate widen must be the LAST commit (ROADMAP guard rule)
**What goes wrong:** Adding the `[DeckFlow.Core/**.cs]` gate section before all 90 sites are documented turns the build red immediately.
**Why it happens:** The gate fires on commit-gated CI; any undocumented site fails the build.
**How to avoid:** Wave 2 gate-widen plan is sequenced AFTER all Wave 1 backfill plans are complete (standard `depends_on` in the plan frontmatter).
**Warning signs:** A plan that edits `.editorconfig` in Wave 1.

### Pitfall 3: `.editorconfig` is on the Do-Not-Modify list
**What goes wrong:** An agent edits `.editorconfig` without user approval, violating CLAUDE.md.
**How to avoid:** The gate-widen plan (29-05) must be `autonomous: false` with a `checkpoint:human-verify` task. The user's approval for this single edit must be obtained at plan execution time.
**Warning signs:** `.editorconfig` change in an `autonomous: true` plan.

### Pitfall 4: Probe must use a temp `.editorconfig` in `DeckFlow.Core/`, not a flag
**What goes wrong:** Trying to pass diagnostic severity overrides via MSBuild properties on the CLI doesn't work — the editorconfig takes precedence over all property-based approaches at the Roslyn analyzer layer.
**Why it happens:** `dotnet build -p:TreatWarningsAsErrors=true` and `/warnaserror:CS1591` still don't surface CS1591 when editorconfig sets `severity = none` at the global level.
**How to avoid:** Use the temp local `.editorconfig` approach (Phase 29 probe methodology, see Code Examples). This is the only approach confirmed to work in this repo.
**Warning signs:** `0 Warning(s)` on the probe build when you know undocumented members exist.

### Pitfall 5: CS1573 is all-or-nothing on param docs
**What goes wrong:** A method that already has some `<param>` tags but not all triggers CS1573. Adding ONLY the missing param tags (while leaving other existing ones) may introduce duplicate CS1573 elsewhere.
**Why it happens:** Phase 19–28 added partial param docs to some CategoryKnowledgeRepository methods.
**How to avoid:** For each CS1573 site, add ALL missing `<param>` tags identified in the CS1573 inventory table above. Do not remove existing `<param>` tags.
**Warning signs:** CS1573 on a method whose other params are already documented.

### Pitfall 6: VSTest unreliable in WSL — build is the gate
**What goes wrong:** `dotnet test` in WSL is unreliable. Doc comments are compile-stripped — zero runtime regression is possible.
**How to avoid:** Use `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Release` as the local verification gate. Push-watch CI runs the full test suite.

## Code Examples

### Probe build methodology (the authoritative inventory technique)

```bash
# Step 1: Create a temporary local .editorconfig that overrides the global suppressor
cat > DeckFlow.Core/.editorconfig << 'EOF'
root = false

[*.cs]
dotnet_diagnostic.CS1591.severity = warning
dotnet_diagnostic.CS1573.severity = warning
dotnet_diagnostic.CS1587.severity = warning
EOF

# Step 2: Run a clean non-incremental build and capture warnings
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"
"$DOTNET" build DeckFlow.Core/DeckFlow.Core.csproj --no-incremental 2>&1 \
  | grep -E "warning CS(1591|1573|1587)" | sort -u

# Step 3: Remove the temp file and verify no git diff
rm DeckFlow.Core/.editorconfig
git diff --exit-code DeckFlow.Core/.editorconfig && echo "clean"
```

### Gate-widen probe (REQUIRED for the 29-05 plan — proves gate is real)

```bash
# Inject probe AFTER adding the [DeckFlow.Core/**.cs] section but BEFORE committing
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"
printf '%s\n' 'namespace DeckFlow.Core;' \
  'public sealed class __TempUndocProbe { public int X { get; init; } }' \
  > DeckFlow.Core/__TempUndocProbe.cs

# With the gate section added, this MUST produce CS1591 on __TempUndocProbe (type + .X).
# If it does NOT warn, the gate section is not taking effect — STOP.
"$DOTNET" build DeckFlow.Core/DeckFlow.Core.csproj --no-incremental 2>&1 \
  | grep "__TempUndocProbe"
# expect: warning CS1591 on __TempUndocProbe AND on __TempUndocProbe.X

rm DeckFlow.Core/__TempUndocProbe.cs   # ALWAYS clean up — never commit the probe
```

### Gate-widen build (all-three-code promotion, from Phase 23 23-05 plan)

```bash
DOTNET="/mnt/c/Program Files/dotnet/dotnet.exe"
rm -rf DeckFlow.Core/obj DeckFlow.Core/bin

# Promote all three doc codes — CS1591 alone false-greens on surviving CS1573
"$DOTNET" build DeckFlow.Core/DeckFlow.Core.csproj -c Release --no-incremental \
  -warnaserror:CS1591,CS1573,CS1587 2>&1 | tee /tmp/29-05-gate.log | tail -5

# Belt-and-suspenders: assert log is clean
grep -E "warning CS(1591|1573|1587)" /tmp/29-05-gate.log && echo "FAIL: doc warning survived" || echo "OK"

# Full-solution guard (DeckFlow.Web gate must stay intact; test projects unaffected)
"$DOTNET" build DeckFlow.sln -c Release 2>&1 | tail -5
```

### CS1591 fix shape (type + every member)

```csharp
// BEFORE — class has <summary> but public members bare:
/// <summary>Exports the full import file.</summary>
public static class FullImportExporter
{
    public static void WriteFile(List<DeckEntry> source, ...) { ... }
    public static string ToText(List<DeckEntry> source, ...) { ... }
}

// AFTER — every public member also documented:
/// <summary>Exports the full import file.</summary>
public static class FullImportExporter
{
    /// <summary>Writes the full import file to <paramref name="path"/>.</summary>
    public static void WriteFile(List<DeckEntry> source, ...) { ... }

    /// <summary>Returns the full import file content as a string.</summary>
    public static string ToText(List<DeckEntry> source, ...) { ... }
}
```

### CS1573 fix shape (complete missing params — do NOT remove existing ones)

```csharp
// BEFORE — 'board' and 'deckCount' missing from a method that already has some params documented:
/// <summary>Replaces all category rows for a source deck.</summary>
/// <param name="source">The deck source identifier.</param>
/// <param name="rows">Category observation rows to persist.</param>
/// <param name="normalizedCardName">...</param>
/// <param name="cancellationToken">Propagates cancellation.</param>
public async Task ReplaceSourceRowsAsync(string source, IReadOnlyList<CategoryKnowledgeRow> rows,
    string normalizedCardName, int deckCount, CancellationToken cancellationToken)

// AFTER — every param documented:
/// <summary>Replaces all category rows for a source deck.</summary>
/// <param name="source">The deck source identifier.</param>
/// <param name="rows">Category observation rows to persist.</param>
/// <param name="normalizedCardName">...</param>
/// <param name="board">The board zone (e.g., "mainboard") these rows belong to.</param>
/// <param name="deckCount">Number of decks contributing to these observations.</param>
/// <param name="cancellationToken">Propagates cancellation.</param>
public async Task ReplaceSourceRowsAsync(...)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| DeckFlow.Core suppressed solution-wide via `[*.cs] severity=none` | Core gate widened to `[DeckFlow.Core/**.cs] severity=warning` | Phase 29 (this work) | CS1591/1573/1587 become build warnings in Core; IDE surfaces them immediately |
| 186 undocumented Core sites | 90 remaining (Phases 19–28 documented new additions) | 2026-06-02 → 2026-06-04 | Actual backfill scope is 90 sites, not 186 |

**Current baseline (confirmed before this phase):**
- Full solution `dotnet build -c Release`: `0 Warning(s) 0 Error(s)` [VERIFIED: probe run 2026-06-04]
- DeckFlow.Web gate (`[DeckFlow.Web/**.cs]`): active from Phase 23, untouched [VERIFIED]
- DeckFlow.Core suppressed by `[*.cs] severity=none`: active [VERIFIED]

## Project Constraints (from CLAUDE.md)

These apply with full authority to every plan and every Codex dispatch in this phase:

1. **DO NOT run Format Document / Code Cleanup** — touch only the `///` lines being added.
2. **NEVER convert `{ get; init; }` to `{ get; }`** — System.Text.Json silently drops get-only properties in .NET 9+. (None of the 29 warning files contain `{ get; init; }` — confirmed — but the rule must be stated.)
3. **NEVER inline `[Attribute]` onto property/declaration lines** — keep attributes on their own lines.
4. **NEVER re-indent C# raw-string literals** — changes the literal value shipped to the AI or used as SQL. Four warning files contain raw-string literals: `CategoryKnowledgeRepository.cs`, `PostgresRelationalDialect.cs`, `SqliteRelationalDialect.cs`, `ReconciliationReporter.cs`.
5. **Preserve LF line endings** — `.gitattributes` enforces LF; editors must not introduce CRLF.
6. **`.editorconfig` is on the Do-Not-Modify list** — the gate-widen edit requires explicit user approval at execution time. The 29-05 plan must be non-autonomous.
7. **No new packages** — this phase adds zero NuGet dependencies.
8. **Build environment** — use `"/mnt/c/Program Files/dotnet/dotnet.exe"` (Windows dotnet.exe over WSL). VSTest is unreliable in WSL; `dotnet build` clean is the local gate.
9. **Commits** — plain default-author commits (no Co-Authored-By trailer); one logical change per commit.
10. **Allowed file set discipline** — each Codex plan must hard-fence its allowed file set to its assigned folder(s) and ignore other tasks.

## Runtime State Inventory

This phase edits source comments + build config only. No stored data, no live-service config, no OS-registered state, no secrets, no build artifacts carry the changed state.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | None — doc comments are compile-stripped; no DB touched | none |
| Live service config | None — no deployed config references doc state | none |
| OS-registered state | None | none |
| Secrets/env vars | None | none |
| Build artifacts | `DeckFlow.Core.xml` regenerates on every build; not committed | none |

**Nothing found in any category** — verified by inspection; doc-comments and editorconfig warning-severity have zero runtime/persisted footprint.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| `dotnet.exe` (Windows, via WSL) | All build/probe tasks | ✓ | .NET 10.0.300 | — |
| `git` (WSL) | Diff verification after probe | ✓ | (WSL native) | — |

No missing dependencies.

## Open Questions (RESOLVED)

1. **Probe build duplication**
   - What we know: The non-incremental build lists each CS1591 warning twice (180 raw lines = 90 unique sites). This is consistent behavior, not a double-count of actual sites.
   - What's unclear: Whether a future SDK version changes this. At execution time, Codex should deduplicate with `sort -u` when parsing the probe output.
   - Recommendation: Always use `sort -u` when counting or listing probe output.

2. **CategoryNormalization.cs in Exporting/**
   - What we know: The initial grep-based scan flagged it, but the probe build shows 0 CS1591 warnings for it. It may already be documented or contain only private members.
   - What's unclear: Exact documentation state.
   - Recommendation: Codex should re-run the probe at execution time to get the current list; the per-file counts above are the authoritative starting point but drift may occur if any files are touched before this phase executes.

## Sources

### Primary (HIGH confidence)
- Probe build output `/tmp/core-doc-probe.log` — compiler-derived CS1591/CS1573/CS1587 inventory for DeckFlow.Core (2026-06-04, this session)
- `.editorconfig` — read verbatim (current suppressor state, Phase 23 gate section)
- `DeckFlow.Core/DeckFlow.Core.csproj` — read verbatim (GenerateDocumentationFile=true, no NoWarn)
- Phase 23 RESEARCH.md (`.planning/milestones/v1.4-phases/23-doc-comment-backfill-part-2-strip-nowarn/23-RESEARCH.md`) — proven patterns reused
- Phase 23 plan 05 (`23-05-PLAN.md`) — gate-widen plan structure reused for 29-05
- Full solution probe build — confirmed `0 Warning(s) 0 Error(s)` baseline (2026-06-04)

### Secondary (MEDIUM confidence)
- Phase 23 plans 01–04 — parallel Wave 1 backfill plan structure used to design Wave 1 of Phase 29
- CLAUDE.md project constraints — applied directly

### Tertiary (LOW confidence)
- Memory entry "Phase 23 Core doc debt" — cited 186 sites; confirmed superseded by 90-site probe count

## Metadata

**Confidence breakdown:**
- Warning site inventory: HIGH — compiler-derived from a probe build run this session
- Suppressor analysis: HIGH — read csproj and editorconfig verbatim; confirmed single suppressor
- Plan structure: HIGH — direct reuse of Phase 23 pattern (proven, archived)
- Risk files: HIGH — grep-confirmed (init-accessor absence; raw-string presence)
- Gate-widen edit: HIGH — exact line positions verified by reading .editorconfig

**Research date:** 2026-06-04
**Valid until:** This research is a snapshot build. The 90-site count and 29-file list are accurate as of the probe run. If any DeckFlow.Core files are modified before Phase 29 executes (unlikely — Phase 29 is off the critical path per ROADMAP), Codex should re-run the probe as its first task to confirm the current count.
