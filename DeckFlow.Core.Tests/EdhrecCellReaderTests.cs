using System.Reflection;
using DeckFlow.Core.Research;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Covers reading the EDHREC bracket-cell cache from the fetcher's real on-disk snake_case shape.
/// </summary>
public sealed class EdhrecCellReaderTests
{
    [Fact]
    public void Read_ReturnsCellsFromValidManifestAndCellFiles()
    {
        using var tempRoot = new TempDirectory();
        WriteManifest(
            tempRoot.Path,
            """
            {
              "selected_commanders": [
                { "commander": "Alpha", "slug": "alpha" }
              ],
              "brackets": ["core", "optimized"],
              "commanders_selected": 1,
              "min_decks": 8000
            }
            """);
        WriteCell(tempRoot.Path, "alpha__core.json", CreateValidCellJson("alpha", "Alpha", "core", 2, 1055, 31, 11, 20, ["1 Sol Ring", "4 Mountain", "95 Island"]));
        WriteCell(tempRoot.Path, "alpha__optimized.json", CreateValidCellJson("alpha", "Alpha", "optimized", 4, 51, 29, 10, 19, ["1 Arcane Signet", "99 Forest"]));

        EdhrecReadResult result = EdhrecCellReader.Read(tempRoot.Path, 400);

        Assert.Null(result.Failure);
        Assert.Equal(2, result.Cells.Count);
        Assert.Equal(400, result.MinCellDeckCount);
        Assert.Equal(1, result.CommandersSelected);
        Assert.Equal(["core", "optimized"], result.Brackets);

        EdhrecCell coreCell = Assert.Single(result.Cells, cell => cell.Bracket == "core");
        Assert.Equal("Alpha", coreCell.Commander);
        Assert.Equal("alpha", coreCell.Slug);
        Assert.Equal(2, coreCell.BracketIndex);
        Assert.Equal(1055, coreCell.NDecks);
        Assert.True(coreCell.Qualifies);
        Assert.Equal(31, coreCell.EdhrecLandCount);
        Assert.Equal(11, coreCell.EdhrecBasicCount);
        Assert.Equal(20, coreCell.EdhrecNonbasicCount);
        Assert.Equal("2024-07-28", coreCell.MinSaveDate);
        Assert.Equal("2026-07-27", coreCell.MaxSaveDate);
        Assert.Equal(100, coreCell.CardCount);
        Assert.Equal(3, coreCell.Cards.Count);
        Assert.Empty(coreCell.ParseFailures);

        EdhrecCell optimizedCell = Assert.Single(result.Cells, cell => cell.Bracket == "optimized");
        Assert.Equal(51, optimizedCell.NDecks);
        Assert.False(optimizedCell.Qualifies);
        Assert.Equal(100, optimizedCell.CardCount);

        Assert.Empty(result.MissingCells);
        Assert.Empty(result.InvalidCells);
        Assert.Empty(result.UnexpectedCells);
        Assert.Empty(result.CardCountAnomalies);
    }

    [Fact]
    public void Read_ParsesDeckEntryQuantitiesAndPunctuatedNames()
    {
        using var tempRoot = new TempDirectory();
        WriteManifest(tempRoot.Path, CreateManifestJson(["alpha"], ["core"]));
        WriteCell(
            tempRoot.Path,
            "alpha__core.json",
            CreateValidCellJson(
                "alpha",
                "Alpha",
                "core",
                2,
                1055,
                12,
                4,
                8,
                ["4 Mountain", "1 Sol Ring", "1 Adrix and Nev, Twincasters", "94 Island"]));

        EdhrecReadResult result = EdhrecCellReader.Read(tempRoot.Path, 400);

        EdhrecCell cell = Assert.Single(result.Cells);
        Assert.Collection(
            cell.Cards,
            card =>
            {
                Assert.Equal(4, card.Quantity);
                Assert.Equal("Mountain", card.Name);
            },
            card =>
            {
                Assert.Equal(1, card.Quantity);
                Assert.Equal("Sol Ring", card.Name);
            },
            card =>
            {
                Assert.Equal(1, card.Quantity);
                Assert.Equal("Adrix and Nev, Twincasters", card.Name);
            },
            card =>
            {
                Assert.Equal(94, card.Quantity);
                Assert.Equal("Island", card.Name);
            });
        Assert.Equal(2, cell.BracketIndex);
        Assert.Equal(1055, cell.NDecks);
    }

