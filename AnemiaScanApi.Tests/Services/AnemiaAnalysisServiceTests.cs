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

    private AnemiaAnalysisService NewService(LegalSettings? legal = null)
    {
        _compressor.Setup(c => c.CompressForStorage(It.IsAny<byte[]>())).Returns(CompressedImage);

        _scans.Setup(r => r.SaveImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectId.GenerateNewId());

        _scans.Setup(r => r.CreateAnemiaScanAsync(It.IsAny<AnemiaScan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AnemiaScan scan, CancellationToken _) => scan);

        return new AnemiaAnalysisService(_scans.Object, _profile.Object, _compressor.Object,
            Options.Create(legal ?? new LegalSettings()), NullLogger<AnemiaAnalysisService>.Instance);
    }

    private Task<Common.Responses.AnalyseAnemiaResponse> WriteAsync(AnemiaAnalysisService service)
        => service.WriteAnalyseAsync(Guid.NewGuid(), 0.93f, "Low_Hb", OriginalImage, null, CancellationToken.None);

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
