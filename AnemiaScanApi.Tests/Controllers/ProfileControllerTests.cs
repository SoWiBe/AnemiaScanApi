using System.Security.Claims;
using System.Text.Json;

using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Common.Requests.Profile;
using AnemiaScanApi.Common.Responses;
using AnemiaScanApi.Controllers;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Tests.Fixtures;

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AnemiaScanApi.Tests.Controllers;

/// <summary>
/// Контракт <c>GET /profile/info</c>. Главное здесь — эндпоинт отдаёт выделенный DTO,
/// а не документ пользователя целиком: до этого в ответ уходили hash_password и
/// refresh_token (P0 №1 в docs/plans/MVP_PLAN.md).
/// </summary>
public class ProfileControllerTests
{
    private const string PasswordHash = "$2a$11$do-not-leak-this-hash";
    private const string RefreshToken = "do-not-leak-this-refresh-token";

    private readonly Mock<IProfileService> _profileService = new();

    private ProfileController NewController(Guid userId)
    {
        var controller = new ProfileController(NullLogger<ProfileController>.Instance, _profileService.Object);

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return controller;
    }

    private static SasUser UserWithSecrets(Guid id, params AnemiaScan[] scans)
    {
        var user = new SasUser
        {
            Id = id,
            Email = "user@example.com",
            FullName = "Иван Иванов",
            BirthDate = new DateTime(1990, 5, 17, 0, 0, 0, DateTimeKind.Utc),
            Sex = Sex.Male,
            Age = 36,
            HashPassword = PasswordHash,
            RefreshToken = RefreshToken,
            RefreshTokenExpires = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        user.AnemiaScans.AddRange(scans);
        return user;
    }

    private void GivenProfile(Guid userId, SasUser user)
        => _profileService
            .Setup(s => s.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

    // ---------- GET /profile/info ----------

    [Fact]
    public async Task GetProfile_ReturnsOkWithProfileDto()
    {
        var userId = Guid.NewGuid();
        GivenProfile(userId, UserWithSecrets(userId));

        var result = await NewController(userId).GetProfileAsync();

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<GetProfileResponse>();
    }

    [Fact]
    public async Task GetProfile_MapsEveryProfileField()
    {
        var userId = Guid.NewGuid();
        var scan = SasUserBuilder.AnemicScan(new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));
        var user = UserWithSecrets(userId, scan);
        GivenProfile(userId, user);

        var result = await NewController(userId).GetProfileAsync();
        var response = (GetProfileResponse)((OkObjectResult)result).Value!;

        response.Email.Should().Be(user.Email);
        response.FullName.Should().Be(user.FullName);
        response.Birthday.Should().Be(user.BirthDate);
        response.Sex.Should().Be(user.Sex);
        response.Age.Should().Be(user.Age);
        response.AnemiaScans.Should().ContainSingle()
            .Which.AnalysisId.Should().Be(scan.AnalysisId);
    }

    /// <summary>
    /// Регрессия на саму утечку: сериализованный ответ не должен содержать ни хеша
    /// пароля, ни refresh-токена — как бы ни менялся маппинг дальше.
    /// </summary>
    [Fact]
    public async Task GetProfile_ResponseNeverCarriesCredentials()
    {
        var userId = Guid.NewGuid();
        GivenProfile(userId, UserWithSecrets(userId, SasUserBuilder.HealthyScan(DateTime.UtcNow)));

        var result = await NewController(userId).GetProfileAsync();
        var json = JsonSerializer.Serialize(((OkObjectResult)result).Value);

        json.Should().NotContain(PasswordHash);
        json.Should().NotContain(RefreshToken);
    }

    /// <summary>
    /// Второй слой той же защиты: у DTO не должно появиться полей с учётными данными,
    /// даже пустых, — тест упадёт при попытке снова вернуть в ответ <see cref="SasUser"/>.
    /// </summary>
    [Fact]
    public void ProfileResponse_ExposesNoCredentialProperties()
    {
        var propertyNames = typeof(GetProfileResponse)
            .GetProperties()
            .Select(p => p.Name.ToLowerInvariant())
            .ToList();

        propertyNames.Should().NotContain(n => n.Contains("password") || n.Contains("hash") || n.Contains("token"));
        propertyNames.Should().NotContain(n => n.Contains("profile"), "профиль должен быть разложен по полям, а не вложен целиком");
    }

    [Fact]
    public async Task GetProfile_ReadsUserIdFromNameIdentifierClaim()
    {
        var userId = Guid.NewGuid();
        GivenProfile(userId, UserWithSecrets(userId));

        await NewController(userId).GetProfileAsync();

        _profileService.Verify(s => s.GetProfileAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _profileService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetProfile_PassesCancellationTokenThrough()
    {
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        GivenProfile(userId, UserWithSecrets(userId));

        await NewController(userId).GetProfileAsync(cts.Token);

        _profileService.Verify(s => s.GetProfileAsync(userId, cts.Token), Times.Once);
    }

    /// <summary>
    /// Контроллер не ловит исключения сам — 404 доезжает до <c>SASMiddleware</c>.
    /// </summary>
    [Fact]
    public async Task GetProfile_UnknownUser_Propagates404()
    {
        var userId = Guid.NewGuid();
        _profileService
            .Setup(s => s.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SASException(ExceptionMessage.ProfileNotFound, 404));

        var act = async () => await NewController(userId).GetProfileAsync();

        await act.Should().ThrowAsync<SASException>()
            .Where(e => e.StatusCode == 404);
    }

    // ---------- PATCH /profile ----------

    [Fact]
    public async Task UpdateProfile_PassesUserIdAndRequestToService()
    {
        var userId = Guid.NewGuid();
        var request = new UpdateProfileRequest { FullName = "Новое Имя", Age = 41 };

        var result = await NewController(userId).UpdateProfileAsync(request);

        _profileService.Verify(s => s.UpdateProfileAsync(userId, request, It.IsAny<CancellationToken>()), Times.Once);
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<UpdateProfileResponse>()
            .Which.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateProfile_ServiceFailure_Propagates()
    {
        var userId = Guid.NewGuid();
        _profileService
            .Setup(s => s.UpdateProfileAsync(userId, It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SASException("Пароли не совпадают", 400));

        var act = async () => await NewController(userId).UpdateProfileAsync(new UpdateProfileRequest());

        await act.Should().ThrowAsync<SASException>()
            .Where(e => e.StatusCode == 400);
    }
}
