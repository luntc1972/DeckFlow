using DeckFlow.Core.Content;

namespace DeckFlow.Core.Tests;

public sealed class PrivateRootTests
{
    [Fact]
    public void Constructor_UnsetRoot_ThrowsNamingEnvironmentVariable()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(null));

        Assert.Contains(PrivateKbRoot.EnvironmentVariableName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_EmptyRoot_ThrowsNamingEnvironmentVariable()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(string.Empty));

        Assert.Contains(PrivateKbRoot.EnvironmentVariableName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ValidRoot_ResolvesAllHelperPaths()
    {
        using var root = new TempDirectory();

        var result = new PrivateKbRoot(root.Path);

        Assert.Equal(Path.GetFullPath(root.Path), result.Root);
        Assert.Equal(Path.Combine(result.Root, "content-kb"), result.ContentKbDir);
        Assert.Equal(Path.Combine(result.Root, "content-kb", "seed", "index-seed.json"), result.SeedFile);
        Assert.Equal(Path.Combine(result.Root, "creator-style-seed"), result.CreatorStyleSeedDir);
    }

    [Fact]
    public void Constructor_RootInsideCheckoutWithGitDirectory_Throws()
    {
        using var root = new TempDirectory();
        File.WriteAllText(Path.Combine(root.Path, "DeckFlow.sln"), string.Empty);
        Directory.CreateDirectory(Path.Combine(root.Path, ".git"));
        var nestedRoot = Directory.CreateDirectory(Path.Combine(root.Path, "artifacts"));

        Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(nestedRoot.FullName));
    }

    [Fact]
    public void Constructor_RootInsideCheckoutWithGitWorktreeFile_Throws()
    {
        using var root = new TempDirectory();
        File.WriteAllText(Path.Combine(root.Path, "DeckFlow.sln"), string.Empty);
        File.WriteAllText(Path.Combine(root.Path, ".git"), "gitdir: elsewhere");

        Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(root.Path));
    }

    [Fact]
    public void Constructor_RootWithStraySolutionAndNoGit_AcceptsRoot()
    {
        using var root = new TempDirectory();
        File.WriteAllText(Path.Combine(root.Path, "DeckFlow.sln"), string.Empty);

        var result = new PrivateKbRoot(root.Path);

        Assert.Equal(Path.GetFullPath(root.Path), result.Root);
    }

    [Fact]
    public void Constructor_SymlinkOutsideCheckoutPointingIntoCheckout_Throws()
    {
        using var checkout = new TempDirectory();
        using var outside = new TempDirectory();
        File.WriteAllText(Path.Combine(checkout.Path, "DeckFlow.sln"), string.Empty);
        Directory.CreateDirectory(Path.Combine(checkout.Path, ".git"));
        var target = Directory.CreateDirectory(Path.Combine(checkout.Path, "artifacts"));
        var linkPath = Path.Combine(outside.Path, "artifacts-link");
        Directory.CreateSymbolicLink(linkPath, target.FullName);

        Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(linkPath));
    }

    [Fact]
    public void Constructor_DeepPathWithLinkPastFortySixSegments_Throws()
    {
        using var outside = new TempDirectory();
        var checkout = Directory.CreateDirectory(Path.Combine(outside.Path, "c"));
        File.WriteAllText(Path.Combine(checkout.FullName, "DeckFlow.sln"), string.Empty);
        Directory.CreateDirectory(Path.Combine(checkout.FullName, ".git"));
        var target = Directory.CreateDirectory(Path.Combine(checkout.FullName, "kb", "child"));
        var deepPath = outside.Path;
        for (var index = 0; index < 50; index++)
        {
            deepPath = Path.Combine(deepPath, $"d{index}");
            Directory.CreateDirectory(deepPath);
        }

        var link = Path.Combine(deepPath, "link");
        CreateSymbolicLinkOrSkip(link, Path.Combine(target.Parent!.FullName));

        var exception = Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(Path.Combine(link, "child")));

        Assert.Contains("must not be inside a DeckFlow product checkout", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ChainedSymlinkIntoCheckout_Throws()
    {
        using var checkout = new TempDirectory();
        using var outside = new TempDirectory();
        File.WriteAllText(Path.Combine(checkout.Path, "DeckFlow.sln"), string.Empty);
        Directory.CreateDirectory(Path.Combine(checkout.Path, ".git"));
        var inner = Directory.CreateDirectory(Path.Combine(checkout.Path, "inner"));
        var junction = Path.Combine(outside.Path, "j1");
        CreateSymbolicLinkOrSkip(junction, inner.FullName);
        var link = Path.Combine(outside.Path, "link");
        Directory.CreateDirectory(Path.Combine(inner.FullName, "kb"));
        CreateSymbolicLinkOrSkip(link, Path.Combine(junction, "kb"));

        Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(link));
    }

    [Fact]
    public void Constructor_RelativeDotDotLinkThroughCheckoutLink_Throws()
    {
        using var outside = new TempDirectory();
        var checkout = Path.Combine(outside.Path, "checkout");
        Directory.CreateDirectory(checkout);
        File.WriteAllText(Path.Combine(checkout, "DeckFlow.sln"), string.Empty);
        Directory.CreateDirectory(Path.Combine(checkout, ".git"));
        Directory.CreateDirectory(Path.Combine(checkout, "kb2"));
        Directory.CreateDirectory(Path.Combine(checkout, "child"));
        var checkoutLink = Path.Combine(outside.Path, "j1");
        var relativeDotDotLink = Path.Combine(outside.Path, "reldots");
        CreateSymbolicLinkOrSkip(checkoutLink, Path.Combine(checkout, "child"));
        // Why: Windows resolves link-target .. lexically, so the resolver is deliberately stricter there (fails closed).
        CreateSymbolicLinkOrSkip(relativeDotDotLink, "j1/../kb2");

        var exception = Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(relativeDotDotLink));

        Assert.Contains(PrivateKbRoot.EnvironmentVariableName, exception.Message, StringComparison.Ordinal);
        Assert.Contains("must not be inside a DeckFlow product checkout", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RelativeLinkToOutsideDirectory_ResolvesAgainstLinkParent()
    {
        using var outside = new TempDirectory();
        var expectedRoot = Path.Combine(outside.Path, "real", "kb");
        Directory.CreateDirectory(expectedRoot);
        var relativeLink = Path.Combine(outside.Path, "rel");
        CreateSymbolicLinkOrSkip(relativeLink, "real/kb");

        var root = new PrivateKbRoot(relativeLink);

        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        Assert.True(string.Equals(Path.GetFullPath(expectedRoot), root.Root, comparison));
    }

    [Fact]
    public void Constructor_SymlinkCycle_ThrowsInvalidOperation()
    {
        using var root = new TempDirectory();
        var firstLink = Path.Combine(root.Path, "a");
        var secondLink = Path.Combine(root.Path, "b");
        var firstLinkCreated = false;
        var secondLinkCreated = false;
        try
        {
            CreateSymbolicLinkOrSkip(firstLink, "b");
            firstLinkCreated = true;
            CreateSymbolicLinkOrSkip(secondLink, "a");
            secondLinkCreated = true;

            var exception = Assert.Throws<InvalidOperationException>(() => new PrivateKbRoot(firstLink));

            Assert.Contains(PrivateKbRoot.EnvironmentVariableName, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (secondLinkCreated)
            {
                DeleteLink(secondLink);
            }

            if (firstLinkCreated)
            {
                DeleteLink(firstLink);
            }
        }
    }

    private static void DeleteLink(string linkPath)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.Delete(linkPath);
        }
        else
        {
            File.Delete(linkPath);
        }
    }

    private static void CreateSymbolicLinkOrSkip(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            throw Xunit.Sdk.SkipException.ForSkip("This operating system cannot create directory symbolic links for the chained-link test.");
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
