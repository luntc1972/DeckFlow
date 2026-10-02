namespace DeckFlow.Web.Services.Harvest;

/// <summary>Formats UTC instants for the harvest operator's Mountain time zone.</summary>
public static class MountainTimeFormatter
{
    private static readonly TimeZoneInfo MountainTimeZone = ResolveMountainTimeZone();

    /// <summary>Formats an instant with an unambiguous Mountain time abbreviation.</summary>
    public static string Format(DateTimeOffset instant)
    {
        var converted = TimeZoneInfo.ConvertTime(instant, MountainTimeZone);
        var abbreviation = MountainTimeZone.Id == "UTC"
            ? "UTC"
            : MountainTimeZone.IsDaylightSavingTime(converted) ? "MDT" : "MST";

        return $"{converted:yyyy-MM-dd HH:mm:ss} {abbreviation}";
    }

    private static TimeZoneInfo ResolveMountainTimeZone()
    {
        // Why: the operator reads the log in Mountain time; America/Denver keeps DST correct.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Denver");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
