using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
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
/// История сканов для экрана истории (P0 №8 в docs/plans/MVP_PLAN.md).
/// Ключевое — выборка строго по userId из JWT и постраничность: до этого
/// эндпоинта список доставался только целиком через GET /profile/info.
/// </summary>
public class AnemiaAnalysisHistoryTests
{
    private readonly Mock<IAnemiaScansRepository> _scans = new();

    private AnemiaAnalysisService NewService(LegalSettings? legal = null) => new(
        _scans.Object,
        Mock.Of<IProfileService>(),
        Mock.Of<IImageCompressor>(),
        Mock.Of<IHemoglobinPredictionService>(),
        Options.Create(legal ?? new LegalSettings()),
        NullLogger<AnemiaAnalysisService>.Instance);

    private static AnemiaScan Scan(DateTime scanDate, bool anemic = true, string? imageSystemId = null) => new()
    {
        Id = Guid.NewGuid(),
        AnalysisId = Guid.NewGuid().ToString(),
        UserId = Guid.NewGuid().ToString(),
        ScanDate = scanDate,
        IsAnemic = anemic,
        Confidence = 0.91,
        HemoglobinLevel = 10.4,
        Severity = "Mild",
        ImageSystemId = imageSystemId ?? Guid.NewGuid().ToString()
    };

    private void GivenScans(Guid userId, long total, params AnemiaScan[] scans)
    {
        _scans.Setup(r => r.CountByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(total);
        _scans.Setup(r => r.GetByUserAsync(userId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scans);
    }

    [Fact]
    public async Task GetHistoryAsync_MapsScanFields()
    {
        var userId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var scan = Scan(new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc), imageSystemId: imageId.ToString());
        GivenScans(userId, 1, scan);

        var result = await NewService().GetHistoryAsync(userId, 1, 20);

        result.Total.Should().Be(1);
        result.Items.Should().HaveCount(1);

        var item = result.Items[0];
        item.Id.Should().Be(scan.Id);
        item.ScanDate.Should().Be(scan.ScanDate);
        item.Sick.Should().Be(Sick.Anemia);
        item.Confidence.Should().Be(scan.Confidence);
        item.HemoglobinLevel.Should().Be(scan.HemoglobinLevel);
        item.Severity.Should().Be("Mild");
        item.ImageSystemId.Should().Be(imageId);
    }

    [Fact]
    public async Task GetHistoryAsync_QueriesOnlyRequestingUser()
    {
        var userId = Guid.NewGuid();
        GivenScans(userId, 0);

        await NewService().GetHistoryAsync(userId, 1, 20);

        // Чужие сканы не должны попадать в выдачу даже теоретически:
        // репозиторий спрашивается ровно про этого пользователя.
        _scans.Verify(r => r.GetByUserAsync(userId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _scans.Verify(r => r.GetByUserAsync(It.Is<Guid>(id => id != userId), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(5, 10, 40)]
    public async Task GetHistoryAsync_TranslatesPageToSkip(int page, int pageSize, int expectedSkip)
    {
        var userId = Guid.NewGuid();
        GivenScans(userId, 100);

        var result = await NewService().GetHistoryAsync(userId, page, pageSize);

        _scans.Verify(r => r.GetByUserAsync(userId, expectedSkip, pageSize, It.IsAny<CancellationToken>()), Times.Once);
        result.Page.Should().Be(page);
        result.PageSize.Should().Be(pageSize);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsDisclaimer()
    {
        var userId = Guid.NewGuid();
        GivenScans(userId, 0);

        var result = await NewService().GetHistoryAsync(userId, 1, 20);

        // Экран истории показывает вердикты, значит обязан показать и оговорку (P0 №12).
        result.Disclaimer.Should().Be(MedicalDisclaimer.Text);
    }

    [Fact]
    public async Task GetHistoryAsync_LeavesImageIdNullWhenNotAGuid()
    {
        var userId = Guid.NewGuid();
        GivenScans(userId, 1, Scan(DateTime.UtcNow, imageSystemId: "не-guid"));

        var result = await NewService().GetHistoryAsync(userId, 1, 20);

        // Одна кривая запись не должна ронять выдачу всей истории.
        result.Items[0].ImageSystemId.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEmptyPageForUserWithoutScans()
    {
        var userId = Guid.NewGuid();
        GivenScans(userId, 0);

        var result = await NewService().GetHistoryAsync(userId, 1, 20);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }
}