    [Fact]
    public void Read_RecordsParseFailuresWithoutDroppingCell()
    {
        using var tempRoot = new TempDirectory();
        WriteManifest(tempRoot.Path, CreateManifestJson(["alpha"], ["core"]));
        WriteCell(
            tempRoot.Path,
            "alpha__core.json",
            CreateValidCellJson("alpha", "Alpha", "core", 2, 1055, 20, 10, 10, ["1 Sol Ring", "Forest", "98 Island"]));

        EdhrecReadResult result = EdhrecCellReader.Read(tempRoot.Path, 400);

        EdhrecCell cell = Assert.Single(result.Cells);
        Assert.Equal(["Forest"], cell.ParseFailures);
        Assert.Equal(2, cell.Cards.Count);
        Assert.DoesNotContain(cell.Cards, card => card.Name == "Forest");
        Assert.Equal(99, cell.CardCount);
        Assert.Single(result.CardCountAnomalies);
    }

    [Fact]
    public void Read_DerivesQualificationFromCellNDecksAndKeepsBelowFloorCells()
    {
        using var tempRoot = new TempDirectory();
        WriteManifest(tempRoot.Path, CreateManifestJson(["alpha"], ["core", "cedh"]));
        WriteCell(tempRoot.Path, "alpha__core.json", CreateValidCellJson("alpha", "Alpha", "core", 2, 1055, 33, 12, 21, ["100 Island"]));
        WriteCell(tempRoot.Path, "alpha__cedh.json", CreateValidCellJson("alpha", "Alpha", "cedh", 5, 51, 30, 10, 20, ["100 Island"]));

        EdhrecReadResult result = EdhrecCellReader.Read(tempRoot.Path, 400);

        Assert.Equal(2, result.Cells.Count);
        Assert.Contains(result.Cells, cell => cell.Bracket == "core" && cell.Qualifies);
        Assert.Contains(result.Cells, cell => cell.Bracket == "cedh" && !cell.Qualifies);
    }

    [Fact]
    public void Read_IgnoresManifestMinDecksWhenDerivingQualification()
    {
        using var tempRoot = new TempDirectory();
        WriteManifest(
            tempRoot.Path,
            """
            {
              "selected_commanders": [
                { "commander": "Alpha", "slug": "alpha" }
              ],
              "brackets": ["core"],
              "commanders_selected": 1,
              "min_decks": 8000
            }
            """);
        WriteCell(tempRoot.Path, "alpha__core.json", CreateValidCellJson("alpha", "Alpha", "core", 2, 1055, 20, 10, 10, ["100 Island"]));

        EdhrecReadResult result = EdhrecCellReader.Read(tempRoot.Path, 400);

        EdhrecCell cell = Assert.Single(result.Cells);
        Assert.True(cell.Qualifies);
        Assert.Equal(1055, cell.NDecks);
    }

