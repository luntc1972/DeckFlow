# Phase 54: Feature Debt - Research

**Researched:** 2026-06-17
**Domain:** Gemini paste-limit verification (FEAT-01) + Commander Spellbook ranking fields (FEAT-02)
**Confidence:** HIGH — all claims verified against live source code and live API

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**FEAT-01 — Gemini unblock:**
- Verify, keep flag-gated default-off. Confirm end-to-end and that Gemini artifacts paste within Gemini's limit when flipped. Leave `DECKFLOW_GEMINI_ENABLED` default `false`. Operator flips in prod.
- Verification must cover all four workflows: deck analysis, deck comparison, cEDH meta-gap, Deck Primer.
- "Within Gemini's limit" measured against Gemini's actual current paste/input ceiling vs real generated artifact size. If an artifact exceeds the limit, record the finding; do NOT silently ship a truncating path. Trimming the Gemini packet is explicitly NOT chosen for this cycle.

**FEAT-02 — Combo ranking:**
- Add `manaValueNeeded`, `popularity`, and `uses` (already partially parsed) to the `SpellbookCombo` record. Preserve existing `{ get; init; }` / record-positional conventions and STJ deserialization safety.
- Ranking order: popularity DESC, then manaValueNeeded ASC as tiebreak.
- Apply ranking at the stub in `DeckPrimerPacketService.BuildComboReferenceText` (line ~420).
- Backward compatibility: new fields are additive; absent JSON properties must degrade gracefully.

### Claude's Discretion
- Exact null/default handling for absent ranking fields; secondary tiebreak when both fields equal/absent (stable order acceptable).
- Test placement: regression tests for new parse fields + ranking go in `DeckFlow.Web.Tests`, matching the `CommanderSpellbookServiceTests` pattern.

### Deferred Ideas (OUT OF SCOPE)
- Trimming/shrinking the Gemini packet to fit the paste limit.
- Enabling Gemini by default in prod.
- KB-12 (codex distill backend).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FEAT-01 | Gemini paste-limit path unblocked and verified across four workflows | §FEAT-01 sections below — flag wiring, per-workflow artifact paths, size cap constants, acceptance criteria |
| FEAT-02 | SpellbookCombo ranking fields captured + combo priority-rank in Deck Primer | §FEAT-02 sections below — live API shape, record surgery, ranking stub location |
</phase_requirements>

---

## Summary

Phase 54 is two narrow, independent tasks on existing code. Neither task changes public-facing behavior beyond enabling Gemini when the operator flips the flag.

**FEAT-01 (Gemini verify):** The Gemini infrastructure is fully wired and has been since Phase 15. The flag `DECKFLOW_GEMINI_ENABLED` (default `false`) gates the UI radio in `_AiSelector.cshtml` and the platform fan-out in `DeckPrimerPacketService.GetEnabledPlatforms`. Analysis, comparison, and meta-gap are single-platform (user-selected), so Gemini works the same day the flag is on — no code change is needed for those three. The Primer workflow emits all three platform prompts simultaneously and has a 32,000-char cap with section-dropping already implemented in `GeminiPrimerPromptVariant`. The cap is backed by an existing test (`PrimerPromptVariantTests.Gemini_OverCap_TrimsWithDisclosure`). **FEAT-01 is purely a verification task** — measure actual sizes of each Gemini artifact, compare against the documented ~30,000–32,768-char paste warning, and surface any overage. No code changes expected; if sizes fit, the only deliverable is a written verification record.

**FEAT-02 (combo ranking):** The `SpellbookCombo` record (:16) is a positional sealed record with three fields today. The API returns `manaValueNeeded` (int, top-level on variant) and `popularity` (int, top-level on variant) — confirmed via live API call. The `uses` field is already parsed (card names). Adding two int fields to the positional record is additive; existing construction sites pass positional arguments so they will require updating at compile time, which is the intended safety net. The ranking stub at `DeckPrimerPacketService.BuildComboReferenceText:423` currently uses immediacy-rank + card count + API index; the upgrade replaces `.ThenBy(item => item.Combo.CardNames.Count)` with `.OrderByDescending(item => item.Combo.Popularity).ThenBy(item => item.Combo.ManaValueNeeded)` with null/zero graceful fallback. Test coverage already exists for the ranking branch (`RankingBranch_FallbackEmitsApiOrderInstruction`).

