namespace SadGallery.Infrastructure.Persistence.Entities;

/// <summary>
/// «اجاره» تک‌ردیفی برای جلوگیری از اجرای هم‌زمان دریافت نرخ در چند نمونه از برنامه.
/// تنها یک ردیف با شناسه ۱ وجود دارد؛ هر نمونه با یک UPDATE شرطی آن را تصاحب می‌کند و
/// اگر <see cref="ExpiresAtUtc"/> گذشته باشد (نمونه قبلی کرش کرده) خودکار آزاد می‌شود.
/// </summary>
public class MarketFetchLease
{
    /// <summary>همیشه ۱ (رکورد تک‌تایی).</summary>
    public int Id { get; set; }

    /// <summary>شناسه دارنده فعلی (نام ماشین:شناسه فرایند) یا تهی.</summary>
    public string? OwnerId { get; set; }

    /// <summary>زمان تصاحب (UTC).</summary>
    public DateTimeOffset AcquiredAtUtc { get; set; }

    /// <summary>انقضای تصاحب (UTC) — محافظت از قفل‌ماندگی پس از کرش.</summary>
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
