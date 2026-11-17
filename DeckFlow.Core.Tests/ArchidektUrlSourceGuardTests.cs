using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace DeckFlow.Core.Tests;

/// <summary>Guards centralized construction of URL source labels, including test fixtures.</summary>
public sealed class ArchidektUrlSourceGuardTests
{
    [Fact]
    public void SourceLabels_DynamicConstruction_UsesBuildUrlSource()
    {
        var root = Directory.GetParent(Path.GetDirectoryName(SourcePath())!)!.FullName;
        var violations = Directory.EnumerateDirectories(root, "DeckFlow.*")
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .Where(path => Path.GetFileName(path) is not ("ArchidektApiUrl.cs" or "ArchidektUrlSourceGuardTests.cs"))
            .SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
            .Where(row => !row.line.Contains("Assert.Equal", StringComparison.Ordinal))
            .Where(row => Regex.IsMatch(row.line, """\$"archidekt_url:|"archidekt_url:[^"]*"\s*\+"""))
            .Select(row => $"{row.path}:{row.index + 1}")
            .ToArray();

        Assert.Empty(violations);
    }

    private static string SourcePath([CallerFilePath] string path = "") => path;
}
