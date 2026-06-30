# Phase 78: Auto-Refreshing Primer (stale flag) - Research

**Researched:** 2026-06-29
**Domain:** ASP.NET 10 MVC Razor view-model flag + canonical deck-hash reuse + golden tests
**Confidence:** HIGH (all findings grounded in cycle13 worktree source with file:line citations)

## Summary

This phase adds a purely-informational "deck changed since this primer was generated" banner to the Deck Primer page (`/deck-primer`). It is a server-computed boolean plus an optional changed-card count, never an auto-rebuild. The implementation is small in surface area but has one non-obvious correctness pivot and one inaccurate assumption in the UI-SPEC that the planner must resolve before writing tasks.

**The correctness pivot (most important finding):** PRIMER-02 says "reuse the primer's existing cache-key computation." The full primer cache key (`PacketSessionCache.ComputeKey(PrimerCacheInputs)`, `DeckPrimerPacketService.cs:204-212`) folds in commander name, target bracket, primer style, selected section IDs, and the Gemini-enabled flag *in addition to* the deck. Reusing that whole key as the staleness hash would make the banner fire when the user changes sections/style/bracket — which contradicts the spec (those are not deck changes). The correct primitive to reuse is the **deck component only**: `BuildCanonicalDeckSourceText(entries)` (`DeckPrimerPacketService.cs:719-736`), which emits `board|quantity|name` lines sorted by board, then name, then quantity. That string already excludes printing (no SetCode/CollectorNumber) and is order-independent (sorted), so hashing it gives exactly the multiset semantics PRIMER-02 demands: reorder = fresh, printing-swap = fresh, add/remove/qty-change = stale.

**The inaccurate UI-SPEC assumption (Open Q2):** §1 of the UI-SPEC states the generated-primer hash "already persists in the download/upload `.zip`, so the stored hash travels with the restored primer." This is **not true today**. The primer zip persists the canonical deck list and a request-context block, but `PacketArtifactStore.LoadPrimerFromZip` (`PacketArtifactStore.cs:578-664`) restores **only** bracket / AI platform / primer style / selected sections — it does not restore the deck, does not restore the generated primer text, and there is no hash field anywhere. There is no `generatedPrimerHash` persisted. The planner must add a new round-trip field (precedent: phase-77 `ScoreJson`) and decide how far to extend the restore path.

**Primary recommendation:** Compute the staleness hash by reusing `BuildCanonicalDeckSourceText` (expose it `internal static`) fed to `PacketSessionCache.ComputeKey`. Carry the generated-primer hash as a hidden round-trip field on `DeckPrimerRequest` mirroring `ScoreJson` (`DeckAnalysisRequest.cs:140-144` + `DeckAnalysis.cshtml:516-521`). Compute the stale boolean + changed-card count in the controller (or a thin service helper), set them on `DeckPrimerViewModel`, and render the banner in `DeckPrimer.cshtml` Step 3. Gate the whole feature behind a new flag `tool.primer.stale-flag` seeded OFF, consumed via the explicit `Snapshot().TryGetValue(key, out var on) && on` pattern (NOT `IsEnabled()`), so flag-OFF output is byte-identical to today.

## User Constraints

No CONTEXT.md exists for this phase yet (only UI-SPEC.md). The UI-SPEC is the binding design contract; its §10 open questions are resolved below. Project-level constraints from `CLAUDE.md` that bind this phase:

