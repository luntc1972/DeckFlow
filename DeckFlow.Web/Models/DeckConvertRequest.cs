namespace DeckFlow.Web.Models;

/// <summary>Request model bound from the deck format conversion form.</summary>
public sealed class DeckConvertRequest
{
    /// <summary>Deck format the submitted input should be parsed as.</summary>
    public string SourceFormat { get; set; } = "Moxfield";
    /// <summary>How the source deck input was supplied.</summary>
    public DeckInputSource InputSource { get; set; } = DeckInputSource.PasteText;
    /// <summary>Public deck URL submitted for conversion.</summary>
    public string DeckUrl { get; set; } = string.Empty;
    /// <summary>Raw pasted deck text submitted for conversion.</summary>
    public string DeckText { get; set; } = string.Empty;
    /// <summary>Deck format the converter should produce.</summary>
    public string TargetFormat { get; set; } = "Archidekt";
    /// <summary>Optional commander name used when pasted text omits a commander section.</summary>
    public string? CommanderOverride { get; set; }
}
