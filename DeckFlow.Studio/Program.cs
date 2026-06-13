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

            builder.Services.AddSingleton(new StudioConfig(isProdConfigured));
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();

            var app = builder.Build();

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
}
