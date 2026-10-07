using SadGallery.Application.Market;

namespace SadGallery.Application.Abstractions;

/// <summary>وضعیت یک تلاش دریافت نرخ از منبع.</summary>
public enum RateFetchStatus
{
    /// <summary>دریافت و تجزیه موفق.</summary>
    Success = 1,

    /// <summary>منبع، اعتبارنامه را نپذیرفت (پاسخ Error: Unauthorized یا ۴۰۱/۴۰۳).</summary>
    Unauthorized = 2,

    /// <summary>خطای HTTP غیر از احراز هویت (مثلاً ۵۰۰ یا ۴۲۹).</summary>
    HttpError = 3,

    /// <summary>اتمام مهلت درخواست.</summary>
    Timeout = 4,

    /// <summary>پاسخ رسید اما قابل تجزیه/اعتبارسنجی نبود.</summary>
    PayloadInvalid = 5,

    /// <summary>خطای شبکه/انتقال (DNS، TLS، قطع اتصال).</summary>
    TransportError = 6,

    /// <summary>منبع تنظیم/فعال نشده است (fail-closed).</summary>
    NotConfigured = 7,

    /// <summary>حجم پاسخ از سقف مجاز بیشتر بود.</summary>
    ResponseTooLarge = 8,

    /// <summary>اجرای هم‌زمان دیگری در جریان بود؛ این نوبت رد شد.</summary>
    SkippedOverlap = 9,

    /// <summary>حداقل فاصله الزامی از درخواست قبلی رعایت نشده بود (احترام به محدودیت منبع).</summary>
    SkippedTooSoon = 10,
}

/// <summary>نتیجه یک فراخوانی منبع نرخ.</summary>
public sealed record RateProviderFetchResult
{
    /// <summary>وضعیت.</summary>
    public required RateFetchStatus Status { get; init; }

    /// <summary>شناسه منبع.</summary>
    public required string ProviderId { get; init; }

    /// <summary>زمان دریافت ما (UTC) — در همه حالت‌ها مقدار دارد.</summary>
    public required DateTimeOffset FetchedAtUtc { get; init; }

    /// <summary>نتیجه تجزیه (در حالت موفق/نامعتبر).</summary>
    public RateParseResult? Payload { get; init; }

    /// <summary>کد وضعیت HTTP (اگر درخواستی ارسال شده باشد).</summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>کد خطای کوتاه و بی‌خطر (بدون داده حساس): Timeout، ResponseTooLarge، Unauthorized…</summary>
    public string? ErrorCode { get; init; }

    /// <summary>توضیح فنی کوتاه — هرگز شامل اعتبارنامه یا آدرس کامل نیست.</summary>
    public string? TechnicalDetail { get; init; }

    /// <summary>مدت درخواست (میلی‌ثانیه).</summary>
    public int DurationMs { get; init; }

    /// <summary>ساخت نتیجه موفق.</summary>
    public static RateProviderFetchResult Ok(string providerId, DateTimeOffset fetchedAtUtc, RateParseResult payload, int durationMs, int? httpStatusCode = null) =>
        new()
        {
            Status = RateFetchStatus.Success,
            ProviderId = providerId,
            FetchedAtUtc = fetchedAtUtc,
            Payload = payload,
            DurationMs = durationMs,
            HttpStatusCode = httpStatusCode,
        };

    /// <summary>ساخت نتیجه ناموفق.</summary>
    public static RateProviderFetchResult Failure(
        string providerId,
        RateFetchStatus status,
        DateTimeOffset fetchedAtUtc,
        string errorCode,
        string? technicalDetail = null,
        int? httpStatusCode = null,
        int durationMs = 0,
        RateParseResult? payload = null) =>
        new()
        {
            Status = status,
            ProviderId = providerId,
            FetchedAtUtc = fetchedAtUtc,
            ErrorCode = errorCode,
            TechnicalDetail = technicalDetail,
            HttpStatusCode = httpStatusCode,
            DurationMs = durationMs,
            Payload = payload,
        };
}

/// <summary>
/// قرارداد منبع نرخ (فاز ۲). پیاده‌سازی‌ها مستقل‌اند و از طریق DI انتخاب می‌شوند:
/// <c>TgnRateProvider</c> (منبع واقعی، HTTP) و <c>FixtureRateProvider</c> (نمونهٔ آزمایشی توسعه).
/// هیچ پیاده‌سازی‌ای نباید در حالت خطا استثنا پرتاب کند، مگر <see cref="OperationCanceledException"/>
/// در پاسخ به لغو واقعی؛ همه شکست‌ها به‌صورت <see cref="RateProviderFetchResult"/> گزارش می‌شوند.
/// </summary>
public interface IRateProvider
{
    /// <summary>شناسه منبع (در همه رکوردها ثبت می‌شود).</summary>
    string ProviderId { get; }

    /// <summary>منبع به‌درستی تنظیم شده است؟ (نبود اعتبارنامه/دامنه مجاز ⇒ false و هیچ درخواستی ارسال نمی‌شود)</summary>
    bool IsConfigured { get; }

    /// <summary>دریافت یک‌بارهٔ نرخ‌ها.</summary>
    Task<RateProviderFetchResult> FetchAsync(CancellationToken cancellationToken);
}
