using MongoDB.Bson;

using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Common.Responses;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Infrastructure.Utils.Core;
using AnemiaScanApi.ML;
using AnemiaScanApi.Settings;

using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Infrastructure.Services;

public class AnemiaAnalysisService(
    IAnemiaScansRepository anemiaScansRepository,
    IProfileService profileService,
    IImageCompressor imageCompressor,
    IHemoglobinPredictionService hemoglobinPredictionService,
    IOptions<LegalSettings> legalSettings,
    ILogger<AnemiaAnalysisService> logger)
    : BaseService<AnemiaAnalysisService>(logger), IAnemiaAnalysisService
{
    public async Task<(ObjectId, Guid)> SaveImageAsync(Guid analysisId, Guid userId, byte[] image,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Analyzing anemia for analysis ID {AnalysisId} and user ID {UserId}", analysisId, userId);

        var imageId = Guid.NewGuid();
        // В GridFS кладём уменьшенную копию: оригинал с телефона — 2-3 МБ, а
        // предсказания уже посчитаны по исходным байтам выше по стеку (P0 №9).
        var storedImage = imageCompressor.CompressForStorage(image);
        var gridFsId = await anemiaScansRepository.SaveImageAsync(
            storedImage, $"anemia_scan_{imageId}",
            "image/jpeg",
            analysisId, 
            userId, 
            cancellationToken);
        
        Logger.LogInformation("Anemia analysis completed for analysis ID {AnalysisId} and user ID {UserId}", analysisId, userId);
        Logger.LogInformation("Image saved to GridFS: {ObjectId}", gridFsId);
        return (gridFsId, imageId);
    }
    
    public async Task<byte[]> GetImageAsync(string analysisId, CancellationToken cancellationToken = default)
    {
        var anemiaScan = await anemiaScansRepository.GetAnemiaScanAsync(analysisId, cancellationToken);
        return await anemiaScansRepository.DownloadImageAsync(anemiaScan.ImageSystemId, cancellationToken);
    }

    public async Task<AnalysisHistoryResponse> GetHistoryAsync(Guid userId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var skip = (page - 1) * pageSize;

        var total = await anemiaScansRepository.CountByUserAsync(userId, cancellationToken);
        var scans = await anemiaScansRepository.GetByUserAsync(userId, skip, pageSize, cancellationToken);

        Logger.LogInformation("Returning {Count} of {Total} scans for user {UserId}", scans.Count, total, userId);

        var items = scans
            .Select(AnalysisHistoryItemResponse.FromScan)
            .ToList();

        return new AnalysisHistoryResponse(items, total, page, pageSize, ResolveDisclaimer());
    }

    /// <summary>
    /// Насколько анализ крови может отстоять от скана по времени. Через месяц Hb
    /// уже мог измениться, и сравнивать его с оценкой по снимку нельзя.
    /// </summary>
    public const int LabMaxDaysFromScan = 14;

    /// <summary>Границы правдоподобного Hb, г/дл. 130 (г/л вместо г/дл) сюда не проходит.</summary>
    public const double LabMinHemoglobin = 3.0;
    public const double LabMaxHemoglobin = 25.0;

    /// <summary>
    /// Запас на часовой пояс. Сервер считает дни по UTC, а пользователь присылает
    /// дату по своему календарю, и она может отличаться от UTC на день: в Алматы
    /// (UTC+5) с полуночи до пяти утра «сегодня» — это уже завтра по UTC. Без
    /// запаса сегодняшний анализ в эти часы отклонялся бы как «дата в будущем».
    /// </summary>
    private const int TimeZoneSlackDays = 1;

    public async Task<LabHemoglobinResponse> SetLabHemoglobinAsync(Guid userId, Guid scanId, double? hemoglobin,
        DateOnly? measuredAt, CancellationToken cancellationToken = default)
    {
        if (hemoglobin is not { } value)
            throw new SASException("Укажите гемоглобин", StatusCodes.Status400BadRequest);

        if (double.IsNaN(value) || value < LabMinHemoglobin || value > LabMaxHemoglobin)
            throw new SASException(
                $"Гемоглобин указывается в г/дл, от {LabMinHemoglobin:0} до {LabMaxHemoglobin:0} (в г/л разделите на 10: 130 г/л = 13.0 г/дл)",
                StatusCodes.Status400BadRequest);

        var scan = await anemiaScansRepository.GetOwnedAsync(scanId, userId, cancellationToken)
                   ?? throw new SASException("Скан не найден", StatusCodes.Status404NotFound);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var date = measuredAt ?? today;

        if (date.DayNumber > today.DayNumber + TimeZoneSlackDays)
            throw new SASException("Дата анализа не может быть в будущем", StatusCodes.Status400BadRequest);

        var scanDay = DateOnly.FromDateTime(scan.ScanDate);
        if (Math.Abs(date.DayNumber - scanDay.DayNumber) > LabMaxDaysFromScan + TimeZoneSlackDays)
            throw new SASException(
                $"Анализ крови должен быть сдан не более чем за {LabMaxDaysFromScan} дней до или после скана: иначе гемоглобин мог измениться и сравнивать его со снимком нельзя",
                StatusCodes.Status400BadRequest);

        // В базе дата лежит полночью UTC. Kind задаётся явно: с Unspecified
        // драйвер считал бы время местным и сдвигал дату на пояс сервера.
        var updated = await anemiaScansRepository.SetLabHemoglobinAsync(
            scanId, userId, value, date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), cancellationToken);

        // Скан могли удалить между чтением и записью.
        if (!updated)
            throw new SASException("Скан не найден", StatusCodes.Status404NotFound);

        Logger.LogInformation("Для скана {ScanId} внесён Hb из анализа крови (модель дала {Predicted})",
            scanId, scan.HemoglobinLevel);

        return new LabHemoglobinResponse(scanId, value, date);
    }

    public async Task<AnalyseAnemiaResponse> WriteAnalyseAsync(
        Guid userId, ClassifierDecision decision, byte[] image, CancellationToken cancellationToken)
    {
        var analysisId = Guid.NewGuid();
        var scanDate = DateTime.UtcNow;

        var (gridFsId, imageId) = await SaveImageAsync(analysisId, userId, image, cancellationToken);
        var isAnemia = decision.IsAnemic;

        // Демография нужна и шкале тяжести (P0 №10), и выбору регрессии Hb: детская
        // и взрослая модели обучены на разных возрастах. Читаем один раз.
        var (ageYears, sex) = await ReadDemographicsAsync(userId, cancellationToken);

        // Регрессия вызывается здесь, а не в контроллере: до чтения профиля
        // возраст неизвестен и выбрать модель нечем. При сбое — null, основной
        // вердикт классификатора от этого не страдает.
        var hemoglobinPrediction = await hemoglobinPredictionService.TryPredictAsync(image, ageYears, cancellationToken);

        // Тяжесть зависит от пола и возраста, поэтому считается здесь, а не в
        // модели: регрессия отдаёт только число Hb и о пациенте не знает.
        var severity = hemoglobinPrediction is null
            ? null
            : SeverityBands.Classify(hemoglobinPrediction.HemoglobinLevel, ageYears, sex);

        if (severity is { DemographicsComplete: false })
            Logger.LogInformation("Профиль {UserId} неполон, тяжесть оценена по строгой шкале", userId);

        // Сводим два независимых пути в один вердикт.
        var verdict = VerdictReconciler.Reconcile(isAnemia, severity, hemoglobinPrediction?.InDomain ?? false);

        if (verdict.Agreement == VerdictAgreement.Disagree)
        {
            Logger.LogInformation(
                "Модели разошлись на скане {AnalysisId}: классификатор {Outcome}, severity {Severity}",
                analysisId, verdict.Outcome, severity!.Severity);
        }

        var anemiaScan = new AnemiaScan
        {
            AnalysisId = analysisId.ToString(),
            ImageSystemId = imageId.ToString(),
            ImageGridFsId = gridFsId,
            AnemiaProbability = decision.AnemiaProbability,
            ClassifierVersion = decision.ClassifierVersion,
            UserId = userId.ToString(),
            // CIELab-регрессия (см. IHemoglobinPredictionService) — отдельный
            // путь от основного TF-классификатора выше; null, если он не
            // смог посчитать признаки для этого изображения.
            HemoglobinLevel = hemoglobinPrediction?.HemoglobinLevel,
            Severity = severity?.Severity,
            // Шкала фиксируется в скане: если пользователь потом поправит в
            // профиле пол или возраст, старые результаты не должны задним
            // числом менять смысл.
            SeverityReference = severity?.Reference.ToString(),
            // Согласие путей пишем в скан, чтобы потом можно было посчитать, как
            // часто модели расходятся, не переобрабатывая снимки заново.
            VerdictAgreement = verdict.Agreement.ToString(),
            // Какая из двух регрессий посчитала Hb — чтобы в истории было видно.
            ModelVersion = hemoglobinPrediction?.ModelVersion,
            IsAnemic = isAnemia,
            ScanDate = scanDate
        };

        // сохраняем анализ в базе данных
        var createdAnemiaScan = await anemiaScansRepository.CreateAnemiaScanAsync(anemiaScan, cancellationToken);

        return new AnalyseAnemiaResponse(createdAnemiaScan.Id, imageId, scanDate, ResolveDisclaimer());
    }

    /// <summary>
    /// Пол и возраст пациента из профиля.
    ///
    /// Возраст берём из даты рождения, а не из поля Age: его пользователь
    /// заполняет один раз и оно устаревает, а шкала у подростка меняется
    /// дважды до 15 лет.
    ///
    /// Сбой чтения профиля не роняет анализ: вернём пустую демографию, и дальше
    /// сработают безопасные значения по умолчанию — строгая шкала тяжести и
    /// отказ считать Hb достоверным.
    /// </summary>
    private async Task<(int? AgeYears, Sex? Sex)> ReadDemographicsAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await profileService.GetProfileAsync(userId, cancellationToken);
            return (AgeFrom(user.BirthDate) ?? user.Age, user.Sex);
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Не удалось прочитать профиль {UserId} для оценки результата", userId);
            return (null, null);
        }
    }

    private static int? AgeFrom(DateTime? birthDate)
    {
        if (birthDate is not { } birth) return null;

        var today = DateTime.UtcNow.Date;
        var age = today.Year - birth.Year;
        if (birth.Date > today.AddYears(-age)) age--;

        return age >= 0 ? age : null;
    }

    /// <summary>
    /// Текст дисклеймера: переопределение из конфига, иначе встроенный (P0 №12).
    /// </summary>
    private string ResolveDisclaimer()
        => string.IsNullOrWhiteSpace(legalSettings.Value.DisclaimerText)
            ? MedicalDisclaimer.Text
            : legalSettings.Value.DisclaimerText;
}