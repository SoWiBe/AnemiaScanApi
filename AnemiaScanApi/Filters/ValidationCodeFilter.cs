using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Infrastructure.Settings;
using AnemiaScanApi.Settings;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Filters;

[AttributeUsage(AttributeTargets.Method)]
public class ValidationCodeFilter(
    IMemoryCache memoryCache,
    IOptions<CodeGeneratorSettings> codeGeneratorSettings) : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionArguments["request"] is not BaseAuthRequest authRequest)
        {
            context.Result = new BadRequestObjectResult("Неверный запрос");
            return;
        }
        
        var cachedCode = memoryCache.Get<string>($"{codeGeneratorSettings.Value.CacheKey}:{authRequest.Email}");

        if (string.IsNullOrWhiteSpace(cachedCode))
        {
            context.Result = new BadRequestObjectResult("Повторно отправьте код подтверждения на почту");
            return;
        }

        if (!cachedCode.Equals(authRequest.EmailCode))
        {
            context.Result = new BadRequestObjectResult("Указан не правильный код подтверждения");
            return;
        }

        await next();
    }
}