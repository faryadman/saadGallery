using SadGallery.Application.Abstractions;
using SadGallery.Application.Media;
using Xunit;

namespace SadGallery.Tests.Unit.Media;

/// <summary>
/// تست‌های اعتبارسنجِ آپلود (فاز ۴) با پردازشگرِ ساختگی.
/// </summary>
/// <remarks>
/// این تست‌ها منطقِ تصمیم را می‌سنجند (محدودیت‌ها، نقشِ «راهنما» بودنِ پسوند،
/// بی‌اثر بودنِ Content-Type) بدون نیاز به کتابخانهٔ واقعیِ تصویر.
/// تستِ نمونه‌های واقعی و مسموم در <c>SkiaImageProcessorTests</c> است.
/// </remarks>
public sealed class ImageUploadValidatorTests
{
    /// <summary>پردازشگرِ ساختگی: فقط می‌گوید «تصویر هست یا نه».</summary>
    private sealed class FakeProcessor : IImageProcessor
    {
        private readonly ImageInspection? _inspection;

        public FakeProcessor(ImageInspection? inspection) => _inspection = inspection;

        public ImageInspection? Inspect(ReadOnlySpan<byte> content) => _inspection;

        public ProcessedImage? Process(ReadOnlySpan<byte> content, ImageProcessingSettings settings) => null;
    }

    private static MediaOptions Options(
        int maxUploadBytes = 1024,
        int maxWidth = 8000,
        int maxHeight = 8000) =>
        new()
        {
            MaxUploadBytes = maxUploadBytes,
            MaxWidth = maxWidth,
            MaxHeight = maxHeight,
        };

    private static UploadCandidate Candidate(
        byte[] content,
        string fileName = "photo.jpg",
        string? contentType = "image/jpeg") =>
        new(fileName, contentType, content.Length, content);

    private static ImageUploadValidator Validator(ImageInspection? inspection, MediaOptions? options = null) =>
        new(new FakeProcessor(inspection), options ?? Options());

    [Fact]
    public void Accepts_WhenProcessorConfirmsRealImageStructure()
    {
        var result = Validator(new ImageInspection(ImageFormat.Jpeg, 800, 600))
            .Validate(Candidate([1, 2, 3, 4]));

        Assert.True(result.IsAccepted);
        Assert.Null(result.Reason);
        Assert.Equal(800, result.Inspection!.Width);
    }

    [Fact]
    public void Rejects_EmptyFile()
    {
        var result = Validator(new ImageInspection(ImageFormat.Jpeg, 800, 600))
            .Validate(Candidate([]));

        Assert.False(result.IsAccepted);
        Assert.Contains("خالی", result.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_FileLargerThanLimit()
    {
        var result = Validator(new ImageInspection(ImageFormat.Jpeg, 800, 600), Options(maxUploadBytes: 10))
            .Validate(Candidate(new byte[11]));

        Assert.False(result.IsAccepted);
        Assert.Contains("حجم", result.Reason!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("page.html")]
    [InlineData("script.svg")]
    [InlineData("archive.zip")]
    public void Rejects_NonImageExtension(string fileName)
    {
        var result = Validator(new ImageInspection(ImageFormat.Jpeg, 800, 600))
            .Validate(Candidate([1, 2, 3], fileName));

        Assert.False(result.IsAccepted);
        Assert.Contains("قالب", result.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_WhenProcessorFindsNoRealImage_SpoofedExtensionAndContentType()
    {
        // سناریوی اصلیِ معیار پذیرش: فایلی که نام و نوعش تصویر است اما ساختارش نیست.
        var result = Validator(inspection: null)
            .Validate(Candidate(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, "photo.jpg", "image/jpeg"));

        Assert.False(result.IsAccepted);
        Assert.Contains("تصویر معتبر نیست", result.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentType_IsAdvisoryOnly_ValidImageWithOddContentTypeIsAccepted()
    {
        // کلاینت‌ها گاهی نوعِ نادرست یا عمومی می‌فرستند؛ تصمیم را ساختارِ واقعی می‌گیرد.
        var result = Validator(new ImageInspection(ImageFormat.Png, 100, 100))
            .Validate(Candidate([1, 2, 3], "photo.png", "application/octet-stream"));

        Assert.True(result.IsAccepted);
    }

    [Fact]
    public void ContentType_NeverOverridesRealStructure()
    {
        // ادعای «image/jpeg» روی محتوای غیرِتصویری بی‌اثر است.
        var result = Validator(inspection: null)
            .Validate(Candidate([0x00, 0x01, 0x02], "photo.jpg", "image/jpeg"));

        Assert.False(result.IsAccepted);
    }

    [Fact]
    public void Rejects_ImagesLargerThanDimensionLimit()
    {
        // سدِ «بمبِ فشرده‌سازی»: ابعاد پیش از تخصیصِ حافظه بررسی می‌شود.
        var result = Validator(new ImageInspection(ImageFormat.Jpeg, 20_000, 20_000), Options())
            .Validate(Candidate([1, 2, 3]));

        Assert.False(result.IsAccepted);
        Assert.Contains("ابعاد", result.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_RejectsOverlappingPublicAndPrivatePaths()
    {
        var options = new MediaOptions { PublicSubPath = "files", PrivateSubPath = "files" };

        Assert.Contains(options.Validate(), error => error.Contains("مسیر عمومی و خصوصی"));
    }

    [Fact]
    public void Options_RejectsInvalidSizes()
    {
        var options = new MediaOptions
        {
            MaxUploadBytes = 0,
            MaxImagesPerProduct = 0,
            Quality = 10,
            StoredMaxSide = 1,
        };

        var errors = options.Validate();

        Assert.True(errors.Count >= 4);
    }
}
