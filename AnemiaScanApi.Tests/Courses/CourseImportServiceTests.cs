using System.Text.Json.Nodes;

using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Infrastructure.Courses;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Services;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AnemiaScanApi.Tests.Courses;

/// <summary>
/// Импорт курса в базу. Главное здесь — повторный импорт: он не должен ломать
/// прогресс тех, кто уже проходит курс, и не должен писать в базу без причины.
/// </summary>
public class CourseImportServiceTests
{
    private readonly Mock<ICoursesRepository> _courses = new();
    private readonly Mock<ICourseContentRepository> _content = new();

    /// <summary>Что сейчас «лежит в базе» — репозитории ниже читают и пишут сюда.</summary>
    private Course? _storedCourse;
    private CourseContent? _storedContent;
    private readonly List<string> _writes = [];

    public CourseImportServiceTests()
    {
        _courses.Setup(r => r.FindBySlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string slug, CancellationToken _) => _storedCourse?.Slug == slug ? _storedCourse : null);
        _courses.Setup(r => r.CreateAsync(It.IsAny<Course>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Course c, CancellationToken _) => { _storedCourse = c; _writes.Add("course:create"); return c; });
        _courses.Setup(r => r.UpdateAsync(It.IsAny<Guid>(), It.IsAny<Course>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Course c, CancellationToken _) => { _storedCourse = c; _writes.Add("course:update"); return c; });

