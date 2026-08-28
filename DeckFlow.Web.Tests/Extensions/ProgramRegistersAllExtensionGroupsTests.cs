using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests.Extensions;

/// <summary>
/// Source-reading tripwire for the composition root. An <c>AddDeckFlow*</c> extension group can be
/// written, reviewed and merged without ever being called from <c>Program.cs</c>, and nothing fails:
/// the services it would have registered are simply absent until something resolves one. This guard
/// closes the Wave 4b M2 gap for every group at once.
/// </summary>
/// <remarks>
/// Reads source text rather than booting the app, matching the <c>CarveOutGuardTests</c> house style.
/// A <c>WebApplicationFactory</c> approach would need <c>Microsoft.AspNetCore.Mvc.Testing</c>, a new
/// dependency this guard does not justify.
/// </remarks>
public sealed class ProgramRegistersAllExtensionGroupsTests
{
    [Fact]
    public void Program_CallsEveryDeclaredAddDeckFlowExtension()
    {
        var repoRoot = GetRepoRoot();
        var webProject = Path.Combine(repoRoot, "DeckFlow.Web");
        var programPath = Path.Combine(webProject, "Program.cs");

        var declared = EnumerateProjectSources(webProject)
            .Where(path => !string.Equals(path, programPath, StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                @"public static IServiceCollection\s+(AddDeckFlow\w+)\s*\("))
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var programText = StripComments(File.ReadAllText(programPath));
        var uncalled = declared
            .Where(name => !programText.Contains(name + "(", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(declared);
        Assert.True(
            uncalled.Length == 0,
            $"Declared but never called from Program.cs: {string.Join(", ", uncalled)}");
    }

    // Why: a commented-out call still contains the method name, so a raw Contains check passes on
    // source that no longer registers anything -- proved by mutation. Block comments go first, then
    // whole-line // comments. Trailing // comments are left alone so a URL literal earlier on a real
    // registration line cannot swallow the call that follows it.
    private static string StripComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        return string.Join(
            '\n',
            withoutBlocks
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    // Why: the scan covers the whole DeckFlow.Web project, not just Extensions/, because
    // AddDeckFlowResiliencePipelines is declared in Services/Http/ResiliencePipelineFactory.cs.
    // Narrowing this back to Extensions/ would silently drop that group from the guard.
    private static IEnumerable<string> EnumerateProjectSources(string projectDirectory)
        => Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DeckFlow.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the current test base directory.");
    }
}
