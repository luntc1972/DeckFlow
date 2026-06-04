# Phase 29: Core XML-Doc Backfill + Gate Widen — Pattern Map

**Mapped:** 2026-06-04
**Files analyzed:** 30 (29 DeckFlow.Core source files + `.editorconfig`)
**Analogs found:** 30 / 30

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Core/Storage/IRelationalDialect.cs` | model/interface | — | `DeckFlow.Core/Content/IContentVideoStore.cs` | role-match (interface doc style) |
| `DeckFlow.Core/Storage/SqliteRelationalDialect.cs` | utility | — | `DeckFlow.Core/Content/IContentVideoStore.cs` | role-match |
| `DeckFlow.Core/Storage/PostgresRelationalDialect.cs` | utility | — | `DeckFlow.Core/Content/IContentVideoStore.cs` | role-match |
| `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` | utility | — | `DeckFlow.Core/Content/ContentVideoStore.cs` | exact (partial docs already; fill gaps) |
| `DeckFlow.Core/Reporting/ReconciliationReporter.cs` | utility | — | `DeckFlow.Core/Content/ContentVideoStore.cs` | role-match (raw-string danger zone) |
| `DeckFlow.Core/Reporting/CategoryCardReporter.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Reporting/CategoryCountReporter.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Reporting/CategorySuggestionReporter.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Reporting/CategoryFilter.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Filtering/DeckEntryFilter.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` | model | — | `DeckFlow.Core/Content/IContentVideoStore.cs` | role-match |
| `DeckFlow.Core/Knowledge/BoardCategoryComparer.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` | service | CRUD | `DeckFlow.Core/Content/ContentVideoStore.cs` | exact (partial docs + raw-string danger) |
| `DeckFlow.Core/Integration/DeckImporterInterfaces.cs` | model/interface | — | `DeckFlow.Core/Content/IContentVideoStore.cs` | exact (partial docs already present) |
| `DeckFlow.Core/Integration/ArchidektRecentDecksImporter.cs` | service | request-response | `DeckFlow.Core/Content/ContentVideoStore.cs` | role-match |
| `DeckFlow.Core/Integration/MoxfieldApiUrl.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Integration/ArchidektApiUrl.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` | service | request-response | `DeckFlow.Core/Content/ContentVideoStore.cs` | role-match (CS1573 fix needed) |
| `DeckFlow.Core/Exporting/DeltaExporter.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Exporting/FullImportExporter.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Exporting/MoxfieldTextExporter.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Exporting/CategoryNormalization.cs` | utility | — | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Parsing/IParser.cs` | model/interface | — | `DeckFlow.Core/Content/IContentVideoStore.cs` | exact (interface, partial docs) |
| `DeckFlow.Core/Parsing/ArchidektParser.cs` | service | transform | `DeckFlow.Core/Content/ContentVideoStore.cs` | role-match |
| `DeckFlow.Core/Parsing/MoxfieldParser.cs` | service | transform | `DeckFlow.Core/Content/ContentVideoStore.cs` | role-match |
| `DeckFlow.Core/Parsing/DeckParseException.cs` | model | — | `DeckFlow.Core/Content/IContentVideoStore.cs` | role-match |
| `DeckFlow.Core/Models/PrintingChoice.cs` | model | — | `DeckFlow.Core/Integration/DeckImporterInterfaces.cs` | exact (enum doc style) |
| `DeckFlow.Core/Models/MatchMode.cs` | model | — | `DeckFlow.Core/Integration/DeckImporterInterfaces.cs` | exact (enum doc style) |
| `DeckFlow.Core/Normalization/CardNormalizer.cs` | utility | transform | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `DeckFlow.Core/Diffing/DiffEngine.cs` | utility | transform | `DeckFlow.Core/Content/IContentSourceStore.cs` | role-match |
| `.editorconfig` | config | — | `.editorconfig` lines 111–115 | exact (Phase 23 gate section) |

---

## Pattern Assignments

### All 29 DeckFlow.Core source files — XML doc-comment style

