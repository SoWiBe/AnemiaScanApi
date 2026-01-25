using Microsoft.AspNetCore.Mvc;

using AnemiaScanApi.Models;
using AnemiaScanApi.Services;

namespace AnemiaScanApi.Controllers;

/// <summary>
/// Controller for user-related operations.
/// </summary>
[ApiController]
[Route("[controller]")]
public class UserController(IUserService userService) : ControllerBase
{
    /// <summary>
    /// Retrieves a user by their username.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    [HttpGet("{username}")]
    [ProducesResponseType(typeof(SasUserModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserByUsername(string username) => Ok(await userService.GetUserByUsernameAsync(username));
    
    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    [HttpPost("create")]
    [ProducesResponseType(typeof(SasUserModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateUser([FromBody] SasUserModel user) => Ok(await userService.CreateUserAsync(user));
}