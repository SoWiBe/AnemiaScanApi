namespace AnemiaScanApi.Common.Constants;

public static class ValidationConstants
{
    public const string RequiredEmailErrorMessage = "Пожалуйста, укажите вашу почту";
    public const string RequiredPasswordErrorMessage = "Пожалуйста, укажите ваш пароль";
    public const string RequiredEmailCodeErrorMessage = "Пожалуйста, укажите код из письма на вашей почте";
    public const string EmailShouldBeLessThan256CharactersErrorMessage = "Email должен быть менее 256 символов";
    public const string RequiredFullNameErrorMessage = "Пожалуйста, укажите ваше полное имя";
    public const string InvalidFullNameErrorMessage = "Полное имя должно содержать от 2 до 256 символов";
    public const string InvalidEmailOrPasswordErrorMessage = "Неверный адрес электронной почты или пароль";
    public const string InvalidEmailCodeErrorMessage = "Неверный код из письма на вашей почте";
    public const string InvalidPasswordErrorMessage = "Пароль должен содержать от 8 до 256 символов";
    public const string PasswordsDoNotMatchErrorMessage = "Пароли не совпадают";
    public const string RequiredBirthDateErrorMessage = "Пожалуйста, укажите вашу дату рождения";
    public const string PasswordShouldBeAtLeast8CharactersErrorMessage = "Пароль должен содержать от 8 до 256 символов";
}