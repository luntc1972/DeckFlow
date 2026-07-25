# Manabase Community Baseline — Increment 1a (backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (or subagent-driven-development) to implement task-by-task. Steps use checkbox (`- [ ]`) tracking.

**Goal:** Compute a flag-gated, empirical per-bracket community **land** baseline (B2–B5) and attach it to the manabase analysis result — loaded from a bundled JSON via a provider that mirrors `CedhLandBaselineProvider`. No UI, no deck-bracket classification yet (those are Increment 1b). Additive: flag OFF → byte-identical output.

**Architecture:** A bundled `Data/manabase-baseline/latest.json` (B2–B5 land means from the research pilot) loaded by `ManabaseBaselineProvider` (mirrors `CedhLandBaselineProvider` exactly: content-root path, `IMemoryCache`, fail-open). `ManabaseAnalysisService` injects the provider, resolves an effective bracket (`options.Bracket ?? mode-derived default`), and — only when flag `analysis.manabase.baseline` is ON — builds a `ManabaseCommunityBaseline` block onto `ManabaseAnalysisResult`. In 1a `options.Bracket` is always null (the controller sets it in 1b), so 1a uses the mode-derived bracket.

**Tech Stack:** C# 12 / .NET 10, System.Text.Json (`JsonSerializerDefaults.Web`), Dapper N/A, xUnit (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`). No new dependencies. LF; changed lines pass the format gate.

**Build/test (Windows dotnet from WSL):**
- Build core: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj`
- Build web: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj`
- Core tests (filtered): `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "ManabaseCommunityBaseline"`
- Web provider tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "ManabaseBaselineProvider"`
- Web wiring tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "ManabaseCommunityBaselineWiring"`
- Full suites: `... test DeckFlow.Core.Tests/...` and `... test DeckFlow.Web.Tests/...`

---

## Patterns to mirror (confirmed file:line)

- **Provider:** `DeckFlow.Web/Services/Manabase/CedhLandBaselineProvider.cs` (whole file) — content-root path in the DI ctor (`:40-49`), `internal` test-seam ctor with explicit path (`:52-59`), `IMemoryCache` + `CacheEntry` record so null is cached (`:111-134,149`), fail-open `catch when (IOException or UnauthorizedAccessException or JsonException)` (`:124-130`), `EnsureLoaded()` (`:62-63`). Snapshot DTOs live in **Core** (`DeckFlow.Core/Manabase/CedhLandBaseline.cs`, `CedhLandBaselineSnapshot` uses `[JsonPropertyName]`).
- **Provider DI + warm-load:** `DeckFlow.Web/Program.cs:94` (`AddSingleton<ICedhLandBaselineProvider, CedhLandBaselineProvider>()`), `:304` (`app.Services.GetRequiredService<ICedhLandBaselineProvider>().EnsureLoaded();`). `AddMemoryCache()` at `:69`.
- **csproj data-file copy:** `DeckFlow.Web/DeckFlow.Web.csproj:37-39` (`<Content Update="Data\cedh-land-baseline\*.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>`).
- **Flag read (fail-safe OFF):** `ManabaseAnalysisService.cs:543-546` `IsFlagOn(key)` (`_featureFlags.Snapshot().TryGetValue(key, out bool enabled) && enabled`). Flag-key consts at `:184-287`. Example use `:356` (`bool cedhLandTarget = IsFlagOn(CedhLandTargetFlagKey);`). `_featureFlags` injected at `:301`; `_cedhLandBaseline` injected at `:305`.
- **Flag seed (both dialects) + catalog:** `FeatureFlagStore.cs:239` (PG seed `('...', FALSE),`), `:287` (SQLite seed `('...', 0),`); `FeatureFlagCatalog.cs:150-153` (description map — REQUIRED or `FeatureFlagCatalogTests` fails).
- **Options + Result shapes:** `ManabaseAnalysisService.cs` — `ManabaseAnalysisOptions` (`:51-73`, sealed class, `init` props), `ManabaseAnalysisResult` (`:88-144`, sealed record + `init` props like `CompanionRow`).
- **Mode enum:** `DeckFlow.Core/Manabase/ManabaseMode.cs` — `Casual`, `Focused`, `Cedh`.
- **Display-only ⇒ NOT a prompt-mutating flag:** do NOT add this key to `DeckAnalysisPacketService.PromptMutatingAnalysisFlags` (`:160`) — this block never mutates the swap prompt/paste artifact.

