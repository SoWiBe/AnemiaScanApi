using System.Security.Claims;
using AnemiaScanApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace AnemiaScanApi.Controllers.Core;

/// <summary>
/// Base controller for Smart Anemia Scan API.
/// </summary>
public abstract class BaseSasController : ControllerBase
{
    /// <summary>
    /// Initializes a new instance of the BaseSasController class.
    /// </summary>
    protected BaseSasController(ILogger<BaseSasController> logger)
    {
        Logger = logger;
    }

    protected ILogger<BaseSasController> Logger { get; private set; }

    protected Guid GetUserId()
    {
        return Guid.Parse(HttpContext.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)!.Value);
    }
}