using DeckFlow.Studio.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Covers the private-root boundary for Studio seed membership reads.
/// </summary>
public sealed class StudioSeedKeyMembershipSourcePrivateRootTests
{
    [Fact]
    public void GetSeedMembership_PrivateRoot_ReadsPrivateSeedWithoutGitResolution()
    {
        var root = Path.Combine(Path.GetTempPath(), "studio-seed-membership-tests", Path.GetRandomFileName());
        try
        {
            var seedFile = Path.Combine(root, "content-kb", "seed", "index-seed.json");
            Directory.CreateDirectory(Path.GetDirectoryName(seedFile)!);
            File.WriteAllText(seedFile, "[{\"naturalKeyType\":\"youtube\",\"naturalKeyValue\":\"private-id\"}]");
            var source = new StudioSeedKeyMembershipSource(
                new StudioPrivateKbRootProvider(null, root),
                NullLogger<StudioSeedKeyMembershipSource>.Instance);

            var result = source.GetSeedMembership();

            Assert.True(result.SeedAvailable);
            Assert.Contains("youtube\u0000private-id", result.NaturalKeys);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetSeedMembership_PrivateRootUnset_FailsClosed()
    {
        var source = new StudioSeedKeyMembershipSource(
            new StudioPrivateKbRootProvider(null, null),
            NullLogger<StudioSeedKeyMembershipSource>.Instance);

        var exception = Assert.Throws<InvalidOperationException>(source.GetSeedMembership);

        Assert.Contains("DECKFLOW_KB_ROOT", exception.Message, StringComparison.Ordinal);
    }
}
