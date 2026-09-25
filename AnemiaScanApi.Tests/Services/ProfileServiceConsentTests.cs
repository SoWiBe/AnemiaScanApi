using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services;
using AnemiaScanApi.Settings;
using AnemiaScanApi.Tests.Fixtures;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AnemiaScanApi.Tests.Services;

/// <summary>
/// Переподписание политики уже зарегистрированным пользователем
/// (<c>POST /profile/consent</c>, P0 №13).
/// </summary>
public class ProfileServiceConsentTests
{
    private const string CurrentPolicyVersion = "2.0";

    private readonly Mock<IUsersRepository> _users = new();

    private ProfileService NewService() => new(
        NullLogger<ProfileService>.Instance,
        _users.Object,
        Options.Create(new LegalSettings { PolicyVersion = CurrentPolicyVersion }));

    private SasUser GivenUser(Guid userId)
    {
        var user = new SasUserBuilder().WithId(userId).Build();
        _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.UpdateAsync(userId, It.IsAny<SasUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, SasUser updated, CancellationToken _) => updated);
        return user;
    }

    [Fact]
    public async Task AcceptConsentAsync_WritesConsentAndPersistsUser()
    {
        var userId = Guid.NewGuid();
        var user = GivenUser(userId);

        var consent = await NewService().AcceptConsentAsync(userId, CurrentPolicyVersion, CancellationToken.None);

        consent.Accepted.Should().BeTrue();
        consent.PolicyVersion.Should().Be(CurrentPolicyVersion);
        consent.AcceptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        user.Consent.Should().BeSameAs(consent);
        _users.Verify(r => r.UpdateAsync(userId, user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptConsentAsync_RejectsStalePolicyVersion()
    {
        var userId = Guid.NewGuid();
        GivenUser(userId);

        var act = () => NewService().AcceptConsentAsync(userId, "1.0", CancellationToken.None);

        (await act.Should().ThrowAsync<SASException>())
            .Which.Message.Should().Be(ExceptionMessage.ConsentVersionMismatch);

        _users.Verify(r => r.UpdateAsync(It.IsAny<Guid>(), It.IsAny<SasUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptConsentAsync_ThrowsNotFoundForUnknownUser()
    {
        var userId = Guid.NewGuid();
        _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((SasUser)null!);

        var act = () => NewService().AcceptConsentAsync(userId, null, CancellationToken.None);

        (await act.Should().ThrowAsync<SASException>())
            .Which.StatusCode.Should().Be(404);
    }
}
