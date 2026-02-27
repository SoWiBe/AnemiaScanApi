using AnemiaScanApi.Common.Auth;

namespace AnemiaScanApi.Common.Responses;

/// <summary>
/// Ответ на регистрацию пользователя
/// </summary>
public class SignUpResponse
{
    /// <summary>
    /// Токен для авторизации.
    /// </summary>
    public TokenRecord TokenRecord { get; init; } = null!;    
}