        _content.Setup(r => r.GetByCourseIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _storedContent?.CourseId == id ? _storedContent : null);
        _content.Setup(r => r.CreateAsync(It.IsAny<CourseContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CourseContent c, CancellationToken _) => { _storedContent = c; _writes.Add("content:create"); return c; });
        _content.Setup(r => r.UpdateAsync(It.IsAny<Guid>(), It.IsAny<CourseContent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, CourseContent c, CancellationToken _) => { _storedContent = c; _writes.Add("content:update"); return c; });
    }

    private CourseImportService NewService() => new(
        _courses.Object, _content.Object, NullLogger<CourseImportService>.Instance);

    private static CourseDocument Document(JsonObject json)
    {
        var parsed = CourseDocumentParser.Parse(json.ToJsonString());
        parsed.Errors.Should().BeEmpty();
        return parsed.Document!;
    }

    private Task<CourseImportResult> Import(JsonObject json, string hash)
        => NewService().ImportAsync(Document(json), hash);

    private static JsonObject Course(string status = "Draft") => CourseDocumentParserTests.ValidCourse(status: status);

    private List<Guid> TaskIds(int dayNumber)
        => _storedContent!.Days.Single(d => d.DayNumber == dayNumber).Tasks.Select(t => t.Id).ToList();

    [Fact]
    public async Task NewCourse_IsCreatedWithCardAndDays()
    {
        var result = await Import(Course(), "hash-1");

        result.Outcome.Should().Be(CourseImportOutcome.Created);
        result.Warnings.Should().BeEmpty();

        _storedCourse!.Id.Should().Be(result.CourseId);
        _storedCourse.Slug.Should().Be("iron-basics-3");
        _storedCourse.Title.Should().Be("Железо в тарелке");
        _storedCourse.TargetAudience.Should().Be(TargetAudience.Adult);
        _storedCourse.DurationDays.Should().Be(3);
        _storedCourse.ContentStatus.Should().Be(CourseContentStatus.Draft);
        _storedCourse.ContentHash.Should().Be("hash-1");

        _storedContent!.CourseId.Should().Be(result.CourseId);
        _storedContent.Days.Select(d => d.DayNumber).Should().Equal(1, 2, 3);
        _storedContent.Days[0].IsRescanCheckpoint.Should().BeTrue();
        _storedContent.Days.SelectMany(d => d.Tasks).Select(t => t.Id)
            .Should().OnlyHaveUniqueItems().And.NotContain(Guid.Empty);
    }

    [Fact]
    public async Task DaysAreStoredInOrder_EvenIfFileIsShuffled()
    {
        var course = Course();
        course["days"] = new JsonArray(
            CourseDocumentParserTests.Day(3), CourseDocumentParserTests.Day(1), CourseDocumentParserTests.Day(2));

        await Import(course, "hash-1");

        _storedContent!.Days.Select(d => d.DayNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ContentIsWrittenBeforeCard()
    {
        await Import(Course(), "hash-1");
        await Import(Course("Published"), "hash-2");

        // Отпечаток лежит в карточке. Если упасть между двумя записями, он
        // останется старым и следующий запуск повторит импорт. В обратном
        // порядке карточка уже считалась бы свежей при старых днях.
        _writes.Should().Equal("content:create", "course:create", "content:update", "course:update");
    }

    [Fact]
    public async Task SameHash_WritesNothing()
    {
        await Import(Course(), "hash-1");
        _writes.Clear();

        var result = await Import(Course(), "hash-1");

        result.Outcome.Should().Be(CourseImportOutcome.Unchanged);
        result.CourseId.Should().Be(_storedCourse!.Id);
        _writes.Should().BeEmpty("синхронизация идёт при каждом старте сервиса");
    }

    [Fact]
    public async Task Reimport_KeepsCourseIdAndTaskIds()
    {
        await Import(Course(), "hash-1");
        var courseId = _storedCourse!.Id;
        var idsBefore = _storedContent!.Days.ToDictionary(d => d.DayNumber, d => d.Tasks.Select(t => t.Id).ToList());

        var edited = Course();
        edited["title"] = "Новый заголовок";
        ((JsonObject)edited["days"]![1]!["tasks"]![0]!)["title"] = "Переформулированная задача";

        var result = await Import(edited, "hash-2");

        result.Outcome.Should().Be(CourseImportOutcome.Updated);
        result.Warnings.Should().BeEmpty();
        _storedCourse.Id.Should().Be(courseId, "на id курса ссылаются записи пользователей");
        _storedCourse.Title.Should().Be("Новый заголовок");
        _storedCourse.ContentHash.Should().Be("hash-2");

        // На id задач ссылается прогресс: сменились бы — слетели бы галочки.
        foreach (var day in _storedContent.Days)
            day.Tasks.Select(t => t.Id).Should().Equal(idsBefore[day.DayNumber]);
        _storedContent.Days[1].Tasks[0].Title.Should().Be("Переформулированная задача");
    }

    [Fact]
    public async Task AddedTask_GetsNewId_ExistingKeepTheirs()
    {
        await Import(Course(), "hash-1");
        var before = TaskIds(2);

        var edited = Course();
        ((JsonArray)edited["days"]![1]!["tasks"]!).Add(new JsonObject { ["title"] = "Третья задача" });
        var result = await Import(edited, "hash-2");

        var after = TaskIds(2);
        after.Should().HaveCount(3);
        after.Take(2).Should().Equal(before);
        after[2].Should().NotBe(Guid.Empty).And.NotBe(before[0]).And.NotBe(before[1]);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task RemovedTask_ProducesWarning()
    {
        await Import(Course(), "hash-1");

        var edited = Course();
        ((JsonArray)edited["days"]![1]!["tasks"]!).RemoveAt(1);
        var result = await Import(edited, "hash-2");

        result.Warnings.Should().ContainSingle().Which.Should().Contain("в дне 2 убрано задач: 1");
    }

    [Fact]
    public async Task RemovedDay_ProducesWarning()
    {
        await Import(Course(), "hash-1");

        var shorter = Course();
        shorter["durationDays"] = 2;
        ((JsonArray)shorter["days"]!).RemoveAt(2);
        var result = await Import(shorter, "hash-2");

        _storedCourse!.DurationDays.Should().Be(2);
        _storedContent!.Days.Should().HaveCount(2);
        result.Warnings.Should().ContainSingle().Which.Should().Contain("убраны дни: 3");
    }

    [Fact]
    public async Task ExplicitTaskId_WinsOverPosition()
    {
        await Import(Course(), "hash-1");
        var before = TaskIds(2);

        // Задачи дня поменяли местами и закрепили id явно: отметки пользователей
        // должны пойти за задачей, а не остаться на месте.
        var edited = Course();
        var tasks = (JsonArray)edited["days"]![1]!["tasks"]!;
        ((JsonObject)tasks[0]!)["id"] = before[1].ToString();
        ((JsonObject)tasks[1]!)["id"] = before[0].ToString();
        await Import(edited, "hash-2");

        TaskIds(2).Should().Equal(before[1], before[0]);
    }

    [Fact]
    public async Task ExplicitIdMovedToAnotherPlace_DoesNotCreateDuplicate()
    {
        await Import(Course(), "hash-1");
        var before = TaskIds(2);

        // Явный id забрал идентификатор второй задачи себе на первое место, а
        // вторая задача id не указала и по позиции унаследовала бы тот же самый.
        var edited = Course();
        ((JsonObject)edited["days"]![1]!["tasks"]![0]!)["id"] = before[1].ToString();
        await Import(edited, "hash-2");

        var after = TaskIds(2);
        after[0].Should().Be(before[1]);
        after.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task PublishedAt_IsSetOnFirstPublication_AndKept()
    {
        await Import(Course(), "hash-1");
        _storedCourse!.PublishedAt.Should().BeNull("черновик не опубликован");

        await Import(Course("Published"), "hash-2");
        var publishedAt = _storedCourse.PublishedAt;
        publishedAt.Should().NotBeNull();

        var edited = Course("Published");
        edited["title"] = "Правка после публикации";
        await Import(edited, "hash-3");

        _storedCourse.PublishedAt.Should().Be(publishedAt, "это дата первой публикации");
        _storedCourse.ContentStatus.Should().Be(CourseContentStatus.Published);
    }

    [Fact]
    public async Task BackToDraft_HidesCourseButKeepsContent()
    {
        await Import(Course("Published"), "hash-1");
        var days = _storedContent!.Days.Count;

        await Import(Course(), "hash-2");

        // Каталог показывает только Published — так курс снимают с витрины.
        _storedCourse!.ContentStatus.Should().Be(CourseContentStatus.Draft);
        _storedContent.Days.Should().HaveCount(days);
    }

    [Fact]
    public async Task FailedWrite_Propagates_SoSyncCanRetry()
    {
        _content.Setup(r => r.CreateAsync(It.IsAny<CourseContent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("mongo недоступна"));

        var act = () => Import(Course(), "hash-1");

        await act.Should().ThrowAsync<TimeoutException>();
        _storedCourse.Should().BeNull("карточка с отпечатком пишется только после дней");
    }
}
