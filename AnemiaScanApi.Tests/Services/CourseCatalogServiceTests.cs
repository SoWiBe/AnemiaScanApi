using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Services;
using AnemiaScanApi.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AnemiaScanApi.Tests.Services;

public class CourseCatalogServiceTests
{
    private readonly Mock<ICoursesRepository> _coursesRepo = new();
    private readonly Mock<ICourseContentRepository> _contentRepo = new();
    private readonly Mock<IUsersRepository> _usersRepo = new();

    private CourseCatalogService NewService() => new(
        _coursesRepo.Object,
        _contentRepo.Object,
        _usersRepo.Object,
        NullLogger<CourseCatalogService>.Instance);

    [Fact]
    public async Task GetCatalog_ReturnsMappedListItems()
    {
        var course = new CourseBuilder().Build();
        _coursesRepo.Setup(r => r.GetPublishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { course });

        var result = (await NewService().GetCatalogAsync()).ToList();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(course.Id);
        result[0].Slug.Should().Be(course.Slug);
    }

    [Fact]
    public async Task GetRecommended_NoUser_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        _usersRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SasUser)null!);

        var result = await NewService().GetRecommendedAsync(userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRecommended_LastScanHealthy_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var user = new SasUserBuilder()
            .WithId(userId)
            .WithScan(SasUserBuilder.HealthyScan(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)))
            .Build();
        _usersRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await NewService().GetRecommendedAsync(userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRecommended_LastScanAnemic_ReturnsAdultCourse()
    {
        var userId = Guid.NewGuid();
        var user = new SasUserBuilder()
            .WithId(userId)
            .WithScans(
                SasUserBuilder.HealthyScan(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                SasUserBuilder.AnemicScan(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)))
            .Build();

        var adultCourse = new CourseBuilder().ForAudience(TargetAudience.Adult).Build();
        var pregnantCourse = new CourseBuilder().ForAudience(TargetAudience.Pregnant).WithSlug("pregnancy").Build();

        _usersRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _coursesRepo.Setup(r => r.GetPublishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pregnantCourse, adultCourse });

        var result = await NewService().GetRecommendedAsync(userId);

        result.Should().NotBeNull();
        result!.Course.Id.Should().Be(adultCourse.Id);
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetRecommended_UsesLatestScanNotFirst()
    {
        var userId = Guid.NewGuid();
        var user = new SasUserBuilder()
            .WithId(userId)
            .WithScans(
                SasUserBuilder.AnemicScan(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                SasUserBuilder.HealthyScan(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)))
            .Build();
        _usersRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await NewService().GetRecommendedAsync(userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDetails_UnknownSlug_ReturnsNull()
    {
        _coursesRepo.Setup(r => r.GetBySlugAsync("nope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Course?)null);

        var result = await NewService().GetDetailsAsync("nope");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDetails_ReturnsPreviewsWithoutTheoryBodies()
    {
        var course = new CourseBuilder().Build();
        var content = new CourseContent
        {
            CourseId = course.Id,
            Days =
            {
                new CourseDay { DayNumber = 1, Theory = "long theory 1", Tasks = { new CourseTask { Title = "t" } } },
                new CourseDay { DayNumber = 2, Theory = "long theory 2", Tasks = { new CourseTask { Title = "t" }, new CourseTask { Title = "t2" } }, IsRescanCheckpoint = true }
            }
        };

        _coursesRepo.Setup(r => r.GetBySlugAsync(course.Slug, It.IsAny<CancellationToken>())).ReturnsAsync(course);
        _contentRepo.Setup(r => r.GetByCourseIdAsync(course.Id, It.IsAny<CancellationToken>())).ReturnsAsync(content);

        var result = await NewService().GetDetailsAsync(course.Slug);

        result.Should().NotBeNull();
        result!.DayPreviews.Should().HaveCount(2);
        result.DayPreviews[1].TaskCount.Should().Be(2);
        result.DayPreviews[1].IsRescanCheckpoint.Should().BeTrue();
    }
}
