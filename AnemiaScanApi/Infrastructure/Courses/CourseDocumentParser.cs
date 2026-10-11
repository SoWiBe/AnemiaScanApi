using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Infrastructure.Courses;

/// <summary>
/// Результат разбора файла курса. Либо документ без ошибок, либо список ошибок
/// для автора — третьего не бывает.
/// </summary>
public sealed record CourseParseResult(CourseDocument? Document, IReadOnlyList<string> Errors)
{
    public bool IsValid => Document is not null && Errors.Count == 0;
}

/// <summary>
/// Разбирает и проверяет файл курса.
///
/// Проверки повторяют docs/schemas/course.schema.json и добавляют то, что схемой
/// не выразить: совпадение <c>durationDays</c> с числом дней, нумерацию дней без
/// дыр, уникальность id задач, полноту опубликованного курса.
///
/// Сообщения написаны для автора контента, а не для программиста: с путём до
/// поля (<c>days[3].tasks[1].title</c>) и тем, что с ним не так.
/// </summary>
public static partial class CourseDocumentParser
{
    public const int SlugMinLength = 3;
    public const int SlugMaxLength = 64;
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 1000;
    public const int ReviewerMaxLength = 200;
    public const int TaskTitleMaxLength = 200;
    public const int TaskDescriptionMaxLength = 2000;
    public const int MaxDurationDays = 365;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Неизвестное поле — ошибка, а не тихий пропуск: опечатка в имени
        // (isRescanCheckPoint) иначе молча превращала бы чекпоинт в обычный день.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    public static CourseParseResult Parse(string json)
    {
        CourseDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<CourseDocument>(json, JsonOptions);
        }
        catch (JsonException e)
        {
            return new CourseParseResult(null, [DescribeJsonError(e)]);
        }

        if (document is null)
            return new CourseParseResult(null, ["Файл пуст: ожидается JSON-объект курса"]);

