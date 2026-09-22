using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Common.Responses;

public record GetProfileResponse(
    string Email,
    string FullName,
    DateTime? Birthday,
    Sex? Sex,
    int? Age,
    IReadOnlyList<AnemiaScan> AnemiaScans);