using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;

namespace AnemiaScanApi.Services;

public class CourseEntitlementService(
    IPaymentIntentsRepository intentsRepository,
    ILogger<CourseEntitlementService> logger)
    : BaseService<CourseEntitlementService>(logger), ICourseEntitlementService
{
    public async Task EnsureDayAccessAsync(
        Course course,
        CourseEnrollment enrollment,
        int dayNumber,
        CancellationToken cancellationToken = default)
    {
        if (course.IsFree || dayNumber <= course.FreeDaysPreview)
        {
            return;
        }

        if (await HasPaidAccessAsync(enrollment, cancellationToken))
        {
            return;
        }

        Logger.LogInformation(
            "Payment gate blocked user {UserId} on day {DayNumber} of course {CourseId} (enrollment {EnrollmentId})",
            enrollment.UserId, dayNumber, course.Id, enrollment.Id);

        throw new SASException(ExceptionMessage.PaymentRequired, 402);
    }

    public async Task<bool> HasPaidAccessAsync(CourseEnrollment enrollment, CancellationToken cancellationToken = default)
    {
        if (enrollment.PaidIntentId is null) return false;

        var intent = await intentsRepository.GetByIdAsync(enrollment.PaidIntentId.Value, cancellationToken);

        // Guard the course id too: a confirmed payment for one course must never unlock another.
        return intent is not null
               && intent.Status == PaymentStatus.Confirmed
               && intent.CourseId == enrollment.CourseId
               && intent.UserId == enrollment.UserId;
    }
}
