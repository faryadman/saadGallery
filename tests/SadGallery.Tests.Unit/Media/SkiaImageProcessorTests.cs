using SadGallery.Application.Abstractions;
using SadGallery.Infrastructure.Media;
using SkiaSharp;
using Xunit;

namespace SadGallery.Tests.Unit.Media;

/// <summary>
/// تست‌های پردازشگر واقعیِ تصویر با <b>نمونه‌های مسموم</b> (فاز ۴).
/// </summary>
/// <remarks>
/// این دقیقاً همان معیارِ پذیرش است: «رد فایل غیرتصویری که پسوند/نوع آن جعل شده،
/// با بررسی واقعی ساختار تصویر — تست با نمونه‌های مسموم».
/// <para>
/// برای ساختِ تصویرهای واقعی از همان کتابخانه استفاده شده؛ سپس نمونه‌های مخرب
/// (اجراییِ ویندوز/لینوکس، HTML، SVG، امضای جعلی، فایلِ نیمه‌بریده و «چندریخت»)
/// به‌عنوان ورودیِ مهاجم به سامانه داده می‌شوند.
/// </para>
/// </remarks>
public sealed class SkiaImageProcessorTests
{
    private readonly SkiaImageProcessor _processor = new();

    private static byte[] RealImage(SKEncodedImageFormat format, int width = 800, int height = 600)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Goldenrod);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);

        return data!.ToArray();
    }

    /// <summary>
    /// دادهٔ تصادفی برای ساختِ نمونهٔ مسموم. از تولیدکنندهٔ رمزنگاری استفاده می‌شود
    /// (قاعدهٔ پروژه: هر جا تصادفی لازم است، نه Random ساده).
    /// </summary>
    private static byte[] RandomBytes(int length, params byte[] header)
    {
        var bytes = new byte[length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

        for (var i = 0; i < header.Length && i < length; i++)
        {
            bytes[i] = header[i];
        }

        return bytes;
    }

    // ─────────── پذیرشِ تصویرهای واقعی ───────────

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, ImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png, ImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Webp, ImageFormat.WebP)]
    public void Inspect_AcceptsRealImages(SKEncodedImageFormat sourceFormat, ImageFormat expected)
    {
        var inspection = _processor.Inspect(RealImage(sourceFormat, 640, 480));

        Assert.NotNull(inspection);
        Assert.Equal(expected, inspection!.Format);
        Assert.Equal(640, inspection.Width);
        Assert.Equal(480, inspection.Height);
    }

    // ─────────── ردِ نمونه‌های مسموم ───────────

    [Fact]
    public void Inspect_RejectsWindowsExecutableNamedAsJpeg()
    {
        // MZ = سرآغازِ واقعیِ فایل اجراییِ ویندوز
        var inspection = _processor.Inspect(RandomBytes(8192, 0x4D, 0x5A));

        Assert.Null(inspection);
    }

    [Fact]
    public void Inspect_RejectsLinuxExecutableNamedAsPng()
    {
        var inspection = _processor.Inspect(RandomBytes(8192, 0x7F, (byte)'E', (byte)'L', (byte)'F'));

        Assert.Null(inspection);
    }

    [Fact]
    public void Inspect_RejectsHtmlOrScript()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("<script>alert('xss')</script>");

        Assert.Null(_processor.Inspect(payload));
    }

    [Fact]
    public void Inspect_RejectsSvg_WhichIsAScriptCarrierNotARaster()
    {
        var svg = System.Text.Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"/>");

        Assert.Null(_processor.Inspect(svg));
    }

    [Fact]
    public void Inspect_RejectsEmptyFile()
    {
        Assert.Null(_processor.Inspect([]));
    }

    [Fact]
    public void Inspect_RejectsPlainText()
    {
        Assert.Null(_processor.Inspect(System.Text.Encoding.UTF8.GetBytes("این یک فایل متنی است")));
    }

    [Fact]
    public void Inspect_RejectsFakeJpegSignature()
    {
        // فقط امضای JPEG جعل شده؛ بقیه زباله است.
        Assert.Null(_processor.Inspect(RandomBytes(4096, 0xFF, 0xD8, 0xFF, 0xE0)));
    }

    [Fact]
    public void Inspect_RejectsFakePngSignature()
    {
        Assert.Null(_processor.Inspect(RandomBytes(4096, 0x89, 0x50, 0x4E, 0x47)));
    }

    [Fact]
    public void Inspect_RejectsFakeWebPSignature()
    {
        // "RIFF" + "WEBP"
        var fake = System.Text.Encoding.ASCII.GetBytes("RIFF");
        var bytes = RandomBytes(4096, fake);
        System.Text.Encoding.ASCII.GetBytes("WEBP").CopyTo(bytes, 8);

        Assert.Null(_processor.Inspect(bytes));
    }

    [Fact]
    public void Inspect_RejectsNullBytesAndControlData()
    {
        Assert.Null(_processor.Inspect(new byte[1024]));
    }

    // ─────────── بازرمزگذاری: پاک‌سازیِ دادهٔ پنهان ───────────

    [Fact]
    public void Process_StripsAppendedPayload_FromPolyglotImage()
    {
        // یک تصویر معتبر که یک فایل اجرایی به انتهای آن چسبیده است.
        // رمزگشایی موفق می‌شود (چون خودِ تصویر معتبر است) اما خروجیِ بازرمزگذاری‌شده
        // فقط پیکسل‌هاست؛ بنابراین دادهٔ اضافه باید در خروجی نباشد.
        var image = RealImage(SKEncodedImageFormat.Jpeg, 400, 300);
        var executable = RandomBytes(8192, 0x4D, 0x5A);
        var polyglot = image.Concat(executable).ToArray();

        var processed = _processor.Process(polyglot, Settings());

        Assert.NotNull(processed);
        Assert.DoesNotContain(executable, processed!.Full);
        Assert.DoesNotContain(executable, processed.Thumbnail);
    }

    [Fact]
    public void Process_RejectsMaliciousInput_EvenIfInspectionWouldPass()
    {
        Assert.Null(_processor.Process(RandomBytes(4096, 0xFF, 0xD8, 0xFF, 0xE0), Settings()));
    }

    [Fact]
    public void Process_CreatesSmallerThumbnailWithinRequestedSide()
    {
        var processed = _processor.Process(RealImage(SKEncodedImageFormat.Jpeg, 2000, 1000), Settings(thumbnailSide: 300));

        Assert.NotNull(processed);
        Assert.True(processed!.ThumbWidth <= 300);
        Assert.True(processed.ThumbHeight <= 300);
        // نسبتِ تصویر حفظ می‌شود: ضلعِ بلند دقیقاً ۳۰۰ می‌شود (۲۰۰۰×۱۰۰۰ ⇒ مقیاس ۰٫۱۵)
        Assert.Equal(300, processed.ThumbWidth);
        Assert.Equal(150, processed.ThumbHeight);
        Assert.True(processed.Thumbnail.Length < processed.Full.Length);
    }

    [Fact]
    public void Process_NeverUpscalesSmallImages()
    {
        var processed = _processor.Process(RealImage(SKEncodedImageFormat.Png, 100, 50), Settings(storedMaxSide: 1600));

        Assert.NotNull(processed);
        Assert.Equal(100, processed!.FullWidth);
        Assert.Equal(50, processed.FullHeight);
    }

    [Fact]
    public void Process_ShrinksLargeImagesToStoredMaxSide()
    {
        var processed = _processor.Process(RealImage(SKEncodedImageFormat.Jpeg, 4000, 2000), Settings(storedMaxSide: 1000));

        Assert.NotNull(processed);
        Assert.Equal(1000, processed!.FullWidth);
        Assert.Equal(500, processed.FullHeight);
    }

    [Fact]
    public void Process_RejectsEmptyInput()
    {
        Assert.Null(_processor.Process([], Settings()));
    }

    private static ImageProcessingSettings Settings(
        int storedMaxSide = 1600,
        int thumbnailSide = 480,
        int quality = 82,
        ImageFormat output = ImageFormat.WebP) =>
        new(storedMaxSide, thumbnailSide, quality, output);
}
