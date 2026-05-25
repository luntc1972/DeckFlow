using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for category knowledge parsing helpers used by the cache write path.
/// </summary>
public sealed class CategoryKnowledgeReporterTests
{
    [Fact]
    public void SplitCategories_KeepsOnlyCardTypeCategoryForWritePath()
    {
        var categories = CategoryKnowledgeReporter.SplitCategories("Artifact").ToList();

        Assert.Equal(new[] { "Artifact" }, categories);
    }
}
