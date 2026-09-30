using System.Globalization;

namespace DeckFlow.Core.Content;

/// <summary>Converts provider-specific insert results into stable content row IDs for relational stores.</summary>
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
