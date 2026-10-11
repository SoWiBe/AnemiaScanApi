using System.Security.Cryptography;
using System.Text;

using AnemiaScanApi.Infrastructure.Services.Core;

namespace AnemiaScanApi.Infrastructure.Courses;

public enum CourseFileStatus
{
    Created,
    Updated,
    Unchanged,

    /// <summary>Файл не прошёл проверку — это правит автор контента.</summary>
    Invalid,

    /// <summary>Файл в порядке, но запись в базу не удалась — это чинится повтором.</summary>
    Failed
}

public sealed record CourseFileResult(string File, CourseFileStatus Status, IReadOnlyList<string> Messages);

public sealed record CourseSyncReport(IReadOnlyList<CourseFileResult> Files)
{
    public int Count(CourseFileStatus status) => Files.Count(f => f.Status == status);

    /// <summary>Есть сбои записи в базу — синхронизацию стоит повторить.</summary>
    public bool HasFailures => Files.Any(f => f.Status == CourseFileStatus.Failed);
}

/// <summary>
/// Приводит базу к содержимому папки с курсами: каждый <c>*.json</c> — один курс.
///
/// КАК КУРС ПОПАДАЕТ НА СЕРВЕР. Файл кладётся в <c>Content/Courses/</c> в
/// репозитории, проходит ревью в pull request, уезжает с деплоем и при старте
/// сервиса загружается в базу. Отдельного шага «залить курс» нет, как нет и
/// админского эндпоинта: всё, что видит пользователь, прошло через git.
///
/// ЧЕГО СИНХРОНИЗАЦИЯ НЕ ДЕЛАЕТ. Курс, файл которого удалили из папки, из базы
/// не удаляется: на него ссылаются записи пользователей. Чтобы убрать курс из
/// каталога, ему ставят <c>contentStatus: "Draft"</c>.
///
/// Один плохой файл не мешает остальным и не роняет сервис — он попадает в
/// отчёт и в лог с перечнем ошибок.
/// </summary>
public class CourseContentSync(ICourseImportService importService, ILogger<CourseContentSync> logger)
{
    /// <summary>
    /// Подмешивается в отпечаток файла. Поднять, когда меняется сама логика
    /// импорта: тогда все курсы перезагрузятся, хотя файлы не менялись.
    /// </summary>
    private const string ImporterVersion = "course-import-v1";

    public async Task<CourseSyncReport> SyncAsync(string folder, CancellationToken cancellationToken = default)
    {
        var results = new List<CourseFileResult>();

        foreach (var path in Directory.EnumerateFiles(folder, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await SyncFileAsync(path, cancellationToken));
        }

        return new CourseSyncReport(results);
    }

    private async Task<CourseFileResult> SyncFileAsync(string path, CancellationToken cancellationToken)
    {
        var file = Path.GetFileName(path);
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var parsed = CourseDocumentParser.Parse(json);

        if (!parsed.IsValid)
            return Invalid(file, parsed.Errors);

        var document = parsed.Document!;

        // Имя файла = slug. Тогда два файла не могут описывать один курс, а курс
        // в папке находится по адресу из URL.
        var expectedName = document.Slug + ".json";
        if (!string.Equals(file, expectedName, StringComparison.Ordinal))
            return Invalid(file, [$"имя файла должно совпадать со slug: ожидается {expectedName}"]);

        try
        {
            var result = await importService.ImportAsync(document, Fingerprint(json), cancellationToken);
            var status = result.Outcome switch
            {
                CourseImportOutcome.Created => CourseFileStatus.Created,
                CourseImportOutcome.Updated => CourseFileStatus.Updated,
                _ => CourseFileStatus.Unchanged
            };

            if (status != CourseFileStatus.Unchanged)
                logger.LogInformation("Курс {Slug} ({Status}): {Days} дн., статус {ContentStatus}",
                    document.Slug, status, document.DurationDays, document.ContentStatus);

            return new CourseFileResult(file, status, result.Warnings);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Курс из файла {File} не записан в базу", file);
            return new CourseFileResult(file, CourseFileStatus.Failed, [e.Message]);
        }
    }

    private CourseFileResult Invalid(string file, IReadOnlyList<string> errors)
    {
        logger.LogError("Файл курса {File} не загружен, ошибок: {Count}. {Errors}",
            file, errors.Count, string.Join(" | ", errors));
        return new CourseFileResult(file, CourseFileStatus.Invalid, errors);
    }

    public static string Fingerprint(string json)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ImporterVersion + "\n" + json)));
}
