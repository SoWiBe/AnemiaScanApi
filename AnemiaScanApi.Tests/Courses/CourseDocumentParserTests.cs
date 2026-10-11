using System.Text.Json;
using System.Text.Json.Nodes;

using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Infrastructure.Courses;

using FluentAssertions;

namespace AnemiaScanApi.Tests.Courses;

/// <summary>
/// Проверка файла курса. Автор контента — не программист, поэтому важны две
/// вещи: плохой файл не проходит, и ошибка говорит, где именно и что не так.
/// </summary>
public class CourseDocumentParserTests
{
    /// <summary>Корректный курс на три дня; тесты портят в нём по одному полю.</summary>
    internal static JsonObject ValidCourse(string slug = "iron-basics-3", string status = "Draft") => new()
    {
        ["slug"] = slug,
        ["title"] = "Железо в тарелке",
        ["description"] = "Короткий курс о питании.",
        ["targetAudience"] = "Adult",
        ["durationDays"] = 3,
        ["contentStatus"] = status,
        ["days"] = new JsonArray(Day(1, checkpoint: true), Day(2), Day(3))
    };

    internal static JsonObject Day(int number, bool checkpoint = false, int tasks = 2) => new()
    {
        ["dayNumber"] = number,
        ["isRescanCheckpoint"] = checkpoint,
        ["theory"] = $"## День {number}\n\nТекст.",
        ["tasks"] = new JsonArray(Enumerable.Range(1, tasks)
            .Select(i => (JsonNode)new JsonObject { ["title"] = $"Задача {number}.{i}" })
            .ToArray())
    };

    private static CourseParseResult Parse(JsonObject course) => CourseDocumentParser.Parse(course.ToJsonString());

    private static JsonObject DayOf(JsonObject course, int index) => (JsonObject)course["days"]![index]!;

    [Fact]
    public void ValidCourse_ParsesWithAllFields()
    {
        var result = Parse(ValidCourse());

        result.Errors.Should().BeEmpty();
        result.IsValid.Should().BeTrue();

        var course = result.Document!;
        course.Slug.Should().Be("iron-basics-3");
        course.TargetAudience.Should().Be(TargetAudience.Adult);
        course.ContentStatus.Should().Be(CourseContentStatus.Draft);
        course.Days.Should().HaveCount(3);
        course.Days![0].IsRescanCheckpoint.Should().BeTrue();
        course.Days[1].Tasks.Should().HaveCount(2);
    }

    [Fact]
    public void ContentStatus_DefaultsToDraft()
    {
        var course = ValidCourse();
        course.Remove("contentStatus");

        // Забытый статус не должен публиковать курс.
        Parse(course).Document!.ContentStatus.Should().Be(CourseContentStatus.Draft);
    }

    [Theory]
    [InlineData("Iron-Basics")]     // заглавные
    [InlineData("iron_basics")]     // подчёркивание
    [InlineData("iron--basics")]    // двойной дефис
    [InlineData("-iron")]           // дефис в начале
    [InlineData("железо")]          // кириллица
    [InlineData("ab")]              // короче трёх символов
    public void Slug_MustBeLowercaseLatinWithDashes(string slug)
    {
        var result = Parse(ValidCourse(slug));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().StartWith("slug:");
    }

