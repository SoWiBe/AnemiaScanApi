using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Controllers;
using AnemiaScanApi.Filters;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Settings;
using AnemiaScanApi.Infrastructure.Utils;
using AnemiaScanApi.Infrastructure.Utils.Core;

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

using IAuthorizationService = AnemiaScanApi.Infrastructure.Services.Core.IAuthorizationService;
using IEmailSender = AnemiaScanApi.Utils.Core.IEmailSender;

namespace AnemiaScanApi.Tests.Auth;

public class CodeGeneratorTests
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static CodeGenerator NewGenerator(int length = 6) => new(
        Options.Create(new CodeGeneratorSettings { Chars = Alphabet, Length = length }));

    [Fact]
    public void Code_HasConfiguredLengthAndAlphabet()
    {
        var code = NewGenerator().GenerateAlphanumericCode();

        code.Should().HaveLength(6);
        code.Should().MatchRegex($"^[{Alphabet}]+$");
    }

    [Fact]
    public void ExplicitLength_OverridesSettings()
        => NewGenerator().GenerateAlphanumericCode(10).Should().HaveLength(10);

    [Fact]
    public void Codes_DoNotRepeat()
    {
        // 32^6 ≈ миллиард вариантов: совпадение среди 200 кодов означало бы
        // сломанный генератор, а не невезение.
        var generator = NewGenerator();
        var codes = Enumerable.Range(0, 200).Select(_ => generator.GenerateAlphanumericCode()).ToList();

        codes.Should().OnlyHaveUniqueItems();
    }
}

/// <summary>
/// Фильтр, который пускает в действие только с верным кодом из письма. Им закрыты
/// вход по коду и регистрация, поэтому важно, что без кода и с чужим кодом
/// действие не выполняется вовсе.
/// </summary>
public class ValidationCodeFilterTests
{
    private readonly Mock<IVerificationCodeStore> _store = new();
    private bool _actionExecuted;

