namespace DeckFlow.Web.Models;

/// <summary>
/// Controls the formatting style requested for generated deck primers.
/// </summary>
public enum PrimerOutputStyle
{
    /// <summary>
    /// Uses the existing standard primer formatting.
    /// </summary>
    Standard,

    /// <summary>
    /// Requests richer Moxfield-paste-ready markdown with structured visual elements.
    /// </summary>
    MoxfieldRich,

    /// <summary>
    /// Requests the richest cEDH primer output with Moxfield-style formatting, deeper competitive guidance, and full cEDH section coverage.
    /// </summary>
    FullCedh
}
