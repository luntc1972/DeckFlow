---
phase: 39-architecture-review
plan: 03
type: execute
wave: 3
depends_on: ["39-02"]
files_modified:
  - DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs
  - DeckFlow.Web/Services/DeckAnalysisPacketService.cs
  - DeckFlow.Web/Program.cs
  - DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs
autonomous: true
requirements: [ARCH-02]

must_haves:
  truths:
    - "Deck Analysis packets resolve Scryfall card references, the 3-stage fuzzy fallback, the cards/named fuzzy stage, and the commander-eligibility lookup byte-identically to before."
    - "Deck Analysis no longer owns a private SearchFallbackCardAsync or Scryfall Func seams; it routes through the injected IScryfallCardResolver."
    - "Scryfall traffic still routes through ScryfallThrottle (incl. ThrowIfUpstreamUnavailable) and the named scryfall pipeline unchanged."
    - "All existing analysis + round-trip + contract tests pass unchanged."
  artifacts:
    - path: "DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs"
      provides: "Resolver gains the Analysis 3-stage fallback (printed:/name: + cards/named fuzzy) + NormalizeLookupName/NormalizeForScryfall + named-execute delegate"
      contains: "SearchPrintingFallbackCardAsync"
    - path: "DeckFlow.Web/Services/DeckAnalysisPacketService.cs"
      provides: "Analysis consuming IScryfallCardResolver; private SearchFallbackCardAsync + 3 Scryfall Func seams deleted"
  key_links:
    - from: "DeckFlow.Web/Services/DeckAnalysisPacketService.cs"
      to: "IScryfallCardResolver"
      via: "injected resolver"
      pattern: "_scryfallCardResolver\\."
    - from: "DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs"
      to: "ScryfallThrottle.ThrowIfUpstreamUnavailable"
      via: "preserved in the Analysis fallback path"
      pattern: "ThrowIfUpstreamUnavailable"
---

<objective>
Migrate Deck Analysis — the third and final Scryfall-touching packet service — onto `IScryfallCardResolver`, preserving its DISTINCT richer fallback behavior (which is NOT byte-identical to Comparison/MetaGap). Analysis uses a 3-stage fallback: two `cards/search` queries (`printed:`/`name:` then bare name, `unique=prints`, `include_multilingual`), then a `cards/named?fuzzy=` stage, plus `NormalizeLookupName`/`NormalizeForScryfall` and a third `Func<RestRequest,...,ScryfallCard>` named-execute seam.

Purpose: Complete the D-04 Scryfall-hydration consolidation by moving Analysis's transport + its richer fallback + the three Func seams onto the resolver, leaving zero private `SearchFallbackCardAsync` across all four packet services — WITHOUT changing any produced byte.

Output: `ScryfallCardResolver` extended with the Analysis fallback + named-execute delegate + the two normalizers; Analysis consumes it; Analysis's three Func seams + private `SearchFallbackCardAsync` deleted; DI + TestServiceFactory updated.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/phases/39-architecture-review/39-CONTEXT.md
@.planning/phases/39-architecture-review/39-02-SUMMARY.md
@./CLAUDE.md

<key_findings>
**Analysis's fallback is NOT the shared one.** It must be preserved as a SEPARATE resolver method (do NOT collapse into the Comparison/MetaGap `SearchFallbackCardAsync`). Exact current behavior (DeckAnalysisPacketService.cs ~1259-1304):
  1. If `string.IsNullOrWhiteSpace(cardName)` → null.
  2. `normalizedCardName = NormalizeLookupName(cardName)`.
  3. For each of two queries: `(printed:"{NormalizeForScryfall(cardName)}" OR name:"{NormalizeForScryfall(cardName)}")` then `NormalizeForScryfall(cardName)` — `RestRequest("cards/search", Get)`, `q=query`, `unique=prints`, `include_multilingual=true`; `ScryfallThrottle.ThrowIfUpstreamUnavailable(response.StatusCode)`; on non-2xx/null `continue`; match = `Data.FirstOrDefault(card => NormalizeLookupName(card.Name) == normalizedCardName) ?? Data.FirstOrDefault()`; return match if found.
  4. Then `cards/named?fuzzy={NormalizeForScryfall(cardName)}` via `_executeNamedAsync` (`RestResponse<ScryfallCard>`); `ThrowIfUpstreamUnavailable`; on 2xx + non-null Data return Data; else null.

