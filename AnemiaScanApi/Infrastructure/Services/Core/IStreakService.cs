using AnemiaScanApi.Common;

namespace AnemiaScanApi.Infrastructure.Services.Core;

/// <summary>
/// Streak calculation for a course enrollment. Pure logic — does not persist.
/// </summary>
public interface IStreakService
{
    /// <summary>
    /// Records that the user did something meaningful on <paramref name="utcNow"/>'s calendar day
    /// (completed a task or attached a checkpoint scan) and updates streak fields in place.
    /// Idempotent within the same UTC calendar day.
    /// </summary>
    void ApplyDayActivity(CourseEnrollment enrollment, DateTime utcNow);
}