**Primary analog:** `DeckFlow.Core/Content/IContentVideoStore.cs` (fully documented, Phase 19–22)

This is the gold-standard exemplar for this phase. Every pattern below is drawn from it and the
other Content KB files. Executors must match this style exactly.

---

#### Pattern 1: Interface method with `<summary>`, `<param>`, `<returns>`

**Analog:** `DeckFlow.Core/Content/IContentVideoStore.cs` lines 39–48

```csharp
/// <summary>
/// Gets a YouTube content video by source and upstream video identifier.
/// </summary>
/// <param name="sourceId">Identifier of the owning content source.</param>
/// <param name="youtubeVideoId">YouTube video identifier.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The content video when found; otherwise <see langword="null"/>.</returns>
Task<ContentVideo?> GetVideoByYoutubeIdAsync(
    long sourceId,
    string youtubeVideoId,
    CancellationToken cancellationToken = default);
```

Rules extracted from this pattern:
- One sentence in `<summary>`, imperative mood ("Gets", "Inserts", "Lists", "Deletes", "Updates").
- `<param>` for every parameter. Short noun phrases, no trailing period. CancellationToken param always reads `"Cancellation token."` (no "Optional" qualifier — that was the old style).
- `<returns>` when non-void. Nullable returns: `"The X when found; otherwise <see langword=\"null\"/>."` pattern.
- No blank line between `///` block and the member declaration.

---

#### Pattern 2: `<inheritdoc/>` on interface implementations

**Analog:** `DeckFlow.Core/Content/ContentVideoStore.cs` lines 42–43 and 69–70

```csharp
/// <inheritdoc />
public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)

/// <inheritdoc />
public async Task<long> InsertVideoAsync(
```

Rule: When a class implements an interface method and adds no new behavior worth documenting
separately, use `/// <inheritdoc />` (with a space before the `/>`). Do NOT repeat the
interface's `<summary>` text on the implementation.

---

#### Pattern 3: Constructor with `<summary>` and `<param>`

**Analog:** `DeckFlow.Core/Content/ContentVideoStore.cs` lines 17–22

```csharp
/// <summary>
/// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
/// </summary>
/// <param name="databasePath">Path to the SQLite file.</param>
public ContentVideoStore(string databasePath)
    : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }
```

Rule: Constructor `<summary>` starts with "Creates", "Initializes", or "Returns" — matches the
existing `CategoryKnowledgeRepository` ctor at line 25–28 (already documented). Use
`<paramref name="x"/>` in the summary text when referencing a parameter inline.

---

#### Pattern 4: Record property one-liners

**Analog:** `DeckFlow.Core/Content/IContentVideoStore.cs` lines 204–210

```csharp
/// <summary>Latest transcript text returned to the distillation orchestrator.</summary>
public sealed record ContentTranscriptBody
{
    /// <summary>Transcript body.</summary>
    public required string Body { get; init; }

    /// <summary>Transcript source matching one of the <see cref="TranscriptSource"/> constants.</summary>
    public required string Source { get; init; }
}
```

Rule: Short properties and enum values use the single-line `/// <summary>text.</summary>` form
(no newline inside the tags). Multi-sentence descriptions use the multi-line form.

---

#### Pattern 5: Enum type + enum values

**Analog:** `DeckFlow.Core/Integration/DeckImporterInterfaces.cs` lines 5–12

```csharp
/// <summary>
/// Indicates whether Moxfield entries were fetched directly or via the Commander Spellbook fallback proxy.
/// </summary>
public enum MoxfieldImportSource
{
    Direct,
    CommanderSpellbookFallback
}
```

**Current state of `PrintingChoice.cs` (undocumented — lines 1–8):**

```csharp
namespace DeckFlow.Core.Models;

public enum PrintingChoice
{
    Unresolved,
    KeepArchidekt,
    UseMoxfield,
}
```

**Required output shape:**

