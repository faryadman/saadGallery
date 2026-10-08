using SadGallery.Application.Abstractions;
using SadGallery.Application.Text;
using SadGallery.Domain.Enums;

namespace SadGallery.Application.Market;

/// <summary>یک نقطه تاریخچه برای نمایش/نمودار.</summary>
public sealed record RateHistoryPoint(
    DateTimeOffset QuotedAtUtc,
    string QuotedAtText,
    decimal Amount,
    string AmountText,
    string UnitText,
    RateQuality Quality);

/// <summary>مدل تاریخچه یک دارایی (صفحه ویژه اعضا).</summary>
public sealed record RateHistoryModel(
    string AssetCode,
    string Title,
    string UnitText,
    int Days,
    IReadOnlyList<RateHistoryPoint> Points,
    string? Notice)
{
    /// <summary>کمترین/بیشترین مقدار در بازه (برای نمودار و خلاصه).</summary>
    public decimal? Min => Points.Count == 0 ? null : Points.Min(p => p.Amount);

    /// <summary>بیشترین مقدار در بازه.</summary>
    public decimal? Max => Points.Count == 0 ? null : Points.Max(p => p.Amount);
}

/// <summary>
/// تاریخچه گسترده نرخ‌ها — فقط برای اعضا (Policy سرور: MemberFeatures).
/// محدودیت‌های سرور (ضد سوءاستفاده): بازه ۱ تا ۹۰ روز و حداکثر ۵۰۰ نقطه در پاسخ.
/// </summary>
public sealed class RateHistoryService
{
    /// <summary>حداکثر بازه مجاز (روز).</summary>
    public const int MaxDays = 90;

    /// <summary>حداکثر تعداد نقطه در پاسخ.</summary>
    public const int MaxPoints = 500;

    private readonly IRateStore _store;
    private readonly IClock _clock;
    private readonly RateFreshnessPolicy _freshness;

    /// <summary>ساخت سرویس.</summary>
    public RateHistoryService(IRateStore store, IClock clock, RateFreshnessPolicy freshness)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(freshness);

        _store = store;
        _clock = clock;
        _freshness = freshness;
    }

    /// <summary>تاریخچه یک دارایی در بازه روزهای اخیر (جدیدترین اول).</summary>
    public async Task<RateHistoryModel> GetAsync(string assetCode, int days, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetCode);

        var safeDays = Math.Clamp(days, 1, MaxDays);
        var definition = Domain.Market.AssetCatalog.FindByCode(assetCode);

        if (definition is null)
        {
            return new RateHistoryModel(assetCode, assetCode, "واحد نامشخص", safeDays, [],
                "کد دارایی ناشناخته است؛ تاریخچه‌ای نمایش داده نمی‌شود.");
        }

        var now = _clock.UtcNow;
        var from = now.AddDays(-safeDays);

        IReadOnlyList<NormalizedRate> rates;

        try
        {
            rates = await _store
                .GetHistoryAsync(assetCode, from, now, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // قطع دیتابیس نباید صفحه اعضا را ۵۰۰ کند؛ پیام صریح و بدون عدد نمایش داده می‌شود.
            return new RateHistoryModel(definition.Code, definition.Title, RateFormat.Unit(definition.QuoteUnit), safeDays, [],
                "دیتابیس در دسترس نیست؛ تاریخچه فعلاً نمایش داده نمی‌شود.");
        }

        if (rates.Count == 0)
        {
            return new RateHistoryModel(definition.Code, definition.Title, RateFormat.Unit(definition.QuoteUnit), safeDays, [],
                $"در {PersianText.ToPersianDigits(safeDays.ToString(System.Globalization.CultureInfo.InvariantCulture))} روز گذشته نرخی برای این دارایی ثبت نشده است.");
        }

        var points = rates
            .OrderByDescending(rate => rate.QuotedAtUtc)
            .Take(MaxPoints)
            .Select(rate => new RateHistoryPoint(
                rate.QuotedAtUtc,
                PersianDate.ToJalaliDateTimeText(rate.QuotedAtUtc),
                rate.Amount,
                RateFormat.Amount(rate.Amount),
                RateFormat.Unit(rate.QuoteUnit),
                _freshness.Evaluate(rate.QuotedAtUtc, now)))
            .ToArray();

        var notice = rates.Count > MaxPoints
            ? $"فقط {PersianText.ToPersianDigits(MaxPoints.ToString(System.Globalization.CultureInfo.InvariantCulture))} نقطه آخر نمایش داده می‌شود."
            : null;

        return new RateHistoryModel(definition.Code, definition.Title, RateFormat.Unit(definition.QuoteUnit), safeDays, points, notice);
    }
}
