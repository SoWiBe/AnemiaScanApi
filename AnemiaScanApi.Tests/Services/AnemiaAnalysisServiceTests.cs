using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Infrastructure.Utils.Core;
using AnemiaScanApi.ML;
using AnemiaScanApi.Settings;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Moq;

namespace AnemiaScanApi.Tests.Services;

/// <summary>
/// Запись результата анализа: в GridFS уходит сжатая копия снимка (P0 №9),
/// а в ответ — медицинский дисклеймер (P0 №12). См. docs/plans/MVP_PLAN.md.
/// </summary>
public class AnemiaAnalysisServiceTests
{
    private static readonly byte[] OriginalImage = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] CompressedImage = [9, 9];

    private readonly Mock<IAnemiaScansRepository> _scans = new();
    private readonly Mock<IProfileService> _profile = new();
    private readonly Mock<IImageCompressor> _compressor = new();
    private readonly Mock<IHemoglobinPredictionService> _hemoglobin = new();

    private AnemiaAnalysisService NewService(LegalSettings? legal = null)
    {
        _compressor.Setup(c => c.CompressForStorage(It.IsAny<byte[]>())).Returns(CompressedImage);

        _scans.Setup(r => r.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectId.GenerateNewId());

        _scans.Setup(r => r.CreateAnemiaScanAsync(It.IsAny<AnemiaScan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnemiaScan scan, CancellationToken _) => scan);

        return new AnemiaAnalysisService(_scans.Object, _profile.Object, _compressor.Object, _hemoglobin.Object,
            Options.Create(legal ?? new LegalSettings()), NullLogger<AnemiaAnalysisService>.Instance);
    }

    private Task<Common.Responses.AnalyseAnemiaResponse> WriteAsync(AnemiaAnalysisService service)
        => service.WriteAnalyseAsync(Guid.NewGuid(), new ClassifierDecision(true, 0.93f, "test-classifier"), OriginalImage, CancellationToken.None);

    /// <summary>
    /// Прогон с посчитанным Hb и заданным профилем — ровно тот путь, на котором
    /// выбирается шкала тяжести (P0 №10).
    /// </summary>
    private async Task<(Common.Responses.AnalyseAnemiaResponse Response, AnemiaScan Scan)> WriteWithProfileAsync(
        float hemoglobin, int? ageYears, Sex? sex, bool hemoglobinInDomain = false)
    {
        var userId = Guid.NewGuid();

        // Регрессия мокается: какую модель она выберет и почему — предмет
        // HemoglobinPredictionServiceTests. Здесь важно, что сервис передаёт ей
        // возраст и честно пробрасывает её флаг достоверности.
        _hemoglobin.Setup(h => h.TryPredictAsync(OriginalImage, ageYears, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HemoglobinPrediction(hemoglobin, ModelVersions.CielabHemoglobinRegressionAdults,
                AgeInDomain: hemoglobinInDomain, InputSegmented: hemoglobinInDomain));

        if (ageYears is not null || sex is not null)
        {
            _profile.Setup(p => p.GetProfileAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SasUser
                {
                    Id = userId,
                    Email = "user@example.com",
                    FullName = "Тест",
                    HashPassword = "hash",
                    Sex = sex,
                    BirthDate = ageYears is { } age ? DateTime.UtcNow.Date.AddYears(-age).AddDays(-1) : null
                });
        }

        // Сервис строим до переопределения мока: NewService() сам настраивает
        // CreateAnemiaScanAsync и затёр бы callback, если сделать наоборот.
        var service = NewService();

        AnemiaScan? created = null;
        _scans.Setup(r => r.CreateAnemiaScanAsync(It.IsAny<AnemiaScan>(), It.IsAny<CancellationToken>()))
            .Callback((AnemiaScan scan, CancellationToken _) => created = scan)
            .ReturnsAsync((AnemiaScan scan, CancellationToken _) => scan);

        var response = await service.WriteAnalyseAsync(userId, new ClassifierDecision(true, 0.9f, "test-classifier"), OriginalImage, CancellationToken.None);

        return (response, created!);
    }

    [Fact]
    public async Task WriteAnalyseAsync_UsesMaleScaleForAdultMan()
    {
        // 12.5 — норма по женской шкале и лёгкая анемия по мужской.
        var (response, scan) = await WriteWithProfileAsync(12.5f, ageYears: 40, sex: Sex.Male);

        response.Severity.Should().Be("Mild");
        response.SeverityReference.Should().Be(SeverityReference.AdultMale);
        response.DemographicsComplete.Should().BeTrue();
        scan.SeverityReference.Should().Be(nameof(SeverityReference.AdultMale),
            "шкала фиксируется в скане, чтобы правка профиля не меняла старые результаты");
    }

    [Fact]
    public async Task WriteAnalyseAsync_UsesFemaleScaleForAdultWoman()
    {
        var (response, _) = await WriteWithProfileAsync(12.5f, ageYears: 40, sex: Sex.Female);

        response.Severity.Should().Be("Non-Anemic");
        response.SeverityReference.Should().Be(SeverityReference.AdultFemale);
    }

    [Fact]
    public async Task WriteAnalyseAsync_UsesChildScaleForChild()
    {
        // 11.5 для ребёнка — норма, для любого взрослого — лёгкая анемия.
        var (response, _) = await WriteWithProfileAsync(11.5f, ageYears: 3, sex: Sex.Female);

        response.Severity.Should().Be("Non-Anemic");
        response.SeverityReference.Should().Be(SeverityReference.PreSchoolChild);
    }

    [Fact]
    public async Task WriteAnalyseAsync_FlagsIncompleteProfile()
    {
        // Профиль не настроен в моке — GetProfileAsync вернёт null, и сервис
        // должен не упасть, а отметить, что демография неполная.
        var (response, _) = await WriteWithProfileAsync(12.5f, ageYears: null, sex: null);

        response.SeverityReference.Should().Be(SeverityReference.AssumedStrictest);
        response.DemographicsComplete.Should().BeFalse();
        response.Severity.Should().Be("Mild", "при неизвестной демографии берётся строгая шкала");
    }

    [Fact]
    public async Task WriteAnalyseAsync_RecordsAgreementWhenPathsMatch()
    {
        // Взрослая женщина, Hb 10.5 -> severity Moderate; классификатор тоже Low_Hb.
        var (response, scan) = await WriteWithProfileAsync(10.5f, ageYears: 40, sex: Sex.Female);

        response.Verdict.Outcome.Should().Be(Sick.Anemia);
        response.Verdict.Agreement.Should().Be(VerdictAgreement.Agree);
        scan.VerdictAgreement.Should().Be(nameof(VerdictAgreement.Agree));
    }

    [Fact]
    public async Task WriteAnalyseAsync_RecordsDisagreement()
    {
        // Классификатор говорит Low_Hb, а Hb 13.5 у женщины — это норма.
        var (response, scan) = await WriteWithProfileAsync(13.5f, ageYears: 40, sex: Sex.Female);

        response.Verdict.Outcome.Should().Be(Sick.Anemia, "вердикт остаётся за классификатором");
        response.Verdict.Agreement.Should().Be(VerdictAgreement.Disagree);
        scan.VerdictAgreement.Should().Be(nameof(VerdictAgreement.Disagree));
    }

    [Fact]
    public async Task WriteAnalyseAsync_PassesPatientAgeToRegression()
    {
        // Какую из двух регрессий брать, решает возраст, а он известен только
        // после чтения профиля — поэтому вызов переехал сюда из контроллера.
        await WriteWithProfileAsync(10.5f, ageYears: 40, sex: Sex.Male);

        _hemoglobin.Verify(h => h.TryPredictAsync(OriginalImage, 40, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteAnalyseAsync_TakesHemoglobinDomainFromPrediction(bool inDomain)
    {
        var (response, _) = await WriteWithProfileAsync(10.5f, ageYears: 40, sex: Sex.Male, hemoglobinInDomain: inDomain);

        response.Verdict.HemoglobinInDomain.Should().Be(inDomain);
    }

    [Fact]
    public async Task WriteAnalyseAsync_RecordsWhichModelComputedHemoglobin()
    {
        var (_, scan) = await WriteWithProfileAsync(10.5f, ageYears: 40, sex: Sex.Male);

        scan.ModelVersion.Should().Be(ModelVersions.CielabHemoglobinRegressionAdults);
    }

    [Fact]
    public async Task WriteAnalyseAsync_LeavesSeverityNullWhenHemoglobinNotComputed()
    {
        var response = await WriteAsync(NewService());

        response.Severity.Should().BeNull();
        response.SeverityReference.Should().BeNull();
        response.Verdict.Agreement.Should().Be(VerdictAgreement.Unavailable);
    }

    [Fact]
    public async Task WriteAnalyseAsync_StoresCompressedImageNotOriginal()
    {
        var service = NewService();

        await WriteAsync(service);

        _compressor.Verify(c => c.CompressForStorage(OriginalImage), Times.Once);
        _scans.Verify(r => r.SaveImageAsync(CompressedImage, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteAnalyseAsync_ReturnsDefaultDisclaimer()
    {
        var response = await WriteAsync(NewService());

        response.Disclaimer.Should().Be(MedicalDisclaimer.Text);
        response.Sick.Should().Be(Sick.Anemia, "Low_Hb — вердикт TF-классификатора об анемии");
    }

    [Fact]
    public async Task WriteAnalyseAsync_PrefersDisclaimerFromConfiguration()
    {
        // Формулировки правятся без релиза мобилки — через Legal__DisclaimerText.
        const string custom = "Скрининг, не диагноз.";

        var response = await WriteAsync(NewService(new LegalSettings { DisclaimerText = custom }));

        response.Disclaimer.Should().Be(custom);
    }
}
