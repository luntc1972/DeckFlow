using DeckFlow.Web.Services.FeatureFlags;

using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Guards the operator-facing flag descriptions shown on /Admin/Flags. Every seeded flag key
/// (see <c>FeatureFlagStore</c>) must carry a non-empty description so the admin page never shows
/// a blank "what it does" cell for a real flag; unknown keys must degrade to an empty string.
/// </summary>
public sealed class FeatureFlagCatalogTests
{
    // The seed contract from FeatureFlagStore. Kept in lockstep with FeatureFlagStoreSeedTests.
    [Theory]
    [InlineData("service.scryfall-tagger.enabled")]
    [InlineData("tool.help.enabled")]
    [InlineData("service.harvest-cron.enabled")]
    [InlineData("tool.categories.enabled")]
    [InlineData("tool.knowledge-base.enabled")]
    [InlineData("tool.manabase.enabled")]
    [InlineData("tool.deck-analysis.enabled")]
    [InlineData("tool.deck-comparison.enabled")]
    [InlineData("tool.cedh-meta-gap.enabled")]
    [InlineData("tool.deck-sync.enabled")]
    [InlineData("tool.convert.enabled")]
    [InlineData("tool.deck-primer.enabled")]
    [InlineData("tool.card-lookup.enabled")]
    [InlineData("tool.mechanic-lookup.enabled")]
    [InlineData("tool.judge-questions.enabled")]
    [InlineData("tool.commander-categories.enabled")]
    [InlineData("analysis.reference.full-oracle-text")]
    [InlineData("analysis.reference.deck-stats")]
    [InlineData("analysis.manabase.accuracy")]
    [InlineData("analysis.manabase.health-band-castability")]
    [InlineData("analysis.manabase.plain-language-verdict")]
    [InlineData("analysis.manabase.commander-castability")]
    [InlineData("analysis.manabase.tap-analyzer")]
    [InlineData("analysis.command-zone-awareness")]
    [InlineData("tool.bracket.enabled")]
    [InlineData("analysis.multi-axis-score")]
    [InlineData("analysis.manabase.mulligan-eval")]
    [InlineData("analysis.manabase.plan-presence")]
    [InlineData("analysis.manabase.keep-shapes")]
    [InlineData("analysis.manabase.ritual-burst-mana")]
    [InlineData("analysis.manabase.ritual-land-credit")]
    [InlineData("analysis.manabase.scry-credit")]
    [InlineData("analysis.manabase.colorless-snow")]
    [InlineData("analysis.manabase.restricted-lands")]
    [InlineData("analysis.manabase.cedh-land-target")]
    [InlineData("sync.directpush-gitbody")]
    [InlineData("sync.reconcile")]
    public void Describe_EverySeededFlag_HasNonEmptyDescription(string key)
    {
        string description = FeatureFlagCatalog.Describe(key);

        Assert.False(string.IsNullOrWhiteSpace(description), $"no description catalogued for '{key}'");
    }

    [Fact]
    public void Describe_UnknownKey_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, FeatureFlagCatalog.Describe("does.not.exist"));
    }

    [Fact]
    public void Descriptions_AreAllNonEmpty()
    {
        Assert.All(FeatureFlagCatalog.Descriptions.Values, d => Assert.False(string.IsNullOrWhiteSpace(d)));
    }
}
