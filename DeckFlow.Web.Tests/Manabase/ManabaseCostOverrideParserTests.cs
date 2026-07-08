using DeckFlow.Web.Services.Manabase;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Validates <see cref="ManabaseCostOverrideParser"/>: braced and shorthand costs, split/DFC
/// names, and tolerant handling of blank/garbage lines.
/// </summary>
public sealed class ManabaseCostOverrideParserTests
{
    [Theory]
    [InlineData("Force of Will: 0", "Force of Will", "0")]
    [InlineData("Blasphemous Act: {R}", "Blasphemous Act", "{R}")]
    [InlineData("Grief: R", "Grief", "{R}")]               // bare letter -> braced
    [InlineData("Some Spell: 1R", "Some Spell", "{1}{R}")] // shorthand -> braced
    [InlineData("Fire // Ice: 2", "Fire // Ice", "{2}")]   // split/DFC name survives
    public void Parse_ValidLine_YieldsBracedCost(string line, string name, string cost)
    {
        var map = ManabaseCostOverrideParser.Parse(line);

        Assert.True(map.TryGetValue(name, out string? actual));
        Assert.Equal(cost, actual);
    }

    [Fact]
    public void Parse_IgnoresGarbageLines_KeepsValidOnes()
    {
        // No colon, empty name, empty cost, and unparseable cost are all skipped; the real line stays.
        var map = ManabaseCostOverrideParser.Parse(
            "Garbage Line No Colon\n: 0\nNo Cost Here: \nJunk Card: zzqq\nReal Card: 0");

        Assert.Single(map);
        Assert.True(map.ContainsKey("Real Card"));
        Assert.Equal("0", map["Real Card"]);
    }

    [Fact]
    public void Parse_NullOrBlank_ReturnsEmpty()
    {
        Assert.Empty(ManabaseCostOverrideParser.Parse(null));
        Assert.Empty(ManabaseCostOverrideParser.Parse("   "));
    }

    [Theory]
    [InlineData("Slash Card: U/R")]   // hybrid shorthand — ambiguous, rejected
    [InlineData("Mixed Card: {1}R")]  // braced + trailing bare — Parse would drop the R
    [InlineData("Junk Card: zzqq")]   // not valid mana symbols
    public void Parse_RejectsAmbiguousOrJunkCost(string line)
    {
        Assert.Empty(ManabaseCostOverrideParser.Parse(line));
    }

    [Fact]
    public void Parse_DuplicateName_LastWins()
    {
        var map = ManabaseCostOverrideParser.Parse("Card: 0\nCard: {R}");

        Assert.Equal("{R}", map["Card"]);
    }

    [Fact]
    public void ParseWithDiagnostics_CapturesMalformedLines_KeepsValid()
    {
        // MEDIUM-11: dropped lines are reported (not silent). No-colon, empty cost, and junk cost
        // are malformed; the real line is accepted. Blank lines never count as malformed.
        var result = ManabaseCostOverrideParser.ParseWithDiagnostics(
            "Garbage Line No Colon\n\nNo Cost Here: \nJunk Card: zzqq\nReal Card: 0");

        Assert.Single(result.Overrides);
        Assert.Equal("0", result.Overrides["Real Card"]);
        Assert.Equal(
            new[] { "Garbage Line No Colon", "No Cost Here:", "Junk Card: zzqq" },
            result.MalformedLines);
    }

    [Fact]
    public void ParseWithDiagnostics_AllValid_NoMalformed()
    {
        var result = ManabaseCostOverrideParser.ParseWithDiagnostics("Force of Will: 0\nBlasphemous Act: {R}");

        Assert.Equal(2, result.Overrides.Count);
        Assert.Empty(result.MalformedLines);
    }

    [Fact]
    public void ParseWithDiagnostics_NullOrBlank_EmptyBoth()
    {
        var result = ManabaseCostOverrideParser.ParseWithDiagnostics("   ");

        Assert.Empty(result.Overrides);
        Assert.Empty(result.MalformedLines);
    }
}
