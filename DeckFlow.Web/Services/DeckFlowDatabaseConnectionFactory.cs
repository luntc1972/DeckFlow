using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;

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

            return new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, configuredConnectionString);
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
