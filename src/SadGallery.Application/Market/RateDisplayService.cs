using System.Globalization;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Text;
using SadGallery.Domain.Enums;

namespace SadGallery.Application.Market;

/// <summary>یک ردیف نمایش نرخ.</summary>
public sealed record RateDisplayItem(
    string AssetCode,
    string Title,
    string AmountText,
    string UnitText,
    RateQuality Quality,
    string QualityLabel,
    DateTimeOffset QuotedAtUtc,
    string QuotedAtText);

/// <summary>مدل آماده نمایش نرخ‌ها در صفحه عمومی.</summary>
public sealed record RateDisplayModel(
    IReadOnlyList<RateDisplayItem> Items,
    DateTimeOffset? LastFetchedUtc,
    string? ProviderId,
    bool IsSampleData,
    RateQuality? OverallQuality,
    string? Notice)
{
    /// <summary>مدل خالی با توضیح فارسی (fail-closed: بدون داده، بدون عدد ساختگی).</summary>
    public static RateDisplayModel Empty(string notice) =>
        new([], null, null, false, null, notice);
}

/// <summary>
/// سرویس نمایش نرخ برای صفحه‌های عمومی.
/// مسیر سریع: خواندن از کش درون‌فرایندی (بدون کوئری دیتابیس).
/// مسیر سرد (پس از راه‌اندازی مجدد برنامه): یک بار بازخوانی از دیتابیس و پر کردن کش.
/// در هر حالت، تازگی نرخ‌ها در «لحظه نمایش» بازمحاسبه می‌شود (نه زمان ثبت).
/// </summary>
public sealed class RateDisplayService
{
    private readonly RateSnapshotCache _cache;
    private readonly IRateStore _store;
    private readonly IClock _clock;
    private readonly RateFreshnessPolicy _freshness;
    private readonly TimeSpan _cacheTtl;

    /// <summary>ساخت سرویس نمایش.</summary>
    public RateDisplayService(RateSnapshotCache cache, IRateStore store, IClock clock, RateFreshnessPolicy freshness, RateOptions options)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(freshness);
        ArgumentNullException.ThrowIfNull(options);