**Primary recommendation:** Plan FEAT-01 as a single verification task (no code changes unless an artifact is oversized), and FEAT-02 as three changes in sequence: (1) record field addition + parse, (2) ranking substitution, (3) regression tests.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Gemini flag parse | API / Backend | — | `Program.cs:78-82` reads env var, binds `AiPlatformOptions` |
| Gemini UI visibility | Frontend Server (SSR) | — | `_AiSelector.cshtml:13,25` injects `IOptions<AiPlatformOptions>` and skips the radio |
| Gemini artifact size gate (Primer) | API / Backend | — | `GeminiPrimerPromptVariant.AppendIfFits` at service layer |
| Gemini artifact size (Analysis/Comparison/MetaGap) | API / Backend | — | No cap enforced — single-platform output routes through the Gemini variant only when user selects it |
| Combo ranking | API / Backend | — | `DeckPrimerPacketService.BuildComboReferenceText:416-429` |
| Combo field parse | API / Backend | — | `CommanderSpellbookService.ParseVariants:180-199` |

---

## FEAT-01: Gemini Unblock — Detailed Findings

### Gemini flag wiring (complete map)

Every code path that checks `GeminiEnabled`: [VERIFIED: source code]

| File | Line | What it does |
|------|------|--------------|
| `Program.cs` | 78-82 | Reads `DECKFLOW_GEMINI_ENABLED` env var, binds `AiPlatformOptions.GeminiEnabled`; default `false` |
| `DeckPrimerPacketService.cs` | 512-518 | `GetEnabledPlatforms(bool)` filters `AiPlatform.All` to exclude Gemini when flag is false |
| `DeckPrimerPacketService.cs` | 131,139 | Internal test ctor accepts `bool geminiEnabled = false` |
| `DeckPrimerPacketService.cs` | 208 | `TryComputeCacheKeyAsync` passes `GeminiEnabled` into `PrimerCacheInputs` so cache key includes Gemini state |
| `DeckPrimerPacketService.cs` | 750 | `PrimerCacheInputs` record carries `GeminiEnabled` field |
| `_AiSelector.cshtml` | 13,25 | Skips rendering the Gemini radio button when `GeminiEnabled = false` |

Note: AiPlatformOptions comment (`AiPlatformOptions.cs:16`) explicitly states "Server-side prompt builders still accept 'Gemini' if posted directly (UI-hide only, per resume decision 2026-05-13)." This means a raw POST with `TargetAiPlatform=Gemini` will still route to `GeminiAnalysisPromptVariant` even when the flag is off — the flag is a UI hide only, not a server-side block. **This is existing behavior, not a new risk.**

### Four Gemini artifact workflows

**Analysis workflow** (`DeckAnalysisPacketService.cs:963-965`): Single-platform — builds one prompt via `_analysisPromptRegistry.Build(AiPlatform.Normalize(request.TargetAiPlatform), ...)`. The `GeminiAnalysisPromptVariant` has `DefensivePromptCharCap = 50,000` chars but no section-dropping — it writes the whole prompt unconditionally. Artifact filename: `30-primer-chatgpt-prompt.txt` (named for the platform key, set dynamically). [VERIFIED: source code]

**Comparison workflow** (`DeckComparisonService.cs:714-715`): Same single-platform pattern. `GeminiComparisonPromptVariant` has no cap constant visible in the header — no trimming. [VERIFIED: source code]

**cEDH meta-gap workflow** (`MetaGapService.cs:547-548`): Same single-platform pattern. `GeminiMetaGapPromptVariant` has no cap. [VERIFIED: source code]

**Deck Primer workflow** (`DeckPrimerPacketService.cs:318-332`): Multi-platform — fans out to all enabled platforms. `GeminiPrimerPromptVariant` has `DefensivePromptCharCap = 32,000` chars and the `AppendIfFits` section-dropping logic. [VERIFIED: source code, `GeminiPrimerPromptVariant.cs:12-13,90-103`]

