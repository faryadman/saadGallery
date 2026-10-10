namespace SadGallery.Infrastructure.Persistence.Entities;

/// <summary>
/// تصویر یک کالا.
/// </summary>
/// <remarks>
/// <para>
/// <b>نام فایلِ کاربر هرگز ذخیره نمی‌شود.</b> <see cref="StoredFileName"/> را سامانه با
/// تولیدکنندهٔ تصادفیِ رمزنگاری می‌سازد؛ نام اصلی فقط برای ممیزی (و پس از پاک‌سازی)
/// در <see cref="OriginalFileName"/> نگه داشته می‌شود.
/// </para>
/// <para>
/// ممیزیِ بارگذاری (چه کسی، کِی، با چه نام و نوعِ ادعایی) در همین جدول ثبت می‌شود تا
/// بتوان در صورت نیاز منبع یک فایل را ردیابی کرد (معیار پذیرش فاز ۴).
/// </para>
/// </remarks>
public class ProductImage
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>نام فایلِ نسخهٔ کامل (تولیدشده توسط سامانه).</summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>نام فایلِ بندانگشتی.</summary>
    public string ThumbnailFileName { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    /// <summary>حجمِ نسخهٔ ذخیره‌شده پس از بهینه‌سازی (بایت).</summary>
    public long SizeBytes { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    /// <summary>قالبِ تشخیص‌داده‌شده از روی ساختار واقعی (نه پسوند).</summary>
    public string DetectedFormat { get; set; } = string.Empty;

    /// <summary>نام فایل در دستگاهِ بارگذار — فقط برای گزارش، پاک‌سازی‌شده.</summary>
    public string? OriginalFileName { get; set; }

    /// <summary>
    /// نوع محتوایی که کلاینت ادعا کرده بود. فقط برای گزارش ثبت می‌شود؛
    /// هرگز مبنای تصمیم امنیتی نیست (قابل جعل است).
    /// </summary>
    public string? ClaimedContentType { get; set; }

    // ---- ممیزی ----

    public int UploadedByUserId { get; set; }

    public DateTimeOffset UploadedAtUtc { get; set; }
}