```csharp
namespace DeckFlow.Core.Models;

/// <summary>
/// Represents the user's resolution for a printing conflict between Moxfield and Archidekt.
/// </summary>
public enum PrintingChoice
{
    /// <summary>No resolution has been chosen yet.</summary>
    Unresolved,
    /// <summary>Keep the existing Archidekt printing.</summary>
    KeepArchidekt,
    /// <summary>Switch to the Moxfield printing.</summary>
    UseMoxfield,
}
```

Rule: Enum type gets a multi-line `<summary>`. Each enum member gets a single-line `<summary>`.
CS1591 fires on the type AND on each public member — both must be documented.

---

#### Pattern 6: Interface method with no return value (void / Task)

**Analog:** `DeckFlow.Core/Content/IContentVideoStore.cs` lines 131–139

```csharp
/// <summary>
/// Deletes a video row by identifier.
/// </summary>
/// <param name="videoId">Video identifier.</param>
/// <param name="cancellationToken">Cancellation token.</param>
Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default);
```

Rule: No `<returns>` tag on `void` or `Task` (no return value). Only add `<returns>` when there
is an actual value being returned.

---

#### Pattern 7: Interface method where `IParser` has undocumented members

**Current state of `IParser.cs` (lines 8–13):**

```csharp
public interface IParser
{
    List<DeckEntry> ParseFile(string filePath);

    List<DeckEntry> ParseText(string content);
}
```

**Required output shape:**

```csharp
/// <summary>
/// Parses a deck text file or string into a list of <see cref="DeckEntry"/> records.
/// </summary>
public interface IParser
{
    /// <summary>Parses the deck file at <paramref name="filePath"/> and returns its entries.</summary>
    /// <param name="filePath">Path to the deck text file.</param>
    /// <returns>The parsed deck entries.</returns>
    List<DeckEntry> ParseFile(string filePath);

    /// <summary>Parses raw deck text and returns its entries.</summary>
    /// <param name="content">Deck text content.</param>
    /// <returns>The parsed deck entries.</returns>
    List<DeckEntry> ParseText(string content);
}
```

Note: `IParser` already has a type-level `<summary>` (line 5–7 in the file). CS1591 fires on the
two undocumented methods, not the type. Add member docs only; do NOT rewrite the existing type doc.

---

#### Pattern 8: `IRelationalDialect` — interface with undocumented property members

**Current state of `IRelationalDialect.cs` (lines 1–15):**

```csharp
public interface IRelationalDialect
{
    /// <summary>
    /// Gets the SQL column definition for a surrogate auto-incrementing primary key.
    /// </summary>
    string SurrogateIdColumnType { get; }
    string FeedbackCreatedUtcColumnType { get; }
    string FeedbackOrderByClause { get; }
    string FeedbackInsertReturningIdSql { get; }
}
```

Three properties lack docs. Required additions (single-line form — properties are concise):

```csharp
/// <summary>Gets the SQL column type for the feedback created-UTC timestamp.</summary>
string FeedbackCreatedUtcColumnType { get; }

/// <summary>Gets the SQL ORDER BY clause for feedback queries (most-recent first).</summary>
string FeedbackOrderByClause { get; }

/// <summary>Gets the SQL INSERT…RETURNING statement for inserting feedback and retrieving the new row identifier.</summary>
string FeedbackInsertReturningIdSql { get; }
```

---

#### Pattern 9: `ReconciliationReporter` — static class with raw-string constants (HIGH RISK)

**Current state (lines 9–62 in `ReconciliationReporter.cs`):**

```csharp
public static class ReconciliationReporter
{
    public const string CategoryFixInstructions =
"""
=== How to fix missing or broken categories in Archidekt ===
...
""";

    public const string MoxfieldImportInstructions =
"""
=== How to import into Moxfield safely ===
...
""";

    public static void WriteReport(DeckDiff diff, string outputPath) { ... }

    public static string ToText(DeckDiff diff) { ... }
```

**DANGER ZONE — raw-string literals at lines 12–44 and 46–60.** The `///` doc lines go ABOVE
the `public const string` declaration line, NOT inside or between the `"""` delimiters. The
indentation of the raw-string content (and the closing `"""`) must not be changed.

**Required additions (doc lines only — do NOT touch the raw-string content):**

