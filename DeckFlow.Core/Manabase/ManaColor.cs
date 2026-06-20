namespace DeckFlow.Core.Manabase;

/// <summary>
/// A single mana color (or colorless), used for source-counting and pip requirements.
/// </summary>
public enum ManaColor
{
    /// <summary>White ({W}).</summary>
    White,

    /// <summary>Blue ({U}).</summary>
    Blue,

    /// <summary>Black ({B}).</summary>
    Black,

    /// <summary>Red ({R}).</summary>
    Red,

    /// <summary>Green ({G}).</summary>
    Green,

    /// <summary>Generic colorless / {C} (snow is modeled as its own color too).</summary>
    Colorless,
}