- ASP.NET 10 + Razor, no framework migration.
- Theme CSS: layout/cross-cutting goes in `site-common.css`; never fork per-theme for this. (UI-SPEC §3 already specifies the one net-new class lives in `site-common.css`.)
- Public repo, no secrets.
- Commits: plain default-author, no Co-Authored-By trailer; update README when behavior changes.
- `.editorconfig` carve-outs: never convert `{ get; init; }` to get-only (System.Text.Json drops get-only members in .NET 9+); never re-indent raw-string literals; preserve LF.
- No new NuGet/npm packages without asking. (This phase needs none.)
- Cycle convention (TAP-04 / BRACKET-05 / SCORE-01): new behavior is flag-gated, seeded OFF in prod, byte-identical when OFF.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PRIMER-01 | Stale indicator when source deck changed since generation | `DeckPrimerViewModel` gains `IsStale`/`ChangedCardCount`; controller computes; banner in `DeckPrimer.cshtml` Step 3 reusing `.deck-restored-notice` + net-new `.deck-restored-notice--stale` (verified primitives exist) |
| PRIMER-02 | Multiset hash; reorder/printing=fresh, add/remove/qty=stale; reuse existing cache-key computation | Reuse `BuildCanonicalDeckSourceText` (`DeckPrimerPacketService.cs:719-736`) + `PacketSessionCache.ComputeKey` (`PacketSessionCache.cs:52-59`). NOT the full `PrimerCacheInputs` key |
| PRIMER-03 | Explicit regenerate only, no auto-rebuild/re-fetch, flag never clobbers | Banner is a read-only view-model boolean; existing `Generate Primer` submit (`DeckPrimer.cshtml:272`) is the only regenerate path; no new fetch introduced |
| PRIMER-04 | Golden tests lock semantics, same change | xUnit tests in `DeckFlow.Web.Tests` mirroring `DeckPrimerPacketServiceTests.cs:158-224` cache-key equality/inequality pattern; diff-count tests can also live in `DeckFlow.Core.Tests` next to `DiffEngineTests.cs` |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Canonical deck multiset hash | API/Backend (`DeckPrimerPacketService` in `DeckFlow.Web/Services`) | — | Hash must be identical to the cache-key path; lives where `BuildCanonicalDeckSourceText` already lives |
| Stale boolean + changed-card count | API/Backend (controller or thin service helper) | — | Server truth per UI-SPEC §1/§4; never client-side |
| Changed-card count (multiset diff cardinality) | Domain (`DeckFlow.Core/Diffing/DiffEngine`) | — | Pure CPU diff already implemented; reuse rather than hand-roll |
| Hidden hash round-trip across posts | Frontend Server (Razor hidden field) + request DTO | — | Mirror phase-77 `ScoreJson` round-trip field |
| Banner render | Frontend Server (Razor `DeckPrimer.cshtml`) | — | Server-rendered, no JS; reuses `.run-button` for regenerate submit |
| Flag gate | API/Backend (`IFeatureFlagCache`) | — | Same gating tier as TAP/BRACKET/SCORE siblings |

## Standard Stack

No new libraries. Everything needed is already in the solution.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Security.Cryptography (SHA-256) | .NET 10 BCL | Hash the canonical deck text | Already used by `PacketSessionCache.ComputeKey` (`PacketSessionCache.cs:1,52-59`) |
| System.Text.Json | .NET 10 BCL | Deterministic serialize before hash | Already used by `ComputeKey` (`PacketSessionCache.cs:23-26,56`) |
| xUnit 2.9.3 | pinned | Golden tests (PRIMER-04) | Both test projects use it |

## Package Legitimacy Audit

**No external packages are installed by this phase.** All capabilities reuse existing in-solution BCL and project code. No npm, NuGet, or other registry additions. slopcheck/registry verification not applicable.

## Architecture Patterns

### System Architecture Diagram

```
                      ┌──────────────────────────────────────────────┐
 Browser POST         │  DeckPrimerController.DeckPrimer (POST)       │
 /deck-primer  ─────► │   - BuildAsync(request) → primer text + hash  │
 (deck + hidden       │   - currentDeckHash = Hash(canonical(deck))   │
  GeneratedPrimerHash)│   - isStale = hashPresent && current != saved │
                      │   - changedCount = DiffEngine(savedDeck,now)  │  (count source: Open Q3)
                      └───────────────┬──────────────────────────────┘
                                      │ ViewModel { PrimerPromptText,
                                      │   IsStale, ChangedCardCount,
                                      │   GeneratedPrimerHash }
                                      ▼
                      ┌──────────────────────────────────────────────┐
                      │  DeckPrimer.cshtml                            │
                      │   Step 3: if (flagOn && IsStale)              │
                      │     render .deck-restored-notice--stale       │
                      │       + [Regenerate primer] (run-button       │
                      │         submit → re-POST /deck-primer)        │
                      │   hidden: GeneratedPrimerHash (when present)  │
                      └──────────────────────────────────────────────┘

  Flag gate: IFeatureFlagCache.Snapshot().TryGetValue("tool.primer.stale-flag", out on) && on
             (absent/null/failure ⇒ OFF ⇒ banner never rendered, no hidden field ⇒ byte-identical)
```

