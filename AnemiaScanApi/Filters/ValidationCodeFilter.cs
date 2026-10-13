using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Infrastructure.Repositories;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AnemiaScanApi.Filters;

[AttributeUsage(AttributeTargets.Method)]
public class ValidationCodeFilter(IVerificationCodeStore codeStore) : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionArguments["request"] is not BaseAuthRequest authRequest)
        {
            context.Result = new BadRequestObjectResult("Неверный запрос");
            return;
        }

        var check = await codeStore.CheckAsync(authRequest.Email!, authRequest.EmailCode,
            context.HttpContext.RequestAborted);

        if (check == VerificationCodeCheck.Missing)
        {
            context.Result = new BadRequestObjectResult("Повторно отправьте код подтверждения на почту");
            return;
        }

        if (check == VerificationCodeCheck.Mismatch)
        {
            context.Result = new BadRequestObjectResult("Указан не правильный код подтверждения");
            return;
        }

        await next();
    }
}
