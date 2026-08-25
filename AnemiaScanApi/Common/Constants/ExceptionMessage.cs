namespace AnemiaScanApi.Common.Constants;

public static class ExceptionMessage
{
    public const string InvalidEmailOrPassword = "Неверный адрес электронной почты или пароль";
    public const string PredictionFail = "Определить вероятность анемии не удалось, повторите попытку позже";
    public const string ProfileNotFound = "Пользователь не найден в системе";
    public const string CourseNotFound = "Курс не найден";
    public const string CourseContentNotFound = "Контент курса не найден";
    public const string EnrollmentNotFound = "Запись на курс не найдена";
    public const string AlreadyEnrolled = "Вы уже записаны на этот курс";
    public const string CourseDayNotFound = "Указанный день курса не найден";
    public const string DayNotYetAvailable = "Этот день ещё не открыт";
    public const string CheckpointScanRequired = "На чекпоинт-дне нужно привязать ре-скан";
    public const string CourseTaskNotFound = "Задача не найдена в этом дне курса";
}