### Recommended structure (files touched)

```
DeckFlow.Web/
├── Controllers/DeckPrimerController.cs     # compute isStale + count, set on VM (3 actions render the view)
├── Models/DeckPrimerRequest.cs             # + GeneratedPrimerHash { get; set; } hidden round-trip field
├── Models/DeckPrimerViewModel.cs           # + IsStale, ChangedCardCount, GeneratedPrimerHash { get; init; }
├── Services/DeckPrimerPacketService.cs     # expose deck-hash helper (BuildCanonicalDeckSourceText internal static; add ComputeDeckMultisetHashAsync)
├── Services/FeatureFlags/FeatureFlagCatalog.cs  # + description for tool.primer.stale-flag
├── Services/FeatureFlags/FeatureFlagStore.cs    # + seed row (Postgres FALSE, SQLite 0)
├── Views/Deck/DeckPrimer.cshtml            # banner in Step 3 (+ optional Step 2 mirror), hidden hash field
└── wwwroot/css/site-common.css             # .deck-restored-notice--stale (one net-new class)
DeckFlow.Web.Tests/                          # golden tests (multiset equivalence + count)
```

### Pattern 1: Reuse the deck-only canonical text, not the full cache key
**What:** Hash `BuildCanonicalDeckSourceText(entries)` (the deck component) via `PacketSessionCache.ComputeKey`, NOT `TryComputeCacheKeyAsync` (which also hashes bracket/style/sections/gemini).
**Why:** The full key's `PrimerCacheInputs` (`DeckPrimerPacketService.cs:204-212,739-745`) mixes deck + options. Section/style/bracket changes are not deck changes; the banner must ignore them per UI-SPEC §9.
**Example (existing canonical-text source):**
```csharp
// Source: DeckPrimerPacketService.cs:719-736 (currently private static)
private static string BuildCanonicalDeckSourceText(IReadOnlyList<DeckEntry> entries)
{
    var builder = new StringBuilder();
    foreach (var entry in entries
                 .OrderBy(entry => entry.Board, StringComparer.OrdinalIgnoreCase)
                 .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                 .ThenBy(entry => entry.Quantity))
    {
        builder.Append(entry.Board); builder.Append('|');
        builder.Append(entry.Quantity); builder.Append('|');
        builder.Append(entry.Name); builder.AppendLine();
    }
    return builder.ToString().TrimEnd();
}
```
Recommend: change to `internal static` (or add a public `ComputeDeckMultisetHashAsync(request, ct)` that loads entries via the same loader path as `TryComputeCacheKeyAsync`, lines 157-180) and feed the text to `PacketSessionCache.ComputeKey`. This guarantees byte-identical canonicalization with the cache key's deck component.

### Pattern 2: Hidden round-trip field (mirror ScoreJson)
**What:** Add `GeneratedPrimerHash` to `DeckPrimerRequest` and render it as a hidden field only when present.
**Why:** The hash captured at generation must travel with the form so the next render can compare. Rendering only when present keeps the flag-OFF page byte-identical.
**Example:**
```csharp
// Source: DeckAnalysisRequest.cs:140-144 (precedent)
public string ScoreJson { get => _scoreJson; set => _scoreJson = value ?? string.Empty; }
```
```cshtml
@* Source: DeckAnalysis.cshtml:516-521 (precedent) *@
@if (!string.IsNullOrEmpty(Model.Request.ScoreJson))
{
    <textarea name="ScoreJson" hidden aria-hidden="true" tabindex="-1">@Model.Request.ScoreJson</textarea>
}
```
Note carve-out: keep `{ get; init; }` on view-model props and `{ get; set; }` with null-coalescing on the request DTO (matches existing `DeckPrimerRequest` style, lines 86-100). Never convert to get-only.

