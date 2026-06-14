using System.Data;
using System.Globalization;
using System.Threading;
using Dapper;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Storage;

/// <summary>
/// Registers the global Dapper type handlers used by the relational stores.
/// </summary>
public static class DapperTypeHandlers
{
    private static int _registered;

    /// <summary>
    /// Registers provider-aware handlers exactly once for both supported database providers.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        // Why: D-07 requires the built-in primitive maps to be removed first so the
        // handler write path is the default binder on SQLite and Postgres.
        SqlMapper.RemoveTypeMap(typeof(DateTime));
        SqlMapper.RemoveTypeMap(typeof(DateTime?));
        SqlMapper.AddTypeHandler(new DateTimeTypeHandler());

        SqlMapper.RemoveTypeMap(typeof(decimal));
        SqlMapper.RemoveTypeMap(typeof(decimal?));
        SqlMapper.AddTypeHandler(new DecimalTypeHandler());

        SqlMapper.RemoveTypeMap(typeof(bool));
        SqlMapper.RemoveTypeMap(typeof(bool?));
        SqlMapper.AddTypeHandler(new BoolTypeHandler());

        SqlMapper.RemoveTypeMap(typeof(Guid));
        SqlMapper.RemoveTypeMap(typeof(Guid?));
        SqlMapper.AddTypeHandler(new GuidTypeHandler());

        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset?));
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());

        // Why: D-01 prefers one global underscore mapping rule over per-query aliases.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    internal static DateTime NormalizeUtc(DateTime value)
        => DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc);
}

internal sealed class DateTimeTypeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override DateTime Parse(object value)
        => value switch
        {
            DateTime dt => DapperTypeHandlers.NormalizeUtc(dt),
            DateTimeOffset dto => DapperTypeHandlers.NormalizeUtc(dto.UtcDateTime),
            string text => DapperTypeHandlers.NormalizeUtc(
                DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)),
            _ => DapperTypeHandlers.NormalizeUtc(
                Convert.ToDateTime(value, CultureInfo.InvariantCulture))
        };

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        var normalized = DapperTypeHandlers.NormalizeUtc(value);
        parameter.Value = parameter is SqliteParameter
            ? normalized.ToString("O", CultureInfo.InvariantCulture)
            : normalized;
    }
}

internal sealed class DecimalTypeHandler : SqlMapper.TypeHandler<decimal>
{
    public override decimal Parse(object value)
        => value switch
        {
            decimal decimalValue => decimalValue,
            double doubleValue => Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture),
            float floatValue => Convert.ToDecimal(floatValue, CultureInfo.InvariantCulture),
            string text => decimal.Parse(text, CultureInfo.InvariantCulture),
            _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
        };

    public override void SetValue(IDbDataParameter parameter, decimal value)
    {
        parameter.Value = parameter is SqliteParameter
            ? value.ToString(CultureInfo.InvariantCulture)
            : value;
    }
}

internal sealed class BoolTypeHandler : SqlMapper.TypeHandler<bool>
{
    public override bool Parse(object value)
        => value switch
        {
            bool boolValue => boolValue,
            long longValue => longValue != 0,
            int intValue => intValue != 0,
            short shortValue => shortValue != 0,
            string text => text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase),
            _ => Convert.ToBoolean(value, CultureInfo.InvariantCulture)
        };

    public override void SetValue(IDbDataParameter parameter, bool value)
    {
        parameter.Value = parameter is SqliteParameter
            ? (value ? 1 : 0)
            : value;
    }
}

internal sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override Guid Parse(object value)
        => value switch
        {
            Guid guidValue => guidValue,
            string text => Guid.Parse(text),
            _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to {nameof(Guid)}.")
        };

    public override void SetValue(IDbDataParameter parameter, Guid value)
    {
        parameter.Value = parameter is SqliteParameter
            ? value.ToString()
            : value;
    }
}

internal sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value)
        => value switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime(),
            DateTime dateTime => new(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc), TimeSpan.Zero),
            string text => ParseOffsetString(text),
            _ => new DateTimeOffset(Convert.ToDateTime(value, CultureInfo.InvariantCulture), TimeSpan.Zero)
        };

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        var normalized = value.ToUniversalTime();
        parameter.Value = parameter is SqliteParameter
            ? normalized.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
            : normalized.UtcDateTime;
    }

    private static DateTimeOffset ParseOffsetString(string text)
    {
        if (DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var roundTripValue))
        {
            return roundTripValue.ToUniversalTime();
        }

        return DateTimeOffset.Parse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).ToUniversalTime();
    }
}
