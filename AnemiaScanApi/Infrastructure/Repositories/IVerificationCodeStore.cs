namespace AnemiaScanApi.Infrastructure.Repositories;

public enum VerificationCodeCheck
{
    /// <summary>Код совпал и не истёк.</summary>
    Valid,

    /// <summary>Кода для адреса нет: его не запрашивали или он истёк.</summary>
    Missing,

    /// <summary>Код для адреса есть, но прислан другой.</summary>
    Mismatch
}

/// <summary>
/// Хранилище кодов подтверждения из писем.
///
/// Раньше коды лежали в IMemoryCache, то есть в памяти процесса: рестарт сервиса
/// (а он происходит при каждом деплое) обнулял их, и человек, получивший письмо
/// за минуту до выкатки, вводил правильный код и получал отказ.
/// </summary>
public interface IVerificationCodeStore
{
    /// <summary>Сохраняет код для адреса, заменяя предыдущий.</summary>
    Task SaveAsync(string email, string code, CancellationToken cancellationToken = default);

    /// <summary>Сверяет присланный код с сохранённым. Код при этом не расходуется.</summary>
    Task<VerificationCodeCheck> CheckAsync(string email, string? code, CancellationToken cancellationToken = default);

    /// <summary>Удаляет код адреса — после того как он сделал своё дело.</summary>
    Task RemoveAsync(string email, CancellationToken cancellationToken = default);
}
