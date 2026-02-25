using System.Net;

using AnemiaScanApi.Exceptions;

namespace AnemiaScanApi.Middleware;

public class SASMiddleware(RequestDelegate next, ILogger<SASMiddleware> logger)
{
    private const string ContentType =  "application/json";
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (SASException ex)
        {
            await HandleExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            await SetupContext(context, new ExceptionResponse(HttpStatusCode.InternalServerError, ex.Message));
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, SASException exception)
    {
        logger.LogError(exception, exception.Message);

        var response = exception.StatusCode switch
        {
            409 => new ExceptionResponse(HttpStatusCode.Conflict, exception.Message),
            404 => new ExceptionResponse(HttpStatusCode.NotFound, exception.Message),
            403 => new ExceptionResponse(HttpStatusCode.Forbidden, exception.Message),
            401 => new ExceptionResponse(HttpStatusCode.Unauthorized, exception.Message),
            400 => new ExceptionResponse(HttpStatusCode.BadRequest, exception.Message),
            _ => new ExceptionResponse(HttpStatusCode.InternalServerError, exception.Message),
        };

        await SetupContext(context, response);
    }
    
    private static async Task SetupContext(HttpContext context, ExceptionResponse response)
    {
        context.Response.ContentType = ContentType;
        context.Response.StatusCode = (int)response.StatusCode;
        await context.Response.WriteAsJsonAsync(response);
    }
}

public record ExceptionResponse(HttpStatusCode StatusCode, string Description);