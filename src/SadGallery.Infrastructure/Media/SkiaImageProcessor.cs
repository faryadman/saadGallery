using Microsoft.Extensions.Logging;
using SadGallery.Application.Abstractions;
using SkiaSharp;

namespace SadGallery.Infrastructure.Media;

/// <summary>
/// رمزگشایی و پردازش تصویر با SkiaSharp (مجوز MIT).
/// </summary>
/// <remarks>
/// <para>
/// چرا رمزگشاییِ واقعی و نه بررسیِ پسوند؟ چون پسوند و <c>Content-Type</c> را کلاینت تعیین
/// می‌کند و هر دو جعل‌پذیرند. اینجا فایل واقعاً توسط یک کتابخانهٔ استاندارد باز می‌شود؛
/// اگر ساختارِ درونیِ آن تصویر نباشد، <c>SKCodec</c> آن را نمی‌پذیرد (مستند در ADR-0014).
/// </para>
/// <para>
/// رفتارِ تأییدشده با نمونه‌های واقعی (مستند در ADR-0014 §۶):
/// فایل اجرایی (MZ/ELF)، متن و HTML، SVG، و فایل‌هایی که فقط امضای JPEG/PNG را جعل کرده‌اند،
/// همگی رد می‌شوند. یک «چندریخت» (تصویر معتبر + فایل اجراییِ چسبیده) پذیرفته می‌شود،
/// اما چون خروجی دوباره رمزگذاری می‌شود، دادهٔ اضافه در آن باقی نمی‌ماند.
/// </para>
/// <para>
/// محدودیتِ صادقانه: تصویری که «نیمه بریده» شده (مثلاً JPEG ناقص) ممکن است پذیرفته شود،
/// چون بخشی از آن قابل رمزگشایی است. این فایل خطری ندارد، اما می‌تواند تصویری ناقص
/// نمایش دهد (ثبت‌شده در docs/KNOWN_LIMITATIONS.md).
/// </para>
/// </remarks>
public sealed class SkiaImageProcessor : IImageProcessor
{
    private readonly ILogger<SkiaImageProcessor>? _logger;

    public SkiaImageProcessor(ILogger<SkiaImageProcessor>? logger = null) => _logger = logger;

    public ImageInspection? Inspect(ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty)
        {
            return null;
        }

        try
        {
            var buffer = content.ToArray();

            using var stream = new MemoryStream(buffer, writable: false);
            using var codec = SKCodec.Create(stream);

            if (codec is null)
            {
                _logger?.LogInformation(
                    "یک فایل به‌عنوان تصویر پذیرفته نشد: کدک نتوانست آن را باز کند ({Length} بایت).",
                    buffer.Length);

                return null;
            }

            // فهرستِ مجاز به‌عمد کوتاه است: JPEG، PNG و WebP.
            // قالب‌های دیگر (GIF/BMP/ICO/HEIF/AVIF/...) پذیرفته نمی‌شوند؛
            // به‌ویژه SVG که می‌تواند حاوی اسکریپت باشد و در اصل یک سند متنی است.
            if (MapToAllowedFormat(codec.EncodedFormat) is not { } format)
            {
                _logger?.LogInformation(
                    "یک فایل به‌دلیل قالبِ غیرمجاز رد شد: {Format} ({Length} بایت).",
                    codec.EncodedFormat, buffer.Length);

                return null;
            }

            var width = codec.Info.Width;
            var height = codec.Info.Height;

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            return new ImageInspection(format, width, height);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // ورودیِ نامعتبر می‌تواند استثناهای گوناگونی از کتابخانه بیرون بدهد؛
            // در مرزِ امنیتی، هر خطای غیرمنتظره به معنای «رد فایل» است.
            _logger?.LogInformation(exception, "بررسی تصویر با خطا مواجه شد؛ فایل رد شد.");

            return null;
        }
    }

    public ProcessedImage? Process(ReadOnlySpan<byte> content, ImageProcessingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (content.IsEmpty)
        {
            return null;
        }

        try
        {
            var buffer = content.ToArray();

            using var stream = new MemoryStream(buffer, writable: false);
            using var codec = SKCodec.Create(stream);

            if (codec is null || MapToAllowedFormat(codec.EncodedFormat) is null)
            {
                return null;
            }

            // رمزگشاییِ کامل: تنها در این صورت است که می‌فهمیم کل فایل واقعاً تصویر است.
            using var source = SKBitmap.Decode(codec);

            if (source is null || source.Width <= 0 || source.Height <= 0)
            {
                return null;
            }

            var outputFormat = MapToSkia(settings.OutputFormat);

            // تغییر اندازه همیشه انجام می‌شود (حتی اگر تصویر کوچک‌تر از حدِ مجاز باشد)،
            // چون هدف فقط کوچک‌کردن نیست: خروجی باید «تازه نوشته‌شده» باشد تا هر
            // دادهٔ پنهان و ابردادهٔ ناخواستهٔ ورودی در آن باقی نماند.
            using var full = ScaleDown(source, settings.StoredMaxSide);
            using var thumb = ScaleDown(source, settings.ThumbnailSide);

            if (full is null || thumb is null)
            {
                return null;
            }

            var fullBytes = Encode(full, outputFormat, settings.Quality);
            var thumbBytes = Encode(thumb, outputFormat, settings.Quality);

            if (fullBytes is null || thumbBytes is null)
            {
                return null;
            }

            return new ProcessedImage(
                Format: settings.OutputFormat,
                Full: fullBytes,
                FullWidth: full.Width,
                FullHeight: full.Height,
                Thumbnail: thumbBytes,
                ThumbWidth: thumb.Width,
                ThumbHeight: thumb.Height);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _logger?.LogWarning(exception, "پردازش تصویر ناموفق بود؛ هیچ فایلی ذخیره نمی‌شود.");

            return null;
        }
    }

    /// <summary>
    /// کوچک‌کردنِ تصویر در صورت نیاز. تصویر هرگز بزرگ‌تر از اندازهٔ اصلی نمی‌شود (مقیاس حداکثر ۱).
    /// </summary>
    private static SKBitmap? ScaleDown(SKBitmap source, int maxSide)
    {
        var scale = Math.Min(1d, Math.Min((double)maxSide / source.Width, (double)maxSide / source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        return source.Resize(new SKSizeI(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
    }

    private static byte[]? Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);

        if (image is null)
        {
            return null;
        }

        using var data = image.Encode(format, quality);

        return data?.ToArray();
    }

    private static ImageFormat? MapToAllowedFormat(SKEncodedImageFormat format) => format switch
    {
        SKEncodedImageFormat.Jpeg => ImageFormat.Jpeg,
        SKEncodedImageFormat.Png => ImageFormat.Png,
        SKEncodedImageFormat.Webp => ImageFormat.WebP,
        _ => null,
    };

    private static SKEncodedImageFormat MapToSkia(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
        ImageFormat.Png => SKEncodedImageFormat.Png,
        ImageFormat.WebP => SKEncodedImageFormat.Webp,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image format."),
    };
}
