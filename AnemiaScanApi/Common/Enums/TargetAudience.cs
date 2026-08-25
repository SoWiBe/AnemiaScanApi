namespace AnemiaScanApi.Common.Enums;

/// <summary>
/// Intended audience for a course.
/// </summary>
public enum TargetAudience
{
    /// <summary>
    /// Adults with mild-to-moderate iron deficiency anemia.
    /// </summary>
    Adult = 0,

    /// <summary>
    /// Pregnant women.
    /// </summary>
    Pregnant = 1,

    /// <summary>
    /// Parents of young children.
    /// </summary>
    Child = 2,

    /// <summary>
    /// Postpartum period.
    /// </summary>
    Postpartum = 3
}