        var errors = Validate(document);
        return new CourseParseResult(errors.Count == 0 ? document : null, errors);
    }

    public static List<string> Validate(CourseDocument course)
    {
        var errors = new List<string>();

        ValidateCard(course, errors);
        ValidateDays(course, errors);

        return errors;
    }

    private static void ValidateCard(CourseDocument course, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(course.Slug))
            errors.Add("slug: не заполнен");
        else if (course.Slug.Length is < SlugMinLength or > SlugMaxLength)
            errors.Add($"slug: длина должна быть от {SlugMinLength} до {SlugMaxLength} символов");
        else if (!SlugPattern().IsMatch(course.Slug))
            errors.Add($"slug: «{course.Slug}» — допустимы только строчные латинские буквы, цифры и дефисы между ними");

        RequireText(course.Title, "title", TitleMaxLength, errors);
        RequireText(course.Description, "description", DescriptionMaxLength, errors);

        if (course.TargetAudience is null)
            errors.Add($"targetAudience: не заполнен — одно из {string.Join(", ", Enum.GetNames<TargetAudience>())}");

        if (course.DoctorReviewerName is { Length: > ReviewerMaxLength })
            errors.Add($"doctorReviewerName: длиннее {ReviewerMaxLength} символов");

        if (course.DurationDays is null)
            errors.Add("durationDays: не заполнен");
        else if (course.DurationDays is < 1 or > MaxDurationDays)
            errors.Add($"durationDays: должно быть от 1 до {MaxDurationDays}");
    }

    private static void ValidateDays(CourseDocument course, List<string> errors)
    {
        if (course.Days is null || course.Days.Count == 0)
        {
            errors.Add("days: в курсе нет ни одного дня");
            return;
        }

        if (course.DurationDays is { } duration && duration != course.Days.Count)
            errors.Add($"durationDays: указано {duration}, а дней в файле {course.Days.Count} — числа должны совпадать");

        var seenDayNumbers = new HashSet<int>();
        var seenTaskIds = new HashSet<Guid>();
        var published = course.ContentStatus == CourseContentStatus.Published;

        for (var i = 0; i < course.Days.Count; i++)
        {
            var day = course.Days[i];
            var path = $"days[{i}]";

            if (day.DayNumber is null)
                errors.Add($"{path}.dayNumber: не заполнен");
            else if (!seenDayNumbers.Add(day.DayNumber.Value))
                errors.Add($"{path}.dayNumber: день {day.DayNumber} встречается дважды");

            if (string.IsNullOrWhiteSpace(day.Theory))
                errors.Add($"{path}.theory: пустая теория");

            var tasks = day.Tasks ?? [];
            for (var t = 0; t < tasks.Count; t++)
            {
                var task = tasks[t];
                var taskPath = $"{path}.tasks[{t}]";

                RequireText(task.Title, $"{taskPath}.title", TaskTitleMaxLength, errors);

                if (task.Description is { Length: > TaskDescriptionMaxLength })
                    errors.Add($"{taskPath}.description: длиннее {TaskDescriptionMaxLength} символов");

                if (task.Id is { } id)
                {
                    if (id == Guid.Empty)
                        errors.Add($"{taskPath}.id: пустой идентификатор — уберите поле, импортёр проставит id сам");
                    else if (!seenTaskIds.Add(id))
                        errors.Add($"{taskPath}.id: идентификатор {id} уже использован в другой задаче");
                }
            }

            // День без задач и без пересъёмки нельзя завершить: день закрывается
            // отметкой последней задачи или прикреплённым сканом (см.
            // CourseEnrollmentService.TryMarkDayCompleted). Один такой день — и
            // курс не сможет пройти до конца никто.
            if (published && tasks.Count == 0 && !day.IsRescanCheckpoint)
                errors.Add($"{path}: в опубликованном курсе у дня должна быть хотя бы одна задача или пересъёмка, " +
                           "иначе его нельзя завершить");
        }

        // Номера должны быть 1..N без пропусков. Проверяем по множеству, а не по
        // порядку в файле: дни могут лежать в любом порядке. Если номера уже
        // пропущены или повторяются, об этом сказано выше — второй раз не пишем.
        var allNumberedOnce = seenDayNumbers.Count == course.Days.Count;
        if (allNumberedOnce)
        {
            var missing = Enumerable.Range(1, course.Days.Count).Where(n => !seenDayNumbers.Contains(n)).ToList();
            if (missing.Count > 0)
                errors.Add($"days: номера дней должны идти от 1 до {course.Days.Count} без пропусков — " +
                           $"не хватает: {string.Join(", ", missing)}");
        }
    }

    private static void RequireText(string? value, string path, int maxLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{path}: не заполнен");
        else if (value.Length > maxLength)
            errors.Add($"{path}: длиннее {maxLength} символов (сейчас {value.Length})");
    }

    /// <summary>
    /// Ошибка разбора в терминах файла: строка и поле. У JsonException номер
    /// строки с нуля — автору показываем с единицы, как в редакторе.
    /// </summary>
    private static string DescribeJsonError(JsonException e)
    {
        var where = e.LineNumber is { } line ? $"строка {line + 1}" : "неизвестная строка";
        var path = string.IsNullOrEmpty(e.Path) || e.Path == "$" ? "" : $", поле {e.Path.TrimStart('$', '.')}";

        // Самая частая причина — лишнее поле или опечатка в его имени.
        var reason = e.Message.Contains("could not be mapped", StringComparison.OrdinalIgnoreCase)
            ? "неизвестное поле — проверьте имя по docs/course-content-schema.md"
            : e.Message.Contains("could not be converted", StringComparison.OrdinalIgnoreCase)
                ? "значение не того типа или не из списка допустимых"
                : "некорректный JSON";

        return $"Файл не разобран ({where}{path}): {reason}. Подробности: {e.Message}";
    }
}
