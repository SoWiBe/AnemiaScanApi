namespace AnemiaScanApi.Models.Auth;

/// <summary>
/// JWT token.
/// </summary>
/// Used for authentication and authorization.
/// <param name="AccessToken"></param>
/// <param name="RefreshToken"></param>
public record TokenRecord(string AccessToken, string RefreshToken);