### Gemini paste limit — the concrete figure

The Gemini web interface (gemini.google.com) enforces a "message too long" warning/block at approximately **30,000–32,768 characters** per paste. [CITED: community reports at support.google.com/gemini/thread/312836444; text-splitter.com/blog/gemini-prompt-splitter-guide]

The code already treats **32,000 chars** (Primer) and **50,000 chars** (Analysis) as its internal caps. The 32,000 figure in `GeminiPrimerPromptVariant` aligns with the live Gemini warning threshold. The 50,000 figure in `GeminiAnalysisPromptVariant` is **above** the Gemini paste warning — this is a pre-existing concern the codebase has not resolved.

**Per-variant cap constants:** [VERIFIED: source code]

| Variant | File | DefensivePromptCharCap | Has section-drop? |
|---------|------|----------------------|-------------------|
| `GeminiAnalysisPromptVariant` | `Analysis/GeminiAnalysisPromptVariant.cs:17` | 50,000 | No |
| `GeminiComparisonPromptVariant` | `Comparison/GeminiComparisonPromptVariant.cs` | Not set | No |
| `GeminiMetaGapPromptVariant` | `MetaGap/GeminiMetaGapPromptVariant.cs` | Not set | No |
| `GeminiPrimerPromptVariant` | `Primer/GeminiPrimerPromptVariant.cs:13` | 32,000 | Yes — `AppendIfFits` |

### What "verified + unblocked" means given default-off

Since the flag stays false, "unblocked + verified" means:

