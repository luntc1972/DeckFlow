# Manabase Increment 2 — Per-Commander Community Baseline Implementation Plan

> **For agentic workers:** Execute task-by-task; each task is an independent commit. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Per-commander land baseline from EDHREC's sanctioned `averages.tgz` dump, blended with the Increment 1 bracket baseline via the shipped `ManabaseBaselineWeighting`, displayed with "Data from EDHREC" attribution.

**Architecture:** Offline CLI generator converts the dump CSV into a `commanders` array added to the existing bundled `Data/manabase-baseline/latest.json` (bracket rows untouched). The provider gains a commander lookup keyed by a shared normalization helper. `ManabaseAnalysisService.BuildCommunityBaseline` feeds the commander cell into `ManabaseBaselineWeighting.Compute` for brackets 2–3 only, and is suppressed when the cEDH meta range renders.

**Tech Stack:** .NET 10, xUnit, System.Text.Json, System.CommandLine (existing CLI host). No new packages.

**Spec:** `.planning/specs/2026-07-17-manabase-commander-baseline-inc2-design.md` (Codex-review CONVERGED). Read it before starting.

## Global Constraints

- Branch `feat/edhrec-bracket-land-target`, worktree `/mnt/c/users/chrislunt/source/personal/deckflow-cmdengine`. Baseline commit `69505cca`.
- Preserve each touched file's existing line endings exactly (repo is LF-enforced via `.gitattributes`; do NOT emit CRLF).
- Never auto-convert `{ get; init; }` to `{ get; }`; never inline `[Attribute]` onto property lines; never re-indent raw-string literals; preserve switch expressions; changed-lines format gate applies.
- Flag `analysis.manabase.baseline` (existing) gates everything; flag OFF must remain byte-identical output.
- No new NuGet/npm packages. Never commit compiled `wwwroot/js/*.js`. Layout CSS goes in `site-common.css`, never `site.css`.
- Commit per task, Conventional Commits, no Co-Authored-By trailer.
- Build via `dotnet build DeckFlow.sln` (WSL resolves `dotnet` per scripts; use the path that works: `dotnet` or `/mnt/c/Program Files/dotnet/dotnet.exe`).

---

### Task 1: Shared commander key helper (Core)

**Files:**
- Create: `DeckFlow.Core/Manabase/ManabaseCommanderKey.cs`
- Test: `DeckFlow.Core.Tests/Manabase/ManabaseCommanderKeyTests.cs`

**Interfaces:**
- Produces: `public static string ManabaseCommanderKey.Create(string name, string? partnerName = null)` — consumed by Task 2 (generator) and Task 4 (provider).

- [ ] **Step 1: Write failing tests**

```csharp
namespace DeckFlow.Core.Tests;

using DeckFlow.Core.Manabase;

public sealed class ManabaseCommanderKeyTests
{
    [Fact]
    public void Create_LoneCommander_NormalizesCaseAndPunctuation()
        => Assert.Equal("y shtola night s blessed", ManabaseCommanderKey.Create("Y'shtola, Night's Blessed"));

    [Fact]
    public void Create_UnicodeApostropheAndAccents_MatchesAsciiForm()
        => Assert.Equal(ManabaseCommanderKey.Create("Y’shtola, Night’s Blessed"), ManabaseCommanderKey.Create("Y'shtola, Night's Blessed"));

    [Fact]
    public void Create_Pair_IsOrderInsensitive()
        => Assert.Equal(
            ManabaseCommanderKey.Create("Halana, Kessig Ranger", "Alena, Kessig Trapper"),
            ManabaseCommanderKey.Create("Alena, Kessig Trapper", "Halana, Kessig Ranger"));

    [Fact]
    public void Create_Pair_UsesDelimiterThatCannotCollideWithLoneNames()
        => Assert.Equal("alena kessig trapper||halana kessig ranger", ManabaseCommanderKey.Create("Halana, Kessig Ranger", "Alena, Kessig Trapper"));

    [Fact]
    public void Create_MdfcName_CollapsesToFrontFace()
        => Assert.Equal("birgi god of storytelling", ManabaseCommanderKey.Create("Birgi, God of Storytelling // Harnfel, Horn of Bounty"));

    [Fact]
    public void Create_PairOfMdfcNames_NormalizesEachBeforeJoining()
        => Assert.Equal(
            ManabaseCommanderKey.Create("Birgi, God of Storytelling // Harnfel, Horn of Bounty", "Esika, God of the Tree // The Prismatic Bridge"),
            ManabaseCommanderKey.Create("Esika, God of the Tree", "Birgi, God of Storytelling"));

    [Fact]
    public void Create_BlankPartner_TreatedAsLone()
        => Assert.Equal(ManabaseCommanderKey.Create("The Ur-Dragon"), ManabaseCommanderKey.Create("The Ur-Dragon", "  "));
}
```

