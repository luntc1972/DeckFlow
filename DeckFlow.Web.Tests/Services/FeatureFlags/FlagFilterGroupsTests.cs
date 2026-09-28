using DeckFlow.Web.Services.FeatureFlags;
using Xunit;

namespace DeckFlow.Web.Tests.Services.FeatureFlags;

/// <summary>Guards namespace-only, dot-terminated chip derivation with friendly labels (D-13).</summary>
public sealed class FlagFilterGroupsTests
{
    [Fact]
    public void Derive_LiveKeyShape_ReturnsNamespaceChipsInOrdinalOrder()
    {
        Assert.Equal(
            new[]
            {
                new FlagFilterGroup("analysis.", "Analysis", 4),
                new FlagFilterGroup("service.", "Background services", 2),
                new FlagFilterGroup("sync.", "Sync", 1),
            },
            FlagFilterGroups.Derive(Keys()));
    }

    [Fact]
    public void Derive_InputOrderDoesNotChangeOutput()
    {
        Assert.Equal(FlagFilterGroups.Derive(Keys()), FlagFilterGroups.Derive(Keys().Reverse()));
    }

    [Fact]
    public void Derive_MultiSegmentKeys_YieldNamespaceChipOnly()
    {
        Assert.Equal(
            new[] { new FlagFilterGroup("x.", "X", 4) },
            FlagFilterGroups.Derive(new[] { "x.a.one", "x.a.two", "x.b.three", "x.solo" }));
    }

    [Fact]
    public void Derive_CatalogNonToolKeys_YieldOneChipPerFirstSegment()
    {
        var keys = FeatureFlagCatalog.Descriptions.Keys.Where(x => !x.StartsWith("tool.", StringComparison.Ordinal));
        var groups = FlagFilterGroups.Derive(keys);

        Assert.Equal(keys.Count(), groups.Sum(x => x.FlagCount));
        Assert.Equal(
            keys.Select(x => x.ToLowerInvariant().Split('.')[0] + ".").Distinct().OrderBy(x => x, StringComparer.Ordinal),
            groups.Select(x => x.Prefix));
        Assert.All(groups, x => Assert.Equal(1, x.Prefix.Count(c => c == '.')));
    }

    [Fact]
    public void Derive_AdjacentNamespaceNames_KeepSeparateDotTerminatedPrefixes()
    {
        Assert.Equal(
            new[]
            {
                new FlagFilterGroup("service.", "Background services", 1),
                new FlagFilterGroup("service-v2.", "Service-v2", 1),
                new FlagFilterGroup("services.", "Services", 1),
            },
            FlagFilterGroups.Derive(new[] { "service.a", "services.b", "service-v2.c" }));
    }

    [Fact]
    public void Derive_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(FlagFilterGroups.Derive(Array.Empty<string>()));
    }

    [Theory]
    [InlineData("nodot")]
    [InlineData(".leading")]
    [InlineData("")]
    [InlineData("   ")]
    public void Derive_KeyWithoutUsableNamespace_ProducesNoChips(string key)
    {
        Assert.Empty(FlagFilterGroups.Derive(new[] { key }));
    }

    [Theory]
    [InlineData("ns..x")]
    [InlineData("ns.")]
    public void Derive_EmptyLaterSegment_StillYieldsNamespaceChip(string key)
    {
        Assert.Equal(new[] { new FlagFilterGroup("ns.", "Ns", 1) }, FlagFilterGroups.Derive(new[] { key }));
    }

    [Fact]
    public void Derive_MixedCaseKeys_GroupCaseInsensitively()
    {
        Assert.Equal(
            new[] { new FlagFilterGroup("service.", "Background services", 3) },
            FlagFilterGroups.Derive(new[] { "Service.Tagger.enabled", "service.tagger.other", "SERVICE.cron.enabled" }));
    }

    [Fact]
    public void Derive_NullSequence_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FlagFilterGroups.Derive(null!));
    }

    [Fact]
    public void Derive_NullEntries_AreSkipped()
    {
        Assert.Equal(
            new[] { new FlagFilterGroup("sync.", "Sync", 1) },
            FlagFilterGroups.Derive(new string?[] { null, "sync.reconcile" }!));
    }

    [Theory]
    [InlineData("analysis", "Analysis")]
    [InlineData("service", "Background services")]
    [InlineData("sync", "Sync")]
    [InlineData("tool", "Tools")]
    [InlineData("service-v2", "Service-v2")]
    [InlineData("foo", "Foo")]
    [InlineData("x", "X")]
    public void LabelFor_ReturnsFriendlyOrFallbackLabel(string segment, string expected)
    {
        Assert.Equal(expected, FlagFilterGroups.LabelFor(segment));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LabelFor_NullOrEmpty_Throws(string? segment)
    {
        Assert.ThrowsAny<ArgumentException>(() => FlagFilterGroups.LabelFor(segment!));
    }

    [Fact]
    public void Derive_NamespacesSharingAFriendlyLabel_KeepSeparateChips()
    {
        Assert.Equal(
            new[]
            {
                new FlagFilterGroup("tool.", "Tools", 1),
                new FlagFilterGroup("tools.", "Tools", 1),
            },
            FlagFilterGroups.Derive(new[] { "tool.a", "tools.b" }));
    }

    private static string[] Keys() =>
    [
        "analysis.cut-lab.commander-floors",
        "analysis.cut-lab.functional-twins",
        "analysis.manabase.accuracy",
        "analysis.wincon-map",
        "service.harvest-cron.enabled",
        "service.scryfall-tagger.enabled",
        "sync.reconcile",
    ];
}
