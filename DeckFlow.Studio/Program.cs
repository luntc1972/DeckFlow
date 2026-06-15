using System.Globalization;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DeckFlow.Studio;

/// <summary>
/// Configures and starts the DeckFlow Studio application.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Bootstraps the ASP.NET Core Blazor Server app with Serilog and service registrations.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static async Task Main(string[] args)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext();

                configuration.WriteTo.Console();
            });

            builder.Configuration.AddUserSecrets<Program>().AddEnvironmentVariables();

            var prodConnStr = builder.Configuration["Studio:ProdConnectionString"];
            var isProdConfigured = !string.IsNullOrEmpty(prodConnStr);
            var studioDataDirectory = ResolveStudioDataDirectory();
            var contentKbDatabasePath = Path.Combine(studioDataDirectory, "content-kb.db");
            var contentKbArtifactRoot = Path.Combine(studioDataDirectory, "content-kb");

            Directory.CreateDirectory(studioDataDirectory);
            Directory.CreateDirectory(contentKbArtifactRoot);

            builder.Services.AddSingleton(new StudioConfig(isProdConfigured));
            builder.Services.AddSingleton<IContentSourceStore>(_ => new ContentSourceStore(contentKbDatabasePath));
            builder.Services.AddSingleton<IContentVideoStore>(_ => new ContentVideoStore(contentKbDatabasePath));
            builder.Services.AddSingleton<IContentSiteIndexStore>(_ => new ContentSiteIndexStore(contentKbDatabasePath));
            builder.Services.AddSingleton<IBlockedVideoStore>(_ => new BlockedVideoStore(contentKbDatabasePath));
            builder.Services.AddSingleton<IContentHarvestRunStore>(_ => new ContentHarvestRunStore(contentKbDatabasePath));
            // Why: Read DECKFLOW_LLM_PROVIDER ONCE so the factory-resolved distiller and
            // StudioDistillConfig.IsSubscriptionProvider are always derived from the same value
            // and can never disagree (HIGH-1 / D-01).
            // Replicates DeckFlow.CLI/ContentKbCommandRunners.cs lines 95-97.
            var providerEnv = builder.Configuration[LlmDistillationProviderFactory.EnvironmentVariableName]
                ?? Environment.GetEnvironmentVariable(LlmDistillationProviderFactory.EnvironmentVariableName);
            var isSubscriptionProvider = !string.IsNullOrWhiteSpace(providerEnv)
                && !string.Equals(providerEnv.Trim(), "openai", StringComparison.OrdinalIgnoreCase);

            // Why: SessionCapOverride registered first so the resolver closure can capture the reference.
            // The same singleton ledger instance is injected into both the Harvest page and the orchestrator,
            // so the override is seen by WouldExceedCapAsync inside DistillOrchestrator (D-03 / Pitfall 6).
            var capOverride = new SessionCapOverride();
            builder.Services.AddSingleton(capOverride);
            builder.Services.AddSingleton<ILlmSpendLedger>(_ => new LlmSpendLedger(contentKbDatabasePath,
                key =>
                {
                    if (key == "DECKFLOW_LLM_MONTHLY_CAP_USD" && capOverride.OverrideUsd.HasValue)
                        return capOverride.OverrideUsd.Value.ToString("F2", CultureInfo.InvariantCulture);
                    return null;
                }));
            builder.Services.AddSingleton<IWhisperSpendLedger>(_ => new WhisperSpendLedger(contentKbDatabasePath));
            builder.Services.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromMinutes(15) });
            // Why: Factory-resolved from the single providerEnv so the distiller and spend flag
            // are always consistent. When provider=openai: metered LlmDistillationService, cap enforced.
            // When provider=claude: subscription CliLlmDistillationService ($0), cap bypassed. (HIGH-1)
            builder.Services.AddSingleton<ILlmDistillationService>(sp =>
                LlmDistillationProviderFactory.Resolve(providerEnv, sp.GetRequiredService<HttpClient>()));
            builder.Services.AddSingleton(new StudioDistillConfig(isSubscriptionProvider));
            builder.Services.AddSingleton<IYouTubeChannelVideoLister>(sp => new YouTubeChannelVideoLister(sp.GetRequiredService<HttpClient>()));
            builder.Services.AddSingleton<IFfmpegAudioChunker, FfmpegAudioChunker>();
            builder.Services.AddSingleton<ITranscriptSource>(sp => new YouTubeTranscriptSource(
                TranscriptProviderFactory.Resolve(sp.GetRequiredService<HttpClient>()),
                new YouTubeAudioSource(sp.GetRequiredService<HttpClient>()),
                new WhisperTranscriptionService(
                    sp.GetRequiredService<IWhisperSpendLedger>(),
                    sp.GetRequiredService<IFfmpegAudioChunker>(),
                    sp.GetRequiredService<HttpClient>()),
                whisperEnabled: false));
            builder.Services.AddSingleton<Func<DateTimeOffset>>(_ => () => DateTimeOffset.UtcNow);
            builder.Services.AddSingleton(new ContentKbOrchestratorOptions
            {
                ArtifactRoot = contentKbArtifactRoot,
            });
            builder.Services.AddContentKbOrchestrator();
            builder.Services.AddScoped<ContentKbOrchestratorSmokeService>();
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();

            var app = builder.Build();
            using (var scope = app.Services.CreateScope())
            {
                _ = scope.ServiceProvider.GetRequiredService<ContentKbOrchestratorSmokeService>();
            }

            Log.Information("Studio prod connection: {Status}", isProdConfigured ? "configured" : "not configured");

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseRouting();

            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            await app.RunAsync();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "DeckFlow Studio host terminated during startup or run.");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static string ResolveStudioDataDirectory()
    {
        var dataDir = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        var root = string.IsNullOrWhiteSpace(dataDir)
            ? Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "studio")
            : Path.Combine(dataDir, "studio");
        return Path.GetFullPath(root);
    }
}
