using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Services;
using AnemiaScanApi.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AnemiaScanApi.Tests.Services;

public class CourseEntitlementServiceTests
{
    private readonly Mock<IPaymentIntentsRepository> _intentsRepo = new();

    private CourseEntitlementService NewService()
        => new(_intentsRepo.Object, NullLogger<CourseEntitlementService>.Instance);

    private static CourseEnrollment EnrollmentFor(Course course, Guid userId, Guid? paidIntentId = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        CourseId = course.Id,
        PaidIntentId = paidIntentId
    };

    private PaymentIntent GivenIntent(Guid userId, Guid courseId, PaymentStatus status)
    {
        var intent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = courseId,
            Status = status
        };
        _intentsRepo.Setup(r => r.GetByIdAsync(intent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(intent);
        return intent;
    }

    [Fact]
    public async Task FreeCourse_AnyDayIsOpen()
    {
        var course = new CourseBuilder().Build();
        var enrollment = EnrollmentFor(course, Guid.NewGuid());

        var act = async () => await NewService().EnsureDayAccessAsync(course, enrollment, 27);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task PaidCourse_DayInsidePreviewWindow_IsOpenWithoutPayment(int dayNumber)
    {
        var course = new CourseBuilder().Paid(freeDaysPreview: 3).Build();
        var enrollment = EnrollmentFor(course, Guid.NewGuid());

        var act = async () => await NewService().EnsureDayAccessAsync(course, enrollment, dayNumber);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PaidCourse_FirstDayPastPreview_WithoutPayment_Throws402()
    {
        var course = new CourseBuilder().Paid(freeDaysPreview: 3).Build();
        var enrollment = EnrollmentFor(course, Guid.NewGuid());

        var act = async () => await NewService().EnsureDayAccessAsync(course, enrollment, 4);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 402);
    }

    [Fact]
    public async Task PaidCourse_WithConfirmedPayment_IsOpen()
    {
        var userId = Guid.NewGuid();
        var course = new CourseBuilder().Paid(freeDaysPreview: 3).Build();
        var intent = GivenIntent(userId, course.Id, PaymentStatus.Confirmed);
        var enrollment = EnrollmentFor(course, userId, intent.Id);

        var act = async () => await NewService().EnsureDayAccessAsync(course, enrollment, 20);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Expired)]
    [InlineData(PaymentStatus.Failed)]
    public async Task PaidCourse_WithUnsettledPayment_Throws402(PaymentStatus status)
    {
        var userId = Guid.NewGuid();
        var course = new CourseBuilder().Paid(freeDaysPreview: 3).Build();
        var intent = GivenIntent(userId, course.Id, status);
        var enrollment = EnrollmentFor(course, userId, intent.Id);

        var act = async () => await NewService().EnsureDayAccessAsync(course, enrollment, 20);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 402);
    }

    [Fact]
    public async Task ConfirmedPaymentForAnotherCourse_DoesNotUnlockThisOne()
    {
        var userId = Guid.NewGuid();
        var course = new CourseBuilder().Paid(freeDaysPreview: 3).Build();
        var intent = GivenIntent(userId, courseId: Guid.NewGuid(), PaymentStatus.Confirmed);
        var enrollment = EnrollmentFor(course, userId, intent.Id);

        var act = async () => await NewService().EnsureDayAccessAsync(course, enrollment, 20);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 402);
    }

    [Fact]
    public async Task DanglingPaidIntentId_Throws402()
    {
        var course = new CourseBuilder().Paid(freeDaysPreview: 3).Build();
        var enrollment = EnrollmentFor(course, Guid.NewGuid(), Guid.NewGuid());

        _intentsRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentIntent?)null!);

        var act = async () => await NewService().EnsureDayAccessAsync(course, enrollment, 20);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 402);
    }
}
