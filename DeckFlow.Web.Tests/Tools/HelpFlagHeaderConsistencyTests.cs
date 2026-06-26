using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Tools;
using Xunit;

namespace DeckFlow.Web.Tests.Tools;

/// <summary>
/// Guards the registry-to-help requires_flag contract for tools with help topics.
/// </summary>
public sealed class HelpFlagHeaderConsistencyTests
{
    [Fact]
    public void RegistryGatedHelpTopics_DeclareMatchingRequiresFlagHeaders()
    {
        var helpRoot = FindProjectHelpRoot();
        var registry = new ToolRegistry();

        foreach (var tool in registry.All.Where(static tool => tool.HelpSlug is not null))
        {
            var helpPath = Path.Combine(helpRoot, $"{tool.HelpSlug}.md");
            Assert.True(File.Exists(helpPath), $"Missing help file for tool '{tool.Key}' at '{helpPath}'.");

            var raw = File.ReadAllText(helpPath);
            var (header, _) = ContentArtifactParser.SplitHeader(raw);

            Assert.True(
                header.TryGetValue("requires_flag", out var requiresFlag),
                $"Help topic '{tool.HelpSlug}' is missing a requires_flag header.");
            Assert.Equal(tool.FlagKey, requiresFlag);
        }
    }

    private static string FindProjectHelpRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var helpRoot = Path.Combine(current.FullName, "DeckFlow.Web", "Help");
            if (Directory.Exists(helpRoot))
                return helpRoot;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find DeckFlow.Web/Help from the test working directory.");
    }
}
