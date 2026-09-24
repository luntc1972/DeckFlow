namespace DeckFlow.Web.Models;

/// <summary>
/// Represents the values and per-page identifiers rendered by the shared deck import control.
/// </summary>
/// <param name="IdPrefix">Prefix for form control identifiers.</param>
/// <param name="PanelKeyPrefix">Prefix for synchronized panel keys.</param>
/// <param name="TextLabel">Label displayed for the pasted deck text input.</param>
/// <param name="DeckUrl">Optional public deck URL value.</param>
/// <param name="DeckText">Optional pasted deck text value.</param>
/// <param name="Source">Selected deck input source.</param>
public sealed record DeckImportControlModel(
    string IdPrefix,
    string PanelKeyPrefix,
    string TextLabel,
    string? DeckUrl = null,
    string? DeckText = null,
    DeckInputSource Source = DeckInputSource.PublicUrl);