`NormalizeLookupName` (curly→straight quotes/apostrophes, en/em-dash→hyphen, ToLowerInvariant) and `NormalizeForScryfall` (" / " → " // ") are used by this fallback AND elsewhere in Analysis (NormalizeForScryfall is used in `LookupCardReferencesAsync` to build the collection-request identifiers, and in `ValidateCommanderAsync`). So these two normalizers must be available to BOTH the resolver and the remaining Analysis code.

**Three Func seams to move** (DeckAnalysisPacketService.cs ~77-79): `_executeCollectionAsync`, `_executeSearchAsync`, `_executeNamedAsync` (the named one is unique to Analysis). The resolver already owns collection + search execute (Plan 02). Add a third `_executeNamedAsync` (`Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>`) to the resolver.

**Analysis collection loop** (`LookupCardReferencesAsync`) builds identifiers with `NormalizeForScryfall(card.Name)` and has its OWN error message ("...returned HTTP {n} while building the analysis packet."). Keep that loop + message + the `resolvedCards` dictionary + `CardReference` mapping IN Analysis; route only the HTTP execute + the fallback + (optionally) the normalizers through the resolver.

**Commander eligibility:** `ValidateCommanderAsync` calls `SearchFallbackCardAsync(commanderName, ...)`. After migration it must call the resolver's Analysis-fallback method so the eligibility lookup stays byte-identical.
</key_findings>

<interfaces>
From DeckFlow.Web/Services/DeckAnalysisPacketService.cs (seams to MOVE):
```csharp
private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>> _executeCollectionAsync;
private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>> _executeSearchAsync;
private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>> _executeNamedAsync;
private static string NormalizeLookupName(string cardName);   // used by fallback + match comparison
private static string NormalizeForScryfall(string cardName);  // used by fallback AND collection identifiers AND ValidateCommanderAsync
```

From DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs (Analysis seam params — must keep flowing):
```csharp
Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsync = null,
Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null,
Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsync = null
```
</interfaces>

@DeckFlow.Web/Services/DeckAnalysisPacketService.cs
@DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs
</context>

<tasks>

<task type="auto">
  <name>Task 1: Extend IScryfallCardResolver with the Analysis 3-stage fallback + named-execute seam + normalizers</name>
  <files>DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs, DeckFlow.Web/Program.cs</files>
  <action>
Add to `IScryfallCardResolver` + impl, WITHOUT altering the Plan-02 members:
  - A third execute delegate `_executeNamedAsync` (`Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>`) built in the public DI ctor exactly like the others (ScryfallThrottle + "scryfall" pipeline + `client.ExecuteAsync<ScryfallCard>`), and as a new optional override param on the internal test ctor.
  - `Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)` — the Analysis 3-stage fallback moved VERBATIM (the two `cards/search` queries with `unique=prints`/`include_multilingual`, the `ThrowIfUpstreamUnavailable` calls, the `NormalizeLookupName(card.Name) == normalizedCardName ?? FirstOrDefault` match, then the `cards/named?fuzzy=` stage via `_executeNamedAsync`). Name it distinctly from the shared `SearchFallbackCardAsync` so BOTH behaviors coexist (Comparison/MetaGap use the simple one, Analysis uses this one).
  - Expose `NormalizeLookupName` and `NormalizeForScryfall` as PUBLIC static methods on the resolver class (or as static helpers in a co-located internal static class) so Analysis's remaining collection-identifier building + `ValidateCommanderAsync` can call the SAME implementation. Move the method bodies verbatim. Keep the XML doc comment on `NormalizeForScryfall` (project requires doc comments on public members).