---

## File Structure

**Create:**
- `DeckFlow.Core/Manabase/ManabaseCommunityBaseline.cs` — snapshot DTOs (`ManabaseBaselineSnapshot`, `ManabaseBracketBaseline`) + result block (`ManabaseCommunityBaseline`) + `ManabaseBracketSource` enum.
- `DeckFlow.Web/Data/manabase-baseline/latest.json` — B2–B5 land means.
- `DeckFlow.Web/Services/Manabase/ManabaseBaselineProvider.cs` — `IManabaseBaselineProvider` + impl.
- `DeckFlow.Core.Tests/Manabase/ManabaseCommunityBaselineTests.cs` — snapshot JSON deserialization.
- `DeckFlow.Web.Tests/ManabaseBaselineProviderTests.cs` — provider load/lookup/fail-open.
- `DeckFlow.Web.Tests/ManabaseCommunityBaselineWiringTests.cs` — flag-gated result-block wiring.

**Modify:**
- `DeckFlow.Web/DeckFlow.Web.csproj` — add the `Data\manabase-baseline\*.json` content copy.
- `DeckFlow.Web/Program.cs` — register provider + warm-load.
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — flag const, `ManabaseAnalysisOptions.Bracket`, provider injection, block build + attach to `ManabaseAnalysisResult`.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — seed the flag OFF (both dialects).
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — flag description.

---

## Task 1: Core types — snapshot DTOs + result block

**Files:** Create `DeckFlow.Core/Manabase/ManabaseCommunityBaseline.cs`, `DeckFlow.Core.Tests/Manabase/ManabaseCommunityBaselineTests.cs`

- [ ] **Step 1: Write the failing test.** Create `DeckFlow.Core.Tests/Manabase/ManabaseCommunityBaselineTests.cs`:

```csharp
using System.Text.Json;
using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Verifies the bundled manabase community-baseline JSON deserializes into the snapshot DTOs
/// (camelCase Web defaults + explicit property names), covering the B2-B5 land rows.
/// </summary>
public sealed class ManabaseCommunityBaselineTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Snapshot_deserializes_bracket_rows()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "generatedUtc": "2026-07-17T00:00:00Z",
              "source": "edhrec-pilot-aggregate",
              "brackets": [
                { "bracket": 2, "avgLands": 35.9, "deckCount": 124221 },
                { "bracket": 3, "avgLands": 35.5, "deckCount": 140632 },
                { "bracket": 4, "avgLands": 34.5, "deckCount": 72399 },
                { "bracket": 5, "avgLands": 30.5, "deckCount": 4761, "note": "genuine-cEDH mean" }
              ]
            }
            """;

        var snapshot = JsonSerializer.Deserialize<ManabaseBaselineSnapshot>(json, WebOptions);

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.SchemaVersion);
        Assert.Equal("edhrec-pilot-aggregate", snapshot.Source);
        Assert.Equal(4, snapshot.Brackets.Count);

        var b3 = snapshot.Brackets.Single(b => b.Bracket == 3);
        Assert.Equal(35.5, b3.AvgLands, 3);
        Assert.Equal(140632, b3.DeckCount);
        Assert.Null(b3.Note);

        var b5 = snapshot.Brackets.Single(b => b.Bracket == 5);
        Assert.Equal("genuine-cEDH mean", b5.Note);
    }
}
```

- [ ] **Step 2: Create the types.** Create `DeckFlow.Core/Manabase/ManabaseCommunityBaseline.cs`:

