namespace DeckFlow.Web.Models;

/// <summary>Time window filter used for cEDH metagame statistics.</summary>
public enum CedhMetaTimePeriod
{
    /// <summary>Use tournament results from the last month.</summary>
    ONE_MONTH = 0,
    /// <summary>Use tournament results from the last three months.</summary>
    THREE_MONTHS = 1,
    /// <summary>Use tournament results from the last six months.</summary>
    SIX_MONTHS = 2,
    /// <summary>Use tournament results from the last year.</summary>
    ONE_YEAR = 3,
    /// <summary>Use all available tournament results.</summary>
    ALL_TIME = 4,
    /// <summary>Use tournament results from the current post-ban metagame.</summary>
    POST_BAN = 5,
}
