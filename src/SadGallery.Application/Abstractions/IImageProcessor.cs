namespace SadGallery.Application.Abstractions;

/// <summary>
/// قالب‌های تصویریِ مجاز برای بارگذاری.
/// </summary>
/// <remarks>
/// فهرست مجاز به‌عمد کوتاه است. <b>SVG عمداً حذف شده</b>: یک فایل SVG در واقع سند XML است و
/// می‌تواند حاوی اسکریپت باشد؛ نمایش آن از دامنهٔ سایت، خطر XSS دارد. تصویرهایی که
/// کاربر بارگذاری می‌کند باید «نقشهٔ بیتی» (Raster) باشند تا بتوان آن‌ها را دوباره
/// رمزگذاری کرد (توضیح کامل در ADR-0014).
/// </remarks>
public enum ImageFormat
{
    Jpeg = 1,
    Png = 2,
    WebP = 3,
}

/// <summary>نتیجه بازرسی یک تصویر (بدون تغییر اندازه).</summary>
public sealed record ImageInspection(ImageFormat Format, int Width, int Height);

/// <summary>تنظیمات پردازش تصویر (اندازه‌ها و کیفیت).</summary>
public sealed record ImageProcessingSettings(int StoredMaxSide, int ThumbnailSide, int Quality, ImageFormat OutputFormat);

/// <summary>تصویرِ پردازش‌شده: نسخهٔ اصلیِ بهینه‌شده + بندانگشتی.</summary>
public sealed record ProcessedImage(
    ImageFormat Format,
    byte[] Full,
    int FullWidth,
    int FullHeight,
    byte[] Thumbnail,
    int ThumbWidth,
    int ThumbHeight);

/// <summary>
/// رمزگشا/پردازشگر تصویر. پیاده‌سازی واقعی در لایه Infrastructure با یک کتابخانهٔ
/// استاندارد انجام می‌شود (SkiaSharp)؛ این واسط وجود دارد چون:
/// (۱) لایه Application نباید به وابستگی بومی (Native) وابسته باشد،
/// (۲) منطق اعتبارسنجی باید بدون کتابخانه واقعی هم قابل تست باشد.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// بررسی <b>ساختار واقعی</b> تصویر. تنها بررسی پسوند یا Content-Type کافی نیست،
    /// چون هر دو به‌سادگی جعل می‌شوند؛ این متد فایل را واقعاً رمزگشایی می‌کند.
    /// </summary>
    /// <returns>اطلاعات تصویر در صورت معتبر بودن، وگرنه <c>null</c>.</returns>
    ImageInspection? Inspect(ReadOnlySpan<byte> content);

    /// <summary>
    /// رمزگشایی، تغییر اندازه و <b>بازرمزگذاری</b> تصویر.
    /// </summary>
    /// <remarks>
    /// بازرمزگذاری فقط برای بهینه‌سازی حجم نیست؛ یک کنترل امنیتی است:
    /// هر دادهٔ پنهانِ چسبیده به تصویر (مانند فایل اجرایی در یک فایل «چندریخت»/Polyglot)
    /// در خروجی حذف می‌شود، چون فقط پیکسل‌ها دوباره نوشته می‌شوند و نه بایت‌های ورودی.
    /// </remarks>
    ProcessedImage? Process(ReadOnlySpan<byte> content, ImageProcessingSettings settings);
}
