using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Tests.Fixtures;

internal sealed class CourseBuilder
{
    private readonly Course _course = new()
    {
        Id = Guid.NewGuid(),
        Slug = "basic-anti-anemia",
        Title = "Базовый анти-анемия",
        Description = "28-дневная программа для взрослых",
        TargetAudience = TargetAudience.Adult,
        DurationDays = 28,
        IsFree = true,
        ContentStatus = CourseContentStatus.Published
    };

    public CourseBuilder ForAudience(TargetAudience audience)
    {
        _course.TargetAudience = audience;
        return this;
    }

    public CourseBuilder WithSlug(string slug)
    {
        _course.Slug = slug;
        return this;
    }

    public CourseBuilder Draft()
    {
        _course.ContentStatus = CourseContentStatus.Draft;
        return this;
    }

    public Course Build() => _course;
}
