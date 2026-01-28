namespace AnemiaScanApi.Services.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base service for SAS services.
/// </summary>
/// <param name="logger"></param>
/// <typeparam name="T"></typeparam>
public abstract class BaseService<T>(ILogger<T> logger)
{
    protected readonly ILogger<T> Logger = logger;
}