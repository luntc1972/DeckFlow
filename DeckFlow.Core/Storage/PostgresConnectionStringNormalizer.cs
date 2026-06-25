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

        // Why: the URL form is the managed-Postgres (Render) connection string, and Render rejects
        // non-SSL connections (28000: SSL/TLS required). Default to Require so a string that omits
        // sslmode still connects; an explicit ?sslmode= in the query below overrides this.
        // TrustServerCertificate is also defaulted on because Npgsql's Require validates the server
        // chain and Render's managed-PG endpoint trips that validation (handshake drop /
        // EndOfStreamException) — the documented Render + Npgsql string pairs Require with trust.
        // Traffic stays encrypted; only chain validation is skipped (accepted Render norm).
        builder.SslMode = SslMode.Require;
        // CS0618: TrustServerCertificate is a documented no-op on this Npgsql (SslMode.Require already
        // skips chain validation). Keep the assignment behind a pragma so the emitted string stays
        // byte-identical to the documented Render pairing rather than silently dropping the key.
#pragma warning disable CS0618
        builder.TrustServerCertificate = true;
#pragma warning restore CS0618

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