        _cache = cache;
        _store = store;
        _clock = clock;
        _freshness = freshness;
        _cacheTtl = TimeSpan.FromSeconds(options.CacheTtlSeconds);
        ProviderIsDisabled = options.IsDisabled;
    }

    /// <summary>منبع نرخ غیرفعال است (نمایش پیام راهنما به‌جای عدد).</summary>
    public bool ProviderIsDisabled { get; }

    /// <summary>ساخت مدل نمایش.</summary>
    public async Task<RateDisplayModel> GetAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var snapshot = _cache.Get();

        if (snapshot is null)
        {
            snapshot = await TryLoadFromStoreAsync(cancellationToken).ConfigureAwait(false);

            if (snapshot is null)
            {
                return RateDisplayModel.Empty(ProviderIsDisabled
                    ? "منبع نرخ هنوز فعال نشده است؛ تا آن زمان هیچ نرخی نمایش داده نمی‌شود (نمایش عدد ساختگی ممنوع است)."
                    : "نرخ‌های بازار در حال حاضر در دسترس نیست. آخرین نرخ ثبت‌شده به‌زودی نمایش داده می‌شود.");
            }
        }

        var items = new List<RateDisplayItem>();
        var qualities = new List<RateQuality>();

        foreach (var rate in snapshot.Publishable)
        {
            if (rate.IsAnomalySuspected)
            {
                continue;
            }

            var quality = _freshness.Evaluate(rate.QuotedAtUtc, now);

            if (quality == RateQuality.Invalid)
            {
                continue;
            }

            items.Add(new RateDisplayItem(
                rate.AssetCode,
                rate.Title,
                RateFormat.Amount(rate.Amount),
                RateFormat.Unit(rate.QuoteUnit),
                quality,
                RateFormat.QualityLabel(quality),
                rate.QuotedAtUtc,
                PersianDate.ToJalaliDateTimeText(rate.QuotedAtUtc)));

            qualities.Add(quality);
        }

        var isSample = string.Equals(snapshot.ProviderId, FixtureRateProvider.Id, StringComparison.OrdinalIgnoreCase);

        if (items.Count == 0)
        {
            return new RateDisplayModel(
                [],
                snapshot.FetchedAtUtc,
                snapshot.ProviderId,
                isSample,
                null,
                "نرخ‌های ثبت‌شده فعلاً معتبر نیستند (کهنه‌تر از حد مجاز). تا دریافت نرخ تازه، هیچ عددی نمایش داده نمی‌شود.");
        }

        var overall = qualities.Max();

        return new RateDisplayModel(
            items,
            snapshot.FetchedAtUtc,
            snapshot.ProviderId,
            isSample,
            overall,
            BuildNotice(isSample, overall, snapshot.FetchedAtUtc));
    }

    private string? BuildNotice(bool isSample, RateQuality overall, DateTimeOffset fetchedAtUtc)
    {
        if (isSample)
        {
            return "داده‌های این بخش «نمونهٔ آزمایشی» است (منبع Fixture در محیط توسعه) و نرخ روز بازار نیست.";
        }

        if (overall >= RateQuality.Stale)
        {
            return $"ارتباط با منبع نرخ قطع است؛ آنچه می‌بینید «آخرین نرخ ثبت‌شده» در {PersianDate.ToJalaliDateTimeText(fetchedAtUtc)} است.";
        }

        return null;
    }

    private async Task<RateCacheSnapshot?> TryLoadFromStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rates = await _store.GetLatestAsync(cancellationToken).ConfigureAwait(false);

            if (rates.Count == 0)
            {
                return null;
            }

            var publishable = rates.Where(rate => !rate.IsAnomalySuspected).ToArray();
            var flagged = rates.Where(rate => rate.IsAnomalySuspected).ToArray();
            var providerId = publishable.Length > 0 ? publishable[0].ProviderId : FirstProviderId(rates);
            var fetchedAt = rates.Max(rate => rate.FetchedAtUtc);

            var snapshot = new RateCacheSnapshot(providerId, publishable, flagged, fetchedAt);
            _cache.Set(providerId, publishable, flagged, fetchedAt);

            return snapshot;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // دیتابیس در دسترس نیست: صفحه عمومی نباید خطا بدهد؛ پیام «در دسترس نیست» نمایش می‌یابد.
            return null;
        }
    }

    private static string FirstProviderId(IReadOnlyList<NormalizedRate> rates) => rates[0].ProviderId;

    /// <summary>عمر کش برای سنجش تازگی مسیر سریع (تست‌ها و پایش).</summary>
    public bool IsCacheExpired() => _cache.IsExpired(_clock.UtcNow, _cacheTtl);
}

/// <summary>قالب‌بندی افقی مقادیر بازار (نمایش — بدون تبدیل واحد).</summary>
public static class RateFormat
{
    /// <summary>عدد با ارقام فارسی و جداکننده هزارگان؛ اعشار فقط در صورت وجود.</summary>
    public static string Amount(decimal amount)
    {
        var text = amount == Math.Truncate(amount)
            ? amount.ToString("N0", CultureInfo.InvariantCulture)
            : amount.ToString("N2", CultureInfo.InvariantCulture);

        return PersianText.ToPersianDigits(text)
            .Replace(",", "٬", StringComparison.Ordinal)
            .Replace(".", "٫", StringComparison.Ordinal);
    }

    /// <summary>نام واحد پول (هرگز تبدیل نمی‌شود — ADR-0009).</summary>
    public static string Unit(CurrencyUnit unit) => unit switch
    {
        CurrencyUnit.Irt => "تومان",
        CurrencyUnit.Irr => "ریال",
        CurrencyUnit.Usd => "دلار",
        CurrencyUnit.Usdt => "تتر",
        _ => "واحد نامشخص",
    };

    /// <summary>برچسب وضعیت اعتبار برای نمایش.</summary>
    public static string QualityLabel(RateQuality quality) => quality switch
    {
        RateQuality.Live => "لحظه‌ای",
        RateQuality.Delayed => "تأخیری",
        RateQuality.Stale => "آخرین نرخ ثبت‌شده",
        _ => "نامعتبر",
    };
}
