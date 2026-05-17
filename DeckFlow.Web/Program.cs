using System.Reflection;
using System.Net.Http;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Polly.Registry;
using Serilog;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Extensions;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Analytics;
using DeckFlow.Web.Services.Harvest;
using DeckFlow.Web.Services.Http;

namespace DeckFlow.Web;

/// <summary>
/// Configures and starts the DeckFlow web application.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Bootstraps the ASP.NET Core MVC app with Serilog and service registrations.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public static async Task Main(string[] args)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);
            var logPath = Path.Combine(builder.Environment.ContentRootPath, "logs", "web-.log");

            builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext();

                // Render only captures stdout/stderr in the service logs, so keep console logging on
                // outside development as well as in development. The file sink remains available for
                // the local logs directory and persistent disk snapshots.
                configuration.WriteTo.Console();

                configuration.WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14);
            });

            // Add services to the container.
            builder.Services
                .AddControllersWithViews()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });
            builder.Services.AddMemoryCache();

            // AI platform toggles. Gemini is hidden in the UI by default because the full
            // packet frequently exceeds Gemini's paste limit (truncates instructions, produces
            // degraded output). Flip DECKFLOW_GEMINI_ENABLED=true to expose it again.
            builder.Services.Configure<AiPlatformOptions>(options =>
            {
                var raw = Environment.GetEnvironmentVariable("DECKFLOW_GEMINI_ENABLED");
                options.GeminiEnabled = bool.TryParse(raw, out var enabled) && enabled;
            });

            // HTTP infrastructure: IHttpClientFactory-backed clients (D-01) + Polly v8 pipelines (D-03..05).
            // Tagger uses a typed client with cookie-disabled SocketsHttpHandler (D-06); other three are named.
            // Pipelines are registered into IResiliencePipelineRegistry<string> via AddResiliencePipeline<...>;
            // services resolve them via ResiliencePipelineProvider<string> (no keyed-services attribute - checker B2).

            builder.Services.AddHttpClient("commander-banlist", c =>
            {
                c.BaseAddress = new Uri("https://mtgcommander.net/");
                c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0");
            });

            builder.Services.AddHttpClient("commander-spellbook", c =>
            {
                c.BaseAddress = new Uri("https://backend.commanderspellbook.com/");
                c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0");
            });

            builder.Services.AddHttpClient("scryfall-rest", c =>
            {
                c.BaseAddress = new Uri("https://api.scryfall.com/");
                c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0 (+https://github.com/luntc1972/DeckFlow)");
                c.DefaultRequestHeaders.Accept.ParseAdd("application/json;q=0.9,*/*;q=0.8");
            });

            // Typed client for Tagger — automatic cookie handling via SocketsHttpHandler CookieContainer
            // per Phase 5 BUG-01 fix. The session cookie set by GET /card/{set}/{num} is replayed
            // automatically on the subsequent POST /graphql by the same handler, removing the need
            // for manual Cookie header construction (which the pre-Phase-5 manual replay path
            // failed to deliver under RestSharp 114 + redirect-disabled config — see 04-ABANDONED.md).
            // HandlerLifetime = 5 min. TaggerSessionCache TTL = 270s (30s below HandlerLifetime)
            // so the cached CSRF token expires before the underlying handler+cookie pair rotates
            // (HIGH-2 invariant — DO NOT lower the 30s margin).
            //
            // The CookieContainer is registered as a singleton so ScryfallTaggerHttpClient can
            // expose it for diagnostic logging (Tagger.SessionFetch log line {CookieCount} slot).
            // The SocketsHttpHandler factory below resolves and uses the same instance, so reads
            // through the typed wrapper reflect the live session state.
            builder.Services.AddSingleton<System.Net.CookieContainer>();
            builder.Services.AddHttpClient("scryfall-tagger", c =>
            {
                c.BaseAddress = new Uri("https://tagger.scryfall.com/");
                c.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow/1.0");
                // Phase 5 BUG-01 follow-up: browser-mimicking request headers to clear
                // Cloudflare's Browser Integrity Check on tagger.scryfall.com. The host
                // appears to 404 requests from Render's egress IP that lack these signals,
                // even though the same UA from a residential IP succeeds. UA stays as
                // "DeckFlow/1.0" per Scryfall API-consumer guidelines (descriptive UA).
                c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                c.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
                c.DefaultRequestHeaders.AcceptEncoding.ParseAdd("deflate");
                c.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");
                c.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
                c.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                c.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                c.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
                c.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            })
            .ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
            {
                UseCookies = true,
                AllowAutoRedirect = true,
                CookieContainer = sp.GetRequiredService<System.Net.CookieContainer>(),
                // Phase 5 BUG-01 follow-up #2: we advertise Accept-Encoding: gzip,deflate,br
                // (browser-mimicking BIC bypass), so the upstream WILL compress responses.
                // Decompression must be enabled or pageResponse.Content is binary
                // garbage and TryExtractCsrfToken returns null (csrf=False in the
                // Tagger.SessionFetch log).
                AutomaticDecompression = System.Net.DecompressionMethods.GZip
                    | System.Net.DecompressionMethods.Deflate
                    | System.Net.DecompressionMethods.Brotli,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            builder.Services.AddSingleton<ScryfallTaggerHttpClient>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var http = factory.CreateClient("scryfall-tagger");
                var cookies = sp.GetRequiredService<System.Net.CookieContainer>();
                return new ScryfallTaggerHttpClient(http, cookies);
            });
            builder.Services.AddSingleton<IScryfallTaggerHttpClient>(sp => sp.GetRequiredService<ScryfallTaggerHttpClient>());

            // Polly v8 pipelines registered into IResiliencePipelineRegistry<string>. Services resolve
            // them via ResiliencePipelineProvider<string>.GetPipeline<RestResponse>(name) - D-05, B2.
            builder.Services.AddDeckFlowResiliencePipelines();

            // CSRF + cookie session store for the Tagger flow (D-07, HIGH-2: 270s TTL).
            builder.Services.AddSingleton<ITaggerSessionCache, TaggerSessionCache>();

            // IScryfallRestClientFactory - defined in Task 4 with static back-compat shim;
            // full IHttpClientFactory wiring lands in Task 10.
            builder.Services.AddSingleton<IScryfallRestClientFactory, ScryfallRestClientFactory>();
            builder.Services.AddSingleton<IHelpContentService, HelpContentService>();
            builder.Services.AddSingleton<IVersionService, VersionService>();
            builder.Services.AddSingleton<IFeedbackStore, FeedbackStore>();
            builder.Services.AddSingleton<IAdminBruteForceTrackerStore, AdminBruteForceTrackerStore>();
            builder.Services.AddDeckFlowFeatureFlags();
            builder.Services.AddDeckFlowHarvest(builder.Environment);
            builder.Services.AddDeckFlowAnalytics(builder.Environment);

            // Honor X-Forwarded-* headers from the reverse proxy so request.Scheme reflects
            // the browser's https scheme, not the http hop from proxy to app. Without this,
            // SameOriginRequestValidator sees scheme=http while Origin=https and rejects the request.
            //
            // Note (TD-04, Phase 03 SC #4, retrieved 2026-04-30): Render does not publish enumerable
            // inbound proxy CIDR ranges (verified at https://render.com/docs/inbound-ip-rules and
            // https://feedback.render.com/features/p/send-the-correct-xforwardedfor). Rather than
            // trust an arbitrary upstream's X-Forwarded-For value to gate the feedback rate limit,
            // the partition key (DeriveFeedbackPartitionKey, below) reads the immediate-peer IP
            // directly. The default loopback trust list (127.0.0.1, ::1) is preserved here for
            // Kestrel container-internal health checks; we do NOT clear it.
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                    | ForwardedHeaders.XForwardedProto
                    | ForwardedHeaders.XForwardedHost;
                // Default loopback entries (127.0.0.1, ::1) preserved - do NOT call Clear().
            });

            builder.Services.AddRateLimiter(options =>
            {
                options.AddPolicy("feedback-submit", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        DeriveFeedbackPartitionKey(httpContext),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromHours(1),
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        }));
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Deck Sync Workbench API",
                    Version = "v1",
                    Description = "Card and commander category suggestion endpoints used by the UI."
                });
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            });
            builder.Services.AddSingleton<ICommanderSearchService>(sp =>
                new ScryfallCommanderSearchService(
                    sp.GetRequiredService<IScryfallRestClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                    sp.GetRequiredService<IMemoryCache>()));
            builder.Services.AddSingleton<ICardSearchService>(sp =>
                new ScryfallCardSearchService(
                    sp.GetRequiredService<IScryfallRestClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                    sp.GetRequiredService<IMemoryCache>()));
            builder.Services.AddSingleton<CardLookupCache>();
            builder.Services.AddSingleton<ICardLookupService>(sp =>
                new ScryfallCardLookupService(
                    sp.GetRequiredService<IScryfallRestClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                    sp.GetRequiredService<CardLookupCache>()));
            builder.Services.AddSingleton<IMechanicLookupService, WotcMechanicLookupService>();
            builder.Services.AddSingleton<ICommanderBanListService>(sp =>
                new CommanderBanListService(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                    sp.GetRequiredService<IMemoryCache>()));
            builder.Services.AddSingleton<ICommanderSpellbookService>(sp =>
                new CommanderSpellbookService(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetService<ILogger<CommanderSpellbookService>>()));
            builder.Services.AddSingleton<IScryfallSetService>(sp =>
                new ScryfallSetService(
                    sp.GetRequiredService<IScryfallRestClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                    sp.GetRequiredService<IMemoryCache>(),
                    sp.GetRequiredService<IMechanicLookupService>()));
            builder.Services.AddSingleton<IEdhTop16Client, EdhTop16Client>();
            builder.Services.AddSingleton<IScryfallTaggerService, ScryfallTaggerService>();
            builder.Services.AddScoped<IChatGptDeckPacketService>(sp =>
                new ChatGptDeckPacketService(
                    sp.GetRequiredService<IScryfallRestClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                    sp.GetRequiredService<IMoxfieldDeckImporter>(),
                    sp.GetRequiredService<IArchidektDeckImporter>(),
                    sp.GetRequiredService<MoxfieldParser>(),
                    sp.GetRequiredService<ArchidektParser>(),
                    sp.GetRequiredService<IMechanicLookupService>(),
                    sp.GetRequiredService<ICommanderBanListService>(),
                    sp.GetRequiredService<IScryfallSetService>(),
                    sp.GetRequiredService<ICommanderSpellbookService>(),
                    sp.GetService<ILogger<ChatGptDeckPacketService>>()));
            builder.Services.AddScoped<IChatGptDeckComparisonService>(sp =>
                new ChatGptDeckComparisonService(
                    sp.GetRequiredService<IScryfallRestClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                    sp.GetRequiredService<IMoxfieldDeckImporter>(),
                    sp.GetRequiredService<IArchidektDeckImporter>(),
                    sp.GetRequiredService<MoxfieldParser>(),
                    sp.GetRequiredService<ArchidektParser>(),
                    sp.GetRequiredService<ICommanderSpellbookService>(),
                    sp.GetService<ILogger<ChatGptDeckComparisonService>>()));
            builder.Services.AddScoped<IChatGptCedhMetaGapService>(sp =>
                new ChatGptCedhMetaGapService(
                    sp.GetRequiredService<IScryfallRestClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                    sp.GetRequiredService<IMoxfieldDeckImporter>(),
                    sp.GetRequiredService<IArchidektDeckImporter>(),
                    sp.GetRequiredService<MoxfieldParser>(),
                    sp.GetRequiredService<ArchidektParser>(),
                    sp.GetRequiredService<IEdhTop16Client>(),
                    sp.GetRequiredService<ICommanderSpellbookService>()));
            builder.Services.AddSingleton<ICategoryKnowledgeStore, CategoryKnowledgeStore>();
            builder.Services.AddSingleton<ArchidektCacheJobService>();
            builder.Services.AddSingleton<IArchidektCacheJobService>(sp => sp.GetRequiredService<ArchidektCacheJobService>());
            builder.Services.AddHostedService(sp => sp.GetRequiredService<ArchidektCacheJobService>());
            builder.Services.AddScoped<ICategorySuggestionService, CategorySuggestionService>();
            builder.Services.AddScoped<ICommanderCategoryService, CommanderCategoryService>();
            builder.Services.AddScoped<IDeckSyncService, DeckSyncService>();
            builder.Services.AddScoped<IDeckConvertService>(sp =>
                new DeckConvertService(
                    sp.GetRequiredService<IScryfallRestClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                    sp.GetRequiredService<IDeckEntryLoader>()));
            builder.Services.AddScoped<IDeckEntryLoader, DeckEntryLoader>();
            builder.Services.AddSingleton<IMoxfieldDeckImporter, MoxfieldApiDeckImporter>();
            builder.Services.AddSingleton<IArchidektDeckImporter, ArchidektApiDeckImporter>();
            builder.Services.AddTransient<MoxfieldParser>();
            builder.Services.AddTransient<ArchidektParser>();

            var app = builder.Build();

            // Must run before any middleware that reads request.Scheme/Host (HttpsRedirection,
            // security headers, SameOriginRequestValidator in controllers) so those see the
            // browser's original scheme/host, not the proxy hop.
            app.UseForwardedHeaders();

            // Phase 12 RENAME-01 (D-03/D-04/D-05): permanent (301) redirects from the legacy
            // chatgpt-* URL slugs to the AI-agnostic deck-analysis / deck-comparison /
            // cedh-meta-gap slugs. Centralized here so DeckController does not accumulate
            // 12 thin redirect actions. Pipeline-order invariant (D-05): this middleware MUST
            // run after UseForwardedHeaders so the 301 Location response honors X-Forwarded-Proto
            // (browser-visible https), not the proxy-hop http scheme. The 9 entries below cover
            // every old page-root plus its /download + /upload sub-routes (D-04). Targets are
            // hardcoded literal absolute paths — no user input is interpolated (T-12-01).
            app.UseRewriter(new RewriteOptions()
                .AddRedirect("^chatgpt-packets$", "deck-analysis", 301)
                .AddRedirect("^chatgpt-packets/download$", "deck-analysis/download", 301)
                .AddRedirect("^chatgpt-packets/upload$", "deck-analysis/upload", 301)
                .AddRedirect("^chatgpt-deck-comparison$", "deck-comparison", 301)
                .AddRedirect("^chatgpt-deck-comparison/download$", "deck-comparison/download", 301)
                .AddRedirect("^chatgpt-deck-comparison/upload$", "deck-comparison/upload", 301)
                .AddRedirect("^chatgpt-cedh-meta-gap$", "cedh-meta-gap", 301)
                .AddRedirect("^chatgpt-cedh-meta-gap/download$", "cedh-meta-gap/download", 301)
                .AddRedirect("^chatgpt-cedh-meta-gap/upload$", "cedh-meta-gap/upload", 301));

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Deck/Error");
                app.UseHsts();
            }

            app.UseDeckFlowSecurityHeaders();

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAnalyticsMiddleware();   // D-12: after UseRouting (endpoint resolved), before MapControllers
            app.UseSerilogRequestLogging();
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("v1/swagger.json", "Deck Sync Workbench API v1");
                    c.RoutePrefix = "swagger";
                });
            }

            app.UseAuthorization();

            app.UseRateLimiter();

            app.UseWhen(
                ctx => ctx.Request.Path.StartsWithSegments("/Admin"),
                branch => branch.UseMiddleware<BasicAuthMiddleware>("DeckFlow Admin"));

            app.MapControllers();
            app.MapDefaultControllerRoute();

            static bool IsAutoBrowserDisabled()
            {
                var raw = Environment.GetEnvironmentVariable("DECKFLOW_DISABLE_AUTO_BROWSER");

                return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }

            if (app.Environment.IsDevelopment()
                && !IsAutoBrowserDisabled())
            {
                app.Lifetime.ApplicationStarted.Register(() =>
                {
                    var launchUrl = app.Urls
                        .OrderByDescending(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault();

                    if (string.IsNullOrWhiteSpace(launchUrl))
                    {
                        return;
                    }

                    try
                    {
                        DevelopmentBrowserLauncher.OpenNewWindow(launchUrl);
                    }
                    catch (Exception exception)
                    {
                        Log.Warning(exception, "Failed to auto-open browser for {LaunchUrl}.", launchUrl);
                    }
                });
            }

            await ValidateDatabaseConnectionsAsync(app.Services, app.Environment, app.Logger);
            app.Logger.LogInformation("Ensuring harvest store schemas during startup.");
            await app.Services.GetRequiredService<IHarvestRunStore>().EnsureSchemaAsync();
            await app.Services.GetRequiredService<IHarvestScheduleStore>().EnsureSchemaAsync();
            app.Logger.LogInformation("Harvest store schemas ensured during startup.");

            app.Logger.LogInformation("Ensuring analytics store schema during startup.");
            await app.Services.GetRequiredService<IRequestMetricsStore>().EnsureSchemaAsync();
            app.Logger.LogInformation("Analytics store schema ensured during startup.");

            // Resolve the IP-hash salt once at startup so the analytics middleware does not
            // perform DB I/O on the hot path. Uses CreateHarvestStateConnection for explicit
            // factory parity with RequestMetricsStore writes (Plan 01) and admin reads (Plan 04).
            try
            {
                var saltAccessor = app.Services.GetRequiredService<AnalyticsSaltAccessor>();
                var harvestConn = DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection(app.Environment);
                await using var saltConnection = harvestConn.CreateConnection();
                await saltConnection.OpenAsync();
                var salt = await IpHasher.ResolveSaltAsync(saltConnection);
                saltAccessor.SetSalt(salt);
                app.Logger.LogInformation("Analytics IP salt resolved.");
            }
            catch (Exception saltEx)
            {
                app.Logger.LogWarning(saltEx,
                    "Analytics IP salt resolution failed; ip_hash will be null until next startup.");
            }

            await app.RunAsync();
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "DeckFlow web host terminated during startup or run.");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Reads the Cloudflare-injected real client IP from the CF-Connecting-IP request header
    /// (Phase 5 BUG-02 fix). Cloudflare always sets this to the originating client IP — single
    /// value per real client, immune to the multi-proxy fan-out that broke Phase 4's
    /// Connection.RemoteIpAddress-based partitioning. Cannot be spoofed past Cloudflare's edge
    /// PROVIDED Render Inbound IP Rules gate the origin to Cloudflare CIDRs (see README "Admin
    /// throttle" operations note). Returns "unknown" and logs a warning when the header is
    /// missing — fail-closed, all unidentifiable traffic shares one partition.
    /// </summary>
    internal static string DeriveCloudflareClientIp(HttpContext context)
    {
        var raw = context.Request.Headers["CF-Connecting-IP"].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            Log.Warning("CF-Connecting-IP missing on {Path} — falling back to 'unknown' partition. Verify Render Inbound IP Rules + Cloudflare proxy.", context.Request.Path.Value ?? "(empty)");
            return "unknown";
        }
        return raw.Trim();
    }

    /// <summary>
    /// Partition key for the feedback-submit rate limiter (TD-04 / Phase 03 SC #4 +
    /// Phase 05 SC #5 corrective). Wraps DeriveCloudflareClientIp so the partition derivation
    /// matches the admin-throttle partition derivation — single source of truth. Phase 03's
    /// "peer:" prefix becomes "feedback:" to make the namespace explicit and disjoint from
    /// "admin:".
    /// </summary>
    internal static string DeriveFeedbackPartitionKey(HttpContext context)
        => "feedback:" + DeriveCloudflareClientIp(context);

    /// <summary>
    /// Partition key for the admin basic-auth brute-force throttle (BUG-02). Same CF-Connecting-IP
    /// derivation as DeriveFeedbackPartitionKey but with "admin:" namespace prefix so admin and
    /// feedback buckets cannot collide.
    /// </summary>
    internal static string DeriveAdminPartitionKey(HttpContext context)
        => "admin:" + DeriveCloudflareClientIp(context);

    private static async Task ValidateDatabaseConnectionsAsync(IServiceProvider services, IWebHostEnvironment environment, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        using var scope = services.CreateScope();
        var feedbackStore = scope.ServiceProvider.GetRequiredService<IFeedbackStore>();
        var knowledgeStore = scope.ServiceProvider.GetRequiredService<ICategoryKnowledgeStore>();

        logger.LogInformation("Validating database connections during startup.");

        await feedbackStore.CountAsync(null, null);
        await knowledgeStore.GetProcessedDeckCountAsync();

        logger.LogInformation("Database connection validation completed successfully.");
    }
}