Program.cs registration is unchanged (the resolver is already registered in Plan 02); only the resolver class grows. Build must stay green as a standalone commit (the new members are additive; nothing consumes them yet).
  </action>
  <acceptance_criteria>
    - Resolver gains `SearchPrintingFallbackCardAsync`, a named-execute delegate + internal override, and public `NormalizeLookupName`/`NormalizeForScryfall`.
    - The 3-stage fallback body (queries, `ThrowIfUpstreamUnavailable`, match logic, `cards/named` stage) is byte-identical to Analysis's current `SearchFallbackCardAsync`.
    - Plan-02 resolver members unchanged; solution builds clean.
  </acceptance_criteria>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/deckflow && grep -c "ThrowIfUpstreamUnavailable\|cards/named\|SearchPrintingFallbackCardAsync" DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs  # expect >=3</automated>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln 2>&1 | grep -E "error|warning" | grep -v "^$" | wc -l  # expect 0</automated>
  </verify>
  <done>Resolver carries both fallback behaviors + named seam + normalizers, build clean. Commit: `refactor(39): add Analysis 3-stage fallback + named seam to IScryfallCardResolver`.</done>
</task>

<task type="auto">
  <name>Task 2: Migrate Deck Analysis onto IScryfallCardResolver; delete its 3 Func seams + private SearchFallbackCardAsync</name>
  <files>DeckFlow.Web/Services/DeckAnalysisPacketService.cs, DeckFlow.Web/Program.cs, DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs</files>
  <action>
**DeckAnalysisPacketService:**
  - Inject `IScryfallCardResolver`. Remove the three Func fields (`_executeCollectionAsync`, `_executeSearchAsync`, `_executeNamedAsync`), their three override ctor params, `restClientOverride`, and the delegate-construction block. Remove the `IScryfallRestClientFactory` + `ResiliencePipelineProvider<string>` ctor params IF grep confirms they are now unused in this service (they exist only to build the three delegates → remove both).
  - `LookupCardReferencesAsync`: keep the chunk loop, the analysis-specific error message, the `resolvedCards`/`CardReference` mapping, the `ExtractMechanicNames` calls — UNCHANGED. Replace `await _executeCollectionAsync(request, ct)` with `await _scryfallCardResolver.ExecuteCollectionAsync(request, ct)`. For identifier building, replace local `NormalizeForScryfall(card.Name)` with `ScryfallCardResolver.NormalizeForScryfall(card.Name)` (or the resolver instance method) so behavior is identical. Replace `await SearchFallbackCardAsync(unresolvedRequest.Name, ct)` with `await _scryfallCardResolver.SearchPrintingFallbackCardAsync(unresolvedRequest.Name, ct)`. The `displayName` logic uses `NormalizeLookupName(...)` — repoint to the resolver's `NormalizeLookupName` so the same normalization decides the display string.
  - `ValidateCommanderAsync`: replace `await SearchFallbackCardAsync(commanderName, ct)` with `await _scryfallCardResolver.SearchPrintingFallbackCardAsync(commanderName, ct)`.
  - DELETE the private `SearchFallbackCardAsync` and the private static `NormalizeLookupName` + `NormalizeForScryfall` (now on the resolver). If any OTHER Analysis code referenced these privates, repoint those references to the resolver's versions too — grep `NormalizeForScryfall\|NormalizeLookupName` in the file and repoint EVERY call site before deleting.

**Program.cs:** add `sp.GetRequiredService<IScryfallCardResolver>()` to the Analysis factory; remove the now-unused `IScryfallRestClientFactory` / `ResiliencePipelineProvider<string>` args.

**TestServiceFactory:** `CreateDeckAnalysisPacketService` keeps its three seam params (`executeCollectionAsync`, `executeSearchAsync`, `executeNamedAsync`) so `DeckAnalysisPacketServiceTests` compiles unchanged. Route them into a `ScryfallCardResolver` built via its internal test ctor (now accepting all three overrides), and inject that resolver. ZERO edits to `DeckAnalysisPacketServiceTests.cs`.