    [Fact]
    public void Read_RecordsCardCountAnomaliesWithoutRejectingCells()
    {
        using var tempRoot = new TempDirectory();
        WriteManifest(tempRoot.Path, CreateManifestJson(["alpha"], ["core"]));
        WriteCell(tempRoot.Path, "alpha__core.json", CreateValidCellJson("alpha", "Alpha", "core", 2, 1055, 20, 10, 10, ["1 Sol Ring", "4 Mountain"]));

        EdhrecReadResult result = EdhrecCellReader.Read(tempRoot.Path, 400);

        EdhrecCell cell = Assert.Single(result.Cells);
        Assert.Equal(5, cell.CardCount);
        Assert.Single(result.CardCountAnomalies);
        Assert.Contains("alpha__core.json", result.CardCountAnomalies[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RecordsMissingPlannedCells()
    {
        using var tempRoot = new TempDirectory();
        WriteManifest(tempRoot.Path, CreateManifestJson(["alpha"], ["core", "optimized"]));
        WriteCell(tempRoot.Path, "alpha__core.json", CreateValidCellJson("alpha", "Alpha", "core", 2, 1055, 20, 10, 10, ["100 Island"]));

        EdhrecReadResult result = EdhrecCellReader.Read(tempRoot.Path, 400);

        Assert.Single(result.Cells);
        Assert.Single(result.MissingCells);
        Assert.Contains("alpha__optimized.json", result.MissingCells[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RecordsInvalidCellsForBadJsonAndSchemaViolations()
    {
        using var tempRoot = new TempDirectory();
        WriteManifest(tempRoot.Path, CreateManifestJson(["alpha", "beta", "gamma", "delta"], ["core"]));
        WriteCell(tempRoot.Path, "alpha__core.json", "{ not json");
        WriteCell(tempRoot.Path, "beta__core.json", CreateCellJsonWithoutNDecks("beta", "Beta", "core", 2));
        WriteCell(tempRoot.Path, "gamma__core.json", CreateValidCellJson("gamma", "Gamma", "mythic", 2, 1055, 20, 10, 10, ["100 Island"]));
        WriteCell(tempRoot.Path, "delta__core.json", CreateValidCellJson("delta", "Delta", "core", 5, 1055, 20, 10, 10, ["100 Island"]));

        EdhrecReadResult result = EdhrecCellReader.Read(tempRoot.Path, 400);

        Assert.Empty(result.Cells);
        Assert.Equal(4, result.InvalidCells.Count);
        Assert.Contains(result.InvalidCells, value => value.Contains("alpha__core.json", StringComparison.Ordinal));
        Assert.Contains(result.InvalidCells, value => value.Contains("beta__core.json", StringComparison.Ordinal) && value.Contains("n_decks", StringComparison.Ordinal));
        Assert.Contains(result.InvalidCells, value => value.Contains("gamma__core.json", StringComparison.Ordinal) && value.Contains("bracket", StringComparison.Ordinal));
        Assert.Contains(result.InvalidCells, value => value.Contains("delta__core.json", StringComparison.Ordinal) && value.Contains("bracket_index", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_RecordsUnexpectedCellFiles()
    {
        using var tempRoot = new TempDirectory();
        WriteManifest(tempRoot.Path, CreateManifestJson(["alpha"], ["core"]));
        WriteCell(tempRoot.Path, "alpha__core.json", CreateValidCellJson("alpha", "Alpha", "core", 2, 1055, 20, 10, 10, ["100 Island"]));
        WriteCell(tempRoot.Path, "beta__core.json", CreateValidCellJson("beta", "Beta", "core", 2, 1055, 20, 10, 10, ["100 Island"]));

        EdhrecReadResult result = EdhrecCellReader.Read(tempRoot.Path, 400);

        Assert.Single(result.Cells);
        Assert.Single(result.UnexpectedCells);
        Assert.Contains("beta__core.json", result.UnexpectedCells[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Read_RejectsInvalidSlugsAndEscapingResolvedPaths()
    {
        using var tempRoot = new TempDirectory();
        WriteManifest(
            tempRoot.Path,
            """
            {
              "selected_commanders": [
                { "commander": "Bad", "slug": "bad/slug" },
                { "commander": "Alpha", "slug": "alpha" }
              ],
              "brackets": ["core", "../../../../../../escape"],
              "commanders_selected": 2,
              "min_decks": 8000
            }
            """);
        WriteCell(tempRoot.Path, "alpha__core.json", CreateValidCellJson("alpha", "Alpha", "core", 2, 1055, 20, 10, 10, ["100 Island"]));

        EdhrecReadResult result = EdhrecCellReader.Read(tempRoot.Path, 400);

        Assert.Single(result.Cells);
        Assert.Equal(3, result.InvalidCells.Count);
        Assert.Contains(result.InvalidCells, value => value.Contains("bad/slug", StringComparison.Ordinal));
        Assert.Contains(result.InvalidCells, value => value.Contains("alpha__../../../../../../escape.json", StringComparison.Ordinal) || value.Contains("/escape.json", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_ReturnsFailureForMissingDirectory()
    {
        string missingDirectory = Path.Combine(Path.GetTempPath(), "deckflow-edhrec-missing-" + Guid.NewGuid().ToString("N"));

        EdhrecReadResult result = EdhrecCellReader.Read(missingDirectory, 400);

        Assert.NotNull(result.Failure);
        Assert.Contains(missingDirectory, result.Failure, StringComparison.Ordinal);
        Assert.Empty(result.Cells);
    }

    [Fact]
    public void Read_ReturnsFailureForUnparseableManifest()
    {
        using var tempRoot = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(tempRoot.Path, "cells"));
        File.WriteAllText(Path.Combine(tempRoot.Path, "manifest.json"), "{ no json");

        EdhrecReadResult result = EdhrecCellReader.Read(tempRoot.Path, 400);

        Assert.NotNull(result.Failure);
        Assert.Contains("manifest.json", result.Failure, StringComparison.Ordinal);
        Assert.Empty(result.Cells);
    }

    [Fact]
    public void ReaderAssembly_DoesNotReferenceDeckFlowWeb()
    {
        AssemblyName[] references = typeof(EdhrecCellReader).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => string.Equals(reference.Name, "DeckFlow.Web", StringComparison.Ordinal));
    }

    private static void WriteManifest(string rootDirectory, string manifestJson)
    {
        Directory.CreateDirectory(Path.Combine(rootDirectory, "cells"));
        File.WriteAllText(Path.Combine(rootDirectory, "manifest.json"), manifestJson);
    }

    private static void WriteCell(string rootDirectory, string fileName, string cellJson)
    {
        Directory.CreateDirectory(Path.Combine(rootDirectory, "cells"));
        File.WriteAllText(Path.Combine(rootDirectory, "cells", fileName), cellJson);
    }

    private static string CreateManifestJson(IReadOnlyList<string> slugs, IReadOnlyList<string> brackets)
    {
        string commandersJson = string.Join(
            ",\n",
            slugs.Select(slug => $"    {{ \"commander\": \"{slug}\", \"slug\": \"{slug}\" }}"));
        string bracketsJson = string.Join(", ", brackets.Select(bracket => $@"""{bracket}"""));

        return $$"""
        {
          "selected_commanders": [
        {{commandersJson}}
          ],
          "brackets": [{{bracketsJson}}],
          "commanders_selected": {{slugs.Count}},
          "min_decks": 8000
        }
        """;
    }

    private static string CreateValidCellJson(
        string slug,
        string commander,
        string bracket,
        int bracketIndex,
        int nDecks,
        int land,
        int basic,
        int nonbasic,
        IReadOnlyList<string> deckEntries)
    {
        string deckJson = string.Join(",\n", deckEntries.Select(entry => $"""    "{entry}" """.TrimEnd()));

        return $$"""
        {
          "artifact": 0,
          "basic": {{basic}},
          "battle": 0,
          "bracket": "{{bracket}}",
          "bracket_index": {{bracketIndex}},
          "budget_counts": {
            "budget": 1,
            "expensive": 1,
            "middle": 1
          },
          "commander": "{{commander}}",
          "commander_card": {
            "name": "{{commander}}",
            "sanitized": "{{slug}}"
          },
          "creature": 0,
          "deck": [
        {{deckJson}}
          ],
          "enchantment": 0,
          "fetched_utc": "2026-07-27T19:09:35Z",
          "instant": 0,
          "land": {{land}},
          "mana_curve": {
            "1": 1
          },
          "n_decks": {{nDecks}},
          "nonbasic": {{nonbasic}},
          "piechart": [],
          "planeswalker": 0,
          "savedate_summary": {
            "distinct_days": {{nDecks}},
            "max_date": "2026-07-27",
            "min_date": "2024-07-28",
            "total": {{nDecks}}
          },
          "similar": [],
          "slug": "{{slug}}",
          "sorcery": 0,
          "tag_counts": {}
        }
        """;
    }

    private static string CreateCellJsonWithoutNDecks(string slug, string commander, string bracket, int bracketIndex)
        => $$"""
        {
          "artifact": 0,
          "basic": 10,
          "battle": 0,
          "bracket": "{{bracket}}",
          "bracket_index": {{bracketIndex}},
          "budget_counts": {
            "budget": 1,
            "expensive": 1,
            "middle": 1
          },
          "commander": "{{commander}}",
          "commander_card": {
            "name": "{{commander}}",
            "sanitized": "{{slug}}"
          },
          "creature": 0,
          "deck": ["100 Island"],
          "enchantment": 0,
          "fetched_utc": "2026-07-27T19:09:35Z",
          "instant": 0,
          "land": 20,
          "mana_curve": {
            "1": 1
          },
          "nonbasic": 10,
          "piechart": [],
          "planeswalker": 0,
          "savedate_summary": {
            "distinct_days": 1,
            "max_date": "2026-07-27",
            "min_date": "2024-07-28",
            "total": 1
          },
          "similar": [],
          "slug": "{{slug}}",
          "sorcery": 0,
          "tag_counts": {}
        }
        """;

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "deckflow-edhrec-" + Guid.NewGuid().ToString("N"));

        public TempDirectory()
            => Directory.CreateDirectory(Path);

        public void Dispose()
            => Directory.Delete(Path, recursive: true);
    }
}
