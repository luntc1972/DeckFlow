using System;
using System.Collections.Generic;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Coverage for <see cref="ContentSiteIndexContentSignature.ComputeBodySha256"/> (D-01/D-02)
/// and the body-hash-inclusive <see cref="ContentSiteIndexContentSignature.BuildSignature"/> (D-03).
/// </summary>
public sealed class ContentSiteIndexContentSignatureTests
{
    private static ContentSiteIndexRow Row(
        string title = "Title",
        string? bodySha256 = null) =>
        new()
        {
            Id = 1,
            Source = "youtube",
            Title = title,
            VideoUrl = "https://example.com/v",
            ArtifactPath = "content-kb/slug/yt-1.md",
            IndexedUtc = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            BodySha256 = bodySha256,
        };

    private const string RawWithLf = "---\nsource: \"Test\"\n---\nline one\nline two\n";
    private const string RawWithCrLf = "---\nsource: \"Test\"\n---\r\nline one\r\nline two\r\n";

    [Fact]
    public void ComputeBodySha256_LfAndCrLfBodies_ProduceIdenticalHash()
    {
        var lfHash = ContentSiteIndexContentSignature.ComputeBodySha256(RawWithLf);
        var crlfHash = ContentSiteIndexContentSignature.ComputeBodySha256(RawWithCrLf);

        Assert.Equal(lfHash, crlfHash);
    }

    [Fact]
    public void ComputeBodySha256_DifferingNonEolBytes_ProduceDifferentHash()
    {
        const string rawA = "---\nsource: \"Test\"\n---\nSame body, one word differs: alpha\n";
        const string rawB = "---\nsource: \"Test\"\n---\nSame body, one word differs: beta\n";

        var hashA = ContentSiteIndexContentSignature.ComputeBodySha256(rawA);
        var hashB = ContentSiteIndexContentSignature.ComputeBodySha256(rawB);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void ComputeBodySha256_EmptyBody_DoesNotThrowAndIsStable()
    {
        const string rawEmptyBody = "---\nsource: \"Test\"\n---\n";

        var first = ContentSiteIndexContentSignature.ComputeBodySha256(rawEmptyBody);
        var second = ContentSiteIndexContentSignature.ComputeBodySha256(rawEmptyBody);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void ComputeBodySha256_EqualsHashOfSplitHeaderBody_NormalizedToLf()
    {
        var (_, body) = ContentArtifactParser.SplitHeader(RawWithCrLf);
        var normalizedBody = body.Replace("\r\n", "\n").Replace("\r", "\n");
        var expected = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedBody)));

        var actual = ContentSiteIndexContentSignature.ComputeBodySha256(RawWithCrLf);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AreContentEqual_DifferingBodySha256_AreNotEqual()
    {
        var a = Row(bodySha256: "a".PadLeft(64, 'a'));
        var b = Row(bodySha256: "b".PadLeft(64, 'b'));

        Assert.False(ContentSiteIndexContentSignature.AreContentEqual(a, b));
    }

    [Fact]
    public void AreContentEqual_EqualBodySha256AndOtherColumns_AreEqual()
    {
        var hash = "c".PadLeft(64, 'c');
        var a = Row(bodySha256: hash);
        var b = Row(bodySha256: hash);

        Assert.True(ContentSiteIndexContentSignature.AreContentEqual(a, b));
    }

    [Fact]
    public void AreContentEqual_NullBodySha256_DoesNotCollideWithRealHash()
    {
        // A real 64-hex-char hash consisting entirely of 'a' must never equal the
        // null-hash sentinel used for legacy rows (T-89-02).
        var realLookingHash = new string('a', 64);
        var withNullHash = Row(bodySha256: null);
        var withRealHash = Row(bodySha256: realLookingHash);

        Assert.False(ContentSiteIndexContentSignature.AreContentEqual(withNullHash, withRealHash));
    }
}