### Pattern 3: Explicit-snapshot flag gate (byte-identical OFF)
**What:** Gate on `Snapshot().TryGetValue(key, out var on) && on`, never `IsEnabled()`.
**Why:** `IFeatureFlagCache.IsEnabled` returns **true (default-on)** for an absent key (`IFeatureFlagCache.cs:13-20`). Experiment flags that must be OFF-by-default use the explicit snapshot read.
**Example:**
```csharp
// Source: DeckAnalysisPacketService.cs:401-403 (multi-axis-score, the closest sibling precedent)
var scoreFlagEnabled = _flagCache is not null
    && _flagCache.Snapshot().TryGetValue(MultiAxisScoreFlag, out var scoreFlagOn)
    && scoreFlagOn;
```

### Anti-Patterns to Avoid
- **Reusing the full `TryComputeCacheKeyAsync` as the staleness hash** — false-positives on section/style/bracket edits (violates UI-SPEC §9).
- **Using `IsEnabled()` for the new flag** — defaults missing keys ON, breaking the seeded-OFF / byte-identical contract.
- **Computing the banner client-side** — UI-SPEC §1/§4/§8 require server truth, no JS dismiss.
- **Auto-rebuilding or re-fetching on stale** — PRIMER-03; the banner is read-only.
- **A second, divergent hash path** — must canonicalize identically to the cache key's deck component or fresh/stale will disagree with the cache.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Canonical deck multiset text | A new name+qty serializer | `BuildCanonicalDeckSourceText` (`DeckPrimerPacketService.cs:719-736`) | Must match the cache key's deck component exactly |
| SHA-256 hashing | New crypto call | `PacketSessionCache.ComputeKey` (`PacketSessionCache.cs:52-59`) | Already deterministic + tested |
| Changed-card count (add/remove/qty, excluding printing) | New diff loop | `DiffEngine.Compare` → `DeckDiff` (`DiffEngine.cs:27-102`), Loose mode | Loose match keys on name+board (`DiffEngine.cs:173-181`), reports printing conflicts separately so they can be excluded from the stale count |
| Flag plumbing | New config | `IFeatureFlagCache` + catalog + seed | Established pattern; guarded by `FeatureFlagCatalogTests` / `FeatureFlagStoreSeedTests` |
| Stale banner CSS | New component | `.deck-restored-notice` (site-common.css) + `.deck-restored-notice--stale` modifier with `var(--gold-warning,#c8a040)` | Verified `.deck-restored-notice` lives only in site-common.css; `--gold-warning` token already defined in theme files with a hex fallback for any gaps |

**Key insight:** The hash, the canonicalization, the diff, and the flag plumbing all already exist. The net-new code is: one hidden field, two view-model properties, a banner partial/block, one CSS modifier, one flag row+description, and the controller wiring that compares two hashes.

## Resolved Open Questions (UI-SPEC §10)

**1. Mirror at Step 2?** Recommend **Step 3 only** for the first cut. Step 2 (`#primer-step-panel-2`, `DeckPrimer.cshtml:143`) is the customization form; the stale flag is a statement about the *displayed artifact* in Step 3. If mirrored later, it must be the same `Model.IsStale` boolean rendered twice — never a second computation. (Confidence: HIGH — grounded in view structure.)

**2. Hash persistence across `.zip` resume — INACCURATE in UI-SPEC.** The primer zip persists `10-primer-deck-list.txt` (canonical deck) and `01-primer-request-context.txt` (with a `deck_source:` block), but `LoadPrimerFromZip` (`PacketArtifactStore.cs:578-664`) restores **only** `target_commander_bracket`, `target_ai_platform`, `primer_style`, and `selected_section_ids`. It does **not** restore the deck, the generated primer text, or any hash. There is no `generatedPrimerHash` field today. Consequences the planner must decide on:
   - The **realistic, low-risk scope** is the **in-session** path: carry `GeneratedPrimerHash` as a hidden field set whenever a primer is generated; compare on the next render against the current deck hash.
   - Faithful `.zip`-resume staleness additionally requires: a new persisted artifact (e.g. `02-primer-deck-hash.txt`) or a `generated_primer_hash:` line in the request-context, **plus** extending `LoadPrimerFromZip` to restore the deck text and the generated primer text so there is something to mark stale. Note: today, uploading a primer zip leaves `DeckSource` empty, so `BuildAsync`'s first guard (`DeckPrimerPacketService.cs:220-223`) throws and the upload catch (`DeckPrimerController.cs:235-248`) re-renders with an error and a fresh request. Confirm/spec this before scoping zip-resume staleness.
   - New field, when added, must be `{ get; init; }` on the view model and `{ get; set; }` with null-coalescing on the request DTO — never get-only (STJ carve-out). (Confidence: HIGH — grounded in restore code.)

