using Npgsql;

namespace DeckFlow.Core.Storage;

/// <summary>
/// Normalizes Postgres connection strings so URL forms can be consumed by Npgsql relational connections.
/// </summary>
public static class PostgresConnectionStringNormalizer
{
    /// <summary>
    /// Converts supported Postgres URL connection strings into the Npgsql key-value form and otherwise returns the input unchanged.
    /// </summary>
    /// <param name="raw">Raw Postgres connection string in URL or key-value format.</param>
    /// <returns>The normalized Npgsql connection string.</returns>
    public static string Normalize(string raw)
    {
        if (!raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            && !raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }

        var uri = new Uri(raw, UriKind.Absolute);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))
        };

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separatorIndex = uri.UserInfo.IndexOf(':');
            if (separatorIndex >= 0)
            {
                builder.Username = Uri.UnescapeDataString(uri.UserInfo[..separatorIndex]);
                builder.Password = Uri.UnescapeDataString(uri.UserInfo[(separatorIndex + 1)..]);
            }
            else
            {
                builder.Username = Uri.UnescapeDataString(uri.UserInfo);
            }
        }

        var query = uri.Query;
        if (!string.IsNullOrEmpty(query))
        {
            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var equalsIndex = pair.IndexOf('=');
                var key = equalsIndex >= 0 ? pair[..equalsIndex] : pair;
                var value = equalsIndex >= 0 ? pair[(equalsIndex + 1)..] : string.Empty;

                if (!key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!Enum.TryParse<SslMode>(Uri.UnescapeDataString(value), ignoreCase: true, out var sslMode))
                {
                    throw new InvalidOperationException($"Unsupported sslmode value '{Uri.UnescapeDataString(value)}'.");
                }

                builder.SslMode = sslMode;
            }
        }

        return builder.ConnectionString;
    }
}
