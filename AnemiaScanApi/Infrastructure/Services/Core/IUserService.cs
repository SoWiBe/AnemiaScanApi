using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Requests.Profile;

namespace AnemiaScanApi.Infrastructure.Services.Core;

public interface IProfileService
{
    public Task<SasUser> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
    public Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    public Task WriteAnalysisAsync(Guid userId, AnemiaScan scan, CancellationToken cancellationToken);
    public Task DeleteProfileAsync(Guid userId, CancellationToken cancellationToken);
}