```csharp
using System.Text.Json.Serialization;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// The bundled community-baseline snapshot (deserialized from Data/manabase-baseline/latest.json).
/// Increment 1 carries per-bracket land means only; ramp/draw and per-commander rows arrive later.
/// </summary>
public sealed record ManabaseBaselineSnapshot
{
    /// <summary>Schema version of the data file (currently 1).</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    /// <summary>ISO-8601 UTC timestamp the file was generated.</summary>
    [JsonPropertyName("generatedUtc")]
    public string? GeneratedUtc { get; init; }

    /// <summary>Provenance label for the numbers (e.g. "edhrec-pilot-aggregate").</summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>Per-bracket rows (B2-B5 in Increment 1).</summary>
    [JsonPropertyName("brackets")]
    public IReadOnlyList<ManabaseBracketBaseline> Brackets { get; init; } = Array.Empty<ManabaseBracketBaseline>();
}

/// <summary>One per-bracket community baseline cell: the average land count real decks run at that bracket.</summary>
public sealed record ManabaseBracketBaseline
{
    /// <summary>Power bracket (2-5; Exhibition/B1 is unsupported).</summary>
    [JsonPropertyName("bracket")]
    public int Bracket { get; init; }

    /// <summary>Average land count across the sample.</summary>
    [JsonPropertyName("avgLands")]
    public double AvgLands { get; init; }

    /// <summary>Number of decks behind the average (display + trust).</summary>
    [JsonPropertyName("deckCount")]
    public int DeckCount { get; init; }

    /// <summary>
    /// Optional per-row provenance. Absent in Increment 1 (the file carries one snapshot-level
    /// <see cref="ManabaseBaselineSnapshot.Source"/>); the provider backfills this from the snapshot
    /// source when the row omits it. Increment 2 may set it per row (corpus vs edhrec).
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>Optional caveat note (e.g. thin/adjusted sample).</summary>
    [JsonPropertyName("note")]
    public string? Note { get; init; }
}

/// <summary>How the effective bracket for a result was chosen (drives the UI "auto-detected" hint in 1b).</summary>
public enum ManabaseBracketSource
{
    /// <summary>Auto-classified from the deck (Increment 1b).</summary>
    Auto,

    /// <summary>Explicitly chosen by the user via the bracket selector.</summary>
    Override,

    /// <summary>Derived from the analysis mode because no classification/override was available.</summary>
    Fallback,
}

/// <summary>
/// The resolved community-baseline block attached to a manabase result: the bracket used, its
/// bundled land average + sample size + provenance, and how the bracket was chosen. Present only
/// when the feature flag is on and a baseline row exists for the bracket.
/// </summary>
public sealed record ManabaseCommunityBaseline
{
    /// <summary>The bracket (2-5) this baseline is for.</summary>
    public required int Bracket { get; init; }

    /// <summary>Average land count real decks run at this bracket.</summary>
    public required double AvgLands { get; init; }

    /// <summary>Sample size behind <see cref="AvgLands"/>.</summary>
    public required int DeckCount { get; init; }

    /// <summary>Provenance label from the data file.</summary>
    public required string? Source { get; init; }

    /// <summary>How the bracket was chosen.</summary>
    public required ManabaseBracketSource BracketSource { get; init; }
}
```

- [ ] **Step 3: Build + run.** `... build DeckFlow.Core/DeckFlow.Core.csproj` (0/0), then `... test DeckFlow.Core.Tests/... --filter "ManabaseCommunityBaseline"` → PASS.
- [ ] **Step 4: Commit.** `git commit -m "feat(manabase): community-baseline snapshot DTOs + result block (Core)"`

---

## Task 2: Bundled data file + csproj copy

**Files:** Create `DeckFlow.Web/Data/manabase-baseline/latest.json`, Modify `DeckFlow.Web/DeckFlow.Web.csproj`

- [ ] **Step 1: Create the data file** `DeckFlow.Web/Data/manabase-baseline/latest.json` (B2-B5 land means from `.planning/research/2026-07-16-edhrec-bracket-land-data.md`; B1 intentionally absent):

