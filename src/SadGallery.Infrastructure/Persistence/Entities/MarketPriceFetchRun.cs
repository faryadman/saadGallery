namespace SadGallery.Infrastructure.Persistence.Entities;

/// <summary>
/// رکورد هر اجرای دریافت نرخ (موفق یا ناموفق). برای پایش سلامت منبع و بررسی تاریخی.
/// هیچ داده حساسی (اعتبارنامه/آدرس کامل) در این جدول ذخیره نمی‌شود.
/// </summary>
public class MarketPriceFetchRun
{
    /// <summary>شناسه اجرا.</summary>
    public long Id { get; set; }

    /// <summary>شناسه منبع.</summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>انگیزه اجرا: Startup | Scheduled | Manual.</summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>شروع (UTC).</summary>
    public DateTimeOffset StartedAtUtc { get; set; }

    /// <summary>پایان (UTC)؛ تهی = اجرای نیمه‌تمام (مثلاً کرش).</summary>
    public DateTimeOffset? FinishedAtUtc { get; set; }

    /// <summary>مدت اجرا (میلی‌ثانیه).</summary>
    public int DurationMs { get; set; }

    /// <summary>نتیجه (RateFetchOutcomeKind).</summary>
    public int Outcome { get; set; }

    /// <summary>کد وضعیت HTTP (در صورت وجود).</summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>تعداد نرخ‌های قابل انتشار.</summary>
    public int AcceptedCount { get; set; }

    /// <summary>تعداد نرخ‌های علامت‌دار (فقط ثبت).</summary>
    public int FlaggedCount { get; set; }

    /// <summary>تعداد اقلام رد‌شده.</summary>
    public int RejectedCount { get; set; }

    /// <summary>کد خطای کوتاه (بدون داده حساس).</summary>
    public string? ErrorCode { get; set; }

    /// <summary>یادداشت (مثلاً فهرست کلیدهای ناشناخته پاسخ).</summary>
    public string? Notes { get; set; }
}
