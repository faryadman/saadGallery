using SadGallery.Application.Abstractions;

namespace SadGallery.Application.Market;

/// <summary>
/// سیاست عقب‌نشینی نمایی پس از خطا (با احترام به محدودیت منبع).
/// پیاپی‌بودن خطاها (Timeout، ۵۰۰، ۴۲۹، JSON نامعتبر، Unauthorized) فاصله تلاش بعدی را
/// دو برابر می‌کند تا سقف تعیین‌شده در تنظیمات؛ بازگشت به حالت عادی با نخستین اجرای موفق.
/// </summary>
public static class RateBackoffPolicy
{
    /// <summary>بیشترین تعداد دفعات دوبرابر شدن (محافظت از سرریز).</summary>
    private const int MaxDoublings = 10;

    /// <summary>تأخیر بعدی بر پایه شمار خطاهای پیاپی.</summary>
    public static TimeSpan NextDelay(int consecutiveFailures, TimeSpan baseInterval, TimeSpan maxBackoff)
    {
        if (baseInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(baseInterval));
        }

        if (maxBackoff < baseInterval)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBackoff), "سقف عقب‌نشینی باید دست‌کم به اندازه بازه پایه باشد.");
        }

        if (consecutiveFailures <= 0)
        {
            return baseInterval;
        }

        var doublings = Math.Min(consecutiveFailures, MaxDoublings);
        var seconds = baseInterval.TotalSeconds * Math.Pow(2, doublings);
        var capped = Math.Min(seconds, maxBackoff.TotalSeconds);

        return TimeSpan.FromSeconds(capped);
    }

    /// <summary>آیا این وضعیت خطا، «خطای پیاپی» برای عقب‌نشینی محسوب می‌شود؟</summary>
    public static bool IsFailure(RateFetchStatus status) => status switch
    {
        RateFetchStatus.Success => false,
        RateFetchStatus.NotConfigured => false,
        RateFetchStatus.SkippedOverlap => false,
        RateFetchStatus.SkippedTooSoon => false,
        _ => true,
    };
}
