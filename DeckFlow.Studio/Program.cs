using System.Diagnostics;
using System.Globalization;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
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
        // Why: resolve the data dir + logs dir BEFORE anything can fail so a packaged,
        // double-clicked exe that crashes on startup (e.g. Kestrel port-in-use) still leaves a
        // log file on disk after the console window closes. logs/ sits beside content-kb.db.
        var studioDataDirectory = ResolveStudioDataDirectory();
        var logDirectory = Path.Combine(studioDataDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, "studio-.log");

        // Why: bootstrap logger captures failures that happen before the host is built — the
        // UseSerilog logger only takes over after builder.Build(). Standard two-stage Serilog init.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
            .CreateBootstrapLogger();

        Log.Information("DeckFlow Studio starting. Data dir: {DataDir}; logs: {LogDir}", studioDataDirectory, logDirectory);

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
                // Why: file sink so crashes survive the console window closing on a double-clicked exe.
                configuration.WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14);
            });

            builder.Configuration.AddUserSecrets<Program>().AddEnvironmentVariables();

            var prodConnStr = builder.Configuration["Studio:ProdConnectionString"];
            var isProdConfigured = !string.IsNullOrEmpty(prodConnStr);
            // Why: presence-only check — never log values (D-07 / SC5). KeyPassphrase is optional
            // and so is intentionally excluded from the presence check.
            var isScpConfigured = !string.IsNullOrEmpty(builder.Configuration["Studio:Scp:Host"])
                && !string.IsNullOrEmpty(builder.Configuration["Studio:Scp:Username"])
                && !string.IsNullOrEmpty(builder.Configuration["Studio:Scp:KeyFile"])
                && !string.IsNullOrEmpty(builder.Configuration["Studio:Scp:RemoteArtifactRoot"]);
            // Why (D-09 REVISED/D-10): presence-only check for the three deploy-confirm keys the
            // DirectPush hash-match poll needs — mirrors the isScpConfigured pattern. Never log the
            // values. AdminUser/AdminPassword must equal the web FEEDBACK_ADMIN_USER/PASSWORD so the
            // /Admin BasicAuth gate accepts the confirmer's request.
            var isConfirmerConfigured = !string.IsNullOrEmpty(builder.Configuration["Studio:PublicSiteBaseUrl"])
                && !string.IsNullOrEmpty(builder.Configuration["Studio:AdminUser"])
                && !string.IsNullOrEmpty(builder.Configuration["Studio:AdminPassword"]);
            var contentKbDatabasePath = Path.Combine(studioDataDirectory, "content-kb.db");
            var contentKbArtifactRoot = Path.Combine(studioDataDirectory, "content-kb");

            Directory.CreateDirectory(studioDataDirectory);
            Directory.CreateDirectory(contentKbArtifactRoot);

            builder.Services.AddSingleton(new StudioConfig(isProdConfigured, isScpConfigured, isConfirmerConfigured));
            builder.Services.AddSingleton<ISshArtifactUploader, SftpArtifactUploader>();
            builder.Services.AddSingleton<ISshArtifactDownloader, SftpArtifactDownloader>();
            builder.Services.AddSingleton<IProdContentReader, ProdContentReader>();
            builder.Services.AddSingleton<IProdStoreFactory, ProdStoreFactory>();
            // Why (D-09 REVISED/SYNC-09): the DirectPush deploy-confirm poller. Depends only on the
            // shared singleton HttpClient (registered below) + IConfiguration — safe as a singleton.
            builder.Services.AddSingleton<IDeployedBodyConfirmer, DeployedBodyConfirmer>();
            builder.Services.AddSingleton<IContentSourceStore>(_ => new ContentSourceStore(contentKbDatabasePath));
            builder.Services.AddSingleton<IContentVideoStore>(_ => new ContentVideoStore(contentKbDatabasePath));
            builder.Services.AddSingleton<IContentSiteIndexStore>(_ => new ContentSiteIndexStore(contentKbDatabasePath));
            // Why (D-08): host-agnostic body_sha256 backfill, bound to the LOCAL content-kb.db
            // store above via the IContentSiteIndexStore singleton — explicitly NOT any
            // ProdStoreFactory prod store (those stay schema-ensure OFF, P88 D-10). Run at
            // startup after this registration (see the app.Services resolution below).
            builder.Services.AddSingleton<IContentArtifactBodyResolver, StudioContentArtifactBodyResolver>();
            builder.Services.AddSingleton<ContentBodyHashBackfill>();
            builder.Services.AddSingleton<IBlockedVideoStore>(_ => new BlockedVideoStore(contentKbDatabasePath));
            // Why: curated creator list (SRC-01) + skipped-candidate list (HSEL-02/03) live in
            // content-kb.db beside the blocked list; schema is ensured lazily on first use.
            builder.Services.AddSingleton<ICreatorSourceStore>(_ => new CreatorSourceStore(contentKbDatabasePath));
            builder.Services.AddSingleton<ISkippedVideoStore>(_ => new SkippedVideoStore(contentKbDatabasePath));
            builder.Services.AddSingleton<IContentHarvestRunStore>(_ => new ContentHarvestRunStore(contentKbDatabasePath));
            // Why: persisted auto-approve settings (D-07) live in the studio data dir, beside content-kb.db,
            // so the operator's on/off + cutoff survive Studio restarts (unlike SessionCapOverride).
            builder.Services.AddSingleton(_ => new AutoApproveSettingsStore(studioDataDirectory));
            // Why: the auto-approve decision (clip count >= cutoff, D-01/D-02) lives behind a swappable
            // seam; the Harvest one-click flow (Plan 03) resolves it to flip approval_status post-distill.
            builder.Services.AddSingleton<IAutoApproveSignal, ClipCountAutoApproveSignal>();
            // Why: Read DECKFLOW_LLM_PROVIDER ONCE so the factory-resolved distiller and
            // StudioDistillConfig.IsSubscriptionProvider are always derived from the same value
            // and can never disagree (HIGH-1 / D-01). The subscription rule lives in one place
            // (LlmDistillationProviderFactory.IsSubscriptionProvider), shared with the CLI.
            var providerEnv = builder.Configuration[LlmDistillationProviderFactory.EnvironmentVariableName]
                ?? Environment.GetEnvironmentVariable(LlmDistillationProviderFactory.EnvironmentVariableName);
            var isSubscriptionProvider = LlmDistillationProviderFactory.IsSubscriptionProvider(providerEnv);

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
            // Why (M1): wrap the shared HttpClient in ResilientHttpHandler so long YouTube-list /
            // transcript GETs retry transient blips (408/429/5xx, HttpRequestException) with backoff
            // instead of failing a whole harvest. POST (LLM/Whisper) is not retried (see the handler).
            builder.Services.AddSingleton(_ => new HttpClient(new ResilientHttpHandler()) { Timeout = TimeSpan.FromMinutes(15) });
            // Why: Factory-resolved from the single providerEnv so the distiller and spend flag
            // are always consistent. When provider=openai: metered LlmDistillationService, cap enforced.
            // When provider=claude: subscription CliLlmDistillationService ($0), cap bypassed. (HIGH-1)
            builder.Services.AddSingleton<ILlmDistillationService>(sp =>
                LlmDistillationProviderFactory.Resolve(providerEnv, sp.GetRequiredService<HttpClient>()));
            builder.Services.AddSingleton(new StudioDistillConfig(isSubscriptionProvider));
            builder.Services.AddSingleton<IYouTubeChannelVideoLister>(sp => new YouTubeChannelVideoLister(sp.GetRequiredService<HttpClient>()));
            builder.Services.AddSingleton<IFfmpegAudioChunker, FfmpegAudioChunker>();
            builder.Services.AddSingleton<IGitRepository, GitRepository>();
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
            builder.Services.AddSingleton<VideoStatusResolver>();
            // Why: PublishStateDeriver is a pure stateless class; singleton is safe and avoids allocation
            // per-request. Pages inject it via [Inject] to derive publish state from ContentSiteIndexRow fields.
            builder.Services.AddSingleton<PublishStateDeriver>();
            builder.Services.AddScoped<ContentKbOrchestratorSmokeService>();
            // Why: DirectPush page orchestration (prod read / diff / SCP / transactional write /
            // git durability push), extracted from the page code-behind (H1). Scoped because the
            // git durability stage depends on the scoped IContentKbOrchestrator — a singleton would
            // capture it (captive dependency), same as PublishCoordinator below.
            builder.Services.AddScoped<DeckFlow.Studio.ViewModels.DirectPushCoordinator>();
            // Why: Publish page orchestration (git repo-info load / export / artifact-copy / diff /
            // stage-and-commit), extracted from the page code-behind (H1). Scoped because it depends
            // on the scoped IContentKbOrchestrator — a singleton would capture it (captive dependency).
            builder.Services.AddScoped<DeckFlow.Studio.ViewModels.PublishCoordinator>();
            // Why: Review page orchestration (queue load / approval-status writes / artifact path
            // containment + read), extracted from the page code-behind (H1). Stateless and both its
            // dependencies are singletons, so it is registered as a singleton too.
            builder.Services.AddSingleton<DeckFlow.Studio.ViewModels.ReviewCoordinator>();
            // Why: PullFromProd page orchestration (read-only prod pull + local-only adopt apply),
            // extracted from the page code-behind (H1). Stateless and all its dependencies are
            // singletons, so it is registered as a singleton too.
            builder.Services.AddSingleton<DeckFlow.Studio.ViewModels.PullFromProdCoordinator>();
            // Why: Harvest page collaborators (Phase 82 SRP split), each owning the I/O for one
            // concern while the page keeps the markup-bound state. All dependencies are singletons
            // except CreatorManagementCoordinator's IContentMaintenanceOrchestrator (scoped) — that
            // one is registered scoped to avoid a captive dependency, same reasoning as
            // DirectPushCoordinator/PublishCoordinator above.
            builder.Services.AddSingleton<DeckFlow.Studio.ViewModels.HarvestQueueCoordinator>();
            builder.Services.AddSingleton<DeckFlow.Studio.ViewModels.AutoApproveSettingsCoordinator>();
            builder.Services.AddScoped<DeckFlow.Studio.ViewModels.CreatorManagementCoordinator>();
            builder.Services.AddSingleton<DeckFlow.Studio.ViewModels.SpendCapCoordinator>();
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();

            var app = builder.Build();
            using (var scope = app.Services.CreateScope())
            {
                // Why (L3): actually RUN the read-only smoke probe at startup. The previous code only
                // resolved the service and discarded it, proving DI could construct it but never that
                // the content database / orchestrator slice is reachable — a broken content-kb.db went
                // unnoticed until first use. Log the result; log (not crash) on failure so the operator
                // still gets the app + error UI rather than a dead double-clicked exe.
                var smoke = scope.ServiceProvider.GetRequiredService<ContentKbOrchestratorSmokeService>();
                try
                {
                    var blockedCount = await smoke.ProbeAsync();
                    Log.Information("Content KB smoke check passed: orchestrator reachable, {BlockedCount} blocked row(s).", blockedCount);
                }
                catch (Exception smokeException)
                {
                    Log.Error(smokeException, "Content KB smoke check failed — the content database or orchestrator wiring may be broken.");
                }
            }

            // Why (D-08): one-time deterministic body_sha256 backfill against the LOCAL
            // content-kb.db store only — the IContentSiteIndexStore singleton resolved here is
            // the line-81 local store, never a ProdStoreFactory prod store (those stay
            // schema-ensure OFF, P88 D-10). Ensure the local store's own schema first (adds
            // body_sha256 if missing on a pre-Phase-89 local DB), then run the idempotent
            // null-only pass so legacy local rows hash identically to web rows (D-08).
            var localIndexStore = app.Services.GetRequiredService<IContentSiteIndexStore>();
            await localIndexStore.EnsureSchemaAsync();
            await app.Services.GetRequiredService<ContentBodyHashBackfill>().RunAsync();
            Log.Information("Content KB body-hash backfill completed for the local content-kb.db store.");

            Log.Information("Studio prod connection: {Status}", isProdConfigured ? "configured" : "not configured");
            Log.Information("Studio SCP: {Status}", isScpConfigured ? "configured" : "not configured");
            Log.Information("Studio deploy-confirm: {Status}", isConfirmerConfigured ? "configured" : "not configured");

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseStaticFiles();
            app.UseRouting();

            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            // Why: a packaged double-clicked exe should feel like a desktop app — open the operator's
            // default browser at the bound URL once Kestrel is listening. Guarded so it never fires in
            // Development or under the test / no-browser env var (DECKFLOW_DISABLE_AUTO_BROWSER), and
            // wrapped so a browser-launch failure can never take down the host.
            var disableAutoBrowser = !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("DECKFLOW_DISABLE_AUTO_BROWSER"));
            if (!app.Environment.IsDevelopment() && !disableAutoBrowser)
            {
                app.Lifetime.ApplicationStarted.Register(() =>
                {
                    try
                    {
                        var addresses = app.Services.GetRequiredService<IServer>()
                            .Features.Get<IServerAddressesFeature>()?.Addresses;
                        var url = addresses?.FirstOrDefault() ?? "http://localhost:5271";
                        // Normalize wildcard bindings to localhost so the browser gets a usable URL.
                        url = url.Replace("http://+", "http://localhost", StringComparison.Ordinal)
                                 .Replace("http://0.0.0.0", "http://localhost", StringComparison.Ordinal)
                                 .Replace("http://[::]", "http://localhost", StringComparison.Ordinal);
                        Log.Information("Opening default browser at {Url}", url);
                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    }
                    catch (Exception browserException)
                    {
                        Log.Warning(browserException,
                            "Could not open the default browser automatically; open the Studio URL manually.");
                    }
                });
            }

            // Why: H2 — Studio is an unauthenticated prod-publish tool; it closes the only
            // network-exposure hole by refusing to bind any non-loopback address. Wildcards
            // (0.0.0.0, +, *, [::]) and routable IPs/hostnames are rejected here so Kestrel
            // never gets to bind them. Fix ASPNETCORE_URLS or Kestrel:Endpoints:*:Url and relaunch.
            var configuredUrls = LoopbackBindGuard.GatherConfiguredBindUrls(builder.Configuration);
            var offending = LoopbackBindGuard.FindNonLoopbackBindings(configuredUrls);
            if (offending.Count > 0)
            {
                Log.Fatal(
                    "DeckFlow Studio refuses to start: non-loopback bind address(es) detected: {Offending}. "
                    + "Studio is an unauthenticated prod-publish tool and may only bind loopback "
                    + "(localhost / 127.0.0.1 / [::1]). "
                    + "Fix ASPNETCORE_URLS or Kestrel:Endpoints:*:Url and relaunch.",
                    string.Join(", ", offending));
                throw new InvalidOperationException(
                    $"DeckFlow Studio refuses to bind a non-loopback address: {string.Join(", ", offending)}");
            }

            await app.RunAsync();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "DeckFlow Studio host terminated during startup or run.");
            // Why: the most common packaged-exe startup crash is a Kestrel bind failure (the pinned
            // http://localhost:5271 already in use). Surface a plain-language remedy in the log file
            // so the operator does not have to decode the Kestrel stack trace.
            var message = exception.Message ?? string.Empty;
            if (exception.GetType().Name.Contains("AddressInUse", StringComparison.Ordinal)
                || message.Contains("bind", StringComparison.OrdinalIgnoreCase)
                || message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
            {
                Log.Fatal("Startup bind failure — the configured port (default http://localhost:5271) is likely "
                    + "already in use. Close the other instance, or set ASPNETCORE_URLS=http://localhost:NNNN to a "
                    + "free port, then relaunch. Full log: {LogDir}", logDirectory);
            }
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