```json
{
  "schemaVersion": 1,
  "generatedUtc": "2026-07-17T00:00:00Z",
  "source": "edhrec-pilot-aggregate",
  "brackets": [
    { "bracket": 2, "avgLands": 35.9, "deckCount": 124221 },
    { "bracket": 3, "avgLands": 35.5, "deckCount": 140632 },
    { "bracket": 4, "avgLands": 34.5, "deckCount": 72399 },
    { "bracket": 5, "avgLands": 30.5, "deckCount": 4761, "note": "genuine-cEDH mean; thin casual-favorite cEDH cells excluded" }
  ]
}
```

- [ ] **Step 2: Add the content-copy** to `DeckFlow.Web/DeckFlow.Web.csproj` next to the cedh-land-baseline block (`:37-39`), same `PreserveNewest`:

```xml
    <Content Update="Data\manabase-baseline\*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
```

- [ ] **Step 3: Build web** `... build DeckFlow.Web/DeckFlow.Web.csproj` → 0/0. (Verified functionally by Task 3's provider test.)
- [ ] **Step 4: Commit.** `git commit -m "feat(manabase): bundle B2-B5 community land baseline data file"`

---

## Task 3: Provider + DI (mirror CedhLandBaselineProvider)

**Files:** Create `DeckFlow.Web/Services/Manabase/ManabaseBaselineProvider.cs`, Modify `DeckFlow.Web/Program.cs`, Create `DeckFlow.Web.Tests/ManabaseBaselineProviderTests.cs`

- [ ] **Step 1: Write the failing tests.** Create `DeckFlow.Web.Tests/ManabaseBaselineProviderTests.cs` (mirror the temp-file style of `FeedbackStoreTests`; use the provider's internal test-seam ctor with an explicit path):

```csharp
using System.IO;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Services.Manabase;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Verifies the manabase community-baseline provider loads the bundled JSON, resolves per-bracket
/// rows, and fails open (missing/malformed file → null, never throws).
/// </summary>
public sealed class ManabaseBaselineProviderTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public ManabaseBaselineProviderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"manabase-baseline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "latest.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private ManabaseBaselineProvider CreateProvider()
        => new(_path, new MemoryCache(new MemoryCacheOptions()));

    private void WriteFile(string json) => File.WriteAllText(_path, json);

    private const string SampleJson = """
        {
          "schemaVersion": 1,
          "source": "edhrec-pilot-aggregate",
          "brackets": [
            { "bracket": 2, "avgLands": 35.9, "deckCount": 124221 },
            { "bracket": 3, "avgLands": 35.5, "deckCount": 140632 },
            { "bracket": 5, "avgLands": 30.5, "deckCount": 4761 }
          ]
        }
        """;

    [Fact]
    public void Known_bracket_returns_row()
    {
        WriteFile(SampleJson);
        var row = CreateProvider().TryGetBracketBaseline(3);
        Assert.NotNull(row);
        Assert.Equal(35.5, row!.AvgLands, 3);
        Assert.Equal(140632, row.DeckCount);
    }

    [Fact]
    public void Row_backfills_snapshot_source()
    {
        WriteFile(SampleJson); // rows omit their own "source"; snapshot source is edhrec-pilot-aggregate
        var row = CreateProvider().TryGetBracketBaseline(2);
        Assert.NotNull(row);
        Assert.Equal("edhrec-pilot-aggregate", row!.Source);
    }

    [Fact]
    public void Unknown_bracket_returns_null()
    {
        WriteFile(SampleJson);
        Assert.Null(CreateProvider().TryGetBracketBaseline(4)); // not in this file
    }

    [Fact]
    public void Missing_file_returns_null_no_throw()
    {
        Assert.Null(CreateProvider().TryGetBracketBaseline(3)); // file never written
    }

    [Fact]
    public void Malformed_file_returns_null_no_throw()
    {
        WriteFile("{ this is not valid json ");
        Assert.Null(CreateProvider().TryGetBracketBaseline(3));
    }
}
```

- [ ] **Step 2: Create the provider** `DeckFlow.Web/Services/Manabase/ManabaseBaselineProvider.cs` (mirror `CedhLandBaselineProvider.cs` exactly — same ctors, cache, fail-open, `CacheEntry`):

```csharp
using System.Text.Json;
using DeckFlow.Core.Manabase;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Web.Services.Manabase;

/// <summary>
/// Loads the committed community-baseline snapshot once and serves per-bracket land lookups from an
/// in-memory cache. Fail-open: a missing or corrupt file degrades to "no baseline", never an error.
/// </summary>
public interface IManabaseBaselineProvider
{
    /// <summary>Warm-loads the baseline snapshot into memory, swallowing file/parse failures.</summary>
    void EnsureLoaded();

    /// <summary>Returns the bundled baseline row for a bracket, or null if absent/unavailable.</summary>
    /// <param name="bracket">Power bracket (2-5).</param>
    ManabaseBracketBaseline? TryGetBracketBaseline(int bracket);
}

/// <inheritdoc />
public sealed class ManabaseBaselineProvider : IManabaseBaselineProvider
{
    private const string CacheKey = "manabase:community-baseline";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _dataFilePath;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    private int _loadFailureLogged;

    /// <summary>DI constructor — locates <c>Data/manabase-baseline/latest.json</c> in the content root.</summary>
    public ManabaseBaselineProvider(
        IWebHostEnvironment env,
        IMemoryCache cache,
        ILogger<ManabaseBaselineProvider>? logger = null)
        : this(
            Path.Combine(env.ContentRootPath, "Data", "manabase-baseline", "latest.json"),
            cache,
            logger)
    {
    }

    /// <summary>Test-seam constructor with an explicit baseline path.</summary>
    internal ManabaseBaselineProvider(string dataFilePath, IMemoryCache cache, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(dataFilePath);
        ArgumentNullException.ThrowIfNull(cache);
        _dataFilePath = dataFilePath;
        _cache = cache;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public void EnsureLoaded() => GetOrLoadSnapshot();

    /// <inheritdoc />
    public ManabaseBracketBaseline? TryGetBracketBaseline(int bracket)
    {
        ManabaseBaselineSnapshot? snapshot = GetOrLoadSnapshot();
        if (snapshot is null)
        {
            return null;
        }

        foreach (ManabaseBracketBaseline row in snapshot.Brackets)
        {
            if (row.Bracket == bracket)
            {
                // Backfill provenance from the snapshot-level source when the row omits its own
                // (Increment 1 rows share one source; Increment 2 may set per-row).
                return row.Source is null ? row with { Source = snapshot.Source } : row;
            }
        }

        return null;
    }

    private ManabaseBaselineSnapshot? GetOrLoadSnapshot()
    {
        if (_cache.TryGetValue<CacheEntry>(CacheKey, out CacheEntry? cached) && cached is not null)
        {
            return cached.Snapshot;
        }

        ManabaseBaselineSnapshot? snapshot = null;
        try
        {
            string json = File.ReadAllText(_dataFilePath);
            snapshot = JsonSerializer.Deserialize<ManabaseBaselineSnapshot>(json, JsonOptions);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            LogLoadFailureOnce(exception);
        }

        _cache.Set(CacheKey, new CacheEntry(snapshot), TimeSpan.FromHours(24));
        return snapshot;
    }

    private void LogLoadFailureOnce(Exception exception)
    {
        if (Interlocked.Exchange(ref _loadFailureLogged, 1) != 0)
        {
            return;
        }

        _logger.LogWarning(
            exception,
            "Manabase community baseline unavailable at {DataFilePath}; continuing without it.",
            _dataFilePath);
    }

    private sealed record CacheEntry(ManabaseBaselineSnapshot? Snapshot);
}
```

- [ ] **Step 3: Register in DI.** In `DeckFlow.Web/Program.cs`, add next to the cEDH provider registration (`:94`), changing only the added line:

```csharp
            builder.Services.AddSingleton<IManabaseBaselineProvider, ManabaseBaselineProvider>();
```

And warm-load next to the cEDH warm-load (`:304`):

```csharp
            app.Services.GetRequiredService<IManabaseBaselineProvider>().EnsureLoaded();
```
(Add the matching `using DeckFlow.Web.Services.Manabase;` only if not already present — the cEDH provider is in the same namespace, so it likely is.)

- [ ] **Step 4: Build + test.** `... build DeckFlow.Web/...` (0/0), then `... test DeckFlow.Web.Tests/... --filter "ManabaseBaselineProvider"` → 5 PASS.
- [ ] **Step 5: Commit.** `git commit -m "feat(manabase): community-baseline provider + DI (mirror CedhLandBaselineProvider)"`

---

## Task 4: Feature flag registration (seed OFF, catalog description)

**Files:** Modify `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs`, `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs`, `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs`

- [ ] **Step 1: Add the flag-key const** in `ManabaseAnalysisService.cs`, alongside the other flag-key consts (near `:184-287`; place after an existing `analysis.manabase.*` const):

```csharp
    /// <summary>
    /// Community-baseline flag key: when ON, attaches the empirical per-bracket land baseline block
    /// to the result (display-only, beside Karsten). Seeded OFF; OFF → byte-identical output.
    /// </summary>
    public const string BaselineFlagKey = "analysis.manabase.baseline";
```

- [ ] **Step 2: Seed OFF in BOTH dialects** in `FeatureFlagStore.cs`. Add to the Postgres seed list (near `:239`, matching the `('key', FALSE),` format):

```sql
        ('analysis.manabase.baseline', FALSE),
```

And the SQLite seed list (near `:287`, matching `('key', 0),`):

```sql
        ('analysis.manabase.baseline', 0),
```
(Both blocks use `ON CONFLICT (key) DO NOTHING`, so re-seeding is safe. Do not reorder existing rows; append within the block.)

- [ ] **Step 3: Add the catalog description** in `FeatureFlagCatalog.cs` (near `:150-153`, matching the existing `["key"] = "desc",` map entries — REQUIRED or `FeatureFlagCatalogTests` fails):

```csharp
        ["analysis.manabase.baseline"] = "Manabase: show the empirical community land baseline (per bracket) beside the Karsten target.",
```

- [ ] **Step 4: Update the flag tests (they are hard-coded, not reflective).** `FeatureFlagCatalogTests` uses explicit `[InlineData]` rows and `FeatureFlagStoreSeedTests` asserts specific seeded keys/values — neither auto-detects the new key, so add it to both:
  - In `DeckFlow.Web.Tests/.../FeatureFlagCatalogTests.cs`: add an `[InlineData("analysis.manabase.baseline")]` (or the exact row shape the existing cases use — open the file and mirror a sibling `analysis.manabase.*` case, e.g. `analysis.manabase.cedh-land-target`).
  - In `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs`: add an assertion that `analysis.manabase.baseline` seeds to `false`/OFF, mirroring the existing `analysis.manabase.cedh-land-target` seed assertion (including the Postgres-literal assertion if that test class checks the SQL text).
- [ ] **Step 5: Build + run the flag tests.** `... build DeckFlow.Web/...` (0/0), then `... test DeckFlow.Web.Tests/... --filter "FeatureFlag"` → PASS (catalog description present, seed OFF in both dialects, no exact-count assertion tripped).
- [ ] **Step 6: Commit.** `git commit -m "feat(manabase): register analysis.manabase.baseline flag (seeded OFF)"`

---

## Task 5: Wire the block into analysis (flag-gated, byte-identical OFF)

**Files:** Modify `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs`, Create `DeckFlow.Web.Tests/ManabaseCommunityBaselineWiringTests.cs`

- [ ] **Step 1: Add `Bracket` to `ManabaseAnalysisOptions`** (`:51-73`), after `SelectedCommander`:

```csharp
    /// <summary>
    /// Optional explicit bracket (2-5) for the community baseline. Null in Increment 1a (the mode
    /// picks the bracket); the controller sets it from deck classification / the selector in 1b.
    /// </summary>
    public int? Bracket { get; init; }
```

- [ ] **Step 2: Add the `CommunityBaseline` prop to `ManabaseAnalysisResult`** (`:98-144` init-props region, next to `CompanionRow`):

```csharp
    /// <summary>Optional empirical per-bracket community land baseline (present only when the flag is on).</summary>
    public ManabaseCommunityBaseline? CommunityBaseline { get; init; }
```

- [ ] **Step 3: Inject the provider.** Add a nullable `IManabaseBaselineProvider? _manabaseBaseline` field and ctor parameter, mirroring how `_cedhLandBaseline` (`:305`) and `_featureFlags` (`:301`) are injected (optional param defaulting to null so existing test ctors still compile). Assign in the ctor body.

- [ ] **Step 4: Add the resolution + build helpers** (private methods near `IsFlagOn` at `:543`):

```csharp
    // Map the 3-value analysis mode to a supported bracket (2-5) when no explicit bracket is given.
    // Casual -> Core(2), Focused -> Upgraded(3), Cedh -> cEDH(5). Overridden by options.Bracket in 1b.
    private static int ResolveBaselineBracket(ManabaseAnalysisOptions options)
        => options.Bracket ?? options.Mode switch
        {
            ManabaseMode.Cedh => 5,
            ManabaseMode.Focused => 3,
            _ => 2,
        };

    private ManabaseCommunityBaseline? BuildCommunityBaseline(ManabaseAnalysisOptions options)
    {
        if (!IsFlagOn(BaselineFlagKey) || _manabaseBaseline is null)
        {
            return null;
        }

        int bracket = ResolveBaselineBracket(options);
        ManabaseBracketBaseline? row = _manabaseBaseline.TryGetBracketBaseline(bracket);
        if (row is null)
        {
            return null;
        }

        return new ManabaseCommunityBaseline
        {
            Bracket = bracket,
            AvgLands = row.AvgLands,
            DeckCount = row.DeckCount,
            Source = row.Source,
            // 1a: no override/classification yet, so the bracket came from the mode.
            BracketSource = options.Bracket is null ? ManabaseBracketSource.Fallback : ManabaseBracketSource.Override,
        };
    }
```

- [ ] **Step 5: Attach the block at ONLY the successful-analysis result construction.** `ManabaseAnalysisService` builds `ManabaseAnalysisResult` in two places: the early **commander-selection-required** return (around `:378`, no report) and the **success** return (around `:493`, with the `Show*` init props). Add `CommunityBaseline = BuildCommunityBaseline(options),` to the **success initializer (~:493) only**. Leave the early-return (~:378) with `CommunityBaseline` unset (null) — the baseline is meaningless without a report. (Line numbers are approximate — anchor on the initializer that sets `ShowFocusedTier`/`CompanionRow`.)

- [ ] **Step 6: Write the wiring tests.** Create `DeckFlow.Web.Tests/ManabaseCommunityBaselineWiringTests.cs`. Mirror the construction of existing `ManabaseAnalysisService` tests (find a sibling test that builds the service with a fake `IFeatureFlagCache`); inject a fake `IManabaseBaselineProvider` returning canned rows and a fake flag cache toggling `analysis.manabase.baseline`. If the existing tests exercise the service end-to-end, prefer a **focused** test of `BuildCommunityBaseline` via a small deck; otherwise test through the public analyze entrypoint. Cover:
  1. **Flag OFF → `result.Report is not null` but `result.CommunityBaseline is null`** (byte-identical: the block is the only new output and it is absent).
  2. **Flag ON, mode Cedh, provider has B5 → `CommunityBaseline.Bracket == 5`, `AvgLands == 30.5`, `BracketSource == Fallback`.**
  3. **Flag ON, mode Casual → `Bracket == 2`, `AvgLands == 35.9`.**
  4. **Flag ON but provider returns null for the resolved bracket → `CommunityBaseline is null`** (graceful).
  5. **Flag ON but provider not injected (null) → `CommunityBaseline is null`** (no throw).

  If the existing `ManabaseAnalysisService` tests use a shared fake-flag helper and a real deck fixture, reuse them; name the fakes `FakeManabaseBaselineProvider` (returns a dict of bracket→row) per the repo's `Fake*` convention.

- [ ] **Step 7: Build + test.** `... build DeckFlow.Web/...` (0/0), then `... test DeckFlow.Web.Tests/... --filter "ManabaseCommunityBaselineWiring"` → PASS. Then the full Web suite → green (flag OFF everywhere else keeps output identical), and full Core suite → green.
- [ ] **Step 8: Commit.** `git commit -m "feat(manabase): attach community-baseline block to analysis result (flag-gated)"`

---

## Task 6: Review for simplification

- [ ] **Step 1:** Review the diff for reuse/reduction (e.g. the block-build helper is the single source; no duplicated flag/provider checks). If your harness has `/simplify`, run it; else review by hand.
- [ ] **Step 2:** Re-run `--filter "ManabaseCommunityBaseline"` (Core) + `--filter "ManabaseBaselineProvider"` + `--filter "ManabaseCommunityBaselineWiring"` → PASS.
- [ ] **Step 3:** Commit if changed: `git add -A && git commit -m "chore(manabase): simplify community-baseline backend" || echo "nothing to simplify"`

---

## Self-Review notes (author)

- **Spec coverage (Increment 1 backend):** Component A (data file, B2-B5, source label), Component B (provider mirroring `CedhLandBaselineProvider`, fail-open, `TryGetBracketBaseline`), Component D (analyzer augment: `ManabaseCommunityBaseline` block, flag `analysis.manabase.baseline` OFF → byte-identical, Karsten untouched). Component C (auto-classify) + Component E (UI selector/display) are **Increment 1b** — deliberately excluded; 1a uses a mode-derived bracket so `options.Bracket` stays null until 1b.
- **Byte-identical-OFF:** the block is the ONLY new result output and it is null when the flag is off; the flag is seeded OFF in both dialects with a catalog description (or `FeatureFlagCatalogTests` fails). No prompt/paste artifact is touched, so the key is intentionally NOT added to `PromptMutatingAnalysisFlags`.
- **Reuse:** provider is a near-verbatim clone of `CedhLandBaselineProvider` (path/cache/fail-open/`CacheEntry`); flag/seed/catalog follow the `analysis.manabase.cedh-land-target` precedent; the result block attaches like `CompanionRow`.
- **Distinct types:** `ManabaseCommunityBaseline` (this result block) is separate from P1's `ManabaseBaselineRow` (DB row, has commander/ramp/draw) and P2's `ManabaseBaselineWeighting`/`ManabaseBaselineSource` enum (weighting provenance) — different concepts, no name reuse. `ManabaseBracketSource` (Auto/Override/Fallback) is new and unrelated to P2's `ManabaseBaselineSource`.
- **Mode→bracket map:** Casual→2, Focused→3, Cedh→5 (monotonic, distinct). This is the 1a fallback; 1b replaces it with real classification via `options.Bracket`.
- **Constraints:** no new deps, LF, changed-lines format gate. New files LF. Compiled JS untouched (no UI in 1a). Do not touch lockfiles.
- **Plan-review (Codex gpt-5.5) folded:** BLOCK — `ManabaseBracketBaseline` gained a nullable `Source` (was snapshot-level only) so `row.Source` compiles; provider backfills it from `snapshot.Source`. MEDIUM — added explicit updates to `FeatureFlagCatalogTests` + `FeatureFlagStoreSeedTests` (they're hard-coded `[InlineData]`, not reflective). LOW — named the two result-construction sites (early ~:378 stays null; success ~:493 gets the block). All other checks (provider injection, `IsFlagOn` fail-safe-OFF, JSON/`IReadOnlyList` deserialize, csproj `Content Update`, byte-identical-OFF, scope) confirmed sound against the repo.
- **Open for 1b:** controller auto-classify (`IBracketClassificationService.ClassifyAsync(deckSource)` — re-loads the deck; run only when flag ON), `ManabaseRequest.Bracket` + `NormalizeKnobs` clamp to 2-5, selector (reuse `BracketTier.Number`, filter B1) + display beside Karsten (`Manabase.cshtml:432`), themes/mobile.