```csharp
public static class ReconciliationReporter
{
    /// <summary>Instructions shown to the user after generating a category-fix delta file for Archidekt.</summary>
    public const string CategoryFixInstructions =
"""
...  ← UNTOUCHED
""";

    /// <summary>Instructions shown to the user after generating a Moxfield import text file.</summary>
    public const string MoxfieldImportInstructions =
"""
...  ← UNTOUCHED
""";

    /// <summary>Writes a reconciliation report for <paramref name="diff"/> to <paramref name="outputPath"/>.</summary>
    /// <param name="diff">The computed deck diff to report on.</param>
    /// <param name="outputPath">File path to write the report to.</param>
    public static void WriteReport(DeckDiff diff, string outputPath) { ... }

    /// <summary>Returns the reconciliation report text for <paramref name="diff"/> using default system labels.</summary>
    /// <param name="diff">The computed deck diff to report on.</param>
    /// <returns>The formatted reconciliation report text.</returns>
    public static string ToText(DeckDiff diff) { ... }
```

---

#### Pattern 10: CS1573 fix — completing partial `<param>` sets

**Analog:** RESEARCH.md CS1573 fix shape — verified against `CategoryKnowledgeRepository.cs` line 403–409.

`ReplaceSourceRowsAsync` currently has docs for `source`, `rows`, and `cancellationToken` but is
missing `board` and `deckCount`:

```csharp
// BEFORE (lines 403–409) — board and deckCount are undocumented:
/// <summary>
/// Replaces all observations for a source with the provided rows.
/// </summary>
/// <param name="source">Source label for the data.</param>
/// <param name="rows">Rows to persist.</param>
/// <param name="cancellationToken">Optional cancellation token.</param>
public async Task ReplaceSourceRowsAsync(string source, IReadOnlyList<CategoryKnowledgeRow> rows, string board = "mainboard", int deckCount = 0, CancellationToken cancellationToken = default)

// AFTER — insert the two missing params between rows and cancellationToken:
/// <summary>
/// Replaces all observations for a source with the provided rows.
/// </summary>
/// <param name="source">Source label for the data.</param>
/// <param name="rows">Rows to persist.</param>
/// <param name="board">The board zone these rows belong to (e.g., <c>mainboard</c>).</param>
/// <param name="deckCount">Number of decks contributing to these observations.</param>
/// <param name="cancellationToken">Optional cancellation token.</param>
public async Task ReplaceSourceRowsAsync(...)
```

Rule: `<param>` tags must appear in the SAME ORDER as the method parameters. Do NOT remove
existing `<param>` tags. Insert only the missing ones in their correct positional slot.

---

### `.editorconfig` gate-widen pattern

**Analog:** `.editorconfig` lines 111–115 (Phase 23 Web gate section)

**Current state (lines 111–115):**

```ini
[DeckFlow.Web/**.cs]
# Phase 23 DOC-02: XML doc-comment gate scoped to DeckFlow.Web
dotnet_diagnostic.CS1591.severity = warning
dotnet_diagnostic.CS1573.severity = warning
dotnet_diagnostic.CS1587.severity = warning
```

**Required addition — append AFTER the Web section, do NOT modify lines 111–115:**

```ini
[DeckFlow.Core/**.cs]
# Phase 29 HSK-01: XML doc-comment gate widened to DeckFlow.Core
dotnet_diagnostic.CS1591.severity = warning
dotnet_diagnostic.CS1573.severity = warning
dotnet_diagnostic.CS1587.severity = warning
```

This is the COMPLETE and EXACT edit for plan 29-05. One additive block. No other lines touched.

---

## Shared Patterns

### `<see langword="null"/>` for nullable returns

**Source:** `DeckFlow.Core/Content/IContentVideoStore.cs` lines 43–44

```csharp
/// <returns>The content video when found; otherwise <see langword="null"/>.</returns>
```

Apply to: any `<returns>` on a method returning `T?` or `Task<T?>`.

---

### `<see cref="X"/>` for cross-references

