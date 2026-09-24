using System.Text;

namespace DeckFlow.Core.Tests.Content;

public sealed class Cp437MojibakeTests
{
    [Fact]
    public void ContentKbArtifacts_ContainNoCp437Mojibake_Signatures()
    {
        var root = GetRepoRoot();
        var contentKb = Path.Combine(root, "content-kb");
        // Why: content-kb is untracked from the public repository in some CI phases.
        if (!Directory.Exists(contentKb))
        {
            return;
        }

        var offenders = Directory.EnumerateFiles(contentKb, "*.md", SearchOption.AllDirectories)
            .Where(path =>
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                return text.Contains("ΓÇ", StringComparison.Ordinal)
                    || text.Contains("┬", StringComparison.Ordinal);
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.True(offenders.Length == 0, $"CP437 mojibake found in {offenders.Length} files:\n{string.Join('\n', offenders)}");
    }

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".editorconfig"))
                && File.Exists(Path.Combine(directory.FullName, "DeckFlow.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the current test base directory.");
    }
}
