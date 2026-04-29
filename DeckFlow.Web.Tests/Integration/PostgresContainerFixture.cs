using System;
using System.Threading;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using Xunit;
using Xunit.Sdk;

namespace DeckFlow.Web.Tests.Integration;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PostgreSqlContainer? _container;
    private bool _attemptedStart;
    private bool _started;
    private string? _skipReason;

    public Task InitializeAsync()
    {
        // Startup is deferred until a test actually needs the connection string.
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task<string> GetConnectionStringOrSkipAsync()
    {
        await EnsureStartedAsync();

        if (_container is null)
        {
            throw SkipException.ForSkip(_skipReason ?? "Postgres integration tests are unavailable.");
        }

        return _container.GetConnectionString();
    }

    private async Task EnsureStartedAsync()
    {
        if (_started || _attemptedStart)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_started || _attemptedStart)
            {
                return;
            }

            _attemptedStart = true;

            var enabled = Environment.GetEnvironmentVariable("DECKFLOW_POSTGRES_TESTS");
            if (!string.Equals(enabled, "1", StringComparison.Ordinal))
            {
                _skipReason = "Postgres integration tests are disabled. Set DECKFLOW_POSTGRES_TESTS=1 and ensure Docker is running to enable.";
                return;
            }

            try
            {
                _container = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .WithDatabase("deckflow_tests")
                    .WithUsername("deckflow")
                    .WithPassword("deckflow")
                    .Build();

                await _container.StartAsync();
                _started = true;
            }
            catch (Exception ex)
            {
                _skipReason = $"Postgres container unavailable: {ex.Message}";
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