Note on the accents test: `CardNormalizer` keeps `\p{L}` letters, so `é` survives — the Unicode-apostrophe test works because `’` is punctuation (stripped to a space, same as `'`). Do NOT add accent-folding; the dump and Scryfall both use the same accented spellings, so keys match without folding. Add this comment to the test file.

- [ ] **Step 2: Run to verify failure** — `dotnet test DeckFlow.Core.Tests --filter ManabaseCommanderKey` → fails: type not found.

- [ ] **Step 3: Implement**

```csharp
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Builds the canonical lookup key for a commander (or partner pair) shared by the EDHREC
/// averages generator and <c>ManabaseBaselineProvider</c>. Each name is normalized separately
/// with <see cref="CardNormalizer.Normalize"/> BEFORE joining (normalizing a joined pair would
/// truncate at the " / " MDFC separator); pair components are ordinal-sorted so partner order
/// never matters; "||" cannot occur in a normalized name (punctuation is stripped), so pair keys
/// can never collide with lone-commander keys.
/// </summary>
public static class ManabaseCommanderKey
{
    /// <summary>Canonical key for a commander or partner pair. Blank partner = lone commander.</summary>
    public static string Create(string name, string? partnerName = null)
    {
        string first = CardNormalizer.Normalize(name);
        if (string.IsNullOrWhiteSpace(partnerName))
        {
            return first;
        }

        string second = CardNormalizer.Normalize(partnerName);
        return string.CompareOrdinal(first, second) <= 0
            ? $"{first}||{second}"
            : $"{second}||{first}";
    }
}
```

- [ ] **Step 4: Run to verify pass** — same filter → all pass. Fix the first test's expected value against actual `CardNormalizer` output if it differs (punctuation → space → collapse); the assertion must encode real behavior, not guesses.

- [ ] **Step 5: Commit** — `feat(manabase): shared commander lookup key for EDHREC baseline`

---

### Task 2: Snapshot DTO extension + EDHREC averages converter (Core)

**Files:**
- Modify: `DeckFlow.Core/Manabase/ManabaseCommunityBaseline.cs` (snapshot records live here)
- Create: `DeckFlow.Core/Manabase/EdhrecAveragesConverter.cs`
- Test: `DeckFlow.Core.Tests/Manabase/EdhrecAveragesConverterTests.cs`

**Interfaces:**
- Consumes: `ManabaseCommanderKey.Create` (Task 1).
- Produces:
  - `ManabaseBaselineSnapshot` gains `CommandersSource` (`string?`, json `commandersSource`) and `Commanders` (`IReadOnlyList<ManabaseCommanderBaseline>`, json `commanders`, default empty).
  - `public sealed record ManabaseCommanderBaseline` — `Name` (string, json `name`), `PartnerName` (string?, json `partnerName`), `AvgLands` (double, json `avgLands`), `DeckCount` (int, json `deckCount`), all `{ get; init; }` with `required` on Name/AvgLands/DeckCount.
  - `public static class EdhrecAveragesConverter` with `public static EdhrecAveragesResult Convert(string csvText, int minDeckCount = 100)` and `public sealed record EdhrecAveragesResult(IReadOnlyList<ManabaseCommanderBaseline> Commanders, int SkippedMalformed, int DuplicateCollisions)`.

- [ ] **Step 1: Add DTO members** (append to `ManabaseCommunityBaseline.cs`, matching existing style):

