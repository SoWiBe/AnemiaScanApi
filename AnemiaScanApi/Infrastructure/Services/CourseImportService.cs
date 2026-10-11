using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Infrastructure.Courses;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;

namespace AnemiaScanApi.Services;

/// <summary>
/// Импорт курса из файла в базу.
///
/// ДВА ПРАВИЛА, РАДИ КОТОРЫХ ЭТОТ КЛАСС СУЩЕСТВУЕТ:
///
/// 1. <b>Id задач переживают повторный импорт.</b> Прогресс пользователя
///    (<see cref="DayCompletion.CompletedTaskIds"/>) ссылается на id задач. Если
///    при каждой правке текста выдавать новые id, у всех, кто идёт по курсу,
///    слетят галочки. Поэтому id берётся у задачи, стоявшей в том же дне на том
///    же месте.
/// 2. <b>Дни пишутся раньше карточки.</b> Транзакций нет (Mongo без реплика-сета),
///    и порядок выбран так, чтобы обрыв посередине чинился сам: отпечаток файла
///    лежит в карточке и пишется последним. Упали после дней, но до карточки —
///    отпечаток старый, следующий запуск повторит импорт целиком.
/// </summary>
public class CourseImportService(
    ICoursesRepository coursesRepository,
    ICourseContentRepository courseContentRepository,
    ILogger<CourseImportService> logger)
    : BaseService<CourseImportService>(logger), ICourseImportService
{
    public async Task<CourseImportResult> ImportAsync(CourseDocument document, string contentHash,
        CancellationToken cancellationToken = default)
    {
        var slug = document.Slug!;
        var existing = await coursesRepository.FindBySlugAsync(slug, cancellationToken);

        if (existing is not null && existing.ContentHash == contentHash)
            return new CourseImportResult(CourseImportOutcome.Unchanged, existing.Id, []);

        var now = DateTime.UtcNow;
        var courseId = existing?.Id ?? Guid.NewGuid();
        var existingContent = existing is null
            ? null
            : await courseContentRepository.GetByCourseIdAsync(courseId, cancellationToken);

        var warnings = new List<string>();
        var days = BuildDays(document, existingContent, warnings);

        if (existingContent is null)
        {
            await courseContentRepository.CreateAsync(
                new CourseContent { Id = Guid.NewGuid(), CourseId = courseId, Days = days, UpdatedAt = now },
                cancellationToken);
        }
        else
        {
            existingContent.Days = days;
            existingContent.UpdatedAt = now;
            await courseContentRepository.UpdateAsync(existingContent.Id, existingContent, cancellationToken);
        }

        var course = existing ?? new Course { Id = courseId, CreatedAt = now };
        course.Slug = slug;
        course.Title = document.Title!.Trim();
        course.Description = document.Description!.Trim();
        course.TargetAudience = document.TargetAudience!.Value;
        course.DurationDays = document.DurationDays!.Value;
        course.ContentStatus = document.ContentStatus;
        course.DoctorReviewerName = string.IsNullOrWhiteSpace(document.DoctorReviewerName)
            ? null
            : document.DoctorReviewerName.Trim();
        // Дата первой публикации. При возврате в черновик не сбрасывается —
        // это история, а видимость в каталоге определяет только статус.
        if (course.ContentStatus == CourseContentStatus.Published)
            course.PublishedAt ??= now;
        course.UpdatedAt = now;
        course.ContentHash = contentHash;

        if (existing is null)
            await coursesRepository.CreateAsync(course, cancellationToken);
        else
            await coursesRepository.UpdateAsync(course.Id, course, cancellationToken);

        foreach (var warning in warnings)
            Logger.LogWarning("Курс {Slug}: {Warning}", slug, warning);

        return new CourseImportResult(
            existing is null ? CourseImportOutcome.Created : CourseImportOutcome.Updated, courseId, warnings);
    }

    /// <summary>
    /// Собирает дни курса, перенося id задач из уже сохранённого контента.
    /// </summary>
    private static List<CourseDay> BuildDays(CourseDocument document, CourseContent? existingContent,
        List<string> warnings)
    {
        var existingDays = (existingContent?.Days ?? []).ToDictionary(d => d.DayNumber);

        // Id, заданные в файле явно, имеют приоритет, и унаследованный id не
        // должен с ними совпасть: иначе при перестановке задач с явными id две
        // задачи получили бы один идентификатор.
        var takenIds = document.Days!
            .SelectMany(d => d.Tasks ?? [])
            .Where(t => t.Id is not null)
            .Select(t => t.Id!.Value)
            .ToHashSet();

        var days = new List<CourseDay>();

        foreach (var day in document.Days!.OrderBy(d => d.DayNumber))
        {
            var dayNumber = day.DayNumber!.Value;
            existingDays.TryGetValue(dayNumber, out var existingDay);
            var tasks = new List<CourseTask>();
            var source = day.Tasks ?? [];

            for (var i = 0; i < source.Count; i++)
            {
                var inherited = existingDay?.Tasks.ElementAtOrDefault(i)?.Id;
                var id = source[i].Id
                         ?? (inherited is { } candidate && takenIds.Add(candidate) ? candidate : NewId(takenIds));

                tasks.Add(new CourseTask
                {
                    Id = id,
                    Title = source[i].Title!.Trim(),
                    Description = string.IsNullOrWhiteSpace(source[i].Description) ? null : source[i].Description!.Trim()
                });
            }

            if (existingDay is not null)
            {
                var kept = tasks.Select(t => t.Id).ToHashSet();
                var dropped = existingDay.Tasks.Count(t => !kept.Contains(t.Id));
                if (dropped > 0)
                    warnings.Add($"в дне {dayNumber} убрано задач: {dropped} — если курс уже проходят, " +
                                 "отметки пользователей по этим задачам пропадут");
            }

            days.Add(new CourseDay
            {
                DayNumber = dayNumber,
                Theory = day.Theory!.Trim(),
                IsRescanCheckpoint = day.IsRescanCheckpoint,
                Tasks = tasks
            });
        }

        var removedDays = existingDays.Keys.Except(days.Select(d => d.DayNumber)).OrderBy(n => n).ToList();
        if (removedDays.Count > 0)
            warnings.Add($"курс стал короче, убраны дни: {string.Join(", ", removedDays)} — " +
                         "у тех, кто уже проходит курс, прогресс по ним больше не показывается");

        return days;
    }

    private static Guid NewId(HashSet<Guid> takenIds)
    {
        var id = Guid.NewGuid();
        takenIds.Add(id);
        return id;
    }
}
