using DeckFlow.Web.Services.FeatureFlags;
using Xunit;

namespace DeckFlow.Web.Tests.Services.FeatureFlags;

/// <summary>Guards FLAGS-01 namespace-only (D-03), dot-terminated, raw-label (D-02) chip derivation.</summary>
public sealed class FlagFilterGroupsTests
{
    [Fact] public void Derive_LiveKeyShape_ReturnsNamespaceChipsInOrdinalOrder() => Assert.Equal(new[] { new FlagFilterGroup("analysis.", "analysis", 4), new FlagFilterGroup("service.", "service", 2), new FlagFilterGroup("sync.", "sync", 1) }, FlagFilterGroups.Derive(Keys()));
    [Fact] public void Derive_InputOrderDoesNotChangeOutput() => Assert.Equal(FlagFilterGroups.Derive(Keys()), FlagFilterGroups.Derive(Keys().Reverse()));
    [Fact] public void Derive_MultiSegmentKeys_YieldNamespaceChipOnly() => Assert.Equal(new[] { new FlagFilterGroup("x.", "x", 4) }, FlagFilterGroups.Derive(new[] { "x.a.one", "x.a.two", "x.b.three", "x.solo" }));
    [Fact] public void Derive_CatalogNonToolKeys_YieldOneChipPerFirstSegment() { var keys = FeatureFlagCatalog.Descriptions.Keys.Where(x => !x.StartsWith("tool.", StringComparison.Ordinal)); var groups = FlagFilterGroups.Derive(keys); Assert.Equal(keys.Count(), groups.Sum(x => x.FlagCount)); Assert.Equal(keys.Select(x => x.ToLowerInvariant().Split('.')[0] + ".").Distinct().OrderBy(x => x, StringComparer.Ordinal), groups.Select(x => x.Prefix)); Assert.All(groups, x => Assert.Equal(1, x.Prefix.Count(c => c == '.'))); }
    [Fact] public void Derive_AdjacentNamespaceNames_KeepSeparateDotTerminatedPrefixes() => Assert.Equal(new[] { new FlagFilterGroup("service.", "service", 1), new FlagFilterGroup("service-v2.", "service-v2", 1), new FlagFilterGroup("services.", "services", 1) }, FlagFilterGroups.Derive(new[] { "service.a", "services.b", "service-v2.c" }));
    [Fact] public void Derive_EmptyInput_ReturnsEmpty() => Assert.Empty(FlagFilterGroups.Derive(Array.Empty<string>()));
    [Theory][InlineData("nodot")][InlineData(".leading")][InlineData("")][InlineData("   ")] public void Derive_KeyWithoutUsableNamespace_ProducesNoChips(string key) => Assert.Empty(FlagFilterGroups.Derive(new[] { key }));
    [Theory][InlineData("ns..x")][InlineData("ns.")] public void Derive_EmptyLaterSegment_StillYieldsNamespaceChip(string key) => Assert.Equal(new[] { new FlagFilterGroup("ns.", "ns", 1) }, FlagFilterGroups.Derive(new[] { key }));
    [Fact] public void Derive_MixedCaseKeys_GroupCaseInsensitively() => Assert.Equal(new[] { new FlagFilterGroup("service.", "service", 3) }, FlagFilterGroups.Derive(new[] { "Service.Tagger.enabled", "service.tagger.other", "SERVICE.cron.enabled" }));
    [Fact] public void Derive_NullSequence_Throws() => Assert.Throws<ArgumentNullException>(() => FlagFilterGroups.Derive(null!));
    [Fact] public void Derive_NullEntries_AreSkipped() => Assert.Equal(new[] { new FlagFilterGroup("sync.", "sync", 1) }, FlagFilterGroups.Derive(new string?[] { null, "sync.reconcile" }!));
    private static string[] Keys() => new[] { "analysis.cut-lab.commander-floors", "analysis.cut-lab.functional-twins", "analysis.manabase.accuracy", "analysis.wincon-map", "service.harvest-cron.enabled", "service.scryfall-tagger.enabled", "sync.reconcile" };
}
