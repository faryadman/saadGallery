using SadGallery.Application.Market;

namespace SadGallery.Application.Abstractions;

/// <summary>شروع یک اجرای واریز نرخ.</summary>
public sealed record RateFetchRunStart(string ProviderId, string Trigger, DateTimeOffset StartedAtUtc);

/// <summary>نتیجه نهایی اجرا.</summary>
public enum RateFetchOutcomeKind
{
    /// <summary>همه اقلام پذیرفته و منتشر شدند.</summary>
    Success = 1,

    /// <summary>اجرا انجام شد اما بخشی از اقلام رد/علامت‌دار شدند.</summary>
    SuccessWithProblems = 2,

    /// <summary>اجرا شکست خورد (منبع در دسترس نبود/پاسخ نامعتبر بود).</summary>
    Failed = 3,

    /// <summary>به دلیل اجرای هم‌زمان (درون‌فرایندی یا چند-نمونه‌ای) این نوبت انجام نشد.</summary>
    SkippedOverlap = 4,

    /// <summary>نتیجه دریافت شد اما ثبت در دیتابیس ممکن نشد (دیتابیس در دسترس نبود).</summary>
    PersistenceFailed = 5,
}

/// <summary>اطلاعات پایان اجرا.</summary>
public sealed record RateFetchRunFinish(
    RateFetchOutcomeKind Outcome,
    DateTimeOffset FinishedAtUtc,
    int DurationMs,
    int AcceptedCount,
    int FlaggedCount,
    int RejectedCount,
    int? HttpStatusCode,
    string? ErrorCode,
    string? Notes);

/// <summary>
/// مخزن نرخ‌ها: تاریخچه، آخرین مقدار، رکورد اجراها و قفل چند-نمونه‌ای.
/// پیاده‌سازی در لایه Infrastructure روی SQL Server است.
/// </summary>
public interface IRateStore
{
    /// <summary>ثبت شروع اجرا؛ شناسه ردیف برمی‌گرداند.</summary>
    Task<long> BeginRunAsync(RateFetchRunStart start, CancellationToken cancellationToken);

    /// <summary>تکمیل رکورد اجرا.</summary>
    Task CompleteRunAsync(long runId, RateFetchRunFinish finish, CancellationToken cancellationToken);

    /// <summary>ثبت نرخ‌ها (قابل انتشار و علامت‌دار) برای یک اجرا.</summary>
    Task SaveRatesAsync(long runId, IReadOnlyList<NormalizedRate> rates, CancellationToken cancellationToken);

    /// <summary>
    /// آخرین نرخِ هر دارایی. تعریف پروژه: بالاترین شناسه رکورد در هر دارایی
    /// (نرخ‌ها فقط افزودنی‌اند؛ بازنویسی تاریخچه ممنوع است) و بدون رکوردهای جهش‌مشکوک.
    /// </summary>
    Task<IReadOnlyList<NormalizedRate>> GetLatestAsync(CancellationToken cancellationToken);

    /// <summary>تاریخچه یک دارایی در بازه مشخص.</summary>
    Task<IReadOnlyList<NormalizedRate>> GetHistoryAsync(string assetCode, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);

    /// <summary>دریافت قفل اجرای چند-نمونه‌ای (اجاره زمانی). false = نمونه دیگری در حال اجراست.</summary>
    Task<bool> TryAcquireLeaseAsync(string ownerId, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>آزادسازی قفل (در صورت انقضا یا خرابی، خودکار آزاد می‌شود).</summary>
    Task ReleaseLeaseAsync(string ownerId, CancellationToken cancellationToken);
}
