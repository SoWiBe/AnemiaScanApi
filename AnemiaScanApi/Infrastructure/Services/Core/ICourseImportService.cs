using AnemiaScanApi.Infrastructure.Courses;

namespace AnemiaScanApi.Infrastructure.Services.Core;

public enum CourseImportOutcome
{
    /// <summary>Курса с таким slug не было — заведён.</summary>
    Created,

    /// <summary>Курс был, файл изменился — карточка и дни обновлены.</summary>
    Updated,

    /// <summary>Отпечаток файла совпал с сохранённым — в базу ничего не писали.</summary>
    Unchanged
}

/// <param name="Warnings">
/// То, что импорт не остановило, но автору стоит знать: например, что из идущего
/// курса убрали задачи и отметки пользователей по ним пропадут.
/// </param>
public sealed record CourseImportResult(CourseImportOutcome Outcome, Guid CourseId, IReadOnlyList<string> Warnings);

/// <summary>
/// Кладёт проверенный файл курса в базу: карточку в Courses, дни в CourseContent
/// (P0 №3 в docs/plans/MVP_PLAN.md).
/// </summary>
public interface ICourseImportService
{
    /// <param name="document">Документ, уже прошедший <see cref="CourseDocumentParser"/>.</param>
    /// <param name="contentHash">Отпечаток исходного файла — по нему пропускается повторный импорт.</param>
    Task<CourseImportResult> ImportAsync(CourseDocument document, string contentHash,
        CancellationToken cancellationToken = default);
}
