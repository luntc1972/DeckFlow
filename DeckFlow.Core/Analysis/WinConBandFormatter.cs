namespace DeckFlow.Core.Analysis;

/// <summary>
/// Maps a <see cref="WinConBand"/> to its coarse assembly-speed label. Shared by the prompt-text
/// builder and the on-page readout so the WINCON-03 hedge wording ("early"/"mid"/"late", never a
/// turn-number claim) stays byte-identical across both surfaces and cannot silently drift.
/// </summary>
public static class WinConBandFormatter
{
    /// <summary>
    /// Returns the coarse assembly-speed label for <paramref name="band"/>. Never a turn number.
    /// </summary>
    /// <param name="band">The coarse assembly band.</param>
    /// <returns>"early", "mid", "late", or "an unknown point" for <see cref="WinConBand.Unknown"/>.</returns>
    public static string Label(WinConBand band)
        => band switch
        {
            WinConBand.Early => "early",
            WinConBand.Mid => "mid",
            WinConBand.Late => "late",
            _ => "an unknown point",
        };
}