1. Spin up the app with `DECKFLOW_GEMINI_ENABLED=true`.
2. Exercise each of the four Gemini workflows with a representative deck (real URL or pasted export).
3. Measure `prompt.Length` (C# `string.Length` = char count, not byte count; UTF-8 chars > 127 count as 1 for STR length) for each generated Gemini artifact.
4. Compare against the 30,000-char Gemini paste warning.
5. Record each artifact's size in a `VERIFICATION.md` table.
6. If all four are ≤ 30,000 chars: close FEAT-01 as verified.
7. If any exceed 30,000 chars: record as a finding with the artifact size — do NOT implement trimming, per CONTEXT.md.

**Minimal repeatable verification artifact:** A table in `54-VERIFICATION.md` with one row per workflow: workflow name, artifact filename, char count, pass/fail vs. 30,000-char threshold.

The existing `PrimerPromptVariantTests.Gemini_OverCap_TrimsWithDisclosure` test (`PrimerPromptVariantTests.cs:108-121`) asserts `prompt.Length <= 32000` with a synthetic oversized deck. That is the unit-test coverage; the verification task adds a real-deck measurement that runs the full pipeline.

### Testing approach for FEAT-01

VSTest in WSL is unreliable (CLAUDE.md note). Prefer:
1. `dotnet build` clean as gate.
2. Run the app with `DECKFLOW_GEMINI_ENABLED=true` (existing `scripts/run-web.ps1` or `run-web.sh`).
3. Paste a real 99-card cEDH deck URL (Moxfield/Archidekt), select Gemini, run all four workflows, and note prompt char counts from the generated zip artifacts.

A new xUnit test in `DeckFlow.Web.Tests` that instantiates each real Gemini variant (like `PrimerPromptVariantTests`) with a representative 100-card decklist and asserts `prompt.Length <= 30000` is feasible and repeatable without VSTest reliability issues (these are pure synchronous CPU tests with no HTTP or DB). This is the recommended approach for CI-stable coverage.

---

## FEAT-02: Combo Ranking Fields — Detailed Findings

### Live API response shape — confirmed field names

Live call to `https://backend.commanderspellbook.com/find-my-combos` confirmed: [VERIFIED: live API call 2026-06-17]

Top-level scalar fields on a variant object:

```json
{
  "id": "4821-5261",
  "status": "OK",
  "spoiler": false,
  "identity": "U",
  "popularity": 101243,
  "manaValueNeeded": 0,
  "manaNeeded": "",
  "description": "...",
  "notes": "",
  "variantCount": 1,
  "bracketTag": "S",
  "easyPrerequisites": "",
  "notablePrerequisites": "...",
  "uses": [...],
  "produces": [...],
  "requires": [...],
  "includes": [...],
  "prices": {...},
  "legalities": {...},
  "of": [...]
}
```

**Key confirmed facts:**
- `popularity` — integer, present at variant top level, not nested. Value: 101243 in the test combo (Isochron Scepter + Dramatic Reversal). **Not inside `uses`** — the code comment in CONTEXT.md saying "already partially parsed" refers to the existing `ExtractCardNames` parsing the `uses` array for card names, not for `popularity`.
- `manaValueNeeded` — integer, present at variant top level, not nested. Value: 0 for zero-mana-cost combos.
- Both fields will be `null`-equivalent in STJ if absent from a response (tolerant parse required, matching existing pattern).
- `uses` array shape: `[{"card": {"name": "..."}, "quantity": 1, "zoneLocations": [...], ...}]` — name is at `uses[i].card.name`, which is exactly what `ExtractCardNames` already reads.

### SpellbookCombo record — current shape and surgery plan

Current record (`CommanderSpellbookService.cs:16`): [VERIFIED: source code]

```csharp
public sealed record SpellbookCombo(
    IReadOnlyList<string> CardNames,
    IReadOnlyList<string> Results,
    string Instructions);
```

**Additive field addition pattern:** Add `int? Popularity` and `int? ManaValueNeeded` as optional positional parameters with defaults (or as `{ get; init; }` properties). Given the STJ carve-out (CLAUDE.md: never convert `{ get; init; }` to get-only), the safest approach is to add `int? Popularity = null` and `int? ManaValueNeeded = null` as the last two positional parameters with defaults so existing construction sites compile without change.

**Construction site inventory** — every `new SpellbookCombo(...)` call: [VERIFIED: source code grep]

| Location | Line(s) | Notes |
|----------|---------|-------|
| `CommanderSpellbookService.ParseVariants` | :198 | Main parse path — add populate from variant JSON |
| `CommanderSpellbookServiceTests.cs` | :46,88,119,161,187,199 etc. | Existing tests pass 3 positional args — additive default params keep them compiling |
| `DeckPrimerPacketServiceTests.cs` | :30,36 | Same — 3-arg construction, defaults handle it |
| `PrimerPromptVariantTests.cs` | :30-38 | Same |

All existing test construction sites pass three positional arguments. Adding `int? Popularity = null, int? ManaValueNeeded = null` as fourth and fifth positional parameters with defaults means **no existing call site breaks** — they compile as `SpellbookCombo(cards, results, instructions, null, null)` implicitly.

**STJ carve-out implication:** The `SpellbookCombo` record is only constructed in code (in `ParseVariants`), not deserialized directly from JSON by STJ. The STJ carve-out applies to types that round-trip through STJ serialization (like `DeckPrimerPacketResult`). `SpellbookCombo` is safe to add nullable positional parameters to. [VERIFIED: CommanderSpellbookService.cs — record is built imperatively from JsonDocument parsing, not via `JsonSerializer.Deserialize<SpellbookCombo>`]

### ParseVariants — exactly where to add the field reads

Current `ParseVariants` (:180-199): [VERIFIED: source code]

```csharp
private static IEnumerable<SpellbookCombo> ParseVariants(JsonElement array)
{
    foreach (var variant in array.EnumerateArray())
    {
        var cards = ExtractCardNames(variant);
        var results = ExtractResults(variant);
        var instructions = ExtractInstructions(variant);

        if (cards.Count == 0 || results.Count == 0) { continue; }

        yield return new SpellbookCombo(cards, results, instructions);  // :198
    }
}
```

Add two reads after `ExtractInstructions`:

```csharp
// Use TryGetInt32 (NOT GetInt32) — GetInt32 throws on a decimal or
// out-of-Int32-range JSON number even when ValueKind==Number. TryGetInt32
// returns false → null, never throwing / never failing the whole result.
int? popularity = variant.TryGetProperty("popularity", out var pop)
    && pop.ValueKind == JsonValueKind.Number
    && pop.TryGetInt32(out var popVal)
    ? popVal
    : null;

int? manaValueNeeded = variant.TryGetProperty("manaValueNeeded", out var mv)
    && mv.ValueKind == JsonValueKind.Number
    && mv.TryGetInt32(out var mvVal)
    ? mvVal
    : null;

yield return new SpellbookCombo(cards, results, instructions, popularity, manaValueNeeded);
```

This matches the existing tolerant pattern used throughout `ExtractCardNames` (null-coalescing, array kind checks). [VERIFIED: source code pattern]

### DeckPrimerPacketService ranking stub — exact replacement

Current stub at `BuildComboReferenceText:416-429`: [VERIFIED: source code]

```csharp
IEnumerable<SpellbookCombo> orderedCombos = combos.IncludedCombos;
if (string.Equals(spikeVerdict, "sufficient", StringComparison.Ordinal))
{
    // Known limitation (31-03 scope fence): SpellbookCombo currently exposes only
    // CardNames/Results/Instructions. Full manaValueNeeded/popularity capture is a
    // follow-up in CommanderSpellbookService, so this branch ranks with the fields
    // available today: produces-immediacy text + piece count + API-order tie-break.
    orderedCombos = combos.IncludedCombos
        .Select((combo, index) => new { Combo = combo, Index = index })
        .OrderBy(item => GetImmediacyRank(item.Combo.Results))
        .ThenBy(item => item.Combo.CardNames.Count)
        .ThenBy(item => item.Index)
        .Select(item => item.Combo);
}
```

After FEAT-02, replace the inner LINQ chain:

```csharp
orderedCombos = combos.IncludedCombos
    .Select((combo, index) => new { Combo = combo, Index = index })
    .OrderByDescending(item => item.Combo.Popularity ?? 0)
    .ThenBy(item => item.Combo.ManaValueNeeded ?? int.MaxValue)
    .ThenBy(item => item.Index)        // stable API-order tiebreak
    .Select(item => item.Combo);
```

Also update the comment to remove the "Known limitation" text and explain the new sort. The `ComboRankingVerdict = "sufficient"` constant (:67) stays unchanged — it continues to select this branch.

**Graceful degradation:** When `Popularity` is null (API absent or future schema change), `?? 0` treats all combos as equally popular, deferring to `ManaValueNeeded` tiebreak. When `ManaValueNeeded` is null, `?? int.MaxValue` puts unknown-cost combos last. When both are null, API-order (stable by `Index`) is preserved — exactly the fallback defined in CONTEXT.md.

### Test patterns for FEAT-02

**CommanderSpellbookServiceTests** (`DeckFlow.Web.Tests/Services/CommanderSpellbookServiceTests.cs`):

Pattern: inject `StubHttpMessageHandler` with literal JSON, call `FindCombosAsync`, assert on result fields. Uses `TestServiceFactory.CreateCommanderSpellbookService(factory, cache)`.

New tests to add (same file, same pattern):
- `ParseVariants_PopularityAndManaValueNeeded_Parsed` — JSON with both fields set, assert `combo.Popularity == X`, `combo.ManaValueNeeded == Y`.
- `ParseVariants_MissingRankingFields_DefaultsToNull` — JSON omitting both fields, assert nulls.

**DeckPrimerPacketServiceTests** (`DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs`):

Pattern: `CreateService(comboResult: ...)` with override delegates, call `BuildAsync`, assert on prompt text. Uses internal test ctor.

New test to add:
- `RankingBranch_PopularityDESC_ManaValueASC` — two combos with swapped popularity, assert the higher-popularity combo appears first in prompt text.

**PrimerPromptVariantTests** (`DeckFlow.Web.Tests/PrimerPromptVariantTests.cs`):

These test the Gemini variant directly. The existing `Gemini_OverCap_TrimsWithDisclosure` test (:108) already passes combos through the ranking path. No ranking-specific assertion needed here — the `DeckPrimerPacketServiceTests` pattern is the right place.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Tolerant JSON field reads | Custom exception-catching | `TryGetProperty` + `ValueKind` check | Already the pattern in `ExtractCardNames`, `ExtractResults`, `ExtractInstructions` |
| LINQ stable sort | Custom sort | `.ThenBy(item => item.Index)` as final tiebreak | Guarantees determinism when popularity and manaValueNeeded are equal |
| Artifact size measurement | New HTTP harness | `prompt.Length` on the string returned by `variant.Build(...)` | Pure CPU test, no HTTP needed — matches `PrimerPromptVariantTests` pattern |

---

## Common Pitfalls

### Pitfall 1: Positional record parameter order breaks construction sites
**What goes wrong:** Adding parameters in the middle of a positional record shifts all downstream construction sites.
**Why it happens:** Positional records use position, not name, for construction.
**How to avoid:** Add new parameters at the END with default values (`= null`). All existing `new SpellbookCombo(cards, results, instructions)` calls continue to compile.
**Warning signs:** Any CS7036 or CS1729 error after the record change.

### Pitfall 2: Treating `popularity` as nested inside `uses`
**What goes wrong:** Reading `uses[i].popularity` or similar — the field isn't there.
**Why it happens:** The CONTEXT.md note "uses (already partially parsed)" refers to card-name extraction from `uses`, not that `popularity` is inside `uses`.
**How to avoid:** Read `popularity` and `manaValueNeeded` directly from the `variant` element, not from any nested array. Confirmed by live API call — both are top-level scalar fields.

### Pitfall 3: Measuring bytes instead of chars for the Gemini limit
**What goes wrong:** Reporting UTF-8 byte counts instead of C# string char counts.
**Why it happens:** `Encoding.UTF8.GetByteCount(prompt)` > `prompt.Length` for non-ASCII text (card names with accented characters, e.g. "Jhoira of the Ghitu").
**How to avoid:** Use `prompt.Length` (char count) as the measure — this matches what `GeminiPrimerPromptVariant.AppendIfFits` uses, and Gemini's paste limit is specified in characters/tokens, not bytes.

### Pitfall 4: Format-gate CI failing on new code
**What goes wrong:** `format-gate` CI job fails if new/changed lines don't satisfy `.editorconfig`.
**Why it happens:** The pre-commit hook and CI run `dotnet format --verify-no-changes` on changed lines only.
**How to avoid:** Never convert `{ get; init; }` to get-only (CarveOut #1); preserve switch expressions; use LF line endings. Run `git config core.hooksPath .githooks` locally before committing.

### Pitfall 5: Gemini analysis 50,000-char cap exceeds paste limit
**What goes wrong:** The `GeminiAnalysisPromptVariant` cap is 50,000 chars — above Gemini's ~30,000-char paste warning. A full analysis prompt may be generated and stored in the zip artifact, but when the user tries to paste it into Gemini, it fails.
**Why it happens:** The 50,000 cap was set defensively high (phase 15 comment: "byte-for-byte copy"). The artifact is generated without section-dropping.
**How to avoid (for this phase):** The verification task explicitly measures the analysis prompt for a representative deck. If it exceeds 30,000 chars, surface it as a finding per CONTEXT.md — do NOT add trimming in this phase.
**Warning signs:** `prompt.Length > 30000` in the verification measurement.

---

## Code Examples

### Tolerant top-level int? read from JsonElement (matches existing pattern)
```csharp
// Source: CommanderSpellbookService.cs:235-246 (ExtractCardNames pattern)
// TryGetInt32 (NOT GetInt32) — no throw on decimal/out-of-range.
int? popularity = variant.TryGetProperty("popularity", out var pop)
    && pop.ValueKind == JsonValueKind.Number
    && pop.TryGetInt32(out var popVal)
    ? popVal
    : null;
```

### Ranking LINQ chain (replacement for stub at DeckPrimerPacketService.cs:423-428)
```csharp
orderedCombos = combos.IncludedCombos
    .Select((combo, index) => new { Combo = combo, Index = index })
    .OrderByDescending(item => item.Combo.Popularity ?? 0)
    .ThenBy(item => item.Combo.ManaValueNeeded ?? int.MaxValue)
    .ThenBy(item => item.Index)
    .Select(item => item.Combo);
```

### Additive positional record extension
```csharp
// Source: CommanderSpellbookService.cs:16-19 (current)
public sealed record SpellbookCombo(
    IReadOnlyList<string> CardNames,
    IReadOnlyList<string> Results,
    string Instructions,
    int? Popularity = null,       // additive — no existing site breaks
    int? ManaValueNeeded = null); // additive — no existing site breaks
```

### Test pattern for new parse fields (CommanderSpellbookServiceTests.cs pattern)
```csharp
// Source: CommanderSpellbookServiceTests.cs:43-75 (existing pattern)
const string json = """
{
  "results": {
    "included": [{
      "uses": [{"card": {"name": "Card A"}}, {"card": {"name": "Card B"}}],
      "produces": [{"feature": {"name": "Win the game"}}],
      "description": "Instructions.",
      "popularity": 5000,
      "manaValueNeeded": 3
    }],
    "almostIncluded": []
  }
}
""";
// Assert: result.IncludedCombos[0].Popularity == 5000
//         result.IncludedCombos[0].ManaValueNeeded == 3
```

---

## Standard Stack

No new packages. All work uses existing project infrastructure. [VERIFIED: source code — no new dependency needed]

| Component | Version | Role |
|-----------|---------|------|
| `System.Text.Json` (built-in) | .NET 10 | JSON parsing in `ParseVariants` |
| xUnit 2.9.3 | existing | Test assertions |
| `DeckFlow.Web.Tests` test project | existing | Target for regression tests |

---

## Package Legitimacy Audit

> Not applicable — no new packages are introduced in this phase.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `DECKFLOW_GEMINI_ENABLED=true` env var | FEAT-01 verification | Set at run time | N/A | None — required for verification |
| Commander Spellbook API | FEAT-01 + FEAT-02 live check | ✓ (verified live) | — | Tests use `StubHttpMessageHandler` |
| `dotnet` SDK | Build + test | ✓ | .NET 10 | — |

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |
| Quick run command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/ --filter "FullyQualifiedName~SpellbookCombo\|FullyQualifiedName~PrimerPrompt"` |
| Full suite command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| FEAT-01 | Gemini primer prompt stays ≤ 32,000 chars with full sections | unit | `dotnet test --filter "PrimerPromptVariantTests"` | ✅ `PrimerPromptVariantTests.cs:108` |
| FEAT-01 | Gemini disabled → 2 platforms; enabled → 3 platforms | unit | `dotnet test --filter "DeckPrimerPacketServiceTests"` | ✅ `:139,148` |
| FEAT-01 | Gemini analysis/comparison/metagap size ≤ 30,000 chars (real deck) | unit | New test in `PrimerPromptVariantTests` or `GeminiVariantSizeTests` | ❌ Wave 0 |
| FEAT-02 | `ParseVariants` captures `popularity` and `manaValueNeeded` | unit | `dotnet test --filter "CommanderSpellbookServiceTests"` | ❌ Wave 0 |
| FEAT-02 | Absent ranking fields parse as null | unit | same | ❌ Wave 0 |
| FEAT-02 | Combos ranked popularity DESC, manaValueNeeded ASC | unit | `dotnet test --filter "DeckPrimerPacketServiceTests"` | ❌ Wave 0 |

### Wave 0 Gaps
- [ ] `DeckFlow.Web.Tests/CommanderSpellbookServiceTests.cs` — 2 new facts (parse present fields, parse absent fields)
- [ ] `DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs` — 1 new test (ranking order assertion)
- [ ] `DeckFlow.Web.Tests/PrimerPromptVariantTests.cs` or new file — Gemini analysis/comparison/metagap size assertions (representative deck, not oversized synthetic)

---

## Security Domain

> No new attack surface introduced. FEAT-01 enables a hidden UI element (Gemini radio) — the server has always accepted Gemini as a platform target even when the flag is off (documented at `AiPlatformOptions.cs:16`). FEAT-02 adds two integer fields to a record parsed from a cached upstream API response — no user-controlled input path.

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | No new surface | `manaValueNeeded`/`popularity` come from upstream API, not user input; existing tolerant parse |
| V6 Cryptography | No | — |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Gemini web UI paste warning triggers at ~30,000 chars | FEAT-01 limit section | If actual limit is lower (e.g., 25,000), even the primer with section-dropping may exceed it; if higher, verification pass threshold is conservative (safe side) |
| A2 | `popularity` and `manaValueNeeded` are always integers when present (not floats) | FEAT-02 parse | If API returns them as floats, `GetInt32()` throws; use `GetDouble()` + cast, or `TryGetInt32` |

**If A2 is wrong:** The live API call returned `"popularity": 101243` and `"manaValueNeeded": 0` as bare JSON integers (no decimal point). Confirmed integer type. Risk is low.

---

## Open Questions (RESOLVED)

1. **Gemini analysis prompt size for a real 100-card cEDH deck** — **RESOLVED: size unknown until measured; the measurement IS the FEAT-01 deliverable (Plan 54-02 Task 1) and any overage is the recorded finding (54-VERIFICATION.md), trimming deferred per CONTEXT.md.**
   - What we know: `GeminiAnalysisPromptVariant.DefensivePromptCharCap = 50,000` — above the paste warning.
   - What's unclear (at research time): the real-world size of the analysis prompt for a max-input deck. Phase 31 spike noted "full-31 max primer ~30.9K chars"; the analysis prompt includes a full reference document and deck profile schema the primer does not, so it is likely the over-limit case.
   - Resolution: This is deliberately a verification output, not a research-blocking unknown. Plan 54-02 Task 1 measures it via the size-measurement test; Task 2 records the verdict. No planning ambiguity remains.

2. **AlmostIncluded variants — should `popularity` and `manaValueNeeded` be parsed there too?** — **RESOLVED: Deferred/out of scope. Ranking applies to `IncludedCombos` only per CONTEXT.md; `SpellbookAlmostCombo` is unchanged.**
   - What we know: `SpellbookAlmostCombo` is a separate record (:24-28) with no ranking fields. CONTEXT.md scope is Deck Primer combo ranking (`IncludedCombos` only).
   - Resolution: `AlmostIncluded` combos are rendered in a fixed block, not ranked. No change to `SpellbookAlmostCombo` is needed. Plan 54-01 touches only `SpellbookCombo` + the IncludedCombos ranking.

---

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` — record definition, ParseVariants, ExtractCardNames (all verified in this session)
- `DeckFlow.Web/Services/DeckPrimerPacketService.cs` — ComboRankingVerdict, ranking stub, GetEnabledPlatforms, Gemini threading
- `DeckFlow.Web/Services/PromptBuilders/Primer/GeminiPrimerPromptVariant.cs` — 32,000 cap, AppendIfFits
- `DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs` — 50,000 cap, no section-dropping
- `DeckFlow.Web.Tests/PrimerPromptVariantTests.cs` — existing size assertion tests
- `DeckFlow.Web.Tests/Services/CommanderSpellbookServiceTests.cs` — existing test patterns
- `DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs` — existing ranking branch tests + Gemini enable/disable tests
- `DeckFlow.Web/Program.cs:78-82, 293-316` — flag bind, all six Gemini variant DI registrations
- `DeckFlow.Web/Views/Shared/_AiSelector.cshtml:13,25` — UI gating
- Live `POST https://backend.commanderspellbook.com/find-my-combos` API call (2026-06-17) — `popularity: 101243`, `manaValueNeeded: 0` confirmed as top-level integer fields

### Secondary (MEDIUM confidence)
- [Gemini Apps Community — Chat Input Character Limit](https://support.google.com/gemini/thread/312836444) — ~30,000 char paste warning
- [Text Splitter Blog — Gemini Prompt Splitter](https://www.text-splitter.com/blog/gemini-prompt-splitter-guide) — corroborates ~30,000 char limit

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; existing service patterns confirmed
- Architecture (Gemini wiring): HIGH — traced all code paths, verified DI registrations
- Architecture (combo ranking): HIGH — live API call confirmed field names and types
- Pitfalls: HIGH — identified from direct code inspection
- Gemini paste limit: MEDIUM — community-sourced; the 32,000-char internal cap in the codebase already aligns with this

**Research date:** 2026-06-17
**Valid until:** 2026-07-17 (Commander Spellbook API shape is versioned; `popularity`/`manaValueNeeded` confirmed live)