```csharp
// In ManabaseBaselineSnapshot, after Brackets:
    /// <summary>Provenance label for the commanders block (e.g. "edhrec-averages"). Absent pre-Increment-2.</summary>
    [JsonPropertyName("commandersSource")]
    public string? CommandersSource { get; init; }

    /// <summary>Per-commander rows from the EDHREC averages dump (Increment 2; empty pre-Increment-2).</summary>
    [JsonPropertyName("commanders")]
    public IReadOnlyList<ManabaseCommanderBaseline> Commanders { get; init; } = Array.Empty<ManabaseCommanderBaseline>();

// New record in the same file:
/// <summary>One per-commander (or partner-pair) community baseline cell from the EDHREC averages dump.</summary>
public sealed record ManabaseCommanderBaseline
{
    /// <summary>Primary commander name as published by EDHREC.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Partner commander name, when the row is a pair.</summary>
    [JsonPropertyName("partnerName")]
    public string? PartnerName { get; init; }

    /// <summary>Average land count across the sample (integer-rounded upstream).</summary>
    [JsonPropertyName("avgLands")]
    public required double AvgLands { get; init; }

    /// <summary>Number of EDHREC decks behind the average.</summary>
    [JsonPropertyName("deckCount")]
    public required int DeckCount { get; init; }
}
```

- [ ] **Step 2: Write failing converter tests** — cover, with inline CSV literals (use raw strings; do NOT re-indent them):
  - Header + simple rows parse; `>= minDeckCount` filter applied; result ordered by `DeckCount` desc then `Name` ordinal.
  - Quoted name with comma + apostrophe (`"Y'shtola, Night's Blessed"`) parses intact.
  - Partner pair row (`commander2` populated) produces `PartnerName`.
  - Malformed rows (non-numeric `avg_land`, empty `commander`, too-few columns) are skipped and counted in `SkippedMalformed`, never throw.
  - Two rows normalizing to the same `ManabaseCommanderKey` keep the higher `DeckCount` and increment `DuplicateCollisions`.
  - Column order is taken from the header (find `commander`, `commander2`, `avg_land`, `number_decks` by name, not fixed index).
- [ ] **Step 3: Verify failure** — `dotnet test DeckFlow.Core.Tests --filter EdhrecAverages` → type not found.
- [ ] **Step 4: Implement `EdhrecAveragesConverter`** — hand-rolled RFC-4180-lite CSV line parser (quoted fields, doubled quotes, commas inside quotes; the dump has no embedded newlines — state that assumption in a comment). Algorithm: split header → locate column indexes by name (missing required column ⇒ return empty result with `SkippedMalformed = 0`? No — throw `FormatException`: a missing column is a wrong-file error, not a bad row; test this) → per data line: parse fields, `int.TryParse` deckCount + `double.TryParse(CultureInfo.InvariantCulture)` avgLands, name non-blank, else skip+count → filter `deckCount >= minDeckCount` → dedupe on `ManabaseCommanderKey.Create(name, partner)` keeping higher deckCount (count collisions) → order `DeckCount` desc, `Name` ordinal asc → materialize records.
- [ ] **Step 5: Verify pass**, run full Core suite: `dotnet test DeckFlow.Core.Tests` → green.
- [ ] **Step 6: Commit** — `feat(manabase): EDHREC averages CSV converter + commander snapshot rows`

---

### Task 3: CLI generator command + regenerate bundled latest.json

**Files:**
- Modify: `DeckFlow.CLI/Program.cs` (register command, mirroring the existing command registrations)
- Create: `DeckFlow.CLI/EdhrecAveragesCommandRunner.cs` (runner — one file per runner is the repo convention: `DeckCommandRunners.cs`, `ManabaseCommandRunner.cs`, `CedhBaselineCommandRunner.cs`; there is NO `CommandRunners.cs`)
- Modify: `DeckFlow.Web/Data/manabase-baseline/latest.json` (generated output — the ONLY generated artifact committed)

**Interfaces:**
- Consumes: `EdhrecAveragesConverter.Convert`, snapshot DTOs (Task 2).
- Produces: CLI command `edhrec-averages --csv <path> --data-file <path>`; regenerated `latest.json` with `commanders` block.