Do NOT touch cache-key helpers / `ResolvePreScryfallCommanderState` / `BuildDeckAnalysisCacheInputs` (D-06) or `PromptBuilders/**` (D-07).
  </action>
  <acceptance_criteria>
    - `grep -c "private async Task<ScryfallCard?> SearchFallbackCardAsync" DeckFlow.Web/Services/DeckAnalysisPacketService.cs` → `:0`.
    - No `_executeCollectionAsync` / `_executeSearchAsync` / `_executeNamedAsync` fields remain in Analysis.
    - Analysis references `_scryfallCardResolver.ExecuteCollectionAsync` and `_scryfallCardResolver.SearchPrintingFallbackCardAsync`.
    - All four packet services now have ZERO private `SearchFallbackCardAsync` and ZERO private `LoadDeckEntriesAsync` (combined with Plans 01/02).
    - `DeckAnalysisPacketServiceTests.cs` source unchanged.
  </acceptance_criteria>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/deckflow && grep -rc "private async Task<ScryfallCard?> SearchFallbackCardAsync" DeckFlow.Web/Services/DeckComparisonService.cs DeckFlow.Web/Services/MetaGapService.cs DeckFlow.Web/Services/DeckAnalysisPacketService.cs  # expect all :0</automated>
    <automated>cd /mnt/c/users/chrislunt/source/personal/deckflow && grep -c "_executeCollectionAsync\|_executeSearchAsync\|_executeNamedAsync" DeckFlow.Web/Services/DeckAnalysisPacketService.cs  # expect 0</automated>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln 2>&1 | grep -E "error|warning" | grep -v "^$" | wc -l  # expect 0</automated>
  </verify>
  <done>Analysis consumes the resolver; all four packet services free of private Scryfall + deck-load duplication; build clean. Commit: `refactor(39): migrate Deck Analysis onto IScryfallCardResolver`.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| app → Scryfall API | Outbound HTTP. Unchanged: same throttle + ThrowIfUpstreamUnavailable + pipeline + request shapes. |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-39-04 | Tampering | Analysis fallback / normalizer / commander-eligibility drift | mitigate | 3-stage fallback + normalizers moved verbatim; every call site repointed to the same implementation; `DeckAnalysisPacketServiceTests` (alternate names, commander eligibility, MDFC) assert output |
| T-39-05 | Denial of Service | Throttle bypass on Analysis path | mitigate | Resolver wraps named + search + collection in ScryfallThrottle; `ThrowIfUpstreamUnavailable` preserved (grep gate) |
| T-39-SC | Tampering | npm/pip/cargo installs | accept | No package installs; no new deps |
</threat_model>

<verification>
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` → 0 errors, 0 new warnings.
- CI runs `DeckAnalysisPacketServiceTests`, `AiPlatformPhase10RoundTripTests`, `ResultContractTests` (VSTest unreliable in WSL → push-and-watch CI is the test proof).
- Grep gates: zero private `SearchFallbackCardAsync` across all three Scryfall services; zero Analysis Func seam fields; `ThrowIfUpstreamUnavailable` present in the resolver.
</verification>

<success_criteria>
- Deck Analysis resolves Scryfall references + its 3-stage fallback + commander eligibility via the injected `IScryfallCardResolver`, byte-identically.
- All four packet services are free of private `LoadDeckEntriesAsync` (Plan 01) and private `SearchFallbackCardAsync` (Plans 02/03); the three Func seams now live on the resolver's internal test ctor.
- Scryfall traffic still routes through ScryfallThrottle (+ ThrowIfUpstreamUnavailable) and the named pipeline.
- `dotnet build` clean; no cache-key (D-06) or PromptBuilders (D-07) edits; existing suites green in CI; resolver internal test seams preserved (D-04, SC4).
</success_criteria>

<output>
Create `.planning/phases/39-architecture-review/39-03-SUMMARY.md` when done. Record: confirmation that all four packet services are duplication-free, that both fallback behaviors (simple + 3-stage) coexist on the resolver, where `NormalizeForScryfall`/`NormalizeLookupName` now live, and that no test files were edited.
</output>
