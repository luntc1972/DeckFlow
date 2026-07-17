# Deck History Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. House override: Codex implements each task (foreman orchestration); Claude reviews between tasks.

**Goal:** Add a `/deck-history` tool where a deck's version history lives in a downloadable JSON file the user owns: snapshot-per-version with dates and notes, pair diffing, and an AI "how has this deck evolved" prompt artifact.

**Architecture:** Pure history logic (records, serializer, appender, diff projector) in `DeckFlow.Core/History/`. Web layer adds one controller + page service + prompt-variant trio following the existing Bracket/Primer patterns: split URL/paste deck input, `IFormFile` upload, hidden-field JSON round-trip, `X-DeckFlow-Filename` blob download, feature-flag gate seeded OFF.

**Tech Stack:** .NET 10, ASP.NET Core MVC, System.Text.Json, xUnit, Playwright.

**Spec:** `.planning/deck-history-design.md` (approved 2026-07-16).

## Global Constraints

- LF line endings everywhere (`.gitattributes` enforces). Preserve each touched file's existing endings exactly.
- Never commit compiled `wwwroot/js/*.js` — TS-only if a script is needed (this plan needs none).
- New/changed C# lines must pass the changed-lines format gate (`scripts/format-check-changed.sh staged`).
- Serializable records use `{ get; init; }` or `required` — NEVER `{ get; }` (STJ silently skips get-only props; CarveOutGuard).
- Never re-indent C# raw-string literals; prompt text uses `StringBuilder.AppendLine` like existing variants.
- Layout CSS goes in `site-common.css` using theme tokens (`--panel`, not `--theme-surface` in dark themes); never `site.css`.
- XML doc comments on every public type/member; file-scoped namespaces; one public type per file.
- No new NuGet/npm packages.
- Build/test via Windows dotnet from WSL: `"/mnt/c/Program Files/dotnet/dotnet.exe"` from the repo root (`/mnt/c/users/chrislunt/source/personal/deckflow`).
- Commit per task, Conventional Commits, no Co-Authored-By trailer (repo-local rule).
- All async methods: optional `CancellationToken cancellationToken = default` last parameter.
- Scope fence: each task lists its files. Do not touch files outside the task's list.

---

### Task 1: Core history records + serializer

**Files:**
- Create: `DeckFlow.Core/History/DeckHistoryFile.cs`
- Create: `DeckFlow.Core/History/DeckHistorySource.cs`
- Create: `DeckFlow.Core/History/DeckSnapshot.cs`
- Create: `DeckFlow.Core/History/SnapshotCard.cs`
- Create: `DeckFlow.Core/History/SnapshotDelta.cs`
- Create: `DeckFlow.Core/History/SnapshotQuantityChange.cs`
- Create: `DeckFlow.Core/History/DeckHistorySerializer.cs`
- Create: `DeckFlow.Core/History/DeckHistoryParseResult.cs`
- Test: `DeckFlow.Core.Tests/DeckHistorySerializerTests.cs`

**Interfaces:**
- Consumes: nothing (leaf task).
- Produces: the record shapes below plus `DeckHistorySerializer.Parse(string) → DeckHistoryParseResult` and `DeckHistorySerializer.Serialize(DeckHistoryFile) → string`. Constants: `DeckHistorySerializer.FormatMarker == "deckflow-history"`, `CurrentFormatVersion == "1.0"`, `CurrentMajorVersion == 1`, `MaxUploadBytes == 1_048_576`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using DeckFlow.Core.History;

namespace DeckFlow.Core.Tests;

public sealed class DeckHistorySerializerTests
{
    private static DeckHistoryFile SampleFile() => new()
    {
        DeckName = "Tivit Ad Nauseam",
        Source = new DeckHistorySource { Site = "moxfield", Url = "https://moxfield.com/decks/abc" },
        Versions =
        [
            new DeckSnapshot
            {
                Id = 1,
                Date = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                Notes = "Initial list.",
                Commander = ["Tivit, Seller of Secrets"],
                Cards = [new SnapshotCard { Name = "Sol Ring", Qty = 1 }, new SnapshotCard { Name = "Island", Qty = 8 }],
            },
        ],
    };

    [Fact]
    public void Serialize_ThenParse_RoundTripsAllFields()
    {
        var json = DeckHistorySerializer.Serialize(SampleFile());
        var result = DeckHistorySerializer.Parse(json);

        Assert.Null(result.Error);
        Assert.NotNull(result.File);
        Assert.Equal("Tivit Ad Nauseam", result.File!.DeckName);
        Assert.Equal("moxfield", result.File.Source?.Site);
        var snapshot = Assert.Single(result.File.Versions);
        Assert.Equal(1, snapshot.Id);
        Assert.Equal("Tivit, Seller of Secrets", Assert.Single(snapshot.Commander));
        Assert.Equal(2, snapshot.Cards.Count);
        Assert.Equal(8, snapshot.Cards[1].Qty);
    }

    [Fact]
    public void Serialize_UsesCamelCaseAndFormatHeader()
    {
        var json = DeckHistorySerializer.Serialize(SampleFile());

        Assert.Contains("\"format\": \"deckflow-history\"", json);
        Assert.Contains("\"formatVersion\": \"1.0\"", json);
        Assert.Contains("\"deckName\"", json);
        Assert.Contains("\"qty\"", json);
        Assert.DoesNotContain("\"Name\"", json);
    }

