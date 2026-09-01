using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Common.Requests.Payments;

/// <summary>
/// Request to start paying for a course.
/// </summary>
/// <param name="CourseSlug">Slug of the course to buy.</param>
/// <param name="Provider">Rail to pay on. Only <see cref="PaymentProviderType.Solana"/> is wired up.</param>
public record CreateCourseIntentRequest(string CourseSlug, PaymentProviderType Provider);
