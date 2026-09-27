using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>Tests lazy private-KB-root resolution for Studio content surfaces.</summary>
public sealed class PrivateRootProviderTests
{
    [Fact]
    public void GetRoot_UnsetSetting_ThrowsNamedConfigurationError()
    {
        var provider = new StudioPrivateKbRootProvider(null, null);

        var exception = Assert.Throws<InvalidOperationException>(provider.GetRoot);

        Assert.Contains("DECKFLOW_KB_ROOT", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRoot_ConfiguredPrivateRoot_ReturnsConfiguredRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "deckflow-private-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var provider = new StudioPrivateKbRootProvider(null, root);

            Assert.Equal(Path.GetFullPath(root), provider.GetRoot().Root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PrivateRootEnvironment_ValidEnvironmentRoot_WinsOverConfiguredRoot()
    {
        var environmentRoot = Path.Combine(Path.GetTempPath(), "deckflow-private-env-" + Guid.NewGuid().ToString("N"));
        var configuredRoot = Path.Combine(Path.GetTempPath(), "deckflow-private-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(environmentRoot);
        Directory.CreateDirectory(configuredRoot);
        try
        {
            var provider = new StudioPrivateKbRootProvider(environmentRoot, configuredRoot);

            Assert.Equal(Path.GetFullPath(environmentRoot), provider.GetRoot().Root);
        }
        finally
        {
            Directory.Delete(environmentRoot, recursive: true);
            Directory.Delete(configuredRoot, recursive: true);
        }
    }

    [Fact]
    public void PrivateRootEnvironment_WhitespaceEnvironmentRoot_FallsBackToConfiguredRoot()
    {
        var configuredRoot = Path.Combine(Path.GetTempPath(), "deckflow-private-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configuredRoot);
        try
        {
            var provider = new StudioPrivateKbRootProvider("  ", configuredRoot);

            Assert.Equal(Path.GetFullPath(configuredRoot), provider.GetRoot().Root);
        }
        finally
        {
            Directory.Delete(configuredRoot, recursive: true);
        }
    }
}
