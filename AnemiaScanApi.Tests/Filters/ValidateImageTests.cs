using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Controllers;
using AnemiaScanApi.Filters;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace AnemiaScanApi.Tests.Filters;

public class ValidateImageTests
{
    private static async Task<(ActionExecutingContext Context, bool Called)> ValidateAsync(IFormFile? file)
    {
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var filters = new List<IFilterMetadata>();
        var controller = new object();
        var context = new ActionExecutingContext(action, filters,
            new Dictionary<string, object?> { ["request"] = new PredictionRequest { ImageData = file! } }, controller);
        var called = false;
        await new ValidateImageAttribute().OnActionExecutionAsync(context, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(action, filters, controller));
        });
        return (context, called);
    }

    private static byte[] ImageBytes(IImageEncoder encoder, int width = 8, int height = 8, bool animated = false)
    {
        using var image = new Image<Rgba32>(width, height);
        if (animated) image.Frames.AddFrame(image.Frames.RootFrame);
        using var stream = new MemoryStream();
        image.Save(stream, encoder);
        return stream.ToArray();
    }

    private static IFormFile Upload(byte[] bytes, string name = "eye.png", string mime = "image/png")
        => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "ImageData", name) { Headers = new HeaderDictionary(), ContentType = mime };

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpg", "image/jpeg")]
    [InlineData("jpeg", "image/jpg")]
    [InlineData("bmp", "image/bmp")]
    [InlineData("gif", "image/gif")]
    [InlineData("webp", "image/webp")]
    public async Task ValidStaticImage_ReachesPrediction(string extension, string mime)
    {
        IImageEncoder encoder = extension switch
        {
            "png" => new PngEncoder(), "jpg" or "jpeg" => new JpegEncoder(),
            "bmp" => new BmpEncoder(), "gif" => new GifEncoder(), _ => new WebpEncoder()
        };
        var (context, called) = await ValidateAsync(Upload(ImageBytes(encoder), $"eye.{extension}", mime));
        called.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Theory]
    [InlineData("eye.jpg", "image/jpeg")]
    [InlineData("eye.png", "image/jpeg")]
    [InlineData("eye.exe", "image/png")]
    public async Task MismatchedOrUnsupportedFormat_NeverReachesPrediction(string name, string mime)
    {
        var (context, called) = await ValidateAsync(Upload(ImageBytes(new PngEncoder()), name, mime));
        called.Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(60)]
    public async Task EmptyOrTruncatedFile_NeverReachesPrediction(int length)
    {
        var (context, called) = await ValidateAsync(Upload(ImageBytes(new PngEncoder())[..length]));
        called.Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task MissingFile_IsRejected()
    {
        var (context, called) = await ValidateAsync(null);
        called.Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task OversizedFile_IsRejectedWithoutReadingIt()
    {
        var file = new Mock<IFormFile>(MockBehavior.Strict);
        file.SetupGet(f => f.Length).Returns(ValidateImageAttribute.MaxFileBytes + 1);
        var (context, called) = await ValidateAsync(file.Object);
        called.Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(413);
        file.Verify(f => f.OpenReadStream(), Times.Never);
    }

    [Theory]
    [InlineData(8193, 1)]
    [InlineData(4001, 4000)]
    public async Task ExcessiveDimensions_AreRejected(int width, int height)
    {
        var (context, called) = await ValidateAsync(Upload(ImageBytes(new PngEncoder(), width, height)));
        called.Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task AnimatedImage_IsRejected()
    {
        var (context, called) = await ValidateAsync(Upload(ImageBytes(new GifEncoder(), animated: true), "eye.gif", "image/gif"));
        called.Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Theory]
    [InlineData(nameof(AnalysisController.PredictAnemia))]
    [InlineData(nameof(AnalysisController.SchedulePredictAnemia))]
    public void BothRoutes_ValidateBeforePrediction_AndLimitRequestBody(string action)
    {
        var attributes = typeof(AnalysisController).GetMethod(action)!.GetCustomAttributes(true);
        attributes.Should().Contain(a => a is ValidateImageAttribute);
        typeof(AnalysisController).GetMethod(action)!.GetCustomAttributesData()
            .Single(a => a.AttributeType == typeof(RequestSizeLimitAttribute))
            .ConstructorArguments[0].Value.Should().Be(ValidateImageAttribute.MaxRequestBytes);
        attributes.OfType<RequestFormLimitsAttribute>().Single().MultipartBodyLengthLimit.Should().Be(ValidateImageAttribute.MaxFileBytes);
    }
}
