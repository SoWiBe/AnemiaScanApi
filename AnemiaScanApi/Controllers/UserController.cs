using AnemiaScanApi.Attributes;
using AnemiaScanApi.Controllers.Core;
using Microsoft.AspNetCore.Mvc;

using AnemiaScanApi.Models;
using AnemiaScanApi.Models.Requests;
using AnemiaScanApi.Models.Responses;
using AnemiaScanApi.Services;

namespace AnemiaScanApi.Controllers;

/// <summary>
/// Controller for user-related operations.
/// </summary>
[ApiController]
[Route("[controller]")]
public class UserController(IUserService userService, ILogger<UserController> logger) : BaseSasController(logger)
{
    /// <summary>
    /// Retrieves a user by their username.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    [HttpGet("{username}")]
    [ProducesResponseType(typeof(SasUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserByUsername(string username) => Ok(await userService.GetUserByUsernameAsync(username));

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("create")]
    [ProducesResponseType(typeof(CreateUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ServiceFilter(typeof(UniqueUsernameAttribute))]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Map request to model
            MapCreateUserRequestToModel(request, out var model);
            
            // Create user
            var response = await userService.CreateUserAsync(model, cancellationToken);
            
            return Ok(new CreateUserResponse { UserId = response.Id });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating user");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Maps CreateUserRequest to SasUserModel
    /// </summary>
    /// <param name="request"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    private void MapCreateUserRequestToModel(CreateUserRequest request, out SasUser model)
    {
        model = new SasUser
        {
            Username = request.Username,
            HashPassword = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };
    }
}