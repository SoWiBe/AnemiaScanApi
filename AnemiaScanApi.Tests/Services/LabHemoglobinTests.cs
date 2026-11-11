using System.Text.Json;

using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Common.Responses;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Infrastructure.Utils.Core;
using AnemiaScanApi.Settings;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AnemiaScanApi.Tests.Services;

/// <summary>
/// Hb из анализа крови, который пользователь вносит к скану. Это единственный
/// способ измерить модели на живых людях, поэтому главное здесь — не пустить в
/// базу мусор: чужой скан, значение в г/л, дату из другого месяца.
/// </summary>
public class LabHemoglobinTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly Mock<IAnemiaScansRepository> _scans = new();
    private readonly Guid _userId = Guid.NewGuid();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private AnemiaAnalysisService NewService() => new(
        _scans.Object,
        Mock.Of<IProfileService>(),
        Mock.Of<IImageCompressor>(),
        Mock.Of<IHemoglobinPredictionService>(),
        Options.Create(new LegalSettings()),
        NullLogger<AnemiaAnalysisService>.Instance);

    private AnemiaScan GivenOwnedScan(DateTime scanDate)
    {
        var scan = new AnemiaScan { Id = Guid.NewGuid(), UserId = _userId.ToString(), ScanDate = scanDate };
        _scans.Setup(r => r.GetOwnedAsync(scan.Id, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(scan);
        _scans.Setup(r => r.SetLabHemoglobinAsync(scan.Id, _userId, It.IsAny<double>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return scan;
    }

    private void VerifyNothingWritten()
        => _scans.Verify(r => r.SetLabHemoglobinAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<double>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);

    private static async Task<SASException> ShouldFail(Func<Task> act, int statusCode)
    {
        var exception = (await act.Should().ThrowAsync<SASException>()).Which;
        exception.StatusCode.Should().Be(statusCode);
        return exception;
    }

    [Fact]
    public async Task Saves_ValueAndCalendarDate()
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);
        var measured = Today.AddDays(-1);

        var result = await NewService().SetLabHemoglobinAsync(_userId, scan.Id, 12.4, measured);

        result.ScanId.Should().Be(scan.Id);
        result.LabHemoglobin.Should().Be(12.4);
        result.LabMeasuredAt.Should().Be(measured);
    }

    /// <summary>
    /// В базу дата уходит полночью UTC с явным Kind. С Kind=Unspecified драйвер
    /// Mongo считает время местным и сдвигает дату на пояс сервера: на машине в
    /// UTC+6 «11 октября» сохранялось как «10 октября, 18:00 UTC».
    /// </summary>
    [Fact]
    public async Task StoredDate_IsUtcMidnightOfTheSameCalendarDay()
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);
        var measured = Today.AddDays(-1);
        DateTime stored = default;
        _scans.Setup(r => r.SetLabHemoglobinAsync(scan.Id, _userId, 12.4, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, Guid _, double _, DateTime date, CancellationToken _) => stored = date)
            .ReturnsAsync(true);

        await NewService().SetLabHemoglobinAsync(_userId, scan.Id, 12.4, measured);

        stored.Kind.Should().Be(DateTimeKind.Utc);
        stored.Should().Be(new DateTime(measured.Year, measured.Month, measured.Day, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task OmittedDate_MeansTodayUtc()
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);

        var result = await NewService().SetLabHemoglobinAsync(_userId, scan.Id, 13.0, null);

        result.LabMeasuredAt.Should().Be(Today);
    }

    [Fact]
    public async Task ForeignOrMissingScan_Is404_AndNothingIsWritten()
    {
        var scanId = Guid.NewGuid();
        _scans.Setup(r => r.GetOwnedAsync(scanId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnemiaScan?)null);

        await ShouldFail(() => NewService().SetLabHemoglobinAsync(_userId, scanId, 12.0, null), 404);

        VerifyNothingWritten();
    }

    [Fact]
    public async Task ScanDeletedBetweenReadAndWrite_Is404()
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);
        _scans.Setup(r => r.SetLabHemoglobinAsync(scan.Id, _userId, It.IsAny<double>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await ShouldFail(() => NewService().SetLabHemoglobinAsync(_userId, scan.Id, 12.0, null), 404);
    }

    // ---------- значение ----------

    [Theory]
    [InlineData(3.0)]
    [InlineData(12.4)]
    [InlineData(25.0)]
    public async Task ValueInsideRange_IsAccepted(double hemoglobin)
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);

        var result = await NewService().SetLabHemoglobinAsync(_userId, scan.Id, hemoglobin, null);

        result.LabHemoglobin.Should().Be(hemoglobin);
    }

    [Theory]
    [InlineData(2.9)]
    [InlineData(25.1)]
    [InlineData(130.0)]         // г/л вместо г/дл — самая частая ошибка ввода
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task ValueOutsideRange_Is400_WithUnitHint(double hemoglobin)
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);

        var exception = await ShouldFail(
            () => NewService().SetLabHemoglobinAsync(_userId, scan.Id, hemoglobin, null), 400);

        exception.Message.Should().Contain("г/дл").And.Contain("130 г/л = 13.0 г/дл");
        VerifyNothingWritten();
    }

    [Fact]
    public async Task MissingValue_Is400()
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);

        var exception = await ShouldFail(
            () => NewService().SetLabHemoglobinAsync(_userId, scan.Id, hemoglobin: null, null), 400);

        exception.Message.Should().Be("Укажите гемоглобин");
        VerifyNothingWritten();
    }

    // ---------- дата ----------

    /// <summary>
    /// Пользователь в Алматы (UTC+5) в час ночи вносит сегодняшний анализ. По
    /// его календарю это «сегодня», по UTC — уже «завтра». Отклонять такую дату
    /// как будущую нельзя.
    /// </summary>
    [Fact]
    public async Task TomorrowByUtc_IsAccepted_BecauseItIsTodayEastOfGreenwich()
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);

        var act = () => NewService().SetLabHemoglobinAsync(_userId, scan.Id, 12.0, Today.AddDays(1));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DateBeyondAnyTimeZone_Is400()
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);

        var exception = await ShouldFail(
            () => NewService().SetLabHemoglobinAsync(_userId, scan.Id, 12.0, Today.AddDays(2)), 400);

        exception.Message.Should().Be("Дата анализа не может быть в будущем");
        VerifyNothingWritten();
    }

    /// <summary>
    /// Правило для пользователя — 14 дней от скана. Сервер считает дни по UTC, а
    /// приложение — по местному календарю, и эти даты расходятся максимум на
    /// день. Поэтому сервер принимает до 15 дней: всё, что приложение сочло
    /// допустимым по своим часам, сервер тоже примет.
    /// </summary>
    [Theory]
    [InlineData(-14, true)]
    [InlineData(14, true)]
    [InlineData(-15, true)]     // запас на часовой пояс
    [InlineData(15, true)]
    [InlineData(-16, false)]
    [InlineData(16, false)]
    public async Task DateFarFromScan_IsRejected(int daysFromScan, bool accepted)
    {
        // Скан сделан 20 дней назад, чтобы «+16 дней после» не вышло в будущее.
        var scanDate = DateTime.UtcNow.Date.AddDays(-20);
        var scan = GivenOwnedScan(scanDate);
        var measured = DateOnly.FromDateTime(scanDate).AddDays(daysFromScan);

        var act = () => NewService().SetLabHemoglobinAsync(_userId, scan.Id, 12.0, measured);

        if (accepted)
            await act.Should().NotThrowAsync();
        else
            (await ShouldFail(act, 400)).Message.Should().Contain("14 дней");
    }

    [Fact]
    public async Task OldScan_AcceptsBloodTestTakenAroundThatScan()
    {
        // Окно привязано к дате скана, а не к сегодняшнему дню: к скану
        // двухмесячной давности можно внести анализ, сданный тогда же.
        var scanDate = DateTime.UtcNow.Date.AddDays(-60);
        var scan = GivenOwnedScan(scanDate);

        var result = await NewService().SetLabHemoglobinAsync(
            _userId, scan.Id, 12.0, DateOnly.FromDateTime(scanDate).AddDays(3));

        result.LabMeasuredAt.Should().Be(DateOnly.FromDateTime(scanDate).AddDays(3));
    }

    // ---------- формат на проводе ----------

    [Fact]
    public void Request_AcceptsCalendarDate()
    {
        var request = JsonSerializer.Deserialize<LabHemoglobinRequest>(
            """{ "hemoglobin": 12.4, "measuredAt": "2026-10-11" }""", WebJson)!;

        request.Hemoglobin.Should().Be(12.4);
        request.MeasuredAt.Should().Be(new DateOnly(2026, 10, 11));
    }

    [Theory]
    [InlineData("2026-10-11T01:00:00+05:00")]   // время с поясом — именно оно раньше сдвигало дату
    [InlineData("2026-10-11T00:00:00Z")]
    [InlineData("11.10.2026")]
    public void Request_RejectsAnythingButCalendarDate(string measuredAt)
    {
        var json = $$"""{ "hemoglobin": 12.4, "measuredAt": "{{measuredAt}}" }""";

        var act = () => JsonSerializer.Deserialize<LabHemoglobinRequest>(json, WebJson);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Request_WithoutFields_LeavesThemNull_SoServiceCanExplain()
    {
        var request = JsonSerializer.Deserialize<LabHemoglobinRequest>("{}", WebJson)!;

        request.Hemoglobin.Should().BeNull("отсутствующее поле — не то же, что ноль");
        request.MeasuredAt.Should().BeNull();
    }

    [Fact]
    public void Response_CarriesDateWithoutTimeOrZone()
    {
        var scanId = Guid.NewGuid();

        var json = JsonSerializer.Serialize(
            new LabHemoglobinResponse(scanId, 12.4, new DateOnly(2026, 10, 11)), WebJson);

        json.Should().Be($$"""{"scanId":"{{scanId}}","labHemoglobin":12.4,"labMeasuredAt":"2026-10-11"}""");
    }

    // ---------- история ----------

    [Fact]
    public async Task History_ExposesLabValues_AsCalendarDate()
    {
        var scan = new AnemiaScan
        {
            Id = Guid.NewGuid(), UserId = _userId.ToString(), ScanDate = DateTime.UtcNow,
            ImageSystemId = Guid.NewGuid().ToString(), LabHemoglobin = 11.8,
            LabMeasuredAt = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc)
        };
        _scans.Setup(r => r.CountByUserAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _scans.Setup(r => r.GetByUserAsync(_userId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([scan]);

        var result = await NewService().GetHistoryAsync(_userId, 1, 20);

        result.Items[0].LabHemoglobin.Should().Be(11.8);
        result.Items[0].LabMeasuredAt.Should().Be(new DateOnly(2026, 10, 11));
        JsonSerializer.Serialize(result.Items[0], WebJson).Should().Contain("\"labMeasuredAt\":\"2026-10-11\"");
    }

    [Fact]
    public async Task History_LabFieldsAreNull_UntilUserEntersThem()
    {
        var scan = new AnemiaScan
        {
            Id = Guid.NewGuid(), UserId = _userId.ToString(), ScanDate = DateTime.UtcNow,
            ImageSystemId = Guid.NewGuid().ToString()
        };
        _scans.Setup(r => r.CountByUserAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _scans.Setup(r => r.GetByUserAsync(_userId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([scan]);

        var result = await NewService().GetHistoryAsync(_userId, 1, 20);

        result.Items[0].LabHemoglobin.Should().BeNull();
        result.Items[0].LabMeasuredAt.Should().BeNull();
    }
}