**3. Changed-card count source.** `TryComputeCacheKeyAsync` returns **only the final hash string** (`DeckPrimerPacketService.cs:148-213`) — it does not expose the per-card multiset, so the count cannot come from the cache-key path. Two viable options:
   - **Preferred:** parse both decks into `List<DeckEntry>` (the loader already does this inside the hash path) and call `DiffEngine.Compare(saved, current)` in **Loose** mode; the count = `ToAdd.Count + CountMismatch.Count + OnlyInArchidekt.Count`, **excluding** `PrintingConflicts` (so printing swaps contribute 0, consistent with fresh). This requires the saved deck entries to be available (see Q2 — only feasible when the source deck at generation is persisted/restored).
   - **Fallback:** if the saved deck content is not available (e.g. only the hash survived), use the **count-suppressed microcopy** variant (UI-SPEC §5 line 101): "Deck changed since this primer was generated. Regenerate to refresh the primer." This avoids a second, lossy pass. (Confidence: HIGH.)

**4. Focus-on-regenerate.** Feasible: the regenerate button is a real `type="submit"` re-posting `/deck-primer`; the page re-renders server-side. Moving focus to `#primer-output` would be a small client-side enhancement after successful regenerate. Recommend treating as an a11y nicety (optional), not a blocker. (Confidence: MEDIUM — standard post-submit behavior; exact focus move is a JS detail.)

**5. Mobile wrap.** Defer to live 390px visual verify per the UI-phase rule (memory: UI phases need visual verify at 2+ viewports across site/azorius/nyx). Only add `.deck-restored-notice--stale { flex-wrap: wrap; }` if crowding is observed. (Confidence: HIGH — matches project convention.)

**6. Flag gating — YES, gate it.** Although REQUIREMENTS lists no flag, every cycle-13 sibling gated its feature seeded OFF: `analysis.manabase.tap-analyzer` (Phase 75), `tool.bracket.enabled` (Phase 76), `analysis.multi-axis-score` (Phase 77) — all seeded FALSE/0 (`FeatureFlagStore.cs:226-229, 261-264`) and consumed via explicit `Snapshot().TryGetValue` (`DeckAnalysisPacketService.cs:401-403`). Recommend **`tool.primer.stale-flag`** (or `analysis.primer.stale-flag`). It must be added to: `FeatureFlagCatalog.Descriptions` (`FeatureFlagCatalog.cs:14-80`), both seed blocks (`FeatureFlagStore.cs:198-231` Postgres FALSE, `FeatureFlagStore.cs:233-266` SQLite 0), and a `[InlineData]` row in `FeatureFlagStoreSeedTests.cs:40-44`. `FeatureFlagCatalogTests` will fail if a seeded key lacks a description. (Confidence: HIGH.)

## Where the stale boolean + count should be computed

**Recommendation: controller, calling a thin service helper.** The three view-rendering actions (`DeckPrimer` POST `:62-111`, `DeckPrimerUpload` `:189-284`; the GET `:42-53` is never stale) each already build a `DeckPrimerViewModel`. After `BuildAsync`, the controller should:
1. Read the flag (`Snapshot().TryGetValue("tool.primer.stale-flag", out var on) && on`). If OFF, set nothing (byte-identical).
2. If ON and `request.GeneratedPrimerHash` is non-empty: compute `currentDeckHash` via the new service helper (reusing `BuildCanonicalDeckSourceText` + `ComputeKey`), set `IsStale = currentDeckHash != request.GeneratedPrimerHash`, and `ChangedCardCount` via `DiffEngine` when the saved deck is available (else suppress).
3. Always set `GeneratedPrimerHash` on the view model to the **current** deck's hash after a fresh generate (so the hidden field re-arms for the next post).

Put the hash + count computation in `DeckPrimerPacketService` (it owns the loader and canonicalization), exposed as e.g. `Task<string?> ComputeDeckMultisetHashAsync(DeckPrimerRequest, CancellationToken)` and a `ComputeChangedCardCount(savedEntries, currentEntries)`. Keep the controller thin (it already catches the same exception families).

