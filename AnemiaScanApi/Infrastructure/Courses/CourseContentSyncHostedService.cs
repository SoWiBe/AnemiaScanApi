using AnemiaScanApi.Settings;

using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Infrastructure.Courses;

/// <summary>
/// Запускает <see cref="CourseContentSync"/> один раз при старте сервиса.
///
/// Работает в фоне и никогда не роняет приложение: API должен подняться, даже
/// если файл курса кривой или база ещё не ответила. Необработанное исключение в
/// BackgroundService по умолчанию останавливает хост — поэтому здесь ловится всё.
/// </summary>
public class CourseContentSyncHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<CourseContentSettings> settings,
    ILogger<CourseContentSyncHostedService> logger) : BackgroundService
{
    /// <summary>
    /// После перезагрузки сервера API может стартовать раньше, чем Mongo начнёт
    /// принимать соединения. Без повтора курсы остались бы незагруженными до
    /// следующего деплоя.
    /// </summary>
    private const int MaxAttempts = 5;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Сразу отдаём управление: до первого await ExecuteAsync выполняется
        // внутри StartAsync и задерживал бы запуск приложения.
        await Task.Yield();

        try
        {
            if (!settings.Value.ImportOnStartup)
            {
                logger.LogInformation("Загрузка курсов при старте выключена (CourseContent:ImportOnStartup)");
                return;
            }

            var folder = ResolveFolder(settings.Value.Path);
            if (!Directory.Exists(folder))
            {
                logger.LogWarning("Папки с курсами нет: {Folder} — каталог останется как есть", folder);
                return;
            }

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                using var scope = scopeFactory.CreateScope();
                var report = await scope.ServiceProvider.GetRequiredService<CourseContentSync>()
                    .SyncAsync(folder, stoppingToken);

                logger.LogInformation(
                    "Курсы синхронизированы из {Folder}: новых {Created}, обновлено {Updated}, без изменений {Unchanged}, " +
                    "с ошибками в файле {Invalid}, не записано {Failed}",
                    folder, report.Count(CourseFileStatus.Created), report.Count(CourseFileStatus.Updated),
                    report.Count(CourseFileStatus.Unchanged), report.Count(CourseFileStatus.Invalid),
                    report.Count(CourseFileStatus.Failed));

                if (!report.HasFailures) return;

                if (attempt < MaxAttempts)
                {
                    logger.LogWarning("Часть курсов не записана, повтор {Next}/{Max} через {Delay} с",
                        attempt + 1, MaxAttempts, RetryDelay.TotalSeconds);
                    await Task.Delay(RetryDelay, stoppingToken);
                }
            }

            logger.LogError("Курсы не удалось записать за {Max} попыток — нужен перезапуск сервиса", MaxAttempts);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Сервис останавливают — это не ошибка.
        }
        catch (Exception e)
        {
            logger.LogError(e, "Синхронизация курсов прервана");
        }
    }

    public static string ResolveFolder(string? configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath) ? CourseContentSettings.DefaultPath : configuredPath;
        return Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
    }
}
