using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Unit tests for the shared <see cref="ArtifactPathSafety"/> guard (90-CONTEXT.md D-11 / T-90-05).
/// Extracted verbatim from PullFromProdCoordinator's former private copy — these tests lock the
/// same behavior in the new shared location: valid content-kb paths resolve and are contained,
/// rooted/traversal/wrong-prefix paths are rejected, and containment is case-insensitive.
/// </summary>
public sealed class ArtifactPathSafetyTests
{
    [Fact]
    public void TryBuildContainedPath_ValidContentKbPath_ReturnsContainedFullPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "artifact-path-safety-tests", Path.GetRandomFileName());

        var result = ArtifactPathSafety.TryBuildContainedPath(root, "content-kb/creator/video.md", out var resolved);

        Assert.True(result);
        Assert.StartsWith(Path.GetFullPath(root), resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("content-kb", "creator", "video.md"), resolved, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\config")]
    [InlineData("\\\\server\\share\\file.md")]
    public void TryBuildContainedPath_RootedPath_Rejected(string artifactPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "artifact-path-safety-tests", Path.GetRandomFileName());

        var result = ArtifactPathSafety.TryBuildContainedPath(root, artifactPath, out var resolved);

        Assert.False(result);
        Assert.Equal(string.Empty, resolved);
    }

    [Theory]
    [InlineData("content-kb/../../../etc/passwd")]
    [InlineData("content-kb/creator/../../secret.md")]
    public void TryBuildContainedPath_TraversalPath_Rejected(string artifactPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "artifact-path-safety-tests", Path.GetRandomFileName());

        var result = ArtifactPathSafety.TryBuildContainedPath(root, artifactPath, out var resolved);

        Assert.False(result);
        Assert.Equal(string.Empty, resolved);
    }

    [Theory]
    [InlineData("other-dir/creator/video.md")]
    [InlineData("video.md")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryBuildContainedPath_NonContentKbPrefix_Rejected(string artifactPath)
    {
        var root = Path.Combine(Path.GetTempPath(), "artifact-path-safety-tests", Path.GetRandomFileName());

        var result = ArtifactPathSafety.TryBuildContainedPath(root, artifactPath, out var resolved);

        Assert.False(result);
        Assert.Equal(string.Empty, resolved);
    }

    [Fact]
    public void TryBuildContainedPath_CaseInsensitiveContainment_StillResolves()
    {
        var root = Path.Combine(Path.GetTempPath(), "ARTIFACT-PATH-SAFETY-TESTS", Path.GetRandomFileName());

        var result = ArtifactPathSafety.TryBuildContainedPath(root.ToLowerInvariant(), "content-kb/creator/video.md", out var resolved);

        Assert.True(result);
        Assert.NotEqual(string.Empty, resolved);
    }

    [Theory]
    [InlineData("content-kb/creator/video.md", true)]
    [InlineData("content-kb/video.md", true)]
    [InlineData("/content-kb/video.md", false)]
    [InlineData("content-kb\\video.md", false)]
    [InlineData("other/content-kb/video.md", false)]
    [InlineData("content-kb", false)]
    public void IsSafeArtifactPath_ReturnsExpected(string artifactPath, bool expected)
    {
        Assert.Equal(expected, ArtifactPathSafety.IsSafeArtifactPath(artifactPath));
    }
}
