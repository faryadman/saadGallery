using SadGallery.Application.Abstractions;
using SadGallery.Domain.Enums;

namespace SadGallery.Application.Media;

/// <summary>نتیجهٔ ذخیره‌سازی یک تصویرِ پردازش‌شده.</summary>
public sealed record StoredImageResult(
    string StoredFileName,
    string ThumbnailFileName,
    ImageFormat Format,
    int Width,
    int Height,
    int ThumbWidth,
    int ThumbHeight,
    long SizeBytes);

/// <summary>خروجی عملیات بارگذاری (موفق یا همراه با پیام فارسی).</summary>
public sealed record MediaUploadOutcome(bool Succeeded, string? Error, StoredImageResult? Stored)
{
    public static MediaUploadOutcome Fail(string error) => new(false, error, null);

    public static MediaUploadOutcome Ok(StoredImageResult stored) => new(true, null, stored);
}

/// <summary>
/// خطِ لولهٔ بارگذاری تصویر: اعتبارسنجی ← رمزگشایی/بازرمزگذاری ← ذخیره با نام تصادفی.
/// </summary>
/// <remarks>
/// <para>
/// ترتیبِ مراحل امنیتی و دلیل هر کدام (ADR-0014):
/// </para>
/// <list type="number">
///   <item><b>اعتبارسنجی</b>: ردِ فایل‌هایی که فقط «ادعا» می‌کنند تصویر هستند.</item>
///   <item><b>بازرمزگذاری</b>: خروجی فقط شامل پیکسل‌هاست، نه بایت‌های ورودی؛ بنابراین هر
///   دادهٔ پنهانِ چسبیده به تصویر حذف می‌شود و ابرداده‌های ناخواسته هم منتقل نمی‌گردد.</item>
///   <item><b>نام تصادفی</b>: نامِ کاربر هرگز استفاده نمی‌شود؛ پس نه مسیر قابل تزریق است،
///   نه فایلِ دیگری بازنویسی می‌شود و نه نام‌ها قابل حدس/شمردن هستند.</item>
/// </list>
/// </remarks>
public sealed class MediaUploadService
{
    private readonly IImageProcessor _processor;
    private readonly IMediaStore _store;
    private readonly MediaOptions _options;
    private readonly ImageUploadValidator _validator;

    public MediaUploadService(
        IImageProcessor processor,
        IMediaStore store,
        MediaOptions options)
    {
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);

        _processor = processor;
        _store = store;
        _options = options;
        _validator = new ImageUploadValidator(processor, options);
    }

    /// <summary>
    /// بارگذاری و ذخیرهٔ یک تصویر عمومیِ محصول.
    /// </summary>
    public async Task<MediaUploadOutcome> SaveProductImageAsync(
        UploadCandidate candidate,
        CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(candidate);

        if (!validation.IsAccepted)
        {
            return MediaUploadOutcome.Fail(validation.Reason ?? "فایل پذیرفته نشد.");
        }

        var settings = new ImageProcessingSettings(
            StoredMaxSide: _options.StoredMaxSide,
            ThumbnailSide: _options.ThumbnailSide,
            Quality: _options.Quality,
            OutputFormat: _options.OutputFormat);

        var processed = _processor.Process(candidate.Content.Span, settings);

        if (processed is null)
        {
            // اعتبارسنجی پذیرفته بود اما پردازش شکست خورد: یعنی در مرحلهٔ رمزگشاییِ کامل
            // مشکلی پیدا شد. هیچ فایلی ذخیره نمی‌شود.
            return MediaUploadOutcome.Fail("پردازش تصویر ناموفق بود؛ فایل ذخیره نشد.");
        }

        // نام‌ها را سامانه تولید می‌کند (تصادفیِ رمزنگاری) و مخزن هم دوباره شکلِ آن‌ها را می‌سنجد.
        var storedName = MediaFileNaming.NewStoredName(processed.Format);
        var thumbName = MediaFileNaming.NewStoredName(processed.Format);

        await _store.SaveAsync(MediaKind.PublicImage, storedName, processed.Full, cancellationToken);
        await _store.SaveAsync(MediaKind.PublicImage, thumbName, processed.Thumbnail, cancellationToken);

        return MediaUploadOutcome.Ok(new StoredImageResult(
            StoredFileName: storedName,
            ThumbnailFileName: thumbName,
            Format: processed.Format,
            Width: processed.FullWidth,
            Height: processed.FullHeight,
            ThumbWidth: processed.ThumbWidth,
            ThumbHeight: processed.ThumbHeight,
            SizeBytes: processed.Full.Length));
    }
}
