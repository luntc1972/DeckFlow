using DeckFlow.Core.Storage;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using Xunit;

namespace DeckFlow.Web.Tests;

// Database provider env vars are process-wide; serialize tests that mutate them.
[CollectionDefinition("DeckFlowDatabaseConnectionFactoryTests", DisableParallelization = true)]
public sealed class DeckFlowDatabaseConnectionFactoryTestsCollection
{
}

[Collection("DeckFlowDatabaseConnectionFactoryTests")]
public sealed class DeckFlowDatabaseConnectionFactoryTests
{
    [Fact]
    public void CreateFeedbackConnection_DefaultsToSqliteArtifactsPath()
    {
        var providerOriginal = Environment.GetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER");
        var connectionOriginal = Environment.GetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING");
        var dataDirOriginal = Environment.GetEnvironmentVariable("MTG_DATA_DIR");

        try
        {
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER", null);
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING", null);
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", null);

            var environment = new FakeWebHostEnvironment(Path.Combine(Path.GetTempPath(), "deckflow-content-" + Guid.NewGuid().ToString("N")));
            var connection = DeckFlowDatabaseConnectionFactory.CreateFeedbackConnection(environment);

            Assert.Equal(RelationalDatabaseProvider.Sqlite, connection.Provider);
            Assert.Contains("feedback.db", connection.ConnectionString, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER", providerOriginal);
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING", connectionOriginal);
            Environment.SetEnvironmentVariable("MTG_DATA_DIR", dataDirOriginal);
        }
    }

    [Fact]
    public void CreateCategoryKnowledgeConnection_Postgres_UsesSharedConfiguredConnectionString()
    {
        var providerOriginal = Environment.GetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER");
        var connectionOriginal = Environment.GetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING");

        try
        {
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER", "Postgres");
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING", "Host=localhost;Database=deckflow;Username=postgres;Password=postgres");

            var environment = new FakeWebHostEnvironment(Path.Combine(Path.GetTempPath(), "deckflow-content-" + Guid.NewGuid().ToString("N")));
            var feedbackConnection = DeckFlowDatabaseConnectionFactory.CreateFeedbackConnection(environment);
            var categoryConnection = DeckFlowDatabaseConnectionFactory.CreateCategoryKnowledgeConnection(environment);

            Assert.Equal(RelationalDatabaseProvider.Postgres, feedbackConnection.Provider);
            Assert.Equal(RelationalDatabaseProvider.Postgres, categoryConnection.Provider);
            Assert.Equal(feedbackConnection.ConnectionString, categoryConnection.ConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER", providerOriginal);
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING", connectionOriginal);
        }
    }

    [Fact]
    public void CreateFeedbackConnection_Postgres_NormalizesRenderDatabaseUri()
    {
        var providerOriginal = Environment.GetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER");
        var connectionOriginal = Environment.GetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING");

        try
        {
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER", "Postgres");
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING", "postgresql://u:p%40w0rd%21@host:5433/db?sslmode=require&application_name=foo");

            var environment = new FakeWebHostEnvironment(Path.Combine(Path.GetTempPath(), "deckflow-content-" + Guid.NewGuid().ToString("N")));
            var connection = DeckFlowDatabaseConnectionFactory.CreateFeedbackConnection(environment);
            var builder = new NpgsqlConnectionStringBuilder(connection.ConnectionString);

            Assert.Equal(RelationalDatabaseProvider.Postgres, connection.Provider);
            Assert.Equal("host", builder.Host);
            Assert.Equal("u", builder.Username);
            Assert.Equal("p@w0rd!", builder.Password);
            Assert.Equal("db", builder.Database);
            Assert.Equal(5433, builder.Port);
            Assert.Equal(SslMode.Require, builder.SslMode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_PROVIDER", providerOriginal);
            Environment.SetEnvironmentVariable("DECKFLOW_DATABASE_CONNECTION_STRING", connectionOriginal);
        }
    }

    private sealed class FakeWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DeckFlow.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
