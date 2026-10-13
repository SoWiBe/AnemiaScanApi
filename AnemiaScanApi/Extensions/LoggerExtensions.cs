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
        // Временный логгер — только на время сборки хоста, чтобы было куда
        // написать, если упадёт сама конфигурация.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            Log.Information("Smart Anemia Scan API started");

            // Приёмники берутся ТОЛЬКО из секции Serilog в appsettings.json. Раньше
            // здесь же стояли WriteTo.Console() и WriteTo.File("logs/log.txt") —
            // в дополнение к консоли и файлам из конфига. Отсюда каждая строка в
            // журнале сервиса дважды и третий файл без ротации и без срока хранения.
            builder.Host.UseSerilog((context, provider, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(provider)
                    .Enrich.FromLogContext();

                // Страховка: если секция Serilog не нашлась (сервис запущен не из
                // своей папки и не увидел appsettings.json), приёмников нет вовсе
                // и сервис молчит. Тогда пишем хотя бы в консоль — под systemd
                // это журнал.
                if (!HasConfiguredSinks(context.Configuration))
                    configuration.WriteTo.Console();
            });
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

    /// <summary>Есть ли в конфигурации хотя бы один приёмник логов (Serilog:WriteTo).</summary>
    public static bool HasConfiguredSinks(IConfiguration configuration)
        => configuration.GetSection("Serilog:WriteTo").GetChildren().Any();
}
