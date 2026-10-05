using System.ComponentModel.DataAnnotations;

using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Requests;
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
    private readonly Mock<IAnemiaScansRepository> _scans = new();
    private readonly Guid _userId = Guid.NewGuid();

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

    [Fact]
    public async Task Saves_ValueAndDateWithoutTimeOfDay()
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);
        var measured = DateTime.UtcNow.Date.AddHours(-30).AddMinutes(17); // вчера, с «грязным» временем

        var result = await NewService().SetLabHemoglobinAsync(_userId, scan.Id, 12.4, measured);

        result.LabHemoglobin.Should().Be(12.4);
        result.LabMeasuredAt.Should().Be(measured.Date);
        _scans.Verify(r => r.SetLabHemoglobinAsync(scan.Id, _userId, 12.4, measured.Date,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OmittedDate_MeansToday()
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);

        var result = await NewService().SetLabHemoglobinAsync(_userId, scan.Id, 13.0, null);

        result.LabMeasuredAt.Should().Be(DateTime.UtcNow.Date);
    }

    [Fact]
    public async Task ForeignOrMissingScan_Is404_AndNothingIsWritten()
    {
        var scanId = Guid.NewGuid();
        _scans.Setup(r => r.GetOwnedAsync(scanId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnemiaScan?)null);

        var act = () => NewService().SetLabHemoglobinAsync(_userId, scanId, 12.0, null);

        (await act.Should().ThrowAsync<SASException>()).Which.StatusCode.Should().Be(404);
        _scans.Verify(r => r.SetLabHemoglobinAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<double>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScanDeletedBetweenReadAndWrite_Is404()
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);
        _scans.Setup(r => r.SetLabHemoglobinAsync(scan.Id, _userId, It.IsAny<double>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => NewService().SetLabHemoglobinAsync(_userId, scan.Id, 12.0, null);

        (await act.Should().ThrowAsync<SASException>()).Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task FutureDate_Is400()
    {
        var scan = GivenOwnedScan(DateTime.UtcNow);

        var act = () => NewService().SetLabHemoglobinAsync(_userId, scan.Id, 12.0, DateTime.UtcNow.AddDays(1));

        (await act.Should().ThrowAsync<SASException>()).Which.StatusCode.Should().Be(400);
    }

    [Theory]
    [InlineData(-14, true)]   // граница включительно
    [InlineData(14, true)]
    [InlineData(-15, false)]
    [InlineData(15, false)]
    public async Task DateFarFromScan_IsRejected(int daysFromScan, bool accepted)
    {
        // Скан сделан 20 дней назад, чтобы «+14 дней после» не вышло в будущее.
        var scanDate = DateTime.UtcNow.Date.AddDays(-20);
        var scan = GivenOwnedScan(scanDate);

        var act = () => NewService().SetLabHemoglobinAsync(_userId, scan.Id, 12.0, scanDate.AddDays(daysFromScan));

        if (accepted)
            await act.Should().NotThrowAsync();
        else
            (await act.Should().ThrowAsync<SASException>()).Which.StatusCode.Should().Be(400);
    }

    [Theory]
    [InlineData(2.9, false)]
    [InlineData(3.0, true)]
    [InlineData(25.0, true)]
    [InlineData(25.1, false)]
    [InlineData(130.0, false)]  // г/л вместо г/дл — самая частая ошибка ввода
    [InlineData(0.0, false)]    // поле не прислали
    public void Request_RejectsValuesOutsideGramsPerDecilitreRange(double hemoglobin, bool valid)
    {
        var request = new LabHemoglobinRequest { Hemoglobin = hemoglobin };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, true);

        isValid.Should().Be(valid);
        if (!valid) results[0].ErrorMessage.Should().Contain("г/дл");
    }

    [Fact]
    public async Task History_ExposesLabValues()
    {
        var scan = new AnemiaScan
        {
            Id = Guid.NewGuid(), UserId = _userId.ToString(), ScanDate = DateTime.UtcNow,
            ImageSystemId = Guid.NewGuid().ToString(), LabHemoglobin = 11.8, LabMeasuredAt = DateTime.UtcNow.Date
        };
        _scans.Setup(r => r.CountByUserAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _scans.Setup(r => r.GetByUserAsync(_userId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([scan]);

        var result = await NewService().GetHistoryAsync(_userId, 1, 20);

        result.Items[0].LabHemoglobin.Should().Be(11.8);
        result.Items[0].LabMeasuredAt.Should().Be(DateTime.UtcNow.Date);
    }
}
