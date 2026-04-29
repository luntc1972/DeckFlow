using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace DeckFlow.Web.Services;

public static class DeckFlowDatabaseConnectionFactory
{
    private const string DatabaseProviderEnvVar = "DECKFLOW_DATABASE_PROVIDER";
    private const string DatabaseConnectionStringEnvVar = "DECKFLOW_DATABASE_CONNECTION_STRING";

    public static RelationalDatabaseConnection CreateFeedbackConnection(IWebHostEnvironment environment)
        => CreateConnection(environment, "feedback.db");

    public static RelationalDatabaseConnection CreateCategoryKnowledgeConnection(IWebHostEnvironment environment)
        => CreateConnection(environment, "category-knowledge.db");

    private static RelationalDatabaseConnection CreateConnection(IWebHostEnvironment environment, string sqliteFileName)
    {
        var providerText = Environment.GetEnvironmentVariable(DatabaseProviderEnvVar);
        if (string.IsNullOrWhiteSpace(providerText))
        {
            return RelationalDatabaseConnection.FromSqlitePath(Path.Combine(ResolveArtifactsPath(environment), sqliteFileName));
        }

        if (!Enum.TryParse<RelationalDatabaseProvider>(providerText, ignoreCase: true, out var provider))
        {
            throw new InvalidOperationException(
                $"Unsupported {DatabaseProviderEnvVar} value '{providerText}'. Supported values: Sqlite, Postgres.");
        }

        var configuredConnectionString = Environment.GetEnvironmentVariable(DatabaseConnectionStringEnvVar);
        if (provider == RelationalDatabaseProvider.Postgres)
        {
            if (string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                throw new InvalidOperationException(
                    $"{DatabaseConnectionStringEnvVar} is required when {DatabaseProviderEnvVar}=Postgres.");
            }

            return new RelationalDatabaseConnection(
                RelationalDatabaseProvider.Postgres,
                NormalizePostgresConnectionString(configuredConnectionString));
        }

        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return RelationalDatabaseConnection.FromSqlitePath(Path.Combine(ResolveArtifactsPath(environment), sqliteFileName));
        }

        var sqliteConnectionString = configuredConnectionString.Contains('=', StringComparison.Ordinal)
            ? configuredConnectionString
            : new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(configuredConnectionString) }.ToString();
        return new RelationalDatabaseConnection(RelationalDatabaseProvider.Sqlite, sqliteConnectionString);
    }

    internal static string NormalizePostgresConnectionString(string raw)
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

    private static string ResolveArtifactsPath(IWebHostEnvironment environment)
    {
        var dataDir = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDir))
        {
            return Path.GetFullPath(dataDir);
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "artifacts"));
    }
}