## Common Pitfalls

### Pitfall 1: Hash drift between cache key and staleness flag
**What goes wrong:** A second canonicalization path yields a different hash than the cache key's deck component; fresh/stale disagrees with cache hits.
**How to avoid:** Reuse the exact `BuildCanonicalDeckSourceText` output; do not re-implement.
**Warning sign:** A reorder or printing-swap unexpectedly marks stale.

### Pitfall 2: Flag-OFF not byte-identical
**What goes wrong:** Rendering the hidden hash field or any markup when the flag is OFF changes the page bytes.
**How to avoid:** Gate both the hidden field and the banner on the explicit-snapshot flag; render nothing when OFF (mirror `ScoreJson`'s "only when present").
**Warning sign:** A diff of the flag-OFF page vs. baseline shows any change.

### Pitfall 3: `IsEnabled()` default-on trap
**What goes wrong:** Using `IsEnabled("tool.primer.stale-flag")` returns true if the key is somehow absent from the snapshot, turning the feature on in prod before intended.
**How to avoid:** Use `Snapshot().TryGetValue(...) && on` (`DeckAnalysisPacketService.cs:401-403`).

### Pitfall 4: Counting printing swaps as changes
**What goes wrong:** A naive entry diff counts a set/collector-number change as a delta, contradicting "printing-swap = fresh."
**How to avoid:** Use `DiffEngine` Loose mode and exclude `PrintingConflicts` from the count (`DiffEngine.cs:73-77,183-192`).

### Pitfall 5: Zip-resume assumption (see Q2)
**What goes wrong:** Planning zip-resume staleness on the false premise that the hash already round-trips. It does not.
**How to avoid:** Scope to in-session first, or explicitly plan the new persisted artifact + restore extension.

## Code Examples

### Reading the flag (controller)
```csharp
// Source pattern: DeckAnalysisPacketService.cs:401-403
private bool StaleFlagEnabled() =>
    _flagCache is not null
    && _flagCache.Snapshot().TryGetValue("tool.primer.stale-flag", out var on)
    && on;
```

### Hashing the deck multiset (service helper)
```csharp
// Reuses PacketSessionCache.ComputeKey (PacketSessionCache.cs:52-59)
// and the canonical text from DeckPrimerPacketService.cs:719-736.
var canonical = BuildCanonicalDeckSourceText(entries);   // internal static after exposure
var deckHash  = PacketSessionCache.ComputeKey(canonical); // 64-char lowercase SHA-256 hex
```

### Changed-card count (excluding printing swaps)
```csharp
// Source: DiffEngine.cs:27-102 ; DeckDiff fields ToAdd/CountMismatch/OnlyInArchidekt/PrintingConflicts
var diff  = new DiffEngine(MatchMode.Loose).Compare(savedEntries, currentEntries);
var count = diff.ToAdd.Count + diff.CountMismatch.Count + diff.OnlyInArchidekt.Count; // printing conflicts excluded
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Primer regenerated silently for current deck every post | Stale flag surfaces deck drift without rebuilding | This phase | User keeps the old artifact, regenerates explicitly |

**Deprecated/outdated:** none relevant.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `--gold-warning` is defined in all 22 themes (only a sample confirmed); the `var(--gold-warning,#c8a040)` fallback covers any gaps | Don't Hand-Roll / UI-SPEC §3 | Low — fallback hex renders correct caution color even if a theme lacks the token; planner should still verify token coverage during visual verify |
| A2 | The intended primary trigger is in-session (hidden hash field), since the zip-resume path does not currently restore deck/primer | Open Q2 | Medium — if the product owner insists on zip-resume staleness, scope grows to include restore-path extension |
| A3 | Flag name `tool.primer.stale-flag` (vs `analysis.primer.stale-flag`) — either namespace is consistent with existing keys | Open Q6 | Low — naming only; pick during plan/discuss |

## Open Questions (RESOLVED)

Both questions are resolved by the operator's locked phase scope (recorded during
plan revision, 2026-06-29). The locked answers are binding on the plans below.

1. **Scope of staleness trigger (in-session vs. zip-resume) — RESOLVED: in-session plumbing + zip-resume activation.**
   - **Decision:** Scope is BOTH the in-session hidden-field round-trip (mechanism
     plumbing only, mirrors phase-77 `ScoreJson`) AND zip-resume as the real
     activation path. The download `.zip` persists the generation-time deck-only
     hash (`02-primer-deck-hash.txt`) and the generation deck snapshot
     (`10-primer-deck-list.txt`); the upload/resume render restores them and
     compares the restored hash against the deck currently held in Step 1 of the
     page form (a separately-imported v2 deck).
   - **Rationale:** Every live render path that rebuilds (`BuildAsync`) produces a
     fresh hash by construction (generate re-builds for the current deck; upload
     today re-builds for the restored deck), so a pure in-session render can never
     surface the banner. Zip-resume is the only flow where an OLD primer and a
     CHANGED current deck legitimately coexist in one render. The in-session
     hidden field is retained as plumbing (re-arms the hash on generate) but is
     honestly NOT the live trigger.

2. **Does a natural in-session "render-without-regenerate" trigger exist? — RESOLVED: No, and that is acceptable.**
   - **Decision:** There is NO pure in-session render-without-regenerate gesture on
     this page (both submit buttons either regenerate = always fresh, or download =
     returns a file with no render). We are NOT adding a new affordance, no
     client-side hashing, and no new fetch. The concrete activation is
     **resume-without-rebuild**: the upload/resume action does NOT call
     `BuildAsync`; it renders the restored primer text verbatim from the zip and
     computes staleness from the restored generation snapshot vs the current Step 1
     deck — with NO upstream re-fetch (PRIMER-03). When the staleness flag is OFF,
     the upload path keeps today's exact behavior (byte-identical, page and zip).

## Environment Availability

This phase is code/config + CSS + tests only — no external runtime dependencies (no new services, no upstream calls). Step 2.6 audit: **SKIPPED (no external dependencies introduced).** Build/test runs on the existing .NET 10 SDK + xUnit already used by the repo.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none (SDK-style; `DeckFlow.Web.Tests.csproj`, `DeckFlow.Core.Tests.csproj`) |
| Quick run command | `dotnet build` clean is the primary gate in WSL (VSTest unreliable per CLAUDE.md); targeted test run via Windows `dotnet.exe` |
| Full suite command | push-and-watch CI, or `dotnet.exe test` from Windows |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PRIMER-02 | Reorder ⇒ same hash (fresh) | unit | `dotnet test DeckFlow.Web.Tests` (new test) | ❌ Wave 0 |
| PRIMER-02 | Printing-swap (set/collector change) ⇒ same hash (fresh) | unit | same | ❌ Wave 0 |
| PRIMER-02 | Add card ⇒ different hash (stale) | unit | same | ❌ Wave 0 |
| PRIMER-02 | Remove card ⇒ different hash (stale) | unit | same | ❌ Wave 0 |
| PRIMER-02 | Quantity change ⇒ different hash (stale) | unit | same | ❌ Wave 0 |
| PRIMER-04 | Changed-card count = add+remove+qty, printing excluded | unit | `dotnet test DeckFlow.Core.Tests` or Web.Tests | ❌ Wave 0 |
| PRIMER-01/03 | Flag OFF ⇒ no banner, no hidden field (byte-identical); flag ON + hash mismatch ⇒ `IsStale=true` | unit (controller/VM) | `dotnet test DeckFlow.Web.Tests` | ❌ Wave 0 |
| Flag seed | `tool.primer.stale-flag` seeded OFF | unit | extend `FeatureFlagStoreSeedTests` `[InlineData]` | ⚠ extend `FeatureFlagStoreSeedTests.cs:40-44` |
| Flag catalog | new key has a description | unit | `FeatureFlagCatalogTests` (auto-guards) | ✅ exists |

### Sampling Rate
- **Per task commit:** `dotnet build` clean (WSL) + targeted new tests.
- **Per wave merge:** full suite (CI / Windows `dotnet.exe test`).
- **Phase gate:** full suite green + live UI visual verify (desktop ~1280 + mobile ~390 across site/azorius/nyx) before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] New golden-test file for multiset hash equivalence (model on `DeckPrimerPacketServiceTests.cs:158-224` equality/inequality pattern, using `CreateRequest`/`CreateService` helpers already in that file).
- [ ] Changed-card-count test(s) (Loose-mode `DiffEngine`, printing-swap excluded — model on `DiffEngineTests.cs:14-58`).
- [ ] Controller/view-model test: flag OFF byte-identical (no `IsStale`, no hidden field) vs flag ON stale.
- [ ] Extend `FeatureFlagStoreSeedTests.cs` with `[InlineData("tool.primer.stale-flag", false)]`.
- [ ] Per memory `feedback_web_page_change_tests_themes_mobile`: a changed page needs xUnit + Playwright e2e at desktop + mobile across themes.