    private async Task<IActionResult?> Run(object request)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, [],
            new Dictionary<string, object?> { ["request"] = request }, controller: new object());

        await new ValidationCodeFilter(_store.Object).OnActionExecutionAsync(context, () =>
        {
            _actionExecuted = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], controller: new object()));
        });

        return context.Result;
    }

    private void GivenCheck(VerificationCodeCheck result)
        => _store.Setup(s => s.CheckAsync("user@example.com", "ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private static BaseAuthRequest Request() => new() { Email = "user@example.com", EmailCode = "ABC123" };

    [Fact]
    public async Task ValidCode_LetsActionRun()
    {
        GivenCheck(VerificationCodeCheck.Valid);

        var result = await Run(Request());

        result.Should().BeNull();
        _actionExecuted.Should().BeTrue();
    }

    [Theory]
    [InlineData(VerificationCodeCheck.Missing, "Повторно отправьте код подтверждения на почту")]
    [InlineData(VerificationCodeCheck.Mismatch, "Указан не правильный код подтверждения")]
    public async Task BadCode_Returns400_AndActionDoesNotRun(VerificationCodeCheck check, string message)
    {
        GivenCheck(check);

        var result = await Run(Request());

        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be(message);
        _actionExecuted.Should().BeFalse();
    }

    [Fact]
    public async Task WrongRequestType_Returns400_WithoutTouchingStore()
    {
        var result = await Run(new object());

        result.Should().BeOfType<BadRequestObjectResult>();
        _actionExecuted.Should().BeFalse();
        _store.VerifyNoOtherCalls();
    }
}

/// <summary>
/// Как контроллер авторизации обращается с кодами: когда сохраняет, когда
/// сверяет, когда удаляет.
/// </summary>
public class AuthorizationControllerCodeTests
{
    private const string Email = "user@example.com";

    private readonly Mock<IAuthorizationService> _auth = new();
    private readonly Mock<IEmailSender> _email = new();
    private readonly Mock<ICodeGenerator> _generator = new();
    private readonly Mock<IVerificationCodeStore> _store = new();
    private readonly List<string> _calls = [];

    public AuthorizationControllerCodeTests()
    {
        _generator.Setup(g => g.GenerateAlphanumericCode(null)).Returns("ABC123");
        _auth.Setup(a => a.IsUserExistAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _store.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("save")).Returns(Task.CompletedTask);
        _email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("send")).Returns(Task.CompletedTask);
    }

    private AuthorizationController NewController() => new(
        NullLogger<AuthorizationController>.Instance, _auth.Object, _email.Object, _generator.Object, _store.Object);

    private void GivenCheck(string code, VerificationCodeCheck result)
        => _store.Setup(s => s.CheckAsync(Email, code, It.IsAny<CancellationToken>())).ReturnsAsync(result);

    [Fact]
    public async Task SendCode_SavesCodeBeforeSendingEmail()
    {
        var result = await NewController().SendCodeAsync(new SendCodeRequest { Email = Email });

        result.Should().BeOfType<OkResult>();
        // Сначала база, потом письмо: иначе при сбое базы человек получил бы
        // код, который не сработает.
        _calls.Should().Equal("save", "send");
        _store.Verify(s => s.SaveAsync(Email, "ABC123", It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendEmailAsync(Email, It.IsAny<string>(), It.Is<string>(body => body.Contains("ABC123")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendCode_WhenStoreFails_NoEmailIsSent()
    {
        _store.Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("mongo недоступна"));

        var act = () => NewController().SendCodeAsync(new SendCodeRequest { Email = Email });

        await act.Should().ThrowAsync<TimeoutException>();
        _calls.Should().NotContain("send");
    }

    [Fact]
    public async Task SendCode_UnknownUser_Is404_AndNothingIsStoredOrSent()
    {
        _auth.Setup(a => a.IsUserExistAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await NewController().SendCodeAsync(new SendCodeRequest { Email = Email });

        result.Should().BeOfType<NotFoundObjectResult>();
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyRegistration_SavesAndSendsCode()
    {
        var request = new VerificationRegistrationRequest { Email = Email, Password = "password1", ConfirmPassword = "password1" };

        var result = await NewController().VerifyRegistrationAsync(request);

        result.Should().BeOfType<OkResult>();
        _calls.Should().Equal("save", "send");
    }

    [Theory]
    [InlineData(VerificationCodeCheck.Valid, typeof(OkResult))]
    [InlineData(VerificationCodeCheck.Mismatch, typeof(BadRequestObjectResult))]
    [InlineData(VerificationCodeCheck.Missing, typeof(BadRequestObjectResult))]
    public async Task VerifyCode_ReflectsStoreAnswer_AndDoesNotConsumeCode(VerificationCodeCheck check, Type expected)
    {
        GivenCheck("ABC123", check);

        var result = await NewController().VerifyCodeAsync(new VerificationCodeRequest { Email = Email, Code = "ABC123" });

        result.Should().BeOfType(expected);
        // Проверка кода — отдельный шаг перед сменой пароля; тот же код должен
        // сработать и на следующем шаге.
        _store.Verify(s => s.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePassword_ValidCode_ChangesPassword_ThenRemovesCode()
    {
        GivenCheck("ABC123", VerificationCodeCheck.Valid);
        var request = new UpdatePasswordRequest { Email = Email, Code = "ABC123", NewPassword = "new-password", ConfirmPassword = "new-password" };

        var result = await NewController().UpdatePasswordAsync(request);

        result.Should().BeOfType<OkResult>();
        _auth.Verify(a => a.UpdatePasswordAsync(Email, "new-password", It.IsAny<CancellationToken>()), Times.Once);
        _store.Verify(s => s.RemoveAsync(Email, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(VerificationCodeCheck.Mismatch)]
    [InlineData(VerificationCodeCheck.Missing)]
    public async Task UpdatePassword_BadCode_ChangesNothing(VerificationCodeCheck check)
    {
        GivenCheck("WRONG1", check);
        var request = new UpdatePasswordRequest { Email = Email, Code = "WRONG1", NewPassword = "new-password", ConfirmPassword = "new-password" };

        var result = await NewController().UpdatePasswordAsync(request);

        result.Should().BeOfType<BadRequestObjectResult>();
        _auth.Verify(a => a.UpdatePasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(s => s.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
