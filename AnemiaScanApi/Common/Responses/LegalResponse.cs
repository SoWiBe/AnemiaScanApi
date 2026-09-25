namespace AnemiaScanApi.Common.Responses;

/// <summary>
/// Действующие юридические тексты: версия политики конфиденциальности и
/// медицинский дисклеймер (P0 №12 и №13). Отдаётся без авторизации — экран
/// регистрации должен показать политику до того, как аккаунт существует.
/// </summary>
public record LegalResponse(
    string PolicyVersion,
    string? PolicyUrl,
    string DisclaimerVersion,
    string DisclaimerText);
