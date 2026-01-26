using AnemiaScanApi.Models.Requests;
using AnemiaScanApi.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AnemiaScanApi.Attributes;

/// <summary>
/// Validates that the username is unique.
/// </summary>
/// <param name="userService"></param>
/// <param name="logger"></param>
[AttributeUsage(AttributeTargets.Method)]
public class UniqueUsernameAttribute(IUserService userService, ILogger<UniqueUsernameAttribute> logger) : ActionFilterAttribute
{
    /// <summary>
    /// Validates that the username is unique.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionArguments["request"] is not CreateUserRequest request) 
        {
            logger.LogError($"Invalid request: {context.ActionArguments}");
            context.Result = new BadRequestObjectResult("Invalid request");
            return;
        }
        
        if (!await userService.IsUsernameUniqueAsync(request.Username)) 
        {
            logger.LogError($"Username is not unique: {request.Username}");
            context.Result = new BadRequestObjectResult("Username is not unique");
            return;
        }
        
        await next();
    }
}