    [Fact]
    public void Parse_UnknownFields_ArePreservedOnReserialize()
    {
        var json = DeckHistorySerializer.Serialize(SampleFile())
            .Replace("\"deckName\"", "\"futureField\": \"keep-me\",\n  \"deckName\"");
        var parsed = DeckHistorySerializer.Parse(json);

        Assert.Null(parsed.Error);
        var rewritten = DeckHistorySerializer.Serialize(parsed.File!);
        Assert.Contains("futureField", rewritten);
        Assert.Contains("keep-me", rewritten);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsError()
    {
        var result = DeckHistorySerializer.Parse("{ not json");
        Assert.Null(result.File);
        Assert.Contains("not valid JSON", result.Error);
    }

    [Fact]
    public void Parse_WrongFormatMarker_ReturnsError()
    {
        var result = DeckHistorySerializer.Parse("{\"format\":\"something-else\",\"formatVersion\":\"1.0\"}");
        Assert.Null(result.File);
        Assert.Contains("not a DeckFlow history file", result.Error);
    }

    [Fact]
    public void Parse_NewerMajorVersion_ReturnsError()
    {
        var result = DeckHistorySerializer.Parse("{\"format\":\"deckflow-history\",\"formatVersion\":\"2.0\",\"deckName\":\"x\",\"versions\":[]}");
        Assert.Null(result.File);
        Assert.Contains("newer version of DeckFlow", result.Error);
    }

    [Fact]
    public void Parse_NewerMinorVersion_IsAccepted()
    {
        var result = DeckHistorySerializer.Parse("{\"format\":\"deckflow-history\",\"formatVersion\":\"1.7\",\"deckName\":\"x\",\"versions\":[]}");
        Assert.Null(result.Error);
        Assert.NotNull(result.File);
    }

    [Fact]
    public void Parse_BrokenIds_AreRepairedInDateOrderWithWarning()
    {
        var json = """
        {
          "format": "deckflow-history",
          "formatVersion": "1.0",
          "deckName": "x",
          "versions": [
            { "id": 9, "date": "2026-07-02T00:00:00Z", "commander": [], "cards": [] },
            { "id": 9, "date": "2026-07-01T00:00:00Z", "commander": [], "cards": [] }
          ]
        }
        """;
        var result = DeckHistorySerializer.Parse(json);

        Assert.Null(result.Error);
        Assert.Equal(1, result.File!.Versions[0].Id);
        Assert.Equal(2, result.File.Versions[1].Id);
        Assert.Equal(DateTimeOffset.Parse("2026-07-01T00:00:00Z"), result.File.Versions[0].Date);
        Assert.Contains(result.Warnings, w => w.Contains("repaired"));
    }

    [Fact]
    public void Parse_NullCollections_NormalizeToEmpty()
    {
        var json = """
        {
          "format": "deckflow-history",
          "formatVersion": "1.0",
          "deckName": "x",
          "versions": [ { "id": 1, "date": "2026-07-01T00:00:00Z", "commander": null, "cards": null } ]
        }
        """;
        var result = DeckHistorySerializer.Parse(json);

        Assert.Null(result.Error);
        Assert.Empty(result.File!.Versions[0].Commander);
        Assert.Empty(result.File.Versions[0].Cards);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter DeckHistorySerializerTests -v minimal`
Expected: FAIL — compile errors, `DeckHistoryFile` does not exist.

- [ ] **Step 3: Implement records + serializer**

`DeckFlow.Core/History/DeckHistoryFile.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckFlow.Core.History;

/// <summary>
/// Root of a user-owned deck version-history file (format "deckflow-history").
/// Snapshot-per-version: every entry in <see cref="Versions"/> carries the complete decklist.
/// </summary>
public sealed record DeckHistoryFile
{
    /// <summary>Format marker; must equal <see cref="DeckHistorySerializer.FormatMarker"/>.</summary>
    public string Format { get; init; } = DeckHistorySerializer.FormatMarker;

    /// <summary>Schema version as "major.minor". Minor bumps are additive-only.</summary>
    public string FormatVersion { get; init; } = DeckHistorySerializer.CurrentFormatVersion;

    /// <summary>Display name of the deck this history tracks.</summary>
    public string DeckName { get; init; } = string.Empty;

    /// <summary>Optional origin of the deck (site + URL).</summary>
    public DeckHistorySource? Source { get; init; }

    /// <summary>Append-ordered snapshots, oldest first.</summary>
    public IReadOnlyList<DeckSnapshot> Versions { get; init; } = [];

    /// <summary>Round-trips fields written by newer DeckFlow versions so re-saving never drops them.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
```

`DeckFlow.Core/History/DeckHistorySource.cs`:

```csharp
namespace DeckFlow.Core.History;

/// <summary>Where the tracked deck lives (e.g. site "moxfield" plus its public URL).</summary>
public sealed record DeckHistorySource
{
    /// <summary>Source site key, e.g. "moxfield" or "archidekt".</summary>
    public string? Site { get; init; }

    /// <summary>Public deck URL, when the deck was imported by URL.</summary>
    public string? Url { get; init; }
}
```

`DeckFlow.Core/History/DeckSnapshot.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckFlow.Core.History;

/// <summary>One dated, complete snapshot of the deck plus the user's note for the change.</summary>
public sealed record DeckSnapshot
{
    /// <summary>Monotonically increasing version id assigned by DeckFlow (repaired on upload if hand-edited).</summary>
    public int Id { get; init; }

    /// <summary>UTC timestamp assigned when the snapshot was appended.</summary>
    public DateTimeOffset Date { get; init; }

    /// <summary>Optional short user label, e.g. "post-ban".</summary>
    public string? Label { get; init; }

    /// <summary>Free-text note explaining why this version changed. May be hand-edited later.</summary>
    public string? Notes { get; init; }

    /// <summary>Commander card name(s).</summary>
    public IReadOnlyList<string> Commander { get; init; } = [];

    /// <summary>The authoritative full mainboard snapshot.</summary>
    public IReadOnlyList<SnapshotCard> Cards { get; init; } = [];

    /// <summary>Derived changes vs the previous version. Recomputed on every upload; never trusted from the file.</summary>
    public SnapshotDelta? Delta { get; init; }

    /// <summary>Round-trips fields written by newer DeckFlow versions.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
```

`DeckFlow.Core/History/SnapshotCard.cs`:

```csharp
namespace DeckFlow.Core.History;

/// <summary>A card name plus copy count inside a snapshot or delta.</summary>
public sealed record SnapshotCard
{
    /// <summary>The card's printed name.</summary>
    public required string Name { get; init; }

    /// <summary>Number of copies.</summary>
    public required int Qty { get; init; }
}
```

`DeckFlow.Core/History/SnapshotDelta.cs`:

```csharp
namespace DeckFlow.Core.History;

/// <summary>Derived adds/cuts/quantity changes vs the previous snapshot.</summary>
public sealed record SnapshotDelta
{
    /// <summary>Cards present in this version but not the previous one.</summary>
    public IReadOnlyList<SnapshotCard> Adds { get; init; } = [];

    /// <summary>Cards present in the previous version but not this one.</summary>
    public IReadOnlyList<SnapshotCard> Cuts { get; init; } = [];

    /// <summary>Cards in both versions whose copy count changed (basic lands, typically).</summary>
    public IReadOnlyList<SnapshotQuantityChange> QtyChanges { get; init; } = [];
}
```

`DeckFlow.Core/History/SnapshotQuantityChange.cs`:

```csharp
namespace DeckFlow.Core.History;

/// <summary>A copy-count change for a card present in both of two compared versions.</summary>
public sealed record SnapshotQuantityChange
{
    /// <summary>The card's printed name.</summary>
    public required string Name { get; init; }

    /// <summary>Copy count in the older version.</summary>
    public required int From { get; init; }

    /// <summary>Copy count in the newer version.</summary>
    public required int To { get; init; }
}
```

`DeckFlow.Core/History/DeckHistoryParseResult.cs`:

```csharp
namespace DeckFlow.Core.History;

/// <summary>
/// Outcome of parsing a history file: the normalized file on success, a user-facing
/// error on hard failure, and non-blocking repair warnings either way.
/// </summary>
public sealed record DeckHistoryParseResult(
    DeckHistoryFile? File,
    string? Error,
    IReadOnlyList<string> Warnings);
```

`DeckFlow.Core/History/DeckHistorySerializer.cs`:

```csharp
using System.Globalization;
using System.Text.Json;

namespace DeckFlow.Core.History;

/// <summary>
/// Parses and writes "deckflow-history" JSON files. Parsing is hand-edit tolerant:
/// structural damage that can be repaired (broken ids, null collections) is repaired
/// with a warning; only wrong format markers, newer major versions, and unparseable
/// JSON are hard failures.
/// </summary>
public static class DeckHistorySerializer
{
    /// <summary>Value the file's "format" property must carry.</summary>
    public const string FormatMarker = "deckflow-history";

    /// <summary>Format version written to new files.</summary>
    public const string CurrentFormatVersion = "1.0";

    /// <summary>Highest major version this build can read.</summary>
    public const int CurrentMajorVersion = 1;

    /// <summary>Upload size cap in bytes (~1 MB — hundreds of Commander versions of headroom).</summary>
    public const int MaxUploadBytes = 1_048_576;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Parses history JSON into a normalized <see cref="DeckHistoryFile"/>.</summary>
    /// <param name="json">Raw file content.</param>
    public static DeckHistoryParseResult Parse(string json)
    {
        DeckHistoryFile? file;
        try
        {
            file = JsonSerializer.Deserialize<DeckHistoryFile>(json, Options);
        }
        catch (JsonException)
        {
            return new DeckHistoryParseResult(null, "This file is not valid JSON.", []);
        }

        if (file is null || !string.Equals(file.Format, FormatMarker, StringComparison.Ordinal))
        {
            return new DeckHistoryParseResult(null, "This file is not a DeckFlow history file.", []);
        }

        var major = ParseMajor(file.FormatVersion);
        if (major is null)
        {
            return new DeckHistoryParseResult(null, "This file's formatVersion is not recognized.", []);
        }

        if (major > CurrentMajorVersion)
        {
            return new DeckHistoryParseResult(
                null, "This file was created by a newer version of DeckFlow and cannot be read here.", []);
        }

        var warnings = new List<string>();
        file = NormalizeVersions(file, warnings);
        return new DeckHistoryParseResult(file, null, warnings);
    }

    /// <summary>Writes the file as indented camelCase JSON.</summary>
    /// <param name="file">History file to serialize.</param>
    public static string Serialize(DeckHistoryFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return JsonSerializer.Serialize(file, Options);
    }

    private static int? ParseMajor(string? formatVersion)
    {
        if (string.IsNullOrWhiteSpace(formatVersion))
        {
            return null;
        }

        var dot = formatVersion.IndexOf('.', StringComparison.Ordinal);
        var majorText = dot < 0 ? formatVersion : formatVersion[..dot];
        return int.TryParse(majorText, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            ? major
            : null;
    }

    private static DeckHistoryFile NormalizeVersions(DeckHistoryFile file, List<string> warnings)
    {
        var versions = (file.Versions ?? []).Select(NormalizeSnapshot).ToList();

        var idsHealthy = versions.Count == 0
            || (versions.Select(v => v.Id).Distinct().Count() == versions.Count
                && versions.Zip(versions.Skip(1), (a, b) => a.Id < b.Id).All(ok => ok)
                && versions[0].Id > 0);

        if (!idsHealthy)
        {
            versions = versions
                .OrderBy(v => v.Date)
                .Select((v, index) => v with { Id = index + 1 })
                .ToList();
            warnings.Add("Version ids were repaired (renumbered in date order).");
        }

        return file with { Versions = versions };
    }

    private static DeckSnapshot NormalizeSnapshot(DeckSnapshot snapshot) => snapshot with
    {
        Commander = snapshot.Commander ?? [],
        Cards = snapshot.Cards ?? [],
    };
}
```

Note: the `?? []` guards look redundant against the record defaults but are required — an explicit `"commander": null` in hand-edited JSON deserializes to `null` despite the initializer.

- [ ] **Step 4: Run tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter DeckHistorySerializerTests -v minimal`
Expected: PASS (9 tests).

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Core/History/ DeckFlow.Core.Tests/DeckHistorySerializerTests.cs
git commit -m "feat(deck-history): add history file records and hand-edit-tolerant serializer"
```

---

### Task 2: VersionDiffProjector

**Files:**
- Create: `DeckFlow.Core/History/VersionDiff.cs`
- Create: `DeckFlow.Core/History/VersionDiffProjector.cs`
- Test: `DeckFlow.Core.Tests/VersionDiffProjectorTests.cs`

**Interfaces:**
- Consumes: `DeckSnapshot`, `SnapshotCard`, `SnapshotQuantityChange` (Task 1); `CardNormalizer.Normalize(string)` from `DeckFlow.Core.Normalization`.
- Produces: `VersionDiff` record and `VersionDiffProjector.Project(DeckSnapshot older, DeckSnapshot newer) → VersionDiff`.

Design note (deviation from spec wording, adopted for simplicity): the projector compares the two snapshots directly with `CardNormalizer`-keyed dictionaries instead of adapting snapshots into `DeckEntry` lists for `DiffEngine.Compare`. Snapshots are name+qty only — `DiffEngine`'s board/strict-printing machinery adds conversion code without adding signal, and `DeckDiff` splits quantity changes across two differently-shaped lists that would need reassembly anyway. Same normalized-name matching semantics.

- [ ] **Step 1: Write the failing tests**

```csharp
using DeckFlow.Core.History;

namespace DeckFlow.Core.Tests;

public sealed class VersionDiffProjectorTests
{
    private static DeckSnapshot Snapshot(string[] commander, params (string Name, int Qty)[] cards) => new()
    {
        Id = 1,
        Date = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
        Commander = commander,
        Cards = cards.Select(c => new SnapshotCard { Name = c.Name, Qty = c.Qty }).ToList(),
    };

    [Fact]
    public void Project_AddedAndCutCards_LandInAddsAndCuts()
    {
        var older = Snapshot(["Tivit, Seller of Secrets"], ("Sol Ring", 1), ("Dockside Extortionist", 1));
        var newer = Snapshot(["Tivit, Seller of Secrets"], ("Sol Ring", 1), ("Mystic Remora", 1));

        var diff = VersionDiffProjector.Project(older, newer);

        Assert.Equal("Mystic Remora", Assert.Single(diff.Adds).Name);
        Assert.Equal("Dockside Extortionist", Assert.Single(diff.Cuts).Name);
        Assert.Empty(diff.QuantityChanges);
    }

    [Fact]
    public void Project_QuantityShift_LandsInQuantityChanges()
    {
        var older = Snapshot([], ("Island", 8));
        var newer = Snapshot([], ("Island", 7));

        var diff = VersionDiffProjector.Project(older, newer);

        Assert.Empty(diff.Adds);
        Assert.Empty(diff.Cuts);
        var change = Assert.Single(diff.QuantityChanges);
        Assert.Equal("Island", change.Name);
        Assert.Equal(8, change.From);
        Assert.Equal(7, change.To);
    }

    [Fact]
    public void Project_CommanderSwap_AppearsAsAddAndCut()
    {
        var older = Snapshot(["Tivit, Seller of Secrets"], ("Sol Ring", 1));
        var newer = Snapshot(["Kraum, Ludevic's Opus"], ("Sol Ring", 1));

        var diff = VersionDiffProjector.Project(older, newer);

        Assert.Equal("Kraum, Ludevic's Opus", Assert.Single(diff.Adds).Name);
        Assert.Equal("Tivit, Seller of Secrets", Assert.Single(diff.Cuts).Name);
    }

    [Fact]
    public void Project_NameMatchingIsNormalized_NotCaseSensitive()
    {
        var older = Snapshot([], ("Sol Ring", 1));
        var newer = Snapshot([], ("sol ring", 1));

        var diff = VersionDiffProjector.Project(older, newer);

        Assert.Empty(diff.Adds);
        Assert.Empty(diff.Cuts);
        Assert.Empty(diff.QuantityChanges);
    }

    [Fact]
    public void Project_ResultsAreAlphabetical()
    {
        var older = Snapshot([], ("Zealous Conscripts", 1), ("Arcane Signet", 1));
        var newer = Snapshot([], ("Brainstorm", 1), ("Abrade", 1));

        var diff = VersionDiffProjector.Project(older, newer);

        Assert.Equal(["Abrade", "Brainstorm"], diff.Adds.Select(a => a.Name).ToArray());
        Assert.Equal(["Arcane Signet", "Zealous Conscripts"], diff.Cuts.Select(c => c.Name).ToArray());
    }

    [Fact]
    public void Project_IdenticalSnapshots_ReturnEmptyDiff()
    {
        var snapshot = Snapshot(["Tivit, Seller of Secrets"], ("Sol Ring", 1), ("Island", 8));

        var diff = VersionDiffProjector.Project(snapshot, snapshot);

        Assert.Empty(diff.Adds);
        Assert.Empty(diff.Cuts);
        Assert.Empty(diff.QuantityChanges);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter VersionDiffProjectorTests -v minimal`
Expected: FAIL — `VersionDiffProjector` does not exist.

- [ ] **Step 3: Implement**

`DeckFlow.Core/History/VersionDiff.cs`:

```csharp
namespace DeckFlow.Core.History;

/// <summary>Adds/cuts/quantity changes between two snapshots, oldest → newest.</summary>
public sealed record VersionDiff(
    IReadOnlyList<SnapshotCard> Adds,
    IReadOnlyList<SnapshotCard> Cuts,
    IReadOnlyList<SnapshotQuantityChange> QuantityChanges)
{
    /// <summary>A diff with no changes.</summary>
    public static readonly VersionDiff Empty = new([], [], []);
}
```

`DeckFlow.Core/History/VersionDiffProjector.cs`:

```csharp
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.History;

/// <summary>
/// Computes the change set between two snapshots by normalized card name.
/// Commander entries participate as one copy each, so commander swaps show as add + cut.
/// </summary>
public static class VersionDiffProjector
{
    /// <summary>Projects the changes from <paramref name="older"/> to <paramref name="newer"/>.</summary>
    /// <param name="older">The chronologically earlier snapshot.</param>
    /// <param name="newer">The chronologically later snapshot.</param>
    public static VersionDiff Project(DeckSnapshot older, DeckSnapshot newer)
    {
        ArgumentNullException.ThrowIfNull(older);
        ArgumentNullException.ThrowIfNull(newer);

        var olderMap = BuildMap(older);
        var newerMap = BuildMap(newer);

        var adds = new List<SnapshotCard>();
        var cuts = new List<SnapshotCard>();
        var qtyChanges = new List<SnapshotQuantityChange>();

        foreach (var (key, entry) in newerMap)
        {
            if (!olderMap.TryGetValue(key, out var previous))
            {
                adds.Add(new SnapshotCard { Name = entry.Name, Qty = entry.Qty });
            }
            else if (previous.Qty != entry.Qty)
            {
                qtyChanges.Add(new SnapshotQuantityChange { Name = entry.Name, From = previous.Qty, To = entry.Qty });
            }
        }

        foreach (var (key, entry) in olderMap)
        {
            if (!newerMap.ContainsKey(key))
            {
                cuts.Add(new SnapshotCard { Name = entry.Name, Qty = entry.Qty });
            }
        }

        return new VersionDiff(
            adds.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            cuts.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            qtyChanges.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static Dictionary<string, (string Name, int Qty)> BuildMap(DeckSnapshot snapshot)
    {
        var map = new Dictionary<string, (string Name, int Qty)>(StringComparer.Ordinal);

        foreach (var name in snapshot.Commander)
        {
            Accumulate(map, name, 1);
        }

        foreach (var card in snapshot.Cards)
        {
            Accumulate(map, card.Name, card.Qty);
        }

        return map;
    }

    private static void Accumulate(Dictionary<string, (string Name, int Qty)> map, string name, int qty)
    {
        var key = CardNormalizer.Normalize(name);
        map[key] = map.TryGetValue(key, out var existing)
            ? (existing.Name, existing.Qty + qty)
            : (name, qty);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter VersionDiffProjectorTests -v minimal`
Expected: PASS (6 tests).

- [ ] **Step 5: Update the spec's projector line and commit**

In `.planning/deck-history-design.md`, replace the `VersionDiffProjector` table row description with: "Compares two snapshots directly via `CardNormalizer`-keyed maps and returns `VersionDiff(Adds, Cuts, QuantityChanges)`. (Direct comparison replaced the originally sketched `DiffEngine` adaptation — snapshots are name+qty only, so board/printing machinery added conversion cost without signal.)"

```bash
git add DeckFlow.Core/History/VersionDiff.cs DeckFlow.Core/History/VersionDiffProjector.cs DeckFlow.Core.Tests/VersionDiffProjectorTests.cs .planning/deck-history-design.md
git commit -m "feat(deck-history): add snapshot pair diff projector"
```

---

### Task 3: DeckHistoryAppender

**Files:**
- Create: `DeckFlow.Core/History/DeckHistoryAppender.cs`
- Create: `DeckFlow.Core/History/DeckHistoryAppendResult.cs`
- Test: `DeckFlow.Core.Tests/DeckHistoryAppenderTests.cs`

**Interfaces:**
- Consumes: Task 1 records; `VersionDiffProjector.Project` (Task 2); `DeckEntry` from `DeckFlow.Core.Models`; `CardNormalizer.Normalize`.
- Produces:
  - `DeckHistoryAppender.CreateNew(string deckName, DeckHistorySource? source) → DeckHistoryFile`
  - `DeckHistoryAppender.BuildSnapshot(IReadOnlyList<DeckEntry> entries, string? notes, string? label, DateTimeOffset dateUtc) → DeckSnapshot` (Id = 0 placeholder; Append assigns)
  - `DeckHistoryAppender.Append(DeckHistoryFile file, DeckSnapshot candidate) → DeckHistoryAppendResult`
  - `DeckHistoryAppender.RecomputeDeltas(DeckHistoryFile file) → DeckHistoryFile`
  - `DeckHistoryAppendResult(DeckHistoryFile File, bool Appended, string? Warning)`

- [ ] **Step 1: Write the failing tests**

```csharp
using DeckFlow.Core.History;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests;

public sealed class DeckHistoryAppenderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-16T12:00:00Z");

    private static DeckEntry Entry(string name, int qty, string board = "mainboard") => new()
    {
        Name = name,
        NormalizedName = name.ToLowerInvariant(),
        Quantity = qty,
        Board = board,
    };

    [Fact]
    public void BuildSnapshot_SplitsCommanderFromMainboardAndDropsMaybeboard()
    {
        var entries = new[]
        {
            Entry("Tivit, Seller of Secrets", 1, "commander"),
            Entry("Sol Ring", 1),
            Entry("Rhystic Study", 1, "maybeboard"),
        };

        var snapshot = DeckHistoryAppender.BuildSnapshot(entries, "note", "label", Now);

        Assert.Equal("Tivit, Seller of Secrets", Assert.Single(snapshot.Commander));
        Assert.Equal("Sol Ring", Assert.Single(snapshot.Cards).Name);
        Assert.Equal("note", snapshot.Notes);
        Assert.Equal("label", snapshot.Label);
        Assert.Equal(Now, snapshot.Date);
    }

    [Fact]
    public void Append_ToNewFile_AssignsIdOneAndEmptyDelta()
    {
        var file = DeckHistoryAppender.CreateNew("My Deck", null);
        var snapshot = DeckHistoryAppender.BuildSnapshot([Entry("Sol Ring", 1)], null, null, Now);

        var result = DeckHistoryAppender.Append(file, snapshot);

        Assert.True(result.Appended);
        var appended = Assert.Single(result.File.Versions);
        Assert.Equal(1, appended.Id);
        Assert.NotNull(appended.Delta);
        Assert.Empty(appended.Delta!.Adds);
        Assert.Empty(appended.Delta.Cuts);
    }

    [Fact]
    public void Append_SecondVersion_GetsNextIdAndComputedDelta()
    {
        var file = DeckHistoryAppender.CreateNew("My Deck", null);
        file = DeckHistoryAppender.Append(
            file, DeckHistoryAppender.BuildSnapshot([Entry("Sol Ring", 1)], null, null, Now)).File;

        var second = DeckHistoryAppender.BuildSnapshot(
            [Entry("Sol Ring", 1), Entry("Mystic Remora", 1)], "added remora", null, Now.AddDays(1));
        var result = DeckHistoryAppender.Append(file, second);

        Assert.True(result.Appended);
        Assert.Equal(2, result.File.Versions.Count);
        Assert.Equal(2, result.File.Versions[1].Id);
        Assert.Equal("Mystic Remora", Assert.Single(result.File.Versions[1].Delta!.Adds).Name);
    }

    [Fact]
    public void Append_IdenticalDeck_DoesNotAppendAndWarns()
    {
        var file = DeckHistoryAppender.CreateNew("My Deck", null);
        file = DeckHistoryAppender.Append(
            file, DeckHistoryAppender.BuildSnapshot([Entry("Sol Ring", 1)], null, null, Now)).File;

        var duplicate = DeckHistoryAppender.BuildSnapshot([Entry("Sol Ring", 1)], "same", null, Now.AddDays(1));
        var result = DeckHistoryAppender.Append(file, duplicate);

        Assert.False(result.Appended);
        Assert.Single(result.File.Versions);
        Assert.Contains("identical", result.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecomputeDeltas_OverwritesHandEditedDeltas()
    {
        var tampered = new DeckHistoryFile
        {
            DeckName = "x",
            Versions =
            [
                new DeckSnapshot { Id = 1, Date = Now, Cards = [new SnapshotCard { Name = "Sol Ring", Qty = 1 }] },
                new DeckSnapshot
                {
                    Id = 2,
                    Date = Now.AddDays(1),
                    Cards = [new SnapshotCard { Name = "Mystic Remora", Qty = 1 }],
                    Delta = new SnapshotDelta { Adds = [new SnapshotCard { Name = "FAKE CARD", Qty = 9 }] },
                },
            ],
        };

        var recomputed = DeckHistoryAppender.RecomputeDeltas(tampered);

        Assert.Equal("Mystic Remora", Assert.Single(recomputed.Versions[1].Delta!.Adds).Name);
        Assert.Equal("Sol Ring", Assert.Single(recomputed.Versions[1].Delta!.Cuts).Name);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter DeckHistoryAppenderTests -v minimal`
Expected: FAIL — `DeckHistoryAppender` does not exist.

- [ ] **Step 3: Implement**

`DeckFlow.Core/History/DeckHistoryAppendResult.cs`:

```csharp
namespace DeckFlow.Core.History;

/// <summary>Result of attempting to append a snapshot: the (possibly unchanged) file plus outcome.</summary>
public sealed record DeckHistoryAppendResult(DeckHistoryFile File, bool Appended, string? Warning);
```

`DeckFlow.Core/History/DeckHistoryAppender.cs`:

```csharp
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.History;

/// <summary>
/// Builds snapshots from loaded deck entries and appends them to a history file.
/// Deltas are always recomputed from the snapshots themselves — the file's stored
/// deltas are a convenience for human readers and are never trusted.
/// </summary>
public static class DeckHistoryAppender
{
    /// <summary>Creates an empty history file for a deck.</summary>
    /// <param name="deckName">Display name for the tracked deck.</param>
    /// <param name="source">Optional deck origin.</param>
    public static DeckHistoryFile CreateNew(string deckName, DeckHistorySource? source) => new()
    {
        DeckName = deckName,
        Source = source,
    };

    /// <summary>
    /// Converts loaded deck entries into a snapshot. Commander-board entries become
    /// <see cref="DeckSnapshot.Commander"/>; mainboard entries become cards; maybeboard
    /// and sideboard entries are dropped. Id is 0 until <see cref="Append"/> assigns it.
    /// </summary>
    /// <param name="entries">Loaded deck entries.</param>
    /// <param name="notes">User note explaining the change.</param>
    /// <param name="label">Optional short label.</param>
    /// <param name="dateUtc">Timestamp to stamp on the snapshot.</param>
    public static DeckSnapshot BuildSnapshot(
        IReadOnlyList<DeckEntry> entries, string? notes, string? label, DateTimeOffset dateUtc)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var commander = entries
            .Where(e => string.Equals(e.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var cards = entries
            .Where(e => string.Equals(e.Board, "mainboard", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => CardNormalizer.Normalize(e.Name), StringComparer.Ordinal)
            .Select(group => new SnapshotCard { Name = group.First().Name, Qty = group.Sum(e => e.Quantity) })
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DeckSnapshot
        {
            Date = dateUtc,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Commander = commander,
            Cards = cards,
        };
    }

    /// <summary>
    /// Appends the candidate snapshot unless it is identical to the latest version.
    /// Assigns the next id and recomputes every delta.
    /// </summary>
    /// <param name="file">History file to append to.</param>
    /// <param name="candidate">Snapshot built by <see cref="BuildSnapshot"/>.</param>
    public static DeckHistoryAppendResult Append(DeckHistoryFile file, DeckSnapshot candidate)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(candidate);

        var latest = file.Versions.Count > 0 ? file.Versions[^1] : null;
        if (latest is not null && VersionDiffProjector.Project(latest, candidate) == VersionDiff.Empty)
        {
            return new DeckHistoryAppendResult(
                file, false, "The imported deck is identical to the latest version — no new snapshot was added.");
        }

        var nextId = latest is null ? 1 : latest.Id + 1;
        var versions = file.Versions.Append(candidate with { Id = nextId }).ToList();
        var updated = RecomputeDeltas(file with { Versions = versions });
        return new DeckHistoryAppendResult(updated, true, null);
    }

    /// <summary>Recomputes every version's delta from the snapshots (first version gets an empty delta).</summary>
    /// <param name="file">History file to refresh.</param>
    public static DeckHistoryFile RecomputeDeltas(DeckHistoryFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var versions = new List<DeckSnapshot>(file.Versions.Count);
        for (var i = 0; i < file.Versions.Count; i++)
        {
            var delta = i == 0
                ? new SnapshotDelta()
                : ToDelta(VersionDiffProjector.Project(file.Versions[i - 1], file.Versions[i]));
            versions.Add(file.Versions[i] with { Delta = delta });
        }

        return file with { Versions = versions };
    }

    private static SnapshotDelta ToDelta(VersionDiff diff) => new()
    {
        Adds = diff.Adds,
        Cuts = diff.Cuts,
        QtyChanges = diff.QuantityChanges,
    };
}
```

Note: `VersionDiffProjector.Project(latest, candidate) == VersionDiff.Empty` relies on record equality — `VersionDiff` holds `IReadOnlyList` references, so record equality is reference-based and this comparison is WRONG. Implement identity as an explicit check instead:

```csharp
    private static bool IsIdentical(VersionDiff diff) =>
        diff.Adds.Count == 0 && diff.Cuts.Count == 0 && diff.QuantityChanges.Count == 0;
```

and use `if (latest is not null && IsIdentical(VersionDiffProjector.Project(latest, candidate)))`. The test in Step 1 catches this if forgotten.

- [ ] **Step 4: Run tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter DeckHistoryAppenderTests -v minimal`
Expected: PASS (5 tests).

- [ ] **Step 5: Run the full Core suite and commit**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -v minimal`
Expected: PASS, no regressions.

```bash
git add DeckFlow.Core/History/DeckHistoryAppender.cs DeckFlow.Core/History/DeckHistoryAppendResult.cs DeckFlow.Core.Tests/DeckHistoryAppenderTests.cs
git commit -m "feat(deck-history): add snapshot builder and append logic"
```

---

### Task 4: Evolution prompt variants + registry

**Files:**
- Create: `DeckFlow.Web/Services/PromptBuilders/Evolution/IEvolutionPromptVariant.cs`
- Create: `DeckFlow.Web/Services/PromptBuilders/Evolution/EvolutionPromptVariantRegistry.cs`
- Create: `DeckFlow.Web/Services/PromptBuilders/Evolution/EvolutionHistoryRenderer.cs`
- Create: `DeckFlow.Web/Services/PromptBuilders/Evolution/ChatGptEvolutionPromptVariant.cs`
- Create: `DeckFlow.Web/Services/PromptBuilders/Evolution/ClaudeEvolutionPromptVariant.cs`
- Create: `DeckFlow.Web/Services/PromptBuilders/Evolution/GeminiEvolutionPromptVariant.cs`
- Test: `DeckFlow.Web.Tests/EvolutionPromptVariantTests.cs`

**Interfaces:**
- Consumes: `DeckHistoryFile`, `DeckSnapshot`, `SnapshotDelta` (Task 1); `AiPlatform` (`DeckFlow.Web/Models/AiPlatform.cs` — class with `Normalize(string?)`, `Default`, and a `Key`; mirror `IPrimerPromptVariant`'s `AiPlatform Platform { get; }` usage exactly).
- Produces: `IEvolutionPromptVariant { AiPlatform Platform; string Build(DeckHistoryFile history, CancellationToken ct = default); }`, `EvolutionPromptVariantRegistry.Build(AiPlatform platform, DeckHistoryFile history, CancellationToken ct = default) → string`, `EvolutionHistoryRenderer.RenderHistoryBody(DeckHistoryFile) → string`.

Prompt shape (all three variants share the rendered history body; the framing text around it differs per platform — the ADR-0001 decoupling applies to the platform framing, which is what drifts, so each variant hand-writes its own framing but reuses the mechanical history rendering):

- Header: deck name, commander(s), version count, first→latest date span.
- `VERSION 1 (<date>) — FULL LIST:` plain `Nx Name` lines (commander lines first, prefixed `Commander:`).
- Each middle version: `VERSION k (<date>)<, label>` + `Notes:` + `Adds:` / `Cuts:` / `Qty:` lines from its delta.
- Latest version: `LATEST — VERSION n (<date>) — FULL LIST:` plain lines.
- Then platform-specific analysis instructions (EXECUTE NOW style for ChatGPT): trajectory of the deck's plan, what the notes say vs what the changes did, meta adaptation, consistency drift, and 3–5 forward suggestions grounded ONLY in cards seen in the history.

- [ ] **Step 1: Write the failing tests**

```csharp
using DeckFlow.Core.History;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Evolution;

namespace DeckFlow.Web.Tests;

public sealed class EvolutionPromptVariantTests
{
    private static DeckHistoryFile History() => new()
    {
        DeckName = "Tivit Ad Nauseam",
        Versions =
        [
            new DeckSnapshot
            {
                Id = 1,
                Date = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
                Commander = ["Tivit, Seller of Secrets"],
                Cards = [new SnapshotCard { Name = "Sol Ring", Qty = 1 }],
                Delta = new SnapshotDelta(),
            },
            new DeckSnapshot
            {
                Id = 2,
                Date = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                Notes = "Cut nothing, added Remora.",
                Commander = ["Tivit, Seller of Secrets"],
                Cards = [new SnapshotCard { Name = "Sol Ring", Qty = 1 }, new SnapshotCard { Name = "Mystic Remora", Qty = 1 }],
                Delta = new SnapshotDelta { Adds = [new SnapshotCard { Name = "Mystic Remora", Qty = 1 }] },
            },
        ],
    };

    [Theory]
    [InlineData("chatgpt")]
    [InlineData("claude")]
    [InlineData("gemini")]
    public void Build_ContainsHeaderTimelineAndBothFullLists(string platformKey)
    {
        var registry = new EvolutionPromptVariantRegistry(
        [
            new ChatGptEvolutionPromptVariant(),
            new ClaudeEvolutionPromptVariant(),
            new GeminiEvolutionPromptVariant(),
        ]);

        var prompt = registry.Build(AiPlatform.Normalize(platformKey), History());

        Assert.Contains("Tivit Ad Nauseam", prompt);
        Assert.Contains("VERSION 1", prompt);
        Assert.Contains("LATEST — VERSION 2", prompt);
        Assert.Contains("Mystic Remora", prompt);
        Assert.Contains("Cut nothing, added Remora.", prompt);
        Assert.Contains("Commander: Tivit, Seller of Secrets", prompt);
    }

    [Fact]
    public void Build_ChatGptVariant_CarriesExecuteNowFraming()
    {
        var prompt = new ChatGptEvolutionPromptVariant().Build(History());
        Assert.Contains("EXECUTE NOW", prompt);
    }

    [Fact]
    public void RenderHistoryBody_SingleVersion_HasOnlyOneFullList()
    {
        var single = History() with { Versions = [History().Versions[0]] };
        var body = EvolutionHistoryRenderer.RenderHistoryBody(single);

        Assert.Contains("VERSION 1", body);
        Assert.DoesNotContain("LATEST", body);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter EvolutionPromptVariantTests -v minimal`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Implement**

`IEvolutionPromptVariant.cs`:

```csharp
using DeckFlow.Core.History;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Evolution;

/// <summary>Strategy interface for building a deck-evolution prompt targeting a specific AI platform.</summary>
internal interface IEvolutionPromptVariant
{
    /// <summary>The AI platform this variant targets.</summary>
    AiPlatform Platform { get; }

    /// <summary>Builds the deck-evolution prompt for the supplied history.</summary>
    /// <param name="history">Parsed, delta-recomputed history file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    string Build(DeckHistoryFile history, CancellationToken cancellationToken = default);
}
```

`EvolutionPromptVariantRegistry.cs` — mirror `PrimerPromptVariantRegistry` exactly (dictionary keyed on `v.Platform`, `AiPlatform.Default` fallback), signature `Build(AiPlatform platform, DeckHistoryFile history, CancellationToken cancellationToken = default)`.

`EvolutionHistoryRenderer.cs`:

```csharp
using System.Text;
using DeckFlow.Core.History;

namespace DeckFlow.Web.Services.PromptBuilders.Evolution;

/// <summary>
/// Renders the mechanical history body shared by every platform variant: header line,
/// first version as a full list, middle versions as delta + notes, latest as a full list.
/// Plain text, never raw JSON (token efficiency).
/// </summary>
internal static class EvolutionHistoryRenderer
{
    /// <summary>Renders the deck's version history as a plain-text block.</summary>
    /// <param name="history">History file to render.</param>
    public static string RenderHistoryBody(DeckHistoryFile history)
    {
        var builder = new StringBuilder();
        var versions = history.Versions;
        builder.AppendLine($"Deck: {history.DeckName}");
        if (versions.Count > 0 && versions[0].Commander.Count > 0)
        {
            builder.AppendLine($"Commander: {string.Join(" / ", versions[^1].Commander)}");
        }

        builder.AppendLine($"Versions: {versions.Count} ({FormatDate(versions[0].Date)} to {FormatDate(versions[^1].Date)})");
        builder.AppendLine();

        AppendFullList(builder, versions[0], isLatest: false);

        for (var i = 1; i < versions.Count - 1; i++)
        {
            AppendDeltaVersion(builder, versions[i]);
        }

        if (versions.Count > 1)
        {
            AppendDeltaSummaryHeader(builder, versions[^1]);
            AppendFullList(builder, versions[^1], isLatest: true);
        }

        return builder.ToString();
    }

    private static void AppendFullList(StringBuilder builder, DeckSnapshot snapshot, bool isLatest)
    {
        var heading = isLatest
            ? $"LATEST — VERSION {snapshot.Id} ({FormatDate(snapshot.Date)}) — FULL LIST:"
            : $"VERSION {snapshot.Id} ({FormatDate(snapshot.Date)}) — FULL LIST:";
        builder.AppendLine(heading);
        foreach (var name in snapshot.Commander)
        {
            builder.AppendLine($"Commander: {name}");
        }

        foreach (var card in snapshot.Cards)
        {
            builder.AppendLine($"{card.Qty}x {card.Name}");
        }

        builder.AppendLine();
    }

    private static void AppendDeltaVersion(StringBuilder builder, DeckSnapshot snapshot)
    {
        AppendDeltaSummaryHeader(builder, snapshot);
        builder.AppendLine();
    }

    private static void AppendDeltaSummaryHeader(StringBuilder builder, DeckSnapshot snapshot)
    {
        var label = string.IsNullOrEmpty(snapshot.Label) ? string.Empty : $", {snapshot.Label}";
        builder.AppendLine($"VERSION {snapshot.Id} ({FormatDate(snapshot.Date)}{label})");
        if (!string.IsNullOrEmpty(snapshot.Notes))
        {
            builder.AppendLine($"Notes: {snapshot.Notes}");
        }

        var delta = snapshot.Delta;
        if (delta is null)
        {
            return;
        }

        if (delta.Adds.Count > 0)
        {
            builder.AppendLine($"Adds: {string.Join(", ", delta.Adds.Select(c => c.Qty > 1 ? $"{c.Qty}x {c.Name}" : c.Name))}");
        }

        if (delta.Cuts.Count > 0)
        {
            builder.AppendLine($"Cuts: {string.Join(", ", delta.Cuts.Select(c => c.Qty > 1 ? $"{c.Qty}x {c.Name}" : c.Name))}");
        }

        if (delta.QtyChanges.Count > 0)
        {
            builder.AppendLine($"Qty: {string.Join(", ", delta.QtyChanges.Select(c => $"{c.Name} {c.From}→{c.To}"))}");
        }
    }

    private static string FormatDate(DateTimeOffset date) => date.ToString("yyyy-MM-dd");
}
```

`ChatGptEvolutionPromptVariant.cs` (Claude/Gemini variants: same structure, own hand-written framing — Claude framing drops the anti-refusal block and uses "Analyze the following deck history"; Gemini framing mirrors the house Gemini tone from `GeminiPrimerPromptVariant`; check that file and match its opening/closing conventions):

```csharp
using System.Text;
using DeckFlow.Core.History;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.PromptBuilders.Evolution;

/// <summary>ChatGPT-targeted deck-evolution prompt.</summary>
internal sealed class ChatGptEvolutionPromptVariant : IEvolutionPromptVariant
{
    /// <inheritdoc />
    public AiPlatform Platform => AiPlatform.ChatGpt;

    /// <inheritdoc />
    public string Build(DeckHistoryFile history, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are an expert Magic: The Gathering Commander deck analyst.");
        builder.AppendLine("EXECUTE NOW: analyze how this deck has evolved across the versions below. Do not ask clarifying questions; work with exactly what is provided.");
        builder.AppendLine();
        builder.AppendLine(EvolutionHistoryRenderer.RenderHistoryBody(history));
        builder.AppendLine("Deliver, in order:");
        builder.AppendLine("1. TRAJECTORY — what the deck's game plan was in version 1 and what it is now, in two sentences each.");
        builder.AppendLine("2. CHANGE ANALYSIS — for each version, whether the notes' stated intent matches what the adds/cuts actually did.");
        builder.AppendLine("3. DRIFT CHECK — cards or packages that no longer fit the current plan.");
        builder.AppendLine("4. NEXT MOVES — 3 to 5 concrete suggestions grounded only in the cards and directions visible in this history. Never invent card names.");
        return builder.ToString();
    }
}
```

Check `AiPlatform.cs` for the exact static instance names (`AiPlatform.ChatGpt` / `.Claude` / `.Gemini` — if the class exposes them differently, e.g. via `Normalize("chatgpt")`, use whatever `ChatGptPrimerPromptVariant` uses for its `Platform` property; copy that pattern verbatim).

- [ ] **Step 4: Run tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter EvolutionPromptVariantTests -v minimal`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Services/PromptBuilders/Evolution/ DeckFlow.Web.Tests/EvolutionPromptVariantTests.cs
git commit -m "feat(deck-history): add evolution prompt variants for ChatGPT, Claude, Gemini"
```

---

### Task 5: Request model + page service + DI

**Files:**
- Create: `DeckFlow.Web/Models/DeckHistoryRequest.cs`
- Create: `DeckFlow.Web/Services/DeckHistoryPageService.cs` (interface + impl + result record co-located, house style)
- Modify: `DeckFlow.Web/Program.cs` (DI block near the other scoped page services)
- Test: `DeckFlow.Web.Tests/DeckHistoryPageServiceTests.cs`

**Interfaces:**
- Consumes: Tasks 1–4; `IDeckEntryLoader.LoadFromSourceAsync(string, UnrecognizedPasteBehavior, CancellationToken)` + `ValidateCommanderDeckSize`; `DeckInputReconciler.Reconcile`; `AiPlatform.Normalize`.
- Produces:

```csharp
public interface IDeckHistoryPageService
{
    Task<DeckHistoryProcessResult> ProcessAsync(
        DeckHistoryRequest request, string? uploadedHistoryJson, CancellationToken cancellationToken = default);
}

public sealed record DeckHistoryProcessResult
{
    public DeckHistoryFile? File { get; init; }
    public string? SerializedJson { get; init; }
    public bool Appended { get; init; }
    public int? PairOlderId { get; init; }
    public int? PairNewerId { get; init; }
    public VersionDiff? PairDiff { get; init; }
    public string PromptText { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string? ErrorMessage { get; init; }
}
```

`DeckHistoryRequest` (mirror `BracketRequest` field naming so `deck-input-store.js` and the split-field toggle bind without changes):

```csharp
public sealed class DeckHistoryRequest
{
    public DeckInputSource DeckInputSource { get; set; } = DeckInputSource.PublicUrl;
    public string DeckUrl { get; set; } = string.Empty;
    public string DeckText { get; set; } = string.Empty;
    public string DeckName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string TargetAiPlatform { get; set; } = string.Empty;
    public string HistoryJson { get; set; } = string.Empty;   // hidden round-trip field
    public int? OlderVersionId { get; set; }
    public int? NewerVersionId { get; set; }
    public string DeckSource =>
        DeckInputSource == DeckInputSource.PublicUrl ? DeckUrl : DeckText;
}
```

**Service behavior (implement exactly):**

1. Resolve history JSON: `uploadedHistoryJson` (file upload wins) else `request.HistoryJson` else none.
2. If history JSON present: `DeckHistorySerializer.Parse`. Hard error → result with `ErrorMessage`, stop. Collect warnings.
3. If `request.DeckSource` non-blank: `LoadFromSourceAsync(request.DeckSource, ..., ct)`, then `ValidateCommanderDeckSize("Deck History", entries)`. Catch `DeckParseException` / `InvalidOperationException` → `ErrorMessage` (exception message), stop. Catch `HttpRequestException` → `UpstreamErrorMessageBuilder`-style copy, stop.
4. Cases:
   - deck + no history → `CreateNew(deckName, source)` then `Append`. Deck name: `request.DeckName` if non-blank else `"Commander Deck"`. Source: `new DeckHistorySource { Site = null, Url = request.DeckInputSource == DeckInputSource.PublicUrl ? request.DeckUrl : null }`.
   - deck + history → `BuildSnapshot` + `Append` (identical-deck warning flows into `Warnings`, `Appended=false`).
   - history only → `RecomputeDeltas` (inspect mode).
   - neither → `ErrorMessage = "Upload a history file, import a deck, or both."`.
5. Timestamp: `DateTimeOffset.UtcNow` captured once in the service via an injected `Func<DateTimeOffset>`? No — keep simple: internal test-seam constructor takes `Func<DateTimeOffset> nowUtc` (house test-seam pattern), public DI ctor defaults it to `() => DateTimeOffset.UtcNow`.
6. Pair diff: valid ids from request (both found in versions, older < newer) else default latest vs previous (requires ≥2 versions). Set `PairOlderId`/`PairNewerId`/`PairDiff` accordingly; all null when <2 versions.
7. Prompt: `EvolutionPromptVariantRegistry.Build(AiPlatform.Normalize(request.TargetAiPlatform), file)` — empty string when file null or 0 versions.
8. `SerializedJson = DeckHistorySerializer.Serialize(file)` when file non-null.

**Tests to write** (`DeckHistoryPageServiceTests`, use the internal test-seam ctor + a fake `IDeckEntryLoader` — follow the existing `Fake*` conventions in `DeckFlow.Web.Tests`):
- deck only → new file with one version, `Appended=true`, prompt non-empty.
- history only → inspect: file returned, `Appended=false`, no error.
- history + identical deck → `Appended=false`, warning contains "identical".
- neither → `ErrorMessage` set.
- corrupted history JSON → `ErrorMessage` contains "not a DeckFlow history file".
- pair selection: explicit valid ids honored; invalid ids fall back to latest-vs-previous.
- deck loader throwing `InvalidOperationException("Deck History deck must contain exactly 100 cards...")` surfaces as `ErrorMessage`.

- [ ] **Step 1: Write the failing tests** (per list above; construct fake loader returning canned `DeckSourceLoadResult`)
- [ ] **Step 2: Run** `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter DeckHistoryPageServiceTests -v minimal` — Expected: FAIL.
- [ ] **Step 3: Implement service + register in `Program.cs`:**

```csharp
builder.Services.AddScoped<IDeckHistoryPageService, DeckHistoryPageService>();
builder.Services.AddSingleton<IEvolutionPromptVariant, ChatGptEvolutionPromptVariant>();
builder.Services.AddSingleton<IEvolutionPromptVariant, ClaudeEvolutionPromptVariant>();
builder.Services.AddSingleton<IEvolutionPromptVariant, GeminiEvolutionPromptVariant>();
builder.Services.AddSingleton<EvolutionPromptVariantRegistry>();
```

(Place beside the existing prompt-variant registrations — search `Program.cs` for `PrimerPromptVariant` and mirror the exact registration style used there, including any `internal` DI helper.)

- [ ] **Step 4: Run tests to verify they pass** — same filter, Expected: PASS.
- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Models/DeckHistoryRequest.cs DeckFlow.Web/Services/DeckHistoryPageService.cs DeckFlow.Web/Program.cs DeckFlow.Web.Tests/DeckHistoryPageServiceTests.cs
git commit -m "feat(deck-history): add page service orchestrating parse, append, diff, and prompt"
```

---

### Task 6: Tool wiring — enum, flag, registry, SEO, help, README

**Files:**
- Modify: `DeckFlow.Web/Models/DeckPageTab.cs` (add `DeckHistory = 16`)
- Modify: `DeckFlow.Web/Services/Tools/ToolRegistry.cs` (one `Create(...)` entry)
- Modify: `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (description entry)
- Modify: `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (both dialect seed lists, near lines 224 and 272 — seed `FALSE`/`0`)
- Modify: `DeckFlow.Web/Seo/SeoPaths.cs` (`Indexable` + `Tools`)
- Create: `DeckFlow.Web/Help/deck-history.md`
- Modify: `README.md` (feature bullet in the tools section)

**Interfaces:**
- Consumes: `DeckPageTab`, tool/flag/SEO registration surfaces.
- Produces: `DeckPageTab.DeckHistory`, flag key `"tool.deck-history.enabled"` (seeded OFF), route `/deck-history` registered in nav/SEO.

- [ ] **Step 1: Add wiring entries**

`DeckPageTab.cs` — append:

```csharp
    /// <summary>Deck version-history tracking page.</summary>
    DeckHistory = 16,
```

`ToolRegistry.cs` — add to the `Build` section after the `convert` entry:

```csharp
        Create("deck-history", "Deck History", "/deck-history", ToolNavSection.Build, "tool.deck-history.enabled", false, "Deck History", "Track your deck's evolution in a file you own — snapshot each change with a note, diff any two versions, and generate an AI prompt about how the deck has grown.", "deck-history", DeckPageTab.DeckHistory, false, "/deck-history/download"),
```

`FeatureFlagCatalog.cs` — add beside the other `tool.*` entries (match the neighboring entries' exact description formatting):

```csharp
            ["tool.deck-history.enabled"] =
                "Deck History tool: version a deck into a downloadable snapshot-history JSON file with notes, pair diffs, and an evolution prompt.",
```

`FeatureFlagStore.cs` — append `('tool.deck-history.enabled', FALSE),` to the Postgres seed list and `('tool.deck-history.enabled', 0),` to the SQLite seed list (keep each list's trailing-comma/ordering conventions).

`SeoPaths.cs` — add `"/deck-history",` to `Indexable` (after `"/bracket",`) and to `Tools`.

`Help/deck-history.md` — write a help topic following `Help/bracket.md`'s structure (what the tool does, the file-you-own model, how to append/inspect/diff, hand-edit tolerance, the AI prompt). The csproj `Help\**\*.md` glob picks it up automatically.

`README.md` — one bullet in the tools list describing Deck History (mirror neighboring bullets' style).

- [ ] **Step 2: Run the existing guard tests**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FeatureFlagCatalogTests|SeoPathsTests|ToolRegistryTests" -v minimal`
Expected: PASS — these suites enforce catalog/seed pairing and SEO list consistency; fix any assertion the new entries trip (e.g. expected-count assertions) by updating the test's expected values in the same commit.

- [ ] **Step 3: Build the full solution**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -v minimal`
Expected: clean, no new warnings.

- [ ] **Step 4: Commit**

```bash
git add DeckFlow.Web/Models/DeckPageTab.cs DeckFlow.Web/Services/Tools/ToolRegistry.cs DeckFlow.Web/Services/FeatureFlags/ DeckFlow.Web/Seo/SeoPaths.cs DeckFlow.Web/Help/deck-history.md README.md DeckFlow.Web.Tests/
git commit -m "feat(deck-history): wire tool registration, feature flag, SEO paths, and help doc"
```

---

### Task 7: Controller + view + CSS

**Files:**
- Create: `DeckFlow.Web/Controllers/DeckHistoryController.cs`
- Create: `DeckFlow.Web/Models/DeckHistoryViewModel.cs`
- Create: `DeckFlow.Web/Views/Deck/DeckHistory.cshtml`
- Modify: `DeckFlow.Web/wwwroot/css/site-common.css` (timeline + diff styles, theme tokens only)
- Test: `DeckFlow.Web.Tests/DeckHistoryControllerTests.cs`

**Interfaces:**
- Consumes: `IDeckHistoryPageService` (Task 5), `DeckHistoryRequest`, flag key + `DeckPageTab.DeckHistory` (Task 6), `DeckHistorySerializer` constants.
- Produces: routes `GET /deck-history`, `POST /deck-history`, `POST /deck-history/download`.

**Controller (implement exactly this shape):**

```csharp
using System.Text;
using DeckFlow.Core.History;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers;

/// <summary>Deck History tool: version a deck into a user-owned downloadable JSON history file.</summary>
public sealed class DeckHistoryController : Controller
{
    private readonly IDeckHistoryPageService _pageService;
    private readonly ILogger<DeckHistoryController> _logger;

    /// <summary>Creates the controller with its page service and logger.</summary>
    public DeckHistoryController(IDeckHistoryPageService pageService, ILogger<DeckHistoryController> logger)
    {
        ArgumentNullException.ThrowIfNull(pageService);
        _pageService = pageService;
        _logger = logger;
    }

    /// <summary>Renders the empty Deck History form.</summary>
    [HttpGet("/deck-history")]
    [FeatureFlagGate("tool.deck-history.enabled")]
    public IActionResult Index() => View("DeckHistory", new DeckHistoryViewModel
    {
        ActiveTab = DeckPageTab.DeckHistory,
        Request = new DeckHistoryRequest(),
    });

    /// <summary>Processes an upload/import/diff request and re-renders the page with results.</summary>
    /// <param name="historyFile">Optional previously downloaded history JSON file.</param>
    /// <param name="request">Form fields.</param>
    [HttpPost("/deck-history")]
    [FeatureFlagGate("tool.deck-history.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> Process(IFormFile? historyFile, DeckHistoryRequest request)
    {
        request ??= new DeckHistoryRequest();
        string? uploadedJson = null;

        if (historyFile is { Length: > 0 })
        {
            if (historyFile.Length > DeckHistorySerializer.MaxUploadBytes)
            {
                return HistoryView(request, error: "History file is too large (limit 1 MB).");
            }

            if (!string.Equals(Path.GetExtension(historyFile.FileName), ".json", StringComparison.OrdinalIgnoreCase))
            {
                return HistoryView(request, error: "Only .json files produced by Download are accepted.");
            }

            using var reader = new StreamReader(historyFile.OpenReadStream(), Encoding.UTF8);
            uploadedJson = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        }

        try
        {
            var result = await _pageService.ProcessAsync(request, uploadedJson, HttpContext.RequestAborted);
            return View("DeckHistory", DeckHistoryViewModel.From(request, result));
        }
        catch (OperationCanceledException)
        {
            return HistoryView(request, error: "The request timed out. Try again.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Deck history processing failed.");
            return HistoryView(request, error: "Something went wrong processing the deck history. Try again.");
        }
    }

    /// <summary>Returns the current history JSON (from the hidden round-trip field) as a file download.</summary>
    /// <param name="request">Form fields carrying <see cref="DeckHistoryRequest.HistoryJson"/>.</param>
    [HttpPost("/deck-history/download")]
    [FeatureFlagGate("tool.deck-history.enabled")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public IActionResult Download(DeckHistoryRequest request)
    {
        request ??= new DeckHistoryRequest();
        var parsed = DeckHistorySerializer.Parse(request.HistoryJson ?? string.Empty);
        if (parsed.File is null)
        {
            return HistoryView(request, error: "Nothing to download yet — import a deck or upload a history file first.");
        }

        var json = DeckHistorySerializer.Serialize(parsed.File);
        var fileName = $"deck-history-{Slug(parsed.File.DeckName)}-{DateTime.UtcNow:yyyyMMdd}.json";
        Response.Headers["X-DeckFlow-Filename"] = fileName;
        return File(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", fileName);
    }

    private ViewResult HistoryView(DeckHistoryRequest request, string error) =>
        View("DeckHistory", new DeckHistoryViewModel
        {
            ActiveTab = DeckPageTab.DeckHistory,
            Request = request,
            ErrorMessage = error,
        });

    private static string Slug(string name)
    {
        var cleaned = new string(name.Trim().ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (cleaned.Contains("--", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrEmpty(cleaned) ? "deck" : cleaned;
    }
}
```

**ViewModel** — `DeckHistoryViewModel` with `ActiveTab`, `Request`, `ErrorMessage`, plus display projections: `TimelineRows` (Id, Date, Label, Notes, CardCount = commander count + sum of card qtys, AddsSummary/CutsSummary short strings), `PairDiff`, `PairOlderId`, `PairNewerId`, `PromptText`, `HistoryJson`, `Warnings`, `HasResult`. Include a static `From(DeckHistoryRequest, DeckHistoryProcessResult)` mapper. All `{ get; init; }`.

**View** — `Views/Deck/DeckHistory.cshtml`. Mirror `Bracket.cshtml`'s skeleton precisely:
- `_DeckToolTabs` at top via the shared layout conventions; intro `<p>` explaining the file-you-own model (no account needed; DeckFlow never stores your history).
- Form (`method="post"`, `enctype="multipart/form-data"`, `action="/deck-history"`):
  - `<input type="file" name="historyFile" accept=".json,application/json">` with a hint row "First visit? Skip this and just import your deck."
  - Split deck input copied from `Bracket.cshtml:50-72` (same `name="DeckInputSource"` select, `name="DeckUrl"` url input, `name="DeckText"` textarea, both-site placeholder text convention).
  - `DeckName` text input, `Label` text input, `Notes` textarea (rows=3).
  - `_AiSelector` partial for `Request.TargetAiPlatform` (as in `DeckPrimer.cshtml:159`).
  - Submit button with the busy-indicator data attribute used by `Bracket.cshtml`'s submit.
- Results section (`@if (Model.HasResult)`):
  - Warnings list (non-blocking, styled as notices).
  - Timeline table: one row per version — Id, Date (yyyy-MM-dd), Label, Notes, Cards, Adds/Cuts summary.
  - Pair-diff block: two `<select>`s (`OlderVersionId`, `NewerVersionId`, options = version ids + dates) inside a small form that re-POSTs to `/deck-history` carrying `HistoryJson` + selector values as hidden/visible fields; render `PairDiff` as three lists (Adds / Cuts / Qty changes).
  - Prompt copy box: `<textarea id="deck-history-prompt" readonly>@Model.PromptText</textarea>` + `<button type="button" class="copy-button" data-copy-target="#deck-history-prompt">Copy</button>` (same as `Bracket.cshtml:309`).
  - Download form posting to `/deck-history/download` with hidden `HistoryJson`, button marked `data-prompt-download-submit` (deck-sync.js intercept).
- Scripts section: same includes as `Bracket.cshtml:334-337` (`deck-input-store.js`, `busy-indicator.js`, `moxfield-extension-bridge.js`, `deck-sync.js`).
- Every error path renders the standard `ErrorMessage` alert markup used by Bracket.

**CSS** — `site-common.css`: `.history-timeline` table styles, `.history-diff` three-column layout (stack on mobile ≤ 640px), `.history-warnings` notice list. Theme tokens only (`var(--panel)`, `var(--text)`, etc. — copy token names from the bracket result card styles).

**Controller tests** (`DeckHistoryControllerTests`, fake `IDeckHistoryPageService`):
- GET returns view with `ActiveTab == DeckPageTab.DeckHistory`.
- POST with oversized file (fake `IFormFile` with `Length` > 1 MB) → `ErrorMessage` about size, service not called.
- POST with non-.json filename → extension error.
- POST happy path passes file content to service and returns its result.
- Download with valid `HistoryJson` → `FileContentResult`, content type `application/json; charset=utf-8`, `X-DeckFlow-Filename` header set, filename matches `deck-history-*-<date>.json`.
- Download with blank `HistoryJson` → error view.

- [ ] **Step 1: Write the failing controller tests**
- [ ] **Step 2: Run** `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter DeckHistoryControllerTests -v minimal` — Expected: FAIL.
- [ ] **Step 3: Implement controller, view model, view, CSS.**
- [ ] **Step 4: Run tests + full Web suite:** `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -v minimal` — Expected: PASS, no regressions.
- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Controllers/DeckHistoryController.cs DeckFlow.Web/Models/DeckHistoryViewModel.cs DeckFlow.Web/Views/Deck/DeckHistory.cshtml DeckFlow.Web/wwwroot/css/site-common.css DeckFlow.Web.Tests/DeckHistoryControllerTests.cs
git commit -m "feat(deck-history): add controller, page, and themed styles"
```

---

### Task 8: E2E smoke spec + screenshots

**Files:**
- Create: `DeckFlow.Web/e2e/deck-history-smoke.spec.ts`
- Screenshots land in: `.planning/ui-design/deck-history/screenshots/`

**Interfaces:**
- Consumes: the finished page; `e2e/support/admin-lock` helpers; the transient-flag-toggle pattern from `bracket-smoke.spec.ts` (admin creds from `FEEDBACK_ADMIN_USER`/`FEEDBACK_ADMIN_PASSWORD`, flag flipped ON for the run and reverted in `afterEach`).
- Produces: CI-runnable smoke coverage + visual-verification screenshots at both Playwright viewports.

Model the spec directly on `bracket-smoke.spec.ts` (read it first; reuse its admin-lock + flag toggle scaffolding verbatim). Cover:

1. Flag ON: `GET /deck-history` renders the form (file input, source select, notes).
2. New history: paste a small Commander deck (reuse the `HIGH_POWER_DECK`-style inline list pattern — commander + ~100 filler lines is unnecessary; the page service validates 100 cards, so paste a filler deck that sums to exactly 100 across commander+mainboard: 1 commander + `10 Plains` × 9 + `9 Island` = adjust to total 100), add a note, submit → timeline shows VERSION 1, prompt textarea non-empty.
3. Download: click the download button → intercepted blob download; assert the response carried `X-DeckFlow-Filename` (via `page.waitForResponse` on `/deck-history/download`).
4. Append: re-submit the same page with one card swapped in the pasted list plus the previous step's downloaded JSON re-uploaded via `setInputFiles` → timeline shows 2 versions and the diff block lists the swap.
5. Screenshots: form + results at the current project viewport across Classic / Azorius / Nyx theme cookies (copy the bracket spec's `themes` loop) into `.planning/ui-design/deck-history/screenshots/`.
6. Flag OFF: `/deck-history` → 404 and no Home tile.

- [ ] **Step 1: Write the spec.**
- [ ] **Step 2: Start the app headless:** `scripts/run-web-test.sh` (never opens a Windows browser). Probe for a stale Windows listener on 5173 first (`cmd.exe /c netstat -ano | grep 5173` — kill or use the WSL server; a stale Windows server serves old builds).
- [ ] **Step 3: Run:** `cd DeckFlow.Web && env -u DISPLAY -u WAYLAND_DISPLAY DECKFLOW_DISABLE_AUTO_BROWSER=true npx --no-install playwright test e2e/deck-history-smoke.spec.ts --reporter=line`
Expected: PASS on both chromium-desktop and chromium-mobile projects.
- [ ] **Step 4: Run the FULL e2e suite** (`npx --no-install playwright test --reporter=line`) — Expected: no regressions (admin specs use the /tmp lock; don't parallelize around them).
- [ ] **Step 5: Eyeball the screenshots** (2 viewports × 3 themes) for layout/theme-token breakage, then commit:

```bash
git add DeckFlow.Web/e2e/deck-history-smoke.spec.ts
git commit -m "test(deck-history): add e2e smoke with flag toggle, download intercept, and theme screenshots"
```

(Screenshots under `.planning/ui-design/` — commit them only if the repo's existing screenshot dirs are tracked; match whatever `git check-ignore` says.)

---

## Final gate (after all tasks)

- [ ] Full test suites: Core + Web + e2e all green.
- [ ] `/simplify` run on the branch diff.
- [ ] `git diff --stat main` vs `git diff --ignore-all-space --stat main` — no EOL churn.
- [ ] README + help doc present; spec updated (Task 2 projector note).
- [ ] Feature flag confirmed seeded OFF (no prod flip until UAT).
- [ ] User manual test + eyeball screenshots before merge to main (ff, user pushes).

## Self-Review Notes

- Spec coverage: format (T1), diff (T2), append/identical/repair (T1/T3), prompts (T4), modes + errors (T5/T7), wiring/SEO/flag/help/README (T6), download/upload + mobile intercept (T7), e2e/screenshots (T8). Divergent-copies = no-op by design (no task needed). Out-of-scope items untouched.
- Known judgment calls: projector bypasses DiffEngine (documented in Task 2, spec updated); `ValidateCommanderDeckSize` enforced on import (matches sibling tools; e2e deck must sum to exactly 100).
- Type consistency: `SnapshotDelta.QtyChanges` (file) vs `VersionDiff.QuantityChanges` (in-memory) is intentional — file property serializes as `qtyChanges` per spec example.
