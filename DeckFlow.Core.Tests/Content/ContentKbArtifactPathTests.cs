using DeckFlow.Core.Content;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for the shared <see cref="ContentKbArtifactPath"/> guard.
/// </summary>
public sealed class ContentKbArtifactPathTests
{
    [Fact]
    public void TryResolveContained_ValidContentKbPath_ReturnsContainedFullPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "artifact-path-safety-tests", Path.GetRandomFileName());

        var result = ContentKbArtifactPath.TryResolveContained(root, "content-kb/creator/video.md", out var resolved);

        Assert.True(result);
        Assert.StartsWith(Path.GetFullPath(root), resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("content-kb", "creator", "video.md"), resolved, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\config")]
    [InlineData("\\\\server\\share\\file.md")]
    public void TryResolveContained_RootedPath_Rejected(string artifactPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "artifact-path-safety-tests", Path.GetRandomFileName());

        var result = ContentKbArtifactPath.TryResolveContained(root, artifactPath, out var resolved);

        Assert.False(result);
        Assert.Equal(string.Empty, resolved);
    }

    [Theory]
    [InlineData("content-kb/../../../etc/passwd")]
    [InlineData("content-kb/creator/../../secret.md")]
    public void TryResolveContained_TraversalPath_Rejected(string artifactPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "artifact-path-safety-tests", Path.GetRandomFileName());

        var result = ContentKbArtifactPath.TryResolveContained(root, artifactPath, out var resolved);

        Assert.False(result);
        Assert.Equal(string.Empty, resolved);
    }

    [Theory]
    [InlineData("other-dir/creator/video.md")]
    [InlineData("video.md")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolveContained_NonContentKbPrefix_Rejected(string artifactPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "artifact-path-safety-tests", Path.GetRandomFileName());

        var result = ContentKbArtifactPath.TryResolveContained(root, artifactPath, out var resolved);

        Assert.False(result);
        Assert.Equal(string.Empty, resolved);
    }

    [Fact]
    public void TryResolveContained_CaseInsensitiveContainment_StillResolves()
    {
        var root = Path.Combine(Path.GetTempPath(), "ARTIFACT-PATH-SAFETY-TESTS", Path.GetRandomFileName());

        var result = ContentKbArtifactPath.TryResolveContained(root.ToLowerInvariant(), "content-kb/creator/video.md", out var resolved);

        Assert.True(result);
        Assert.NotEqual(string.Empty, resolved);
    }

    [Theory]
    [InlineData("content-kb/creator/video.md", true)]
    [InlineData("content-kb/video.md", true)]
    [InlineData("/content-kb/video.md", false)]
    [InlineData("C:\\x", false)]
    [InlineData("\\content-kb\\video.md", false)]
    [InlineData("content-kb/../x", false)]
    [InlineData("other/content-kb/video.md", false)]
    [InlineData("content-kb", false)]
    public void IsSafe_ReturnsExpected(string artifactPath, bool expected)
    {
        Assert.Equal(expected, ContentKbArtifactPath.IsSafe(artifactPath));
    }
}
