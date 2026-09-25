namespace AnemiaScanApi.Common.Responses;

/// <summary>
/// Записанное на сервере согласие пользователя (P0 №13). Клиент сравнивает
/// <paramref name="PolicyVersion"/> с версией из <c>GET /legal</c> и, если они
/// разошлись, просит подписать заново через <c>POST /profile/consent</c>.
/// </summary>
public record ConsentResponse(string PolicyVersion, DateTime AcceptedAt, bool Accepted);
