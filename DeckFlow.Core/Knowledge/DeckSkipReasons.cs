namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Stable reasons recorded when a queued deck is skipped.
/// </summary>
public static class DeckSkipReasons
{
    /// <summary>Written by the legality gate when deck format is not Commander.</summary>
    public const string NotCommanderFormat = "not_commander_format";

    /// <summary>Written by the legality gate when no commander is present.</summary>
    public const string NoCommander = "no_commander";

    /// <summary>Written by the legality gate when deck size is invalid.</summary>
    public const string WrongSize = "wrong_size";

    /// <summary>Written for a transient or import failure.</summary>
    public const string FetchFailure = "fetch_failure";

    /// <summary>Written by retention processing.</summary>
    public const string Retention = "retention";
}
