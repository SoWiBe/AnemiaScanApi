using AnemiaScanApi.Common;
using AnemiaScanApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnemiaScanApi.Tests.Services;

public class StreakServiceTests
{
    private static readonly DateTime Day1 = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    private static StreakService NewService() => new(NullLogger<StreakService>.Instance);

    [Fact]
    public void FirstEverActivity_StartsStreakAtOne()
    {
        var enrollment = new CourseEnrollment();
        var service = NewService();

        service.ApplyDayActivity(enrollment, Day1);

        enrollment.CurrentStreak.Should().Be(1);
        enrollment.LongestStreak.Should().Be(1);
        enrollment.LastActivityDate.Should().Be(Day1.Date);
    }

    [Fact]
    public void ConsecutiveDays_IncrementStreak()
    {
        var enrollment = new CourseEnrollment();
        var service = NewService();

        service.ApplyDayActivity(enrollment, Day1);
        service.ApplyDayActivity(enrollment, Day1.AddDays(1));
        service.ApplyDayActivity(enrollment, Day1.AddDays(2));

        enrollment.CurrentStreak.Should().Be(3);
        enrollment.LongestStreak.Should().Be(3);
    }

    [Fact]
    public void SameDayCalledTwice_IsIdempotent()
    {
        var enrollment = new CourseEnrollment();
        var service = NewService();

        service.ApplyDayActivity(enrollment, Day1);
        service.ApplyDayActivity(enrollment, Day1.AddHours(5));

        enrollment.CurrentStreak.Should().Be(1);
        enrollment.LastActivityDate.Should().Be(Day1.Date);
    }

    [Fact]
    public void GapOfMoreThanOneDay_ResetsStreakButKeepsLongest()
    {
        var enrollment = new CourseEnrollment();
        var service = NewService();

        service.ApplyDayActivity(enrollment, Day1);
        service.ApplyDayActivity(enrollment, Day1.AddDays(1));
        service.ApplyDayActivity(enrollment, Day1.AddDays(2));

        service.ApplyDayActivity(enrollment, Day1.AddDays(5));

        enrollment.CurrentStreak.Should().Be(1);
        enrollment.LongestStreak.Should().Be(3);
    }

    [Fact]
    public void NewLongerStreak_UpdatesLongest()
    {
        var enrollment = new CourseEnrollment();
        var service = NewService();

        service.ApplyDayActivity(enrollment, Day1);
        service.ApplyDayActivity(enrollment, Day1.AddDays(1));

        service.ApplyDayActivity(enrollment, Day1.AddDays(10));
        service.ApplyDayActivity(enrollment, Day1.AddDays(11));
        service.ApplyDayActivity(enrollment, Day1.AddDays(12));

        enrollment.CurrentStreak.Should().Be(3);
        enrollment.LongestStreak.Should().Be(3);
    }
}
