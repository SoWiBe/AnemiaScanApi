using AnemiaScanApi.Common;
using AnemiaScanApi.Infrastructure.Services.Core;

namespace AnemiaScanApi.Services;

public class StreakService(ILogger<StreakService> logger)
    : BaseService<StreakService>(logger), IStreakService
{
    public void ApplyDayActivity(CourseEnrollment enrollment, DateTime utcNow)
    {
        var today = utcNow.Date;
        var last = enrollment.LastActivityDate?.Date;

        if (last == today)
        {
            return;
        }

        if (last is null)
        {
            enrollment.CurrentStreak = 1;
        }
        else if (last.Value.AddDays(1) == today)
        {
            enrollment.CurrentStreak += 1;
        }
        else
        {
            enrollment.CurrentStreak = 1;
        }

        if (enrollment.CurrentStreak > enrollment.LongestStreak)
        {
            enrollment.LongestStreak = enrollment.CurrentStreak;
        }

        enrollment.LastActivityDate = today;
    }
}
