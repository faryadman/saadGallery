namespace SadGallery.Application.Market;

/// <summary>عکس فوری نرخ‌ها در حافظه (منبع نمایش صفحه).</summary>
public sealed record RateCacheSnapshot(
    string ProviderId,
    IReadOnlyList<NormalizedRate> Publishable,
    IReadOnlyList<NormalizedRate> Flagged,
    DateTimeOffset FetchedAtUtc);

/// <summary>
/// کش درون‌فرایندی آخرین مجموعه نرخ. نقش: پاسخ سریع صفحه‌های عمومی بدون کوئری دیتابیس
/// (معیار پذیرش فاز ۲: زیر ۵۰ms و بدون کوئری اضافی).
/// <para>
/// نگه‌داشت «آخرین مجموعه معتبر» به‌صورت اتمیک انجام می‌شود؛ در صورت خرابی منبع،
/// همین مجموعه با برچسب کهنگی نمایش داده می‌شود (سیستم خراب نمی‌شود).
/// </para>
/// </summary>
public sealed class RateSnapshotCache
{
    private readonly object _gate = new();
    private RateCacheSnapshot? _snapshot;

    /// <summary>جایگزینی اتمیک مجموعه نرخ.</summary>
    public void Set(string providerId, IReadOnlyList<NormalizedRate> publishable, IReadOnlyList<NormalizedRate> flagged, DateTimeOffset fetchedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(publishable);
        ArgumentNullException.ThrowIfNull(flagged);

        var snapshot = new RateCacheSnapshot(providerId, publishable, flagged, fetchedAtUtc);

        lock (_gate)
        {
            _snapshot = snapshot;
        }
    }

    /// <summary>آخرین مجموعه (ممکن است کهنه باشد) یا <c>null</c> اگر هیچ‌گاه پر نشده است.</summary>
    public RateCacheSnapshot? Get()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    /// <summary>آیا کش هرگز پر نشده است؟</summary>
    public bool IsEmpty => Get() is null;

    /// <summary>عمر کش نسبت به لحظه داده‌شده (یا <c>null</c> اگر خالی باشد).</summary>
    public TimeSpan? Age(DateTimeOffset nowUtc) => Get() is { } snapshot ? nowUtc - snapshot.FetchedAtUtc : null;

    /// <summary>آیا کش از عمر مفید قابل تنظیم گذشته است؟ (نشانه نیاز به بازخوانی از دیتابیس)</summary>
    public bool IsExpired(DateTimeOffset nowUtc, TimeSpan ttl) =>
        Age(nowUtc) is not { } age || age > ttl;
}
