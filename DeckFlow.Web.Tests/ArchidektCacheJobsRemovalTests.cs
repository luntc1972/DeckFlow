using System.Net;
using System.Text;
using DeckFlow.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Proves D-14 removed the public Archidekt cache-job surface from the real web host.
/// </summary>
[Collection("AdminEnvSerial")]
public sealed class ArchidektCacheJobsRemovalTests
{
    private const string RemovedPrefix = "/api/archidekt-cache-jobs";

    // Why (D-14, HARV-12): anonymous script callers could start a harvest through this public API.
    [Fact]
    public async Task RemovedJobRoutes_ThroughRealHost_Return404WithEmptyBody()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"cache-jobs-removal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        using var providerScope = EnvScope.Clear("DECKFLOW_DATABASE_PROVIDER");
        using var connectionScope = EnvScope.Clear("DECKFLOW_DATABASE_CONNECTION_STRING");
        using var dataDirectoryScope = EnvScope.Set("MTG_DATA_DIR", tempDirectory);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var cancellationToken = cancellationTokenSource.Token;
        var app = Program.BuildApp(new[] { "--urls", "http://127.0.0.1:0" });
        var mismatches = new List<string>();

        try
        {
            // Why: this keeps the host lifetime below HarvestScheduleService's 60-second first tick.
            await app.StartAsync(cancellationToken);
            using var client = new HttpClient
            {
                BaseAddress = new Uri(app.Urls.First()),
                Timeout = TimeSpan.FromSeconds(15),
            };

            var probes = new[]
            {
                new HttpRequestMessage(HttpMethod.Post, RemovedPrefix)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                },
                new HttpRequestMessage(HttpMethod.Get, $"{RemovedPrefix}/00000000-0000-0000-0000-000000000001"),
                new HttpRequestMessage(HttpMethod.Get, $"{RemovedPrefix}/active"),
                new HttpRequestMessage(HttpMethod.Get, RemovedPrefix),
                new HttpRequestMessage(HttpMethod.Post, $"{RemovedPrefix}/active")
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                },
            };

            foreach (var probe in probes)
            {
                using (probe)
                {
                    using var response = await client.SendAsync(probe, cancellationToken);
                    var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    if (response.StatusCode != HttpStatusCode.NotFound || body.Length != 0)
                    {
                        mismatches.Add($"{probe.Method} {probe.RequestUri}: {(int)response.StatusCode}, {body.Length} bytes, {contentType}");
                    }
                }
            }

            using var controlResponse = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "/api/suggestions/card"),
                cancellationToken);
            if (controlResponse.StatusCode != HttpStatusCode.MethodNotAllowed)
            {
                var body = await controlResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                mismatches.Add($"GET /api/suggestions/card: {(int)controlResponse.StatusCode}, {body.Length} bytes, {controlResponse.Content.Headers.ContentType?.MediaType}");
            }
        }
        finally
        {
            await app.StopAsync(CancellationToken.None);
            await app.DisposeAsync();
            SqliteConnection.ClearAllPools();

            // Why: SQLite file handles on Windows can outlive the pool clear, and a leftover temp directory is harmless.
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        Assert.Empty(mismatches);
    }

    [Fact]
    public void RemovedJobTypes_AreAbsentFromWebAssembly()
    {
        var removedNames = new[]
        {
            "ArchidektCacheJobsController",
            "ArchidektCacheJobStartRequest",
            "ArchidektCacheJobStatusResponse",
            "ArchidektCacheJobEnqueueResponse",
        };

        var presentNames = typeof(Program).Assembly
            .GetTypes()
            .Select(static type => type.Name)
            .Where(removedNames.Contains)
            .ToArray();

        Assert.Empty(presentNames);
    }

    [Fact]
    public void NoControllerOrAction_DeclaresTheRemovedRoutePrefix()
    {
        var normalizedPrefix = RemovedPrefix.TrimStart('/');
        var templates = typeof(Program).Assembly
            .GetTypes()
            .Where(static type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(static type => type.GetCustomAttributes(inherit: true)
                .Concat(type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .SelectMany(method => method.GetCustomAttributes(inherit: true))))
            .OfType<IRouteTemplateProvider>()
            .Select(static provider => provider.Template)
            .Where(static template => !string.IsNullOrWhiteSpace(template))
            .Where(template => template!.TrimStart('/').Contains(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(templates);
    }
}
