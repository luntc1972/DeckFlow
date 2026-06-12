---
phase: 39-architecture-review
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Core/Loading/DeckEntryLoader.cs
  - DeckFlow.Web/Services/DeckComparisonService.cs
  - DeckFlow.Web/Services/MetaGapService.cs
  - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
  - DeckFlow.Web/Services/DeckPrimerPacketService.cs
  - DeckFlow.Web/Program.cs
  - DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs
  - DeckFlow.Core.Tests/DeckEntryLoaderTests.cs
autonomous: true
requirements: [ARCH-02]

must_haves:
  truths:
    - "Deck Analysis, Comparison, Meta-Gap, and Primer packets still load decks identically (URL auto-detect + paste cascade) with no observable behavior change."
    - "The Moxfield Commander-Spellbook fallback notice still surfaces for Analysis and Primer flows exactly as before."
    - "No packet service owns a private LoadDeckEntriesAsync; all four route through the existing IDeckEntryLoader (extended additively)."
    - "All existing packet-service, round-trip, and contract tests pass unchanged; cache-key canonical text is byte-identical."
  artifacts:
    - path: "DeckFlow.Core/Loading/DeckEntryLoader.cs"
      provides: "Additive LoadFromSourceAsync(source-autodetect) returning entries + optional fallback notice"
      contains: "LoadFromSourceAsync"
    - path: "DeckFlow.Web/Services/DeckComparisonService.cs"
      provides: "Comparison service consuming IDeckEntryLoader; private LoadDeckEntriesAsync deleted"
    - path: "DeckFlow.Web/Services/MetaGapService.cs"
      provides: "MetaGap service consuming IDeckEntryLoader; private LoadDeckEntriesAsync deleted"
    - path: "DeckFlow.Web/Services/DeckAnalysisPacketService.cs"
      provides: "Analysis service consuming IDeckEntryLoader (with notice); private LoadDeckEntriesAsync deleted"
    - path: "DeckFlow.Web/Services/DeckPrimerPacketService.cs"
      provides: "Primer service consuming IDeckEntryLoader (with notice); private LoadDeckEntriesAsync deleted"
  key_links:
    - from: "DeckFlow.Web/Services/DeckAnalysisPacketService.cs"
      to: "IDeckEntryLoader.LoadFromSourceAsync"
      via: "injected loader"
      pattern: "_deckEntryLoader\\.LoadFromSourceAsync"
    - from: "DeckFlow.Web/Program.cs"
      to: "IDeckEntryLoader"
      via: "already-registered DeckEntryLoader resolved by all four packet services"
      pattern: "GetRequiredService<IDeckEntryLoader>"
---

<objective>
Eliminate the four duplicated private `LoadDeckEntriesAsync` deck-load implementations in the ChatGPT-packet services by routing all deck loading through the EXISTING `IDeckEntryLoader` (D-03), extending that loader ADDITIVELY only where the packet flow has behavior the loader does not yet cover.

Purpose: Collapse deck-IO duplication across the core value path (packet artifacts) onto the already-proven loader seam (`DeckSyncService`/`DeckConvertService` already consume it), without changing any observable behavior or cache-key bytes.

Output: An extended `IDeckEntryLoader` with a source-autodetect entry point; four packet services that inject and call it; four deleted private `LoadDeckEntriesAsync` bodies; updated DI wiring and test construction.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/phases/39-architecture-review/39-CONTEXT.md
@./CLAUDE.md

<key_findings>
The four packet `LoadDeckEntriesAsync` bodies are NOT identical to `IDeckEntryLoader.LoadAsync`, and not identical to each other. Behavioral differences that MUST be preserved:

1. **Source auto-detection (all four).** Packet flow takes a single `deckSource` string and decides Moxfield-vs-Archidekt by URL host, then falls back to a parser cascade for pasted text. `IDeckEntryLoader.LoadAsync(DeckLoadRequest)` requires the platform + input-kind specified up front and does NOT auto-detect. → The loader must be EXTENDED with a source-autodetect entry point (additive).

2. **Paste cascade (Comparison, Analysis, Primer):** try `MoxfieldParser.ParseText` → on `DeckParseException` try `ArchidektParser.ParseText` → on `DeckParseException` throw `InvalidOperationException("The submitted deck was not recognized as a Moxfield URL, Archidekt URL, Moxfield export, or Archidekt export.")`.
   **MetaGap paste cascade differs:** Moxfield parse → on `DeckParseException` it calls `ArchidektParser.ParseText` directly WITHOUT a catch (so a second `DeckParseException` propagates rather than being re-wrapped). This subtle difference is currently swallowed by each service's outer `LoadDeckAsync` try/catch that re-wraps `DeckParseException` into `InvalidOperationException`. Preserve net observable behavior; do NOT silently "fix" MetaGap to match the others unless behavior is provably identical end-to-end.