## Security Domain

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | yes | The hashed input is the user's deck text; it is canonicalized and SHA-256'd, never reflected/executed. The hidden `GeneratedPrimerHash` field is attacker-controllable (it round-trips through the browser) — treat it as untrusted: it only drives a boolean compare + a non-negative count display, so a tampered value at worst shows/hides a benign banner. Do not use it to fetch, rebuild, or index anything. |
| V6 Cryptography | yes (non-secret) | SHA-256 via BCL `PacketSessionCache.ComputeKey`; this is a content fingerprint, not a security primitive — no key, no secret. |
| V2/V3/V4 Auth/Session/Access | no | Public tool, no auth surface added. The existing `[ValidateAntiForgeryToken]` on the POST actions (`DeckPrimerController.cs:61,119,187`) already protects the form. |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Tampered hidden hash field | Tampering | Compare-only usage; never drive I/O or array indexing from it; count must be clamped non-negative and only computed from server-loaded decks |
| Microcopy injection via changed-count | Injection | Render the integer count through Razor encoding (default); never interpolate deck/card names into the banner |
| CSRF on regenerate | Tampering | Existing `[ValidateAntiForgeryToken]` + `@Html.AntiForgeryToken()` (`DeckPrimer.cshtml:87`) already cover the form |

## Sources

