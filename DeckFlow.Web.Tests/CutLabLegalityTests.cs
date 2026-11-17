using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabLegality"/> covering legal-multiple recognition and caps.</summary>
public sealed class CutLabLegalityTests
{
    [Theory]
    [InlineData("Plains")]
    [InlineData("Island")]
    [InlineData("Swamp")]
    [InlineData("Mountain")]
    [InlineData("Forest")]
    [InlineData("Snow-Covered Plains")]
    [InlineData("Snow-Covered Island")]
    [InlineData("Snow-Covered Swamp")]
    [InlineData("Snow-Covered Mountain")]
    [InlineData("Snow-Covered Forest")]
    [InlineData("Wastes")]
    [InlineData("Persistent Petitioners")]
    [InlineData("Dragon's Approach")]
    [InlineData("Relentless Rats")]
    [InlineData("Rat Colony")]
    [InlineData("Shadowborn Apostle")]
    [InlineData("Slime Against Humanity")]
    [InlineData("Templar Knight")]
    [InlineData("Nazgûl")]
    [InlineData("Seven Dwarves")]
    public void IsLegalMultiple_RecognizedCards_ReturnsTrue(string name)
    {
        Assert.True(CutLabLegality.IsLegalMultiple(name));
    }

    [Fact]
    public void IsLegalMultiple_NormalSingleton_ReturnsFalse()
    {
        Assert.False(CutLabLegality.IsLegalMultiple("Sol Ring"));
    }

    [Fact]
    public void LegalMax_LegalMultiple_ReturnsExpandedCap()
    {
        Assert.Equal(150, CutLabLegality.LegalMax("Forest"));
        Assert.Equal(150, CutLabLegality.LegalMax("Relentless Rats"));
    }

    [Fact]
    public void LegalMax_Singleton_ReturnsOne()
    {
        Assert.Equal(1, CutLabLegality.LegalMax("Sol Ring"));
    }

    // Why: Typed pool names must retain the legal-multiple cap across case and punctuation variants.
    [Theory]
    [InlineData("plains")]
    [InlineData("SNOW-COVERED ISLAND")]
    [InlineData("snow covered island")]
    [InlineData("Dragon\u2019s Approach")]
    [InlineData("relentless rats")]
    public void IsLegalMultiple_CaseOrPunctuationVariant_ReturnsTrue(string name)
    {
        Assert.True(CutLabLegality.IsLegalMultiple(name));
    }

    // Why: Lowercase typed basics must receive the same copy cap as canonical names.
    [Fact]
    public void LegalMax_LowercaseBasic_MatchesCanonicalCap()
    {
        Assert.Equal(CutLabLegality.LegalMax("Plains"), CutLabLegality.LegalMax("plains"));
    }
}
