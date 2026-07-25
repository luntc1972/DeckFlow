using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// <see cref="CardTypeLine"/> — the canonical front-face reader shared by the plan-role gate and the
/// land-type test. The front face (before <c>//</c>) decides permanent-ness.
/// </summary>
public sealed class CardTypeLineTests
{
    [Theory]
    [InlineData("Creature — Giant // Instant — Adventure", "Creature — Giant")]
    [InlineData("Instant // Land", "Instant")]
    [InlineData("Legendary Creature — Human", "Legendary Creature — Human")]
    [InlineData(null, "")]
    public void FrontFace_TakesBeforeSlashSlash_Trimmed(string? typeLine, string expected)
        => Assert.Equal(expected, CardTypeLine.FrontFace(typeLine));

    [Theory]
    [InlineData("Instant", true)]
    [InlineData("Sorcery", true)]
    [InlineData("Instant // Land", true)]                        // spell/land MDFC: instant front
    [InlineData("Creature — Giant // Instant — Adventure", false)] // Adventure: permanent creature front
    [InlineData("Legendary Creature — Human", false)]
    [InlineData("Artifact", false)]
    [InlineData("Enchantment", false)]
    [InlineData("Land", false)]
    [InlineData(null, false)]
    public void IsNonPermanentFront_JudgesFrontFaceOnly(string? typeLine, bool expected)
        => Assert.Equal(expected, CardTypeLine.IsNonPermanentFront(typeLine));

    [Theory]
    [InlineData("Legendary Creature — Elf Warrior", "Creature")]
    [InlineData("Artifact Creature — Golem", "Creature")]
    [InlineData("Basic Land — Forest", "Land")]
    [InlineData("Sorcery", "Sorcery")]
    [InlineData("Enchantment — Aura", "Enchantment")]
    [InlineData("Creature — Elf // Instant — Adventure", "Creature")]
    [InlineData(null, "Other")]
    [InlineData("", "Other")]
    public void PrimaryType_UsesFrontFacePriorityBuckets(string? typeLine, string expected)
        => Assert.Equal(expected, CardTypeLine.PrimaryType(typeLine));
}
