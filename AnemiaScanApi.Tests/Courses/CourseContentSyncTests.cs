using AnemiaScanApi.Infrastructure.Courses;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Settings;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AnemiaScanApi.Tests.Courses;

/// <summary>
/// Синхронизация папки с курсами. Запускается при каждом старте сервиса, поэтому
/// главное требование — один плохой файл не мешает остальным и не роняет запуск.
/// </summary>
public sealed class CourseContentSyncTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anemiascan-courses-" + Guid.NewGuid().ToString("N"));
    private readonly Mock<ICourseImportService> _import = new();
    private readonly List<(string Slug, string Hash)> _imported = [];

    public CourseContentSyncTests()
    {
        Directory.CreateDirectory(_folder);

        _import.Setup(s => s.ImportAsync(It.IsAny<CourseDocument>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CourseDocument doc, string hash, CancellationToken _) =>
            {
                _imported.Add((doc.Slug!, hash));
                return new CourseImportResult(CourseImportOutcome.Created, Guid.NewGuid(), []);
            });
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private CourseContentSync NewSync() => new(_import.Object, NullLogger<CourseContentSync>.Instance);

    private void WriteCourse(string fileName, string slug)
        => File.WriteAllText(Path.Combine(_folder, fileName), CourseDocumentParserTests.ValidCourse(slug).ToJsonString());

    [Fact]
    public async Task ImportsEveryValidFile()
    {
        WriteCourse("iron-basics.json", "iron-basics");
        WriteCourse("sleep-and-energy.json", "sleep-and-energy");

        var report = await NewSync().SyncAsync(_folder);

        report.Count(CourseFileStatus.Created).Should().Be(2);
        report.HasFailures.Should().BeFalse();
        _imported.Select(i => i.Slug).Should().Equal("iron-basics", "sleep-and-energy");
    }

    [Fact]
    public async Task InvalidFile_IsReported_AndDoesNotBlockOthers()
    {
        WriteCourse("iron-basics.json", "iron-basics");
        File.WriteAllText(Path.Combine(_folder, "broken.json"), "{ \"slug\": \"broken\" ");

        var report = await NewSync().SyncAsync(_folder);

        report.Count(CourseFileStatus.Created).Should().Be(1);
        var broken = report.Files.Single(f => f.Status == CourseFileStatus.Invalid);
        broken.File.Should().Be("broken.json");
        broken.Messages.Should().NotBeEmpty();
        // Плохой файл чинит автор, повтор тут не поможет.
        report.HasFailures.Should().BeFalse();
    }

    [Fact]
    public async Task FileName_MustMatchSlug()
    {
        WriteCourse("my-course.json", "iron-basics");

        var report = await NewSync().SyncAsync(_folder);

        report.Files.Single().Status.Should().Be(CourseFileStatus.Invalid);
        report.Files.Single().Messages.Single().Should().Contain("iron-basics.json");
        _imported.Should().BeEmpty();
    }

    [Fact]
    public async Task DatabaseFailure_IsReportedAsFailed_AndOtherFilesStillImport()
    {
        WriteCourse("a-course.json", "a-course");
        WriteCourse("b-course.json", "b-course");
        _import.Setup(s => s.ImportAsync(It.Is<CourseDocument>(d => d.Slug == "a-course"), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("mongo недоступна"));

        var report = await NewSync().SyncAsync(_folder);

        report.Files.Single(f => f.File == "a-course.json").Status.Should().Be(CourseFileStatus.Failed);
        report.Files.Single(f => f.File == "b-course.json").Status.Should().Be(CourseFileStatus.Created);
        report.HasFailures.Should().BeTrue("сбой базы чинится повтором — его и запускает фоновый сервис");
    }

    [Fact]
    public async Task Fingerprint_ChangesOnlyWhenFileChanges()
    {
        WriteCourse("iron-basics.json", "iron-basics");

        await NewSync().SyncAsync(_folder);
        await NewSync().SyncAsync(_folder);
        _imported[0].Hash.Should().Be(_imported[1].Hash);

        var path = Path.Combine(_folder, "iron-basics.json");
        var original = File.ReadAllText(path);
        var edited = original.Replace("\"Adult\"", "\"Pregnant\"");
        edited.Should().NotBe(original, "тест должен действительно изменить файл");
        File.WriteAllText(path, edited);
        await NewSync().SyncAsync(_folder);

        _imported[2].Hash.Should().NotBe(_imported[0].Hash);
    }

    [Fact]
    public async Task NonJsonFilesAndSubfolders_AreIgnored()
    {
        WriteCourse("iron-basics.json", "iron-basics");
        File.WriteAllText(Path.Combine(_folder, "README.md"), "# Курсы");
        Directory.CreateDirectory(Path.Combine(_folder, "drafts"));
        File.WriteAllText(Path.Combine(_folder, "drafts", "wip.json"), "не json");

        var report = await NewSync().SyncAsync(_folder);

        report.Files.Should().ContainSingle().Which.File.Should().Be("iron-basics.json");
    }

    [Fact]
    public async Task EmptyFolder_IsNotAnError()
    {
        var report = await NewSync().SyncAsync(_folder);

        report.Files.Should().BeEmpty();
        report.HasFailures.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyConfiguredPath_FallsBackToDefault(string? configured)
    {
        // Незаданная переменная окружения приезжает пустой строкой и перетирает
        // значение из appsettings — без отката курсы молча перестали бы грузиться.
        CourseContentSyncHostedService.ResolveFolder(configured)
            .Should().Be(Path.Combine(AppContext.BaseDirectory, CourseContentSettings.DefaultPath));
    }

    [Fact]
    public void RelativePath_IsResolvedFromApplicationFolder_NotCurrentDirectory()
    {
        CourseContentSyncHostedService.ResolveFolder("Content/Other")
            .Should().Be(Path.Combine(AppContext.BaseDirectory, "Content/Other"));
    }
}

/// <summary>
/// Файлы курсов, которые лежат в репозитории и уедут на сервер. Тест ловит
/// ошибку в контенте до деплоя: на сервере плохой файл просто не загрузится, и
/// узнать об этом можно будет только из лога.
/// </summary>
public class ShippedCourseContentTests
{
    private static readonly string Folder =
        Path.Combine(AppContext.BaseDirectory, CourseContentSettings.DefaultPath);

    public static TheoryData<string> CourseFiles()
    {
        var data = new TheoryData<string>();
        if (Directory.Exists(Folder))
            foreach (var path in Directory.EnumerateFiles(Folder, "*.json").OrderBy(p => p, StringComparer.Ordinal))
                data.Add(Path.GetFileName(path));
        return data;
    }

    [Fact]
    public void ContentFolder_IsCopiedToBuildOutput()
    {
        // Если папка не доехала до сборки, она не доедет и до сервера — и
        // каталог курсов останется пустым без единой ошибки.
        Directory.Exists(Folder).Should().BeTrue($"ожидается папка {Folder}");
        CourseFiles().Should().NotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(CourseFiles))]
    public void CourseFile_IsValid_AndNamedAfterSlug(string fileName)
    {
        var result = CourseDocumentParser.Parse(File.ReadAllText(Path.Combine(Folder, fileName)));

        result.Errors.Should().BeEmpty($"файл {fileName} должен проходить проверку импортёра");
        fileName.Should().Be(result.Document!.Slug + ".json");
    }
}