    [Theory]
    [InlineData("slug")]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("targetAudience")]
    [InlineData("durationDays")]
    [InlineData("days")]
    public void MissingRequiredField_IsNamedInError(string field)
    {
        var course = ValidCourse();
        course.Remove(field);

        var result = Parse(course);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.StartsWith(field + ":"));
    }

    [Fact]
    public void DurationDays_MustMatchNumberOfDays()
    {
        var course = ValidCourse();
        course["durationDays"] = 7;

        Parse(course).Errors.Should().ContainSingle()
            .Which.Should().Contain("указано 7").And.Contain("дней в файле 3");
    }

    [Fact]
    public void DayNumbers_MustNotRepeat()
    {
        var course = ValidCourse();
        DayOf(course, 2)["dayNumber"] = 2;

        Parse(course).Errors.Should().ContainSingle().Which.Should().Contain("день 2 встречается дважды");
    }

    [Fact]
    public void DayNumbers_MustStartFromOneWithoutGaps()
    {
        var course = ValidCourse();
        DayOf(course, 2)["dayNumber"] = 5;

        Parse(course).Errors.Should().ContainSingle().Which.Should().Contain("не хватает: 3");
    }

    [Fact]
    public void DaysInAnyOrder_AreAccepted()
    {
        var course = ValidCourse();
        course["days"] = new JsonArray(Day(3), Day(1), Day(2));

        Parse(course).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyTheory_PointsToTheDay()
    {
        var course = ValidCourse();
        DayOf(course, 1)["theory"] = "   ";

        Parse(course).Errors.Should().ContainSingle().Which.Should().Be("days[1].theory: пустая теория");
    }

    [Fact]
    public void EmptyTaskTitle_PointsToTheTask()
    {
        var course = ValidCourse();
        ((JsonObject)DayOf(course, 2)["tasks"]![1]!)["title"] = "";

        Parse(course).Errors.Should().ContainSingle().Which.Should().Be("days[2].tasks[1].title: не заполнен");
    }

    [Fact]
    public void TooLongTitle_IsRejected()
    {
        var course = ValidCourse();
        course["title"] = new string('я', CourseDocumentParser.TitleMaxLength + 1);

        Parse(course).Errors.Should().ContainSingle().Which.Should().StartWith("title: длиннее 200");
    }

    [Fact]
    public void UnknownField_IsRejected_NotSilentlyIgnored()
    {
        var course = ValidCourse();
        // Опечатка в регистре: без запрета неизвестных полей чекпоинт молча
        // превратился бы в обычный день.
        DayOf(course, 0).Remove("isRescanCheckpoint");
        DayOf(course, 0)["isRescanCheckPoint"] = true;

        var result = Parse(course);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("неизвестное поле");
    }

    [Fact]
    public void UnknownAudience_IsRejected()
    {
        var course = ValidCourse();
        course["targetAudience"] = "Teenager";

        var result = Parse(course);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("targetAudience");
    }

    [Fact]
    public void MalformedJson_ReportsLineNumber()
    {
        const string json = "{\n  \"slug\": \"iron-basics-3\",\n  \"title\": \n}";

        var result = CourseDocumentParser.Parse(json);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("строка 4");
    }

    [Fact]
    public void AllErrors_AreReportedAtOnce()
    {
        var course = ValidCourse("Bad Slug");
        course["title"] = "";
        DayOf(course, 0)["theory"] = "";

        // Автор не должен чинить файл по одной ошибке за запуск.
        Parse(course).Errors.Should().HaveCount(3);
    }

    [Fact]
    public void DuplicateTaskIds_AreRejected()
    {
        var id = Guid.NewGuid().ToString();
        var course = ValidCourse();
        ((JsonObject)DayOf(course, 0)["tasks"]![0]!)["id"] = id;
        ((JsonObject)DayOf(course, 1)["tasks"]![0]!)["id"] = id;

        Parse(course).Errors.Should().ContainSingle().Which.Should().Contain("уже использован");
    }

    [Fact]
    public void PublishedCourse_RejectsDayThatCannotBeCompleted()
    {
        var course = ValidCourse(status: "Published");
        course["days"] = new JsonArray(Day(1, checkpoint: true), Day(2, tasks: 0), Day(3));

        // День без задач и без пересъёмки закрыть нечем — курс не дойдёт до конца.
        Parse(course).Errors.Should().ContainSingle().Which.Should().StartWith("days[1]:");
    }

    [Fact]
    public void PublishedCourse_AllowsCheckpointDayWithoutTasks()
    {
        var course = ValidCourse(status: "Published");
        course["days"] = new JsonArray(Day(1, checkpoint: true, tasks: 0), Day(2), Day(3));

        Parse(course).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DraftCourse_MayHaveDaysWithoutTasks()
    {
        var course = ValidCourse();
        course["days"] = new JsonArray(Day(1), Day(2, tasks: 0), Day(3));

        Parse(course).IsValid.Should().BeTrue("черновик можно сохранять недописанным");
    }

    /// <summary>
    /// Схема для автора (docs/schemas/course.schema.json) и проверки импортёра не
    /// должны расходиться в лимитах: файл, прошедший валидатор схемы, обязан
    /// загрузиться.
    /// </summary>
    [Fact]
    public void Limits_MatchPublishedJsonSchema()
    {
        var schemaPath = Path.Combine(FindRepoRoot(), "AnemiaScanApi", "docs", "schemas", "course.schema.json");
        using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var properties = schema.RootElement.GetProperty("properties");
        var task = schema.RootElement.GetProperty("$defs").GetProperty("task").GetProperty("properties");

        properties.GetProperty("slug").GetProperty("minLength").GetInt32().Should().Be(CourseDocumentParser.SlugMinLength);
        properties.GetProperty("slug").GetProperty("maxLength").GetInt32().Should().Be(CourseDocumentParser.SlugMaxLength);
        properties.GetProperty("title").GetProperty("maxLength").GetInt32().Should().Be(CourseDocumentParser.TitleMaxLength);
        properties.GetProperty("description").GetProperty("maxLength").GetInt32().Should().Be(CourseDocumentParser.DescriptionMaxLength);
        properties.GetProperty("durationDays").GetProperty("maximum").GetInt32().Should().Be(CourseDocumentParser.MaxDurationDays);
        properties.GetProperty("doctorReviewerName").GetProperty("maxLength").GetInt32().Should().Be(CourseDocumentParser.ReviewerMaxLength);
        task.GetProperty("title").GetProperty("maxLength").GetInt32().Should().Be(CourseDocumentParser.TaskTitleMaxLength);
        task.GetProperty("description").GetProperty("maxLength").GetInt32().Should().Be(CourseDocumentParser.TaskDescriptionMaxLength);
    }

    internal static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AnemiaScanApi.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Не найден корень репозитория (AnemiaScanApi.sln)");
    }
}
