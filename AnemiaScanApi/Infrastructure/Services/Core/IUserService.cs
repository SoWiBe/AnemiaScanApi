using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Requests.Profile;

namespace AnemiaScanApi.Infrastructure.Services.Core;

public interface IProfileService
{
    public Task<SasUser> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
    public Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    public Task WriteAnalysisAsync(Guid userId, AnemiaScan scan, CancellationToken cancellationToken);
    public Task DeleteProfileAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Записывает принятие действующей версии политики (P0 №13).
    /// </summary>
    /// <param name="policyVersion">
    /// Версия, показанная пользователю. Null — считаем, что показали действующую;
    /// иная версия — 400, мы не записываем согласие под текст, которого он не видел.
    /// </param>
    public Task<UserConsent> AcceptConsentAsync(Guid userId, string? policyVersion, CancellationToken cancellationToken);
}