3. **Moxfield URL fallback notice (Analysis, Primer ONLY):** Moxfield URL loads via `IMoxfieldDeckImporter.ImportWithSourceAsync` and captures `result.FallbackNotice` into `_lastImportNotice` (Commander-Spellbook fallback notice surfaced to UI). Comparison + MetaGap use plain `ImportAsync` (NO notice). The extended loader entry point must return the notice so Analysis/Primer keep surfacing it; Comparison/MetaGap simply ignore the returned notice.
</key_findings>

<interfaces>
From DeckFlow.Core/Loading/DeckEntryLoader.cs:
```csharp
public interface IDeckEntryLoader
{
    Task<List<DeckEntry>> LoadAsync(DeckLoadRequest request, CancellationToken cancellationToken = default);
    void ValidateCommanderDeckSize(string systemName, IReadOnlyList<DeckEntry> entries, int requiredDeckSize = 100);
}
```
Dependencies already held by DeckEntryLoader: IMoxfieldDeckImporter, IArchidektDeckImporter, MoxfieldParser, ArchidektParser.

From DeckFlow.Core/Integration/DeckImporterInterfaces.cs:
```csharp
public sealed record MoxfieldImportResult(IReadOnlyList<DeckEntry> Entries, MoxfieldImportSource Source, string? FallbackNotice = null);
Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default);
Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default);
```

From DeckFlow.Web/Services/DeckSyncService.cs — reference consumer pattern: inject `IDeckEntryLoader`, call `_deckEntryLoader.LoadAsync(new DeckLoadRequest(...))`.
</interfaces>

@DeckFlow.Core/Loading/DeckEntryLoader.cs
@DeckFlow.Web/Services/DeckSyncService.cs
</context>

<tasks>

<task type="auto">
  <name>Task 1: Extend IDeckEntryLoader with source-autodetect + fallback-notice (additive, behavior-preserving)</name>
  <files>DeckFlow.Core/Loading/DeckEntryLoader.cs, DeckFlow.Core.Tests/DeckEntryLoaderTests.cs</files>
  <action>
Add ONE new method to `IDeckEntryLoader` and implement it in `DeckEntryLoader`. Do NOT alter the existing `LoadAsync`/`ValidateCommanderDeckSize` signatures or bodies (additive only, per D-03 path-2).

New entry point owns the source-autodetect + paste-cascade + Moxfield-notice logic currently duplicated in the packet services. Define a small return type so callers that need the Commander-Spellbook fallback notice can read it while callers that do not can ignore it:
  - A `sealed record DeckSourceLoadResult(List<DeckEntry> Entries, string? FallbackNotice)` in the same namespace/file.
  - `Task<DeckSourceLoadResult> LoadFromSourceAsync(string deckSource, CancellationToken cancellationToken = default)` on the interface + impl.

