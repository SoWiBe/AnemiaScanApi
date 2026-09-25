using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Services;
using AnemiaScanApi.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AnemiaScanApi.Tests.Services;

public class CourseEnrollmentServiceTests
{
    private readonly Mock<ICoursesRepository> _coursesRepo = new();
    private readonly Mock<ICourseContentRepository> _contentRepo = new();
    private readonly Mock<ICourseEnrollmentsRepository> _enrollmentsRepo = new();
    private readonly Mock<IAnemiaScansRepository> _scansRepo = new();

    private CourseEnrollmentService NewService() => new(
        _coursesRepo.Object,
        _contentRepo.Object,
        _enrollmentsRepo.Object,
        _scansRepo.Object,
        new StreakService(NullLogger<StreakService>.Instance),
        NullLogger<CourseEnrollmentService>.Instance);

    [Fact]
    public async Task Enroll_UnknownSlug_Throws404()
    {
        _coursesRepo.Setup(r => r.GetBySlugAsync("nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Course?)null);

        var act = async () => await NewService().EnrollAsync(Guid.NewGuid(), "nope");

        await act.Should().ThrowAsync<SASException>()
            .Where(e => e.StatusCode == 404);
    }

    [Fact]
    public async Task Enroll_AlreadyEnrolled_Throws409()
    {
        var userId = Guid.NewGuid();
        var course = new CourseBuilder().Build();
        _coursesRepo.Setup(r => r.GetBySlugAsync(course.Slug, It.IsAny<CancellationToken>())).ReturnsAsync(course);
        _enrollmentsRepo.Setup(r => r.GetByUserAndCourseAsync(userId, course.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CourseEnrollment { UserId = userId, CourseId = course.Id });

        var act = async () => await NewService().EnrollAsync(userId, course.Slug);

        await act.Should().ThrowAsync<SASException>()
            .Where(e => e.StatusCode == 409);
    }

    [Fact]
    public async Task Enroll_Success_CreatesActiveEnrollment()
    {
        var userId = Guid.NewGuid();
        var course = new CourseBuilder().Build();
        _coursesRepo.Setup(r => r.GetBySlugAsync(course.Slug, It.IsAny<CancellationToken>())).ReturnsAsync(course);
        _enrollmentsRepo.Setup(r => r.GetByUserAndCourseAsync(userId, course.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CourseEnrollment?)null);

        CourseEnrollment? persisted = null;
        _enrollmentsRepo.Setup(r => r.CreateAsync(It.IsAny<CourseEnrollment>(), It.IsAny<CancellationToken>()))
            .Callback<CourseEnrollment, CancellationToken>((e, _) => persisted = e)
            .ReturnsAsync((CourseEnrollment e, CancellationToken _) => e);

        var response = await NewService().EnrollAsync(userId, course.Slug);

        response.CourseId.Should().Be(course.Id);
        response.CourseSlug.Should().Be(course.Slug);
        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(userId);
        persisted.Status.Should().Be(EnrollmentStatus.Active);
    }

    [Fact]
    public async Task MarkTaskDone_FutureDay_Throws409()
    {
        var userId = Guid.NewGuid();
        var course = new CourseBuilder().Build();
        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = course.Id,
            EnrolledAt = DateTime.UtcNow
        };
        var day = new CourseDay { DayNumber = 5, Theory = "t", Tasks = { new CourseTask { Title = "x" } } };
        var content = new CourseContent { CourseId = course.Id, Days = { day } };

        _enrollmentsRepo.Setup(r => r.GetByIdAsync(enrollment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(enrollment);
        _coursesRepo.Setup(r => r.GetByIdAsync(course.Id, It.IsAny<CancellationToken>())).ReturnsAsync(course);
        _contentRepo.Setup(r => r.GetByCourseIdAsync(course.Id, It.IsAny<CancellationToken>())).ReturnsAsync(content);

        var act = async () => await NewService().MarkTaskDoneAsync(userId, enrollment.Id, 5, day.Tasks[0].Id);

        await act.Should().ThrowAsync<SASException>()
            .Where(e => e.StatusCode == 409);
    }

    [Fact]
    public async Task MarkTaskDone_OtherUsersEnrollment_Throws404()
    {
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var enrollment = new CourseEnrollment { Id = Guid.NewGuid(), UserId = owner };

        _enrollmentsRepo.Setup(r => r.GetByIdAsync(enrollment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(enrollment);

        var act = async () => await NewService().MarkTaskDoneAsync(stranger, enrollment.Id, 1, Guid.NewGuid());

        await act.Should().ThrowAsync<SASException>()
            .Where(e => e.StatusCode == 404);
    }

    [Fact]
    public async Task MarkTaskDone_AllTasksInPlainDay_SetsCompletedAt()
    {
        var userId = Guid.NewGuid();
        var course = new CourseBuilder().Build();
        course.DurationDays = 1;
        var task1 = new CourseTask { Id = Guid.NewGuid(), Title = "a" };
        var task2 = new CourseTask { Id = Guid.NewGuid(), Title = "b" };
        var day = new CourseDay { DayNumber = 1, Theory = "t", Tasks = { task1, task2 } };
        var content = new CourseContent { CourseId = course.Id, Days = { day } };
        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = course.Id,
            EnrolledAt = DateTime.UtcNow
        };

        _enrollmentsRepo.Setup(r => r.GetByIdAsync(enrollment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(enrollment);
        _coursesRepo.Setup(r => r.GetByIdAsync(course.Id, It.IsAny<CancellationToken>())).ReturnsAsync(course);
        _contentRepo.Setup(r => r.GetByCourseIdAsync(course.Id, It.IsAny<CancellationToken>())).ReturnsAsync(content);
        _enrollmentsRepo.Setup(r => r.UpdateAsync(enrollment.Id, It.IsAny<CourseEnrollment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, CourseEnrollment e, CancellationToken _) => e);

        var service = NewService();
        await service.MarkTaskDoneAsync(userId, enrollment.Id, 1, task1.Id);
        await service.MarkTaskDoneAsync(userId, enrollment.Id, 1, task2.Id);

        var completion = enrollment.Days.Single();
        completion.CompletedTaskIds.Should().BeEquivalentTo(new[] { task1.Id, task2.Id });
        completion.CompletedAt.Should().NotBeNull();
        enrollment.Status.Should().Be(EnrollmentStatus.Completed);
    }

    [Fact]
    public async Task MarkTaskDone_CheckpointDayWithoutScan_DoesNotComplete()
    {
        var userId = Guid.NewGuid();
        var course = new CourseBuilder().Build();
        course.DurationDays = 1;
        var task = new CourseTask { Id = Guid.NewGuid(), Title = "x" };
        var day = new CourseDay { DayNumber = 1, Theory = "t", Tasks = { task }, IsRescanCheckpoint = true };
        var content = new CourseContent { CourseId = course.Id, Days = { day } };
        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = course.Id,
            EnrolledAt = DateTime.UtcNow
        };

        _enrollmentsRepo.Setup(r => r.GetByIdAsync(enrollment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(enrollment);
        _coursesRepo.Setup(r => r.GetByIdAsync(course.Id, It.IsAny<CancellationToken>())).ReturnsAsync(course);
        _contentRepo.Setup(r => r.GetByCourseIdAsync(course.Id, It.IsAny<CancellationToken>())).ReturnsAsync(content);
        _enrollmentsRepo.Setup(r => r.UpdateAsync(enrollment.Id, It.IsAny<CourseEnrollment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, CourseEnrollment e, CancellationToken _) => e);

        await NewService().MarkTaskDoneAsync(userId, enrollment.Id, 1, task.Id);

        var completion = enrollment.Days.Single();
        completion.CompletedTaskIds.Should().ContainSingle();
        completion.CompletedAt.Should().BeNull();
        enrollment.Status.Should().Be(EnrollmentStatus.Active);
    }

    [Fact]
    public async Task AttachCheckpointScan_OnNonCheckpointDay_Throws400()
    {
        var userId = Guid.NewGuid();
        var course = new CourseBuilder().Build();
        var day = new CourseDay { DayNumber = 1, Theory = "t", Tasks = { new CourseTask { Title = "x" } } };
        var content = new CourseContent { CourseId = course.Id, Days = { day } };
        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = course.Id,
            EnrolledAt = DateTime.UtcNow
        };

        _enrollmentsRepo.Setup(r => r.GetByIdAsync(enrollment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(enrollment);
        _coursesRepo.Setup(r => r.GetByIdAsync(course.Id, It.IsAny<CancellationToken>())).ReturnsAsync(course);
        _contentRepo.Setup(r => r.GetByCourseIdAsync(course.Id, It.IsAny<CancellationToken>())).ReturnsAsync(content);

        var act = async () => await NewService().AttachCheckpointScanAsync(userId, enrollment.Id, 1, Guid.NewGuid());

        await act.Should().ThrowAsync<SASException>()
            .Where(e => e.StatusCode == 400);
    }
}
