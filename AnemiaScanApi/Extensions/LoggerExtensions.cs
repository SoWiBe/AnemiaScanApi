using Serilog;

namespace AnemiaScanApi.Extensions;

/// <summary>
/// Extension methods for configuring logging.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Adds logging configuration
    /// </summary>
    /// <param name="builder"></param>
    public static void AddLogging(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            Log.Information("Smart Anemia Scan API started");
            
            builder.Host.UseSerilog((context, provider, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(provider)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("logs/log.txt"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error starting Smart Anemia Scan API");
        }    
        finally
        {
            Log.CloseAndFlush();
        }
    }
}