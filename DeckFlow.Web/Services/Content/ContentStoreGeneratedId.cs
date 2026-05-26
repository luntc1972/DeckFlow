using System.Globalization;

namespace DeckFlow.Web.Services.Content;

internal static class ContentStoreGeneratedId
{
    public static long Read(object? scalar)
    {
        if (scalar is null || scalar == DBNull.Value)
        {
            throw new InvalidOperationException("expected a generated id but the insert returned no row");
        }

        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }
}