Implement `LoadFromSourceAsync` to reproduce the packet flow EXACTLY:
  1. Trim the source. If `Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)`:
     - host contains "moxfield.com" → call `_moxfieldDeckImporter.ImportWithSourceAsync(deckSource, ct)`, return `new(result.Entries.ToList(), result.FallbackNotice)`. (Use the ORIGINAL `deckSource`, not the trimmed copy — match each service's current arg: Comparison/Primer/Analysis pass `deckSource`; MetaGap passes the trimmed value. Pick the value that keeps importer input identical to today; if they differ, see Task 2 note and pass the original `deckSource` for the notice-bearing callers and trimmed for MetaGap — confirm by reading each call site before finalizing.)
     - host contains "archidekt.com" → `await _archidektDeckImporter.ImportAsync(deckSource, ct)`, return `new(entries, null)`.
  2. Else paste cascade: `try { return new(_moxfieldParser.ParseText(deckSource), null); } catch (DeckParseException) {}` then `try { return new(_archidektParser.ParseText(deckSource), null); } catch (DeckParseException) {}` then `throw new InvalidOperationException("The submitted deck was not recognized as a Moxfield URL, Archidekt URL, Moxfield export, or Archidekt export.");`.

IMPORTANT — MetaGap divergence: MetaGap's current paste cascade lets the SECOND `DeckParseException` propagate (no inner catch) and has no final `InvalidOperationException`. Determine whether MetaGap's outer `LoadDeckAsync` try/catch (which re-wraps `DeckParseException` into `InvalidOperationException`) makes the END-TO-END observable result identical to the cascade above. If identical, all four can share `LoadFromSourceAsync`. If NOT identical, document the difference in the SUMMARY and either (a) keep the net message identical by routing MetaGap through the shared method (preferred, since the outer wrap already normalizes the message) or (b) flag for a STOP. The exception MESSAGE strings asserted by tests must not change — grep `MetaGapServiceTests` for the parse-failure assertions and preserve them.

Add focused tests in `DeckEntryLoaderTests.cs` (xUnit, Core.Tests) for `LoadFromSourceAsync`: Moxfield URL returns notice from importer; Archidekt URL returns null notice; pasted Moxfield text parses; pasted Archidekt text parses; unrecognized text throws `InvalidOperationException` with the exact message above. Use the existing Core.Tests fake importer/parser patterns (`Fake*`/`Stub*`); do NOT add a mocking library.
  </action>
  <acceptance_criteria>
    - `IDeckEntryLoader` gains exactly one new method (`LoadFromSourceAsync`) plus the `DeckSourceLoadResult` record; existing members byte-unchanged.
    - New method reproduces URL-autodetect + paste-cascade + Moxfield notice capture.
    - New Core.Tests cover URL-moxfield-notice, URL-archidekt, paste-moxfield, paste-archidekt, unrecognized-throws.
  </acceptance_criteria>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln 2>&1 | grep -E "error|warning" | grep -v "^$" | wc -l  # expect 0</automated>
  </verify>
  <done>Loader extended additively, builds clean, new tests authored. Commit: `refactor(39): add IDeckEntryLoader.LoadFromSourceAsync (source autodetect + notice)`.</done>
</task>

<task type="auto">
  <name>Task 2: Migrate all four packet services onto IDeckEntryLoader; delete private LoadDeckEntriesAsync</name>
  <files>DeckFlow.Web/Services/DeckComparisonService.cs, DeckFlow.Web/Services/MetaGapService.cs, DeckFlow.Web/Services/DeckAnalysisPacketService.cs, DeckFlow.Web/Services/DeckPrimerPacketService.cs, DeckFlow.Web/Program.cs, DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs</files>
  <action>
For each of the four services, inject `IDeckEntryLoader` and replace the private `LoadDeckEntriesAsync` call sites with `_deckEntryLoader.LoadFromSourceAsync(...)`, then DELETE the private `LoadDeckEntriesAsync` method body.

Per-service detail:
  - **Comparison** (`LoadDeckAsync` line ~335 calls `LoadDeckEntriesAsync`): replace with `var loaded = await _deckEntryLoader.LoadFromSourceAsync(deckSource, ct); var entries = loaded.Entries;` (notice ignored — Comparison surfaces none today). Keep the surrounding playable/optional split + commander inference + `ReflagCommanderEntry` UNCHANGED.
  - **MetaGap** (`LoadDeckAsync` line ~442): same replacement; notice ignored (MetaGap surfaces none today).
  - **Analysis** (call sites ~300, ~400, ~1082): replace each `LoadDeckEntriesAsync(...)` with `LoadFromSourceAsync`, and set `_lastImportNotice = loaded.FallbackNotice;` immediately after, EXACTLY where the old method set it. Preserve the `_lastImportNotice = null` reset semantics (the loader returns null notice for non-Moxfield-URL paths, so assignment alone reproduces both the reset and the set). Verify every read of `_lastImportNotice` still sees the same value at the same point in the flow.
  - **Primer** (call sites ~174, ~253): same as Analysis — set `_lastImportNotice = loaded.FallbackNotice`. Primer ALSO has a `_loadDeckEntriesAsyncOverride` test seam (`Func<string,CancellationToken,Task<List<DeckEntry>>>`). KEEP that seam working: if the override is set, it returns entries with NO notice (matches today). Route: if `_loadDeckEntriesAsyncOverride is not null` use it (notice = null), else call the loader. Do NOT remove the Primer override seam in this plan — Primer tests depend on it.

Remove now-unused private fields/ctor params where they become dead: each service holds `_moxfieldDeckImporter`, `_archidektDeckImporter`, `_moxfieldParser`, `_archidektParser` solely for deck loading. After migration, IF a service no longer references any of them anywhere else, remove the field + ctor param and the corresponding `Program.cs` DI argument. GREP each service for residual uses BEFORE removing (e.g. Analysis may still use a parser elsewhere — confirm). For any service that still uses an importer/parser elsewhere, leave it. Inject `IDeckEntryLoader` as a new ctor param in each service's `internal` ctor and wire it in `Program.cs` via `sp.GetRequiredService<IDeckEntryLoader>()` (already registered at Program.cs:374).

Update `TestServiceFactory` create methods so they construct each service with an `IDeckEntryLoader` (build a real `DeckEntryLoader` from the existing fake importers/parsers the factory already wires, OR pass a fake loader — prefer reusing the real `DeckEntryLoader` over the existing fakes so the autodetect path stays exercised). Do NOT change the `Func<RestRequest,...>` Scryfall seam params in this plan — those move in Plans 02/03.

Do NOT touch: cache-key helpers (`BuildCanonicalDeckSourceText`, `BuildDeckComparisonCacheInputs`, `ResolvePreScryfallCommanderState`, `BuildDeckAnalysisCacheInputs`) — D-06. Do NOT touch `Services/PromptBuilders/**` — D-07.
  </action>
  <acceptance_criteria>
    - `grep -rl "private async Task<List<DeckEntry>> LoadDeckEntriesAsync" DeckFlow.Web/Services/{DeckComparisonService,MetaGapService,DeckAnalysisPacketService,DeckPrimerPacketService}.cs` returns NOTHING (count 0).
    - Each of the four services references `_deckEntryLoader.LoadFromSourceAsync`.
    - `_lastImportNotice` in Analysis + Primer is assigned from `loaded.FallbackNotice` at the same flow position as before.
    - Primer's `_loadDeckEntriesAsyncOverride` seam still functions.
    - Program.cs passes `IDeckEntryLoader` into all four service factories.
  </acceptance_criteria>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/deckflow && grep -c "private async Task<List<DeckEntry>> LoadDeckEntriesAsync" DeckFlow.Web/Services/DeckComparisonService.cs DeckFlow.Web/Services/MetaGapService.cs DeckFlow.Web/Services/DeckAnalysisPacketService.cs DeckFlow.Web/Services/DeckPrimerPacketService.cs  # expect all :0</automated>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln 2>&1 | grep -E "error|warning" | grep -v "^$" | wc -l  # expect 0</automated>
  </verify>
  <done>All four private LoadDeckEntriesAsync deleted, all four route through the loader, build clean. Commit: `refactor(39): route packet services through IDeckEntryLoader.LoadFromSourceAsync`.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| (none new) | Pure internal refactor; no new untrusted input crosses any boundary. Deck source already validated by existing parsers/importers. |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-39-01 | Tampering | Behavior drift in deck-load (cache-key / fallback notice) | mitigate | Existing packet/round-trip/contract suites + byte-identical cache-key canonical text (D-06); grep gates confirm private copies deleted |
| T-39-SC | Tampering | npm/pip/cargo installs | accept | No package installs in this plan; CLAUDE.md forbids new deps without approval |
</threat_model>

<verification>
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` → 0 errors, 0 new warnings.
- VSTest is unreliable in WSL (PROJECT constraint) → the named suites run in CI: `DeckAnalysisPacketServiceTests`, `DeckComparisonServiceTests`, `MetaGapServiceTests`, `DeckPrimerPacketServiceTests`, `AiPlatformPhase10RoundTripTests`, `ResultContractTests`, plus the new `DeckEntryLoaderTests`. Push-and-watch CI is the test proof point; local proof = clean build + grep gates.
- Grep gate: zero private `LoadDeckEntriesAsync` remain across the four services.
</verification>

<success_criteria>
- All four packet services load decks via the extended `IDeckEntryLoader`; no private `LoadDeckEntriesAsync` survives.
- Moxfield fallback notice still surfaces for Analysis + Primer; Comparison + MetaGap unchanged.
- `dotnet build DeckFlow.sln` clean (0 new warnings); cache-key canonical text byte-identical (D-06); no `PromptBuilders/**` edits (D-07).
- Existing test suites + new loader tests green in CI.
</success_criteria>

<output>
Create `.planning/phases/39-architecture-review/39-01-SUMMARY.md` when done. Record: the MetaGap paste-cascade equivalence finding (identical end-to-end or flagged), which importer/parser fields were removed vs retained per service, and confirmation that `_lastImportNotice` flow position is preserved.
</output>
