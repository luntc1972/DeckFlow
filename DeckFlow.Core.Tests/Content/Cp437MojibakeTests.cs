using System.Text;
using DeckFlow.Core.Content;

namespace DeckFlow.Core.Tests.Content;

public sealed class Cp437MojibakeTests
{
    [Cp437Fact]
    public void ContentKbArtifacts_ContainNoCp437Mojibake_Signatures()
    {
        var root = PrivateKbRoot.FromEnvironment();
        var contentKb = root.ContentKbDir;

        var offenders = Directory.EnumerateFiles(contentKb, "*.md", SearchOption.AllDirectories)
            .Where(path =>
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                return text.Contains("ΓÇ", StringComparison.Ordinal)
                    || text.Contains("┬", StringComparison.Ordinal);
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Path.GetRelativePath(root.Root, path))
            .ToArray();

        Assert.True(offenders.Length == 0, $"CP437 mojibake found in {offenders.Length} files:\n{string.Join('\n', offenders)}");
    }

}
