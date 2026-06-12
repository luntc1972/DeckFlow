using System.Reflection;
using System.Net.Http;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
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
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.Comparison;
using DeckFlow.Web.Services.PromptBuilders.FollowUp;
using DeckFlow.Web.Services.PromptBuilders.MetaGap;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Options;

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
            builder.Services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(options => options.ViewLocationExpanders.Add(new DeckFlow.Web.Controllers.DeckViewLocationExpander()));
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
                PooledConnectionLifetime = TaggerSessionCache.HandlerLifetime,
            })
            .SetHandlerLifetime(TaggerSessionCache.HandlerLifetime);

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
            builder.Services.AddSingleton<DeckFlow.Core.Content.IContentSiteIndexStore>(_ =>
                new DeckFlow.Core.Content.ContentSiteIndexStore(
                    DeckFlowDatabaseConnectionFactory.CreateContentSiteIndexConnection(builder.Environment)));
            builder.Services.AddSingleton<ContentKbArtifactPathResolver>();
            builder.Services.AddSingleton<IContentKbSeedLoader, ContentKbSeedLoader>();
            // Admin YouTube export: transient lister so each request gets a factory-managed
            // HttpClient (handler rotation) for the per-video YoutubeExplode metadata calls.
            builder.Services.AddHttpClient("youtube-metadata", c => c.Timeout = TimeSpan.FromMinutes(5));
            builder.Services.AddTransient<DeckFlow.Core.Integration.IYouTubeChannelVideoLister>(sp =>
                new DeckFlow.Core.Integration.YouTubeChannelVideoLister(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient("youtube-metadata")));
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
            builder.Services.AddSingleton<PacketSessionCache>();
            builder.Services.AddSingleton<ICardLookupService>(sp =>
                new ScryfallCardLookupService(
                    sp.GetRequiredService<IScryfallRestClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                    sp.GetRequiredService<CardLookupCache>()));
            builder.Services.AddSingleton<IScryfallCardResolver>(sp =>
                new ScryfallCardResolver(
                    sp.GetRequiredService<IScryfallRestClientFactory>(),
                    sp.GetRequiredService<ResiliencePipelineProvider<string>>()));
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
            builder.Services.AddSingleton<IScryfallTaggerLookupService, ScryfallTaggerLookupService>();
            // AiPlatform prompt-builder strategy registries (Phase 15-02)
            builder.Services.AddSingleton<IAnalysisPromptVariant, ChatGptAnalysisPromptVariant>();
            builder.Services.AddSingleton<IAnalysisPromptVariant, ClaudeAnalysisPromptVariant>();
            builder.Services.AddSingleton<IAnalysisPromptVariant, GeminiAnalysisPromptVariant>();
            builder.Services.AddSingleton<AnalysisPromptVariantRegistry>();
            builder.Services.AddSingleton<ISetUpgradePromptVariant, ChatGptSetUpgradePromptVariant>();
            builder.Services.AddSingleton<ISetUpgradePromptVariant, ClaudeSetUpgradePromptVariant>();
            builder.Services.AddSingleton<ISetUpgradePromptVariant, GeminiSetUpgradePromptVariant>();
            builder.Services.AddSingleton<SetUpgradePromptVariantRegistry>();
            builder.Services.AddSingleton<IComparisonPromptVariant, ChatGptComparisonPromptVariant>();
            builder.Services.AddSingleton<IComparisonPromptVariant, ClaudeComparisonPromptVariant>();
            builder.Services.AddSingleton<IComparisonPromptVariant, GeminiComparisonPromptVariant>();
            builder.Services.AddSingleton<ComparisonPromptVariantRegistry>();
            builder.Services.AddSingleton<IFollowUpPromptVariant, ChatGptFollowUpPromptVariant>();
            builder.Services.AddSingleton<IFollowUpPromptVariant, ClaudeFollowUpPromptVariant>();
            builder.Services.AddSingleton<IFollowUpPromptVariant, GeminiFollowUpPromptVariant>();
            builder.Services.AddSingleton<FollowUpPromptVariantRegistry>();
            builder.Services.AddSingleton<IMetaGapPromptVariant, ChatGptMetaGapPromptVariant>();
            builder.Services.AddSingleton<IMetaGapPromptVariant, ClaudeMetaGapPromptVariant>();
            builder.Services.AddSingleton<IMetaGapPromptVariant, GeminiMetaGapPromptVariant>();
            builder.Services.AddSingleton<MetaGapPromptVariantRegistry>();
            builder.Services.AddSingleton<IPrimerPromptVariant, ChatGptPrimerPromptVariant>();
            builder.Services.AddSingleton<IPrimerPromptVariant, ClaudePrimerPromptVariant>();
            builder.Services.AddSingleton<IPrimerPromptVariant, GeminiPrimerPromptVariant>();
            builder.Services.AddSingleton<PrimerPromptVariantRegistry>();

            builder.Services.AddScoped<IDeckAnalysisPacketService>(sp =>
                new DeckAnalysisPacketService(
                    sp.GetRequiredService<IScryfallCardResolver>(),
                    sp.GetRequiredService<IDeckEntryLoader>(),
                    sp.GetRequiredService<IMechanicLookupService>(),
                    sp.GetRequiredService<ICommanderBanListService>(),
                    sp.GetRequiredService<IScryfallSetService>(),
                    sp.GetRequiredService<ICommanderSpellbookService>(),
                    sp.GetRequiredService<AnalysisPromptVariantRegistry>(),
                    sp.GetRequiredService<SetUpgradePromptVariantRegistry>(),
                    sp.GetRequiredService<PacketSessionCache>(),
                    sp.GetService<ILogger<DeckAnalysisPacketService>>()));
            builder.Services.AddScoped<IDeckComparisonService>(sp =>
                new DeckComparisonService(
                    sp.GetRequiredService<IScryfallCardResolver>(),
                    sp.GetRequiredService<IDeckEntryLoader>(),
                    sp.GetRequiredService<ICommanderSpellbookService>(),
                    sp.GetRequiredService<ComparisonPromptVariantRegistry>(),
                    sp.GetRequiredService<FollowUpPromptVariantRegistry>(),
                    sp.GetRequiredService<PacketSessionCache>(),
                    sp.GetService<ILogger<DeckComparisonService>>()));
            builder.Services.AddScoped<IMetaGapService>(sp =>
                new MetaGapService(
                    sp.GetRequiredService<IScryfallCardResolver>(),
                    sp.GetRequiredService<IDeckEntryLoader>(),
                    sp.GetRequiredService<IEdhTop16Client>(),
                    sp.GetRequiredService<ICommanderSpellbookService>(),
                    sp.GetRequiredService<MetaGapPromptVariantRegistry>(),
                    sp.GetRequiredService<PacketSessionCache>()));
            builder.Services.AddScoped<IDeckPrimerPacketService>(sp =>
                new DeckPrimerPacketService(
                    sp.GetRequiredService<IDeckEntryLoader>(),
                    sp.GetRequiredService<ICommanderSpellbookService>(),
                    sp.GetRequiredService<IEdhTop16Client>(),
                    sp.GetRequiredService<ICategoryKnowledgeStore>(),
                    sp.GetRequiredService<PrimerPromptVariantRegistry>(),
                    sp.GetRequiredService<PacketSessionCache>(),
                    sp.GetRequiredService<IOptions<AiPlatformOptions>>(),
                    sp.GetService<ILogger<DeckPrimerPacketService>>()));
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
            app.Logger.LogInformation("Ensuring content site-index schema during startup.");
            await app.Services.GetRequiredService<DeckFlow.Core.Content.IContentSiteIndexStore>().EnsureSchemaAsync();
            await app.Services.GetRequiredService<IContentKbSeedLoader>().LoadIfPresentAsync();
            app.Logger.LogInformation("Content site-index schema ensured and seed load completed during startup.");

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
