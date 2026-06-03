namespace DeckFlow.Web.Models;

/// <summary>View model for the deck format conversion workflow.</summary>
public sealed class DeckConvertViewModel
{
    /// <summary>Deck workflow tab that should render as active.</summary>
    public DeckPageTab ActiveTab { get; init; } = DeckPageTab.Convert;
    /// <summary>Conversion form input echoed back to the Razor view.</summary>
    public DeckConvertRequest Request { get; init; } = new();
    /// <summary>Converted deck text produced by the conversion service.</summary>
    public string? ConvertedText { get; init; }
    /// <summary>Error message shown when conversion fails.</summary>
    public string? ErrorMessage { get; init; }
    /// <summary>Whether conversion requires an explicit commander name before it can continue.</summary>
    public bool MissingCommander { get; init; }
}
