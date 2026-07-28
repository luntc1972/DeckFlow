using System;
using DeckFlow.Core.Normalization;
using Xunit;

namespace DeckFlow.Core.Tests.Normalization;

public sealed class ScryfallCollectionIdentifierTests
{
    [Theory]
    [InlineData("Fire // Ice", "Fire")]
    [InlineData("Fire / Ice", "Fire")]
    [InlineData("Fire//Ice", "Fire")]
    [InlineData("Delver of Secrets // Insectile Aberration", "Delver of Secrets")]
    [InlineData("Agadeem's Awakening // Agadeem, the Undercrypt", "Agadeem's Awakening")]
    [InlineData("Kellan, Inquisitive Prodigy // Tail the Suspect", "Kellan, Inquisitive Prodigy")]
    [InlineData("Who // What // When // Where // Why", "Who")]
    [InlineData("Sol Ring", "Sol Ring")]
    [InlineData("Ach! Hans, Run!", "Ach! Hans, Run!")]
    [InlineData("  Fire // Ice  ", "Fire")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void ToFaceIdentifier_ReturnsExpectedFaceName(string cardName, string expected)
    {
        string identifier = ScryfallCollectionIdentifier.ToFaceIdentifier(cardName);

        Assert.Equal(expected, identifier);
    }

    [Fact]
    public void ToFaceIdentifier_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ScryfallCollectionIdentifier.ToFaceIdentifier(null!));
    }

    [Fact]
    public void ToFaceIdentifier_PreservesCaseAndPunctuation_IsNotCardNormalizerNormalize()
    {
        const string cardName = "Agadeem's Awakening // X";

        string identifier = ScryfallCollectionIdentifier.ToFaceIdentifier(cardName);
        string normalized = CardNormalizer.Normalize(cardName);

        Assert.Equal("Agadeem's Awakening", identifier);
        Assert.NotEqual(normalized, identifier);
    }
}