- [ ] **Step 1: Add runner** as `DeckFlow.CLI/EdhrecAveragesCommandRunner.cs` (study `CedhBaselineCommandRunner.cs` first and match its shape — Serilog logging, and the repo's runner error convention: catch `IOException`, `UnauthorizedAccessException`, `InvalidOperationException`, `FormatException`, `JsonException`, log the error, return `1`; see `DeckCommandRunners.cs:86-90` for the pattern). Core logic:

```csharp
    /// <summary>
    /// Converts an extracted EDHREC averages.csv into the bundled manabase-baseline data file,
    /// replacing the commanders block while preserving the pilot brackets block untouched.
    /// </summary>
    public static async Task<int> RunEdhrecAveragesAsync(string csvPath, string dataFilePath)
    {
        string csvText = await File.ReadAllTextAsync(csvPath).ConfigureAwait(false);
        EdhrecAveragesResult result = EdhrecAveragesConverter.Convert(csvText);

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        ManabaseBaselineSnapshot existing = JsonSerializer.Deserialize<ManabaseBaselineSnapshot>(
            await File.ReadAllTextAsync(dataFilePath).ConfigureAwait(false), jsonOptions)
            ?? throw new InvalidOperationException($"Existing data file is empty: {dataFilePath}");

        ManabaseBaselineSnapshot updated = existing with
        {
            GeneratedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            CommandersSource = "edhrec-averages",
            Commanders = result.Commanders,
        };

        await File.WriteAllTextAsync(dataFilePath, JsonSerializer.Serialize(updated, jsonOptions)).ConfigureAwait(false);
        Log.Information(
            "Wrote {Count} commander baselines ({Skipped} malformed skipped, {Collisions} duplicate collisions) to {Path}",
            result.Commanders.Count, result.SkippedMalformed, result.DuplicateCollisions, dataFilePath);
        return 0;
    }
```

(Wrap the body in the try/catch convention above. `generatedUtc` refreshes at snapshot root — decided in spec review; no `commandersGeneratedFromDump` field — `generatedUtc` suffices, decided at plan review. Verify `with` round-trips all snapshot properties — it does, records copy untouched members.)

- [ ] **Step 2: Register command** in `Program.cs` beside the other commands: `new Command("edhrec-averages", "Convert an EDHREC averages.csv dump into the bundled manabase-baseline data file.")` with `--csv` (required) and `--data-file` (default `DeckFlow.Web/Data/manabase-baseline/latest.json` relative to repo root — take the literal default; the operator runs from repo root).
- [ ] **Step 3: Build** — `dotnet build DeckFlow.sln` → 0 errors/0 new warnings.
- [ ] **Step 4: Run against the real dump.** The CSV is staged at `/tmp/claude-1000/-mnt-c-users-chrislunt-source-personal-deckflow/3e922861-5894-43c4-a081-81b2f317c214/scratchpad/averages-jul26-y7v08maq/averages.csv` (foreman will copy it if the path is gone — flag NEEDS_CONTEXT if missing). Run the command; verify output: `brackets` array byte-identical to before (4 pilot rows, values 35.9/35.5/34.5/30.5 + note), `commanders` length = 3,179, first row The Ur-Dragon avgLands 35 deckCount 48802, `commandersSource: "edhrec-averages"`, `schemaVersion` still 1, `source` still `edhrec-pilot-aggregate`. Do NOT commit the CSV.
- [ ] **Step 5: Commit** — `feat(manabase): edhrec-averages CLI generator + bundled per-commander baselines` (includes regenerated `latest.json`).

---

### Task 4: Provider commander lookup (Web)

**Files:**
- Modify: `DeckFlow.Web/Services/Manabase/ManabaseBaselineProvider.cs`
- Test: `DeckFlow.Web.Tests/ManabaseBaselineProviderTests.cs` (extend)

**Interfaces:**
- Consumes: `ManabaseCommanderKey.Create` (Task 1), `Commanders` on snapshot (Task 2).
- Produces: on `IManabaseBaselineProvider`:

```csharp
    /// <summary>Returns the bundled per-commander baseline row, or null if absent/unavailable.</summary>
    /// <param name="commanderNames">1 (lone) or 2 (partner pair) commander names; other counts return null.</param>
    ManabaseCommanderBaseline? TryGetCommanderBaseline(IReadOnlyList<string> commanderNames);
```

(List-shaped to match `resolved.CommanderNames` and `CedhLandBaselineProvider.TryGetBaseline` — the caller already holds that list. This is the signature the spec's Provider section defines.)

- [ ] **Step 1: Failing tests** — extend the existing test file using its established snapshot-file test seam: lone hit; pair hit given `["Halana, Kessig Ranger","Alena, Kessig Trapper"]` AND the reversed order; lone name does NOT match a pair row sharing the primary name; unknown name ⇒ null; 0 or 3 names ⇒ null; case/punctuation-insensitive hit; two rows collapsing to the same key ⇒ the higher-`DeckCount` row wins (spec dedup rule, mirrored provider-side as defense-in-depth); snapshot without `commanders` (Increment 1 file shape) ⇒ commander lookup null while `TryGetBracketBaseline` still works; corrupt file ⇒ null.
- [ ] **Step 2: Verify failure.**
- [ ] **Step 3: Implement** — build `Dictionary<string, ManabaseCommanderBaseline>` (ordinal comparer) once inside snapshot load (extend the `CacheEntry` record to carry it: `private sealed record CacheEntry(ManabaseBaselineSnapshot? Snapshot, IReadOnlyDictionary<string, ManabaseCommanderBaseline>? Commanders)`), keyed `ManabaseCommanderKey.Create(row.Name, row.PartnerName)`; duplicate keys: keep the higher-`DeckCount` row (same rule as the generator — the file is normally pre-deduped, but the provider must not depend on that; comment this). Lookup: count 1 ⇒ `Create(names[0])`; count 2 ⇒ `Create(names[0], names[1])` (helper sorts internally, so one probe suffices — no dual-order probing needed, unlike `CedhLandBaselineProvider.CandidateKeys`); else null.
- [ ] **Step 4: Verify pass**, full provider test class green.
- [ ] **Step 5: Commit** — `feat(manabase): per-commander baseline lookup on provider`

---

### Task 5: Weighting integration + result block (Web + Core record)

**Files:**
- Modify: `DeckFlow.Core/Manabase/ManabaseCommunityBaseline.cs` (extend result record)
- Modify: `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` (`BuildCommunityBaseline` + call site ~line 524)
- Test: `DeckFlow.Web.Tests/ManabaseBracketResolutionTests.cs` (extend; follow its existing fake/seam pattern)

**Interfaces:**
- Consumes: `IManabaseBaselineProvider.TryGetCommanderBaseline` (Task 4), `ManabaseBaselineWeighting.Compute` (shipped), `resolved.CommanderNames`, `report` (for suppression).
- Produces: `ManabaseCommunityBaseline` record gains:

```csharp
    /// <summary>Where the displayed land value came from (commander cell, blend, or bracket-global).</summary>
    public required ManabaseBaselineSource ValueSource { get; init; }

    /// <summary>EDHREC deck count behind the commander cell when it contributed (Commander/Blended); null for Global.</summary>
    public int? CommanderDeckCount { get; init; }

    /// <summary>Display name(s) for the commander cell when it contributed (e.g. "The Ur-Dragon"); null for Global.</summary>
    public string? CommanderDisplayName { get; init; }
```

  All existing constructions gain `ValueSource = ManabaseBaselineSource.Global` (compiler will find them).

- [ ] **Step 1: Failing tests** — through the service seam used by `ManabaseBracketResolutionTests`, with a provider fake/file exposing both bracket rows and commander rows:
  - Bracket 2, commander with `deckCount >= 400` ⇒ `ValueSource = Commander`, `AvgLands` = commander value, `CommanderDeckCount`/`CommanderDisplayName` set, `DeckCount` = commander deck count.
  - Bracket 3, commander `deckCount` 250 ⇒ `Blended`, value equals the linear formula `w*commander + (1-w)*global` with `w = (250-100)/300`.
  - Bracket 2, commander `deckCount` 50 ⇒ `Global` (bracket row values, commander fields null).
  - Bracket 4 and 5 with a 48k-deck commander ⇒ `Global` (commander cell ignored).
  - No commander resolved (empty `CommanderNames`) ⇒ `Global`.
  - cEDH suppression: when the report renders the meta range ⇒ `CommunityBaseline` is null. The suppression predicate must copy the view's `@if` at `Manabase.cshtml:455-459` **verbatim — all five members**: `TargetLandsRangeLow`, `TargetLandsRangeHigh`, `BaselineLandsMean`, `BaselineDeckCount`, `BaselineLandsSd` (read the view first; if any name differs, the view wins). A partial predicate would suppress the community line when the range does NOT render — the spec's "never show two" rule broken in the opposite direction.
  - Flag OFF ⇒ null (existing regression stays green).
- [ ] **Step 2: Verify failure.**
- [ ] **Step 3: Implement.** Change `BuildCommunityBaseline(ManabaseAnalysisOptions options)` to `BuildCommunityBaseline(ManabaseAnalysisOptions options, IReadOnlyList<string> commanderNames, ManabaseReport report)`; call site passes `resolved.CommanderNames` and the built `report`. Body after the existing bracket-row fetch:

```csharp
        // The commander-keyed cEDH meta range (CedhLandBaselineProvider) supersedes the community
        // line — never show two differently-sourced community baselines at once. This predicate must
        // mirror the view's meta-range render condition (Manabase.cshtml ~455-459) MEMBER-FOR-MEMBER.
        if (report.TargetLandsRangeLow is not null
            && report.TargetLandsRangeHigh is not null
            && report.BaselineLandsMean is not null
            && report.BaselineDeckCount is not null
            && report.BaselineLandsSd is not null)
        {
            return null;
        }

        // Commander cell participates only at brackets 2-3: dump means are bracket-agnostic and the
        // EDHREC population is casual-dominated, so a popular commander's mean would drown the
        // optimized/cEDH bracket signal (spec: Weighting integration).
        ManabaseCommanderBaseline? commanderRow = bracket is 2 or 3 && commanderNames.Count is 1 or 2
            ? _manabaseBaseline.TryGetCommanderBaseline(commanderNames)
            : null;

        ManabaseBaselineResult weighted = ManabaseBaselineWeighting.Compute(
            commanderRow?.AvgLands, null, null, commanderRow?.DeckCount ?? 0,
            row.AvgLands, null, null);

        bool commanderContributed = weighted.Lands.Source
            is ManabaseBaselineSource.Commander or ManabaseBaselineSource.Blended;

        return new ManabaseCommunityBaseline
        {
            Bracket = bracket,
            AvgLands = weighted.Lands.Value ?? row.AvgLands,
            DeckCount = commanderContributed ? commanderRow!.DeckCount : row.DeckCount,
            Source = commanderContributed ? "edhrec-averages" : row.Source,
            BracketSource = bracketSource,
            ValueSource = weighted.Lands.Source,
            CommanderDeckCount = commanderContributed ? commanderRow!.DeckCount : null,
            CommanderDisplayName = commanderContributed
                ? commanderRow!.PartnerName is null ? commanderRow.Name : $"{commanderRow.Name} + {commanderRow.PartnerName}"
                : null,
        };
```

  (Verify the exact five report property names against the view's `@if` at `Manabase.cshtml:455-459` before writing this predicate — the view is the source of truth; nullability forms (`is not null` vs `is { }` on value types) must match what `ManabaseReport` actually declares. `ManabaseBaselineSource.None` cannot occur here because `row.AvgLands` is always present as global.)
- [ ] **Step 4: Verify pass**, run full Web suite → green, no fewer passes than 1514.
- [ ] **Step 5: Commit** — `feat(manabase): blend per-commander EDHREC cell into community baseline (brackets 2-3)`

---

### Task 6: UI line copy + attribution + muted CSS

**Files:**
- Modify: `DeckFlow.Web/Views/Deck/Manabase.cshtml` (community-baseline block, currently ~lines 465–476)
- Modify: `DeckFlow.Web/wwwroot/css/site-common.css` (one rule)
- Test: extend `DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs` — the existing Razor-rendering test host (`IRazorViewEngine`-based, ~line 958). Do NOT put rendered-HTML assertions in `ManabaseBracketResolutionTests` (service-level, never renders Razor).

**Interfaces:**
- Consumes: `ValueSource`, `CommanderDeckCount`, `CommanderDisplayName`, `Source` (Task 5).

- [ ] **Step 1: Failing render tests** — in `ManabaseViewRenderTests` (reuse its render helper + model builders), rendered page (flag ON, seeded data) contains:
  - Commander source: `EDHREC decks for The Ur-Dragon average` and `Data from EDHREC` inside `class="manabase-baseline-source"`.
  - Global source at bracket 2 with pilot provenance: existing `Community baseline · Core` copy, NO attribution span (pilot source is not EDHREC-labeled).
  - Flag OFF: neither string renders (byte-identical regression already covered — keep it green).
- [ ] **Step 2: Verify failure.**
- [ ] **Step 3: Update the view block** — replace the existing `@if (Model.CommunityBaseline is { } baseline)` body:

```razor
            @if (Model.CommunityBaseline is { } baseline)
            {
                var bracketName = ManabaseCommunityBaseline.BracketName(baseline.Bracket);
                var detected = baseline.BracketSource == ManabaseBracketSource.Auto ? " (auto-detected)" : null;
                var fromEdhrec = baseline.Source?.StartsWith("edhrec-averages", StringComparison.Ordinal) == true;
                <p class="manabase-summary-lands manabase-community-baseline">
                    @if (baseline.CommanderDisplayName is { } commanderName)
                    {
                        <strong>Community baseline</strong> @: · @bracketName@detected:
                        <text>EDHREC decks for @commanderName average <strong>~@baseline.AvgLands.ToString("F1") lands</strong> (@baseline.DeckCount.ToString("N0") decks).</text>
                    }
                    else
                    {
                        <strong>Community baseline</strong> @: · @bracketName@detected
                        <text>(@baseline.DeckCount.ToString("N0") decks): <strong>~@baseline.AvgLands.ToString("F1") lands.</strong></text>
                    }
                    @if (fromEdhrec)
                    {
                        <span class="manabase-baseline-source">Data from EDHREC</span>
                    }
                </p>
            }
```

  (Blended shows the commander phrasing with the blended value — one human line, no "blended" jargon, per spec open-question resolution. Match the file's exact Razor whitespace/indent conventions; the `@:` fragments above are indicative — express the same copy in the file's existing style and delete the Increment-1 "attribution intentionally omitted" comment.)
- [ ] **Step 4: CSS** — in `site-common.css`, near the other `.manabase-*` rules:

```css
.manabase-baseline-source {
  margin-left: 0.5rem;
  font-size: 0.85em;
  color: var(--muted);
}
```

- [ ] **Step 5: Verify pass**; `dotnet build DeckFlow.sln` clean; full Web suite green.
- [ ] **Step 6: Commit** — `feat(manabase): commander-aware baseline line + EDHREC attribution`

---

### Task 7: README + docs + suite gate

**Files:**
- Modify: `README.md` (manabase feature bullet: mention EDHREC-derived community baseline + attribution + generator command)
- Modify: `.planning/specs/2026-07-17-manabase-commander-baseline-inc2-design.md` (status → IMPLEMENTED)

- [ ] **Step 1:** README: extend the existing manabase section with 2-3 sentences (community baseline now blends per-commander EDHREC averages at brackets 2-3; data refreshed via `dotnet run --project DeckFlow.CLI -- edhrec-averages --csv <path>`; data © EDHREC, non-commercial community license).
- [ ] **Step 2:** Full gate: `dotnet build DeckFlow.sln` 0/0, `dotnet test DeckFlow.Core.Tests` green, `dotnet test DeckFlow.Web.Tests` green (≥1514 pass / 16 skip pattern; no new failures).
- [ ] **Step 3: Commit** — `docs(manabase): README + spec status for Increment 2`

---

## Out of scope (do NOT touch)

- `manabase_baseline` DB table / `ManabaseBaselineStore` (stays parked).
- Bracket selector UI, pilot bracket rows, Karsten math, cEDH meta-range computation.
- Live e2e specs (foreman runs the visual sweep separately).
- Lockfiles, `.gitattributes`, workflows, `wwwroot/js/*`.
