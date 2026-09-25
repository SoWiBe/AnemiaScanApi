using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services;
using AnemiaScanApi.Settings;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AnemiaScanApi.Tests.Services;

/// <summary>
/// Согласие на обработку персданных пишется на бэк при регистрации
/// (P0 №13 в docs/plans/MVP_PLAN.md). Галка, которую сервер не записал,
/// юридически не существует — поэтому регистрация без неё не проходит.
/// </summary>
public class AuthorizationServiceConsentTests
{
    private const string CurrentPolicyVersion = "1.0";

    private readonly Mock<IUsersRepository> _users = new();

    /// <summary>Пользователь, дошедший до вставки в репозиторий.</summary>
    private SasUser? _createdUser;

    private AuthorizationService NewService()
    {
        _users.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _users.Setup(r => r.CreateUserAsync(It.IsAny<SasUser>(), It.IsAny<CancellationToken>()))
            .Callback((SasUser user, CancellationToken _) => _createdUser = user)
            .ReturnsAsync((SasUser user, CancellationToken _) => user);
        _users.Setup(r => r.UpdateUserAsync(It.IsAny<SasUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SasUser user, CancellationToken _) => user);

        var jwt = Options.Create(new JwtSettings
        {
            Secret = "test-secret-key-that-is-long-enough-for-hmac-sha256",
            Issuer = "test",
            Audience = "test",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 30
        });
        var legal = Options.Create(new LegalSettings { PolicyVersion = CurrentPolicyVersion });

        return new AuthorizationService(NullLogger<AuthorizationService>.Instance, _users.Object, jwt, legal);
    }

    private static SignUpRequest NewRequest(bool consentAccepted = true, string? policyVersion = null) => new()
    {
        Email = "user@example.com",
        EmailCode = "ABC123",
        FullName = "Иван Иванов",
        BirthDate = new DateTime(1990, 5, 17, 0, 0, 0, DateTimeKind.Utc),
        Password = "password123",
        ConfirmPassword = "password123",
        ConsentAccepted = consentAccepted,
        PolicyVersion = policyVersion
    };

    [Fact]
    public async Task RegisterAsync_StoresConsentWithCurrentPolicyVersion()
    {
        await NewService().RegisterAsync(NewRequest(policyVersion: CurrentPolicyVersion));

        _createdUser.Should().NotBeNull();
        _createdUser!.Consent.Should().NotBeNull();
        _createdUser.Consent!.Accepted.Should().BeTrue();
        _createdUser.Consent.PolicyVersion.Should().Be(CurrentPolicyVersion);
        _createdUser.Consent.AcceptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RegisterAsync_AssumesCurrentVersionWhenClientOmitsIt()
    {
        await NewService().RegisterAsync(NewRequest());

        _createdUser!.Consent!.PolicyVersion.Should().Be(CurrentPolicyVersion);
    }

    [Fact]
    public async Task RegisterAsync_RejectsWhenConsentNotAccepted()
    {
        var act = () => NewService().RegisterAsync(NewRequest(consentAccepted: false));

        (await act.Should().ThrowAsync<SASException>())
            .Which.Message.Should().Be(ExceptionMessage.ConsentRequired);

        _users.Verify(r => r.CreateUserAsync(It.IsAny<SasUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_RejectsConsentForStalePolicyVersion()
    {
        // Клиент показал старый текст политики — записать такое согласие нельзя.
        var act = () => NewService().RegisterAsync(NewRequest(policyVersion: "0.9"));

        (await act.Should().ThrowAsync<SASException>())
            .Which.Message.Should().Be(ExceptionMessage.ConsentVersionMismatch);

        _users.Verify(r => r.CreateUserAsync(It.IsAny<SasUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