### Primary (HIGH confidence — cycle13 worktree source)
- `DeckFlow.Web/Services/DeckPrimerPacketService.cs:148-213` (TryComputeCacheKeyAsync), `:204-212,739-745` (PrimerCacheInputs), `:719-736` (BuildCanonicalDeckSourceText)
- `DeckFlow.Web/Services/PacketSessionCache.cs:52-59` (ComputeKey)
- `DeckFlow.Core/Diffing/DiffEngine.cs:27-102,173-192` (Compare, Loose keys, printing-conflict detection)
- `DeckFlow.Web/Controllers/DeckPrimerController.cs:42-284` (three view-rendering actions, exception handling)
- `DeckFlow.Web/Views/Deck/DeckPrimer.cshtml:143-333` (Step 2/3 panels, submit buttons, scripts)
- `DeckFlow.Web/Models/DeckPrimerViewModel.cs`, `DeckPrimerRequest.cs:86-115`
- `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs:220-251` (BuildPrimerZip), `:578-664` (LoadPrimerFromZip — restores options only)
- `DeckFlow.Web/Models/DeckAnalysisRequest.cs:135-144` + `Views/Deck/DeckAnalysis.cshtml:516-521` (ScoreJson round-trip precedent)
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs:401-403` (explicit-snapshot flag read), `:120-128` (flag-key conventions)
- `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagCache.cs:13-20` (IsEnabled default-on), `FeatureFlagCatalog.cs:14-80`, `FeatureFlagStore.cs:198-266`
- `DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs:158-224` (cache-key equality/inequality golden-test pattern), `FeatureFlagStoreSeedTests.cs:40-44`
- CSS: `.deck-restored-notice` confirmed only in `wwwroot/css/site-common.css`; `--gold-warning` token present in theme files; `warning-banner` forked per-theme

### Secondary / Tertiary
None — no web sources needed; all claims are codebase-grounded.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — reuses existing in-solution BCL + project code, no new packages.
- Architecture / reuse points: HIGH — every primitive located with file:line.
- Open Q2 (zip-resume): HIGH on the *finding* (restore path verified), MEDIUM on *scope decision* (product call).
- Pitfalls: HIGH — derived directly from sibling-phase patterns.

**Research date:** 2026-06-29
**Valid until:** ~2026-07-29 (stable; codebase-internal, no fast-moving external deps)