**Source:** `DeckFlow.Core/Content/IContentVideoStore.cs` lines 25 and 206

```csharp
/// <param name="transcriptStatus">Transcript status matching one of the <see cref="TranscriptStatus"/> constants.</param>
/// <summary>Transcript source matching one of the <see cref="TranscriptSource"/> constants.</summary>
```

Apply to: any summary or param text that references a named type, enum, or constant class
defined elsewhere in the codebase.

---

### `<paramref name="x"/>` for inline param references in summary text

**Source:** `DeckFlow.Core/Content/ContentVideoStore.cs` line 19

```csharp
/// <summary>
/// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
/// </summary>
```

Apply to: constructor and method summaries that mention one of their own parameters by name.

---

### `/// <inheritdoc />` on interface implementations

**Source:** `DeckFlow.Core/Content/ContentVideoStore.cs` lines 42, 69 (and throughout)

```csharp
/// <inheritdoc />
public async Task EnsureSchemaAsync(...)
```

Apply to: every class method that implements an interface method and adds no behavior worth
separately documenting. This is the preferred form for implementation classes — it eliminates
duplication without suppressing the warning.

---

### One-line form for concise members

**Source:** `DeckFlow.Core/Content/IContentVideoStore.cs` lines 204–210

```csharp
/// <summary>Transcript body.</summary>
public required string Body { get; init; }
```

Apply to: short properties, enum members, and single-purpose constants where a sentence fits on
one line. The multi-line form is for longer descriptions only.

---

## Raw-String Literal Safety Zone

**Files requiring special care (from RESEARCH.md):**

| File | Raw-String Count | Risk |
|------|-----------------|------|
| `Knowledge/CategoryKnowledgeRepository.cs` | 50 | HIGH — SQL queries interspersed with method bodies |
| `Reporting/ReconciliationReporter.cs` | 4 | MEDIUM — constants with raw-string values |
| `Storage/PostgresRelationalDialect.cs` | 2 | LOW |
| `Storage/SqliteRelationalDialect.cs` | 2 | LOW |

**Rule for all four files:** The `///` lines being added go ABOVE the member declaration line.
They never appear inside a `"""..."""` block. The indentation of the closing `"""` and all
content between the delimiters must be preserved exactly as-is.

**Concrete example from `CategoryKnowledgeRepository.cs` lines 403–409** (safe add point):

```
401:    }
402:
403:    /// <summary>          ← ADD HERE (above the public keyword)
404:    /// ...
405:    /// </summary>
406:    /// <param ...>
407:    public async Task ReplaceSourceRowsAsync(...)
408:    {
409:        ...
410:        var deleteCommand = connection.CreateCommand();
411:        deleteCommand.CommandText = "DELETE FROM ...";  ← raw string NOT near this add point
```

Raw strings in this file start at lines 64, 75, 87, 92, 99, 115, 134, 140, 149, 155, 164, 176,
183, 192, 210, 217, 244, 252, 290, 299 — all inside method bodies, not at member declaration
level. Doc comment additions at member declaration level (lines ~33, ~45, ~235, ~409, ~508) are
safely above the nearest raw string.

---

## No Analog Found

All 30 files have analogs. No entries in this section.

---

## Metadata

**Analog search scope:** `DeckFlow.Core/Content/`, `DeckFlow.Core/Integration/`, `DeckFlow.Core/Knowledge/`, `.editorconfig`
**Files scanned:** 8 source files + `.editorconfig`
**Pattern extraction date:** 2026-06-04

**Key constraint reminders for Codex (from CLAUDE.md + RESEARCH.md):**
1. Touch ONLY the `///` lines being added. Do NOT run Format Document or Code Cleanup.
2. Never convert `{ get; init; }` to `{ get; }`.
3. Never re-indent raw-string literals (changes the SQL/text value).
4. Never inline `[Attribute]` onto a declaration line.
5. Preserve LF line endings.
6. `.editorconfig` edit (plan 29-05) is non-autonomous — requires human approval at execution time.
7. Build gate: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Release` must be `0 Warning(s) 0 Error(s)`.
